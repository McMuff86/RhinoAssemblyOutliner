// DocEventHandler.cpp : Document event handler implementation

#include "stdafx.h"
#include "DocEventHandler.h"
#include "VisibilityUserData.h"

CDocEventHandler::CDocEventHandler(CVisibilityData& visData)
	: m_visData(visData)
{
	Register();
	Enable(TRUE);
}

void CDocEventHandler::OnEndOpenDocument(CRhinoDoc& doc, const wchar_t* filename, BOOL bMerge, BOOL bReference)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	// After doc loads, find all instance objects with our UserData and sync
	CRhinoObjectIterator it(doc, CRhinoObjectIterator::normal_objects);
	it.SetObjectFilter(ON::instance_reference);

	const CRhinoObject* pObject = nullptr;
	while ((pObject = it.Next()) != nullptr)
	{
		if (pObject->ObjectType() != ON::instance_reference)
			continue;

		const ON_UUID instanceId = pObject->Attributes().m_uuid;

		// Check for our userdata on the object's attributes
		CComponentVisibilityData* pUD = CComponentVisibilityData::Cast(
			pObject->Attributes().GetUserData(VisibilityUserDataId));

		if (pUD && !pUD->HiddenPaths.empty())
		{
			pUD->SyncToVisData(instanceId, m_visData);
		}
	}
}

void CDocEventHandler::OnBeginSaveDocument(CRhinoDoc& doc, const wchar_t* filename, BOOL bExportSelected)
{
	AFX_MANAGE_STATE(AfxGetStaticModuleState());

	// Before save, persist CVisibilityData to UserData on each managed instance
	std::vector<ON_UUID> managedIds;
	m_visData.GetManagedInstanceIds(managedIds);

	for (const auto& instanceId : managedIds)
	{
		const CRhinoObject* pObject = doc.LookupObject(instanceId);
		if (!pObject || pObject->ObjectType() != ON::instance_reference)
			continue;

		// Attach UserData to object attributes for persistence
		ON_3dmObjectAttributes newAttrs = pObject->Attributes();

		// Remove existing UD if present
		CComponentVisibilityData* pExisting = CComponentVisibilityData::Cast(
			newAttrs.GetUserData(VisibilityUserDataId));
		if (pExisting)
		{
			newAttrs.DetachUserData(pExisting);
			delete pExisting;
		}

		// Create and populate new UD
		CComponentVisibilityData* pUD = new CComponentVisibilityData();
		pUD->SyncFromVisData(instanceId, m_visData);

		if (!pUD->HiddenPaths.empty())
		{
			if (!newAttrs.AttachUserData(pUD))
				delete pUD;
		}
		else
		{
			delete pUD;
		}

		doc.ModifyObjectAttributes(CRhinoObjRef(pObject), newAttrs);
	}
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
