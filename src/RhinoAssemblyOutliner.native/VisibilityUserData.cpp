// VisibilityUserData.cpp : ON_UserData persistence implementation

#include "stdafx.h"
#include "VisibilityUserData.h"
#include "VisibilityData.h"

ON_OBJECT_IMPLEMENT(CComponentVisibilityData, ON_UserData, "A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78");

CComponentVisibilityData::CComponentVisibilityData()
{
	m_userdata_uuid = VisibilityUserDataId;
	m_application_uuid = ON_rhino5_id; // Rhino application id
	m_userdata_copycount = 1;
}

bool CComponentVisibilityData::GetDescription(ON_wString& description)
{
	description = L"RhinoAssemblyOutliner Component Visibility Data";
	return true;
}

bool CComponentVisibilityData::Write(ON_BinaryArchive& archive) const
{
	// Version 1 header for forward compatibility
	if (!archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, 1, 0))
		return false;

	bool rc = false;
	for (;;)
	{
		// Write count
		int count = static_cast<int>(HiddenPaths.size());
		if (!archive.WriteInt(count))
			break;

		// Write each path as a string
		bool pathsOk = true;
		for (const auto& path : HiddenPaths)
		{
			ON_wString wPath(path.c_str());
			if (!archive.WriteString(wPath))
			{
				pathsOk = false;
				break;
			}
		}
		if (!pathsOk)
			break;

		rc = true;
		break;
	}

	if (!archive.EndWrite3dmChunk())
		rc = false;

	return rc;
}

bool CComponentVisibilityData::Read(ON_BinaryArchive& archive)
{
	int major_version = 0;
	int minor_version = 0;
	if (!archive.BeginRead3dmChunk(TCODE_ANONYMOUS_CHUNK, &major_version, &minor_version))
		return false;

	bool rc = false;
	HiddenPaths.clear();

	for (;;)
	{
		if (major_version != 1)
			break;

		int count = 0;
		if (!archive.ReadInt(&count))
			break;

		bool pathsOk = true;
		for (int i = 0; i < count; i++)
		{
			ON_wString wPath;
			if (!archive.ReadString(wPath))
			{
				pathsOk = false;
				break;
			}
			ON_String utf8(wPath);
			HiddenPaths.insert(std::string(static_cast<const char*>(utf8)));
		}
		if (!pathsOk)
			break;

		rc = true;
		break;
	}

	if (!archive.EndRead3dmChunk())
		rc = false;

	return rc;
}

void CComponentVisibilityData::SyncFromVisData(const ON_UUID& instanceId, CVisibilityData& visData)
{
	HiddenPaths.clear();
	visData.GetHiddenPaths(instanceId, HiddenPaths);
}

void CComponentVisibilityData::SyncToVisData(const ON_UUID& instanceId, CVisibilityData& visData)
{
	// Clear existing and set from our stored paths
	visData.ResetInstance(instanceId);
	for (const auto& path : HiddenPaths)
	{
		visData.SetComponentHidden(instanceId, path.c_str());
	}
}
