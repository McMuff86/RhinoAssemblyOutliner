// RhinoAssemblyOutliner.native.h : main header file for the RhinoAssemblyOutliner.native DLL.
//

#pragma once


#include "resource.h" // main symbols

// CRhinoAssemblyOutliner_nativeApp
// See RhinoAssemblyOutliner.nativeApp.cpp for the implementation of this class
//

class CRhinoAssemblyOutliner_nativeApp : public CWinApp
{
public:
  // CRITICAL: DO NOT CALL RHINO SDK FUNCTIONS HERE!
  // Only standard MFC DLL instance construction belongs here. 
  // All other significant initialization should take place in
  // CRhinoAssemblyOutliner_nativePlugIn::OnLoadPlugIn().
	CRhinoAssemblyOutliner_nativeApp() = default;

  // CRITICAL: DO NOT CALL RHINO SDK FUNCTIONS HERE!
  // Only standard MFC DLL instance initialization belongs here. 
  // All other significant initialization should take place in
  // CRhinoAssemblyOutliner_nativePlugIn::OnLoadPlugIn().
	BOOL InitInstance() override;
  
  // CRITICAL: DO NOT CALL RHINO SDK FUNCTIONS HERE!
  // Only standard MFC DLL instance clean up belongs here. 
  // All other significant cleanup should take place in either
  // CRhinoAssemblyOutliner_nativePlugIn::OnSaveAllSettings() or
  // CRhinoAssemblyOutliner_nativePlugIn::OnUnloadPlugIn().  
	int ExitInstance() override;
  
	DECLARE_MESSAGE_MAP()
};
