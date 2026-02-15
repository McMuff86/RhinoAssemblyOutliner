// DocEventHandler.cpp : Document event handler implementation
// Uses document-level user strings (key: RAO_VisibilityState) for persistence.
// Delegates serialization to NativeApi's PersistVisibilityState/LoadVisibilityState.

#include "stdafx.h"
#include "DocEventHandler.h"

// Shared doc key — must match NativeApi.cpp
static const wchar_t* RAO_DOC_KEY = L"RAO_VisibilityState";

// Forward declarations for serialization helpers in NativeApi.cpp
extern ON_wString SerializeVisibilityState(CVisibilityData& visData);
extern void DeserializeVisibilityState(const ON_wString& data, CVisibilityData& visData);

CDocEventHandler::CDocEventHandler(CVisibilityData& visData)
	: m_visData(visData)
{
	Register();
	Enable(TRUE);
}

void CDocEventHandler::OnEndOpenDocument(CRhinoDoc& doc, const wchar_t* filename, BOOL bMerge, BOOL bReference)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	// Read visibility state from document user strings
	ON_wString serialized;
	doc.GetDocTextString(RAO_DOC_KEY, serialized);
	DeserializeVisibilityState(serialized, m_visData);
}

void CDocEventHandler::OnBeginSaveDocument(CRhinoDoc& doc, const wchar_t* filename, BOOL bExportSelected)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	// Serialize visibility state to document user strings
	ON_wString serialized = SerializeVisibilityState(m_visData);
	doc.SetDocTextString(RAO_DOC_KEY, serialized);
}

void CDocEventHandler::OnCloseDocument(CRhinoDoc& doc)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());
	m_visData.ClearAll();
}

void CDocEventHandler::OnDeleteObject(CRhinoDoc& doc, CRhinoObject& object)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	if (object.ObjectType() != ON::instance_reference)
		return;

	const ON_UUID instanceId = object.Attributes().m_uuid;
	if (m_visData.IsManaged(instanceId))
	{
		m_visData.ResetInstance(instanceId);
	}
}
