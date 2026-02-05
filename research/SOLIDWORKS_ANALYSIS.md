# SolidWorks Assembly Management - Deep Dive Analysis

> Comprehensive research on how SolidWorks handles assembly visibility, component management, and the FeatureManager Design Tree.

---

## 1. FeatureManager Design Tree

### 1.1 Overview

Der FeatureManager Design Tree ist das Herzstück der SolidWorks-Assemblyverwaltung. Er befindet sich auf der linken Seite des Fensters und zeigt eine hierarchische Übersicht aller Komponenten.

### 1.2 Tree Structure für Assemblies

Der FeatureManager zeigt für jede Assembly-Komponente:

```
[Icon] (f/-/nichts) Dateiname<Instanz-Nr> [Konfiguration] [Display State]
```

**Breakdown:**
1. **Component Icon**: Part-Icon oder Subassembly-Icon
2. **Fix/Float Indicator**:
   - `(f)` = Fixed (kann nicht bewegt werden)
   - `(-)` = Float (hat noch Freiheitsgrade, kann bewegt werden)
   - *nichts* = Fully constrained durch Mates
3. **Dateiname**: Der Filename des Parts/Subassembly
4. **Instance Count**: `<1>`, `<2>`, etc. - zählt die Instanzen (continued auch nach Löschen!)
5. **Configuration Name**: Welche Configuration aktiv ist
6. **Display State**: Welcher Display State aktiv ist

### 1.3 Expansion/Collapse Behavior

- Klick auf `+`/`-` neben Komponente expandiert/collapsed Details
- **Right-click → "Collapse Items"**: Collapsed alle Items im Tree
- Subassemblies können expandiert werden um deren Komponenten zu sehen

### 1.4 Display Pane (Rechte Seite des Trees)

Der Display Pane wird durch einen **Pfeil oben rechts** am FeatureManager geöffnet/geschlossen.

**Spalten im Display Pane:**

| Spalte | Icon | Funktion |
|--------|------|----------|
| 1. Hide/Show | Eye/Glasses | Sichtbarkeit toggeln |
| 2. Display Style | Box-Icon | Shaded, Wireframe, Hidden Lines, etc. |
| 3. Appearance | Farbkreis | Material/Farbe |
| 4. Transparency | Halb-transparentes Icon | Transparenz on/off |

**Interaktion:**
- **Left-Click** auf Icon: Toggled die Eigenschaft
- **Right-Click** in beliebiger Spalte einer Zeile: Zeigt Menü mit ALLEN Display-Optionen

---

## 2. Hide/Show Components

### 2.1 Hide Component vs. Suppress - Fundamentaler Unterschied

| Eigenschaft | HIDDEN | SUPPRESSED |
|-------------|--------|------------|
| **Sichtbar?** | Nein | Nein |
| **In Memory geladen?** | JA | NEIN |
| **Mates aktiv?** | JA | NEIN (auch suppressed) |
| **Mass Properties?** | JA (Option: Ignore Hidden) | NEIN |
| **Collision Detection?** | JA (Option) | NEIN |
| **BOM?** | JA | NEIN |
| **Pack & Go?** | JA | JA (aber nicht geöffnet) |
| **Performance Impact** | Nur Graphics entlastet | Voll entlastet |

**Kernkonzept:**
- **Hide** = "Invisible aber existent" - Teil ist nur nicht gerendert
- **Suppress** = "Temporär gelöscht" - als ob es nicht existiert

### 2.2 Unload Hidden Components

Ein Hybrid-Feature: 
- **Right-click auf Assembly-Name → "Unload Hidden Components"**
- Entfernt die Mathematik-Daten von Hidden Components aus dem Speicher
- Gibt Performance wie bei Suppress, behält aber BOM-Eintrag etc.
- Muss nach jedem weiteren Hide erneut ausgeführt werden (nicht persistent)

### 2.3 Methoden zum Hide/Show

#### A) Context Menu (Right-Click)
```
Right-Click auf Component → Show/Hide → Hide Component
Right-Click auf Component → Show/Hide → Show Component
```

#### B) Display Pane Eye-Column
- Click auf Eye-Icon toggled Visibility
- Wenn Icon = "durchgestrichenes Auge" oder grau → Hidden
- Wenn Icon = Auge sichtbar → Shown

#### C) Keyboard Shortcuts (SEHR WICHTIG für UX!)

| Shortcut | Aktion | Kontext |
|----------|--------|---------|
| **Tab** | Hide Component | Hover über Component |
| **Shift + Tab** | Show Component | Hover über Position wo hidden Component war |
| **Ctrl + Shift + Tab** | Zeigt ALLE hidden Components temporär transparent | Halten, dann Components anklicken zum Show |

#### D) Show Hidden Components Mode
- **Assembly Toolbar → "Show Hidden Components"**
- Oder Right-Click im Graphics Area (nicht auf Component) → Show Hidden Components
- **Invertiert die Ansicht:** Sichtbare werden hidden, Hidden werden sichtbar
- Klick auf gezeigte Components → werden permanent sichtbar
- **Exit Show-Hidden** beendet den Modus

#### E) Show with Dependents
- **Right-Click auf Assembly/Subassembly → "Show with Dependents"**
- Zeigt Komponente UND alle nested hidden Components (auch tief verschachtelt!)
- **Perfekt für Subassemblies** wo man nicht einzeln suchen will

### 2.4 Visual Feedback für Hidden Components

- **FeatureManager Tree:** Icon wird grau/ausgegraut
- **Display Pane Eye Column:** Durchgestrichenes Auge oder leeres Icon
- Im Graphics Area: Komplett unsichtbar (anders als Transparent)

---

## 3. Visibility Mechanics - Per-Instance!

### 3.1 Instanz vs. Definition

**KRITISCH:** Visibility ist **per-Instance**, nicht per-Definition!

```
Part A erscheint 3x in Assembly:
  Part_A<1> → Visible
  Part_A<2> → Hidden  
  Part_A<3> → Visible
```

Jede Instanz kann individuell hidden/shown werden!

### 3.2 Nested Assemblies - Komplexität

Bei verschachtelten Assemblies gibt es **zwei Visibility-Layers:**

1. **Top-Level Assembly Visibility** (override)
2. **Subassembly interne Visibility** (original)

**Behavior:**
- Hidden in Subassembly → Bleibt hidden (es sei denn top-level override)
- Top-Level kann Subassembly-Components **überschreiben** (siehe Display Pane mit "Assembly Level" Appearances)
- "Show with Dependents" respektiert alle Levels

### 3.3 Subassembly Component Picking Problem

Wenn man auf Subassembly klickt:
- SolidWorks selektiert oft das **Part innerhalb** statt die Subassembly
- Lösung: **Right-Click → Select Other** um zwischen verschachtelten Komponenten zu wählen

---

## 4. Display States - Gespeicherte Visibility-Sets

### 4.1 Konzept

Display States speichern:
- **Hide/Show** Status aller Components
- **Display Mode** (Shaded, Wireframe, etc.)
- **Appearance** (Farben, Materialien)
- **Transparency** (On/Off pro Component)

**Nicht gespeichert:** Suppress/Unsuppress, Mates, Dimensions

### 4.2 Display States vs. Configurations

| Display States | Configurations |
|----------------|----------------|
| NUR visuelle Darstellung | Geometrie, Suppression, Mates, Dimensions |
| Sehr performant | Kann langsam werden |
| Schnelles Umschalten | Rebuild nötig |
| Ideal für "Ansichten" | Ideal für "Varianten" |

### 4.3 Linking Display States to Configurations

**Option:** Right-Click auf Display State → Properties → "Link Display States to configurations"

- **Linked:** Jede Configuration hat ihren eigenen Satz Display States (1:1)
- **Unlinked (Default):** Display States sind unabhängig, wechseln NICHT mit Configuration

**Use Case Unlinked:**
- 2 Configurations (Single/Dual)
- 2 Display States (All Shown / Panels Hidden)
- = 4 Kombinationen mit nur 2+2 statt 4 Configurations!

### 4.4 Display State Management

**Location:** ConfigurationManager Tab

**Aktionen:**
- **Add Display State:** Right-Click → "Add Display State"
- **Activate:** Double-Click auf Display State Name
- **Rename:** Slow double-click (wie Datei umbenennen)
- **Properties:** Right-Click → Properties (für Link-Option)

### 4.5 Display States in Drawings

- Jede Drawing View kann einen anderen Display State referenzieren
- **View PropertyManager → Display State Section**
- Ermöglicht verschiedene Darstellungen ohne neue Configs

---

## 5. Isolate Command

### 5.1 Konzept

**Isolate = Temporärer Display State** der nur ausgewählte Komponenten zeigt.

Andere Komponenten werden:
- **Hidden** (unsichtbar)
- **Transparent** (durchsichtig)
- **Wireframe** (nur Kanten)

### 5.2 Workflow

1. **Select** Components (im Graphics Area oder FeatureManager)
2. **Right-Click → Isolate**
3. Popup erscheint mit Visibility-Optionen für "andere"
4. **Work on isolated components**
5. **Exit Isolate** (Button im Confirmation Corner)

### 5.3 Isolate + Edit in Context

**Perfekte Kombination:**
1. Select Part + relevante Nachbar-Parts
2. Isolate
3. Right-Click → Edit Component (in Context)
4. Arbeiten ohne visuelles "Noise"
5. Exit Edit
6. Exit Isolate

### 5.4 Option: Save as Display State

Im Isolate-Mode gibt es Option:
- **Save the visibility as a new display state**
- Macht den temporären Zustand permanent abrufbar

---

## 6. Edit Component in Context

### 6.1 Workflow

**Aktivierung:**
- Right-Click auf Part → **Edit Part** (oder Edit Component)
- Oder: Select Part → Assembly Tab → Edit Component Button

**Visual Feedback:**
- Editierte Part → Vollständig sichtbar, mit blauem Rahmen im FeatureManager
- **Alle anderen Parts → werden transparent** (by default)

### 6.2 Assembly Transparency Settings

**Location:** Tools → Options → System Options → Display → "Assembly transparency for in context edit"

**Optionen:**
1. **Force Transparency** (Default): Alle anderen Parts werden transparent
2. **Opaque Assembly**: Alles bleibt normal sichtbar
3. **Maintain Transparency**: Respektiert vorhandene Transparency-Settings

### 6.3 Während Edit: Andere Parts verstecken

- Expand Display Pane → Part auf Transparent setzen
- Oder: Hide top feature des editierten Parts (um es temporär unsichtbar zu machen während man andere sieht)

### 6.4 Exit Edit

- Klick auf **Edit Component Button** (toggles)
- Oder: Confirmation Corner → Checkmark
- Assembly kehrt zu vorherigem Display-State zurück

---

## 7. Visual Feedback Summary

### 7.1 Component Icons im FeatureManager

| Status | Icon-Darstellung |
|--------|------------------|
| Visible & Resolved | Normales farbiges Icon |
| Hidden | Ausgegraut/Dimmed Icon |
| Suppressed | Grau mit "X" oder spezielles Suppress-Icon |
| Lightweight | Blaues Feather-Icon |
| Being Edited | Blauer Rahmen um Icon |

### 7.2 Display Pane Eye Column States

| State | Icon |
|-------|------|
| Shown | Offenes Auge |
| Hidden | Geschlossenes/Durchgestrichenes Auge oder leer |
| Hidden (Assembly Override) | Spezielles "Override"-Icon |

### 7.3 Graphics Area Feedback

| State | Darstellung |
|-------|-------------|
| Normal | Solid shaded |
| Transparent | Durchsichtig, Edges sichtbar |
| Hidden | Komplett unsichtbar |
| Wireframe | Nur Kanten/Linien |
| Selected | Highlighted (meist cyan/grün) |
| Edit in Context (andere) | Transparent oder Wireframe |

---

## 8. Keyboard Shortcuts Reference

| Shortcut | Aktion |
|----------|--------|
| **Tab** | Hide hovered Component |
| **Shift+Tab** | Show Component at cursor position |
| **Ctrl+Shift+Tab** | Temporarily show all hidden as transparent |
| **Ctrl+H** | Hide/Unhide (customizable) |
| **F9** | Toggle FeatureManager visibility |

---

## 9. Context Menu Optionen

### 9.1 Right-Click auf Component

```
- Show/Hide
  ├── Hide Component
  ├── Show Component  
  ├── Show with Dependents
  └── Hide with Dependents
  
- Isolate
- Component Properties...
- Edit Part (Edit in Context)
- Open Part (in new window)
- Suppress
- Unsuppress
- Unsuppress with Dependents

- Float / Fix
- Make Virtual / Make External
```

### 9.2 Right-Click im Graphics Area (Empty Space)

```
- Show Hidden Components
- View Orientation
- Display Style options
- Zoom/Pan/Rotate
```

### 9.3 Right-Click auf Assembly (Top Level)

```
- Unload Hidden Components
- Collapse Items
- Show/Hide with Dependents
- Assembly Properties
```

---

## 10. Data Structure Insights

### 10.1 Wo wird Visibility gespeichert?

- **Per Display State** in der Assembly-Datei (.sldasm)
- Jede Component-Instance hat Visibility-Flag pro Display State
- Subassembly-Overrides werden in Parent Assembly gespeichert

### 10.2 Component Instance Naming

```
ComponentName<InstanceNumber>
z.B.: Bolt_M6x20<3>
```

- Instance Number ist persistent (auch nach Delete anderer Instanzen)
- Instance 1, 2, 5, 6 möglich wenn 3, 4 gelöscht wurden

### 10.3 Display State Storage

```
Assembly.sldasm
├── Configurations
│   ├── Default
│   └── Variant_A
└── Display States
    ├── Default (linked to Default config?)
    ├── Exploded View
    └── Hidden Enclosure
```

---

## 11. Key UX Takeaways für Implementation

### 11.1 Was macht SolidWorks richtig?

1. **Klare Trennung** Hide (visual) vs. Suppress (structural)
2. **Per-Instance Visibility** - flexibel
3. **Display States** - gespeicherte Visibility-Sets, sehr effizient
4. **Tab/Shift+Tab** - schnelles Toggling ohne Menü
5. **Ctrl+Shift+Tab** - transparente Preview aller Hidden
6. **Show with Dependents** - löst Nested-Problem elegant
7. **Isolate** - temporärer Fokus-Modus
8. **Display Pane** - alle Optionen auf einen Blick, clickable Icons

### 11.2 UX Patterns zu übernehmen

1. **Eye-Icon Column** im Tree für schnelles Visibility-Toggle
2. **Keyboard Shortcuts** für häufige Aktionen
3. **Temporary Show Mode** (Ctrl+Shift+Tab equivalent)
4. **"Show with Dependents"** für hierarchische Sichtbarkeit
5. **Isolate Command** für fokussiertes Arbeiten
6. **Display States** als speicherbare Presets
7. **Grayed Icons** für hidden Components im Tree
8. **Context-Sensitive Menus** mit relevanten Optionen

### 11.3 Potentielle Verbesserungen

1. **Besseres Nested-Handling** ohne explizites "Show with Dependents"
2. **Live-Preview** beim Hover über Hide-Button
3. **Undo-Stack** für Visibility-Änderungen
4. **Filter** im Tree für "nur hidden" / "nur visible"

---

## 12. Quellen

- SolidWorks Official Help Documentation (2021-2024)
- Hawk Ridge Systems Blog
- Computer Aided Technology (CATI) Blog
- Javelin Tech (TriMech) Blog
- SolidWorks Forums
- Reddit r/SolidWorks Community

---

*Recherche durchgeführt: Februar 2026*
*Für: RhinoAssemblyOutliner Projekt*
