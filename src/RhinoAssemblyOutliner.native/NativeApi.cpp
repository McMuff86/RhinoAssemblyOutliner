// NativeApi.cpp : Exported C API implementation

#include "stdafx.h"
#include "NativeApi.h"
#include "VisibilityConduit.h"
#include "DocEventHandler.h"
#include "Constants.h"
#include "AssemblyUserData.h"

// B4: Validate that System.Guid (C#) and ON_UUID are binary-compatible for P/Invoke.
// Both are 16-byte structs with identical memory layout (Data1/Data2/Data3/Data4).
// C# marshals 'ref Guid' as a pointer, which the C++ side receives as 'const ON_UUID*'.
static_assert(sizeof(ON_UUID) == 16, "ON_UUID must be 16 bytes to match System.Guid layout");

// Version: increment when API changes (5 = ON_AssemblyUserData persistence)
static const int NATIVE_API_VERSION = 5;

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

static CRhinoDoc* ActiveDoc()
{
	return RhinoApp().ActiveDoc();
}

static const CRhinoObject* FindDocObject(const ON_UUID* instanceId)
{
	if (!instanceId)
		return nullptr;

	CRhinoDoc* pDoc = ActiveDoc();
	if (!pDoc)
		return nullptr;

	return pDoc->LookupObject(*instanceId);
}

static const ON_AssemblyUserData* FindAssemblyData(const ON_UUID* instanceId)
{
	const CRhinoObject* pObj = FindDocObject(instanceId);
	if (!pObj)
		return nullptr;

	const ON_UserData* pData = pObj->Attributes().GetUserData(AssemblyUserDataId);
	return ON_AssemblyUserData::Cast(pData);
}

static bool ModifyAttributesWithData(
	const ON_UUID* instanceId,
	const ON_AssemblyUserData* dataToAttach,
	bool removeOnly)
{
	const CRhinoObject* pObj = FindDocObject(instanceId);
	CRhinoDoc* pDoc = ActiveDoc();
	if (!pObj || !pDoc)
		return false;

	CRhinoObjectAttributes attrs = pObj->Attributes();

	if (ON_UserData* existing = attrs.GetUserData(AssemblyUserDataId))
		delete existing;

	if (!removeOnly && dataToAttach)
	{
		ON_AssemblyUserData* copy = new ON_AssemblyUserData(*dataToAttach);
		if (!attrs.AttachUserData(copy))
		{
			delete copy;
			return false;
		}
	}

	return pDoc->ModifyObjectAttributes(CRhinoObjRef(pObj), attrs, true);
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
		ON_UUID instanceId = ON_UuidFromString(uuidStr.c_str());
		if (ON_UuidIsNil(instanceId))
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

void __stdcall PersistVisibilityState()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !g_pVisData)
		return;

	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (!pDoc)
		return;

	ON_wString serialized = SerializeVisibilityState(*g_pVisData);
	pDoc->SetUserString(RAO_DOC_KEY, serialized);
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
	pDoc->GetUserString(RAO_DOC_KEY, serialized);
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

bool __stdcall AttachAssemblyData(
	const ON_UUID* instanceId,
	const ON_UUID* sourceDefId,
	const wchar_t* sourceDefName,
	const int* hiddenIndices,
	int hiddenCount,
	int componentCount)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!instanceId || !sourceDefId || hiddenCount < 0 || componentCount < 0)
		return false;

	ON_AssemblyUserData data;
	data.m_sourceDefinitionId = *sourceDefId;
	data.m_sourceDefinitionName = sourceDefName ? sourceDefName : L"";
	data.m_componentCount = componentCount;
	data.m_hiddenComponentIndices.Empty();

	for (int i = 0; i < hiddenCount; ++i)
	{
		if (!hiddenIndices)
			return false;
		data.m_hiddenComponentIndices.Append(hiddenIndices[i]);
	}

	return ModifyAttributesWithData(instanceId, &data, false);
}

bool __stdcall HasAssemblyData(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());
	return FindAssemblyData(instanceId) != nullptr;
}

bool __stdcall RemoveAssemblyData(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());
	return ModifyAttributesWithData(instanceId, nullptr, true);
}

bool __stdcall GetSourceDefinitionId(
	const ON_UUID* instanceId,
	ON_UUID* outSourceDefId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!outSourceDefId)
		return false;

	const ON_AssemblyUserData* pData = FindAssemblyData(instanceId);
	if (!pData)
		return false;

	*outSourceDefId = pData->m_sourceDefinitionId;
	return true;
}

int __stdcall GetSourceDefinitionName(
	const ON_UUID* instanceId,
	wchar_t* buffer,
	int bufferSize)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	const ON_AssemblyUserData* pData = FindAssemblyData(instanceId);
	if (!pData)
		return -1;

	const int required = pData->m_sourceDefinitionName.Length();
	if (!buffer || bufferSize <= 0)
		return required;

	wcsncpy_s(buffer, static_cast<size_t>(bufferSize), pData->m_sourceDefinitionName, _TRUNCATE);
	return required;
}

int __stdcall GetHiddenComponentIndices(
	const ON_UUID* instanceId,
	int* buffer,
	int maxCount)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	const ON_AssemblyUserData* pData = FindAssemblyData(instanceId);
	if (!pData)
		return -1;

	const int count = pData->m_hiddenComponentIndices.Count();
	if (buffer && maxCount > 0)
	{
		const int toCopy = (count < maxCount) ? count : maxCount;
		for (int i = 0; i < toCopy; ++i)
			buffer[i] = pData->m_hiddenComponentIndices[i];
	}

	return count;
}

int __stdcall GetAssemblyComponentCount(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	const ON_AssemblyUserData* pData = FindAssemblyData(instanceId);
	if (!pData)
		return -1;

	return pData->m_componentCount;
}
