// NativeApi.cpp : Exported C API implementation

#include "stdafx.h"
#include "NativeApi.h"
#include "VisibilityConduit.h"
#include "DocEventHandler.h"

// Version: increment when API changes (4 = ComponentState enum + conduit improvements)
static const int NATIVE_API_VERSION = 4;

static bool g_initialized = false;
static CVisibilityData* g_pVisData = nullptr;
static CVisibilityConduit* g_pConduit = nullptr;
static CDocEventHandler* g_pDocEventHandler = nullptr;

/// Helper: trigger a document redraw after visibility changes
static void RedrawActiveDoc()
{
	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (pDoc)
		pDoc->Redraw();
}

bool __stdcall NativeInit()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (g_initialized)
		return true;

	g_pVisData = new CVisibilityData();
	g_pConduit = new CVisibilityConduit(*g_pVisData);
	g_pDocEventHandler = new CDocEventHandler(*g_pVisData);
	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (pDoc)
		g_pConduit->Enable(pDoc->RuntimeSerialNumber());

	g_initialized = true;
	return true;
}

void __stdcall NativeCleanup()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized)
		return;

	if (g_pDocEventHandler)
	{
		g_pDocEventHandler->Enable(FALSE);
		delete g_pDocEventHandler;
		g_pDocEventHandler = nullptr;
	}

	if (g_pConduit)
	{
		g_pConduit->Disable();
		delete g_pConduit;
		g_pConduit = nullptr;
	}

	if (g_pVisData)
	{
		delete g_pVisData;
		g_pVisData = nullptr;
	}

	g_initialized = false;
}

bool __stdcall SetComponentVisibility(
	const ON_UUID* instanceId,
	const char* componentPath,
	bool visible)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId || !componentPath || !g_pVisData)
		return false;

	if (visible)
		g_pVisData->SetComponentVisible(*instanceId, componentPath);
	else
		g_pVisData->SetComponentHidden(*instanceId, componentPath);

	RedrawActiveDoc();
	return true;
}

bool __stdcall IsComponentVisible(
	const ON_UUID* instanceId,
	const char* componentPath)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId || !componentPath || !g_pVisData)
		return true;

	return !g_pVisData->IsComponentHidden(*instanceId, componentPath);
}

int __stdcall GetHiddenComponentCount(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId || !g_pVisData)
		return 0;

	return g_pVisData->GetHiddenCount(*instanceId);
}

void __stdcall ResetComponentVisibility(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId || !g_pVisData)
		return;

	g_pVisData->ResetInstance(*instanceId);
	RedrawActiveDoc();
}

void __stdcall SetDebugLogging(bool enabled)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (g_pConduit)
		g_pConduit->SetDebugLogging(enabled);
}

int __stdcall GetNativeVersion()
{
	return NATIVE_API_VERSION;
}

/// Serialize visibility data to a pipe-separated string for doc user strings.
/// Format: <uuid>|<path>:<state>|<path>:<state>\n per instance
ON_wString SerializeVisibilityState(CVisibilityData& visData)
{
	ON_wString result;
	std::vector<ON_UUID> managedIds;
	visData.GetManagedInstanceIds(managedIds);

	for (const auto& instanceId : managedIds)
	{
		char uuidBuf[64];
		ON_UuidToString(instanceId, uuidBuf);

		ON_wString line;
		line.Format(L"%S", uuidBuf);

		// Get all states for this instance
		CVisibilitySnapshot snap = visData.TakeSnapshot();
		auto it = snap.m_data.find(instanceId);
		if (it == snap.m_data.end() || it->second.states.empty())
			continue;

		for (const auto& pair : it->second.states)
		{
			ON_wString entry;
			entry.Format(L"|%S:%d", pair.first.c_str(), (int)pair.second);
			line += entry;
		}

		result += line;
		result += L"\n";
	}
	return result;
}

/// Deserialize visibility data from doc user string format
void DeserializeVisibilityState(const ON_wString& data, CVisibilityData& visData)
{
	if (data.IsEmpty())
		return;

	ON_String utf8(data);
	const char* p = static_cast<const char*>(utf8);
	std::string str(p);

	size_t lineStart = 0;
	while (lineStart < str.size())
	{
		size_t lineEnd = str.find('\n', lineStart);
		if (lineEnd == std::string::npos)
			lineEnd = str.size();

		std::string line = str.substr(lineStart, lineEnd - lineStart);
		lineStart = lineEnd + 1;

		if (line.empty())
			continue;

		// Parse: <uuid>|<path>:<state>|<path>:<state>
		size_t firstPipe = line.find('|');
		if (firstPipe == std::string::npos)
			continue;

		std::string uuidStr = line.substr(0, firstPipe);
		ON_UUID instanceId;
		if (!ON_UuidFromString(uuidStr.c_str(), instanceId))
			continue;

		size_t pos = firstPipe + 1;
		while (pos < line.size())
		{
			size_t nextPipe = line.find('|', pos);
			if (nextPipe == std::string::npos)
				nextPipe = line.size();

			std::string entry = line.substr(pos, nextPipe - pos);
			pos = nextPipe + 1;

			size_t colon = entry.find(':');
			if (colon == std::string::npos)
				continue;

			std::string path = entry.substr(0, colon);
			int state = std::atoi(entry.substr(colon + 1).c_str());

			if (state >= CS_VISIBLE && state <= CS_TRANSPARENT)
				visData.SetState(instanceId, path.c_str(), static_cast<ComponentState>(state));
		}
	}
}

static const wchar_t* RAO_DOC_KEY = L"RAO_VisibilityState";

void __stdcall PersistVisibilityState()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !g_pVisData)
		return;

	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (!pDoc)
		return;

	ON_wString serialized = SerializeVisibilityState(*g_pVisData);
	pDoc->SetDocTextString(RAO_DOC_KEY, serialized);
}

void __stdcall LoadVisibilityState()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !g_pVisData)
		return;

	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (!pDoc)
		return;

	ON_wString serialized;
	pDoc->GetDocTextString(RAO_DOC_KEY, serialized);
	DeserializeVisibilityState(serialized, *g_pVisData);
}

int __stdcall GetManagedInstances(ON_UUID* buffer, int maxCount)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !g_pVisData)
		return 0;

	std::vector<ON_UUID> ids;
	g_pVisData->GetManagedInstanceIds(ids);

	int count = static_cast<int>(ids.size());
	if (buffer && maxCount > 0)
	{
		int toCopy = (count < maxCount) ? count : maxCount;
		for (int i = 0; i < toCopy; i++)
			buffer[i] = ids[i];
	}

	return count;
}

bool __stdcall IsConduitEnabled()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_pConduit)
		return false;

	return g_pConduit->IsEnabled() ? true : false;
}

bool __stdcall SetComponentState(
	const ON_UUID* instanceId,
	const char* path,
	int state)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId || !path || !g_pVisData)
		return false;

	if (state < CS_VISIBLE || state > CS_TRANSPARENT)
		return false;

	g_pVisData->SetState(*instanceId, path, static_cast<ComponentState>(state));
	RedrawActiveDoc();
	return true;
}

int __stdcall GetComponentState(
	const ON_UUID* instanceId,
	const char* path)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId || !path || !g_pVisData)
		return CS_VISIBLE;

	return static_cast<int>(g_pVisData->GetState(*instanceId, path));
}
