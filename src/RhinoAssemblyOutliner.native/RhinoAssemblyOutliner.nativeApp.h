// RhinoAssemblyOutliner.nativeApp.h : MFC DLL entry point

#pragma once

#include "resource.h"

class CRhinoAssemblyOutliner_nativeApp : public CWinApp
{
public:
	CRhinoAssemblyOutliner_nativeApp() = default;
	BOOL InitInstance() override;
	int ExitInstance() override;
	DECLARE_MESSAGE_MAP()
};
