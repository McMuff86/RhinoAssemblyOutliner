// VisibilityUserData.h : ON_UserData-derived class to persist hidden component
// paths with the document. Attached to CRhinoInstanceObject via UserData.

#pragma once

#include <unordered_set>
#include <string>

// UUID for this userdata class — defined once in VisibilityUserData.cpp
// {A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78}
extern const ON_UUID VisibilityUserDataId;

class CVisibilityData; // forward

class CComponentVisibilityData : public ON_UserData
{
	ON_OBJECT_DECLARE(CComponentVisibilityData);

public:
	CComponentVisibilityData();
	~CComponentVisibilityData() override = default;

	// ON_UserData overrides
	bool GetDescription(ON_wString& description) override;
	bool Archive() const override { return true; }

	// ON_UserData serialization
	bool Write(ON_BinaryArchive& archive) const override;
	bool Read(ON_BinaryArchive& archive) override;

	// Data
	std::unordered_set<std::string> HiddenPaths;

	// Sync helpers
	void SyncFromVisData(const ON_UUID& instanceId, CVisibilityData& visData);
	void SyncToVisData(const ON_UUID& instanceId, CVisibilityData& visData);
};
