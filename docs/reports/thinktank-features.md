# Think Tank: Feature-Analyse & Verbesserungsvorschläge

> **Datum:** 2026-02-15  
> **Repo:** RhinoAssemblyOutliner | Branch: nightly/15-02-sprint1-refactor  
> **Autor:** Think Tank Sub-Agent  
> **Status:** Complete

---

## Inhaltsverzeichnis

1. [Feature-Gap-Analyse vs SolidWorks FeatureManager](#1-feature-gap-analyse-vs-solidworks-featuremanager)
2. [UX-Verbesserungen](#2-ux-verbesserungen)
3. [Technische Verbesserungen](#3-technische-verbesserungen)
4. [Neue Feature-Ideen (priorisiert)](#4-neue-feature-ideen-priorisiert)
5. [Marktpositionierung](#5-marktpositionierung)

---

## 1. Feature-Gap-Analyse vs SolidWorks FeatureManager

### 1.1 Was SolidWorks hat, das wir NICHT haben

| # | SolidWorks Feature | Status bei uns | Relevanz für Rhino | Empfehlung |
|---|---|---|---|---|
| 1 | **Display States** (named visibility presets) | ❌ Nicht vorhanden | 🔴 Sehr hoch — Schreiner/Metallbauer brauchen Ansichten | **P0 für v1.x** |
| 2 | **Suppress/Unsuppress** (Strukturell aus Memory entladen) | ❌ Nicht vorhanden | 🟠 Hoch bei grossen Assemblies | P1 (als ComponentState.Suppressed im C++ Layer geplant) |
| 3 | **Tab/Shift+Tab** Hover-basiertes Hide/Show | ❌ Nicht vorhanden | 🟠 Hoch — Power-User-Effizienz | **P0 — Quick Win** |
| 4 | **Display Pane** (Multi-Spalten: Eye + Display Mode + Transparency + Farbe) | ⚠️ Nur Eye-Spalte | 🟡 Mittel — nice-to-have | P2 |
| 5 | **"Show Hidden Components" Mode** (invertierte Ansicht) | ❌ Nicht vorhanden | 🟠 Hoch — Wiederfinden versteckter Objekte | P1 |
| 6 | **Ctrl+Shift+Tab** (alle Hidden transparent anzeigen) | ❌ Nicht vorhanden | 🟡 Mittel | P2 |
| 7 | **Per-Instance Component Visibility (Produktion)** | ⚠️ C++ Conduit gebaut, Integration in UI fehlt | 🔴 Kritisch — **USP des Plugins** | **P0 Sprint 3-4** |
| 8 | **Edit-in-Context Transparency** (andere Parts werden transparent bei BlockEdit) | ❌ Nicht vorhanden (Rhino macht das nativ teilweise) | 🟡 Mittel | P2 |
| 9 | **Rollover Highlight** (Hover über Tree → Highlight im Viewport) | ❌ Nicht vorhanden | 🟠 Hoch — Orientierung | P1 |
| 10 | **Component Color Overrides per Instance** | ❌ Nicht vorhanden | 🟡 Mittel | P2 (C++ Infrastruktur geplant) |

### 1.2 Was für Rhino-User am wichtigsten ist

Basierend auf den typischen Workflows (Schreiner, Metallbauer, Fassadenbau):

1. **Per-Instance Component Visibility** — DAS Feature, das Rhino fehlt. Einzelne Schublade in einem Schrank ausblenden, ohne alle anderen zu beeinflussen.
2. **Display States** — "Struktur", "Komplett", "Ohne Verkleidung" als gespeicherte Presets.
3. **Keyboard Shortcuts** — H/S/I für schnelles Arbeiten.
4. **Show Hidden Components Mode** — "Wo ist das Teil hin?" ist ein reales Problem.
5. **Hover Highlight** — Bei 200+ Instanzen muss man sehen, welches Teil man im Tree hat.

### 1.3 Was in Rhinos Kontext keinen Sinn macht (weglassen)

| SolidWorks Feature | Warum nicht für Rhino |
|---|---|
| **Fix/Float Indicators (f/-)** | Rhino hat kein Constraint-System. Blocks sind immer "frei". |
| **Configurations** (Parametric Variants) | Rhino ist nicht parametrisch. Verschiedene Definitionen = Varianten. |
| **Mates/Constraints im Tree** | Fundamental anderes Paradigma. Würde eine CAD-Engine erfordern. |
| **Rollback Bar** | Kein Feature-History in Rhino. Blocks sind nicht parametrisch. |
| **Drag & Drop Reorder im Tree** | Rhino-Blocks haben keine intrinsische Reihenfolge. Die Reihenfolge im Tree ist beliebig. |
| **Lightweight/Resolved States** | Rhino hat keine "Lightweight" Darstellung für Blocks. |

---

## 2. UX-Verbesserungen

### 2.1 Aktueller UI-Flow vs. SolidWorks

**Was gut funktioniert:**
- ✅ Hierarchischer Tree mit Expand/Collapse
- ✅ Bidirektionale Selection (Tree ↔ Viewport)
- ✅ Eye-Icon Toggle
- ✅ Kontextmenü mit logischen Sektionen
- ✅ Assembly Mode (Root-Fokus)
- ✅ Suchfilter
- ✅ Detail Panel

**Was fehlt oder schlecht ist:**

| Problem | SolidWorks-Referenz | Fix |
|---|---|---|
| **Kein visuelles Feedback für Hidden Items** — versteckte Instanzen sehen im Tree gleich aus wie sichtbare | Grayed Icons + kursiver Text | Style: `TextColor = Colors.Gray`, Icon mit reduzierter Opacity |
| **Isolate Mode hat kein klares Enter/Exit** | Banner + Exit Button + ESC | `IsolateBar` Widget oben im Panel: "Isolate Mode — 3/47 visible [✕ Exit]" |
| **Kein Inline-Rename** | Slow Double-Click zum Umbenennen | Eto TreeGridView unterstützt dies begrenzt — F2-Shortcut + `BeginEdit()` |
| **Kein Multi-Select mit Shift/Ctrl** | Standard Windows Multi-Select | `AllowMultipleSelection = true` auf TreeGridView |
| **Drag & Drop fehlt** | DnD zum Umorganisieren | **Weglassen** — siehe 1.3, macht für Rhino-Blocks keinen Sinn |
| **Keine Breadcrumb-Navigation im Assembly Mode** | Path-Anzeige oben | `Label: "Document > Küche_Montage > Oberschrank_600 #1"` mit klickbaren Segmenten |

### 2.2 Keyboard Shortcuts — Vollständiger Vorschlag

**Aktuell implementiert:** H, S, I, Space, F, Del, Enter

**Fehlende, hochwertige Shortcuts:**

| Shortcut | Aktion | Priorität |
|---|---|---|
| **Esc** | Exit Isolate / Deselect All | P0 |
| **Ctrl+A** | Select All (im Tree) | P0 |
| **Ctrl+H** | Show All (Reset) | P0 |
| **Shift+H** | Show with Dependents | P1 |
| **Ctrl+Shift+H** | Hide with Dependents | P1 |
| **Tab** | Hide hovered (Viewport-Fokus, SW-Pattern) | P1 |
| **Shift+Tab** | Show at hover position | P1 |
| **Ctrl+1-9** | Display State Quick Switch | P2 |
| **F2** | Inline Rename | P2 |
| **Ctrl+E** | Expand All | P1 |
| **Ctrl+Shift+E** | Collapse All | P1 |

### 2.3 Drag & Drop

**Empfehlung: NICHT implementieren** für Tree-Reorder.

Rhino-Blocks haben keine definierte Reihenfolge. DnD suggeriert dem User eine Ordnung, die nicht existiert. SolidWorks braucht DnD wegen der parametrischen Feature-Reihenfolge — das ist in Rhino nicht relevant.

**Ausnahme:** Drag FROM Tree TO Viewport könnte nützlich sein (Block-Instanz platzieren), ist aber ein komplett anderes Feature und wäre v3.0.

### 2.4 Multi-Select

**Status:** Nicht implementiert.

**Empfehlung:** P0 für v1.0.

Multi-Select ist essentiell für:
- Mehrere Instanzen gleichzeitig ausblenden
- "Select All Same Definition" funktioniert bereits, aber manuelles Ctrl+Click fehlt
- Isolate auf Auswahl (mehrere Teile gleichzeitig isolieren)

**Implementation:** `TreeGridView.AllowMultipleSelection = true` + Anpassung aller Event Handler auf `SelectedItems` (Plural).

### 2.5 Inline-Rename

**Status:** Nicht implementiert.

Rhino unterstützt `RhinoObject.Name` — Umbenennen einer Block-Instanz ist sinnvoll. Eto.Forms `TreeGridView` hat aber limitierte Inline-Edit-Unterstützung.

**Pragmatischer Ansatz:** F2 öffnet einen kleinen Dialog (InputBox) statt echtem Inline-Edit. Aufwand: 2h. Echter Inline-Edit mit Eto: 2-3 Tage, fragil.

---

## 3. Technische Verbesserungen

### 3.1 Performance bei grossen Assemblies (1000+ Instanzen)

**Aktuelle Architektur-Probleme:**

| Problem | Impact | Lösung |
|---|---|---|
| **Full Tree Rebuild bei jedem Event** | O(n) bei jeder Änderung | Inkrementelle Updates: nur geänderte Subtrees rebuilden |
| **Alle Nodes upfront erstellt** | Langsamer Start bei 10K+ Instanzen | Lazy Loading: Children erst bei Expand laden |
| **`ObservableCollection` auf Children** | Unnötige CollectionChanged Events | Ersetzen durch `List<T>` — Tree wird ohnehin komplett neu gebaut |
| **`AssemblyNode.Id = Guid.NewGuid()`** | Instabile IDs, O(n) Suche bei Selection Sync | Rhino Object GUID als Key verwenden |
| **Kein Virtualisiertes Scrolling** | Eto TreeGridView rendert alle sichtbaren Nodes | Pagination oder Flat-List-Fallback für XL Dokumente |

**Konkrete Verbesserungen mit Aufwand:**

1. **Lazy Loading** (5 SP) — Sentinel-Child-Pattern: Expand-Arrow erscheint, Children laden on-demand.
2. **Stabile IDs** (2 SP) — `BlockInstanceNode.Id = instance.Id` statt `Guid.NewGuid()`. Ermöglicht O(1) Lookup.
3. **Inkrementelle Updates** (8 SP) — `OnAddRhinoObject` → Node einfügen statt Full Rebuild. `OnDeleteRhinoObject` → Node entfernen.
4. **Tiered Debouncing** (2 SP) — Selection: 0ms. Object add/delete: 100ms. Definition change: 250ms. Doc open: 500ms.
5. **`List<T>` statt `ObservableCollection`** (1 SP) — Trivial, aber messbar bei 10K+ Nodes.

**Performance-Ziele:**

| Szenario | Ziel |
|---|---|
| < 100 Instanzen | Instant, keine Optimierung nötig |
| 100–1'000 Instanzen | < 50ms Tree Rebuild |
| 1'000–10'000 Instanzen | < 200ms, Lazy Loading |
| 10'000+ | Virtualisierter Tree, Background Building |

### 3.2 Memory Management

**Aktuelle Probleme:**

1. **`VisibilityService` hält alte Doc-Referenz** — Wenn Dokument wechselt, zeigt der Service auf ein totes Dokument. Fix: `RuntimeSerialNumber` tracken, Service neu erstellen bei Doc-Wechsel.
2. **`PerInstanceVisibilityConduit` (C# PoC) dupliziert Geometrie pro Frame** — `var dupGeom = geom.Duplicate()` in jedem Frame → 1200 Allokationen/Sekunde bei 20 Komponenten @60fps. **Bereits gelöst** durch C++ Migration mit `dp.DrawObject()`.
3. **Timer-Leak** — `System.Timers.Timer` wird nur in `PanelClosing` disposed. Panel sollte `IDisposable` implementieren.
4. **Static `_service` in `TestPerInstanceVisibilityCommand`** — Lebt ewig, hält Doc-Referenz. Entfernen für Produktion.

**Empfehlung:** Memory-Cleanup als Teil des Sprint 1 Refactoring. Aufwand: 1 Tag.

### 3.3 Threading/Async

**Rhinos Threading-Modell:**
- RhinoCommon API: nur UI-Thread
- Display Pipeline (Conduit): Display-Thread
- `System.Timers.Timer`: ThreadPool-Thread
- Rhino Events: UI-Thread

**Aktuelle Threading-Issues:**

1. **`_needsRefresh` ohne Synchronisation** — Gelesen/geschrieben von Timer-Thread und UI-Thread. Fix: `volatile` oder `Interlocked.Exchange`.
2. **C# PerInstanceVisibilityConduit Data Race** — `_managedInstances` (HashSet) gelesen auf Display-Thread, geschrieben von UI-Thread. **Gelöst** im C++ Layer durch `CRITICAL_SECTION` + Snapshot-Pattern.
3. **Zukünftige Async-Möglichkeit:** Tree Building auf Background Thread mit `Task.Run`, Ergebnisse via `RhinoApp.InvokeOnUiThread` marshallen. Sinnvoll erst ab 5K+ Instanzen.

**C++ Conduit Snapshot-Pattern (bereits implementiert ✅):**
```
Frame Start → SC_PREDRAWOBJECTS → Single Lock → Snapshot nehmen
SC_DRAWOBJECT × N → Lockfree Snapshot lesen
SC_POSTDRAWOBJECTS → Snapshot invalidieren
```
Reduziert Lock-Acquisitions von N×M (Objekte × Kanäle) auf 1 pro Frame.

### 3.4 C++ Conduit Optimierungen

**Kritische Probleme (aus Think Tank 3):**

| Problem | Severity | Lösung |
|---|---|---|
| **Selection Highlight: Heap-Allokation pro Edge pro Frame** | 🔴 Kritisch — Framedrops bei Selektion | In SC_POSTDRAWOBJECTS verschieben, `dp.DrawObject()` statt manuelle Edge-Extraktion |
| **Kein SC_CALCBOUNDINGBOX** | 🟠 Hoch — ZoomExtents clippt | Channel registrieren, managed Instance BBoxes einbeziehen |
| **HasHiddenDescendants ist O(n)** | 🟡 Mittel | Prefix-Set vorberechnen, O(1) Lookup |
| **String-Allokationen im Hot Path** | 🟡 Mittel | `std::to_string(i)` cachen für häufige Indices (0-99) |
| **Lock-Contention** | ✅ Gelöst | Snapshot-Pattern bereits implementiert |

**Empfohlene Reihenfolge:**
1. Selection Highlight Fix → eliminiert den grössten Performance-Killer
2. SC_CALCBOUNDINGBOX → trivial, verhindert visuelle Bugs
3. Prefix-Set Optimierung → skaliert nested Blocks

---

## 4. Neue Feature-Ideen (priorisiert)

### P0: Must-have für v1.0

| Feature | Aufwand | Begründung |
|---|---|---|
| **Grayed Styling für Hidden Items** | 2h | Ohne visuelles Feedback im Tree weiss der User nicht, was hidden ist. Basic UX. |
| **Multi-Select (Ctrl+Click, Shift+Click)** | 4h | Bulk-Operationen sind essentiell. Ohne Multi-Select kann man nicht effizient arbeiten. |
| **Esc = Exit Isolate / Deselect** | 1h | Fundamentale UX-Erwartung. User drücken instinktiv Esc. |
| **Show All Button + Shortcut** | 1h | "Alles zurücksetzen" muss 1-Click sein. |
| **Show/Hide with Dependents** | 3h | Recursive tree walk — Infrastruktur existiert. Ohne dies muss man jedes nested Teil einzeln togglen. |
| **Mixed-State Parent Icon (◐)** | 2h | Eltern-Knoten muss anzeigen, wenn Kinder teilweise hidden sind. |
| **IDisposable auf Panel + Memory Cleanup** | 4h | Production readiness. Timer-Leaks, stale Doc References. |
| **Stabile Node IDs (Rhino GUID)** | 4h | Prerequisite für performante Selection Sync. |

**P0 Total: ~21h = 3 fokussierte Arbeitstage**

### P1: Nice-to-have für v1.x

| Feature | Aufwand | Begründung |
|---|---|---|
| **Per-Instance Component Visibility UI** | 21 SP (~60h) | USP des Plugins. C++ Backend steht, UI-Integration fehlt (ComponentNode, Lazy Loading, Eye-Click Routing). Sprint 3-4 Scope. |
| **Display States** (Named Visibility Presets) | 15 SP (~40h) | SolidWorks-Killer-Feature. DisplayStateManager + Dropdown + Persistence. |
| **Hover Highlight** (Tree → Viewport Preview) | 5 SP (~15h) | "Welches Teil ist das?" — Orientation bei grossen Assemblies. |
| **"Show Hidden" Mode** (invertierte Ansicht) | 8h | Alle Hidden transparent zeigen, Click = Show. Löst "wo ist mein Teil hin?" |
| **Breadcrumb Navigation** (Assembly Mode) | 8h | "Document > Assembly > Sub-Assembly" — Orientierung in tiefer Hierarchie. |
| **Lazy Loading für Tree** | 5 SP (~15h) | Performance für 1K+ Instanzen. |
| **Inkrementelle Tree Updates** | 8 SP (~24h) | Kein Full Rebuild bei jedem Event. |
| **BOM Export (CSV/Excel)** | 8h | Instanz-Zählung → Stückliste. Hoher User-Value für Schreiner/Metallbauer. |
| **"Select All Same Definition"** Context Menu Action | 2h | Alle HEA200 Träger selektieren → Zählung, Export. |
| **Suppress/Unsuppress** (ComponentState enum) | 2 Tage | Hidden vs. Suppressed. Suppressed = nicht in BBox, nicht in BOM. |

### P2: Future Vision v2.0

| Feature | Aufwand | Begründung |
|---|---|---|
| **Per-Instance Color Overrides** | 1.5 Tage C++ + 2 Tage C# | Einzelne Instanz einfärben (z.B. rot = "hier nacharbeiten"). |
| **Per-Component Transparency** | 2 Tage C++ + 1 Tag C# | Gehäuse transparent, Innenleben sichtbar. |
| **Ghost Mode** (Hidden = semi-transparent statt unsichtbar) | 5 SP | Alternative zu komplett ausblenden. Kontext bleibt erhalten. |
| **Grasshopper API** | 3 Wochen | Programmatische Visibility-Steuerung. Parametric Assembly Mode. |
| **Multi-Document / Worksession Support** | 2 Wochen | Cross-File Assemblies. |
| **Virtualisierter Tree (WebView)** | 2 Wochen | Performance für 10K+ Instanzen via HTML/JS Tree (jstree o.ä.). |
| **Component Properties Panel** | 1 Woche | Material, Farbe, Transparency pro Instanz editieren. |
| **Undo für Visibility-Änderungen** | 5 SP | C# nutzt Rhino's Undo. C++ braucht Custom Undo Records. |
| **Block Library Browser** | 2 Wochen | Block-Definitionen durchsuchen, Preview, Insert per Drag&Drop. |
| **Assembly Comparison** (Diff zweier Zustände) | 2 Wochen | "Was hat sich geändert?" — Highlighting von Unterschieden. |

---

## 5. Marktpositionierung

### 5.1 Wettbewerbsanalyse

| Feature | **RhinoAssemblyOutliner** | **VisualARQ** | **Blocks Edit** (Rhino native) | **Rhino Block Manager** |
|---|---|---|---|---|
| Hierarchischer Instanz-Baum | ✅ Vollständig, rekursiv | ⚠️ BIM-fokussiert, IFC-Hierarchie | ❌ Flat | ❌ Nur Definitionen |
| Per-Instance Visibility | ✅ **Einzigartig** (C++ Conduit) | ❌ | ❌ | ❌ |
| Per-Instance Component Visibility | ✅ **Einzigartig** | ❌ | ❌ | ❌ |
| Bidirektionale Selection | ✅ | ⚠️ BIM-Objekte only | ❌ | ⚠️ Def → Instanzen |
| Isolate Mode | ✅ | ❌ | ❌ | ❌ |
| Display States | 🔜 v1.x | ⚠️ Via IFC Views | ❌ | ❌ |
| Keyboard Shortcuts | ✅ H/S/I/F/Del | ❌ | ❌ | ❌ |
| Assembly Mode (Root-Fokus) | ✅ | ❌ | ❌ | ❌ |
| Suchfilter | ✅ | ✅ | ❌ | ⚠️ Basic |
| Preis | TBD (vermutlich $50-100) | ~€800+ | Kostenlos (Rhino) | Kostenlos (Rhino) |
| C++ Performance | ✅ Hybrid | ❌ Rein C# | N/A | N/A |
| .3dm Persistence | ✅ ON_UserData | ✅ Eigenes Format | N/A | N/A |

### 5.2 USP — Unique Selling Proposition

**"Der fehlende Assembly Manager für Rhino."**

Drei USPs, die kein anderes Rhino-Plugin bietet:

1. **Per-Instance Component Visibility** — Einzelne Bauteile innerhalb einer spezifischen Block-Instanz ein/ausblenden, ohne andere Instanzen zu beeinflussen. Kein anderes Rhino-Plugin kann das.

2. **SolidWorks-artige UX** — Eye-Icons, Isolate Mode, Keyboard Shortcuts, Mixed-State Icons. Fühlt sich an wie ein professionelles CAD-Tool, nicht wie ein Rhino-Addon.

3. **C++ Performance** — Hybrid C#/C++ Architektur mit Display Pipeline Conduit. Kein "Ghost Artifact"-Problem wie bei reinen C#-Conduits. Nativ schnell.

### 5.3 Zielgruppe

**Primär:**
- **Schreiner/Möbelbauer** — Komplexe Möbelstücke mit Beschlägen, Scharnieren, Schubladen. 50-500 Block-Instanzen.
- **Metallbauer** — Stahlkonstruktionen mit Trägern, Verbindungen, Bolzen. 100-2000 Instanzen.
- **Fassadenbauer** — Repetitive Panels mit Unterkonstruktion. 100-1000 Instanzen.

**Sekundär:**
- Architekten die Rhino für Detail-Design nutzen (nicht nur Konzept)
- Product Designer mit Assembly-artigen Modellen
- Alle Rhino-User die den Block Manager unzureichend finden

### 5.4 Positionierung vs. VisualARQ

VisualARQ ist **kein direkter Konkurrent** — es ist ein BIM-Tool. Überschneidung nur bei "Baumstruktur in Rhino". Aber:

| | RhinoAssemblyOutliner | VisualARQ |
|---|---|---|
| **Fokus** | Block-Assembly-Management | BIM/IFC |
| **Zielgruppe** | Schreiner, Metallbauer, Produktdesigner | Architekten |
| **Preis** | ~$50-100 | ~€800+ |
| **Lernkurve** | Minimal (SolidWorks-User fühlen sich sofort zuhause) | Hoch (BIM-Konzepte) |
| **Overhead** | Zero — lightweight Plugin | Schwer — verändert Rhino-Workflow fundamental |

**Klare Abgrenzung:** Wir sind das Tool für "Ich baue Dinge aus Blöcken und will sie effizient verwalten." VisualARQ ist "Ich baue Gebäude nach BIM-Standards."

### 5.5 Pricing-Strategie (Empfehlung)

- **v1.0:** $49 (Einmalkauf) oder $39/Jahr Subscription
- **v2.0 (mit Per-Instance Component Visibility):** $79 Einmalkauf oder $59/Jahr
- **Kostenlose Trial:** 30 Tage Full-Feature
- **Distribution:** Rhino Yak Package Manager + eigene Website
- **food4Rhino Listing** für Sichtbarkeit

---

## Zusammenfassung: Die wichtigsten Handlungen

### Sofort (Sprint 1 Refactor — diese Woche)

1. ✅ Grayed Styling für Hidden Items (2h)
2. ✅ Multi-Select aktivieren (4h)
3. ✅ Esc = Exit Isolate (1h)
4. ✅ Show All Button (1h)
5. ✅ Memory Cleanup (IDisposable, stale refs) (4h)
6. ✅ Stabile Node IDs (4h)
7. ✅ C++ Selection Highlight Fix (2 Tage)
8. ✅ C++ SC_CALCBOUNDINGBOX (0.5 Tage)

### Sprint 2 (v1.0 Polish)

9. Show/Hide with Dependents
10. Mixed-State Icon
11. Breadcrumb Navigation
12. BOM Export (CSV)
13. Inkrementelle Tree Updates (mindestens für Add/Delete)

### Sprint 3-4 (v1.x — Per-Instance Component Visibility UI)

14. ComponentNode Model + Lazy Loading
15. Eye-Click Routing (C# → C++ basierend auf Node-Type)
16. Display States (Named Presets)
17. Hover Highlight (Tree → Viewport)

### Sprint 5+ (v2.0 Vision)

18. Color/Transparency Overrides per Instance
19. Ghost Mode
20. Grasshopper API
21. Virtualisierter Tree

---

*"Don't try to replicate SolidWorks. Replicate its interaction patterns while respecting Rhino's architecture. Users don't want SolidWorks-in-Rhino; they want Rhino-with-assembly-management."*
