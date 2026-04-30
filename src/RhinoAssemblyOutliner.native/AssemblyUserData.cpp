// AssemblyUserData.cpp : ON_UserData persistence for assembly metadata.

#include "stdafx.h"
#include "AssemblyUserData.h"

// {FB1B5311-8282-4608-9514-D8E74E2ABC41}
const ON_UUID AssemblyUserDataId = {
	0xfb1b5311, 0x8282, 0x4608,
	{ 0x95, 0x14, 0xd8, 0xe7, 0x4e, 0x2a, 0xbc, 0x41 }
};

// {68EE26AC-D516-4F50-9DE2-46D105702323} - same as C# plugin.
static const ON_UUID PluginId = {
	0x68ee26ac, 0xd516, 0x4f50,
	{ 0x9d, 0xe2, 0x46, 0xd1, 0x05, 0x70, 0x23, 0x23 }
};

ON_OBJECT_IMPLEMENT(ON_AssemblyUserData, ON_UserData, "7713A117-496E-4342-A786-F4DE0953EC07");

ON_AssemblyUserData::ON_AssemblyUserData()
	: m_classVersion(1)
	, m_sourceDefinitionId(ON_nil_uuid)
	, m_componentCount(0)
{
	m_userdata_uuid = AssemblyUserDataId;
	m_application_uuid = PluginId;
	m_userdata_copycount = 1;
}

ON_AssemblyUserData::ON_AssemblyUserData(const ON_AssemblyUserData& src)
	: ON_UserData(src)
	, m_classVersion(src.m_classVersion)
	, m_sourceDefinitionId(src.m_sourceDefinitionId)
	, m_sourceDefinitionName(src.m_sourceDefinitionName)
	, m_hiddenComponentIndices(src.m_hiddenComponentIndices)
	, m_componentCount(src.m_componentCount)
{
	m_userdata_uuid = AssemblyUserDataId;
	m_application_uuid = PluginId;
	m_userdata_copycount = 1;
}

ON_AssemblyUserData& ON_AssemblyUserData::operator=(const ON_AssemblyUserData& src)
{
	if (this != &src)
	{
		ON_UserData::operator=(src);
		m_classVersion = src.m_classVersion;
		m_sourceDefinitionId = src.m_sourceDefinitionId;
		m_sourceDefinitionName = src.m_sourceDefinitionName;
		m_hiddenComponentIndices = src.m_hiddenComponentIndices;
		m_componentCount = src.m_componentCount;

		m_userdata_uuid = AssemblyUserDataId;
		m_application_uuid = PluginId;
		m_userdata_copycount = 1;
	}

	return *this;
}

bool ON_AssemblyUserData::GetDescription(ON_wString& description)
{
	description = L"Assembly Outliner Data";
	return true;
}

bool ON_AssemblyUserData::Transform(const ON_Xform&)
{
	// Metadata-only state: it should travel unchanged with moves, copies,
	// rotations, scales, and paste operations.
	return true;
}

bool ON_AssemblyUserData::Write(ON_BinaryArchive& archive) const
{
	if (!archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, 1, 0))
		return false;

	bool rc = false;
	for (;;)
	{
		if (!archive.WriteInt(m_classVersion))
			break;
		if (!archive.WriteUuid(m_sourceDefinitionId))
			break;
		if (!archive.WriteString(m_sourceDefinitionName))
			break;

		const int hiddenCount = m_hiddenComponentIndices.Count();
		if (!archive.WriteInt(hiddenCount))
			break;

		bool indicesOk = true;
		for (int i = 0; i < hiddenCount; ++i)
		{
			if (!archive.WriteInt(m_hiddenComponentIndices[i]))
			{
				indicesOk = false;
				break;
			}
		}
		if (!indicesOk)
			break;

		if (!archive.WriteInt(m_componentCount))
			break;

		rc = true;
		break;
	}

	if (!archive.EndWrite3dmChunk())
		rc = false;

	return rc;
}

bool ON_AssemblyUserData::Read(ON_BinaryArchive& archive)
{
	int majorVersion = 0;
	int minorVersion = 0;
	if (!archive.BeginRead3dmChunk(TCODE_ANONYMOUS_CHUNK, &majorVersion, &minorVersion))
		return false;

	bool rc = false;
	m_hiddenComponentIndices.Empty();

	for (;;)
	{
		if (majorVersion != 1)
			break;

		if (!archive.ReadInt(&m_classVersion))
			break;
		if (!archive.ReadUuid(m_sourceDefinitionId))
			break;
		if (!archive.ReadString(m_sourceDefinitionName))
			break;

		int hiddenCount = 0;
		if (!archive.ReadInt(&hiddenCount))
			break;
		if (hiddenCount < 0 || hiddenCount > 100000)
			break;

		bool indicesOk = true;
		for (int i = 0; i < hiddenCount; ++i)
		{
			int index = 0;
			if (!archive.ReadInt(&index))
			{
				indicesOk = false;
				break;
			}
			m_hiddenComponentIndices.Append(index);
		}
		if (!indicesOk)
			break;

		if (!archive.ReadInt(&m_componentCount))
			break;

		rc = true;
		break;
	}

	if (!archive.EndRead3dmChunk())
		rc = false;

	return rc;
}
