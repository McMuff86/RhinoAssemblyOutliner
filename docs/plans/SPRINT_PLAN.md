# Sprint Plan — BlockForge (RhinoAssemblyOutliner)

**Stand:** 15. Februar 2026  
**Architektur:** Definition Cloning + ON_UserData + Custom Grips (Hybrid C#/C++)  
**Tempo:** 10-15h/Woche + Nacht-Sessions mit Agents

---

## Sprint 1 ✅ DONE — Outliner UI + Basic Visibility

**Zeitraum:** Jan 2026  
**Deliverables:** Dockbares Panel, Tree View, Selection Sync, Visibility Toggle, Assembly Mode

---

## Sprint 2 ✅ DONE — Polish, Tests, Docs, Review Fixes

**Zeitraum:** Feb 2026 (erste Hälfte)  
**Deliverables:** 97 Tests, User Guide, CI, Plugin Icons, 5 Review Blockers fixed, Architecture v3 Research

---

## Sprint 3 — Definition Cloning MVP (C#)

**Zeitraum:** Feb–März 2026 (3 Wochen)  
**Ziel:** Echte Per-Instance Component Visibility via Definition Cloning in reinem C#.

**Abhängigkeiten:** Sprint 2 ✅

### Tasks

- [ ] `VisibilityState` Value Object (immutable, hashable) — 2h
- [ ] `VariantManager` implementieren (GetOrCreateVariant, ReassignInstance) — 8h
- [ ] `DefinitionCache` mit Deduplizierung (gleiche States = 1 Variant) — 4h
- [ ] Naming Convention `__aov_{hash}` für Variant-Definitionen — 1h
- [ ] UI Integration: Eye-Icon auf Component-Ebene → VariantManager — 6h
- [ ] `InstanceDefinitionModified` Event → RefreshAllVariants() — 4h
- [ ] GarbageCollect für Waisen-Varianten (delayed, 5s) — 3h
- [ ] Undo/Redo Support (BeginUndoRecord/EndUndoRecord) — 4h
- [ ] Tests: VariantManager, DefinitionCache, GC — 6h
- [ ] Manual Testing: 10+ Instanzen, verschiedene Configs — 3h

**Aufwand:** ~41h (3 Wochen à 14h)

**Deliverables:**
- Per-Instance Visibility funktioniert im Outliner
- Variant-Definitionen werden korrekt erstellt/gecached
- Undo/Redo funktioniert
- BlockEdit refreshed Varianten

**Exit Gate:** User kann einzelne Components pro Instanz ein-/ausblenden. 5 Instanzen derselben Definition mit unterschiedlichen Sichtbarkeiten gleichzeitig.

---

## Sprint 4 — C++ ON_UserData + Persistence

**Zeitraum:** März–April 2026 (3 Wochen)  
**Ziel:** Assembly-Daten persistieren via ON_UserData (C++). Save/Load roundtrip.

**Abhängigkeiten:** Sprint 3

### Tasks

- [ ] `ON_AssemblyUserData` Klasse (C++) — Read/Write/Archive — 8h
- [ ] P/Invoke Bridge: AttachAssemblyData, HasAssemblyData, GetSourceDefinitionId — 6h
- [ ] P/Invoke Bridge: Configuration CRUD (Add/Remove/SetActive/Get) — 8h
- [ ] Event Callback System (C++ → C#) — AssemblyChangedCallback — 6h
- [ ] `EventBridge` (C#) — marshals C++ callbacks to UI thread — 4h
- [ ] DocumentUserStrings Registry (RAO::def::*, RAO::version) — 3h
- [ ] Restore-Workflow bei File Open (scan + validate + reassign) — 6h
- [ ] Copy/Paste Support (intra-doc + cross-doc fallback) — 5h
- [ ] Roundtrip Tests: Save → Load → Verify UserData — 6h
- [ ] Graceful Degradation Test: File ohne Plugin öffnen — 2h

**Aufwand:** ~54h (3–4 Wochen à 14h)

**Deliverables:**
- Assembly-Daten überleben Save/Load
- Copy/Paste behält Assembly-State
- C++ ↔ C# Event Bridge funktioniert
- File ohne Plugin ladbar (graceful degradation)

**Exit Gate:** 3dm-File mit 10 Assembly-Instanzen: speichern, Rhino neustarten, laden → alle States korrekt wiederhergestellt.

---

## Sprint 5 — Custom Grips (C++) + Properties Panel

**Zeitraum:** April–Mai 2026 (3 Wochen)  
**Ziel:** Custom Grips für Component-Level Interaktion + Properties Panel.

**Abhängigkeiten:** Sprint 4

### Tasks

- [ ] `CAssemblyGrip` (C++ CRhinoGripObject-Subklasse) — 8h
- [ ] Grip Registration bei EnableGrips() — 4h
- [ ] Axis-Constraint Grips (X/Y/Z only) — 4h
- [ ] Grip Drag → Dimension Update → UserData Update — 6h
- [ ] Custom Grip Draw (visual feedback) — 4h
- [ ] Properties Panel (Eto): Configuration Dropdown — 6h
- [ ] Properties Panel: Key-Value Editor (Instance Properties) — 6h
- [ ] Properties Panel: Component List mit Visibility Toggle — 4h
- [ ] "Convert to Assembly" Command — 3h
- [ ] "Revert to Block" Command — 2h
- [ ] Tests: Grip interaction, Properties CRUD — 5h

**Aufwand:** ~52h (3–4 Wochen à 14h)

**Deliverables:**
- Custom Grips sichtbar und draggable
- Properties Panel mit Config-Dropdown
- Convert to/from Assembly Commands

**Exit Gate:** User kann Grip draggen → Dimension ändert sich → State persistiert nach Save/Load.

---

## Sprint 6 — Configuration System (Named States)

**Zeitraum:** Mai–Juni 2026 (2 Wochen)  
**Ziel:** Named Configurations pro Instance (wie SolidWorks Configurations).

**Abhängigkeiten:** Sprint 5

### Tasks

- [ ] `ConfigurationService` (C#): CRUD für Named Configs — 6h
- [ ] Config-Vererbung (Parent → Derived, Inheritance Chain) — 5h
- [ ] UI: Configuration Dropdown im Properties Panel — 3h
- [ ] UI: "New Configuration" / "Delete Configuration" Dialog — 4h
- [ ] Config-Wechsel → Variant-Assignment (resolve chain → VisibilityState) — 4h
- [ ] "Default" Config (implizit, alles sichtbar) — 2h
- [ ] Batch: "Apply Config to all Instances of Definition" — 3h
- [ ] Tests: Config CRUD, Inheritance, Batch ops — 5h

**Aufwand:** ~32h (2–3 Wochen à 14h)

**Deliverables:**
- Named Configurations erstellen/wechseln/löschen
- Config-Vererbung funktioniert
- Batch-Konfiguration möglich

**Exit Gate:** Instance mit 3 Configs (Default, Wartung, Minimal) — Wechsel via Dropdown, Undo/Redo korrekt.

---

## Sprint 7 — BOM Export + Food4Rhino Release

**Zeitraum:** Juni–Juli 2026 (3 Wochen)  
**Ziel:** MVP auf Food4Rhino veröffentlichen.

**Abhängigkeiten:** Sprint 6

### Tasks

- [ ] BOM Generator: aggregiert Components über alle Instanzen — 6h
- [ ] CSV Export — 3h
- [ ] Excel Export (EPPlus oder ClosedXML) — 5h
- [ ] "Export for Sharing" Command (Reset + Purge + Strip) — 4h
- [ ] Food4Rhino Listing erstellen (Screenshots, Description, Tags) — 4h
- [ ] Yak Package finalisieren (Icons, Manifest, native DLL bundling) — 4h
- [ ] End-to-End Testing (vollständiger Workflow) — 6h
- [ ] README für GitHub (Installation, Usage, Screenshots) — 3h
- [ ] Performance Testing: 100+ Instanzen, 10+ Configs — 4h
- [ ] Bug Fixes & Polish — 8h

**Aufwand:** ~47h (3–4 Wochen à 14h)

**Deliverables:**
- BlockForge Free auf Food4Rhino
- BOM Export (CSV + Excel)
- Dokumentation für End-User
- Yak Package

**Exit Gate:** Plugin installierbar via Yak, Outliner + Visibility + Configs + BOM funktionieren end-to-end.

---

## Sprint 8+ — Advanced Features (Future)

**Zeitraum:** Q3–Q4 2026  
**Keine festen Tasks, Priorisierung nach User-Feedback.**

### Kandidaten

- [ ] Grasshopper Integration (Assembly-Nodes als GH Components)
- [ ] Nested Block Visibility (rekursives Cloning, max 2 Ebenen)
- [ ] Dimension Overrides per Config
- [ ] Material Overrides per Config
- [ ] Assembly Constraints (Light) — Snap/Attachment Points
- [ ] Varianten-Manager / Design Table (Excel Import)
- [ ] Drawing Integration (BOM-Tabelle in Rhino Layout)
- [ ] Cutting Lists / Hardware Lists (Schreiner-Reports)
- [ ] Explosionsdarstellung
- [ ] Pro-Lizenz System (Licensing)
- [ ] Rhino 9 Kompatibilität

---

## Timeline-Übersicht

```
        Feb         März        April       Mai         Juni        Juli
  ├──────────┼──────────┼──────────┼──────────┼──────────┼──────────┤
  │ Sprint 3 │          │ Sprint 5 │          │ Sprint 7 │
  │ Def Clone│ Sprint 4 │ Grips +  │ Sprint 6 │ BOM +    │
  │ MVP (C#) │ UserData │ Props    │ Configs  │ Release  │
  │          │ (C++)    │ (C++)    │          │ 🚀 F4R   │
  └──────────┴──────────┴──────────┴──────────┴──────────┘
```

**Gesamtaufwand bis Release:** ~226h (~4.5 Monate à 14h/Woche)
