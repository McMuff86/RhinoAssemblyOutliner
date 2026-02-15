# ADR-003: Data Persistence Strategy

**Status:** Accepted  
**Date:** 2026-02-15

## Decision

Use three persistence mechanisms, each for its appropriate scope:

| Data | Mechanism | Scope | Why |
|------|-----------|-------|-----|
| Hidden components per instance | `ON_UserData` (C++) | Per-object in .3dm | Travels with copy/paste/import; auto-serialized; lives on the object it describes |
| Assembly root selection | `RhinoDoc.Strings` (UserText) | Per-document | Simple key-value; persists in .3dm; human-readable |
| Named visibility states (v2) | `DocumentData` (C#) | Per-document | Complex structure (JSON map); persists in .3dm |
| UI preferences | `Plugin.Settings` | Per-user global | Panel size, default mode — not document-specific |

## ON_UserData for Component Visibility

```cpp
class CComponentVisibilityData : public ON_UserData {
    ON_UuidList m_hidden_component_ids;
    
    bool Archive() const override { return true; }  // Persist to .3dm
    bool Write(ON_BinaryArchive& archive) const override {
        return archive.WriteArray(m_hidden_component_ids);
    }
    bool Read(ON_BinaryArchive& archive) override {
        return archive.ReadArray(m_hidden_component_ids);
    }
};
```

**Why ON_UserData over UserText:**
- Binary serialization (faster, smaller for UUID lists)
- Travels with copy/paste automatically
- No string parsing/generation for UUID arrays
- Native C++ integration (no cross-boundary serialization for persistence)

**Why UUID addressing (not index):**
- Block definition edits reorder/delete objects, shifting indices
- UUIDs are stable across edits
- On definition change: validate stored UUIDs, prune stale entries
- Store component name alongside UUID as fallback match heuristic

## Document UserText for Assembly Root

```csharp
// Save
doc.Strings.SetString("RAO_AssemblyRoot", rootId.ToString());
// Load
var str = doc.Strings.GetValue("RAO_AssemblyRoot");
Guid.TryParse(str, out var rootId);
```

Simple, inspectable, adequate for single-value storage.

## Named Visibility States (v2)

```csharp
// Stored as DocumentData — complex nested structure
public class VisibilityStatesData : DocumentData {
    // stateName → { instanceId → Set<hidden componentIds> }
    public Dictionary<string, Dictionary<Guid, HashSet<Guid>>> States;
}
```

Too complex for UserText key-value pairs. DocumentData provides structured persistence that still saves to .3dm.

## Consequences

- Component visibility data survives file round-trips, copy/paste, import/export
- No external mapping tables to maintain — data lives on the object
- C++ owns persistence for visibility (no cross-boundary serialization overhead)
- C# reads visibility state via P/Invoke queries (RAO_IsComponentHidden, RAO_GetHiddenComponentIds)
