# RhinoAssemblyOutliner

**SolidWorks-style Assembly Outliner for Rhino 8**

A dockable panel plugin that brings hierarchical block instance management to Rhino 8 — inspired by the SolidWorks FeatureManager Design Tree. Navigate, control visibility, and organize complex block assemblies with ease.

> Screenshots coming soon

---

## Features

### 🌳 Hierarchical Tree View
Recursive visualization of block instances with expand/collapse, showing the full nesting hierarchy of your assembly. Linked (🔗), embedded (📦), and linked+embedded (📎) blocks are distinguished by icon.

### 👁️ Visibility Control
Toggle visibility per instance with the eye icon column. Supports **Show/Hide with Dependents** for recursive operations on entire sub-trees. Parent nodes display a mixed-state icon (◐) when some children are hidden.

### 🔍 Isolate Mode
Isolate one or more instances to focus your work. A banner shows "Isolate Mode — N of M visible" with a quick exit button. All other objects are temporarily hidden.

### 🏗️ Assembly Mode
Set any block instance as the **Assembly Root** to focus the tree on a single sub-assembly. Switch between Document Mode and Assembly Mode via the toolbar dropdown.

### 🔗 Bidirectional Selection Sync
Click a node in the tree to select it in the viewport — or select in the viewport and the tree follows. Debounced for smooth interaction.

### 📋 Detail Panel
View selected item properties including definition name, instance number, layer, link type, and user attributes (UserText key-value pairs).

### 🔎 Search & Filter
Filter the tree by object or definition name with case-insensitive search.

### ↕️ Drag & Drop Reorder
Reorder items within the tree via drag and drop (within the outliner panel).

---

## Keyboard Shortcuts

| Key | Action |
|-------|--------------------------------------|
| `H` | Hide selected instance(s) |
| `S` | Show selected instance(s) |
| `I` | Isolate selected instance(s) |
| `Space` | Show All (reset visibility) |
| `F` | Zoom to selected instance |
| `Del` | Delete selected instance |
| `Enter` | BlockEdit selected instance |

---

## Installation

### Yak Package Manager (coming soon)

```
_PackageManager
```

Search for **RhinoAssemblyOutliner** and install.

### Manual Install

1. Download the latest `.rhp` file from [Releases](https://github.com/your-username/RhinoAssemblyOutliner/releases)
2. In Rhino 8, run `_PlugInManager`
3. Click **Install…** and select the `.rhp` file
4. Restart Rhino
5. Run `OpenOutliner` to open the panel

---

## Requirements

- **Rhino 8** (Windows)
- **.NET 7** runtime (ships with Rhino 8)

---

## Usage

1. Run `OpenOutliner` in the Rhino command line
2. The Assembly Outliner panel opens as a dockable side panel
3. Your document's block hierarchy is displayed automatically
4. Right-click any node for context menu actions (Select, Zoom To, Isolate, Hide, Edit Block, Properties)

---

## Roadmap

### v1.0 — Current
- Full assembly tree with visibility control, isolate mode, keyboard shortcuts, assembly mode, search, detail panel, selection sync, drag-drop reorder, status bar, mixed-state eye icons, context menu with visibility/selection/navigation/editing sections.

### v2.0 — Per-Instance Component Visibility (in progress)
- **C++ DisplayConduit** intercepting `SC_DRAWOBJECT` for per-component draw control ✅ built
- Hide individual components within a single block instance (not all instances of that definition)
- `ON_UserData` persistence — hidden component state saved in `.3dm` files ✅ built
- P/Invoke bridge with 12 native API functions ✅ built
- Document event handling (auto-sync on open/save/close) ✅ built
- Path-based component addressing for nested blocks ✅ built
- Performance target: >30 fps with 100+ managed instances

---

## License

[MIT](LICENSE) © Adrian Muff

---

## Contributing

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for guidelines.
