# C++ SDK Deep Dive: SC_DRAWOBJECT & Block Instance Interception

**Research Date:** 2026-02-15  
**Agent:** Research Agent (subagent)  
**Status:** Complete  
**Risk Level:** CRITICAL PATH — this determines project feasibility

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [SC_DRAWOBJECT Deep-Dive](#2-sc_drawobject-deep-dive)
3. [Block Instance Rendering Pipeline](#3-block-instance-rendering-pipeline)
4. [CRhinoCacheHandle & Display Caching](#4-crhinocachehandle--display-caching)
5. [Alternative Approaches](#5-alternative-approaches)
6. [Existing Plugins & Prior Art](#6-existing-plugins--prior-art)
7. [McNeel Discourse Findings](#7-mcneel-discourse-findings)
8. [Risk Assessment & Recommendations](#8-risk-assessment--recommendations)

---

## 1. Executive Summary

### The Critical Question
> Does Rhino's display pipeline fire `SC_DRAWOBJECT` for each sub-object within a block instance, or only once for the block instance as a whole?

### Answer: **Once for the block instance as a whole.**

Based on SDK documentation, code samples, and forum evidence:

- `SC_DRAWOBJECT` fires with `m_pChannelAttrs->m_pObject` pointing to the **`CRhinoInstanceObject`** (the block instance), NOT individual components within it.
- Rhino draws block instances as a unit — it internally iterates definition objects, but the conduit only sees the top-level instance object.
- **This means:** You CAN intercept block instance drawing. You CAN suppress it with `return false`. You CAN then custom-draw individual components from the definition. **This is the viable approach.**

### Confidence Level: **HIGH (85%)**

The approach is architecturally sound. The remaining 15% uncertainty is around:
- Edge cases with nested blocks and display caching
- Selection highlight integration when drawing custom
- Performance at scale with many managed instances

---

## 2. SC_DRAWOBJECT Deep-Dive

### 2.1 How SC_DRAWOBJECT Works in C++

From the official SDK guide ("Highlighting Objects in Conduits"):

```cpp
CTestHighlightCurveConduit::CTestHighlightCurveConduit()
: CRhinoDisplayConduit( CSupportChannels::SC_DRAWOBJECT )
{
}

bool CTestHighlightCurveConduit::ExecConduit(
    CRhinoDisplayPipeline& dp, UINT nChannel, bool& bTerminate)
{
    switch (nChannel)
    {
    case CSupportChannels::SC_DRAWOBJECT:
    {
        // m_pChannelAttrs->m_pObject = the object about to be drawn
        // m_pDisplayAttrs = modifiable display attributes
        if (m_pChannelAttrs->m_pObject->m_runtime_object_serial_number == m_target_sn)
            m_pDisplayAttrs->m_ObjectColor = RGB(255, 105, 180);
    }
    break;
    }
    return true; // true = pipeline draws normally; false = suppress default draw
}
```

**Key behaviors:**
- `return true` → Pipeline proceeds with default drawing of the object
- `return false` → Pipeline **skips** drawing this object entirely
- `m_pDisplayAttrs` can be modified to change color, material, visibility attributes before drawing
- The conduit can call `dp.DrawObject()` or other draw methods to substitute custom drawing

### 2.2 Suppressing and Replacing Block Drawing

The critical pattern for our use case:

```cpp
bool ExecConduit(CRhinoDisplayPipeline& dp, UINT nChannel, bool& bTerminate)
{
    if (nChannel != CSupportChannels::SC_DRAWOBJECT)
        return true;
    
    const CRhinoObject* obj = m_pChannelAttrs->m_pObject;
    
    // Only intercept instance objects (blocks)
    if (!obj || obj->ObjectType() != ON::instance_reference)
        return true;
    
    const CRhinoInstanceObject* iobj = static_cast<const CRhinoInstanceObject*>(obj);
    ON_UUID instanceId = iobj->Id();
    
    // Check if this instance has custom visibility
    if (m_managed_instances.find(instanceId) == m_managed_instances.end())
        return true; // Not managed, draw normally
    
    // Get the block definition
    const CRhinoInstanceDefinition* idef = iobj->InstanceDefinition();
    if (!idef) return true;
    
    ON_Xform xform = iobj->InstanceXform();
    
    // Draw only visible components
    for (int i = 0; i < idef->ObjectCount(); i++)
    {
        const CRhinoObject* component = idef->Object(i);
        ON_UUID componentId = component->Id();
        
        if (IsComponentHidden(instanceId, componentId))
            continue;
        
        // dp.DrawObject handles display mode, materials, colors correctly
        dp.DrawObject(component, &xform);
    }
    
    return false; // Suppress default (complete) block drawing
}
```

### 2.3 `return false` vs C# `DrawObject = false`

| Aspect | C++ `return false` | C# `e.DrawObject = false` |
|--------|-------------------|--------------------------|
| Mechanism | Integrated into pipeline's draw loop | Event flag checked after handler |
| Screen invalidation | Pipeline tracks what was drawn | Pipeline doesn't know about conduit geometry |
| Ghost artifacts | **Should not occur** — pipeline owns the draw | **Occurs** — pipeline invalidation misses conduit draws |
| Display cache | Can use `CRhinoCacheHandle` directly | No cache integration |
| Performance | Native, inline | Managed/native boundary overhead |

**This is WHY C++ is essential.** The C++ `return false` replaces the draw call within the pipeline's own drawing loop, so invalidation, caching, and z-buffering all work correctly. The C# approach draws "on top" which bypasses these systems.

### 2.4 What `dp.DrawObject()` Actually Does

From the API documentation:
> "These routines are primarily used to draw the 'current' object inside of a conduit's SC_DRAWOBJECT channel, exactly the way the pipeline would draw it... in other words, if your conduit doesn't call one of these, then the pipeline will."

`dp.DrawObject(const CRhinoObject*, const ON_Xform*)` handles:
- Current display mode (wireframe/shaded/rendered/artistic)
- Object materials and colors
- "By Parent" / "By Layer" / "By Object" color resolution
- Wire density, edge display settings
- Render meshes for shaded modes
- Curve/surface display density

**This eliminates the need for per-geometry-type drawing logic** that plagued the C# PoC.

### 2.5 The `dp.DrawObject(idef, &xform)` Overload

From the "Dynamically Inserting Blocks" guide, there's a special overload:

```cpp
dp->DrawObject(m_idef, &m_xform); // Draws entire definition with transform
```

This draws the **entire** instance definition. For our use case, we DON'T want this — we want to iterate components individually and skip hidden ones. So we use the per-object overload instead.

---

## 3. Block Instance Rendering Pipeline

### 3.1 How Rhino Internally Draws Block Instances

Based on SDK analysis and the `ActiveObjectNestingLevel` API:

1. **Object iteration:** Pipeline iterates all document objects. For each, fires `SC_DRAWOBJECT`.
2. **Block instances:** When the object is a `CRhinoInstanceObject`, the pipeline (if conduit doesn't suppress) calls its internal block draw routine.
3. **Internal block draw:** Iterates `idef->ObjectCount()` components, applies instance transform, draws each. For nested blocks, recurses.
4. **Conduit sees:** Only the top-level `CRhinoInstanceObject`. Sub-objects in the definition do NOT individually trigger `SC_DRAWOBJECT`.

**Evidence:**
- The `ActiveObjectNestingLevel()` method on `CRhinoDisplayPipeline` returns "nesting level" info — this exists because the pipeline tracks block nesting depth during its internal draw. Conduits at `SC_DRAWOBJECT` level see only level 0 (top-level objects).
- The forum thread from michaelvollrath (June 2024) shows z-fighting when drawing block contents alongside the parent block — confirming the block draws as a unit and conduit-drawn geometry overlaps.
- No SDK sample or documentation shows `SC_DRAWOBJECT` firing for definition sub-objects.

### 3.2 Nested Block Handling

For our approach (iterating definition objects and drawing individually):

```
Block Instance A (managed)
├── Component 1 (Brep) → dp.DrawObject(comp1, &xformA)
├── Component 2 (Curve) → HIDDEN, skip
├── Nested Block B (InstanceObject) → dp.DrawObject(compB, &xformA)  
│   └── B draws as normal block (all sub-objects)
└── Component 4 (Mesh) → dp.DrawObject(comp4, &xformA)
```

When we call `dp.DrawObject(nestedBlockObj, &parentXform)`, Rhino draws the nested block normally (all its sub-objects). This is correct for MVP — nested block visibility is atomic.

For v2 (per-component visibility within nested blocks), we'd need to recursively iterate nested definitions too.

### 3.3 Rhino 8 Sub-Object Selection in Blocks

Rhino 8 added "Sub-object selection selects objects in block instances through nesting levels." This means Rhino 8's display pipeline IS aware of individual objects within blocks for selection purposes. This is encouraging — it means the pipeline's internal infrastructure already deals with per-component identity within blocks.

---

## 4. CRhinoCacheHandle & Display Caching

### 4.1 What CRhinoCacheHandle Is

From the API reference:

```cpp
class CRhinoCacheHandle
{
public:
    CRhinoCacheHandle() = default;
    const CRhinoObject* ParentObject() const;
    void Reset();
    
    std::shared_ptr<class CRhCacheData> m_cache_data;
    std::shared_ptr<class CRhVboCurveSetData> m_vbo_curve_data;
    std::shared_ptr<class CRhVboData> m_vbo_data;
};
```

It stores:
- **VBO (Vertex Buffer Object) data** — GPU-uploaded mesh data
- **VBO curve data** — GPU-uploaded curve data  
- **Generic cache data** — display-mode-specific cached representations

### 4.2 How to Use CRhinoCacheHandle

Many `CRhinoDisplayPipeline::Draw*` methods accept a `CRhinoCacheHandle*`:

```cpp
void DrawBrep(const ON_Brep* brep, const ON_Color& wireColor, 
              int wireDensity, bool edgeAnalysis, CRhinoCacheHandle* cache);
void DrawMesh(const ON_Mesh& mesh, bool wires, bool shaded, CRhinoCacheHandle* cache);
void DrawAnnotation(ON_Annotation& annotation, ON_Color color, 
                    void* updater, CRhinoCacheHandle* cache);
```

**Usage pattern:**
```cpp
// Store cache per component per managed instance
struct ComponentCache {
    CRhinoCacheHandle cache;
    bool dirty = true;
};

// In ExecConduit:
ComponentCache& cc = GetOrCreateCache(instanceId, componentIndex);
if (cc.dirty) {
    cc.cache.Reset(); // Force rebuild
    cc.dirty = false;
}
dp.DrawBrep(brep, wireColor, wireDensity, edgeAnalysis, &cc.cache);
```

### 4.3 Cache Invalidation

Cache handles are automatically invalidated when:
- The geometry changes (new render mesh)
- Display mode changes
- Material assignments change

Manual invalidation via `Reset()` when:
- Visibility state changes (component hidden/shown)
- Block definition is edited
- Document closes

### 4.4 Does `dp.DrawObject()` Use Caching?

**Likely yes, partially.** When calling `dp.DrawObject(component, &xform)` on a definition sub-object, the pipeline will use the component's own cache handles (which are part of `CRhinoObject`). The transform is applied separately. This means we get Rhino's native caching for free when using `dp.DrawObject()`.

**However:** The per-object cache may be shared across all instances of the same definition. Since all instances share the same definition objects, modifying cache state for one instance's custom draw could affect others. This needs testing.

**Recommendation:** Use `dp.DrawObject(component, &xform)` for the initial implementation. If cache conflicts arise, fall back to explicit `DrawBrep`/`DrawMesh` with per-instance `CRhinoCacheHandle` objects.

---

## 5. Alternative Approaches

### 5.1 If SC_DRAWOBJECT Interception Works (PRIMARY — Expected to Work)

As described in §2. This is the primary approach. Confidence: 85%.

### 5.2 Fallback A: SC_PREDRAWOBJECT Hide + SC_POSTDRAWOBJECTS Custom Draw

Similar to the C# approach but with better cache integration:

```cpp
// In SC_PREDRAWOBJECT: suppress the block
m_pChannelAttrs->m_bDrawObject = false;

// In SC_POSTDRAWOBJECTS: draw custom
for (auto& [instanceId, visData] : m_managed) {
    DrawInstanceCustom(dp, instanceId, visData);
}
```

**Pros:** Simpler suppression mechanism.  
**Cons:** Same ghost artifact issues as C# — drawing in POSTDRAWOBJECTS is outside the per-object draw loop, so invalidation won't track it.  
**Verdict:** Not recommended. Use only if `return false` in SC_DRAWOBJECT doesn't work.

### 5.3 Fallback B: Object Replacement ("Fake Blocks")

Instead of intercepting drawing, replace managed block instances with exploded geometry:

1. When user hides a component: Explode the block instance into individual objects
2. Hide the unwanted objects
3. Group the remaining objects and tag them
4. On "show all": Delete the group, re-insert the block instance

**Pros:** Works with zero display pipeline hacking. 100% reliable rendering.  
**Cons:**
- Loses block instance identity (no longer a block)
- Block definition updates won't propagate
- Complex undo/redo handling
- Memory overhead (duplicated geometry)
- Breaks the core block paradigm

**Verdict:** Last resort. Acceptable for PoC validation but not for production.

### 5.4 Fallback C: Custom Display Mode

Create a custom display mode that handles block instances differently:

```cpp
class CPerInstanceVisibilityDisplayMode : public CRhinoDisplayMode
{
    void DrawObject(CRhinoDisplayPipeline& dp, const CRhinoObject* obj) override
    {
        if (obj->ObjectType() == ON::instance_reference && IsManaged(obj))
        {
            DrawCustom(dp, obj);
            return;
        }
        CRhinoDisplayMode::DrawObject(dp, obj);
    }
};
```

**Pros:** Full control over drawing. No conduit needed.  
**Cons:**
- Forces users to use a specific display mode
- Must implement ALL display mode functionality (shading, materials, etc.)
- Very high development cost
- Users can't use their preferred display modes

**Verdict:** Too invasive. Not recommended.

### 5.5 Fallback D: ObjectDecoration / Custom Draw Handler

Some Rhino SDK versions have `CRhinoObject::SetCustomDrawHandler()` or object decoration mechanisms.

**Status:** Not confirmed to exist in Rhino 8 C++ SDK. Needs verification.  
**Verdict:** Unknown feasibility. Low priority investigation.

### 5.6 Fallback E: Ask McNeel

Post on discourse.mcneel.com with the specific use case. Dale Fugier (McNeel SDK developer) is known to be responsive. He may:
- Confirm the SC_DRAWOBJECT approach works
- Suggest a better API
- Expose new functionality if needed

**Verdict:** Should be done regardless, after building the minimal PoC. Having concrete code to show makes the discussion more productive.

---

## 6. Existing Plugins & Prior Art

### 6.1 VisualARQ

**What it is:** BIM plugin for Rhino by Asuni CAD. Creates parametric architectural objects (walls, doors, windows).

**How it handles blocks:** VisualARQ objects are NOT standard Rhino blocks. They are custom `CRhinoObject`-derived types with their own display logic. They use Grasshopper styles to define geometry parametrically.

**Per-instance visibility:** VisualARQ objects support per-instance material assignment and display attributes. They achieve this by being custom objects, not by modifying standard block behavior.

**Relevance to us:** Low. Different approach (custom objects vs. standard blocks).

### 6.2 Elefront

**What it is:** Grasshopper plugin for block management. Provides components to create, modify, and query block instances.

**How it handles blocks:** Elefront works at the definition/instance management level — creating definitions, inserting instances, reading/writing attributes. It does NOT do per-instance component visibility.

**Relevance to us:** Low. No display pipeline tricks.

### 6.3 No Known Plugin Does Per-Instance Component Visibility

After extensive searching, **no existing Rhino plugin implements per-instance hiding of components within a block instance**. This is genuinely novel territory.

The "Per Block Instance Wishes" forum thread (June 2024) confirms this is a community-requested feature that Rhino doesn't natively support. Users reference Revit's "exclude from group" and SolidWorks' "exploded view" as analogous features in other software.

---

## 7. McNeel Discourse Findings

### 7.1 "DisplayConduit - Cannot Hide Block Instance Definition?" (June 2024)

**URL:** discourse.mcneel.com/t/183638  
**Author:** michaelvollrath

**Problem:** User trying to draw block sub-objects in a Python display conduit. Gets z-fighting because the conduit-drawn geometry overlaps with Rhino's own block drawing.

**Key insight:** The user is drawing in `DrawForeground` (equivalent to `SC_DRAWFOREGROUND`), which draws AFTER the main objects. The block still draws normally, then conduit draws on top → z-fighting.

**Relevance:** Confirms that:
1. Block instances draw as a unit in the main draw pass
2. Drawing block components manually from a conduit causes conflicts unless you suppress the original block draw
3. The C# approach (draw in PostDraw/Foreground) is fundamentally flawed for this use case

### 7.2 "Per Block Instance Wishes" (June 2024)

**URL:** discourse.mcneel.com/t/185033  
**Author:** barden00

**Wishes:**
1. Per block instance hide geometries inside the block ← **Our exact use case**
2. Per block instance display attribute overrides
3. Per block instance position translation (exploded views)

**Community response:** Multiple users want this. References to Revit and SolidWorks. No McNeel response indicating plans to implement natively.

**Relevance:** Validates the market need. Confirms Rhino doesn't support this natively. Our plugin fills a real gap.

### 7.3 Related Threads

- **"Block parents, block instances"** (discourse.mcneel.com/t/164219) — Discusses "By Parent" color limitations. Only one color per instance, not per-component. Confirms the limitation we're working around.
- **"Wish: SetObjectDisplayMode and BlockInstances"** (2014, discourse.mcneel.com/t/8497) — Old wish for per-block display modes. Never implemented. Shows long-standing demand.

---

## 8. Risk Assessment & Recommendations

### 8.1 Updated Risk Matrix

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| SC_DRAWOBJECT `return false` doesn't suppress block | **10%** (was 20%) | Project-killing | Build minimal PoC in week 1 |
| Ghost artifacts with C++ approach | **15%** | High — same issue as C# | Should not occur (theory); verify with PoC |
| Selection highlight lost when suppressing draw | **70%** | Medium — usability issue | Check `IsSelected()` and draw highlighted manually |
| Display cache conflicts between instances | **40%** | Medium — performance | Use per-instance CRhinoCacheHandle if needed |
| Nested block handling complexity | **30%** | Medium — scope creep | MVP: atomic nested blocks. v2: recursive |
| Performance with 100+ managed instances | **30%** | Medium — unusable at scale | Profile early; batch identical visibility states |

### 8.2 Recommended Validation Plan

**Week 1 PoC — Must-validate items:**

1. **Test 1:** Create conduit with `SC_DRAWOBJECT`. Does `m_pChannelAttrs->m_pObject` return a `CRhinoInstanceObject` for blocks?
2. **Test 2:** Return `false` for a specific block instance. Does it disappear?
3. **Test 3:** Return `false` AND draw individual components via `dp.DrawObject(component, &xform)`. Does it render correctly?
4. **Test 4:** Move/rotate the block instance after test 3. Any ghost artifacts?
5. **Test 5:** Select the block instance. Does selection highlight work? If not, test manual highlight drawing.
6. **Test 6:** Test in wireframe, shaded, and rendered modes.

**If all 6 tests pass → GREEN LIGHT for full implementation.**

### 8.3 Recommended Architecture

```
SC_DRAWOBJECT handler:
├── Is this a CRhinoInstanceObject? → No → return true (normal draw)
├── Is it in our managed set? → No → return true  
├── Get definition & visibility data
├── For each component in definition:
│   ├── Hidden? → skip
│   └── Visible? → dp.DrawObject(component, &instanceXform)
├── Is instance selected? → Draw selection highlight
└── return false (suppress default draw)
```

### 8.4 Key Implementation Notes

1. **Use `dp.DrawObject(component, &xform)` NOT `dp.DrawBrep/DrawMesh`** — let Rhino handle display mode, materials, and caching.

2. **Thread safety:** `SC_DRAWOBJECT` runs on the display thread. Use `std::shared_mutex` for the managed instances set.

3. **BoundingBox:** Subscribe to `SC_CALCBOUNDINGBOX` as well. When drawing components individually, the bounding box should still encompass the full block instance (even hidden components), so zooming works correctly.

4. **Nested blocks:** When iterating definition objects and finding a `CRhinoInstanceObject`, call `dp.DrawObject()` on it — Rhino will handle the nested block normally.

5. **"By Parent" colors:** When drawing components with `dp.DrawObject()`, need to verify that "By Parent" color resolution works correctly. The component's "By Parent" normally resolves to the instance's layer/color. When drawing manually, this context might be lost. Test early.

---

## Appendix: Key SDK References

| Resource | URL |
|----------|-----|
| CRhinoDisplayConduit class | developer.rhino3d.com/api/cpp/class_c_rhino_display_conduit.html |
| CRhinoDisplayPipeline class | developer.rhino3d.com/api/cpp/class_c_rhino_display_pipeline.html |
| CRhinoCacheHandle class | developer.rhino3d.com/api/cpp/class_c_rhino_cache_handle.html |
| Highlighting Objects in Conduits (guide) | developer.rhino3d.com/guides/cpp/highlighting-objects-in-conduits/ |
| Dynamically Inserting Blocks (guide) | developer.rhino3d.com/guides/cpp/dynamically-inserting-blocks/ |
| Display Conduits overview (RhinoCommon) | developer.rhino3d.com/guides/rhinocommon/display-conduits/ |
| McNeel: Cannot Hide Block Definition | discourse.mcneel.com/t/183638 |
| McNeel: Per Block Instance Wishes | discourse.mcneel.com/t/185033 |
| Rhino 8 C++ SDK download | rhino3d.com/download/rhino-sdk/8/latest/ |
| SDK samples (GitHub) | github.com/mcneel/rhino-developer-samples |

---

*Research complete. Confidence in primary approach: HIGH. Recommend proceeding to C++ PoC immediately.*
