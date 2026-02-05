# CAD Industry Analysis: Assembly Visibility Patterns

> **Ziel:** Best Practices und Patterns aus etablierten CAD-Programmen identifizieren, die wir für Rhino übernehmen können.

---

## 1. Autodesk Inventor

### Browser/Model Tree
- **Specification Tree** mit hierarchischer Darstellung aller Komponenten
- Jeder Knoten zeigt Komponenten-Status über Icons (sichtbar/hidden/suppressed)
- **Drag & Drop** zum Umorganisieren von Komponenten
- Rechtsklick-Kontextmenüs für alle Operationen

### Visibility Controls - Das Drei-Schichten-Modell

Inventor unterscheidet KLAR zwischen:

1. **Visibility (View Representations)**
   - Rein visuell - beeinflusst BOM NICHT
   - Toggle ON/OFF pro Komponente
   - Transparenz-Einstellungen
   - Appearance-Overrides
   - **Schnell, keine Performance-Auswirkung**

2. **Enabled/Disabled State**
   - Komponente kann deaktiviert werden
   - Wird für Berechnungen ignoriert
   - Bleibt aber in Memory geladen

3. **Suppress (Level of Detail)**
   - Entlädt Komponente aus Memory
   - **Echte Performance-Verbesserung**
   - Reduziert Speicherverbrauch signifikant
   - Beeinflusst BOM

### Design View Representations - KILLER FEATURE

**Was sie speichern:**
- Visibility-Zustand jeder Komponente
- Transparenz-Einstellungen
- Appearance-Overrides
- Zoom-Stufe und Kamera-Winkel
- Sketch/Work Feature Visibility
- Enabled/Disabled States

**Key Patterns:**
- **"Nothing Visible"** - Spezielle View Rep die ALLES ausschaltet
- Komponenten in "Nothing Visible" verbrauchen weniger Grafikspeicher
- **Locking** - View Reps können gesperrt werden gegen Änderungen
- **Associativity** - Verknüpfung zu Drawing Views

### Level of Detail (LOD)
- **Memory Management** durch Suppress
- Vereinfachte Darstellungen für große Assemblies
- "All Suppressed" für maximale Performance
- Substitution: Viele Parts durch ein vereinfachtes Part ersetzen

### ✅ Patterns für Rhino

| Pattern | Beschreibung | Priorität |
|---------|--------------|-----------|
| **Drei-Schichten-Modell** | Visibility vs Enabled vs Suppress | HOCH |
| **Named Representations** | Gespeicherte Visibility-Zustände | HOCH |
| **"Nothing Visible" öffnen** | Performance beim Laden | MITTEL |
| **View Rep Locking** | Änderungsschutz | NIEDRIG |

---

## 2. Autodesk Fusion 360

### Browser Tree Konzept
- **Root Component** = Top-Level (wie ein "Assembly-Folder")
- Hierarchische Komponenten-Struktur
- Alles in EINEM Dokument (vs. external files)
- Reihenfolge im Browser = Erstellungsreihenfolge (parametrisch!)

### Component Visibility
- **Eye Icon** neben jeder Komponente
- Click toggles Visibility
- Kaskadiert zu Child-Komponenten
- Bodies innerhalb von Components haben EIGENE Visibility

### Hierarchie & Joints
- **Components können Components enthalten** (Nesting)
- Joints definieren Beziehungen, nicht Position in Tree
- "Ground" Komponente = fest, andere beweglich

### Grouping Pattern
- **Keine echten Folders** - stattdessen: Empty Component erstellen
- Components hineinziehen als "Subassembly"
- User-Workaround, nicht native Feature

### Cloud-Architektur
- Component View in Fusion Team
- Tabellarische Ansicht der Hierarchie
- Thumbnails für jede Komponente
- Zugriff auf Metadaten OHNE ganzes File zu öffnen

### ✅ Patterns für Rhino

| Pattern | Beschreibung | Priorität |
|---------|--------------|-----------|
| **Eye Icon Toggle** | Universell verstanden | HOCH |
| **Components als Folders** | Gruppierung durch Struktur | HOCH |
| **Bodies + Components** | Zwei Ebenen der Hierarchie | MITTEL |
| **Tabellarische Ansicht** | Alternative zum Tree | NIEDRIG |

---

## 3. CATIA V5/V6

### Product Structure (Specification Tree)
- **F3 = Hide/Show Tree** (globale Sichtbarkeit)
- Hierarchische Produktstruktur
- Jede Komponente = eigenes File (CATProduct, CATPart)
- Cross-highlighting zwischen Tree und 3D View

### Hide/Show Mechanismen

**Zwei Modi unterscheiden:**

1. **Visualization Mode (Cache/Lightweight)**
   - Parts nicht vollständig geladen
   - Hidden Status wird im Tree ANGEZEIGT (Icon)
   - Weniger Memory-Verbrauch

2. **Design Mode (Full)**
   - Parts vollständig geladen
   - Hidden Status wird NICHT automatisch im Tree angezeigt!
   - **Best Practice: IMMER aus Tree heraus hide/show!**

### Swap Visible Space
- Versteckte Objekte werden in "Swap Space" verschoben
- Right-click → Show bringt sie zurück
- Konzeptuell: Zwei "Räume" (sichtbar/unsichtbar)

### Graph vs. 3D View
- "Center Graph" - Tree zu selektiertem Objekt navigieren
- "Fit All In" - Alle Objekte im View zeigen
- Bidirektionale Synchronisation

### ✅ Patterns für Rhino

| Pattern | Beschreibung | Priorität |
|---------|--------------|-----------|
| **Lightweight Mode** | Für große Assemblies | HOCH |
| **Tree-basiertes Hide/Show** | Konsistentere UX | HOCH |
| **Cross-highlighting** | Tree ↔ 3D Sync | HOCH |
| **Swap Space Konzept** | Hidden Items Management | MITTEL |

---

## 4. Siemens NX

### Assembly Navigator
- Hierarchische Baumansicht
- Komponenten mit Status-Icons
- Right-click Menüs für Operationen
- **Filter-Optionen** im Navigator

### Reference Sets - DAS KILLER-FEATURE

**Arten von Reference Sets:**

1. **System-definiert (automatisch):**
   - **Model** - Nur Solid-Geometrie (keine Datums, Sketches)
   - **Empty** - Nichts anzeigen
   - **Entire Part** - Alles anzeigen

2. **User-definiert:**
   - Beliebige Objekt-Kombinationen
   - Benannt und wiederverwendbar
   - Pro Part/Subassembly definiert

**Was Reference Sets können:**
- Visibility kontrollieren
- Memory-Verbrauch reduzieren
- Detail-Level steuern
- Mass-Berechnungen beeinflussen (wenn Geometrie excluded)

**"Excluded Reference Set" Problem:**
- Part unsichtbar aber Visibility-Checkbox ist ON
- Lösung: Reference Set wechseln (Replace Reference Set)
- Häufige Fehlerquelle für Anfänger!

### Visibility Control Stack
1. Reference Set (was KANN sichtbar sein)
2. Layer Visibility (zusätzlicher Filter)
3. Show/Hide Status (manueller Toggle)

**Alle drei müssen "sichtbar" sein damit Objekt erscheint!**

### Part Navigator vs Assembly Navigator
- Part Navigator: Features, Bodies, Sketches im aktuellen Part
- Assembly Navigator: Komponenten-Hierarchie
- Unterschiedliche Kontexte, unterschiedliche Operationen

### ✅ Patterns für Rhino

| Pattern | Beschreibung | Priorität |
|---------|--------------|-----------|
| **Reference Set Konzept** | Vordefinierte Sichtbarkeits-Sets | HOCH |
| **System vs User Sets** | Model/Empty/Full + Custom | HOCH |
| **Multi-Layer Visibility** | Reference + Layer + Manual | MITTEL |
| **Navigator Types** | Part vs Assembly Kontext | NIEDRIG |

---

## 5. SolidWorks (Bonus)

### FeatureManager Design Tree
- Hierarchische Struktur
- **Display Pane** - Spalte für schnelle Visibility-Toggles
- Eye-Icon Spalte für Hide/Show
- Configuration- und Display-State Namen sichtbar

### Display States
- Speichern: Visibility, Transparency, Appearance
- **Unabhängig von Configurations**
- Mehrere Display States pro Configuration möglich
- Umschalten per Doppelklick im ConfigurationManager

### Configurations vs Display States
- **Configurations:** Geometrie-Änderungen (Maße, Features)
- **Display States:** Nur visuelle Eigenschaften
- Können verknüpft werden (Link Display States to Configurations)

### ✅ Patterns für Rhino

| Pattern | Beschreibung | Priorität |
|---------|--------------|-----------|
| **Display Pane** | Schnellzugriff auf Visibility | HOCH |
| **States vs Configs** | Trennung visual/structural | MITTEL |
| **State-Config Linking** | Optionale Verknüpfung | NIEDRIG |

---

## 6. Rhino Native - Aktueller Stand

### Was Rhino mit Blocks KANN

**BlockManager:**
- Liste aller Block-Definitionen
- Properties: Name, Count, Nested Blocks
- Update/Replace linked blocks
- Kein Tree-View für Instanzen!

**BlockEdit:**
- In-place Editing eines Blocks
- Andere Objekte werden ausgegraut
- Änderungen propagieren zu allen Instanzen

**Visibility:**
- Blocks sind auf Layers
- Layer-Visibility = Block-Visibility
- **KEINE per-instance Visibility!**

### Was Rhino NICHT kann (Limitations)

1. **Kein hierarchischer Assembly-Tree**
   - Nested Blocks existieren, aber kein Viewer dafür
   - Man sieht nur "flache" Liste im BlockManager

2. **Keine per-instance Visibility**
   - Block auf Layer = alle Instanzen gleich
   - Workaround: Separate Layers pro "Zustand"

3. **Keine gespeicherten View States**
   - Named Views speichern Kamera, nicht Visibility
   - Manuell Layers ein/ausschalten

4. **Nested Block Problems**
   - Visibility-Bugs bei tiefer Verschachtelung
   - Inkonsistentes Verhalten dokumentiert

### User Workarounds (was Leute machen)

1. **Layer-Strategie:**
   - Separate Layer pro Block-"Konfiguration"
   - Layer-States für verschiedene Ansichten
   - Umständlich bei vielen Blocks

2. **Grasshopper + Elefront:**
   - Block-Management programmatisch
   - Visibility über Grasshopper steuern
   - Baking zu hidden Layers

3. **VisualARQ:**
   - Dynamic Blocks via Grasshopper Definitions
   - Aber: Plugin, nicht native

4. **Multiple Files:**
   - Separate .3dm Files als "Assemblies"
   - Worksession für Referenzen
   - File-Management-Overhead

### ✅ Was wir BAUEN müssen

| Feature | Native Rhino | Wir liefern |
|---------|--------------|-------------|
| Hierarchischer Tree | ❌ | ✅ Assembly Outliner |
| Per-Instance Visibility | ❌ | ✅ Toggle pro Instanz |
| Nested Block Navigation | ❌ | ✅ Expand/Collapse |
| View States | ❌ | ⚠️ Via Layer States |
| Cross-highlighting | ❌ | ✅ Bidirektionale Selektion |

---

## 7. Gemeinsame Patterns - Das große Bild

### Universal UI Patterns

```
┌─────────────────────────────────────┐
│ 👁️ Tree-based Visibility Toggle     │  ALLE Programme
├─────────────────────────────────────┤
│ 📂 Hierarchical Component Structure │  ALLE Programme
├─────────────────────────────────────┤
│ 🔄 Bidirectional Selection Sync     │  ALLE Programme
├─────────────────────────────────────┤
│ 📋 Named View/Display States        │  Inventor, SW, NX
├─────────────────────────────────────┤
│ ⚡ Lightweight/Full Load Modes      │  CATIA, Inventor
├─────────────────────────────────────┤
│ 🎚️ Multi-Level Visibility Control   │  NX (Reference Sets)
└─────────────────────────────────────┘
```

### Visibility-Konzepte Vergleich

| Programm | Visual Only | Suppress/Unload | Named States |
|----------|-------------|-----------------|--------------|
| Inventor | View Rep | LOD | ✅ Beide |
| Fusion | Eye Toggle | ❌ | ❌ |
| CATIA | Hide/Show | Cache Mode | ❌ Native |
| NX | Reference Set | Empty Set | ✅ Custom Sets |
| SolidWorks | Display State | Suppress | ✅ Display States |
| **Rhino** | Layer | ❌ | Layer States |

### Best Practices (alle Programme)

1. **Immer Tree-basiert arbeiten**
   - Konsistentere UX
   - Status immer sichtbar
   - Kontext-Menüs verfügbar

2. **Visibility ≠ Suppress**
   - Hide = schnell, visual only
   - Suppress = langsam, spart Memory
   - User muss Unterschied verstehen

3. **Named States sind Gold wert**
   - Arbeitsstände speichern
   - Schnell wechseln
   - Team-Kommunikation

4. **Cross-Highlighting ist Pflicht**
   - Tree → 3D Selektion
   - 3D → Tree Navigation
   - Zoom to Selected

---

## 8. Pitfalls & Anti-Patterns

### ❌ Was NICHT funktioniert

1. **Zu tiefe Hierarchie**
   - >5 Levels werden unübersichtlich
   - Performance-Probleme
   - User verlieren Überblick

2. **Visibility-Chaos ohne States**
   - Manuelles Ein/Ausschalten geht verloren
   - Kein Undo für Visibility-Änderungen
   - Frustration bei großen Assemblies

3. **Inkonsistente UI**
   - CATIA: Hide von Surface vs Tree = unterschiedlich!
   - Verwirrend für User
   - **Wir: EINE Methode, konsistent**

4. **Fehlende Feedback-Mechanismen**
   - NX "Excluded Reference Set" - Part unsichtbar aber warum?
   - User braucht klare Indikatoren
   - Tooltips, Status-Icons

5. **Memory-Leaks bei großen Assemblies**
   - Visibility ≠ Unload
   - User denken "hidden = nicht geladen"
   - Klare Kommunikation nötig

### ✅ Was FUNKTIONIERT

1. **Eye Icon Convention**
   - 👁️ = Universell verstanden
   - Ein Klick = Toggle
   - Schnell und intuitiv

2. **Collapsible Tree**
   - Expand/Collapse für Übersicht
   - Expand All / Collapse All
   - Remember Expansion State

3. **Right-Click Context Menus**
   - Alle relevanten Operationen
   - Kontext-abhängig
   - Keyboard Shortcuts

4. **Multi-Select Operations**
   - Mehrere Items selektieren
   - Bulk Hide/Show
   - Batch-Operationen

---

## 9. Empfehlungen für Rhino Assembly Outliner

### Phase 1: Core Features (MVP)

```
MUST HAVE:
├── Hierarchischer Tree für Blocks
├── 👁️ Eye Toggle für Visibility (per instance)
├── Expand/Collapse für Nested Blocks
├── Bidirektionale Selektion (Tree ↔ 3D)
└── Right-click Context Menu (Hide/Show/Select)
```

### Phase 2: Enhanced Features

```
SHOULD HAVE:
├── Named View States (via Layer States?)
├── "Show Only Selected" / "Hide Selected"
├── Multi-Select Bulk Operations
├── Search/Filter im Tree
└── Zoom to Selected
```

### Phase 3: Power Features

```
NICE TO HAVE:
├── Reference Set-artiges Konzept
├── Lightweight Load Mode
├── Custom Visibility Presets
├── Export/Import von States
└── API für Grasshopper Integration
```

### UI Mockup Konzept

```
┌──────────────────────────────────────┐
│ Assembly Outliner            [≡] [×] │
├──────────────────────────────────────┤
│ [🔍 Search...              ] [⚙️]   │
├──────────────────────────────────────┤
│ 👁️ ▼ 🏠 Building                    │
│ 👁️   ├─ ▼ 🧱 Structure              │
│ 👁️   │    ├─ 🟦 Column-A (×12)      │
│ 👁️   │    ├─ 🟦 Column-B (×8)       │
│ 👁️   │    └─ 🟦 Beam (×24)          │
│ 👁️   ├─ ▶ 🪟 Facade [+]             │
│ 👁️   └─ ▶ 🚪 Doors [+]              │
├──────────────────────────────────────┤
│ Total: 156 instances, 12 definitions │
└──────────────────────────────────────┘
```

### Key Decisions

| Entscheidung | Empfehlung | Begründung |
|--------------|------------|------------|
| Visibility | Per-Instance | Inventor/SW Pattern |
| Tree-Struktur | By Definition | Übersichtlicher als by instance |
| States | Via Layer States | Rhino-native, kein Custom-System |
| Selection | Bidirectional | Standard in allen CAD |

---

## 10. Fazit

### Die goldenen Regeln

1. **Visibility muss EINFACH sein** - Ein Klick
2. **Hierarchie muss SICHTBAR sein** - Tree View
3. **Selektion muss SYNCHRON sein** - Bidirektional
4. **Zustände müssen SPEICHERBAR sein** - Named States
5. **Feedback muss KLAR sein** - Status-Icons

### Differenzierung zu anderen Plugins

Unser Vorteil: Wir bauen **genau das, was Rhino fehlt**, nicht ein komplettes neues System. Integration in bestehende Rhino-Workflows (Layers, Named Views, etc.) statt Paralleluniversum.

### Nächste Schritte

1. [ ] UI Mockups basierend auf diesen Patterns
2. [ ] Technical Spec für Phase 1
3. [ ] Prototyp mit Core Features
4. [ ] User Testing mit Architekten/Designern

---

*Erstellt: 2026-02-05*
*Quellen: Autodesk Help, Siemens Documentation, User Forums, Industry Best Practices*
