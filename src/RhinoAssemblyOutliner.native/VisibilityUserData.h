// VisibilityUserData.h : ON_UserData-derived class to persist hidden component
// paths with the document. Attached to CRhinoInstanceObject via UserData.

#pragma once

#include <unordered_set>
#include <string>

// Static UUID for this userdata class
// {A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78}
static const ON_UUID VisibilityUserDataId = {
	0xa7b3c4d5, 0xe6f7, 0x4890,
	{ 0xab, 0x12, 0xcd, 0x34, 0xef, 0x56, 0xab, 0x78 }
};

class CVisibilityData; // forward

class CComponentVisibilityData : public ON_UserData
{
	ON_OBJECT_DECLARE(CComponentVisibilityData)

public:
	CComponentVisibilityData();
	~CComponentVisibilityData() override = default;

	// ON_Object overrides
	ON_UUID UserDataClassUuid() const override { return VisibilityUserDataId; }
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
