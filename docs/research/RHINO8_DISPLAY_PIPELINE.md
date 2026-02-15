# Rhino 8 C++ Display Pipeline — Research

## Architecture Overview

Rhino's display pipeline (`CRhinoDisplayPipeline`) is a large, complex class that handles all rendering from 3D model to 2D pixels. It supports multiple backends:

- **OpenGL** (default on Windows/Linux)
- **Metal** (macOS)
- **DirectX** (Windows alternative)
- **Vulkan** (future)
- **Software** (fallback)

Plugins should NOT derive from `CRhinoDisplayPipeline`. Instead, use **Display Conduits** (`CRhinoDisplayConduit`) to hook into the pipeline at specific channels.

## Display Conduit Channels

Conduits subscribe to one or more channels via bitmask in their constructor. During each frame, the pipeline raises events in this order:

### Channel Execution Order

| Order | Channel (C++) | RhinoCommon Override | Purpose |
|-------|---------------|---------------------|---------|
| 1 | — | `ObjectCulling` | Create list of objects to draw (culling) |
| 2 | `SC_CALCBOUNDINGBOX` | `CalculateBoundingBox` | Determine scene extents for clipping |
| 3 | — | `CalculateBoundingBoxZoomExtents` | Tighter bbox for Zoom Extents command |
| 4 | `SC_PREDRAWOBJECTS` | `PreDrawObjects` | Draw before all objects (depth on) |
| 5 | `SC_DRAWOBJECT` | `PreDrawObject` | Called per-object, before each object draws |
| 6 | `SC_POSTDRAWOBJECTS` | `PostDrawObjects` | Draw after all non-highlighted objects (depth on) |
| 7 | — | `DrawForeground` | Draw with depth writing/testing OFF |
| 8 | `SC_DRAWOVERLAY` | `DrawOverlay` | Feedback/temporary geometry on top of everything |

### Critical Channels for Our Plugin

#### `SC_CALCBOUNDINGBOX`
- **Must** include bounding boxes of any custom-drawn geometry
- If your conduit draws geometry outside the document's bbox, it will be z-clipped without this
- Implementation: `m_pChannelAttrs->m_BoundingBox.Union(yourBBox)`

#### `SC_DRAWOBJECT` (Most Important for Us)
- Called **once per object** in the draw list
- Access to `m_pChannelAttrs->m_pObject` — the current object being drawn
- Access to `m_pDisplayAttrs` — can override display attributes:
  - `m_ObjectColor` — override the object's draw color
  - `m_bVisible` — can set to false to **skip drawing** this object
  - Other display attributes (line width, etc.)
- If your conduit doesn't call `DrawObject()`, the pipeline draws normally
- If you call `m_pChannelAttrs->m_bDrawObject = false`, the pipeline **skips** the object

**This is how we implement per-component visibility and highlighting:**
```cpp
case CSupportChannels::SC_DRAWOBJECT:
{
    const CRhinoObject* obj = m_pChannelAttrs->m_pObject;
    
    // Hide specific objects
    if (ShouldHide(obj))
        m_pChannelAttrs->m_bDrawObject = false;
    
    // Override color for highlighting
    if (ShouldHighlight(obj))
        m_pDisplayAttrs->m_ObjectColor = RGB(255, 165, 0); // orange
    
    // Make transparent
    if (ShouldBeTransparent(obj))
        m_pDisplayAttrs->m_ObjectColor = SetAlpha(m_pDisplayAttrs->m_ObjectColor, 128);
}
```

#### `SC_POSTDRAWOBJECTS`
- Draw custom overlays after all objects (selection highlights, ghosted geometry, etc.)
- Depth writing/testing still ON — objects properly occlude
- Good for drawing wireframe outlines of hidden objects

#### `SC_DRAWOVERLAY`
- Draws on top of everything (no depth test)
- Good for UI elements, labels, selection boxes

### C++ Conduit Example

```cpp
class CAssemblyOutlinerConduit : public CRhinoDisplayConduit
{
public:
    CAssemblyOutlinerConduit()
        : CRhinoDisplayConduit(
            CSupportChannels::SC_CALCBOUNDINGBOX |
            CSupportChannels::SC_DRAWOBJECT |
            CSupportChannels::SC_POSTDRAWOBJECTS)
    {}

    bool ExecConduit(
        CRhinoDisplayPipeline& dp,
        UINT nChannel,
        bool& bTerminate) override
    {
        switch (nChannel)
        {
        case CSupportChannels::SC_CALCBOUNDINGBOX:
            // Include hidden objects' bboxes if we draw ghosted versions
            break;

        case CSupportChannels::SC_DRAWOBJECT:
        {
            const CRhinoObject* obj = m_pChannelAttrs->m_pObject;
            // Check if this object should be hidden/highlighted/transparent
            break;
        }

        case CSupportChannels::SC_POSTDRAWOBJECTS:
            // Draw ghosted wireframes of hidden objects
            // Draw selection highlights
            break;
        }
        return true;  // ALWAYS return true
    }
};
```

## CRhinoDisplayPipeline Drawing Methods

### Object Drawing
- `DrawObject(const CRhinoObject*)` — draw a Rhino object with its attributes
- `DrawObject(const CRhinoInstanceDefinition*, const ON_Xform*)` — **draw a block definition with transform** (key for our use case!)
- `DrawObjects(ObjectArray, CDisplayPipelineAttributes*)` — batch draw

### Geometry Primitives
- `DrawBrep(const ON_Brep&, color, wireDensity)` — draw BRep wireframe
- `DrawBrep(const ON_Brep*, color, wireDensity, edgeAnalysis, CRhinoCacheHandle*)` — cached version
- `DrawShadedBrep(ON_Brep*, material)` — shaded BRep drawing
- `DrawCurve(const ON_Curve&, color, thickness)` — draw curves
- `DrawLine(ON_3dPoint, ON_3dPoint, color, thickness)` — draw lines
- `DrawPoint(ON_3dPoint)` — draw points
- `DrawMesh(const ON_Mesh&, ...)` — draw meshes
- `DrawBox(ON_BoundingBox, color, thickness)` — bounding box wireframe

### 2D Drawing
- `Draw2dLine(...)` — screen-space lines
- `Draw2dRectangle(...)` — screen-space rectangles
- `DrawBitmap(CRhinoDib, x, y)` — draw images
- `DrawString(...)` — text with optional caching

### State Management
- `PushObjectColor(COLORREF)` / `PopObjectColor()` — color stack
- `EnableDepthWriting(bool)` / `EnableDepthTesting(bool)` — z-buffer control
- `PushDepthMode()` / `PopDepthMode()` — depth mode stack

## CRhinoCacheHandle — Display Caching

### Purpose
`CRhinoCacheHandle` stores GPU-side cached representations (VBOs) of geometry to avoid re-tessellating every frame.

### Structure
```cpp
class CRhinoCacheHandle {
    std::shared_ptr<CRhCacheData> m_cache_data;        // general cache
    std::shared_ptr<CRhVboCurveSetData> m_vbo_curve_data; // curve VBOs
    std::shared_ptr<CRhVboData> m_vbo_data;             // mesh VBOs
    
    const CRhinoObject* ParentObject() const;  // owning object
    void Reset();                               // invalidate cache
};
```

### Usage Pattern
```cpp
// Member variable — persists across frames
CRhinoCacheHandle m_cache;

// In drawing code
dp.DrawBrep(brep, color, wireDensity, edgeAnalysis, &m_cache);
```

### Invalidation
- Cache auto-invalidates when the parent `CRhinoObject` changes
- Call `Reset()` manually if you change what you're drawing
- Cache is per-pipeline — if multiple viewports exist, each has its own cache
- Shared_ptr semantics mean cache data is freed when all references drop

### When to Use
- **Always** for repeatedly drawn geometry (conduit objects)
- Especially important for curves (tessellation is expensive)
- BReps benefit significantly (render mesh extraction is costly)
- Less critical for simple primitives (lines, points)

## Performance Considerations

### What's Expensive
1. **Mesh extraction from BReps** — the most expensive operation; always cache
2. **Curve tessellation** — converting NURBS to polylines for display; cache with CRhinoCacheHandle
3. **Per-object overhead in SC_DRAWOBJECT** — called for EVERY object, keep logic minimal
4. **State changes** (material switches, depth mode changes) — batch similar objects
5. **DrawShadedBrep with new materials** — material setup has GPU overhead
6. **String/text drawing without cache** — font rasterization is expensive

### Performance Best Practices
1. **Use CRhinoCacheHandle** for all custom-drawn geometry
2. **Minimize work in SC_DRAWOBJECT** — use lookup tables (hash maps), not searches
3. **Check `dp.InterruptDrawing()`** — if the pipeline wants to bail, respect it
4. **Batch draw calls** — group objects by material/color to minimize state changes
5. **Use `SC_PREDRAWOBJECTS`** for setup, `SC_DRAWOBJECT` for per-object decisions
6. **Avoid allocations** in conduit callbacks — pre-allocate in constructor
7. **Use `ActiveObjectNestingLevel()`** to understand block instance nesting depth

### Frame Budget
- Rhino targets interactive frame rates (30-60 fps)
- `dp.InterruptDrawing()` returns true if the frame is taking too long
- The pipeline may skip objects if drawing takes too long — your conduit should cooperate

## Block Instance Drawing Internals

### How Rhino Draws Block Instances

1. `CRhinoInstanceObject` stores a reference to an `CRhinoInstanceDefinition` plus an `ON_Xform`
2. The pipeline **flattens** block instances during drawing:
   - Pushes the instance transform onto the transform stack
   - Iterates over all objects in the instance definition
   - Draws each definition object with the accumulated transform
   - For nested blocks, this recurses
3. In `SC_DRAWOBJECT`, the conduit sees **each sub-object** individually, not the block as a whole
4. Use `ActiveObjectNestingLevel()` and `ActiveTopLevelObject()` to determine block context

### Drawing Block Definitions Programmatically

```cpp
// Draw a block definition with a custom transform
dp.PushObjectColor(color);
dp.DrawObject(instanceDefinition, &xform);
dp.PopObjectColor();
```

This is used for dynamic block insertion previews and can be used in conduits for ghosted/preview drawing.

### Block Display Caching

From Rhino's display performance notes:
> "Polysurface, mesh, and extrusion objects inside nested blocks are cached for rapid display."

- Rhino caches the **display meshes** of block definition objects
- All instances of the same definition share the cached geometry
- Only the transform differs per instance
- This makes blocks significantly faster than equivalent loose geometry

### Implications for Our Plugin

1. **In SC_DRAWOBJECT**, we see individual sub-objects of blocks — we need `ActiveTopLevelObject()` to map back to the block instance
2. **Hiding a block instance** means setting `m_bDrawObject = false` for ALL sub-objects of that instance
3. **Transparency/color override** must be applied per sub-object but tracked per instance
4. **Nested blocks** require recursive tracking — `ActiveObjectNestingLevel()` gives depth
5. **Performance**: since Rhino already caches block geometry, our conduit overhead is mainly the per-object lookup in SC_DRAWOBJECT

## Key API References

- [CRhinoDisplayPipeline Class](https://developer.rhino3d.com/api/cpp/class_c_rhino_display_pipeline.html)
- [CRhinoDisplayConduit (Highlighting Example)](https://developer.rhino3d.com/guides/cpp/highlighting-objects-in-conduits/)
- [Display Conduits Guide (RhinoCommon)](https://developer.rhino3d.com/guides/rhinocommon/display-conduits/)
- [Dynamically Inserting Blocks](https://developer.rhino3d.com/guides/cpp/dynamically-inserting-blocks/)
- [CRhinoCacheHandle Class](https://developer.rhino3d.com/api/cpp/class_c_rhino_cache_handle.html)
- [CRhinoInstanceObject Class](https://developer.rhino3d.com/api/cpp/class_c_rhino_instance_object.html)
