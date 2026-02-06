// VisibilityConduit.cpp : CRhinoDisplayConduit implementation
//
// Core logic: in SC_DRAWOBJECT, suppress managed instances via
// m_bDrawObject = false, then manually draw only visible components
// using dp.DrawObject() which uses Rhino's own rendering path.

#include "stdafx.h"
#include "VisibilityConduit.h"

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

	// --- This instance has hidden components: take over drawing ---

	// Suppress the default drawing of this object
	m_pChannelAttrs->m_bDrawObject = false;

	const CRhinoInstanceObject* pInstance =
		static_cast<const CRhinoInstanceObject*>(pObject);
	const CRhinoInstanceDefinition* pDef = pInstance->InstanceDefinition();
	if (!pDef)
		return true;

	ON_Xform instanceXform = pInstance->InstanceXform();

	// Iterate definition components, skip hidden ones
	int componentCount = pDef->ObjectCount();
	for (int i = 0; i < componentCount; i++)
	{
		if (m_visData.IsComponentHidden(instanceId, i))
			continue;

		const CRhinoObject* pComponent = pDef->Object(i);
		if (!pComponent)
			continue;

		// Skip objects that are hidden in the definition
		if (!pComponent->IsVisible())
			continue;

		if (pComponent->ObjectType() == ON::instance_reference)
		{
			// Nested block instance — recurse
			const CRhinoInstanceObject* pNestedInstance =
				static_cast<const CRhinoInstanceObject*>(pComponent);
			DrawNestedInstance(dp, pNestedInstance, instanceXform, 0);
		}
		else
		{
			DrawComponent(dp, pComponent, instanceXform);
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

void CVisibilityConduit::DrawNestedInstance(
	CRhinoDisplayPipeline& dp,
	const CRhinoInstanceObject* pNestedInstance,
	const ON_Xform& parentXform,
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
		const CRhinoObject* pComponent = pDef->Object(i);
		if (!pComponent || !pComponent->IsVisible())
			continue;

		if (pComponent->ObjectType() == ON::instance_reference)
		{
			const CRhinoInstanceObject* pDeeper =
				static_cast<const CRhinoInstanceObject*>(pComponent);
			DrawNestedInstance(dp, pDeeper, combinedXform, depth + 1);
		}
		else
		{
			DrawComponent(dp, pComponent, combinedXform);
		}
	}
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
