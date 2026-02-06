// VisibilityConduit.h : CRhinoDisplayConduit for per-instance component visibility
//
// Intercepts SC_DRAWOBJECT to suppress managed block instances and
// re-draw only their visible components.

#pragma once

#include "VisibilityData.h"

class CVisibilityConduit : public CRhinoDisplayConduit
{
public:
	/// Construct with a reference to the shared visibility data
	explicit CVisibilityConduit(CVisibilityData& visData);

	bool ExecConduit(
		CRhinoDisplayPipeline& dp,
		UINT nChannel,
		bool& bTerminate
	) override;

private:
	static const int MAX_NESTING_DEPTH = 32;

	/// Draw a single component with the given transform
	void DrawComponent(
		CRhinoDisplayPipeline& dp,
		const CRhinoObject* pComponent,
		const ON_Xform& xform
	);

	/// Recursively draw a nested block instance, combining transforms
	void DrawNestedInstance(
		CRhinoDisplayPipeline& dp,
		const CRhinoInstanceObject* pNestedInstance,
		const ON_Xform& parentXform,
		int depth
	);

	/// Resolve display color for a component
	ON_Color GetComponentColor(
		const CRhinoObject* pComponent,
		const CRhinoDoc* pDoc
	);

	CVisibilityData& m_visData;
};
