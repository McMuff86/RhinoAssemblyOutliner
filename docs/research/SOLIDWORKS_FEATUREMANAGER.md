# SolidWorks FeatureManager & Assembly Tree — Research

## Overview

SolidWorks' FeatureManager Design Tree is the gold standard for MCAD assembly management. It provides a hierarchical tree view of every part, sub-assembly, feature, and mate in a model. Understanding its patterns informs our Rhino outliner design.

## Assembly Tree Structure

The FeatureManager shows:
- **Top-level assembly** as root node
- **Sub-assemblies** as expandable child nodes (recursive nesting)
- **Parts (components)** as leaf nodes within assemblies
- **Features** within parts (holes, fillets, chamfers, etc.)
- **Mates** (constraints between components)
- **Reference geometry** (planes, axes, coordinate systems)

Each component in the tree shows a **three-level naming convention** by default:
1. **Primary:** Component Name (e.g., `Bolt-M8x30`)
2. **Secondary:** Configuration Name (e.g., `Default`)
3. **Tertiary:** Display State Name (e.g., `Display State-1`)

These can be customized via `Tools > Options > FeatureManager`.

## Per-Component Visibility

### Hide/Show (Lightweight)

- **Right-click → Hide/Show → Hide Component** toggles visual visibility
- Hidden components remain fully loaded in memory — geometry participates in mates, interference detection, and mass properties
- Hidden state is **Display State-specific** — hiding a part in one Display State doesn't affect other Display States
- Hidden components show a **translucent icon** in the FeatureManager tree
- The **"Show Hidden Components"** button (Assembly tab) temporarily reveals all hidden components with a translucent/ghosted appearance for selection

### Suppress/Unsuppress (Heavyweight)

- **Right-click → Suppress** completely removes the component from memory
- Suppressed components do NOT participate in mates, mass calculations, interference checks, or BOM
- Suppressed state is **Configuration-specific** — a part can be suppressed in one configuration but active in another
- Suppressed components show a **grayed-out icon** with a distinctive marker in the tree
- Suppression is saved with the file (persistent)
- Use case: simplifying large assemblies for performance, creating configuration variants

### Key Difference: Hide vs. Suppress

| Aspect | Hide | Suppress |
|--------|------|----------|
| In memory | Yes | No |
| Mates active | Yes | No |
| In BOM | Yes | No |
| Performance gain | Minimal (display only) | Significant |
| Scope | Display State | Configuration |
| Persistence | Display State | Configuration |

## Display States

Display States control **visual appearance only** without affecting geometry or design data:

- **Visibility** (show/hide per component)
- **Display mode** per component (Shaded, Wireframe, Hidden Lines Removed, Shaded with Edges)
- **Color/appearance** overrides per component, face, or body
- **Transparency** toggle per component
- **Line style** overrides

### Key Properties

- Display States are managed in the **ConfigurationManager** tab (bottom of the FeatureManager pane)
- Multiple Display States can exist per Configuration
- Display States can be **linked to Configurations** (changes propagate) or **independent**
- Created via right-click → "Add Display State"
- Each Display State stores a complete snapshot of all visual properties for every component

### Persistence

- Display States are saved within the assembly file (.sldasm)
- Each component instance stores its own visibility/appearance per Display State
- The active Display State is also saved and restored on file open

## Configurations

Configurations control **geometric and structural variation**:

- Which components are suppressed/resolved
- Component configurations (each part can have its own configurations)
- Dimension values, feature suppression
- Custom properties per configuration

Configurations and Display States are **orthogonal**: a Configuration defines what's structurally present; a Display State defines how it looks.

## Isolate Mode

SolidWorks' **Isolate** command is a powerful temporary focus tool:

- Select one or more components, then **right-click → Isolate**
- All **non-selected** components are set to one of:
  - **Hidden** (default)
  - **Transparent**
  - **Wireframe**
- The user chooses the isolation style
- An "Exit Isolate" button/bar appears at the top of the viewport
- Isolation is **temporary** — exiting restores previous visibility state
- Components sharing a mate with the isolated component can optionally be shown

### Isolate UX Pattern
1. User selects components of interest
2. Everything else fades away (hidden/transparent/wireframe)
3. User works on the isolated set
4. Clicking "Exit Isolate" restores the full assembly view

This is extremely useful for complex assemblies and is a pattern we should replicate.

## Selection & Highlighting

- Clicking a component in the FeatureManager **selects it in the viewport** (and vice versa — bidirectional sync)
- Selected components show a **blue highlight** in the viewport
- Hidden components **cannot be selected** in the viewport, but CAN be selected in the tree
- Selecting a hidden component in the tree shows its bounding location with a dashed outline
- **Rollover highlighting**: hovering over a tree node highlights the corresponding component in the viewport with a subtle edge highlight
- Sub-assembly selection: clicking a sub-assembly node selects all its children

## Component Properties Per Instance

Each component instance in an assembly has:
- **Component Properties** dialog (right-click → Component Properties):
  - Override configuration used by this instance
  - Override display state used by this instance
  - Flexibility (rigid vs. flexible sub-assembly)
  - Envelope status
  - Exclude from BOM
  - Reference/fixed status

This means the **same part file** can appear multiple times in an assembly, each instance with different configurations, display states, and properties.

## Lessons for Our Rhino Implementation

### Must-Have Patterns

1. **Bidirectional selection sync** — clicking tree ↔ clicking viewport must be tightly coupled
2. **Hide/Show per instance** — lightweight visibility toggle that doesn't remove from document
3. **Isolate mode** — temporary focus with transparent/wireframe fallback for non-selected
4. **Visual feedback in tree** — hidden items should look different (faded icon, strikethrough, etc.)
5. **Display State concept** — save/restore named visibility configurations

### Should-Have Patterns

6. **Suppress equivalent** — fully unload block instances for performance (Rhino doesn't natively support this, but we could skip drawing)
7. **Per-instance overrides** — different colors, transparency per block instance (via object attributes)
8. **Rollover highlighting** — hover in tree highlights in viewport

### Design Differences from SolidWorks

- Rhino doesn't have native configurations — we'd implement Display States as our own saved presets
- Rhino blocks are simpler than SW components — no internal features/mates
- We need to handle nested blocks (sub-assemblies) which map well to SW's sub-assembly concept
- Rhino's layer system partially overlaps with SW's visibility — we should integrate, not fight it

### UX Takeaways

- The **right-click context menu** is the primary interaction point for visibility changes
- **Keyboard shortcuts** for common actions (H = hide, Ctrl+Q = isolate) speed up workflow
- **Batch operations** — select multiple items, apply visibility change to all
- The tree should show **instance count** and **status indicators** (resolved, lightweight, suppressed)

## Sources

- [SolidWorks Help: Displaying Components in FeatureManager](https://help.solidworks.com/2021/english/SolidWorks/sldworks/c_displaying_components_fmdt.htm)
- [SolidWorks Help: Isolate](https://help.solidworks.com/2021/English/SWConnected/swdotworks/HIDD_COMP_ISOLATE_DLG.htm)
- [SolidWorks Blog: Hide vs. Suppressed](https://blogs.solidworks.com/solidworksblog/2012/05/hide-vs-suppressed-in-solidworks-assemblies.html)
- [Mechanitec: Mastering Display States](https://mechanitec.ca/display-states-solidworks/)
- [Javelin Tech: FeatureManager Customization](https://www.javelin-tech.com/blog/2022/11/make-solidworks-featuremanager-more-useful/)
