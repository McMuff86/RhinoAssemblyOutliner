# CLAUDE.md - RhinoAssemblyOutliner

## Projekt-Übersicht

Ein SolidWorks FeatureManager-artiger **Assembly Outliner** für Rhino 8. Zeigt Block-Hierarchien in einer dockbaren Baumstruktur.

**Repo:** https://github.com/McMuff86/RhinoAssemblyOutliner
**Stack:** C# / .NET 7.0 / RhinoCommon 8.0 / Eto.Forms

## Projekt-Struktur

```
RhinoAssemblyOutliner/
├── src/RhinoAssemblyOutliner/
│   ├── RhinoAssemblyOutlinerPlugin.cs   # Plugin-Einstiegspunkt
│   ├── Model/                            # Datenmodelle
│   │   ├── AssemblyNode.cs              # Basis-Knoten
│   │   ├── BlockInstanceNode.cs         # Block-Instanz
│   │   ├── DocumentNode.cs              # Dokument-Root
│   │   └── AssemblyTreeBuilder.cs       # Tree-Builder
│   ├── UI/                               # Eto.Forms UI
│   │   ├── AssemblyOutlinerPanel.cs     # Haupt-Panel (IPanel)
│   │   ├── AssemblyTreeView.cs          # TreeGridView
│   │   └── DetailPanel.cs               # Properties
│   ├── Commands/                         # Rhino Commands
│   │   └── OpenOutlinerCommand.cs
│   └── Services/                         # Business Logic
├── tests/                                # xUnit Tests
└── docs/
    ├── SPEC.md                          # Detaillierte Spezifikation
    ├── ARCHITECTURE.md                  # Architektur-Diagramme
    ├── RESEARCH.md                      # API-Recherche
    └── TEST_PLAN.md                     # Testplan
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

| Klasse | Zweck |
|--------|-------|
| `AssemblyTreeBuilder` | Baut den hierarchischen Baum aus RhinoDoc |
| `AssemblyOutlinerPanel` | Dockbares Panel mit IPanel Interface |
| `BlockInstanceNode` | Repräsentiert eine Block-Instanz im Baum |
| `AssemblyTreeItem` | Eto TreeGridItem Wrapper |

## Commands

- `AssemblyOutliner` - Öffnet das Panel
- `AssemblyOutlinerRefresh` - Aktualisiert den Baum manuell

## Build & Test

```bash
# Build
dotnet build

# Tests
dotnet test

# In Rhino laden
# Plugin-DLL nach Rhino Plugins-Ordner kopieren
```

## Offene Design-Entscheidungen

1. **Performance-Schwelle:** Ab wann Lazy Loading? (1000+ Nodes?)
2. **Block Edit Integration:** Wie tief anbinden?
3. **Mac-Kompatibilität:** Testen erforderlich

## Ressourcen

- [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/)
- [Eto.Forms Docs](http://pages.picoe.ca/docs/api/)
- [Rhino Panel Sample](https://github.com/mcneel/rhino-developer-samples/tree/7/rhinocommon/cs/SampleCsEto)

---

*Siehe `progress.txt` für aktuelle Tasks.*
