// VisibilityData.h : Thread-safe per-instance component visibility state
//
// Stores which components within block instances are hidden.
// Uses CRITICAL_SECTION for thread safety (render thread vs UI thread).

#pragma once

#include <unordered_map>
#include <unordered_set>

/// Hash functor for ON_UUID in std containers
struct ON_UUID_Hash
{
	size_t operator()(const ON_UUID& id) const noexcept
	{
		// ON_UUID is 16 bytes — hash the first 8 as size_t for speed
		const size_t* p = reinterpret_cast<const size_t*>(&id);
		return p[0] ^ (p[1] * 0x9e3779b97f4a7c15ULL);
	}
};

/// Equality functor for ON_UUID in std containers
struct ON_UUID_Equal
{
	bool operator()(const ON_UUID& a, const ON_UUID& b) const noexcept
	{
		return ON_UuidCompare(a, b) == 0;
	}
};

/// RAII lock helper for CRITICAL_SECTION
class CAutoLock
{
public:
	explicit CAutoLock(CRITICAL_SECTION& cs) : m_cs(cs) { ::EnterCriticalSection(&m_cs); }
	~CAutoLock() { ::LeaveCriticalSection(&m_cs); }

	CAutoLock(const CAutoLock&) = delete;
	CAutoLock& operator=(const CAutoLock&) = delete;

private:
	CRITICAL_SECTION& m_cs;
};

/// Thread-safe visibility state storage.
/// Maps instance UUID -> set of hidden component indices.
class CVisibilityData
{
public:
	CVisibilityData()
	{
		::InitializeCriticalSection(&m_cs);
	}

	~CVisibilityData()
	{
		::DeleteCriticalSection(&m_cs);
	}

	CVisibilityData(const CVisibilityData&) = delete;
	CVisibilityData& operator=(const CVisibilityData&) = delete;

	/// Hide a component within a specific block instance
	void SetComponentHidden(const ON_UUID& instanceId, int componentIndex)
	{
		CAutoLock lock(m_cs);
		m_data[instanceId].insert(componentIndex);
	}

	/// Show a component within a specific block instance
	void SetComponentVisible(const ON_UUID& instanceId, int componentIndex)
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		if (it != m_data.end())
		{
			it->second.erase(componentIndex);
			if (it->second.empty())
				m_data.erase(it);
		}
	}

	/// Reset all hidden components for a specific instance
	void ResetInstance(const ON_UUID& instanceId)
	{
		CAutoLock lock(m_cs);
		m_data.erase(instanceId);
	}

	/// Check if this instance has any hidden components (is managed by us)
	bool IsManaged(const ON_UUID& instanceId) const
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		return it != m_data.end() && !it->second.empty();
	}

	/// Check if a specific component is hidden
	bool IsComponentHidden(const ON_UUID& instanceId, int componentIndex) const
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		if (it == m_data.end())
			return false;
		return it->second.count(componentIndex) > 0;
	}

	/// Get the number of hidden components for a specific instance
	int GetHiddenCount(const ON_UUID& instanceId) const
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		if (it == m_data.end())
			return 0;
		return static_cast<int>(it->second.size());
	}

	/// Clear all visibility data
	void ClearAll()
	{
		CAutoLock lock(m_cs);
		m_data.clear();
	}

private:
	mutable CRITICAL_SECTION m_cs;
	std::unordered_map<ON_UUID, std::unordered_set<int>, ON_UUID_Hash, ON_UUID_Equal> m_data;
};
