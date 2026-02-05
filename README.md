# Rhino Assembly Outliner

[![Rhino 8](https://img.shields.io/badge/Rhino-8-blue?logo=rhinoceros)](https://www.rhino3d.com/)
[![.NET 7.0+](https://img.shields.io/badge/.NET-7.0+-purple?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()

A SolidWorks FeatureManager-style **Assembly Outliner** for Rhino 8 that displays block hierarchies, nesting, and component status in a persistent, dockable tree structure.

---

## 🎯 The Problem

Rhino's built-in Block Manager shows a **flat list of block definitions** — not the actual instance hierarchy in your document. This makes navigating complex assemblies difficult.

**What's missing:**
- No hierarchical instance tree
- No parent → child context
- Limited bidirectional selection
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

### Planned Features
- 📊 BOM (Bill of Materials) export
- 🎨 Custom icons per block type
- 💾 Saved tree configurations

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

For detailed usage, see the [User Guide](docs/USER_GUIDE.md).

---

## 🏗️ Architecture

Built with:
- **Eto.Forms** — Cross-platform UI framework
- **RhinoCommon** — Rhino 8 API
- **C# / .NET 7** — Modern, type-safe code

```
RhinoAssemblyOutliner/
├── Commands/        # Rhino commands
├── UI/              # Eto.Forms panels and controls
├── Model/           # Tree data structures
├── Services/        # Selection, visibility, events
└── Resources/       # Icons and assets
```

See [ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed design documentation.

---

## 🤝 Contributing

Contributions are welcome! Please read:

1. [CONTRIBUTING.md](CONTRIBUTING.md) — Development setup & guidelines
2. [ARCHITECTURE.md](docs/ARCHITECTURE.md) — Technical overview
3. [SPEC.md](docs/SPEC.md) — Feature specification

### Development Setup

```bash
# Clone
git clone https://github.com/your-org/RhinoAssemblyOutliner.git
cd RhinoAssemblyOutliner

# Build
dotnet restore
dotnet build

# Debug in Rhino 8
# Configure VS/Rider to launch Rhino as debug target
```

---

## 📖 Documentation

| Document | Description |
|----------|-------------|
| [SPEC.md](docs/SPEC.md) | Feature specification & comparison |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Technical architecture |
| [USER_GUIDE.md](docs/USER_GUIDE.md) | End-user documentation |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Development guide |

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
