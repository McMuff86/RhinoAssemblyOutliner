# CLAUDE.md - RhinoAssemblyOutliner (BlockForge)

## Projekt-Übersicht

**BlockForge** — ein SolidWorks FeatureManager-artiger Assembly Outliner für Rhino 8 mit Per-Instance Component Visibility.

**Repo:** https://github.com/McMuff86/RhinoAssemblyOutliner  
**Stack:** C# / .NET 7.0 / RhinoCommon 8.0 / Eto.Forms + C++ / Rhino 8 C++ SDK

## Architektur: Hybrid C#/C++ (v3 — Definition Cloning)

> **DisplayConduit-Ansatz aufgegeben** (Rhino rendert Block-Instanzen atomar).  
> **Neuer Ansatz:** Definition Cloning + ON_UserData + Custom Grips.

```
┌─────────────────────────────────────────────────────────────┐
│                    HYBRID ARCHITECTURE                       │
│                                                             │
│  ┌──────────────────┐     ┌──────────────────────────────┐  │
│  │  C++ Native DLL  │     │  C# RhinoCommon Plugin       │  │
│  │                  │     │                              │  │
│  │  • ON_UserData   │◄───►│  • AssemblyManager           │  │
│  │    (persistence) │     │  • VariantManager (cloning)  │  │
│  │  • Event Handler │     │  • ConfigurationService      │  │
│  │  • Custom Grips  │     │  • UI Panel (Eto)            │  │
│  └──────────────────┘     └──────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Projekt-Struktur

```
RhinoAssemblyOutliner/
├── ROADMAP.md                             # Phasen-Roadmap (BlockForge Vision)
├── CHANGELOG.md                           # Changelog (Keep a Changelog format)
├── src/
│   ├── RhinoAssemblyOutliner/             # C# Plugin (UI + Commands)
│   │   ├── Model/                         # Datenmodelle (AssemblyNode, TreeBuilder)
│   │   ├── UI/                            # Eto.Forms (Panel, TreeView, DetailPanel)
│   │   ├── Commands/                      # Rhino Commands
│   │   └── Services/                      # Business Logic + PerInstanceVisibility
│   └── RhinoAssemblyOutliner.native/      # C++ Native DLL
│       ├── NativeApi.h/cpp                # P/Invoke Bridge
│       └── CustomObject/                  # Assembly Object Prototyp
├── tests/                                 # xUnit Tests (97 tests)
├── docs/
│   ├── architecture/                      # ← AKTUELLE ARCHITEKTUR
│   │   └── assembly-object-architecture.md  # Hybrid Architecture Design
│   ├── vision/
│   │   └── product-vision-v2.md           # BlockForge Product Vision
│   ├── research/                          # Research (alle relevant)
│   │   ├── architecture-proposal-v3.md    # Definition Cloning Proposal
│   │   ├── custom-object-feasibility.md   # C++ Custom Object Feasibility
│   │   ├── cpp-custom-objects-research.md # C++ SDK Research
│   │   ├── visualarq-reverse-engineering.md
│   │   ├── solidworks-configurations-research.md
│   │   ├── per-instance-visibility-research.md
│   │   ├── existing-solutions-research.md
│   │   └── eto-ui-fixes.md
│   ├── plans/                             # Sprint Planning
│   │   └── SPRINT_PLAN.md                 # Aktiver Sprint Plan (Sprint 3-8+)
│   ├── reports/                           # Reports
│   └── archive/                           # ← ARCHIVIERTE DOCS (Phase 1, veraltet)
│       └── README.md                      # Erklärt was archiviert wurde
└── progress.txt                           # Task Tracker
```

## Architektur-Kernkonzepte

### Definition Cloning (VariantManager)
- Original-Definition bleibt unverändert
- Pro Visibility-State wird eine Variant-Definition erstellt (Geometrie ohne hidden Components)
- Naming: `{OriginalName}__aov_{hash8}` (Assembly Outliner Variant)
- Deduplizierung: gleiche States teilen eine Variant

### ON_UserData (C++ Persistence)
- `ON_AssemblyUserData` auf jedem Assembly-InstanceObject
- Speichert: sourceDefinitionId, activeConfigName, configurations[]
- Archive()=true → überlebt Save/Load
- Round-trip safe ohne Plugin

### Configurations
- Named Visibility-Presets pro Instance
- Vererbung: Parent → Derived
- Implizite "Default" Config (alles sichtbar)

## Entwicklungs-Richtlinien

### Code-Stil
- C# 11 mit nullable reference types
- XML-Dokumentation für alle public APIs
- `_camelCase` für private Felder
- Ein Klasse pro File

### RhinoCommon Patterns
- Panel via `IPanel` Interface
- Events in `PanelClosing()` unsubscriben
- `RhinoApp.InvokeOnUiThread()` für UI-Updates
- Event-Debouncing (100ms Timer)
- `BeginUndoRecord`/`EndUndoRecord` für multi-step Ops

## Commands

- `AssemblyOutliner` — Öffnet das Panel
- `AssemblyOutlinerRefresh` — Aktualisiert den Baum

## Build & Test

```bash
dotnet build          # C# Plugin
dotnet test           # 97 xUnit Tests
```

C++ Native DLL: VS 2022 mit Rhino 8 C++ SDK.

## Archivierte Dokumentation

Alte Docs (DisplayConduit-Ansatz, Sprint 1+2 Reviews) in `docs/archive/`.  
Siehe `docs/archive/README.md` für Details.

---

*Siehe `ROADMAP.md` für die Projekt-Roadmap und `docs/plans/SPRINT_PLAN.md` für aktive Sprint-Planung.*
