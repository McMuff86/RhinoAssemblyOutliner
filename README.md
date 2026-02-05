# Rhino Assembly Outliner

A SolidWorks FeatureManager-style **Assembly Outliner** for Rhino 8 that displays block hierarchies, nesting, and component status in a persistent, dockable tree structure.

## Overview

Rhino's Block Manager shows a flat list of block definitions. This plugin provides what's missing: a **hierarchical instance tree** showing the actual document structure.

```
📄 Kitchen_Assembly.3dm
├─ 📦 UpperCabinet_600 #1 👁 Layer: Furniture::Upper
│   ├─ 📦 Hinge_Blum_110 #1 👁
│   ├─ 📦 Hinge_Blum_110 #2 👁
│   └─ ⬡ SidePanel_L 👁
├─ 📦 UpperCabinet_600 #2 👁
└─ 📦 Countertop_L #1 👁
```

## Features

- **Hierarchical Tree**: Recursive block instance visualization
- **Bidirectional Selection**: Click in tree ↔ select in viewport
- **Visibility Toggle**: Show/Hide/Isolate per entry
- **Instance Info**: Layer, link type, user attributes
- **Search & Filter**: Find components quickly
- **Context Menu**: Select all same, edit block, zoom to

## Requirements

- Rhino 8 (Windows/Mac)
- .NET 7.0+

## Installation

*Coming soon*

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup.

## License

MIT License - see [LICENSE](LICENSE)
