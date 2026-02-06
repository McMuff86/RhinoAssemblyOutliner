# CLAUDE.md - RhinoAssemblyOutliner

## Projekt-Übersicht

Ein SolidWorks FeatureManager-artiger **Assembly Outliner** für Rhino 8. Zeigt Block-Hierarchien in einer dockbaren Baumstruktur.

**Repo:** https://github.com/McMuff86/RhinoAssemblyOutliner
**Stack:** C# / .NET 7.0 / RhinoCommon 8.0 / Eto.Forms + C++ / Rhino 8 C++ SDK

## Architektur: Hybrid C#/C++

```
┌──────────────────────────────────────────────────────┐
│                     Rhino 8                          │
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────────────────┐                            │
│  │  C# Plugin (.rhp)    │  ← Einziges Rhino Plugin  │
│  │                      │                            │
│  │  - Outliner Panel    │                            │
│  │  - Commands          │     P/Invoke               │
│  │  - Tree View         │──────────────┐             │
│  │  - Selection Sync    │              │             │
│  └──────────────────────┘              ▼             │
│                            ┌──────────────────────┐  │
│                            │  C++ DLL (.dll)      │  │
│                            │                      │  │
│                            │  - Display Conduit   │  │
│                            │  - Block Rendering   │  │
│                            │  - Visibility Logic  │  │
│                            │  - Cache Management  │  │
│                            └──────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Projekt-Struktur

```
RhinoAssemblyOutliner/
├── RhinoAssemblyOutliner.sln              # Solution (C# + C++ Projekte)
├── src/
│   ├── RhinoAssemblyOutliner/             # C# Plugin (UI + Commands)
│   │   ├── RhinoAssemblyOutlinerPlugin.cs # Plugin-Einstiegspunkt
│   │   ├── Model/                         # Datenmodelle
│   │   │   ├── AssemblyNode.cs            # Basis-Knoten
│   │   │   ├── BlockInstanceNode.cs       # Block-Instanz
│   │   │   ├── DocumentNode.cs            # Dokument-Root
│   │   │   ├── OutlinerViewMode.cs        # Assembly/Document Mode
│   │   │   └── AssemblyTreeBuilder.cs     # Tree-Builder
│   │   ├── UI/                            # Eto.Forms UI
│   │   │   ├── AssemblyOutlinerPanel.cs   # Haupt-Panel (IPanel)
│   │   │   ├── AssemblyTreeView.cs        # TreeGridView
│   │   │   └── DetailPanel.cs             # Properties
│   │   ├── Commands/                      # Rhino Commands
│   │   │   ├── OpenOutlinerCommand.cs
│   │   │   ├── RefreshOutlinerCommand.cs
│   │   │   └── TestPerInstanceVisibilityCommand.cs
│   │   └── Services/                      # Business Logic
│   │       ├── SelectionSyncService.cs
│   │       ├── VisibilityService.cs
│   │       └── PerInstanceVisibility/     # C# PoC (wird durch C++ ersetzt)
│   │           ├── ComponentVisibilityData.cs
│   │           ├── PerInstanceVisibilityConduit.cs
│   │           └── PerInstanceVisibilityService.cs
│   │
│   └── RhinoAssemblyOutliner.native/      # C++ Native DLL
│       ├── RhinoAssemblyOutliner.native.vcxproj
│       ├── RhinoAssemblyOutliner.native.def  # Export-Definitionen
│       ├── stdafx.h/cpp                   # Precompiled Header + Rhino SDK
│       ├── RhinoAssemblyOutliner.nativeApp.h/cpp  # MFC DLL Entry
│       └── NativeApi.h/cpp                # Exportierte C API (P/Invoke)
│
├── tests/                                 # xUnit Tests
├── docs/
│   ├── SPEC.md                            # Detaillierte Spezifikation
│   ├── ARCHITECTURE.md                    # Architektur-Diagramme
│   ├── CPP_ROADMAP.md                     # C++ Implementation Roadmap
│   ├── CPP_SDK_RESEARCH.md               # C++ SDK API Research
│   ├── PER_INSTANCE_VISIBILITY.md        # PoC Ergebnisse + Learnings
│   ├── FEATURE_ASSEMBLY_MODE.md          # Assembly Mode Feature
│   ├── PACKAGING.md                      # Yak Distribution
│   ├── USER_GUIDE.md                     # Benutzerhandbuch
│   └── TEST_PLAN.md                      # Testplan
└── progress.txt                           # Task Tracker
```

## Entwicklungs-Richtlinien

### Code-Stil
- C# 11 mit nullable reference types
- XML-Dokumentation für alle public APIs
- `_camelCase` für private Felder
- Ein Klasse pro File

### RhinoCommon Patterns
- Panel via `IPanel` Interface registrieren
- Events in `PanelClosing()` unsubscriben (Memory Leaks!)
- `RhinoApp.InvokeOnUiThread()` für UI-Updates
- Event-Debouncing für Performance (100ms Timer)

### Block-Traversierung
```csharp
// Rekursiv durch verschachtelte Blöcke
for (int i = 0; i < definition.ObjectCount; i++)
{
    var obj = definition.Object(i);
    if (obj is InstanceObject nested)
        ProcessBlock(nested.InstanceDefinition);  // Rekursion
}
```

## Wichtige Klassen

| Klasse | Sprache | Zweck |
|--------|---------|-------|
| `AssemblyTreeBuilder` | C# | Baut den hierarchischen Baum aus RhinoDoc |
| `AssemblyOutlinerPanel` | C# | Dockbares Panel mit IPanel Interface |
| `BlockInstanceNode` | C# | Repräsentiert eine Block-Instanz im Baum |
| `AssemblyTreeItem` | C# | Eto TreeGridItem Wrapper |
| `NativeApi` | C++ | Exportierte C API für Per-Instance Visibility (P/Invoke) |

## Commands

- `AssemblyOutliner` - Öffnet das Panel
- `AssemblyOutlinerRefresh` - Aktualisiert den Baum manuell

## Build & Test

```bash
# C# Plugin Build
dotnet build

# C++ Native DLL Build (benötigt Rhino 8 C++ SDK + MSVC v142 Toolset)
MSBuild.exe src\RhinoAssemblyOutliner.native\RhinoAssemblyOutliner.native.vcxproj -p:Configuration=Release -p:Platform=x64

# Tests
dotnet test

# In Rhino laden
# Plugin-DLL + Native-DLL in denselben Ordner kopieren
```

### Build-Voraussetzungen C++
- Visual Studio mit MSVC v142 (VS 2019) Toolset
- Rhino 8 C++ SDK (installiert unter `C:\Program Files\Rhino 8 SDK\`)
- Windows 10 SDK

## Offene Design-Entscheidungen

1. **Performance-Schwelle:** Ab wann Lazy Loading? (1000+ Nodes?)
2. **Block Edit Integration:** Wie tief anbinden?
3. **Mac-Kompatibilität:** Testen erforderlich

## Ressourcen

- [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/)
- [Eto.Forms Docs](http://pages.picoe.ca/docs/api/)
- [Rhino Panel Sample](https://github.com/mcneel/rhino-developer-samples/tree/7/rhinocommon/cs/SampleCsEto)

---

## Multi-Agent Setup

Dieses Projekt nutzt ein **Multi-Agent System** für effiziente Entwicklung. Jeder Agent hat spezialisierte Aufgaben.

### Agent-Rollen

| Agent | Rolle | Output |
|-------|-------|--------|
| Coordinator | Orchestration, Synthese, User-Kommunikation | Commits, `CLAUDE.md` |
| Research | CAD-Industrie Analyse, Best Practices | `research/` |
| Coder | Implementation, Bug-Fixes, Tests | `src/` |
| Docs | Dokumentation, User Guides | `docs/`, root |

---

*Siehe `progress.txt` für aktuelle Tasks.*
