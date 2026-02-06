// NativeApi.cpp : Exported C API implementation

#include "stdafx.h"
#include "NativeApi.h"

// Version: increment when API changes
static const int NATIVE_API_VERSION = 1;

static bool g_initialized = false;

bool __stdcall NativeInit()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (g_initialized)
		return true;

	// TODO: Create and enable CRhinoDisplayConduit here
	g_initialized = true;
	return true;
}

void __stdcall NativeCleanup()
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized)
		return;

	// TODO: Disable and destroy CRhinoDisplayConduit here
	g_initialized = false;
}

bool __stdcall SetComponentVisibility(
	const ON_UUID* instanceId,
	int componentIndex,
	bool visible)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId)
		return false;

	// TODO: Implement with CRhinoDisplayConduit + visibility data
	return false;
}

bool __stdcall IsComponentVisible(
	const ON_UUID* instanceId,
	int componentIndex)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId)
		return true;

	// TODO: Query visibility data
	return true;
}

int __stdcall GetHiddenComponentCount(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId)
		return 0;

	// TODO: Query visibility data
	return 0;
}

void __stdcall ResetComponentVisibility(const ON_UUID* instanceId)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (!g_initialized || !instanceId)
		return;

	// TODO: Clear visibility data and redraw
}

int __stdcall GetNativeVersion()
{
	return NATIVE_API_VERSION;
}
