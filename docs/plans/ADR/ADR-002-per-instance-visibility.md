# ADR-002: Per-Instance Component Visibility via SC_DRAWOBJECT

**Status:** Accepted (pending validation in Sprint 3)  
**Date:** 2026-02-15  
**Context:** The plugin's killer feature — hiding individual components within a single block instance without affecting other instances of the same definition.

## Decision

Use C++ `CRhinoDisplayConduit` on `SC_DRAWOBJECT` channel to intercept block instance drawing and replace it with per-component drawing that skips hidden components.

## Mechanism

```
Normal Rhino draw: BlockInstance → draw all definition objects with instance transform
Our conduit:       BlockInstance → check managed set → if managed:
                     return false (suppress normal draw)
                     iterate definition objects
                     skip hidden components (UUID lookup in ON_UserData)
                     draw remaining via dp.DrawObject(comp, &xform)
```

### Why SC_DRAWOBJECT (not SC_PREDRAWOBJECT)

| Channel | Behavior | Ghost Artifacts? |
|---------|----------|-----------------|
| SC_PREDRAWOBJECT + custom draw | Draws before object, must suppress separately | Yes — invalidation mismatch |
| SC_DRAWOBJECT + return false | Replaces object's draw call in-pipeline | No — pipeline tracks correctly |

### State Management

**Per-instance state:** `CComponentVisibilityData : ON_UserData` attached to `InstanceObject`.

```cpp
class CComponentVisibilityData : public ON_UserData {
    ON_UuidList m_hidden_component_ids;  // UUIDs, not indices
};
```

**Runtime state:** `CPerInstanceVisibilityConduit` maintains:

```cpp
std::unordered_map<ON_UUID, InstanceDrawCache, UUIDHash> m_cache;
// Only instances with hidden components are in this map.
// Unmanaged instances: zero overhead (O(1) HashSet miss).
```

### Selection Highlight

When we suppress the normal draw, we also suppress Rhino's selection highlight. Fix:

```cpp
bool isSelected = (m_pChannelAttrs->m_pObject->IsSelected() != 0);
if (isSelected) {
    ON_Color selColor = RhinoApp().AppSettings().SelectedObjectColor();
    DrawComponentHighlighted(dp, comp, xform, selColor);
}
```

Must handle both normal pass and highlighted-object pass (Rhino draws highlighted objects separately).

### Display Cache

Use `CRhinoCacheHandle` per managed instance to avoid re-processing geometry every frame:

```cpp
dp.DrawObject(comp, &xform, nullptr, cache);  // Rhino reuses GPU-uploaded mesh
```

Cache invalidation triggers:
- Block definition changed → clear caches for that definition
- Visibility state changed → mark instance cache dirty
- Document close → clear all

### UUID vs Index Addressing

**Decision: UUIDs only.** The C# PoC used `int componentIndex` — this breaks when definitions are edited (objects reordered/deleted). UUIDs survive definition edits.

On definition change events, validate stored UUIDs against current definition and remove stale entries.

### Nested Blocks

**v2.0:** Treat nested blocks atomically — hide/show the entire nested instance.  
**Future:** Path-based addressing: `[instance_uuid, nested_instance_uuid, component_uuid]`.

## Risk: SC_DRAWOBJECT May Not Work for Blocks

**Probability:** 20% (low but project-critical)  
**Validation:** Sprint 3, Task 1 — minimal C++ PoC that intercepts a block and draws it shifted.

**Fallbacks (in order):**
1. SC_PREDRAWOBJECT hide + SC_POSTDRAWOBJECTS custom draw (with cache integration)
2. `CRhinoObject::SetCustomDrawHandler()` if available
3. Ask McNeel (Steve Baer, developer forum)

## Consequences

- Zero overhead for non-managed instances (HashSet miss)
- Managed instances: O(k) per frame where k = component count — acceptable with dp.DrawObject caching
- Feature degrades gracefully if C++ plugin not loaded (C# provides instance-level visibility only)
