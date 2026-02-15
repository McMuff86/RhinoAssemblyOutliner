# Existing Solutions Research: Per-Instance Block Modifications in Rhino

**Date:** 2026-02-15  
**Purpose:** Understand how existing plugins/tools handle per-instance block modifications and what gaps exist.

---

## 1. Rhino's Built-in Block System — Fundamental Limitations

### What the BlockManager CAN Do
- Create/edit/delete block definitions
- Nested blocks (blocks within blocks)
- Linked blocks (external file references)
- Per-instance: **transform** (position, rotation, scale)
- Per-instance: **layer assignment** (the instance itself)
- Per-instance: **name** (instance name, not definition name)
- Per-instance: **display color** via "By Parent" on sub-objects
- Rhino 8+: Block Definitions panel with visual highlighting of components

### What it CANNOT Do
- **Per-instance visibility of sub-components** — ALL instances show ALL geometry
- **Per-instance material override of sub-components** — material is definition-level
- **Per-instance geometry variation** — no dynamic blocks like AutoCAD
- **Per-instance attribute override** — User Text on definition objects is shared

### Why No Per-Instance Visibility?
Rhino's block architecture is fundamentally **definition-based**: one `InstanceDefinition` → N `InstanceReference` objects. The instance stores ONLY a transform (4x4 matrix) and a reference to the definition. There is **no per-instance override storage** in the data model.

**Forum evidence:**
- [Per Block Instance Wishes (2024)](https://discourse.mcneel.com/t/per-block-instance-wishes/185033) — Direct feature request for per-instance geometry hiding and display attribute changes. No McNeel commitment.
- [Dynamic Blocks Function in Rhino (2015)](https://discourse.mcneel.com/t/dynamic-blocks-function-in-rhino-such-as-the-one-in-autocad-for-rhino-6-or-already-possible/27271) — Long-standing request for AutoCAD-style dynamic blocks.
- [Are Dynamic Blocks Coming to Rhino 8? (2022)](https://discourse.mcneel.com/t/are-dynamic-blocks-coming-to-rhino-8/141846) — Community asking again; no definitive answer.
- [Suggestion for Dynamic Block Feature (2024)](https://discourse.mcneel.com/t/suggestion-for-dynamic-block-feature-in-rhino/193919) — Still being requested.

**McNeel's implicit position:** Dynamic blocks / per-instance overrides are "use Grasshopper for that." Rhino 8 added DWG dynamic block *import* support (display only), but no native creation.

---

## 2. VisualARQ — BIM Plugin for Rhino

**Website:** https://www.visualarq.com  
**Developer:** Asuni CAD (Barcelona)  
**Food4Rhino:** https://www.food4rhino.com/en/app/visualarq

### Architecture
VisualARQ uses its **own custom object system** layered on top of Rhino, NOT standard block definitions:

- **Styles** define parametric object types (Wall Style, Door Style, Window Style, etc.)
- Each style can use:
  - Built-in parametric geometry (walls with layers, doors with frames/panels)
  - **3D Block** for Model view + **2D Block** for Plan view + **custom profile** for wall openings
  - **Grasshopper definitions** ("Grasshopper Styles") for unlimited parametric objects

### Per-Instance Behavior
- **Each VisualARQ object IS a unique instance** with its own parameters (width, height, etc.)
- Parameters are stored per-instance, geometry is regenerated on-the-fly
- Component visibility is controlled through **style configuration**, not per-instance
- You can create multiple styles (e.g., "Door_SinglePanel", "Door_DoublePanel") but within one style, all instances share the same component set

### Grasshopper Styles
- A Grasshopper definition becomes the "engine" for a VisualARQ style
- Output geometry components are assigned to representations (Model, Plan, Section)
- Parameters exposed in GH become editable per-instance in VisualARQ
- **Key insight:** Each "geometry block" in the output can be toggled, but this is style-level, not instance-level

### Relevance to Our Project
- VisualARQ solves "variants" through **multiple styles**, not per-instance component visibility
- The Grasshopper Style approach is powerful but heavyweight (requires GH engine running)
- **Not a model for per-instance block component visibility** — different paradigm entirely

### Links
- [Grasshopper Styles documentation](https://help.visualarq.com/architectural-objects/grasshopper-styles/)
- [Parametric architectural objects](https://www.visualarq.com/feature/architectural-objects/)
- [Get Started with GH Styles](https://www.visualarq.com/tutorial/grasshopper-styles/get-started/)

---

## 3. Elefront — Grasshopper Plugin for Block Management

**Food4Rhino:** https://www.food4rhino.com/en/app/elefront  
**Developer:** Front Inc.

### What It Does
- **Bake geometry** with user-defined attributes AND Rhino attributes from Grasshopper
- **Reference Rhino objects** back into Grasshopper (by name, layer, attribute filter)
- **Block operations:** Define blocks, bake block instances, reference existing blocks
- **Attribute management:** User Text key-value pairs on objects

### Per-Instance Capabilities
- **User Text attributes CAN be set per-instance** on block references (stored on the InstanceReference object)
- **BUT:** This is metadata only — it doesn't affect geometry display
- Block definitions are still shared; Elefront can redefine a block but this affects ALL instances
- "When baking an object, it can only have one set of Elefront attributes" — per object, not per sub-component

### Block Workflow
```
Elefront workflow:
1. Define Block (name, geometry, base point, attributes)
2. Bake Block Instance (with per-instance transform + user attributes)
3. Reference Block by name/attributes back into GH

Limitation: No way to say "this instance shows component A but not B"
```

### Modify Attributes Inside Blocks
- [Forum thread](https://discourse.mcneel.com/t/modify-attributes-of-geometry-inside-a-block/113547): "This subject is driving me half crazy" — modifying attributes of geometry INSIDE block definitions requires redefining the entire block
- Workaround: Use "Define Block" to redefine (replaces geometry for ALL instances)

### Relevance
- Elefront provides the best GH-based block management but **does not solve per-instance component visibility**
- Its per-instance User Text is useful for data but not for display control
- The "redefine block" approach = clone pattern (create variant definitions)

---

## 4. Instance Manager — Grasshopper Plugin

**Food4Rhino:** https://www.food4rhino.com/app/instance-manager  
**GH Docs:** https://grasshopperdocs.com/components/instancemanager/defineBlock.html

### What It Does
- Define or modify instance definitions from Grasshopper
- Create block instances with transforms
- Deconstruct existing blocks

### Relevance
- Similar to Elefront but simpler
- Still bound by Rhino's definition-based block architecture
- No per-instance component overrides

---

## 5. Block Edit New — Rhino Plugin

**Food4Rhino:** https://www.food4rhino.com/en/app/block-edit-new  
**Forum:** https://discourse.mcneel.com/t/great-block-edit-plugin-functions-that-hopefully-will-become-native/151622

### Features
- **Make Unique** — creates a copy of the block definition for one instance (exactly the clone pattern!)
- Double-click navigation for nested blocks (like SketchUp)
- Support for editing non-uniformly scaled blocks
- Toggle lock/hide rest of model during edit

### Relevance
- "Make Unique" is the closest existing feature to per-instance variation
- It works by **cloning the definition** — creating `BlockName_001`, `BlockName_002`, etc.
- This is the standard workaround but leads to definition proliferation
- **This is exactly what our Assembly Outliner could improve upon** with smart variant management

---

## 6. Grasshopper-Based Approaches

### Native GH Block Components (Rhino 8+)
Rhino 8 added native Grasshopper components for blocks:
- `Define Block` — Create/modify block definitions
- `Block Instance` — Create instances with transforms
- `Query Block` — Deconstruct existing blocks
- `Import Block` — Import from external files

Source: https://www.rhino3d.com/features/grasshopper/blocks/

### Clone-and-Modify Pattern
The most common GH approach for "variants":
```
1. Reference existing block definition
2. Deconstruct → get geometry list
3. Filter/modify geometry (remove components, change attributes)
4. Create NEW block definition with modified geometry
5. Place instances of the new definition

Result: N variants = N definitions (memory × N)
```

### Forum Discussion
[Blocks management in Grasshopper (2020)](https://discourse.mcneel.com/t/blocks-management-in-grasshopper/99662):
> "By now, it has become obvious that McNeel will never improve block management in Rhino, but blocks are just completely indispensable when you tackle construction or fabrication processes."

This frustration drives the community to GH-based workarounds.

---

## 7. RhinoCommon / C++ SDK — API Analysis

### InstanceDefinitionTable API
**Source:** https://developer.rhino3d.com/api/rhinocommon/rhino.docobjects.tables.instancedefinitiontable

Key methods:
```csharp
// Create
int Add(string name, string description, Point3d basePoint, 
        IEnumerable<GeometryBase> geometry, IEnumerable<ObjectAttributes> attributes)

// Modify definition (affects ALL instances)
bool Modify(InstanceDefinition idef, string newName, string newDescription, bool quiet)
bool Modify(int idefIndex, UserData userData, bool quiet)
bool ModifyGeometry(int idefIndex, IEnumerable<GeometryBase> newGeometry, bool quiet)

// Query
InstanceDefinition Find(string name)
InstanceDefinition[] GetList(bool ignoreDeleted)

// Delete
bool Delete(int idefIndex, bool deleteReferences, bool quiet)
```

### Critical Observation
- `ModifyGeometry` replaces ALL geometry in a definition → affects ALL instances
- There is NO `ModifyInstanceGeometry` or per-instance override API
- `InstanceObject` (the reference) stores: `Transform`, `InstanceDefinition` reference, standard `ObjectAttributes`
- Standard `ObjectAttributes` includes: Layer, Color, Material, Visibility — but these apply to the WHOLE instance, not sub-components

### InstanceObject Properties
```csharp
class InstanceObject : RhinoObject
{
    InstanceDefinition InstanceDefinition { get; }  // shared definition
    Transform InstanceXform { get; }                  // per-instance transform
    // Inherited from RhinoObject:
    ObjectAttributes Attributes { get; }              // per-instance attrs (layer, color, etc.)
}
```

### What Would Be Needed for Per-Instance Component Visibility
```csharp
// DOES NOT EXIST — hypothetical API:
class InstanceObject
{
    // Override visibility per definition sub-object
    Dictionary<Guid, bool> ComponentVisibilityOverrides { get; }
    
    // Override attributes per definition sub-object  
    Dictionary<Guid, ObjectAttributes> ComponentAttributeOverrides { get; }
}
```

This would require McNeel to:
1. Add override storage to `InstanceObject` / `ON_InstanceRef`
2. Modify the display pipeline to check overrides during block rendering
3. Modify file format (3DM) to persist overrides

### Sample Code: Clone Block Definition for Variant
```csharp
// The workaround: clone definition to create variant
public static int CloneBlockDefinition(RhinoDoc doc, int sourceIndex, 
                                        string newName, int[] excludeIndices)
{
    var idef = doc.InstanceDefinitions[sourceIndex];
    var objects = idef.GetObjects();
    
    var newGeometry = new List<GeometryBase>();
    var newAttributes = new List<ObjectAttributes>();
    
    for (int i = 0; i < objects.Length; i++)
    {
        if (excludeIndices.Contains(i)) continue; // Skip excluded components
        newGeometry.Add(objects[i].Geometry.Duplicate());
        newAttributes.Add(objects[i].Attributes.Duplicate());
    }
    
    return doc.InstanceDefinitions.Add(
        newName, 
        idef.Description,
        idef.BasePoint,
        newGeometry,
        newAttributes
    );
}
```

### Developer Samples
- [Instance Definition Tree](https://developer.rhino3d.com/samples/rhinocommon/instance-definition-tree/) — enumerate nested block structure
- [GitHub rhinocommon source](https://github.com/mcneel/rhinocommon/blob/master/dotnet/rhino/rhinosdkinstance.cs)

---

## 8. AutoCAD Dynamic Blocks — The Gold Standard Comparison

AutoCAD's Dynamic Blocks support:
- **Visibility States** — toggle sub-component groups on/off per instance
- **Lookup Parameters** — dropdown to select configurations
- **Stretch/Move/Rotate actions** — per-instance geometric modifications
- **Flip states** — mirror sub-components

Rhino 8 can **import and display** AutoCAD dynamic blocks with their selected states, but cannot create or edit them.

---

## 9. State of the Art Summary

| Feature | Rhino Native | VisualARQ | Elefront | Block Edit New | AutoCAD |
|---------|-------------|-----------|----------|---------------|---------|
| Per-instance transform | ✅ | ✅ | ✅ | ✅ | ✅ |
| Per-instance layer | ✅ | ✅ | ✅ | ✅ | ✅ |
| Per-instance color | ✅ (whole) | ✅ | ✅ | ✅ | ✅ |
| Per-instance user data | ✅ (on ref) | ✅ | ✅ | ❌ | ✅ |
| Per-instance **component visibility** | ❌ | ❌* | ❌ | ❌ | ✅ |
| Per-instance **component attributes** | ❌ | ❌* | ❌ | ❌ | ✅ |
| Variant creation (clone) | Manual | Styles | Redefine | Make Unique | Visibility States |

*VisualARQ achieves variations through multiple styles/Grasshopper parameters, not true per-instance component overrides.

---

## 10. Conclusions & Implications for RhinoAssemblyOutliner

### The Gap
**No existing Rhino plugin provides true per-instance block component visibility.** This is a fundamental limitation of Rhino's block architecture (`ON_InstanceRef` stores only a transform).

### Current Workarounds (all have downsides)
1. **Clone definitions** ("Make Unique") — definition proliferation, memory overhead
2. **Multiple styles** (VisualARQ) — heavyweight, requires BIM plugin
3. **Grasshopper regeneration** — requires GH running, not interactive
4. **Layer-based hiding** — affects ALL instances, not per-instance

### Our Approach Options
1. **Display Conduit approach** — intercept rendering, apply per-instance overrides visually without changing the data model. Store override data in User Text or custom UserDictionary on each InstanceObject.
2. **Smart clone management** — automate the clone pattern but with intelligent deduplication and a UI that presents it as "component visibility" while creating variant definitions behind the scenes.
3. **Hybrid** — use display conduit for interactive preview, create actual variant definitions only on "commit" or export.

### Key Technical Insight
The [Display Conduit thread](https://discourse.mcneel.com/t/displayconduit-cannot-hide-block-instance-definition/183638) shows someone trying to use `DisplayConduit` for block instance rendering control — they got geometry drawing working but had z-ordering issues. This confirms the conduit approach is feasible but tricky.

### Recommendation
**Option 2 (Smart Clone Management)** is most practical:
- Works within Rhino's existing data model
- No display pipeline hacks
- File saves/exports work correctly
- Other plugins can understand the blocks
- Our Assembly Outliner becomes the "smart manager" that presents cloned definitions as variants of a parent assembly

The UI would show:
```
Assembly: ChairAssembly
├── Seat       [👁 visible in all]
├── Backrest   [👁 visible in 3 of 5 instances]  
├── Armrest_L  [👁 visible in 2 of 5 instances]
└── Armrest_R  [👁 visible in 2 of 5 instances]

Behind the scenes:
- ChairAssembly (Seat + Backrest + Armrest_L + Armrest_R) — 2 instances
- ChairAssembly__noArms (Seat + Backrest) — 1 instance  
- ChairAssembly__noBack (Seat + Armrest_L + Armrest_R) — 1 instance
- ChairAssembly__seatOnly (Seat) — 1 instance
```

---

## References

- [Rhino Block Documentation](http://docs.mcneel.com/rhino/8/help/en-us/commands/block.htm)
- [Rhino 8 Block Features](https://www.rhino3d.com/features/blocks/)
- [Rhino 8 GH Block Components](https://www.rhino3d.com/features/grasshopper/blocks/)
- [InstanceDefinitionTable API](https://developer.rhino3d.com/api/rhinocommon/rhino.docobjects.tables.instancedefinitiontable)
- [rhinocommon source (GitHub)](https://github.com/mcneel/rhinocommon/blob/master/dotnet/rhino/rhinosdkinstance.cs)
- [Per Block Instance Wishes](https://discourse.mcneel.com/t/per-block-instance-wishes/185033)
- [Dynamic Blocks Request (2015)](https://discourse.mcneel.com/t/dynamic-blocks-function-in-rhino-such-as-the-one-in-autocad-for-rhino-6-or-already-possible/27271)
- [Dynamic Blocks Request (2024)](https://discourse.mcneel.com/t/suggestion-for-dynamic-block-feature-in-rhino/193919)
- [Blocks Management in GH](https://discourse.mcneel.com/t/blocks-management-in-grasshopper/99662)
- [Modify Block Attributes (Elefront)](https://discourse.mcneel.com/t/modify-attributes-of-geometry-inside-a-block/113547)
- [DisplayConduit for Block Instances](https://discourse.mcneel.com/t/displayconduit-cannot-hide-block-instance-definition/183638)
- [Block Edit New Plugin](https://www.food4rhino.com/en/app/block-edit-new)
- [Elefront Plugin](https://www.food4rhino.com/en/app/elefront)
- [VisualARQ](https://www.visualarq.com)
- [Instance Manager (GH)](https://www.food4rhino.com/app/instance-manager)
