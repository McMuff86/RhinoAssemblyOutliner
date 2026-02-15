# SolidWorks FeatureManager vs RhinoAssemblyOutliner (RAO) — Feature Comparison

**Datum:** 15.02.2026  
**Branch:** `nightly/15-02-sprint1-refactor`  
**Autor:** Sentinel (automatisierte Analyse)

---

## 1. Feature Matrix

| Feature | SolidWorks FeatureManager | RAO (aktuell) | Status | Aufwand |
|---------|--------------------------|---------------|--------|---------|
| **Hierarchischer Baumansicht** | ✅ Vollständig (Parts, Assemblies, Features, Mates) | ✅ Block-Instanz-Hierarchie (DocumentNode → BlockInstanceNode) | ✅ Parität | — |
| **Per-Instance Visibility (Hide/Show)** | ✅ Einfach per Rechtsklick/Augensymbol | ✅ Top-Level + Nested via Native DLL + DisplayConduit | ✅ Parität | — |
| **Per-Instance Component Visibility** | ✅ Natives Feature | ✅ Eigene Implementierung (ComponentVisibilityData, NativeVisibilityInterop) | ✅ Parität | — |
| **Selection Sync (Tree ↔ Viewport)** | ✅ Bidirektional, sofort | ✅ SelectionSyncService (bidirektional) | ✅ Parität | — |
| **Zoom-to-Selected** | ✅ "Zoom to Selection" | ✅ ZoomToRequested Event | ✅ Parität | — |
| **Isolate Mode** | ✅ "Isolate" blendet alles andere aus | ✅ Isolate mit Banner + pre-isolate State Restore | ✅ Parität | — |
| **Search/Filter** | ✅ Suchfeld im Tree | ✅ SearchBox + _filterText | ✅ Parität | — |
| **Detail Panel (Properties)** | ✅ PropertyManager (umfangreich) | ⚠️ DetailPanel (Basisinfos + UserText) | 🟡 Teilweise | M |
| **Assembly Mode (Root-Selection)** | ❌ Nicht nötig (native Assembly-Dateien) | ✅ OutlinerViewMode.Assembly + SetAsAssemblyRoot | ✅ RAO-Unique | — |
| **Document/Assembly Mode Toggle** | ❌ Implizit durch Dateiformat | ✅ ModeDropdown (Document/Assembly) | ✅ RAO-Unique | — |
| **Mates/Constraints** | ✅ Vollständiges Mate-System (Coincident, Concentric, Distance, Angle, Gear, Cam, Slot, etc.) | ❌ Nicht vorhanden | 🔴 Fehlt | XL |
| **Configurations** | ✅ Multiple Konfigurationen pro Teil/Assembly (Dimensionen, Suppress, Display) | ❌ Nicht vorhanden | 🔴 Fehlt | XL |
| **Display States** | ✅ Saved visual states (Hide/Show, Colors, Transparency, Display Mode) pro Configuration | ❌ Nicht vorhanden | 🔴 Fehlt | L |
| **SpeedPak** | ✅ Lightweight Representation für Performance | ❌ Nicht vorhanden (Rhino hat kein Äquivalent) | ⚪ N/A | — |
| **Large Design Review (LDR)** | ✅ Öffnet grosse Assemblies ohne alles zu laden | ❌ Nicht vorhanden | 🟡 Nice-to-have | L |
| **Bill of Materials (BOM)** | ✅ Automatisch generiert, konfigurierbar | ❌ Nicht vorhanden | 🔴 Fehlt | L |
| **Exploded Views** | ✅ Animierte Explosionsansichten | ❌ Nicht vorhanden | 🟡 Nice-to-have | L |
| **Interference Detection** | ✅ Solid-to-Solid + Surface Checks | ❌ Nicht vorhanden | 🟡 Nice-to-have | L |
| **Drag & Drop Reorder** | ✅ Komponenten im Tree verschieben | ❌ Nicht vorhanden | 🟡 Important | M |
| **Context Menu (Vollständig)** | ✅ Open Part, Edit, Suppress, Replace, Pattern, Mirror, etc. | ⚠️ Hide/Show/Isolate/ZoomTo/SetAsRoot | 🟡 Teilweise | M |
| **Breadcrumb Navigation** | ✅ Seit 2016, erweitert 2025 (auch in LDR) | ❌ Nicht vorhanden | 🟡 Important | M |
| **Component Patterns** | ✅ Linear, Circular, Feature-driven, Sketch-driven | ❌ Nicht vorhanden (Rhino: manuell oder Grasshopper) | ⚪ N/A | — |
| **Custom Properties (Metadata)** | ✅ Im PropertyManager, verknüpft mit BOM | ⚠️ UserAttributes Dictionary auf BlockInstanceNode | 🟡 Teilweise | M |
| **Assembly Visualization** | ✅ Sortierung/Farbcodierung nach Property (Mass, Volume, etc.) | ❌ Nicht vorhanden | 🟡 Nice-to-have | L |
| **Component Preview Window** | ✅ Isolierte Geometrie-Vorschau (seit 2025) | ❌ Nicht vorhanden | 🟡 Nice-to-have | M |
| **Suppress/Unsuppress** | ✅ Temporär aus Assembly entfernen (spart Memory) | ❌ Nicht vorhanden | 🟡 Important | M |
| **Replace Component** | ✅ Komponente austauschen mit Mate-Erhalt | ❌ Nicht vorhanden | 🟡 Nice-to-have | M |
| **Linked/External References** | ✅ In-context Editing, External References Manager | ⚠️ LinkType + LinkedFilePath auf BlockInstanceNode | 🟡 Teilweise | M |
| **Tree Display Options** | ✅ Show/Hide Feature Names, Flat Tree, etc. | ❌ Nicht vorhanden | 🟡 Nice-to-have | S |
| **Multi-Select in Tree** | ✅ Ctrl/Shift + Click | ❌ Nicht vorhanden | 🔴 Fehlt | S |
| **Keyboard Shortcuts** | ✅ Umfangreich konfigurierbar | ⚠️ Tests vorhanden (KeyboardShortcutTests) | 🟡 Teilweise | S |
| **Copy with Mates** | ✅ Komponenten mit Mates duplizieren | ❌ N/A (keine Mates) | ⚪ N/A | — |
| **Document Groups** | ✅ Seit 2025: Sessions speichern/wiederherstellen | ❌ Nicht vorhanden | ⚪ N/A | — |
| **Recursive Cycle Protection** | ✅ Implizit durch Dateiformat | ✅ _visitedDefinitions + MaxRecursionDepth=100 | ✅ Parität | — |
| **Event Debouncing** | ⚠️ Internes Threading | ✅ _refreshTimer (100ms) + Interlocked | ✅ Parität | — |
| **Instance Counting** | ⚠️ Via BOM | ✅ InstanceNumber + TotalInstanceCount | ✅ RAO-Unique | — |

**Legende:** ✅ Vorhanden | ⚠️ Teilweise | ❌ Fehlt | ⚪ N/A (nicht relevant für Rhino)  
**Aufwand:** S = <1 Woche | M = 1-3 Wochen | L = 1-2 Monate | XL = 3+ Monate

---

## 2. Was SolidWorks besser kann

### 2.1 Mates/Constraints System
SolidWorks' grösster Vorteil ist das parametrische Mate-System. Benutzer definieren Beziehungen (Coincident, Concentric, Distance, Angle, Tangent, etc.) zwischen Komponenten. Das Assembly bleibt intelligent — Änderungen propagieren automatisch. **RAO kann das nicht replizieren**, weil Rhino kein parametrischer Modeler ist. Dies ist ein fundamentaler Architekturunterschiede, kein Bug.

### 2.2 Configurations & Display States
SolidWorks erlaubt multiple Konfigurationen einer Assembly (unterschiedliche Dimensionen, unterdrückte Teile, verschiedene Display States). Ein File enthält dutzende Varianten. Display States speichern visuelle Zustände (Farben, Transparenz, Hide/Show) unabhängig von der Geometrie. **RAO hat aktuell keine Möglichkeit, visuelle Zustände zu speichern und zu wechseln.**

### 2.3 Bill of Materials (BOM)
Automatische Stücklisten-Generierung mit Verknüpfung zu Custom Properties. In der Industrie essentiell für Fertigung und Beschaffung. **RAO zeigt zwar Instance-Counts, generiert aber keine exportierbare BOM.**

### 2.4 Breadcrumb Navigation
Seit SW 2016 können User durch die Hierarchie navigieren ohne den Tree zu scrollen. Besonders bei tiefen Verschachtelungen enorm hilfreich. In SW 2025 auch in Large Design Review verfügbar.

### 2.5 Multi-Select & Batch-Operationen
SolidWorks erlaubt Ctrl+Click / Shift+Click im Tree für Mehrfachauswahl mit Batch-Operationen (alle ausblenden, alle supprimieren). RAO scheint aktuell nur Einzelselektion zu unterstützen.

### 2.6 PropertyManager Tiefe
SolidWorks PropertyManager zeigt kontextsensitive Eigenschaften: Masse, Material, Mate-Details, Konfiguration, Custom Properties — alles in einem Panel. RAOs DetailPanel zeigt Basis-Infos.

---

## 3. Was RAO besser/anders kann (Rhino-spezifische Vorteile)

### 3.1 Assembly Mode aus beliebigem Block
RAOs `OutlinerViewMode.Assembly` mit `SetAsAssemblyRoot` erlaubt, jeden Block als Assembly-Root zu definieren. SolidWorks hat feste Assembly-Dateien — man kann nicht spontan eine Sub-Assembly zum Root machen, ohne sie separat zu öffnen.

### 3.2 Per-Instance Component Visibility mit Native DLL
RAOs Implementierung mit C++ Native DLL + DisplayConduit + Path-Based Addressing (`SetComponentState` mit dot-separated paths wie "1.0.2") ist technisch beeindruckend. SolidWorks hat das nativ, aber RAO musste es **von Grund auf für Rhino bauen** — ein erheblicher Engineering-Aufwand der zeigt, dass das Team Rhinos Limitierungen kreativ überwindet.

### 3.3 Nicht-destruktive Visibility (Objects bleiben selektierbar)
Die DisplayConduit-Strategie v2 hält Objekte sichtbar für Rhinos Selection-System, zeichnet aber nur sichtbare Komponenten. SolidWorks versteckt Objekte komplett (nicht mehr selektierbar wenn hidden). RAOs Ansatz ist in manchen Workflows überlegen.

### 3.4 Rhino-Ökosystem Integration
- Arbeitet mit **Rhinos Block-System** (Blocks = Components)
- Nutzt **Layers** als zusätzliche Organisationsebene
- **Grasshopper-Kompatibilität** für parametrische Workflows
- **Linked Blocks** für externe Referenzen (ähnlich SW External References, aber flexibler)

### 3.5 Leichtgewichtig & Fokussiert
RAO ist ein Plugin — kein monolithisches System. Benutzer bekommen Assembly-Management ohne den Overhead eines vollständigen parametrischen Modelers. Ideal für Architektur, Produktdesign, Visualisierung wo Rhinos NURBS-Stärken gefragt sind.

### 3.6 Instance Counting & Naming
Automatische Instanz-Nummerierung (`BlockName #1`, `#2`, etc.) mit `TotalInstanceCount` gibt sofortigen Überblick über Wiederverwendung — in SolidWorks muss man dafür die BOM konsultieren.

---

## 4. Gap-Analyse mit Priorisierung

### 🔴 Critical Gaps (blockiert Adoption)

| Gap | Warum kritisch | Aufwand |
|-----|---------------|---------|
| **Multi-Select im Tree** | Grundfunktionalität die jeder CAD-User erwartet. Ohne Multi-Select sind Batch-Operationen unmöglich. | S (1 Woche) |
| **Display States / Saved Views** | Industrienutzer müssen visuelle Zustände speichern und wechseln können (z.B. "Montageansicht", "Explosionsansicht", "Nur Gehäuse"). Ohne das wird RAO nicht als ernsthaftes Tool wahrgenommen. | L (6-8 Wochen) |
| **BOM-Export** | Jeder Ingenieur braucht Stücklisten. Mindestens CSV-Export mit Instanz-Counts, Definition-Names und Custom Properties. | M (2-3 Wochen) |

### 🟡 Important Gaps (User erwarten es)

| Gap | Warum wichtig | Aufwand |
|-----|--------------|---------|
| **Breadcrumb Navigation** | Tiefe Hierarchien werden ohne Breadcrumbs mühsam zu navigieren | M (2 Wochen) |
| **Suppress/Unsuppress** | Temporäres Entfernen von Komponenten spart Memory bei grossen Assemblies und beschleunigt Viewport | M (2-3 Wochen) |
| **Drag & Drop Reorder** | Benutzer wollen die Hierarchie organisieren können | M (2 Wochen) |
| **Erweiterte Context-Menüs** | "Select All Instances", "Replace", "Rename", "Go to Definition" | M (1-2 Wochen) |
| **Vollständige Keyboard Shortcuts** | Produktivität hängt von Shortcuts ab (H=Hide, S=Show, I=Isolate, etc.) | S (1 Woche) |

### 🟢 Nice-to-have (Differenzierung)

| Gap | Potential | Aufwand |
|-----|----------|---------|
| **Assembly Visualization** (Farbcodierung nach Property) | Cooles Feature für Design-Reviews | L (4-6 Wochen) |
| **Exploded Views** | Beeindruckend für Präsentationen, aber Grasshopper kann das teilweise | L (6-8 Wochen) |
| **Component Preview Window** | Hilft bei Selektion in dichten Assemblies | M (2-3 Wochen) |
| **Interference Detection** | Nischenbedarf in Rhino (eher Mesh-basiert) | L (6-8 Wochen) |
| **Tree Display Options** (Flat Tree, Hide Features, etc.) | Kleine QoL-Verbesserung | S (3-5 Tage) |

---

## 5. Roadmap-Vorschlag

### Phase 1: Parity (Core-Features) — 8-12 Wochen

**Ziel:** RAO wird als ernsthaftes Assembly-Management-Tool wahrgenommen.

| Woche | Feature | Aufwand |
|-------|---------|---------|
| 1 | Multi-Select im Tree (Ctrl+Click, Shift+Click) | S |
| 2-3 | BOM-Export (CSV/JSON mit Instance Counts, Definitions, UserText) | M |
| 3-4 | Erweiterte Context-Menüs + Vollständige Keyboard Shortcuts | M |
| 5-6 | Breadcrumb Navigation | M |
| 7-8 | Suppress/Unsuppress (Komponente aus Conduit entfernen, Memory sparen) | M |
| 9-10 | Display States v1 (Named visibility presets speichern/laden) | L(teil) |
| 11-12 | Drag & Drop Reorder + Detail Panel Erweiterung | M |

**Deliverable:** RAO v2.0 — "Assembly Management für Profis"

### Phase 2: Beyond SolidWorks (Rhino-Unique Features) — 8-12 Wochen

**Ziel:** RAO bietet Features die SolidWorks nicht kann.

| Woche | Feature | Differenzierung |
|-------|---------|----------------|
| 1-3 | **Grasshopper-Integration** — Assembly-Daten als GH-Data-Tree, parametrische Assembly-Steuerung | SolidWorks hat kein visuelles Scripting |
| 4-5 | **Layer-Assembly Sync** — Automatische Layer-Struktur aus Assembly-Hierarchie generieren | Rhino-unique |
| 6-7 | **Assembly Visualization** — Farbcodierung nach UserText-Properties (z.B. Material, Gewicht) | Ähnlich SW, aber flexibler |
| 8-9 | **Smart Groups** — Virtuelle Gruppierungen basierend auf Queries ("alle Schrauben M8") | SolidWorks hat das nicht |
| 10-12 | **Assembly Templates** — Speichern/Laden von Assembly-Strukturen als Templates | Schnelleres Re-Use |

**Deliverable:** RAO v3.0 — "Beyond FeatureManager"

### Realistische Zeitschätzung

| Milestone | Timeline | Voraussetzung |
|-----------|----------|---------------|
| Phase 1 Start | Sofort (Sprint 1 Refactor abschliessen) | Branch merge |
| RAO v2.0 Release | +3 Monate (Mai 2026) | 1 Entwickler Vollzeit |
| Phase 2 Start | Juni 2026 | v2.0 stabil |
| RAO v3.0 Release | +3 Monate (August 2026) | User-Feedback aus v2.0 |

**Hinweis:** Features wie Mates/Constraints und Configurations sind **architektonisch nicht sinnvoll** für Rhino. Rhino ist ein Direct Modeler — parametrische Constraints gehören in Grasshopper. RAO sollte sich auf **Assembly-Organisation und -Visualisierung** fokussieren, nicht versuchen SolidWorks' parametrisches Paradigma zu kopieren.

---

## Fazit

RAO hat bereits eine **solide Grundlage**: hierarchische Baumansicht, per-instance visibility (inkl. native DLL), selection sync, isolate mode, und search. Die grössten Lücken sind **Multi-Select**, **BOM-Export** und **Display States** — alle drei sind in Phase 1 adressierbar.

Der strategische Vorteil von RAO liegt nicht in SolidWorks-Parität, sondern in **Rhino-nativen Features** die SolidWorks nicht bieten kann: Grasshopper-Integration, Layer-Sync, und flexible Block-basierte Assemblies ohne parametrische Lock-ins.

**Empfehlung:** Phase 1 priorisieren, dann User-Feedback sammeln bevor Phase 2 finalisiert wird.
