# Sprint Plan — RhinoAssemblyOutliner

> **Letzte Aktualisierung:** 2026-02-15  
> **Branch:** `nightly/15-02-sprint1-refactor`  
> **Verfügbare Zeit:** ~10-15h/Woche (Abende/Wochenenden) + Nacht-Sessions mit Agents

---

## Übersicht

**Vision:** Der fehlende Assembly Manager für Rhino — SolidWorks-artige UX mit Per-Instance Component Visibility als USP.

**Ziel v1.0:** Polished C#-only Plugin auf Yak veröffentlichen.  
**Ziel v2.0:** Per-Instance Component Visibility (C++ Conduit) produktionsreif.  
**Ziel v3.0:** SolidWorks-Parity (Display States, BOM, Multi-Select) + Beyond.

**Timeline (realistisch bei 10-15h/Woche):**

| Milestone | Sprint | Ziel-Datum | Status |
|-----------|--------|-----------|--------|
| v1.0-rc | Sprint 1 | ✅ 2026-02-15 | ✅ DONE |
| v1.0 | Sprint 2 | KW 9 (Ende Feb) | 🔄 ~50% |
| v2.0-alpha | Sprint 3 | KW 11 (Mitte Mär) | 🔄 ~60% |
| v2.0 | Sprint 4 | KW 14 (Ende Mär) | ⏳ |
| v2.1 Quick Wins | Sprint 5 | KW 17 (Ende Apr) | ⏳ |
| v3.0 Parity | Sprint 6-7 | KW 24 (Mitte Jun) | ⏳ |
| v4.0 Beyond | Sprint 8+ | Q3 2026 | ⏳ |

---

## Sprint 1: v1.0-rc — UX Polish ✅ DONE

**Dauer:** 1 Woche | **Status:** ✅ KOMPLETT (2026-02-15) | **Effort:** ~16h

- [x] 1.1 Keyboard Shortcuts (H, Shift+H, I, Esc, F, Ctrl+H, Space, Enter) — 3h
- [x] 1.2 Grayed/italic Styling für Hidden Items — 1.5h
- [x] 1.3 Mixed-State Parent Eye Icon (◐) — 1.5h
- [x] 1.4 Show All + Ctrl+Shift+H — 30min
- [x] 1.5 Show with Dependents (rekursiv) — 2h
- [x] 1.6 Hide with Dependents (rekursiv) — 1h
- [x] 1.7 Isolate Mode mit Banner + Esc Exit + State Restore — 3h
- [x] 1.8 Collapse All / Expand All Toolbar — 30min
- [x] 1.9 Double-Click → BlockEdit — 30min
- [x] 1.10 Status Bar — 1h
- [x] 1.11 Context Menu Restructure — 1.5h

**Bonus (erledigt):**
- [x] Refactoring Items 1-3 (Stable Node IDs, Doc Reference Leak, ObservableCollection) — *Quelle: REVIEW_SPRINT1*
- [x] IDisposable Pattern, XML Docs, EditorConfig — *Quelle: Review*
- [x] Drag & Drop + Ctrl+Up/Down Reorder — *Quelle: Think Tank*

---

## Sprint 2: v1.0 — Release Prep (aktuelle Woche)

**Dauer:** 1 Woche | **Status:** 🔄 ~50% | **Effort geplant:** ~18h

### Bereits erledigt
- [x] 2.2 Fix VisibilityService Doc Reference Leak — 1h — *Quelle: Refactoring Checklist*
- [x] 2.3 Fix Duplicate Panel Registration → Plugin.OnLoad() — 30min — *Quelle: Review*
- [x] 2.4 IDisposable auf Panel (Timer Cleanup) — 30min — *Quelle: Review*

### Offen
- [ ] 2.1 **Bug Bash** — systematisches Testen ALLER Features in Rhino 8 — 4h — *⚠️ Adi hat noch NIE in Rhino getestet!*
- [ ] 2.5 Unit Tests für Model Layer (AssemblyNode, Tree Ops) — 4h — Ziel: ≥80% Coverage — *teilweise vorhanden (4 Test-Dateien existieren)*
- [ ] 2.6 Plugin Icon (256×256 PNG) — 1h
- [x] 2.7 README mit Screenshots + Fixes — 2h — *Quelle: Doc Audit* ✅ 2026-02-15
  - [x] GitHub-Username in Release-Link (`McMuff86`) — *Quelle: Doc Audit Prio 1*
  - [x] Command-Name verifizieren → `AssemblyOutliner` — *Quelle: Doc Audit Prio 1*
  - [x] API-Funktionen-Anzahl korrigiert (12 → 14) — *Quelle: Doc Audit*
- [ ] 2.8 Yak Package Build Script finalisieren — 2h — *`build-yak.ps1` existiert bereits*
- [ ] 2.9 Test auf cleanem Rhino 8 Install — 2h — Abhängig von: 2.8
- [ ] 2.10 Publish v1.0 auf Yak — 1h — Abhängig von: 2.9

### Doc-Fixes (parallel, low effort)
- [x] 2.11 Sprint Plan Status aktualisieren (dieses Dokument) — 30min — *Quelle: Doc Audit* ✅ 2026-02-15
- [x] 2.12 CLAUDE.md: fehlende Files in Struktur ergänzen (NativeVisibilityInterop.cs, TestNativeVisibilityCommand.cs) — 15min — *Quelle: Doc Audit* ✅ 2026-02-15
- [x] 2.13 CONTRIBUTING.md Duplikat auflösen (root vs docs/) — 15min — *Quelle: Doc Audit* ✅ 2026-02-15
- [x] 2.14 ARCHITECTURE.md v1 → Verweis auf V2 — 15min — *Quelle: Doc Audit* ✅ 2026-02-15

**Sprint 2 Total verbleibend: ~12h**

---

## Sprint 3: C++ Conduit Validation & Per-Instance Visibility

**Dauer:** 2 Wochen | **Status:** 🔄 ~60% (Infrastruktur steht, Validation pending) | **Effort geplant:** ~20h verbleibend

### Bereits erledigt (während Sprint 1/2)
- [x] 3.1 C++ SDK Setup, VS Project, Build Config (`build-native.ps1`) — *PlatformToolset v143*
- [x] 3.5 `CComponentVisibilityData` ON_UserData mit Chunked Serialization
- [x] 3.6 P/Invoke Bridge — alle 14 extern C Funktionen, NativeApi.h/.cpp
- [x] 3.A DocEventHandler: Auto-Sync on open/save/close/delete
- [x] 3.B VisibilityConduit: SC_DRAWOBJECT Interception + Nested Block Recursion
- [x] 3.C ComponentState Enum (Visible/Hidden/Suppressed/Transparent)
- [x] 3.D Snapshot Pattern (`CVisibilitySnapshot` für lock-free Rendering)
- [x] 3.E SC_CALCBOUNDINGBOX — korrektes ZoomExtents
- [x] 3.F SC_POSTDRAWOBJECTS — Selection Highlights via DrawObject *(ohne Heap-Allokationen)*
- [x] 3.G SC_PREDRAWOBJECTS — Frame-Start Snapshot
- [x] 3.H HasHiddenDescendants — O(1) Prefix-Lookup
- [x] 3.I API Version 4

### Offen — Validation (⚠️ EXIT GATE)
- [ ] 3.2 **SC_DRAWOBJECT Validation** — Block-Instanz intercepten, shifted zeichnen, kein Ghost — 4h — *Benötigt Windows + Rhino 8*
- [ ] 3.3 **Per-Component Draw Test** — Definition-Objekte einzeln zeichnen, eines skippen — 4h — Abhängig von: 3.2
- [ ] 3.4 **Selection Highlight Test** — gelbes Wireframe auf managed Instance — 2h — Abhängig von: 3.3
- [ ] 3.7 **End-to-End Smoke Test** — C# UI → C++ Hide → Viewport Update — 4h — Abhängig von: 3.4

### C++ Performance Fixes
- [ ] 3.8 **Selection Highlight Heap-Fix verifizieren** — Bestätigen dass SC_POSTDRAWOBJECTS + DrawObject keine Allokationen macht — 1h — *Quelle: Think Tank Features §3.4*
- [ ] 3.9 **Node-ID Stabilität** — C++ Seite verwendet stabile Rhino GUIDs (nicht NewGuid) — 1h — *Quelle: Think Tank Features §3.1*

**EXIT GATE:** Wenn 3.2 fehlschlägt → Fallback-Plan aus ADR-002 ausführen.

**Sprint 3 Total verbleibend: ~16h**

---

## Sprint 4: v2.0 — Component Visibility UI Integration

**Dauer:** 2 Wochen | **Status:** ⏳ | **Effort:** ~25h

- [ ] 4.1 **Component Tree Nodes** — Block-Instanzen expandierbar zu Komponenten — 6h — Abhängig von: Sprint 3 — *Quelle: Think Tank Features §4*
- [ ] 4.2 **Eye-Click Routing** — Klick auf Komponenten-Eye → C++ SetComponentState via P/Invoke — 3h — Abhängig von: 4.1
- [ ] 4.3 **Display Cache Integration** — CRhinoCacheHandle pro managed Instance — 4h — *Quelle: Sprint Plan alt 4.1*
- [ ] 4.4 **Thread Safety** — shared_mutex auf Conduit State, Interlocked auf C# Flags — 2h — *Quelle: Think Tank Features §3.3*
- [ ] 4.5 **Definition Change Handler** — UUID Validation bei BlockEdit Exit, Stale UUIDs prunen — 2h
- [ ] 4.6 **Mixed State für Component-Ebene** — Parent ◐ wenn Komponenten teilweise hidden — 1h — Abhängig von: 4.1
- [ ] 4.7 **Performance Test** — 100+ managed Instances, >30fps bei Rotation — 3h — Abhängig von: 4.3
- [ ] 4.8 **Edge Case Tests** — Copy/Paste, Undo/Redo, Linked Blocks, Nested Blocks — 3h
- [ ] 4.9 **Yak Package v2.0** — Bundle beide .rhp Files — 1h — Abhängig von: 4.7

**Sprint 4 Deliverable:** v2.0 — Per-Instance Component Visibility produktionsreif.

---

## Sprint 5: Quick Wins & UX Polish

**Dauer:** 2 Wochen | **Status:** ⏳ | **Effort:** ~21h

*Quelle: Think Tank Features §2 + §4 P0/P1*

### Multi-Select (Critical Gap #1)
- [ ] 5.1 **Multi-Select aktivieren** — `AllowMultipleSelection = true`, Event Handler auf `SelectedItems` anpassen — 4h — *Quelle: SW Comparison §4 Critical*
- [ ] 5.2 **Batch-Operationen** — Hide/Show/Isolate auf Mehrfachauswahl — 2h — Abhängig von: 5.1

### UX Quick Wins
- [ ] 5.3 **Hover Highlight** (Tree → Viewport Preview) — 3h — *Quelle: Think Tank §1.2 #9*
- [ ] 5.4 **Breadcrumb Navigation** im Assembly Mode — 3h — *Quelle: SW Comparison §4 Important*
- [ ] 5.5 **"Show Hidden" Mode** — invertierte Ansicht, alle Hidden transparent zeigen — 3h — *Quelle: Think Tank §1.1 #5*
- [ ] 5.6 **Inline Rename** (F2 → InputBox Dialog) — 2h — *Quelle: Think Tank §2.5*
- [ ] 5.7 **Ctrl+A Select All** im Tree — 30min — *Quelle: Think Tank §2.2*
- [ ] 5.8 **Tab/Shift+Tab** Viewport-Hover Hide/Show (SolidWorks Pattern) — 2h — *Quelle: Think Tank §1.1 #3*
- [ ] 5.9 **Select All Same Definition** Context Menu — 1h — *Quelle: Think Tank §4 P1*

**Sprint 5 Deliverable:** v2.1 — Spürbar bessere UX.

---

## Sprint 6: SolidWorks Parity — Critical Gaps

**Dauer:** 4 Wochen | **Status:** ⏳ | **Effort:** ~40-50h

*Quelle: SW Comparison §4 Critical Gaps*

### BOM-Export (Critical Gap #2)
- [ ] 6.1 **BOM Data Model** — Instance Counts, Definition Names, UserText Properties sammeln — 4h
- [ ] 6.2 **CSV Export** — Stückliste als CSV mit konfigurierbaren Spalten — 4h — Abhängig von: 6.1
- [ ] 6.3 **Excel Export** (optional, via EPPlus oder ClosedXML) — 3h — Abhängig von: 6.1

### Display States (Critical Gap #3)
- [ ] 6.4 **DisplayStateManager** — Named Visibility Presets speichern/laden — 8h — *Quelle: Think Tank §1.1 #1, SW Comparison §2.2*
- [ ] 6.5 **Display State Dropdown** — UI zum Wechseln zwischen States — 3h — Abhängig von: 6.4
- [ ] 6.6 **Persistence** — Display States in .3dm (UserDictionary oder UserData) — 4h — Abhängig von: 6.4
- [ ] 6.7 **Ctrl+1-9 Quick Switch** — Keyboard Shortcuts für erste 9 States — 1h — Abhängig von: 6.5

### Suppress/Unsuppress
- [ ] 6.8 **Suppress in UI** — ComponentState.Suppressed über Context Menu — 3h — *C++ Infrastruktur existiert bereits (ComponentState enum)*
- [ ] 6.9 **Suppressed Styling** — eigenes Icon, aus BBox/BOM ausgeschlossen — 2h — Abhängig von: 6.8

### Erweiterte Context Menüs
- [ ] 6.10 **Go to Definition** — Spring zur Block-Definition — 1h — *Quelle: SW Comparison §4 Important*
- [ ] 6.11 **Replace Component** — Block-Instanz durch andere Definition ersetzen — 3h
- [ ] 6.12 **Properties Dialog** — erweiterte Eigenschaften editieren — 3h

**Sprint 6 Deliverable:** v3.0 — Professionelles Assembly-Management-Tool.

---

## Sprint 7+: Beyond SolidWorks

**Dauer:** 8-12 Wochen | **Status:** ⏳ | **Effort:** ~80-120h

*Quelle: Think Tank §4 P2 + SW Comparison §5 Phase 2*

### Grasshopper Integration (Rhino-Unique USP)
- [ ] 7.1 **GH Assembly Data Component** — Assembly-Daten als Data Tree — 2 Wochen
- [ ] 7.2 **GH Visibility Control** — Programmatische Visibility-Steuerung — 1 Woche

### Visual Enhancements
- [ ] 7.3 **Ghost Mode** — Hidden = semi-transparent statt unsichtbar — 15h — *Quelle: Think Tank §4 P2*
- [ ] 7.4 **Per-Instance Color Overrides** — Instanz einfärben (z.B. rot = nacharbeiten) — 10h — *C++ Infrastruktur nötig*
- [ ] 7.5 **Per-Component Transparency** — Gehäuse transparent, Innenleben sichtbar — 8h — *C++ Infrastruktur existiert teilweise*
- [ ] 7.6 **Assembly Visualization** — Farbcodierung nach Property (Material, Gewicht) — 15h — *Quelle: SW Comparison §3*

### Performance & Skalierung
- [ ] 7.7 **Lazy Loading** für Tree — Children erst bei Expand laden — 5h — *Quelle: Think Tank §3.1*
- [ ] 7.8 **Inkrementelle Tree Updates** — kein Full Rebuild bei Add/Delete — 8h — *Quelle: Think Tank §3.1*
- [ ] 7.9 **Virtualisierter Tree** (WebView) für 10K+ Instanzen — 2 Wochen — *Quelle: Think Tank §4 P2*

### Weitere Features
- [ ] 7.10 **Smart Groups** — virtuelle Gruppierungen via Queries ("alle M8 Schrauben") — 2 Wochen
- [ ] 7.11 **Layer-Assembly Sync** — Auto Layer-Struktur aus Assembly-Hierarchie — 1 Woche
- [ ] 7.12 **Assembly Templates** — Strukturen als Templates speichern/laden — 1 Woche
- [ ] 7.13 **Undo für Visibility** — Rhino Undo Integration + C++ Custom Undo Records — 15h

---

## Backlog (priorisiert)

*Features ohne festen Sprint, nach Priorität sortiert.*

| Prio | Feature | Aufwand | Quelle |
|------|---------|---------|--------|
| P1 | XML Docs für Commands + DetailPanel + IAssemblyNode | 3h | Doc Audit §5 |
| P1 | `volatile`/`Interlocked` auf `_needsRefresh` | 30min | Think Tank §3.3 |
| P1 | Static `_service` in TestCommand entfernen (Prod) | 15min | Think Tank §3.2 |
| P2 | Tree Display Options (Flat Tree, Feature Names) | 4h | SW Comparison §4 |
| P2 | Component Preview Window | 8h | SW Comparison §4 |
| P2 | Edit-in-Context Transparency | 4h | Think Tank §1.1 #8 |
| P2 | Detail Panel erweitern (Masse, Material, Custom Props) | 8h | SW Comparison §2.6 |
| P3 | Block Library Browser | 2 Wochen | Think Tank §4 P2 |
| P3 | Assembly Comparison (Diff) | 2 Wochen | Think Tank §4 P2 |
| P3 | Multi-Document / Worksession Support | 2 Wochen | Think Tank §4 P2 |
| P3 | Exploded Views | 6-8 Wochen | SW Comparison §4 |
| P3 | Interference Detection | 6-8 Wochen | SW Comparison §4 |
| P3 | food4Rhino Listing + Marketing | 4h | Think Tank §5 |
| P3 | README Badges (Build, Yak Version) | 1h | Doc Audit §7 |

---

## Quellen-Referenz

| Kürzel | Dokument |
|--------|----------|
| Doc Audit | `docs/reports/documentation-audit.md` |
| Think Tank | `docs/reports/thinktank-features.md` |
| SW Comparison | `docs/reports/solidworks-comparison.md` |
| Review | `docs/plans/REVIEW_SPRINT1.md` |
| ADR-002 | `docs/plans/ADR/ADR-002-*` |
| Refactoring | `docs/plans/REFACTORING_CHECKLIST.md` |

---

*Realistisch geplant für Abende/Wochenenden + Agent-Nächte. Priorität: USP (Per-Instance Visibility) vor Parity (BOM/Display States) vor Beyond (Grasshopper).*
