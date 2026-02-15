// VisibilityConduit.h : CRhinoDisplayConduit for per-instance component visibility
//
// Intercepts SC_DRAWOBJECT to suppress managed block instances and
// re-draw only their visible components using path-based filtering.
// Uses SC_CALCBOUNDINGBOX for correct zoom extents.
// Uses SC_POSTDRAWOBJECTS for selection highlights (no per-frame heap allocs).

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

	/// Draw selection highlights for all managed selected instances.
	/// Called from SC_POSTDRAWOBJECTS — uses DrawObject instead of manual edge extraction.
	void DrawSelectionHighlights(CRhinoDisplayPipeline& dp);

	/// Compute bounding box contribution for managed instances (only visible components).
	/// Called from SC_CALCBOUNDINGBOX.
	void CalcVisibleBoundingBox();

	/// Resolve display color for a component
	ON_Color GetComponentColor(
		const CRhinoObject* pComponent,
		const CRhinoDoc* pDoc
	);

	/// Build a child path string: "parentPath.childIndex" or just "childIndex"
	static std::string BuildPath(const std::string& parentPath, int childIndex);

	/// Accumulate bounding box for visible components of a nested block
	void AccumulateNestedBBox(
		const CRhinoInstanceObject* pNestedInstance,
		const ON_Xform& parentXform,
		const ON_UUID& topLevelId,
		const std::string& parentPath,
		int depth,
		ON_BoundingBox& bbox
	);

	CVisibilityData& m_visData;
	CVisibilitySnapshot m_snapshot;  ///< Per-frame snapshot, taken once at SC_PREDRAWOBJECTS
	bool m_snapshotValid = false;    ///< Whether snapshot is valid for this frame
	bool m_debugLogging = false;
};
