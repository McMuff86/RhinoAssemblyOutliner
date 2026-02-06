// VisibilityConduit.h : CRhinoDisplayConduit for per-instance component visibility
//
// Intercepts SC_DRAWOBJECT to suppress managed block instances and
// re-draw only their visible components using path-based filtering.

#pragma once

#include "VisibilityData.h"
#include <string>

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

	/// Enable/disable debug output to Rhino command line
	void SetDebugLogging(bool enabled) { m_debugLogging = enabled; }
	bool GetDebugLogging() const { return m_debugLogging; }

private:
	static const int MAX_NESTING_DEPTH = 32;

	/// Draw a single component with the given transform.
	/// Uses dp.DrawObject, which handles all geometry types via Rhino's pipeline.
	void DrawComponent(
		CRhinoDisplayPipeline& dp,
		const CRhinoObject* pComponent,
		const ON_Xform& xform
	);

	/// Recursively draw a nested block instance with path-based filtering.
	/// Only recurses into sub-blocks that contain hidden descendants.
	void DrawNestedFiltered(
		CRhinoDisplayPipeline& dp,
		const CRhinoInstanceObject* pNestedInstance,
		const ON_Xform& parentXform,
		const ON_UUID& topLevelId,
		const std::string& parentPath,
		int depth
	);

	/// Resolve display color for a component
	ON_Color GetComponentColor(
		const CRhinoObject* pComponent,
		const CRhinoDoc* pDoc
	);

	/// Build a child path string: "parentPath.childIndex" or just "childIndex"
	static std::string BuildPath(const std::string& parentPath, int childIndex);

	CVisibilityData& m_visData;
	bool m_debugLogging = false;
};
