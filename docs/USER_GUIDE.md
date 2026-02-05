# User Guide

> 🚧 **Work in Progress** — This documentation will be completed as the plugin reaches feature-complete status.

## Table of Contents

- [Installation](#installation)
- [Getting Started](#getting-started)
- [Panel Overview](#panel-overview)
- [Navigation](#navigation)
- [Selection](#selection)
- [Visibility Controls](#visibility-controls)
- [Search & Filter](#search--filter)
- [Context Menu](#context-menu)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [FAQ](#faq)

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
- 📦 Block instance
- ⬡ Geometry object

---

## Panel Overview

*Screenshot placeholder*

| Area | Description |
|------|-------------|
| **Toolbar** | Refresh, expand/collapse, settings |
| **Search Bar** | Filter tree by name |
| **Tree View** | Hierarchical object structure |
| **Detail Panel** | Selected item properties |

---

## Navigation

### Expanding Nodes

- **Click arrow** to expand/collapse a node
- **Double-click** to expand and zoom to object
- **Right-click** → **Expand All** for deep expansion

### Scrolling

The tree view supports standard scroll gestures. For large documents, use the search bar to find specific components quickly.

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
2. Choose **Select All Same**
3. All instances highlight in viewport and tree

---

## Visibility Controls

### Toggle Visibility

Click the 👁 icon next to any node to show/hide it.

| Icon | State |
|------|-------|
| 👁 | Visible |
| 👁‍🗨 | Hidden |
| ◐ | Mixed (some children hidden) |

### Isolate

Right-click → **Isolate** to:
- Hide all other objects
- Show only selected object and its children

### Show All

Click **Show All** in toolbar to reset all visibility.

---

## Search & Filter

### Basic Search

Type in the search bar to filter the tree:
- Matches object names
- Matches block definition names
- Case-insensitive

### Filter Options

*Coming soon*

- By layer
- By object type
- By link type (embedded/linked)

---

## Context Menu

Right-click any node for these options:

| Action | Description |
|--------|-------------|
| **Select** | Select in viewport |
| **Select All Same** | Select all instances of this definition |
| **Zoom To** | Zoom viewport to object bounds |
| **Isolate** | Hide everything except this |
| **Hide** | Hide this object |
| **Edit Block** | Enter block edit mode (blocks only) |
| **Properties** | Open Rhino properties panel |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `F5` | Refresh tree |
| `Ctrl+F` | Focus search bar |
| `Escape` | Clear search / selection |
| `→` | Expand node |
| `←` | Collapse node |
| `Enter` | Zoom to selected |

---

## FAQ

### Why doesn't my block show children?

Blocks must be "exploded" in the tree to show contents. Click the expand arrow (►) next to the block instance.

### Why is the tree empty?

- Check that you have a document open
- Ensure the document contains block instances
- Click refresh (🔄) in the toolbar

### How do I update after changing blocks?

The tree auto-updates when you:
- Add/delete objects
- Create/modify blocks
- Change visibility or layers

For manual refresh, press `F5` or click the refresh button.

### Can I export the tree as a BOM?

*Coming in a future release*

---

## Troubleshooting

### Plugin doesn't load

1. Verify Rhino 8 is installed
2. Check plugin is enabled: `PlugInManager` → Find Assembly Outliner → Enable
3. Restart Rhino

### Performance issues with large files

- Collapse unused branches
- Use search to filter visible nodes
- Disable auto-expand in settings (coming soon)

### Report a Bug

Open an issue at: [GitHub Issues](https://github.com/your-org/RhinoAssemblyOutliner/issues)

Include:
- Rhino version (`About Rhino`)
- Plugin version
- Steps to reproduce
- Sample file (if possible)

---

*Last updated: February 2026*
