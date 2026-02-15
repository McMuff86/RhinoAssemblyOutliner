// NativeApi.cpp : Exported C API implementation

#include "stdafx.h"
#include "NativeApi.h"
#include "VisibilityConduit.h"
#include "DocEventHandler.h"
#include "VisibilityUserData.h"

// Version: increment when API changes (3 = persistence + extended API)
static const int NATIVE_API_VERSION = 3;

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
	g_pConduit->Enable(RhinoApp().ActiveDoc()->RuntimeSerialNumber());

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

void __stdcall PersistVisibilityState()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !g_pVisData)
		return;

	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (!pDoc)
		return;

	std::vector<ON_UUID> managedIds;
	g_pVisData->GetManagedInstanceIds(managedIds);

	for (const auto& instanceId : managedIds)
	{
		const CRhinoObject* pObject = pDoc->LookupObject(instanceId);
		if (!pObject || pObject->ObjectType() != ON::instance_reference)
			continue;

		// Attach UserData to the object's attributes for persistence
		ON_3dmObjectAttributes newAttrs = pObject->Attributes();

		// Remove existing visibility userdata if present
		CComponentVisibilityData* pExisting = CComponentVisibilityData::Cast(
			newAttrs.GetUserData(VisibilityUserDataId));
		if (pExisting)
		{
			newAttrs.DetachUserData(pExisting);
			delete pExisting;
		}

		CComponentVisibilityData* pUD = new CComponentVisibilityData();
		pUD->SyncFromVisData(instanceId, *g_pVisData);

		if (!pUD->HiddenPaths.empty())
		{
			if (!newAttrs.AttachUserData(pUD))
				delete pUD;
		}
		else
		{
			delete pUD;
		}

		pDoc->ModifyObjectAttributes(CRhinoObjRef(pObject), newAttrs);
	}
}

void __stdcall LoadVisibilityState()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !g_pVisData)
		return;

	CRhinoDoc* pDoc = RhinoApp().ActiveDoc();
	if (!pDoc)
		return;

	CRhinoObjectIterator it(*pDoc, CRhinoObjectIterator::normal_objects);
	it.SetObjectFilter(ON::instance_reference);

	const CRhinoObject* pObject = nullptr;
	while ((pObject = it.Next()) != nullptr)
	{
		if (pObject->ObjectType() != ON::instance_reference)
			continue;

		const ON_UUID instanceId = pObject->Attributes().m_uuid;
		CComponentVisibilityData* pUD = CComponentVisibilityData::Cast(
			pObject->Attributes().GetUserData(VisibilityUserDataId));

		if (pUD && !pUD->HiddenPaths.empty())
		{
			pUD->SyncToVisData(instanceId, *g_pVisData);
		}
	}
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
