# User Guide

## Table of Contents

- [Installation](#installation)
- [Getting Started](#getting-started)
- [Panel Overview](#panel-overview)
- [Navigation](#navigation)
- [Selection](#selection)
- [Visibility Controls](#visibility-controls)
- [Assembly Mode](#assembly-mode)
- [Search & Filter](#search--filter)
- [Context Menu](#context-menu)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Workflows](#workflows)
- [FAQ](#faq)
- [Troubleshooting](#troubleshooting)

---

## Installation

### System Requirements

- Rhino 8 (Windows or macOS)
- .NET 7.0 Runtime (included with Rhino 8)

### Install via Package Manager

*Coming soon*

### Manual Installation

1. Download the latest `.rhp` file from [Releases](https://github.com/your-org/RhinoAssemblyOutliner/releases)
2. In Rhino, run `PlugInManager`
3. Click "Install..." and select the downloaded file
4. Restart Rhino

---

## Getting Started

### Opening the Panel

Use one of these methods:
- Run command: `AssemblyOutliner`
- Menu: **Panels** → **Assembly Outliner**
- Right-click toolbar → **Assembly Outliner**

### First Look

The Assembly Outliner displays your document as a hierarchical tree:

```
📄 MyProject.3dm
├─ 📦 Cabinet_Base #1
│   ├─ 📦 Drawer_500 #1
│   ├─ 📦 Drawer_500 #2
│   └─ ⬡ Back_Panel
├─ 📦 Cabinet_Base #2
└─ ⬡ Countertop
```

**Icons:**
- 📄 Document root
- 📦 Block instance (embedded)
- 🔗 Block instance (linked)
- 📎 Block instance (linked & embedded)
- ⬡ Geometry object

---

## Panel Overview

| Area | Description |
|------|-------------|
| **Toolbar** | Mode dropdown, Refresh, Expand All, Collapse All |
| **Search Bar** | Filter tree by name |
| **Tree View** | Hierarchical object structure with visibility toggles |
| **Detail Panel** | Selected item properties and user attributes |
| **Status Bar** | Instance count, definition count, hidden count |

---

## Navigation

### Expanding Nodes

- **Click arrow** to expand/collapse a node
- **Double-click** a block node to enter BlockEdit mode
- **Right-click** → **Expand All** for deep expansion

### Zooming

- Select a node and press **F** to zoom/frame the object in the viewport
- Right-click → **Zoom To** for the same action

---

## Selection

### Tree → Viewport

- **Single click** a node to select it in the viewport
- **Ctrl+Click** to add to selection
- **Shift+Click** for range selection

### Viewport → Tree

When you select objects in the viewport, the corresponding tree nodes:
- Automatically highlight
- Expand parent nodes to reveal selection
- Scroll into view

### Select All Same

To select all instances of a block definition:
1. Right-click any instance
2. Choose **Select All Same Definition**
3. All instances highlight in viewport and tree

---

## Visibility Controls

### Toggle Visibility

Click the 👁 icon next to any node to toggle its visibility.

| Icon | State |
|------|-------|
| 👁 | Visible |
| 👁‍🗨 | Hidden |
| ◐ | Mixed (some children hidden) |

Hidden items appear with **grayed-out, italic text** in the tree for immediate visual feedback.

### Hide a Component

1. Select one or more nodes in the tree
2. Press **H** (or click the eye icon, or right-click → **Hide**)
3. The object disappears from the viewport
4. The tree node turns gray and italic

### Show a Component

1. Select the hidden node(s) in the tree (gray/italic)
2. Press **S** (or click the eye icon, or right-click → **Show**)
3. The object reappears in the viewport

### Isolate

Isolate shows only the selected object and hides everything else:

1. Select the node you want to focus on
2. Press **I** (or right-click → **Isolate**)
3. A banner appears: *"Isolate Mode — N of M visible"*
4. Only the selected object (and its children) remain visible

**To exit Isolate Mode:**
- Press **Space** (Show All)
- Or click the **✕ Exit Isolate** button in the banner
- All objects return to their pre-isolate visibility state

### Show All

Press **Space** to make all objects visible again. This resets all visibility toggles and exits Isolate Mode.

### Show/Hide with Dependents

For nested assemblies, you can show or hide a node and all its children:
- Right-click → **Show with Dependents** — shows the node and all descendants
- Right-click → **Hide with Dependents** — hides the node and all descendants

### Three-Tier Visibility

The Assembly Outliner respects Rhino's layer system. An object is visible only if:

1. ✅ **Layer** is visible (Rhino-native — managed via Layers panel)
2. ✅ **Instance** is visible (eye icon toggle in the outliner)
3. ✅ **Component** is visible (per-instance component visibility — v2.0)

> **Tip:** If you show an object in the outliner but it doesn't appear, check if its layer is turned off.

---

## Assembly Mode

Assembly Mode lets you focus on a single assembly root, filtering the tree to show only that assembly and its children.

### Setting an Assembly Root

1. Right-click any block instance in the tree
2. Choose **Set as Assembly Root**
3. The tree filters to show only that instance's hierarchy
4. The mode dropdown in the toolbar switches to **Assembly Mode**

### Switching Between Modes

Use the **mode dropdown** in the toolbar:
- **Document Mode** — shows the entire document hierarchy (default)
- **Assembly Mode** — shows only the selected assembly root

### When to Use Assembly Mode

- **Large documents** with many top-level assemblies — focus on one at a time
- **Kitchen projects** — focus on one cabinet to work on its components
- **Steel structures** — isolate one portal frame from the full building

### Returning to Document Mode

Select **Document Mode** from the mode dropdown, or right-click → **Clear Assembly Root**.

---

## Search & Filter

### Basic Search

Type in the search bar to filter the tree:
- Matches object names and block definition names
- Case-insensitive
- Press **Ctrl+F** to focus the search bar
- Press **Escape** to clear the search

---

## Context Menu

Right-click any node for these options:

| Section | Action | Shortcut | Description |
|---------|--------|----------|-------------|
| **Visibility** | Show | S | Make visible |
| | Hide | H | Make invisible |
| | Isolate | I | Show only this, hide rest |
| | Show All | Space | Reset all visibility |
| | Show with Dependents | — | Show node + all children |
| | Hide with Dependents | — | Hide node + all children |
| **Selection** | Select in Viewport | — | Select object |
| | Select All Same Definition | — | Select all instances of this definition |
| **Navigation** | Zoom To | F | Frame object in viewport |
| | Set as Assembly Root | — | Focus tree on this assembly |
| **Editing** | Edit Block | — | Enter BlockEdit mode |
| | Properties | — | Open Rhino properties panel |

---

## Keyboard Shortcuts

All shortcuts work when the tree panel has focus.

| Shortcut | Action |
|----------|--------|
| **H** | Hide selected node(s) |
| **S** | Show selected node(s) |
| **I** | Isolate selected |
| **Space** | Show All (reset visibility) |
| **F** | Zoom to / Frame selected |
| **Del** | Delete selected object |
| **F5** | Refresh tree |
| **Ctrl+F** | Focus search bar |
| **Escape** | Clear search / exit isolate mode |
| **→** | Expand node |
| **←** | Collapse node |
| **Enter** | Edit block (BlockEdit) |

---

## Workflows

### Workflow 1: Inspecting a Cabinet Interior

1. Open your kitchen assembly file
2. The outliner shows all cabinets in the hierarchy
3. Find `Unterschrank_600 #1` and expand it
4. Press **H** on `Tür` (door) to hide it → you can now see inside
5. Press **H** on `Schublade #2` (drawer) to hide it for better access
6. Work on the interior components
7. Press **Space** to show all when done

### Workflow 2: Isolating a Sub-Assembly

1. Right-click on a block instance → **Set as Assembly Root**
2. The tree now shows only this assembly's hierarchy
3. Press **I** on a component to isolate it (hide siblings)
4. Work on the isolated component
5. Press **Space** to show all components again
6. Switch back to **Document Mode** when done

### Workflow 3: Counting Components for BOM

1. Stay in **Document Mode** to see everything
2. Right-click on any instance of a part (e.g., `Scharnier_Blum`)
3. Choose **Select All Same Definition**
4. The status bar shows the count
5. All instances are selected in the viewport for verification

---

## FAQ

### Why doesn't my block show children?

Click the expand arrow (►) next to the block instance to show its children.

### Why is the tree empty?

- Check that you have a document open
- Ensure the document contains block instances
- Click Refresh (🔄) in the toolbar or press **F5**

### How do I update after changing blocks?

The tree auto-updates when you add/delete objects, create/modify blocks, or change visibility. For manual refresh, press **F5**.

### An object is shown in the tree but invisible in the viewport?

Check if the object's **layer** is turned off. The outliner controls instance visibility, but layer visibility is managed through Rhino's Layers panel.

### Can I export the tree as a BOM?

*Coming in a future release (v2.0+)*

---

## Troubleshooting

### Plugin doesn't load

1. Verify Rhino 8 is installed
2. Check plugin is enabled: `PlugInManager` → Find Assembly Outliner → Enable
3. Restart Rhino

### Performance issues with large files

- Collapse unused branches
- Use search to filter visible nodes
- Use Assembly Mode to focus on one sub-assembly

### Report a Bug

Open an issue at: [GitHub Issues](https://github.com/your-org/RhinoAssemblyOutliner/issues)

Include:
- Rhino version (`About Rhino`)
- Plugin version
- Steps to reproduce
- Sample file (if possible)

---

*Last updated: February 2026*
