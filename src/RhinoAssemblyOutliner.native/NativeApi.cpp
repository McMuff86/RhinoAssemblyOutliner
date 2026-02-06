// NativeApi.cpp : Exported C API implementation

#include "stdafx.h"
#include "NativeApi.h"
#include "VisibilityConduit.h"

// Version: increment when API changes (2 = path-based API)
static const int NATIVE_API_VERSION = 2;

static bool g_initialized = false;
static CVisibilityData* g_pVisData = nullptr;
static CVisibilityConduit* g_pConduit = nullptr;

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
	g_pConduit->Enable(RhinoApp().ActiveDoc()->RuntimeSerialNumber());

	g_initialized = true;
	return true;
}

void __stdcall NativeCleanup()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized)
		return;

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
