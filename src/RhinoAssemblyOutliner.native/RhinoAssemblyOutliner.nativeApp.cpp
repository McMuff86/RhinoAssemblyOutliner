// RhinoAssemblyOutliner.nativeApp.cpp : MFC DLL entry point

#include "stdafx.h"
#include "RhinoAssemblyOutliner.nativeApp.h"

BEGIN_MESSAGE_MAP(CRhinoAssemblyOutliner_nativeApp, CWinApp)
END_MESSAGE_MAP()

CRhinoAssemblyOutliner_nativeApp theApp;

BOOL CRhinoAssemblyOutliner_nativeApp::InitInstance()
{
	CWinApp::InitInstance();
	return TRUE;
}

int CRhinoAssemblyOutliner_nativeApp::ExitInstance()
{
	return CWinApp::ExitInstance();
}
