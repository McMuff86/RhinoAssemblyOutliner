// DocEventHandler.h : CRhinoEventWatcher for document lifecycle events
// Handles persistence sync on open/save/close and cleanup on object delete.

#pragma once

#include "VisibilityData.h"

class CDocEventHandler : public CRhinoEventWatcher
{
public:
	explicit CDocEventHandler(CVisibilityData& visData);
	~CDocEventHandler() override = default;

	// CRhinoEventWatcher overrides
	void OnEndOpenDocument(CRhinoDoc& doc, const wchar_t* filename, BOOL bMerge, BOOL bReference) override;
	void OnBeginSaveDocument(CRhinoDoc& doc, const wchar_t* filename, BOOL bExportSelected) override;
	void OnCloseDocument(CRhinoDoc& doc) override;
	void OnDeleteObject(CRhinoDoc& doc, CRhinoObject& object) override;

private:
	CVisibilityData& m_visData;
};
