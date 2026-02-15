// VisibilityConduit.cpp : CRhinoDisplayConduit implementation
//
// Core logic: in SC_DRAWOBJECT, suppress managed instances via
// m_bDrawObject = false, then manually draw only visible components
// using dp.DrawObject() which uses Rhino's own rendering path.
//
// Path-based filtering allows hiding components at any nesting depth.
// E.g. hiding path "1.0" hides the first child of the second component.

#include "stdafx.h"
#include "VisibilityConduit.h"
#include <cstdio>

CVisibilityConduit::CVisibilityConduit(CVisibilityData& visData)
	: CRhinoDisplayConduit(CSupportChannels::SC_DRAWOBJECT)
	, m_visData(visData)
{
}

bool CVisibilityConduit::ExecConduit(
	CRhinoDisplayPipeline& dp,
	UINT nChannel,
	bool& bTerminate)
{
	// We only handle SC_DRAWOBJECT
	if (nChannel != CSupportChannels::SC_DRAWOBJECT)
		return true;

	// Get the object being drawn
	if (!m_pChannelAttrs || !m_pChannelAttrs->m_pObject)
		return true;

	const CRhinoObject* pObject = m_pChannelAttrs->m_pObject;

	// Only intercept instance references (block instances)
	if (pObject->ObjectType() != ON::instance_reference)
		return true;

	// Check if this instance is managed by us (has hidden components)
	const ON_UUID instanceId = pObject->Attributes().m_uuid;
	if (!m_visData.IsManaged(instanceId))
		return true;

	if (m_debugLogging)
	{
		char buf[64];
		ON_UuidToString(instanceId, buf);
		ON_wString msg;
		msg.Format(L"[Conduit] SC_DRAWOBJECT: instance %S, managed=YES, suppressing default draw\n", buf);
		RhinoApp().Print(msg);
	}

	// --- This instance has hidden components: take over drawing ---

	// Suppress the default drawing of this object
	m_pChannelAttrs->m_bDrawObject = false;

	const CRhinoInstanceObject* pInstance =
		static_cast<const CRhinoInstanceObject*>(pObject);
	const CRhinoInstanceDefinition* pDef = pInstance->InstanceDefinition();
	if (!pDef)
		return true;

	ON_Xform instanceXform = pInstance->InstanceXform();

	// Iterate definition components, skip hidden ones using path-based lookup
	int componentCount = pDef->ObjectCount();
	for (int i = 0; i < componentCount; i++)
	{
		std::string path = std::to_string(i);

		// Check if this exact path is hidden
		if (m_visData.IsComponentHidden(instanceId, path.c_str()))
		{
			if (m_debugLogging)
			{
				ON_wString msg;
				msg.Format(L"[Conduit]   component[%d] path=\"%S\" => HIDDEN, skipping\n", i, path.c_str());
				RhinoApp().Print(msg);
			}
			continue;
		}

		const CRhinoObject* pComponent = pDef->Object(i);
		if (!pComponent)
			continue;

		// Skip objects that are hidden in the definition
		if (!pComponent->IsVisible())
			continue;

		if (pComponent->ObjectType() == ON::instance_reference)
		{
			// Nested block instance — check if it has hidden descendants
			const CRhinoInstanceObject* pNestedInstance =
				static_cast<const CRhinoInstanceObject*>(pComponent);

			if (m_visData.HasHiddenDescendants(instanceId, path.c_str()))
			{
				// This sub-block has hidden children, recurse with filtering
				if (m_debugLogging)
				{
					ON_wString msg;
					msg.Format(L"[Conduit]   component[%d] path=\"%S\" => nested block with hidden descendants, recursing\n", i, path.c_str());
					RhinoApp().Print(msg);
				}
				DrawNestedFiltered(dp, pNestedInstance, instanceXform, instanceId, path, 0);
			}
			else
			{
				// No hidden descendants — draw entire sub-block normally
				if (m_debugLogging)
				{
					ON_wString msg;
					msg.Format(L"[Conduit]   component[%d] path=\"%S\" => nested block, no hidden descendants, DrawObject\n", i, path.c_str());
					RhinoApp().Print(msg);
				}
				DrawComponent(dp, pComponent, instanceXform);
			}
		}
		else
		{
			if (m_debugLogging)
			{
				ON_wString msg;
				msg.Format(L"[Conduit]   component[%d] path=\"%S\" type=%d => DrawObject\n", i, path.c_str(), pComponent->ObjectType());
				RhinoApp().Print(msg);
			}
			DrawComponent(dp, pComponent, instanceXform);
		}
	}

	// Selection highlight: if instance is selected, draw yellow wireframe overlay
	if (pObject->IsSelected())
	{
		// Selection highlight: draw visible components again as wireframe
		// using Rhino's selection color
		ON_Color selColor(255, 255, 0); // Yellow selection wireframe

		for (int i = 0; i < componentCount; i++)
		{
			std::string path = std::to_string(i);
			if (m_visData.IsComponentHidden(instanceId, path.c_str()))
				continue;

			const CRhinoObject* pComp = pDef->Object(i);
			if (!pComp || !pComp->IsVisible())
				continue;

			// Draw wireframe edges with selection color
			const ON_Geometry* pGeom = pComp->Geometry();
			if (!pGeom)
				continue;

			const ON_Brep* pBrep = ON_Brep::Cast(pGeom);
			if (pBrep)
			{
				for (int ei = 0; ei < pBrep->m_E.Count(); ei++)
				{
					const ON_BrepEdge& edge = pBrep->m_E[ei];
					ON_Curve* pCrv = edge.DuplicateCurve();
					if (pCrv)
					{
						pCrv->Transform(instanceXform);
						dp.DrawCurve(*pCrv, selColor, 2);
						delete pCrv;
					}
				}
			}

			const ON_Mesh* pMesh = ON_Mesh::Cast(pGeom);
			if (pMesh)
			{
				ON_Mesh meshCopy(*pMesh);
				meshCopy.Transform(instanceXform);
				dp.DrawMeshWires(meshCopy, selColor, 2);
			}

			const ON_Extrusion* pExtr = ON_Extrusion::Cast(pGeom);
			if (pExtr)
			{
				ON_Brep* pBrepFromExtr = pExtr->BrepForm();
				if (pBrepFromExtr)
				{
					for (int ei = 0; ei < pBrepFromExtr->m_E.Count(); ei++)
					{
						const ON_BrepEdge& edge = pBrepFromExtr->m_E[ei];
						ON_Curve* pCrv = edge.DuplicateCurve();
						if (pCrv)
						{
							pCrv->Transform(instanceXform);
							dp.DrawCurve(*pCrv, selColor, 2);
							delete pCrv;
						}
					}
					delete pBrepFromExtr;
				}
			}
		}
	}

	// CRITICAL: return true to continue the pipeline for other objects.
	// return false would abort the ENTIRE frame!
	return true;
}

void CVisibilityConduit::DrawComponent(
	CRhinoDisplayPipeline& dp,
	const CRhinoObject* pComponent,
	const ON_Xform& xform)
{
	if (!pComponent)
		return;

	// Use dp.DrawObject with transform — this is the simplest and most
	// complete approach. It uses Rhino's own rendering path, handles all
	// geometry types, materials, display modes, and caching automatically.
	dp.DrawObject(pComponent, &xform);
}

void CVisibilityConduit::DrawNestedFiltered(
	CRhinoDisplayPipeline& dp,
	const CRhinoInstanceObject* pNestedInstance,
	const ON_Xform& parentXform,
	const ON_UUID& topLevelId,
	const std::string& parentPath,
	int depth)
{
	if (!pNestedInstance || depth >= MAX_NESTING_DEPTH)
		return;

	const CRhinoInstanceDefinition* pDef = pNestedInstance->InstanceDefinition();
	if (!pDef)
		return;

	// Combine transforms: parent * nested
	ON_Xform combinedXform = parentXform * pNestedInstance->InstanceXform();

	int componentCount = pDef->ObjectCount();
	for (int i = 0; i < componentCount; i++)
	{
		std::string childPath = BuildPath(parentPath, i);

		// Check if this exact path is hidden
		if (m_visData.IsComponentHidden(topLevelId, childPath.c_str()))
		{
			if (m_debugLogging)
			{
				ON_wString msg;
				msg.Format(L"[Conduit]   nested path=\"%S\" => HIDDEN, skipping\n", childPath.c_str());
				RhinoApp().Print(msg);
			}
			continue;
		}

		const CRhinoObject* pComponent = pDef->Object(i);
		if (!pComponent || !pComponent->IsVisible())
			continue;

		if (pComponent->ObjectType() == ON::instance_reference)
		{
			const CRhinoInstanceObject* pDeeper =
				static_cast<const CRhinoInstanceObject*>(pComponent);

			if (m_visData.HasHiddenDescendants(topLevelId, childPath.c_str()))
			{
				// Recurse deeper with filtering
				DrawNestedFiltered(dp, pDeeper, combinedXform, topLevelId, childPath, depth + 1);
			}
			else
			{
				// No hidden descendants — draw entire sub-block normally
				DrawComponent(dp, pComponent, combinedXform);
			}
		}
		else
		{
			DrawComponent(dp, pComponent, combinedXform);
		}
	}
}

std::string CVisibilityConduit::BuildPath(const std::string& parentPath, int childIndex)
{
	if (parentPath.empty())
		return std::to_string(childIndex);
	return parentPath + "." + std::to_string(childIndex);
}

ON_Color CVisibilityConduit::GetComponentColor(
	const CRhinoObject* pComponent,
	const CRhinoDoc* pDoc)
{
	if (!pComponent)
		return ON_Color(128, 128, 128);

	const ON_3dmObjectAttributes& attrs = pComponent->Attributes();

	// Color source: by object
	if (attrs.ColorSource() == ON::color_from_object)
		return attrs.m_color;

	// Color source: by layer
	if (pDoc && attrs.ColorSource() == ON::color_from_layer)
	{
		int layerIndex = attrs.m_layer_index;
		const CRhinoLayerTable& layerTable = pDoc->m_layer_table;
		if (layerIndex >= 0 && layerIndex < layerTable.LayerCount())
		{
			const CRhinoLayer& layer = layerTable[layerIndex];
			return layer.Color();
		}
	}

	// Color source: by parent — use object color as fallback
	if (attrs.ColorSource() == ON::color_from_parent)
		return attrs.m_color;

	// Fallback gray
	return ON_Color(128, 128, 128);
}
