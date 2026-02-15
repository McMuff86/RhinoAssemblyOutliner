# SolidWorks Configurations Research

> **Ziel:** Das SolidWorks Konfigurations-System vollständig verstehen, um relevante Konzepte in Rhino nachzubauen.
> **Datum:** 2026-02-15

---

## 1. SolidWorks Datenmodell

### Dateistruktur

SolidWorks ist **file-basiert** mit drei Hauptdateitypen:

| Typ | Extension | Inhalt |
|-----|-----------|--------|
| Part | `.sldprt` | Ein einzelnes Bauteil mit Features, Sketches, Configurations |
| Assembly | `.sldasm` | Referenziert Parts/Sub-Assemblies via Dateipfad + Configuration |
| Drawing | `.slddrw` | 2D-Ableitung, referenziert Parts/Assemblies + deren Configurations |

### Hierarchie

```
Assembly (.sldasm)
├── Component Instance 1 → Part A (.sldprt) [Config: "M6"]
├── Component Instance 2 → Part A (.sldprt) [Config: "M8"]
├── Component Instance 3 → Part B (.sldprt) [Config: "Default"]
├── Sub-Assembly (.sldasm) [Config: "Variante_Links"]
│   ├── Component Instance → Part C ...
│   └── ...
├── Mates (Constraints zwischen Components)
└── Assembly Features (Cuts, Holes die im Assembly-Kontext existieren)
```

### Referenzierung

- **Assembly → Part:** Über **Dateipfad** (absolut oder relativ). SW sucht in definierten Suchpfaden.
- **Assembly → Configuration:** Jede Component-Instance speichert welche Configuration des referenzierten Parts/Assembly sie nutzt. **Verschiedene Instances desselben Parts können unterschiedliche Configurations referenzieren.**
- **Ein Part = Eine Datei, viele Configurations.** Keine separaten Dateien pro Variante.

---

## 2. Configurations im Detail

### Was speichert eine Configuration?

Eine Configuration ist ein **Named Set von Parameter-Overrides** innerhalb eines Part oder Assembly. Sie speichert **Delta-Werte** gegenüber dem Basis-Zustand:

#### Part-Level Configurations

| Parameter | Beispiel |
|-----------|----------|
| **Dimensionen** | Bohrung Ø10 vs Ø12 |
| **Feature Suppress State** | Fase an/aus, Bohrungsmuster an/aus |
| **Material** | Stahl vs Aluminium |
| **Appearance/Color** | Rot vs Blau |
| **Custom Properties** | Teilenummer, Beschreibung, Gewicht |
| **Sketch Relations** | Driving/Driven State |
| **Scale Factors** | X/Y/Z Skalierung |
| **Mass Property Overrides** | Für Kaufteile |
| **End Conditions** | Blind vs Through All |
| **Sheet Metal Bend State** | Gebogen vs Abgewickelt |

#### Assembly-Level Configurations

Zusätzlich zu Part-Level:

| Parameter | Beispiel |
|-----------|----------|
| **Component Configuration** | Welche Config jeder Component nutzt |
| **Component Suppress State** | Komponente sichtbar/unterdrückt |
| **Component Fixed/Float** | Position fixiert oder nicht |
| **Mate Suppress State** | Constraints an/aus |
| **Assembly Feature Suppress** | Assembly-Cuts an/aus |

### Derived Configurations (Parent/Child)

- Eine Configuration kann eine **Child-Configuration** einer anderen sein
- Child erbt alle Parameter der Parent-Configuration
- Child überschreibt nur explizit geänderte Parameter
- **Typischer Use Case:** Grösse als Parent, Finish als Child

```
Washer
├── Config: M6 (Parent)
│   ├── Config: M6_Zink (Derived)
│   └── Config: M6_Blank (Derived)
├── Config: M8 (Parent)
│   ├── Config: M8_Zink (Derived)
│   └── Config: M8_Blank (Derived)
```

### Design Tables (Excel-driven)

- Excel-Tabelle **eingebettet** im Part/Assembly
- Spaltenheader = Parameter-Referenz (spezielle Syntax: `$Dimension@Feature`)
- Zeilen = Configurations
- Ermöglicht **Massenerfassung** von hunderten Configs
- Bidirektional: Änderungen in SW oder in Excel möglich
- Syntax-lastig und fehleranfällig bei grossen Tabellen

### Configuration Table (neuere Alternative)

- Eingebaut in SW UI (kein Excel nötig)
- Tabellenformat, Parameter-zentrisch
- Einfacher als Design Tables, aber weniger mächtig

### Performance-Grenzen

- **Praxis:** 10-50 Configurations pro Part sind üblich und performant
- **Grenzwerte:** Hunderte Configs möglich, aber Dateiöffnung wird langsam
- **Jede Configuration speichert eigene Tessellation** (Display-Mesh) → Dateigrösse wächst linear
- **Rebuild-Zeit** steigt mit Anzahl Configs (alle müssen rebuildet werden bei Feature-Änderungen)
- **Empfehlung von Dassault:** Für >50 Varianten eher DriveWorks/Automation statt Configurations

---

## 3. Display States vs Configurations

### Fundamentaler Unterschied

| Aspekt | Configuration | Display State |
|--------|--------------|---------------|
| **Scope** | Geometrie + Darstellung | Nur Darstellung |
| **Dimensionen** | ✅ | ❌ |
| **Feature Suppress** | ✅ | ❌ |
| **Material** | ✅ | ❌ |
| **Component Visibility** | ✅ (via Suppress) | ✅ (via Hide/Show) |
| **Appearance/Color** | ✅ | ✅ |
| **Transparency** | ❌ | ✅ |
| **Performance** | Schwer (rebuild nötig) | Leicht (kein rebuild) |
| **Rebuild** | Ja | Nein |

### Interaktion

- Display States existieren **pro Configuration** oder **global**
- Setting: **"Link Display States to Configurations"**
  - **Linked:** Jede Configuration hat eigene Display States
  - **Unlinked:** Display States sind unabhängig, verfügbar in allen Configurations
- Typisch: Configurations für geometrische Varianten, Display States für Ansichten (z.B. "Explosionsansicht farbig", "Röntgen-Ansicht")

### Key Insight für Rhino

> Display States sind das **leichtgewichtige** Konzept. Sie ändern nichts an der Geometrie, nur an der Darstellung. Das ist konzeptionell näher an dem, was in Rhino ohne Feature-History machbar ist.

---

## 4. Suppress / Resolve / Lightweight

### Suppress (Unterdrückt)

- Komponente ist **komplett deaktiviert**
- Kein Geometry geladen, kein Memory-Verbrauch
- Feature Tree zeigt ~~durchgestrichenen~~ Namen
- Mates zu suppressed Components werden ebenfalls suppressed
- **Configuration-spezifisch:** Component kann in Config A suppressed und in Config B resolved sein
- **Technisch:** SW ignoriert die Komponente beim Rebuild vollständig

### Resolved (Vollständig geladen)

- Alle Feature-Daten im RAM
- Feature Tree vollständig navigierbar
- Editieren möglich
- Höchster Memory-Verbrauch

### Lightweight (Leichtgewicht)

- Nur **Display-Mesh (Tessellation) + Referenzgeometrie** geladen
- Feature Tree sichtbar aber Features nicht editierbar
- Mates funktionieren (Referenzgeometrie vorhanden)
- **On-Demand Resolving:** Doppelklick auf Component lädt voll
- ~70-80% weniger RAM als Resolved
- **Seit SW 2023:** "Optimized Resolved Mode" — SW entscheidet automatisch was lightweight/resolved sein soll, ohne sichtbare Lightweight-Markierung

### Large Design Review

- Nur Display-Mesh, **keine** Referenzgeometrie
- Keine Mates, kein Editieren
- Für reine Visualisierung grosser Assemblies (10'000+ Teile)

### Technische Implementierung

```
Suppressed:    [nichts geladen]
Lightweight:   [Tessellation] + [Referenz-Geometrie (Flächen, Kanten für Mates)]
Resolved:      [Tessellation] + [Referenz-Geometrie] + [Feature-Daten] + [Sketch-Daten]
```

---

## 5. BOM Integration

### BOM-Typen

| Typ | Beschreibung |
|-----|-------------|
| **Top Level Only** | Nur direkte Kinder des Top-Assembly |
| **Parts Only** | Alle Parts flach aufgelistet (keine Sub-Assemblies) |
| **Indented** | Hierarchisch mit Sub-Assemblies als Gruppen |

### Configurations in BOMs

- **Configuration Property "BOM Quantity":** Kann auf "Show" oder "Don't Show" gesetzt werden
- **Konfigurierbar pro Config:** "Show configuration as separate item in BOM" vs. "Show as same item"
- **Part Number pro Configuration:** Jede Config kann eigene Teilenummer haben
- **"Envelope" Components:** Werden nie in BOM gezeigt (Referenz-Geometrie)
- **Suppressed Components:** Werden **nicht** in BOM aufgelistet

### Custom Properties & BOM

- **Configuration-spezifische Custom Properties:** Füllen BOM-Spalten
- Properties: Teilenummer, Beschreibung, Material, Gewicht, Vendor, etc.
- BOM-Template bestimmt welche Properties als Spalten erscheinen

---

## 6. Mapping auf Rhino's Block-System

### Fundamentale Unterschiede

| SolidWorks | Rhino |
|-----------|-------|
| Feature-basiert (parametrisch) | Direct Modeling (explizite Geometrie) |
| File = Part/Assembly | File = ganzes Modell, Blocks für Instanzen |
| Configuration = Parameter-Overrides | Kein natives Äquivalent |
| Assembly = externe Referenzen | Block = eingebettete Definition |
| Mates (Constraints) | Keine parametrischen Constraints |

### Was übernehmen?

#### ✅ Direkt übertragbar

1. **Display States → Layer States / Visibility States**
   - Hide/Show pro Objekt/Layer
   - Color/Material Overrides
   - Transparency
   - **Leichtgewichtig, kein Geometry-Rebuild**

2. **Suppress-Konzept → Block Instance Visibility**
   - Block Instance visible/hidden pro Configuration
   - Kein Memory-Gewinn (Rhino lädt Block Definition immer), aber visuell äquivalent
   - Alternative: Block Definition gar nicht referenzieren (echtes Suppress)

3. **BOM-Struktur → Assembly Outliner**
   - Hierarchie: Nested Blocks = Sub-Assemblies
   - Block Definition = Part
   - Block Instance = Component Instance
   - Custom Properties via User Strings / Attributes

4. **Configuration-spezifische Properties**
   - Teilenummer, Beschreibung per Configuration
   - User Strings auf Block Instances

#### ⚠️ Angepasst übertragbar

5. **Configurations (geometrisch) → Multiple Block Definitions**
   - Rhino hat keine parametrischen Dimensionen
   - Stattdessen: **Separate Block Definitions pro geometrische Variante**
   - "M6_Washer" und "M8_Washer" = zwei Block Definitions (nicht eine mit Configs)
   - **Configuration in unserem System = Set von:**
     - Welche Block Definition jede Instance referenziert
     - Visibility State pro Instance
     - Property Overrides
     - Display Overrides (Color, Material)

6. **Derived Configurations → Configuration Inheritance**
   - Parent Configuration definiert Basis-Zustand
   - Child Configuration überschreibt nur Deltas
   - Gut abbildbar als Software-Konzept

#### ❌ Nicht übertragbar

7. **Dimension-Overrides:** Rhino hat keine parametrischen Dimensionen
8. **Feature Suppress:** Rhino hat keinen Feature Tree
9. **Mates:** Rhino hat keine parametrischen Constraints
10. **Lightweight Mode:** Rhino hat eigenes Mesh-System, aber kein on-demand Loading von Block-Definitionen

### Vorgeschlagenes Rhino-Konfigurationsmodell

```
Assembly (Rhino File)
├── Configuration "Standard"
│   ├── Instance 1 → BlockDef "Bracket_M6" [Visible] [Color: Silver]
│   ├── Instance 2 → BlockDef "Bolt_M6x20" [Visible]
│   └── Instance 3 → BlockDef "Cover_Plate" [Visible]
│
├── Configuration "Heavy Duty"
│   ├── Instance 1 → BlockDef "Bracket_M8" [Visible] [Color: Black]
│   ├── Instance 2 → BlockDef "Bolt_M8x25" [Visible]
│   ├── Instance 3 → BlockDef "Cover_Plate" [Visible]
│   └── Instance 4 → BlockDef "Reinforcement" [Visible]  ← nur in dieser Config
│
├── Configuration "Simplified"
│   ├── Instance 1 → BlockDef "Bracket_M6" [Visible]
│   ├── Instance 2 → BlockDef "Bolt_M6x20" [HIDDEN]  ← "suppressed"
│   └── Instance 3 → BlockDef "Cover_Plate" [Visible]
```

**Eine Configuration speichert pro Instance:**
- Referenzierte Block Definition (welche Variante)
- Visibility (sichtbar/hidden = suppress-Äquivalent)
- Display Overrides (Color, Material, Transparency)
- Custom Properties (Teilenummer, Beschreibung)
- Transform Override (optional, für Positionsvarianten)

---

## 7. Vergleich mit anderen CAD-Systemen

### Autodesk Inventor

| Konzept | Inventor-Name | Vergleich zu SW |
|---------|--------------|-----------------|
| Part Configurations | **iPart** (Factory/Member) | Ähnlich, aber jede Variante wird als **separate Datei** (Member) generiert |
| Assembly Configurations | **iAssembly** | Ähnlich wie iPart, generiert Member-Dateien |
| Design Table | **iPart Table** | Excel-basiert wie SW, aber Members werden materialisiert |
| Display States | **Design View Representations** | Äquivalent |
| Lightweight | **Level of Detail (LOD)** | Expliziter: User definiert LOD-Stufen mit vereinfachter Geometrie |
| Suppress | **Suppress** | Identisch |

**Key Difference:** Inventor materialisiert Varianten als separate Dateien. SW hält alles in einer Datei. Inventor-Ansatz ist expliziter, SW-Ansatz ist kompakter.

### CATIA V5/V6

| Konzept | CATIA-Name | Vergleich zu SW |
|---------|-----------|-----------------|
| Configurations | **Design Tables + Rules** | Mächtiger, regelbasiert (Knowledge Advisor) |
| Display States | **Scene Management** | Ähnlich |
| Suppress | **Activate/Deactivate** | Identisch |
| Lightweight | **CGR Mode** (Computed Graphic Representation) | Ähnlich, eigenes Tessellation-Format |
| Assembly | **Product Structure** | Komplexer, mehr Metadaten |

**Key Difference:** CATIA trennt strenger zwischen Design (CATPart) und Manufacturing (CATProduct). Konfigurationen sind stärker regelbasiert.

### Siemens NX

| Konzept | NX-Name | Vergleich zu SW |
|---------|---------|-----------------|
| Configurations | **Expressions + Part Families** | Expression-basiert (Variablen), Part Families mit Spreadsheet |
| Display States | **Arrangement** | Ähnlich |
| Suppress | **Suppress** | Identisch |
| Lightweight | **JT Representation** | Separates leichtgewichtiges Format |
| Assembly | **Component Patterns + Arrangements** | Flexibler, Arrangements für Positions-Varianten |

**Key Difference:** NX Arrangements sind mächtiger — sie können Component-Positionen variieren (z.B. Tür offen/geschlossen), was in SW nur über Configurations geht.

### Zusammenfassung: Beste Ansätze

| Aspekt | Bester Ansatz | Quelle |
|--------|--------------|--------|
| Visibility-Varianten ohne Rebuild | Display States (leichtgewichtig) | SW / alle |
| Geometrische Varianten | Explizite Definitions (wie Inventor Members) | Inventor |
| Positions-Varianten | Arrangements | NX |
| Regelbasierte Configs | Knowledge Templates | CATIA |
| BOM-Integration | Configuration Properties | SW |

### Empfehlung für Rhino Assembly Outliner

1. **Display States** als Kernkonzept (lightweight, kein Rebuild nötig)
2. **Configurations als Named State-Sets** (Kombination aus Visibility + Block-Def-Referenz + Properties)
3. **Keine parametrischen Dimensionen** anstreben (passt nicht zu Rhinos Philosophie)
4. **Block Definition Swapping** für geometrische Varianten (Inventor-ähnlich)
5. **Property-System** pro Configuration für BOM-Integration
6. **Derived Configurations** als Nice-to-have (Parent/Child Inheritance)

---

## Quellen

- DriveWorks: "The Ultimate Guide to SOLIDWORKS Configurations" (2025)
- CATI: "SOLIDWORKS Configurations Part 1 & 2" (2022)
- SolidWorks Help: Configurations Overview, Configurable Parameters
- Epectec Blog: "SolidWorks Configurations for Parts and Assembly Modeling"
- Hawk Ridge Systems: "SOLIDWORKS File Opening Modes"
- Javelin Tech: "Increase Your SOLIDWORKS Assembly Speed" (2025)
- Autodesk Forums: "Inventor equivalent to configurations in Solidworks"
