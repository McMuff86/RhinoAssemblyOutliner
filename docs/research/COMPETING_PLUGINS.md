# Competing Plugins & Related Tools — Research

## Overview

There is no direct competitor that provides a SolidWorks-style assembly outliner for Rhino. The existing tools approach the problem from different angles — BIM, Grasshopper data management, or block editing. This represents a significant gap in the Rhino ecosystem.

---

## 1. VisualARQ

**Website:** [visualarq.com](https://www.visualarq.com/) | [Food4Rhino](https://www.food4rhino.com/en/app/visualarq)
**Developer:** Asuni (same company behind Food4Rhino)
**Price:** Commercial (~€1,000)
**Type:** BIM plugin for Rhino

### What It Does

- Adds **parametric architectural objects** (walls, windows, doors, beams, stairs, slabs, roofs)
- Provides a **project browser/tree panel** for navigating BIM elements by category
- Generates **2D documentation** automatically (plans, sections, elevations)
- **IFC import/export** for BIM interoperability
- **Quantity takeoffs and schedules**
- Clash detection between object sets
- Grasshopper components for parametric BIM object creation

### Assembly-Relevant Features

- **Project tree** organizes objects by type/level/category — closest thing to an outliner in the Rhino ecosystem
- **Visibility control** per level/category via the tree
- **Object properties panel** with BIM-specific attributes
- **Section views** that clip the model

### What It Does Well

- Mature, polished UI integrated into Rhino
- Strong BIM workflow for architecture
- Good documentation and support
- Active development (regular updates)

### What's Missing (for Our Use Case)

- **Architecture-only** — not designed for mechanical assemblies or general block management
- No concept of **component instances** in the mechanical sense
- No **per-instance visibility** independent of BIM categories
- No **isolate mode** or assembly-focused UX
- No **nested block exploration** or hierarchy view
- Heavyweight — installs a lot of functionality users may not need

### Relevance: LOW-MEDIUM
VisualARQ's project browser is the closest UI precedent for a tree panel in Rhino, but it's firmly BIM-focused and not applicable to mechanical assembly workflows.

---

## 2. Elefront

**Website:** [docs.elefront.io](https://docs.elefront.io/) | [Food4Rhino](https://www.food4rhino.com/en/app/elefront)
**Developer:** Ramon van der Heijden (formerly at CORE studio / Thornton Tomasetti)
**Price:** Free
**Type:** Grasshopper plugin for model data management

### What It Does

- **Bake geometry** from Grasshopper to Rhino with full attribute control
- Assign **user-defined key-value attributes** to objects
- **Reference and filter** Rhino objects back into Grasshopper by attributes
- **Block management** in Grasshopper:
  - Define Block Definitions from GH geometry
  - Create Block Instances with transforms
  - Reference existing Block Instances
  - Push/update definitions from GH to Rhino
  - Query block instance data

### Assembly-Relevant Features

- **Block Definition/Instance** components for programmatic assembly creation
- **Attribute management** — attach metadata to objects (could represent component properties)
- **Filtering** — query objects by name, layer, attribute, type
- Objects treated as a **data model** (key-value pairs = lightweight database)

### What It Does Well

- Excellent **Grasshopper integration** — the standard for GH-to-Rhino data management
- Clean, well-documented API
- Block management is more capable than Rhino's native tools
- Free and open source
- Active community and support

### What's Missing (for Our Use Case)

- **Grasshopper-only** — no standalone UI panel or tree view in Rhino
- No **interactive outliner** — everything is code/definition-driven
- No **visibility control** from a tree (manual attribute-based approach)
- No **display pipeline integration** — doesn't draw anything custom
- No **selection sync** between a tree and the viewport
- Requires Grasshopper knowledge

### Relevance: MEDIUM
Elefront proves there's demand for better block/data management in Rhino. Its attribute system is a useful pattern, but it's a complementary tool, not a competitor to our outliner.

---

## 3. Block Edit New

**Website:** [Food4Rhino](https://www.food4rhino.com/en/app/block-edit-new)
**Developer:** Third-party (individual developer)
**Price:** Free/unknown
**Type:** Rhino plugin for block editing

### What It Does

- Enhanced block editing commands beyond Rhino's native `BlockEdit`:
  - **Make Unique** — create a unique copy of a block definition (native Rhino requires explode + redefine)
  - **Double-click navigation** into/out of nested blocks (SketchUp-style)
  - **Editing non-uniformly scaled blocks**
  - **Toggle lock/hide rest** — hide everything except the block being edited
  - Navigate **nested block tree** by double-clicking up and down

### Assembly-Relevant Features

- **Nested block navigation** — closest to an assembly tree navigation pattern
- **Hide rest** while editing — similar to isolate mode
- **Make Unique** — essential for assembly workflows where instances need to diverge

### What It Does Well

- Addresses real, painful gaps in Rhino's native block editing
- SketchUp-style double-click navigation is intuitive
- Community demand is evident (enthusiastic forum posts)

### What's Missing (for Our Use Case)

- **No tree panel** — interaction is through viewport double-clicking, not a sidebar
- **Editing-focused** — designed for modifying block contents, not managing visibility/assembly state
- No **multi-instance visibility control**
- No **display state** or named visibility configurations
- Limited documentation

### Relevance: HIGH
Block Edit New validates the demand for better block management in Rhino. Its navigation pattern (double-click into nested blocks) is worth emulating. Our plugin would complement it — we provide the tree/outliner, they provide the editing workflow.

---

## 4. Assembler (Grasshopper)

**Website:** [Food4Rhino](https://www.food4rhino.com/en/app/assembler)
**Type:** Grasshopper plugin

### What It Does

- Builds and manages **assemblages** — procedural assembly of components
- Rule-based placement and connection of parts
- Designed for generative/combinatorial design

### Relevance: LOW
Completely different use case (generative design, not interactive management).

---

## 5. Native Rhino Block Tools

For completeness, Rhino 8's built-in block management:

- **BlockManager** panel — lists block definitions (not instances!)
- **Block editing** via `BlockEdit` command (enters editing mode)
- **Insert** command for placing blocks
- **Properties panel** shows block instance info when selected
- **Layers panel** — primary visibility management (but layer-based, not instance-based)

### What's Missing Natively

- **No instance-level tree** — you can't see all instances organized hierarchically
- **No per-instance visibility** — visibility is layer-based
- **No nested block exploration** in a tree view
- **No isolate mode** for block instances
- **No selection sync** between a tree and viewport for block hierarchy
- **No display states** (named visibility configurations)
- **No assembly-style context menu** (hide/show/isolate/transparent per instance)

---

## Market Gap Analysis

### What Exists
| Need | Tool | Coverage |
|------|------|----------|
| BIM tree navigation | VisualARQ | Architecture only |
| Block creation/management (GH) | Elefront | Grasshopper only |
| Better block editing | Block Edit New | Editing only, no tree |
| Generative assembly | Assembler | Not interactive |
| Basic block list | Rhino native | Definitions only |

### What's Missing (Our Opportunity)

1. **Interactive assembly tree panel** with nested block hierarchy → **nobody does this**
2. **Per-instance visibility control** from a tree → **nobody does this**
3. **Isolate mode** for block instances → **nobody does this**
4. **Bidirectional selection sync** (tree ↔ viewport) → **VisualARQ partially, but BIM-only**
5. **Named display states** for visibility configurations → **nobody does this**
6. **Transparency/wireframe per instance** → **nobody does this**

### Competitive Positioning

Our plugin fills a **clear, unserved niche**: bringing SolidWorks/Inventor-style assembly management to Rhino. The closest analogy in the Rhino ecosystem doesn't exist. Users currently manage complex block assemblies through layers (which don't map well to instance hierarchy) and manual hide/show commands.

**Target users:**
- Mechanical designers using Rhino (jewelry, product design, industrial design)
- Users importing SolidWorks/STEP files (which come in as nested blocks)
- Architecture firms using blocks for repeated components (furniture, fixtures)
- Anyone working with complex nested block structures

**Key differentiator:** We're the only tool that provides a **real-time, interactive tree panel** with per-instance visibility control, selection sync, and display state management for Rhino blocks.

---

## Sources

- [VisualARQ Features](https://www.visualarq.com/features/)
- [Elefront Documentation](https://docs.elefront.io/)
- [Elefront on Food4Rhino](https://www.food4rhino.com/en/app/elefront)
- [Block Edit New Forum Discussion](https://discourse.mcneel.com/t/great-block-edit-plugin-functions-that-hopefully-will-become-native/151622)
- [Assembler on Food4Rhino](https://www.food4rhino.com/en/app/assembler)
- [McNeel Forum: Amazing Block Plugin](https://discourse.mcneel.com/t/amazing-block-plugin/159855)
