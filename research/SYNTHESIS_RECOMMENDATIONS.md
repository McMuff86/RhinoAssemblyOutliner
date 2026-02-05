# Synthesis & Recommendations for RhinoAssemblyOutliner

> **Ergebnis der Multi-Agent CAD Research**  
> Datum: 2026-02-05  
> Basierend auf: SOLIDWORKS_ANALYSIS.md, CAD_INDUSTRY_ANALYSIS.md

---

## Executive Summary

Nach Analyse von **5 führenden CAD-Systemen** (SolidWorks, Inventor, Fusion 360, CATIA, Siemens NX) und Rhinos aktuellen Limitationen haben wir klare Empfehlungen für die Implementation des Assembly Outliners.

**Das Differenzierungsmerkmal:** Per-Instance Component Visibility ist in Rhino nicht nativ möglich. Mit einem Hybrid C++/C# Plugin können wir dieses Feature liefern und damit einen echten Mehrwert schaffen.

---

## 1. MUST-HAVE Features (MVP)

Diese Features sind **non-negotiable** für einen erfolgreichen Launch:

| Feature | Quelle | Begründung |
|---------|--------|------------|
| **👁️ Eye Icon Toggle** | Alle CAD | Universal verstanden, 1-Click |
| **Per-Instance Visibility** | SolidWorks, Inventor | **USP** - das kann Rhino nicht |
| **Hierarchischer Tree** | Alle CAD | Zeigt verschachtelte Strukturen |
| **Bidirektionale Selektion** | Alle CAD | Tree ↔ Viewport Sync Pflicht |
| **Context Menu** | Alle CAD | Hide/Show/Isolate/Zoom |
| **Grayed Icons für Hidden** | Alle CAD | Klares visuelles Feedback |
| **Isolate Command** | SolidWorks | Fokussiertes Arbeiten |

### MVP User Flow
```
1. User öffnet komplexes Block-Modell
2. Assembly Outliner zeigt Hierarchie
3. User klickt Eye-Icon → Komponente wird hidden (NUR diese Instanz!)
4. User kann weiter selektieren im Viewport
5. Context-Menü → "Show All" stellt alles wieder her
6. Visibility wird mit File gespeichert
```

---

## 2. NICE-TO-HAVE Features (v2)

Diese Features erhöhen den Wert, sind aber nicht launch-kritisch:

| Feature | Quelle | Priorität |
|---------|--------|-----------|
| **Named Display States** | SW, NX | HOCH - Zustände speichern |
| **Keyboard Shortcuts** (Tab/Shift+Tab) | SolidWorks | HOCH - Power-User Effizienz |
| **Show with Dependents** | SolidWorks | MITTEL - Nested-Handling |
| **Search/Filter im Tree** | Alle CAD | MITTEL - Grosse Assemblies |
| **Ghost Mode** (semi-transparent) | CATIA | NIEDRIG - Nice-to-have |
| **Reference Sets** | Siemens NX | NIEDRIG - Komplex, später |

---

## 3. UX-Patterns: 1:1 übernehmen

Diese Patterns sind **industriestandard** und sollten unverändert übernommen werden:

### 3.1 Eye Icon Convention
```
👁️  = Sichtbar (ausgefülltes Auge, farbig)
〰️  = Hidden (durchgestrichenes/leeres Auge, grau)
◐   = Gemischt (Parent mit hidden + visible children)
```

### 3.2 Visual Feedback für Hidden Items
- **Icon:** Ausgegraut
- **Text:** Grau oder kursiv
- **Position im Tree:** Bleibt erhalten (nicht ausblenden!)

### 3.3 Isolate Pattern
```
1. User selektiert Komponente(n)
2. Click "Isolate"
3. Alle ANDEREN Komponenten werden hidden
4. User arbeitet fokussiert
5. "Isolate Off" oder ESC → Alles wieder sichtbar
```

### 3.4 Context Menu Struktur
```
Right-Click auf Komponente:
├── 👁️ Show
├── 〰️ Hide  
├── ─────────────
├── 🎯 Isolate
├── 🔄 Show All
├── ─────────────
├── 🔍 Zoom to
├── ✏️ Select in Viewport
├── ─────────────
├── 📋 Select All Same Definition
└── ⚙️ Edit Block
```

### 3.5 Keyboard Shortcuts (SolidWorks-inspiriert)
| Shortcut | Aktion |
|----------|--------|
| **H** | Hide selected |
| **Shift+H** | Show selected |
| **Ctrl+H** | Show All |
| **I** | Isolate selected |
| **Esc** | Exit Isolate / Deselect |

---

## 4. Rhino-Adaptionen

Diese Patterns müssen wir für Rhino **anpassen**:

### 4.1 Layer Integration

**Problem:** Rhino hat bereits Layer-basierte Visibility. Wir adden per-instance obendrauf.

**Lösung:** Zweistufiges System
```
Layer Visibility (Rhino-native)
     ↓
Per-Instance Visibility (unser Feature)
     ↓
Resultat: Beide müssen "visible" sein für Sichtbarkeit
```

**UX-Klarstellung:**
- Wenn Layer hidden → Komponente hidden (wir können nicht überschreiben)
- Wenn Layer visible → Unsere per-instance Visibility greift

### 4.2 Display States / View States

**Problem:** Rhino hat keine echten Display States wie SolidWorks.

**Lösung für v1:** Leverage Rhino's **Layer States**
- Layer States können Layer-Visibility speichern
- Wir dokumentieren: "Nutze Layer States für globale Ansichten"

**Lösung für v2:** Custom Named Visibility States
- In UserData auf Document-Level speichern
- Dropdown im Panel zur Auswahl
- "Save Current State" / "Apply State" Buttons

### 4.3 Edit in Context → BlockEdit

**Problem:** Rhino hat kein "Edit in Context" wie SolidWorks.

**Lösung:** Integration mit `BlockEdit` Command
- Doppelklick im Tree → startet BlockEdit
- Context-Menü: "Edit Block" → startet BlockEdit
- Nach BlockEdit-Exit: Tree refreshen

### 4.4 Keine Parametrik

**Problem:** Rhino-Blocks haben keine Configurations wie SolidWorks.

**Lösung:** Nicht versuchen zu emulieren
- Fokus auf Visibility, nicht auf Parametrik
- Das ist ein anderes Feature (z.B. Grasshopper Definitions)

---

## 5. Architektur-Entscheidungen

### 5.1 Warum Hybrid C++/C#?

| Ansatz | Vorteile | Nachteile |
|--------|----------|-----------|
| Nur C# | Schnellere Entwicklung, einfacher Debug | Ghost Artifacts, Selection-Probleme |
| Nur C++ | Volle Pipeline-Kontrolle | Langsame UI-Entwicklung, schwerer zu maintainen |
| **Hybrid** | Best of both worlds | Komplexerer Build, aber lösbar |

**Entscheidung:** Hybrid C++/C#
- C++ für: DisplayConduit, UserData, Performance-kritisches
- C# für: UI (Eto.Forms), Business Logic, Commands

### 5.2 Visibility Storage

**Entscheidung:** ON_UserData auf Instance-Objekten

```cpp
class CComponentVisibilityData : public ON_UserData {
    ON_UuidList m_hidden_component_ids;  // UUID-basiert = robust
};
```

**Vorteile:**
- Persisted automatisch mit .3dm File
- Rhino-native Architektur
- Keine externe Datei nötig

### 5.3 Tree-Struktur

**Entscheidung:** Grouped by Definition mit Instance-Count

```
📦 Cabinet_600 (×3)
│   ├── Instance #1  [👁️]
│   ├── Instance #2  [👁️]
│   └── Instance #3  [〰️]  ← Diese hidden
```

**Alternative:** Flat Instance List
- Jede Instanz einzeln zeigen
- Pro: Einfacher zu implementieren
- Contra: Unübersichtlich bei vielen Instanzen

**Entscheidung:** Grouped für v1, optional Flat View für v2

---

## 6. Implementation Roadmap (Updated)

### Phase 1: C++ Core (1-2 Wochen)
```
[~] Rhino 8 C++ SDK Setup
[ ] CRhinoDisplayConduit für Block-Rendering
[ ] ON_UserData für Visibility State
[ ] Extern C API für P/Invoke
[ ] Minimal Test: Hide hardcoded Component
```

### Phase 2: C# Integration (1 Woche)
```
[ ] P/Invoke Wrapper (NativeVisibilityAPI.cs)
[ ] VisibilityService Update für native Calls
[ ] Integration Tests
```

### Phase 3: UI Features (1-2 Wochen)
```
[ ] Eye Icon Column mit Toggle
[ ] Grayed Items für Hidden
[ ] Context Menu Update
[ ] Isolate Mode
```

### Phase 4: Polish (1 Woche)
```
[ ] Keyboard Shortcuts
[ ] Edge Cases (BlockEdit, Linked Blocks)
[ ] Performance Optimization
[ ] Dokumentation
```

### Phase 5: v2 Features (Future)
```
[ ] Named Visibility States
[ ] Show with Dependents
[ ] Search/Filter
[ ] Ghost Mode Option
```

---

## 7. Key Differentiators vs. Competition

| Feature | Native Rhino | VisualARQ | Other Plugins | **Ours** |
|---------|--------------|-----------|---------------|----------|
| Hierarchical Tree | ❌ | ⚠️ BIM Focus | ⚠️ Basic | ✅ Full |
| Per-Instance Visibility | ❌ | ❌ | ❌ | ✅ **Unique** |
| Bidirectional Selection | ❌ | ⚠️ | ⚠️ | ✅ |
| Display States | ❌ | ⚠️ | ❌ | 🔜 v2 |
| C++ Performance | N/A | ❌ C# | ❌ C# | ✅ Hybrid |

---

## 8. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| C++ SDK complexity | HIGH | Early PoC, iterative development |
| Selection issues | MEDIUM | PreDrawObject approach validated |
| Performance with 1000+ blocks | MEDIUM | Lazy registration, caching |
| Mac compatibility | LOW | C++ SDK supports macOS |
| Rhino version updates | LOW | Target Rhino 8 only |

---

## 9. Success Metrics

**MVP Success:**
- [ ] Per-instance visibility works without artifacts
- [ ] File save/load preserves visibility state
- [ ] Selection works on hidden-component instances
- [ ] UI is responsive with 100+ block instances

**v1.0 Success:**
- [ ] 50+ downloads in first month
- [ ] Positive feedback from beta testers
- [ ] No critical bugs reported

---

*Erstellt: 2026-02-05*
*Quellen: SOLIDWORKS_ANALYSIS.md, CAD_INDUSTRY_ANALYSIS.md*
