// VisibilityData.h : Thread-safe per-instance component visibility state
//
// Stores which components within block instances are hidden/suppressed/transparent.
// Uses CRITICAL_SECTION for thread safety (render thread vs UI thread).
//
// Paths are dot-separated index strings, e.g.:
//   "0"     — first component in the top-level definition
//   "1.0"   — first child of the second component (nested block)
//   "1.0.2" — third child inside a doubly-nested block

#pragma once

#include <unordered_map>
#include <unordered_set>
#include <string>
#include <vector>

/// Component state enum — supports hide, suppress, and transparency
enum ComponentState
{
	CS_VISIBLE     = 0,
	CS_HIDDEN      = 1,   // Visual only — still in BOM, still in bbox
	CS_SUPPRESSED  = 2,   // Structural — excluded from BOM, bbox, export
	CS_TRANSPARENT = 3    // Draw with alpha transparency
};

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

/// Lock-free snapshot of visibility data for one frame.
/// Taken once at frame start, used for all checks during that frame.
/// Same query API as CVisibilityData but without locks.
class CVisibilitySnapshot
{
public:
	CVisibilitySnapshot() = default;

	/// Per-instance data: component states + precomputed parent prefixes
	struct InstanceData
	{
		std::unordered_map<std::string, ComponentState> states;
		std::unordered_set<std::string> parentPrefixes; // for O(1) HasHiddenDescendants
	};

	/// Check if this instance has any non-visible components (is managed by us)
	bool IsManaged(const ON_UUID& instanceId) const
	{
		auto it = m_data.find(instanceId);
		return it != m_data.end() && !it->second.states.empty();
	}

	/// Get the state of a component (CS_VISIBLE if not found)
	ComponentState GetComponentState(const ON_UUID& instanceId, const char* path) const
	{
		auto it = m_data.find(instanceId);
		if (it == m_data.end())
			return CS_VISIBLE;
		auto sit = it->second.states.find(std::string(path));
		if (sit == it->second.states.end())
			return CS_VISIBLE;
		return sit->second;
	}

	/// Check if a specific component path is hidden (CS_HIDDEN or CS_SUPPRESSED)
	bool IsComponentHidden(const ON_UUID& instanceId, const char* path) const
	{
		ComponentState s = GetComponentState(instanceId, path);
		return s == CS_HIDDEN || s == CS_SUPPRESSED;
	}

	/// Check if a component is suppressed (excluded from bbox too)
	bool IsComponentSuppressed(const ON_UUID& instanceId, const char* path) const
	{
		return GetComponentState(instanceId, path) == CS_SUPPRESSED;
	}

	/// Check if a component should be drawn with transparency
	bool IsComponentTransparent(const ON_UUID& instanceId, const char* path) const
	{
		return GetComponentState(instanceId, path) == CS_TRANSPARENT;
	}

	/// O(1) check if any descendant path is non-visible
	bool HasHiddenDescendants(const ON_UUID& instanceId, const char* pathPrefix) const
	{
		auto it = m_data.find(instanceId);
		if (it == m_data.end())
			return false;
		return it->second.parentPrefixes.count(std::string(pathPrefix)) > 0;
	}

	/// Get all managed instance IDs
	void GetManagedInstanceIds(std::vector<ON_UUID>& outIds) const
	{
		outIds.reserve(m_data.size());
		for (const auto& pair : m_data)
		{
			if (!pair.second.states.empty())
				outIds.push_back(pair.first);
		}
	}

	/// Direct access to internal data (for building the snapshot)
	std::unordered_map<ON_UUID, InstanceData, ON_UUID_Hash, ON_UUID_Equal> m_data;
};


/// Thread-safe visibility state storage.
/// Maps instance UUID -> map of component path -> ComponentState.
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

	/// Set a component to a specific state
	void SetState(const ON_UUID& instanceId, const char* path, ComponentState state)
	{
		CAutoLock lock(m_cs);
		if (state == CS_VISIBLE)
		{
			// Remove from map (visible is the default)
			auto it = m_data.find(instanceId);
			if (it != m_data.end())
			{
				it->second.erase(std::string(path));
				if (it->second.empty())
					m_data.erase(it);
			}
			RebuildPrefixes(instanceId);
		}
		else
		{
			m_data[instanceId][std::string(path)] = state;
			RebuildPrefixes(instanceId);
		}
	}

	/// Get a component's state
	ComponentState GetState(const ON_UUID& instanceId, const char* path) const
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		if (it == m_data.end())
			return CS_VISIBLE;
		auto sit = it->second.find(std::string(path));
		if (sit == it->second.end())
			return CS_VISIBLE;
		return sit->second;
	}

	/// Hide a component at a given path within a specific block instance
	void SetComponentHidden(const ON_UUID& instanceId, const char* path)
	{
		SetState(instanceId, path, CS_HIDDEN);
	}

	/// Show a component at a given path within a specific block instance
	void SetComponentVisible(const ON_UUID& instanceId, const char* path)
	{
		SetState(instanceId, path, CS_VISIBLE);
	}

	/// Reset all hidden components for a specific instance
	void ResetInstance(const ON_UUID& instanceId)
	{
		CAutoLock lock(m_cs);
		m_data.erase(instanceId);
		m_prefixes.erase(instanceId);
	}

	/// Check if this instance has any non-visible components (is managed by us)
	bool IsManaged(const ON_UUID& instanceId) const
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		return it != m_data.end() && !it->second.empty();
	}

	/// Check if a specific component path is hidden (CS_HIDDEN or CS_SUPPRESSED)
	bool IsComponentHidden(const ON_UUID& instanceId, const char* path) const
	{
		CAutoLock lock(m_cs);
		auto it = m_data.find(instanceId);
		if (it == m_data.end())
			return false;
		auto sit = it->second.find(std::string(path));
		if (sit == it->second.end())
			return false;
		return sit->second == CS_HIDDEN || sit->second == CS_SUPPRESSED;
	}

	/// Check if any path starting with the given prefix is non-visible.
	/// Uses precomputed prefix set for O(1) lookup.
	bool HasHiddenDescendants(const ON_UUID& instanceId, const char* pathPrefix) const
	{
		CAutoLock lock(m_cs);
		auto it = m_prefixes.find(instanceId);
		if (it == m_prefixes.end())
			return false;
		return it->second.count(std::string(pathPrefix)) > 0;
	}

	/// Get the number of non-visible component paths for a specific instance
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
		m_prefixes.clear();
	}

	/// Get all hidden paths for an instance (copies into output set — backward compat)
	void GetHiddenPaths(const ON_UUID& instanceId, std::unordered_set<std::string>& outPaths) const
	{
		CAutoLock lock(m_cs);
		outPaths.clear();
		auto it = m_data.find(instanceId);
		if (it != m_data.end())
		{
			for (const auto& pair : it->second)
			{
				if (pair.second == CS_HIDDEN || pair.second == CS_SUPPRESSED)
					outPaths.insert(pair.first);
			}
		}
	}

	/// Get all managed instance IDs
	void GetManagedInstanceIds(std::vector<ON_UUID>& outIds) const
	{
		CAutoLock lock(m_cs);
		outIds.reserve(m_data.size());
		for (const auto& pair : m_data)
		{
			if (!pair.second.empty())
				outIds.push_back(pair.first);
		}
	}

	/// Take a lock-free snapshot of all data for use during one frame.
	/// Call once at frame start, use the snapshot for all checks.
	CVisibilitySnapshot TakeSnapshot() const
	{
		CAutoLock lock(m_cs);
		CVisibilitySnapshot snap;
		for (const auto& pair : m_data)
		{
			if (pair.second.empty())
				continue;
			CVisibilitySnapshot::InstanceData& instData = snap.m_data[pair.first];
			instData.states = pair.second;

			// Copy precomputed prefixes
			auto pit = m_prefixes.find(pair.first);
			if (pit != m_prefixes.end())
				instData.parentPrefixes = pit->second;
		}
		return snap;
	}

private:
	/// Rebuild the parent prefix set for an instance after state changes.
	/// For path "1.0.2", adds prefixes "1" and "1.0".
	/// Must be called while lock is held.
	void RebuildPrefixes(const ON_UUID& instanceId)
	{
		auto it = m_data.find(instanceId);
		if (it == m_data.end() || it->second.empty())
		{
			m_prefixes.erase(instanceId);
			return;
		}

		auto& prefixSet = m_prefixes[instanceId];
		prefixSet.clear();

		for (const auto& pair : it->second)
		{
			const std::string& path = pair.first;
			// Add all parent prefixes of this path
			// e.g. for "1.0.2" add "1.0.2", "1.0", "1"
			prefixSet.insert(path);
			size_t pos = path.rfind('.');
			while (pos != std::string::npos)
			{
				std::string prefix = path.substr(0, pos);
				prefixSet.insert(prefix);
				pos = prefix.rfind('.');
			}
		}
	}

	mutable CRITICAL_SECTION m_cs;

	/// instance UUID -> (component path -> state)
	std::unordered_map<ON_UUID,
		std::unordered_map<std::string, ComponentState>,
		ON_UUID_Hash, ON_UUID_Equal> m_data;

	/// instance UUID -> set of parent prefixes for O(1) HasHiddenDescendants
	std::unordered_map<ON_UUID,
		std::unordered_set<std::string>,
		ON_UUID_Hash, ON_UUID_Equal> m_prefixes;
};
