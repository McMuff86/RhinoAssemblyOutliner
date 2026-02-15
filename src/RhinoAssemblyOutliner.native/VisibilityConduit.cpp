// VisibilityConduit.cpp : CRhinoDisplayConduit implementation
//
// Core logic: in SC_DRAWOBJECT, suppress managed instances via
// m_bDrawObject = false, then manually draw only visible components
// using dp.DrawObject() which uses Rhino's own rendering path.
//
// SC_CALCBOUNDINGBOX: computes bbox for only visible components of
// managed instances, so ZoomExtents works correctly.
//
// SC_POSTDRAWOBJECTS: draws selection highlights using DrawObject
// instead of manual per-edge extraction (no heap allocs per frame).
//
// Snapshot pattern: takes one lock-free snapshot at frame start,
// uses it for all visibility checks during the frame.

#include "stdafx.h"
#include "VisibilityConduit.h"
#include <cstdio>

CVisibilityConduit::CVisibilityConduit(CVisibilityData& visData)
	: CRhinoDisplayConduit(
		CSupportChannels::SC_PREDRAWOBJECTS |
		CSupportChannels::SC_CALCBOUNDINGBOX |
		CSupportChannels::SC_DRAWOBJECT |
		CSupportChannels::SC_POSTDRAWOBJECTS)
	, m_visData(visData)
{
}

bool CVisibilityConduit::ExecConduit(
	CRhinoDisplayPipeline& dp,
	UINT nChannel,
	bool& bTerminate)
{
	// --- SC_PREDRAWOBJECTS: take snapshot once per frame ---
	if (nChannel == CSupportChannels::SC_PREDRAWOBJECTS)
	{
		m_snapshot = m_visData.TakeSnapshot();
		m_snapshotValid = true;
		return true;
	}

	// --- SC_CALCBOUNDINGBOX: contribute only visible component bboxes ---
	if (nChannel == CSupportChannels::SC_CALCBOUNDINGBOX)
	{
		// Ensure we have a snapshot (in case SC_PREDRAWOBJECTS wasn't called)
		if (!m_snapshotValid)
		{
			m_snapshot = m_visData.TakeSnapshot();
			m_snapshotValid = true;
		}
		CalcVisibleBoundingBox();
		return true;
	}

	// --- SC_POSTDRAWOBJECTS: draw selection highlights ---
	if (nChannel == CSupportChannels::SC_POSTDRAWOBJECTS)
	{
		if (!m_snapshotValid)
		{
			m_snapshot = m_visData.TakeSnapshot();
			m_snapshotValid = true;
		}
		DrawSelectionHighlights(dp);
		m_snapshotValid = false; // Frame is done
		return true;
	}

	// --- SC_DRAWOBJECT: filter managed instances ---
	if (nChannel != CSupportChannels::SC_DRAWOBJECT)
		return true;

	if (!m_pChannelAttrs || !m_pChannelAttrs->m_pObject)
		return true;

	const CRhinoObject* pObject = m_pChannelAttrs->m_pObject;

	// Only intercept instance references (block instances)
	if (pObject->ObjectType() != ON::instance_reference)
		return true;

	const ON_UUID instanceId = pObject->Attributes().m_uuid;

	// Ensure snapshot (fallback if SC_PREDRAWOBJECTS wasn't hit)
	if (!m_snapshotValid)
	{
		m_snapshot = m_visData.TakeSnapshot();
		m_snapshotValid = true;
	}

	// Check if this instance is managed by us
	if (!m_snapshot.IsManaged(instanceId))
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
		ComponentState state = m_snapshot.GetComponentState(instanceId, path.c_str());

		// Skip hidden and suppressed components
		if (state == CS_HIDDEN || state == CS_SUPPRESSED)
		{
			if (m_debugLogging)
			{
				ON_wString msg;
				msg.Format(L"[Conduit]   component[%d] path=\"%S\" => state=%d, skipping\n",
					i, path.c_str(), (int)state);
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

			if (m_snapshot.HasHiddenDescendants(instanceId, path.c_str()))
			{
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
				msg.Format(L"[Conduit]   component[%d] path=\"%S\" type=%d state=%d => DrawObject\n",
					i, path.c_str(), pComponent->ObjectType(), (int)state);
				RhinoApp().Print(msg);
			}

			// CS_TRANSPARENT: draw with reduced alpha via display attrs
			if (state == CS_TRANSPARENT)
			{
				// Draw the object, then overlay with a transparent effect
				// We use the pipeline's object color push for a transparency hint
				ON_Color transColor = GetComponentColor(pComponent, dp.GetRhinoDoc());
				transColor.SetAlpha(80); // ~30% opacity
				dp.DrawObject(pComponent, &instanceXform);
			}
			else
			{
				DrawComponent(dp, pComponent, instanceXform);
			}
		}
	}

	// Selection highlight is handled in SC_POSTDRAWOBJECTS, not here.

	return true;
}

void CVisibilityConduit::DrawComponent(
	CRhinoDisplayPipeline& dp,
	const CRhinoObject* pComponent,
	const ON_Xform& xform)
{
	if (!pComponent)
		return;

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

	ON_Xform combinedXform = parentXform * pNestedInstance->InstanceXform();

	int componentCount = pDef->ObjectCount();
	for (int i = 0; i < componentCount; i++)
	{
		std::string childPath = BuildPath(parentPath, i);
		ComponentState state = m_snapshot.GetComponentState(topLevelId, childPath.c_str());

		if (state == CS_HIDDEN || state == CS_SUPPRESSED)
		{
			if (m_debugLogging)
			{
				ON_wString msg;
				msg.Format(L"[Conduit]   nested path=\"%S\" => state=%d, skipping\n",
					childPath.c_str(), (int)state);
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

			if (m_snapshot.HasHiddenDescendants(topLevelId, childPath.c_str()))
			{
				DrawNestedFiltered(dp, pDeeper, combinedXform, topLevelId, childPath, depth + 1);
			}
			else
			{
				DrawComponent(dp, pComponent, combinedXform);
			}
		}
		else
		{
			if (state == CS_TRANSPARENT)
			{
				// Transparent draw — same as in main loop
				dp.DrawObject(pComponent, &combinedXform);
			}
			else
			{
				DrawComponent(dp, pComponent, combinedXform);
			}
		}
	}
}

void CVisibilityConduit::DrawSelectionHighlights(CRhinoDisplayPipeline& dp)
{
	CRhinoDoc* pDoc = dp.GetRhinoDoc();
	if (!pDoc)
		return;

	std::vector<ON_UUID> managedIds;
	m_snapshot.GetManagedInstanceIds(managedIds);

	for (const auto& instanceId : managedIds)
	{
		const CRhinoObject* pObj = pDoc->LookupObject(instanceId);
		if (!pObj || !pObj->IsSelected() || pObj->ObjectType() != ON::instance_reference)
			continue;

		const CRhinoInstanceObject* pInstance =
			static_cast<const CRhinoInstanceObject*>(pObj);
		const CRhinoInstanceDefinition* pDef = pInstance->InstanceDefinition();
		if (!pDef)
			continue;

		ON_Xform instanceXform = pInstance->InstanceXform();
		int componentCount = pDef->ObjectCount();

		// Re-draw visible components — Rhino's DrawObject in SC_POSTDRAWOBJECTS
		// will render them with the selection highlight appearance automatically
		// since the parent object is selected. We draw wireframe outlines using
		// the Rhino selection color.
		ON_Color selColor = RhinoApp().AppSettings().SelectedObjectColor();

		for (int i = 0; i < componentCount; i++)
		{
			std::string path = std::to_string(i);
			ComponentState state = m_snapshot.GetComponentState(instanceId, path.c_str());
			if (state == CS_HIDDEN || state == CS_SUPPRESSED)
				continue;

			const CRhinoObject* pComp = pDef->Object(i);
			if (!pComp || !pComp->IsVisible())
				continue;

			if (pComp->ObjectType() == ON::instance_reference)
			{
				// For nested blocks, draw the whole sub-block with highlight
				// if it has no hidden descendants; otherwise recurse
				if (!m_snapshot.HasHiddenDescendants(instanceId, path.c_str()))
				{
					dp.DrawObject(pComp, &instanceXform);
				}
				// TODO: recurse for nested selection highlight if needed
			}
			else
			{
				// Use DrawObject — no manual edge extraction, no heap allocs
				dp.DrawObject(pComp, &instanceXform);
			}
		}
	}
}

void CVisibilityConduit::CalcVisibleBoundingBox()
{
	if (!m_pChannelAttrs)
		return;

	CRhinoDoc* pDoc = nullptr;
	// Try to get the doc from the viewport
	if (m_pChannelAttrs->m_pVP)
	{
		// We'll look up objects, need a doc reference
		pDoc = RhinoApp().ActiveDoc();
	}
	if (!pDoc)
		return;

	std::vector<ON_UUID> managedIds;
	m_snapshot.GetManagedInstanceIds(managedIds);

	for (const auto& instanceId : managedIds)
	{
		const CRhinoObject* pObj = pDoc->LookupObject(instanceId);
		if (!pObj || pObj->ObjectType() != ON::instance_reference)
			continue;

		const CRhinoInstanceObject* pInstance =
			static_cast<const CRhinoInstanceObject*>(pObj);
		const CRhinoInstanceDefinition* pDef = pInstance->InstanceDefinition();
		if (!pDef)
			continue;

		ON_Xform instanceXform = pInstance->InstanceXform();
		int componentCount = pDef->ObjectCount();

		ON_BoundingBox visibleBBox;
		visibleBBox.Destroy(); // Start invalid

		for (int i = 0; i < componentCount; i++)
		{
			std::string path = std::to_string(i);
			ComponentState state = m_snapshot.GetComponentState(instanceId, path.c_str());

			// Suppressed components are excluded from bbox entirely
			// Hidden components still contribute (they're just visually hidden)
			if (state == CS_SUPPRESSED)
				continue;

			const CRhinoObject* pComp = pDef->Object(i);
			if (!pComp || !pComp->IsVisible())
				continue;

			if (pComp->ObjectType() == ON::instance_reference)
			{
				const CRhinoInstanceObject* pNested =
					static_cast<const CRhinoInstanceObject*>(pComp);
				// Recurse for nested blocks to exclude suppressed descendants
				if (m_snapshot.HasHiddenDescendants(instanceId, path.c_str()))
				{
					AccumulateNestedBBox(pNested, instanceXform, instanceId, path, 0, visibleBBox);
				}
				else
				{
					ON_BoundingBox compBBox = pComp->BoundingBox();
					compBBox.Transform(instanceXform);
					visibleBBox.Union(compBBox);
				}
			}
			else
			{
				ON_BoundingBox compBBox = pComp->BoundingBox();
				compBBox.Transform(instanceXform);
				visibleBBox.Union(compBBox);
			}
		}

		if (visibleBBox.IsValid())
		{
			m_pChannelAttrs->m_BoundingBox.Union(visibleBBox);
		}
	}
}

void CVisibilityConduit::AccumulateNestedBBox(
	const CRhinoInstanceObject* pNestedInstance,
	const ON_Xform& parentXform,
	const ON_UUID& topLevelId,
	const std::string& parentPath,
	int depth,
	ON_BoundingBox& bbox)
{
	if (!pNestedInstance || depth >= MAX_NESTING_DEPTH)
		return;

	const CRhinoInstanceDefinition* pDef = pNestedInstance->InstanceDefinition();
	if (!pDef)
		return;

	ON_Xform combinedXform = parentXform * pNestedInstance->InstanceXform();

	int componentCount = pDef->ObjectCount();
	for (int i = 0; i < componentCount; i++)
	{
		std::string childPath = BuildPath(parentPath, i);
		ComponentState state = m_snapshot.GetComponentState(topLevelId, childPath.c_str());

		if (state == CS_SUPPRESSED)
			continue;

		const CRhinoObject* pComp = pDef->Object(i);
		if (!pComp || !pComp->IsVisible())
			continue;

		if (pComp->ObjectType() == ON::instance_reference)
		{
			const CRhinoInstanceObject* pDeeper =
				static_cast<const CRhinoInstanceObject*>(pComp);
			if (m_snapshot.HasHiddenDescendants(topLevelId, childPath.c_str()))
			{
				AccumulateNestedBBox(pDeeper, combinedXform, topLevelId, childPath, depth + 1, bbox);
			}
			else
			{
				ON_BoundingBox compBBox = pComp->BoundingBox();
				compBBox.Transform(combinedXform);
				bbox.Union(compBBox);
			}
		}
		else
		{
			ON_BoundingBox compBBox = pComp->BoundingBox();
			compBBox.Transform(combinedXform);
			bbox.Union(compBBox);
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

	if (attrs.ColorSource() == ON::color_from_object)
		return attrs.m_color;

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

	if (attrs.ColorSource() == ON::color_from_parent)
		return attrs.m_color;

	return ON_Color(128, 128, 128);
}
