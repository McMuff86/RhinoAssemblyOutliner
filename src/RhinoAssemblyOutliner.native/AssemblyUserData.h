// AssemblyUserData.h : ON_UserData for persisted assembly metadata.
// Attached to block instance object attributes.

#pragma once

// UUID for this userdata item - defined once in AssemblyUserData.cpp.
// {FB1B5311-8282-4608-9514-D8E74E2ABC41}
extern const ON_UUID AssemblyUserDataId;

class ON_AssemblyUserData : public ON_UserData
{
	ON_OBJECT_DECLARE(ON_AssemblyUserData);

public:
	ON_AssemblyUserData();
	ON_AssemblyUserData(const ON_AssemblyUserData& src);
	ON_AssemblyUserData& operator=(const ON_AssemblyUserData& src);
	~ON_AssemblyUserData() override = default;

	// ON_UserData overrides.
	bool GetDescription(ON_wString& description) override;
	bool Archive() const override { return true; }
	bool Transform(const ON_Xform& xform) override;

	// ON_UserData serialization.
	bool Write(ON_BinaryArchive& archive) const override;
	bool Read(ON_BinaryArchive& archive) override;

	int m_classVersion;
	ON_UUID m_sourceDefinitionId;
	ON_wString m_sourceDefinitionName;
	ON_SimpleArray<int> m_hiddenComponentIndices;
	int m_componentCount;
};
