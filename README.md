# Rhino Assembly Outliner

[![Rhino 8](https://img.shields.io/badge/Rhino-8-blue?logo=rhinoceros)](https://www.rhino3d.com/)
[![.NET 7.0+](https://img.shields.io/badge/.NET-7.0+-purple?logo=dotnet)](https://dotnet.microsoft.com/)
[![C++17](https://img.shields.io/badge/C++-17-orange?logo=cplusplus)](https://isocpp.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()

A SolidWorks FeatureManager-style **Assembly Outliner** for Rhino 8 that displays block hierarchies, nesting, and component status in a persistent, dockable tree structure.

**Hybrid C++/C# Architecture** for native performance and modern UI.

---

## 🎯 The Problem

Rhino's built-in Block Manager shows a **flat list of block definitions** — not the actual instance hierarchy in your document. This makes navigating complex assemblies difficult.

**What's missing:**
- No hierarchical instance tree
- No parent → child context
- Limited bidirectional selection
- **No per-instance component visibility** ← Game-changer!
- No BOM export from structure

## ✨ The Solution

Assembly Outliner provides the **missing hierarchical instance tree** that shows your actual document structure:

```
📄 Kitchen_Assembly.3dm
├─ 📦 UpperCabinet_600 #1     👁 Layer: Furniture::Upper
│   ├─ 📦 Hinge_Blum_110 #1   👁
│   ├─ 📦 Hinge_Blum_110 #2   👁
│   └─ ⬡ SidePanel_L          👁
├─ 📦 UpperCabinet_600 #2     👁
├─ 📦 LowerCabinet_600 #1     👁
│   ├─ 📦 Drawer_500 #1       👁
│   └─ 📦 Drawer_500 #2       👁
└─ 📦 Countertop_L #1         👁
```

---

## 📋 Features

### Navigation & Visualization
- 🌳 **Hierarchical Tree** — Recursive block instance visualization
- 🔢 **Instance Count** — See how many of each definition exist
- 📍 **Layer Display** — Layer assignment per instance
- 🔗 **Link Type** — Embedded, Linked, or EmbeddedAndLinked

### Interaction
- 🔄 **Bidirectional Selection** — Click in tree ↔ select in viewport
- 👁 **Visibility Toggle** — Show/Hide/Isolate per entry
- 🔍 **Search & Filter** — Find components quickly
- 📋 **Context Menu** — Select all same, edit block, zoom to
- 🏗️ **Assembly Mode** — Focus on a single sub-assembly root

### ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **H** | Hide selected |
| **S** | Show selected |
| **I** | Isolate selected |
| **Space** | Show All (reset visibility) |
| **F** | Zoom to / Frame selected |
| **Del** | Delete selected |
| **Enter** | Edit block (BlockEdit) |
| **F5** | Refresh tree |
| **Ctrl+F** | Focus search bar |
| **Esc** | Clear search / exit isolate |

### 🆕 Per-Instance Component Visibility (v2.0)

**The killer feature Rhino doesn't have!**

Hide individual components within a single block instance — without affecting other instances of the same definition:

```
📦 Cabinet_600 #1     👁️ (all visible)
│   ├─ ⬡ Korpus       👁️
│   ├─ ⬡ Tür          〰️ ← HIDDEN only in this instance
│   └─ ⬡ Rückwand     👁️

📦 Cabinet_600 #2     👁️ (all visible)
│   ├─ ⬡ Korpus       👁️
│   ├─ ⬡ Tür          👁️ ← Still visible here!
│   └─ ⬡ Rückwand     👁️
```

Achieved through a **native C++ DisplayConduit** that intercepts Rhino's rendering pipeline. See [Architecture](#architecture).

---

## 📦 Installation

### Requirements
- **Rhino 8** (Windows or macOS)
- **.NET 7.0+** (included with Rhino 8)

### Via Package Manager
*Coming soon — will be available on Rhino Package Manager*

### Manual Installation
1. Download the latest `.rhp` from [Releases](https://github.com/your-org/RhinoAssemblyOutliner/releases)
2. In Rhino, run `PlugInManager`
3. Click **Install...** and select the downloaded file
4. Restart Rhino

---

## 🚀 Quick Start

1. Open a Rhino document with block instances
2. Run command: `AssemblyOutliner`
3. Dock the panel where convenient
4. Click any node to select in viewport
5. Use **H** / **S** / **I** / **Space** for visibility control

For detailed usage, see the [User Guide](docs/USER_GUIDE.md).

---

## 🏗️ Architecture

**Hybrid C++/C# Plugin** — best of both worlds:

| Component | Language | Purpose |
|-----------|----------|---------|
| **DisplayConduit** | C++ | Intercept rendering, custom component visibility |
| **UserData** | C++ | Persist visibility state to .3dm file |
| **UI (Eto.Forms)** | C# | Modern, responsive tree interface |
| **Commands** | C# | Rhino command integration |
| **Services** | C# | Business logic, event handling |

For the full architecture with data flow diagrams, state management, and P/Invoke API surface, see **[ARCHITECTURE_V2.md](docs/ARCHITECTURE_V2.md)**.

Design decisions are documented in **[ADRs](docs/plans/ADR/)**.

---

## 🗺️ Roadmap

| Version | Status | Focus |
|---------|--------|-------|
| **v0.1.0** | ✅ Done | Core outliner: tree, selection sync, visibility, search, assembly mode |
| **v0.2.0** | 🔄 In Progress | UX polish: keyboard shortcuts, grayed hidden items, isolate flow, status bar |
| **v1.0.0** | 📋 Planned | Stable release: bug fixes, tests, Yak package, documentation |
| **v2.0.0** | 📋 Planned | Per-instance component visibility via C++ DisplayConduit |

See [SPRINT_PLAN.md](docs/plans/SPRINT_PLAN.md) for detailed sprint breakdown.

---

## 🤝 Contributing

Contributions are welcome! See the [Contributing Guide](docs/CONTRIBUTING.md) for:

- How to build from source
- Project structure
- Coding conventions & commit format
- Testing instructions

---

## 📖 Documentation

| Document | Description |
|----------|-------------|
| [User Guide](docs/USER_GUIDE.md) | End-user documentation |
| [Architecture V2](docs/ARCHITECTURE_V2.md) | Technical architecture (hybrid C++/C#) |
| [Sprint Plan](docs/plans/SPRINT_PLAN.md) | Development roadmap |
| [Contributing](docs/CONTRIBUTING.md) | Development guide |
| [Changelog](CHANGELOG.md) | Version history |
| [ADRs](docs/plans/ADR/) | Architecture Decision Records |

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

---

## 🙏 Acknowledgments

- Inspired by SolidWorks FeatureManager
- Built with [RhinoCommon](https://developer.rhino3d.com/)
- UI powered by [Eto.Forms](https://github.com/picoe/Eto)

---

<p align="center">
  Made with ❤️ for the Rhino community
</p>
