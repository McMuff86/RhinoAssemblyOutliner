# Think Tank 3: C++ Conduit Improvement Strategy

> Date: 2026-02-15 | Sprint 1 Refactor  
> Purpose: Guide the Code Agent on conduit improvements  
> Status: Plan — not yet implemented

---

## 1. Current Conduit Analysis

### What Works Well

- **Path-based filtering** is clean and correct. Dot-separated index strings (`"0"`, `"1.0"`, `"1.0.2"`) naturally model the block hierarchy and allow per-instance control of any component at any nesting depth.
- **`dp.DrawObject(pComponent, &xform)`** is the right call — it delegates to Rhino's full rendering path (materials, display modes, render meshes, caching) instead of re-implementing geometry drawing.
- **Thread safety** via `CRITICAL_SECTION` + RAII `CAutoLock` is solid. The lock granularity (per-call) is fine for current scale.
- **Recursive nested block handling** (`DrawNestedFiltered`) correctly stacks transforms and short-circuits when no hidden descendants exist.
- **Managed-instance check** at the top of `ExecConduit` means unmanaged objects pass through with zero overhead — only a UUID hash lookup.

### Gaps and Problems

| Gap | Severity | Detail |
|-----|----------|--------|
| **No `SC_CALCBOUNDINGBOX`** | Medium | If all visible components are smaller than the full instance bbox, zoom-extents may clip. Not critical now but will bite with isolate/suppress. |
| **Selection highlight is catastrophic** | High | Manual edge extraction via `DuplicateCurve()` + `Transform()` per edge per frame. Allocates and deletes heap memory every frame. O(edges) per managed instance. Completely breaks on complex geometry (>1000 edges). |
| **No `CRhinoCacheHandle` usage** | Medium | Currently relies on Rhino's internal caching via `DrawObject`, which is fine. But selection highlight draws are uncached raw geometry — expensive. |
| **No color/transparency overrides** | Feature gap | Can't do per-instance component color overrides, transparency, or wireframe display mode. |
| **No suppress (structural hide)** | Feature gap | Only visual hide exists. No way to exclude from BOM/mass/export. |
| **`HasHiddenDescendants` is O(n)** | Low-Medium | Linear scan over all hidden paths per call. Fine for <100 hidden paths per instance, but scales poorly. |
| **No pre-highlight (hover)** | Feature gap | SolidWorks shows subtle highlight on tree hover. We have nothing. |
| **String allocations in hot path** | Low | `std::to_string(i)` and `BuildPath()` allocate on heap every frame per component. |
| **Lock contention potential** | Low | Each `IsComponentHidden` call acquires the lock. In a scene with 50 managed instances × 20 components = 1000 lock acquisitions per frame. |

---

## 2. SolidWorks Lessons Applied to Rhino

### 2.1 Display States (Named Visibility Presets)

**SW Concept:** A Display State stores show/hide, color, transparency, and display mode for every component. Multiple Display States per configuration.

**Rhino Implementation:**

```cpp
// New structure in VisibilityData.h
struct DisplayState {
    std::string name;
    std::unordered_set<std::string> hiddenPaths;
    std::unordered_map<std::string, ON_Color> colorOverrides;    // path → color
    std::unordered_map<std::string, float> transparencyOverrides; // path → alpha [0..1]
    // Future: displayMode overrides (wireframe, shaded, etc.)
};

// CVisibilityData stores:
// instance UUID → active display state name
// instance UUID → map<stateName, DisplayState>
```

**API surface** (new P/Invoke functions):
- `CreateDisplayState(instanceId, name)` → copies current state as named preset
- `ActivateDisplayState(instanceId, name)` → swaps active hidden/color/transparency sets
- `DeleteDisplayState(instanceId, name)`
- `ListDisplayStates(instanceId, outNames, outCount)`

**Persistence:** Extend `CComponentVisibilityData` (ON_UserData) archive format to v2, add chunked display state data. v1 files load as a single "Default" display state.

**Effort:** 3–4 days (data model + API + persistence migration + C# UI)

### 2.2 Suppress vs Hide

**SW Concept:** Suppress removes from memory entirely — no mates, no BOM, no mass. Hide is visual only.

**Rhino Mapping:**

| Concept | Implementation |
|---------|---------------|
| **Hide** (visual) | Current `SetComponentHidden` — conduit skips draw, object remains in doc |
| **Suppress** (structural) | New state flag per path. Suppressed components are excluded from: export selection, bounding box, and reported to C# as "suppressed" so `BlockInfoService` can exclude from counts/BOM |

Suppress doesn't truly unload geometry in Rhino (block definitions are shared), but we can:
1. Skip in conduit draw (same as hide)
2. Skip in `SC_CALCBOUNDINGBOX` (suppressed components don't affect bbox)
3. Report suppressed state to C# for BOM/export filtering
4. Show distinct icon state in tree (grayed + strikethrough vs. faded)

```cpp
// In VisibilityData, change storage:
enum class ComponentState : uint8_t {
    Visible = 0,
    Hidden = 1,      // visual only — in BOM, in bbox
    Suppressed = 2   // structural — excluded from BOM, bbox, export
};

// Replace unordered_set<string> with:
std::unordered_map<std::string, ComponentState> componentStates;
```

**Effort:** 2 days (data model change + API + conduit bbox logic + C# icon states)

### 2.3 Transparent/Wireframe Per-Component Rendering

**SW Concept:** Each component in a Display State can be independently set to transparent, wireframe, or hidden-lines-removed.

**Rhino Implementation Strategy:**

For **transparency**, we can't simply set alpha on `m_pDisplayAttrs->m_ObjectColor` because `DrawObject()` uses the object's own material pipeline. Instead:

```cpp
// Option A: Use CDisplayPipelineAttributes override (if DrawObject respects it)
// — Needs testing. DrawObject(obj, &xform) may ignore pipeline attrs.

// Option B: Draw shaded with modified material
//   1. Get the object's render material
//   2. Clone it, set transparency
//   3. dp.DrawShadedBrep(brep, modifiedMaterial)
//   — Problem: only works for BReps, need mesh path too

// Option C (Recommended): Draw object normally, then overdraw transparent
//   1. dp.DrawObject(pComponent, &xform)  — normal draw
//   2. If transparent: enable blending, draw again with alpha material
//   — Double-draw but correct and simple
```

For **wireframe per-component**, draw only edges instead of calling `DrawObject`:

```cpp
void DrawComponentWireframe(CRhinoDisplayPipeline& dp, 
                            const CRhinoObject* pComp, 
                            const ON_Xform& xform, 
                            ON_Color wireColor)
{
    // Use dp.DrawWireframeObject — if available
    // Otherwise: dp.DrawObject with display mode override
    CDisplayPipelineAttributes da;
    da.m_bShadeSurface = false;  // wireframe only
    // This needs investigation — Rhino's API may or may not support this cleanly
}
```

**Recommendation:** Start with transparency only (Option C). Wireframe per-component requires deeper pipeline investigation.

**Effort:** Transparency: 2 days. Wireframe: 3 days (includes API research).

### 2.4 Component Color Overrides Per Instance

**SW Concept:** Each instance can override the color of any component, independent of the definition's color.

**Implementation:**

```cpp
// In VisibilityData (or the DisplayState struct):
std::unordered_map<std::string, ON_Color> m_colorOverrides; // path → color

// In conduit, before drawing a component:
auto colorIt = colorOverrides.find(path);
if (colorIt != colorOverrides.end())
{
    dp.PushObjectColor(colorIt->second);
    dp.DrawObject(pComponent, &xform);
    dp.PopObjectColor();
}
else
{
    dp.DrawObject(pComponent, &xform);
}
```

**Important:** `PushObjectColor` / `PopObjectColor` should work with `DrawObject` — but needs verification. If not, we fall back to `DrawShadedBrep` with a custom material, which is more work but guaranteed.

**API:**
- `SetComponentColorOverride(instanceId, path, r, g, b, a)` → sets override
- `ClearComponentColorOverride(instanceId, path)` → removes override
- `ClearAllColorOverrides(instanceId)` → resets instance

**Effort:** 1.5 days (data + conduit + API). Risk: `PushObjectColor` behavior with `DrawObject` needs testing.

---

## 3. Performance Optimization Plan

### 3.1 CRhinoCacheHandle Integration

**Current state:** No explicit caching. `DrawObject(pComponent, &xform)` uses Rhino's internal object caching, which is fine for the normal draw path.

**Where caching matters:**
1. **Selection highlight** — currently uncached, re-extracts edges every frame
2. **Wireframe overrides** — if we draw wireframe manually, cache the curves
3. **Ghosted/transparent re-draw** — if we double-draw for transparency

**Implementation plan:**

```cpp
// Per-managed-instance cache structure
struct InstanceDrawCache {
    // One CRhinoCacheHandle per component for selection highlights
    std::vector<CRhinoCacheHandle> componentCaches;
    bool valid = false;
    
    void Invalidate() { 
        for (auto& c : componentCaches) c.Reset();
        valid = false; 
    }
};

// Store in conduit or VisibilityData:
std::unordered_map<ON_UUID, InstanceDrawCache, ON_UUID_Hash, ON_UUID_Equal> m_caches;
```

**Invalidation triggers:**
- Component visibility change → invalidate that instance's cache
- Object modification event → invalidate
- Display mode change → Rhino handles this internally for `DrawObject`, but our custom draws need manual invalidation

**Effort:** 2 days

### 3.2 Hash-Based Lookups in SC_DRAWOBJECT

**Current approach:** In `ExecConduit`, we check `pObject->ObjectType() == ON::instance_reference`, then look up the UUID in `m_visData`. This is already O(1) via `unordered_map`. Good.

**Bottleneck:** The inner loop calls `IsComponentHidden(instanceId, path)` per component, each acquiring the critical section. For 20 components × 50 managed instances = 1000 lock acquisitions per frame.

**Optimization: snapshot pattern**

```cpp
// At the start of SC_DRAWOBJECT for a managed instance, take a snapshot:
thread_local std::unordered_set<std::string> t_hiddenSnapshot;

// In ExecConduit, after confirming managed:
m_visData.GetHiddenPaths(instanceId, t_hiddenSnapshot); // one lock acquisition

// Then check t_hiddenSnapshot.count(path) — no locking needed
```

This reduces lock acquisitions from N (components) to 1 (per managed instance per frame).

**`HasHiddenDescendants` optimization:** Pre-compute a prefix set. When hidden paths change, build a set of all prefixes (e.g., hiding "1.0.2" adds "1" and "1.0" to a prefix set). Then `HasHiddenDescendants` becomes O(1) lookup instead of O(n) scan.

```cpp
// In CVisibilityData, maintain alongside m_data:
std::unordered_map<ON_UUID, std::unordered_set<std::string>, ...> m_prefixes;

// When adding hidden path "1.0.2":
//   m_prefixes[id].insert("1");
//   m_prefixes[id].insert("1.0");
// HasHiddenDescendants just checks m_prefixes[id].count(prefix)
```

**Effort:** 1 day

### 3.3 Frame Budget Analysis

At 30fps, each frame has **33ms**. The display pipeline shares this with all conduits, Rhino's own drawing, and GPU work. Our conduit budget should be **<2ms** for the CPU-side logic (excluding GPU draw calls which are deferred).

**Current cost estimates per managed instance:**
- UUID lookup: ~50ns (hash + compare)
- Hidden path check per component: ~100ns (hash + string compare + lock)
- `DrawObject` call: ~1–5µs (Rhino internal dispatch, cached geometry)
- Total per managed instance (20 components): ~25–120µs

**For 100 managed instances:** 2.5–12ms. This is within budget but tight.

**With snapshot optimization:** Lock once per instance → ~15–80µs per instance → 1.5–8ms total. Comfortable.

**Selection highlight cost (current):**
- Edge duplication + transform: ~500µs per component (depends on complexity)
- For 20 components with average 100 edges each: ~10ms per selected managed instance
- **This is the main performance problem.** A single complex selected instance can blow the frame budget.

### 3.4 Benchmark Targets

| Scenario | Target | Current (estimated) |
|----------|--------|-------------------|
| 100 managed instances, 20 comps each, no selection | >60fps | ~50-60fps ✓ |
| 100 managed instances, 20 comps each, 1 selected | >30fps | ~15-25fps ✗ |
| 100 managed instances, 20 comps each, 5 selected | >30fps | ~5-10fps ✗✗ |
| 500 managed instances, 20 comps each, no selection | >30fps | ~20-30fps ⚠ |
| Nested 3-deep, 50 instances, 10 comps each level | >30fps | ~30-40fps ⚠ |

**Critical path for optimization:** Selection highlight is the #1 performance blocker.

---

## 4. Selection Highlight Improvements

### 4.1 Current Problems

The current selection highlight code in `ExecConduit` is deeply flawed:

1. **Heap allocation per edge per frame** — `DuplicateCurve()` + `delete` for every BRep edge
2. **CPU-side transform** — transforming every curve/point on CPU instead of using GPU transform
3. **No caching** — re-extracts edges from BRep topology every single frame
4. **Incomplete** — only handles `ON_Brep`, `ON_Mesh`, and `ON_Extrusion`. Misses `ON_SubD`, `ON_NurbsCurve`, etc.
5. **Extrusion conversion** — `pExtr->BrepForm()` allocates a full BRep just to get edges, then deletes it. Extremely wasteful.
6. **Wrong channel** — doing highlight work inside `SC_DRAWOBJECT` means it's interleaved with normal drawing instead of being a separate pass.

### 4.2 Recommended Approach

**Move selection highlight to `SC_POSTDRAWOBJECTS`:**

```cpp
// Constructor: add channel
CVisibilityConduit::CVisibilityConduit(CVisibilityData& visData)
    : CRhinoDisplayConduit(
        CSupportChannels::SC_CALCBOUNDINGBOX |
        CSupportChannels::SC_DRAWOBJECT |
        CSupportChannels::SC_POSTDRAWOBJECTS)
    , m_visData(visData)
{}

// In ExecConduit:
case CSupportChannels::SC_POSTDRAWOBJECTS:
{
    DrawSelectionHighlights(dp);
    break;
}
```

**Why `SC_POSTDRAWOBJECTS` not `SC_DRAWOVERLAY`?**
- `SC_POSTDRAWOBJECTS` has depth testing ON — highlights correctly occlude behind other geometry
- `SC_DRAWOVERLAY` draws on top of everything — wrong for selection highlights that should respect depth

**Better highlight strategy — use `DrawObject` with color override:**

```cpp
void CVisibilityConduit::DrawSelectionHighlights(CRhinoDisplayPipeline& dp)
{
    // Iterate managed instances that are selected
    for (const auto& [instanceId, hiddenPaths] : m_cachedSnapshots)
    {
        const CRhinoObject* pObj = FindObjectById(instanceId);
        if (!pObj || !pObj->IsSelected())
            continue;
            
        // Option A: Use dp.DrawObject with wireframe display attrs
        // This leverages Rhino's own highlighting path
        
        // Option B: Use dp.ObjectAt() if conduit provides it
        
        // Option C: Push wireframe attrs and re-draw visible components
        // with Rhino's selection color
        dp.EnableDepthWriting(false);  // don't overwrite z-buffer
        ON_Color selColor = RhinoApp().AppSettings().SelectedObjectColor();
        
        const auto* pInst = static_cast<const CRhinoInstanceObject*>(pObj);
        const auto* pDef = pInst->InstanceDefinition();
        ON_Xform xform = pInst->InstanceXform();
        
        for (int i = 0; i < pDef->ObjectCount(); i++)
        {
            std::string path = std::to_string(i);
            if (hiddenPaths.count(path)) continue;
            
            const CRhinoObject* pComp = pDef->Object(i);
            if (!pComp || !pComp->IsVisible()) continue;
            
            // Draw wireframe overlay with selection color
            dp.PushObjectColor(selColor);
            // Use DrawWireframe or draw with wireframe display mode
            dp.DrawObject(pComp, &xform);  // TODO: force wireframe mode
            dp.PopObjectColor();
        }
        
        dp.EnableDepthWriting(true);
    }
}
```

**Even better — investigate `CRhinoDisplayPipeline::DrawHighlightedObject()`:**
Rhino may have an internal method for this. Check the SDK headers for `DrawHighlighted*` methods.

### 4.3 Pre-Highlight (Hover)

For hover highlighting (tree node mouseover → viewport feedback):

1. C# sends hovered component path to C++ via new API: `SetHoveredComponent(instanceId, path)`
2. Conduit stores the hovered component info (single path, not a set)
3. In `SC_POSTDRAWOBJECTS`, draw the hovered component with a subtle highlight (lighter color, thinner wireframe)
4. On mouse-leave, C# calls `ClearHoveredComponent()`

**Debounce:** C# should debounce hover events (~50ms) to avoid flooding the native side.

**Effort:** Highlight rework: 3 days. Pre-highlight: 1 day.

---

## 5. Nested Block Challenges

### 5.1 Transform Stacking

Current implementation in `DrawNestedFiltered`:

```cpp
ON_Xform combinedXform = parentXform * pNestedInstance->InstanceXform();
```

This is **correct** — Rhino uses row-vector convention, and transforms compose left-to-right (parent * child). The instance xform transforms from definition space to parent space, and `parentXform` transforms from parent space to world space.

**Potential issue:** If a nested block's `InstanceXform()` includes scale, and we later need to draw wireframe with fixed-width lines, the line thickness will scale. This is cosmetic but noticeable.

**Verification needed:** Test with non-uniform scale instances (stretch in one axis). Confirm that `DrawObject(pComp, &combinedXform)` handles this correctly — it should, since Rhino applies the xform to the geometry.

### 5.2 Path-Based Addressing: String vs Int Array

**Current:** Dot-separated strings — `"1.0.2"`.

**Alternative:** `std::vector<int>` or fixed-size `std::array<uint16_t, MAX_DEPTH>`.

**Analysis:**

| Aspect | String (`"1.0.2"`) | Int Array (`{1, 0, 2}`) |
|--------|---------------------|--------------------------|
| Hash cost | ~15ns (FNV on 5 chars) | ~5ns (combine 3 ints) |
| Comparison | ~5ns (memcmp 5 bytes) | ~3ns (memcmp 6 bytes) |
| Memory | 5 bytes + SSO (usually stack) | 6 bytes + vector overhead (heap) |
| Construction | `std::to_string` + concat (~50ns) | push_back × 3 (~10ns) |
| Prefix check | `string::compare(0, n, prefix)` (clean) | Compare first N elements (clean) |
| Debug readability | Excellent | Requires formatting |
| P/Invoke marshalling | Trivial (`const char*`) | Complex (pointer + length) |
| Existing code impact | Zero (current format) | Rewrite all path logic |

**Verdict:** Keep strings. The performance difference is negligible (<50ns per operation) and strings are far easier to debug, marshal across P/Invoke, and persist. The `HasHiddenDescendants` prefix optimization (§3.2) eliminates the main string-scanning bottleneck.

If profiling later shows path operations as a hotspot (unlikely), introduce a `PathId` numeric hash for the inner loop while keeping strings for the API surface.

### 5.3 Shared Definitions — Per-Instance State

**Scenario:** Block definition "Motor" has 5 components. Instance A hides component[0], Instance B shows all components.

**Current behavior:** Correct. `CVisibilityData` keys on `instanceId` (UUID), not definition ID. Instance A's hidden set = `{"0"}`, Instance B has no entry → fully visible. The conduit checks per-instance.

**Edge case — definition modification:** If the user edits the block definition (adds/removes/reorders components), all path indices shift. Current behavior: paths become stale. Component[0] used to be "Bolt" but is now "Nut" after reorder.

**Mitigation options:**
1. **Listen for `ON_InstanceDefinition::Modified` events** → clear all managed instances of that definition → user must re-hide
2. **Use object UUIDs instead of indices** for paths → more robust but definition objects have unstable UUIDs across edits
3. **Hybrid:** Use indices for performance, validate on definition change events, warn user

**Recommendation:** Option 1 for now — it's simple and correct. Definition edits are rare in assembly workflows. Add a `DocEventHandler` hook for `OnModifyObjectAttributes` or `ReplaceObject` that detects definition changes.

**Effort:** 0.5 days

---

## 6. Concrete Improvement Tasks (Priority Order)

### P0 — Critical (Sprint 1)

| # | Task | Effort | Impact |
|---|------|--------|--------|
| 1 | **Remove selection highlight from SC_DRAWOBJECT** — delete the entire edge-extraction block. Move to SC_POSTDRAWOBJECTS with `DrawObject`-based highlight (see §4.2). Eliminates heap allocs per frame. | 2 days | Fixes frame drops on selection |
| 2 | **Add SC_CALCBOUNDINGBOX channel** — include managed instances' bboxes. Simple: iterate managed IDs, get object bbox, union. | 0.5 days | Fixes zoom-extents clipping |
| 3 | **Snapshot pattern for lock reduction** — `GetHiddenPaths` once per instance, use `thread_local` snapshot (see §3.2) | 0.5 days | Reduces lock contention 20× |

### P1 — High (Sprint 2)

| # | Task | Effort | Impact |
|---|------|--------|--------|
| 4 | **Per-instance color overrides** — add `colorOverrides` map, `PushObjectColor`/`PopObjectColor` in draw path, new API functions (§2.4) | 1.5 days | SW feature parity |
| 5 | **HasHiddenDescendants prefix optimization** — maintain prefix set on write, O(1) lookup (§3.2) | 0.5 days | Scales nested blocks |
| 6 | **Pre-highlight (hover) support** — single hovered path, subtle wireframe in SC_POSTDRAWOBJECTS, C# debounced API (§4.3) | 1.5 days | UX polish, SW parity |
| 7 | **Definition change detection** — hook `OnReplaceObject`/`OnModifyObject` for instance definitions, invalidate stale paths (§5.3) | 0.5 days | Correctness |

### P2 — Medium (Sprint 3)

| # | Task | Effort | Impact |
|---|------|--------|--------|
| 8 | **Suppress vs Hide states** — `ComponentState` enum, bbox exclusion, API extension, C# icon differentiation (§2.2) | 2 days | SW feature parity |
| 9 | **Per-component transparency** — investigate `DrawObject` with alpha override, implement Option C if needed (§2.3) | 2 days | Isolate mode enhancement |
| 10 | **CRhinoCacheHandle for custom draws** — cache highlight wireframes and transparency re-draws (§3.1) | 2 days | Performance for complex geo |
| 11 | **Display States** — named presets with hide/color/transparency snapshots, persistence v2 (§2.1) | 3–4 days | Power feature |

### P3 — Low (Backlog)

| # | Task | Effort | Impact |
|---|------|--------|--------|
| 12 | **Wireframe per-component display mode** — force wireframe for specific components within shaded view | 3 days | Nice-to-have |
| 13 | **String allocation elimination** — pre-compute path strings for common depths (0-99), use `string_view` where possible | 1 day | Micro-optimization |
| 14 | **Benchmark harness** — automated test with N instances × M components, measure frame times | 1 day | Regression prevention |
| 15 | **SRWLOCK migration** — replace `CRITICAL_SECTION` with `SRWLOCK` for reader/writer distinction (render reads, UI writes) | 0.5 days | Better concurrency |

---

## Summary

The conduit architecture is fundamentally sound. The path-based per-instance model correctly maps to the assembly-component hierarchy. The three critical improvements are:

1. **Fix selection highlighting** (P0-1) — current approach is architecturally wrong and kills performance
2. **Add bounding box channel** (P0-2) — trivial fix, prevents visual bugs
3. **Reduce lock contention** (P0-3) — simple snapshot pattern, big scalability win

Everything else builds on these foundations. Color overrides and pre-highlight are the highest-value feature additions. Display States is the most ambitious feature but should wait until the rendering foundation is solid.
