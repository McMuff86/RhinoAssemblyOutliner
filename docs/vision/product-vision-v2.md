# Product Vision v2: Assembly System for Rhino

> **Codename:** BlockForge  
> **Tagline:** *"SolidWorks Assembly Management — inside Rhino."*  
> **Datum:** 15. Februar 2026  
> **Status:** Vision Draft

---

## 1. Was wir bauen

Ein **Assembly-Management-System für Rhino** — das erste Plugin, das Rhino-Blocks wie echte Engineering-Assemblies behandelt. Mit Per-Instance Configurations, Custom Grips, Properties Panel, und BOM-Export.

**Die Kernidee:** Rhino hat Blocks. Aber Blocks sind dumm — jede Instanz ist identisch. Wir machen sie intelligent. Jede Instanz kann eigene Sichtbarkeiten, Dimensionen, Varianten und Properties haben. Ohne Grasshopper, ohne Scripting.

### Was wir NICHT sind

| Wir sind NICHT | Warum nicht |
|---|---|
| VisualARQ / BIM | Keine Wände, Türen, IFC. Wir sind domain-agnostisch. |
| Parametric Modeler | Kein Constraint-Solver, keine Sketch-Features. |
| Grasshopper-Ersatz | Kein visuelles Programmieren. Wir sind ein Produktivitäts-Tool. |
| CAM/Nesting Tool | Wir enden bei der Stückliste. Fertigung ist downstream. |

### Kern-USP

**Per-Instance Intelligence auf Rhino Blocks** — etwas, das Rhino nativ nicht kann und das kein existierendes Plugin liefert. Der User definiert eine Block Definition mit Varianten/Konfigurationen, und jede Instanz kann individuell konfiguriert werden — mit visueller Kontrolle über den Outliner und Custom Grips direkt im Viewport.

---

## 2. Zielgruppen (priorisiert)

### Tier 1 — Primär (hier liegt das Geld)

**🪚 Schreiner & Möbelbauer**
- Pain: Möbel-Assemblies in Rhino sind manuell. Jeder Schrank, jede Schublade einzeln.
- Use Case: Korpus-Block mit konfigurierbaren Dimensionen, Beschläge als Unter-Assemblies, automatische Stückliste.
- Zahlungsbereitschaft: Hoch. Gewohnt an Software-Lizenzen (Cabinet Vision: $5000+).
- Marktgrösse: Gross. Viele Schreiner nutzen Rhino als günstige Alternative zu spezialisierten Tools.

**🏗 Metallbauer & Fassadenbauer**
- Pain: Repetitive Fassadenelemente, Stahlkonstruktionen mit vielen identischen-aber-doch-verschiedenen Teilen.
- Use Case: Fassadenpanel-Block mit variablen Grössen, Befestigungselemente als Sub-Components.
- Zahlungsbereitschaft: Hoch. Industriekontext.

### Tier 2 — Sekundär

**🎨 Produktdesigner**
- Pain: Gehäuse-Assemblies, Stücklisten für Prototyping.
- Use Case: Produkt mit konfigurierbaren Varianten, Explosionsdarstellungen.

**🏛 Architekten (Custom Components)**
- Pain: Wiederkehrende Elemente die nicht BIM sind (Custom Möbel, Einbauten, Installationen).
- Bewusste Abgrenzung: Wir sind für die Dinge, für die VisualARQ zu viel ist.

### Tier 3 — Nice to Have

**🔧 Maker / Fabrication**
- CNC-Projekte, 3D-Print Assemblies. Kleineres Budget, aber Community-Effekt.

---

## 3. Core Features

### Haben wir bereits ✅
- Assembly Outliner (Tree View, Expand/Collapse)
- Hierarchische Block-Navigation
- Visibility Toggle pro Node
- Zoom-to-Object / Select in Viewport
- Custom C++ RhinoCommon Object (Grundlage)

### Phase 1 — MVP 🎯

**Per-Instance Component Visibility**
- Einzelne Sub-Components pro Instanz ein/ausblenden
- Use Case: Schrank mit/ohne Rückwand, Schublade offen/geschlossen
- *Das ist der Feature, der die Story erzählt.*

**Properties Panel**
- Instance-Level Properties anzeigen und editieren
- Key-Value Pairs pro Instanz (Material, Finish, Artikelnummer)
- Standard Rhino Panel-Integration (nicht eigenes Fenster)

**BOM Export (Basic)**
- Stückliste als CSV/Excel
- Aggregiert über alle Instanzen
- Spalten: Name, Quantity, Material, Custom Properties

### Phase 2 — Configurations 🔧

**Per-Instance Configurations**
- Definition: Eine Block Definition hat mehrere "Configurations" (z.B. Grössen S/M/L)
- Jede Instanz wählt eine Configuration
- Configurations steuern: Sichtbarkeit von Sub-Components, Transformationen, Properties
- UI: Dropdown im Properties Panel

**Custom Grips**
- Stretch-Grips an Block-Instanzen
- User definiert Grip-Points in der Block Definition
- Instanz-Level: Grips verändern Dimensionen der Instanz
- Visuelles Feedback im Viewport

**Smart Duplicate**
- Block-Instanz duplizieren MIT allen Per-Instance-Settings
- Rhino's native Duplicate verliert unsere Custom Data

### Phase 3 — Full Vision 🚀

**Assembly Constraints (Light)**
- Snap-Points zwischen Components (Beschlag → Bohrung)
- Keine vollständigen Constraints wie SolidWorks, aber "Attachment Points"
- Genug für: "Scharnier sitzt hier am Korpus"

**Varianten-Manager**
- Globale Varianten-Tabelle (wie SolidWorks Design Table)
- Excel/CSV Import für Varianten-Definitionen
- "Konfiguriere alle Instanzen vom Typ X auf Variante Y"

**Drawing Integration**
- BOM-Tabelle direkt in Rhino Layout
- Ballon-Nummern / Positionsnummern
- Auto-Update bei Änderungen

**Report Generator**
- Cutting Lists (Zuschnittlisten für Schreiner)
- Hardware Lists (Beschlaglisten)
- Custom Report Templates

**Explosionsdarstellung**
- Automatische Explosion entlang Achsen
- Animations-Export (für Montageanleitungen)

---

## 4. Part/Assembly Konzept

### Architektur-Entscheidung: Alles in .3dm

Kein eigenes File-Format. Gründe:
- Rhino-User erwarten .3dm-Kompatibilität
- Collaboration wird nicht komplizierter
- File-Referenzen (wie SolidWorks) sind fragil und Rhino-fremd

### Mapping

| SolidWorks | Rhino (nativ) | BlockForge |
|---|---|---|
| Part | Block Definition | **Part Definition** (Block Def + Metadata) |
| Assembly | — | **Assembly** (hierarchische Block-Struktur) |
| Configuration | — | **Configuration** (Custom Object Data) |
| Instance | Block Instance | **Component Instance** (Block Inst + Per-Instance Data) |
| BOM | — | **BOM Generator** |

### Wo leben die Daten?

```
Block Definition (Rhino-nativ)
└── BlockForge Metadata (UserDictionary / Custom Object)
    ├── Configuration Definitions
    │   ├── Config "Standard": {visibility: [...], properties: {...}}
    │   ├── Config "Gross": {visibility: [...], properties: {...}}
    │   └── Config "Mit Schublade": {visibility: [...], properties: {...}}
    ├── Grip Definitions (Points + Constraints)
    └── Property Schema (welche Properties existieren)

Block Instance (Rhino-nativ)
└── BlockForge Instance Data (UserDictionary / Custom Object)
    ├── Active Configuration: "Gross"
    ├── Per-Instance Visibility Overrides
    ├── Per-Instance Property Values
    └── Grip State (aktuelle Dimensionen)
```

### Configurations-Modell

**Nicht** separate Block Definitions pro Variante (Rhino's naiver Ansatz — explodiert bei vielen Varianten). Stattdessen:

1. **Eine** Block Definition enthält **alle** Geometrie aller Varianten
2. Configurations definieren, welche Sub-Components sichtbar sind
3. Instanzen wählen eine Configuration → zeigen nur relevante Geometrie
4. Per-Instance Overrides möglich (Configuration als Basis, dann tweaken)

Das ist elegant, performant, und funktioniert mit Rhino's Block-System.

---

## 5. Naming & Branding

### Empfehlung: **BlockForge**

| Kandidat | Pro | Contra |
|---|---|---|
| **BlockForge** | Stark, einprägsam, "Forge" = bauen/schmieden, domain-agnostisch | Kein "Rhino" im Namen |
| AssemblyManager | Beschreibend, klar | Generisch, langweilig, klingt nach Enterprise-Software |
| RhinoAssembly | Hat "Rhino", klar | McNeel könnte Trademark-Bedenken haben |
| ComponentManager | Klar | Langweilig |
| BlockForge for Rhino | Best of both | Etwas lang |

**Empfehlung:** **BlockForge** als Produktname, **"BlockForge for Rhino"** als voller Name auf Food4Rhino.

**Taglines (Kandidaten):**
- *"Assembly Intelligence for Rhino"*
- *"Your blocks, but smarter."*
- *"From blocks to products."*

---

## 6. Geschäftsmodell

### Marktanalyse — Vergleichbare Plugins

| Plugin | Preis | Funktionsumfang |
|---|---|---|
| VisualARQ | €695 | BIM-System für Rhino (Wände, Türen, IFC) |
| Lands Design | €495 | Landschaftsarchitektur |
| RhinoNest | €995 | Nesting/CNC |
| Bongo | $495 | Animation |
| PanelingTools | Gratis | Paneling (von McNeel) |
| Elefront | Gratis | Block/Data Management (Grasshopper) |
| VisualARQ + Grasshopper | €695 | Der nächste Vergleichspunkt |

### Preispositionierung

**Empfehlung: Freemium → €195–€295**

| Tier | Preis | Features |
|---|---|---|
| **BlockForge Free** | €0 | Outliner, Basic Visibility, 1 Assembly (max 50 Components) |
| **BlockForge Pro** | €195/Lizenz | Alles: Configurations, Grips, BOM, Properties, Unlimited |
| **BlockForge Pro (mit Wartung)** | €195 + €49/Jahr | Pro + Updates + Support |

**Warum €195 und nicht €495+:**
- Wir sind nicht so umfangreich wie VisualARQ (das ist ein komplettes BIM-System)
- €195 ist Impulskauf-Bereich für professionelle User
- Niedrigerer Preis → mehr Volumen → grössere Community → mehr Feedback
- Preis kann mit Features steigen (Phase 3 → €295)

**Warum nicht gratis:**
- C++ Plugin = ernsthafter Entwicklungsaufwand
- Gratis-Plugins bekommen weniger Respekt/Vertrauen ("wenn's gratis ist, wird's wohl nix")
- Professionelle User zahlen gerne für Tools, die ihnen Zeit sparen

### Revenue-Szenarien (Jahr 1)

| Szenario | Lizenzen | Revenue |
|---|---|---|
| Konservativ | 100 | €19'500 |
| Realistisch | 300 | €58'500 |
| Optimistisch | 700 | €136'500 |

Food4Rhino hat ~500k registrierte User. Selbst 0.05% Conversion = 250 Lizenzen.

---

## 7. Roadmap

### Phase 1: MVP — "The Outliner That Does More" (Q1–Q2 2026)

**Scope:**
- ✅ Assembly Outliner (Tree View, Navigation, Visibility)
- 🔨 Per-Instance Component Visibility (DER Killer-Feature)
- 🔨 Basic Properties Panel (Key-Value pro Instanz)
- 🔨 BOM Export (CSV)
- 🔨 Food4Rhino Listing (Free Tier)

**Ziel:** Erste User, erstes Feedback, Proof of Concept auf dem Markt.  
**Meilenstein:** 50 Downloads, 10 aktive User, 5 Feedback-Gespräche.

### Phase 2: Configurations — "Now We're Talking" (Q3–Q4 2026)

**Scope:**
- Per-Instance Configurations
- Custom Grips (Basic: Stretch in 1-2 Achsen)
- Properties Panel mit Configuration-Dropdown
- Smart Duplicate
- BOM Export (Excel, gruppiert)
- **Pro-Lizenz Launch**

**Ziel:** Zahlende Kunden, Product-Market-Fit validieren.  
**Meilenstein:** 100 Pro-Lizenzen, klares Feedback welche Branche am meisten Wert sieht.

### Phase 3: Full Assembly — "The Vision" (2027)

**Scope:**
- Advanced Grips (Multi-Axis, Snap)
- Assembly Constraints (Light)
- Varianten-Manager / Design Table
- Drawing Integration (Layout BOM)
- Report Generator (Cutting Lists)
- Explosionsdarstellung

**Ziel:** Category Leader für Assembly Management in Rhino.  
**Meilenstein:** 500+ Pro-Lizenzen, Food4Rhino Top 20.

### Realistische Timeline

```
        2026                              2027
  Q1      Q2      Q3      Q4      Q1      Q2
  ├───────┼───────┼───────┼───────┼───────┤
  │ Phase 1 (MVP) │  Phase 2 (Config)   │
  │               │                     │ Phase 3 ...
  │ Outliner ✅    │ Configurations      │ Constraints
  │ Visibility    │ Custom Grips        │ Varianten-Mgr
  │ Properties    │ Pro Launch 💰       │ Drawing Int.
  │ BOM (CSV)     │ BOM (Excel)         │ Reports
  │ F4R Listing   │                     │ Explosions
  └───────────────┴─────────────────────┘
```

---

## 8. Wettbewerbsvorteil & Moat

### Warum das niemand sonst baut

1. **C++ Barrier:** Die meisten Rhino-Plugin-Entwickler arbeiten mit C#/Python. C++ Custom Objects sind eine Klasse für sich — mehr Performance, mehr Möglichkeiten, höhere Einstiegshürde.

2. **Nischen-Problem:** Für McNeel zu speziell. Für VisualARQ nicht BIM genug. Für Grasshopper-User zu "tool-artig". Genau dazwischen ist unser Sweet Spot.

3. **Network Effects:** Je mehr User, desto mehr Block-Bibliotheken, desto mehr Templates, desto wertvoller das Ökosystem.

### Defensibility

- **Technische Tiefe:** C++ Custom Objects, Custom Grips, tiefe Rhino-Integration. Nicht trivial nachzubauen.
- **First Mover:** Aktuell gibt es kein vergleichbares Assembly-Tool für Rhino (Elefront ist Grasshopper-only, VisualARQ ist BIM).
- **Community:** Wer einmal seine Assemblies in BlockForge organisiert hat, migriert nicht einfach weg.

---

## 9. Risiken & Mitigationen

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|---|---|---|
| McNeel baut es nativ | Niedrig | Hoch | Schneller sein, Community aufbauen, Features bieten die über "nativ" hinausgehen |
| Zu kleiner Markt | Mittel | Hoch | Phase 1 validiert Markt. Pivot möglich. Free Tier = kein finanzielles Risiko für User |
| Scope Creep → nie fertig | Hoch | Hoch | Strikte Phasen. Phase 1 = nur 4 Features. Ship early. |
| VisualARQ expandiert | Niedrig | Mittel | Unser USP ist domain-agnostisch. VisualARQ wird immer BIM-fokussiert bleiben. |

---

## 10. Sofort-Entscheidungen (für Adi)

1. **Name:** BlockForge — ja oder nein?
2. **Phase 1 Scope:** Stimmt die Feature-Liste? Zu viel? Zu wenig?
3. **Pricing:** €195 Pro — im richtigen Bereich?
4. **Free Tier Limit:** 1 Assembly / 50 Components — fair?
5. **Zielgruppe Fokus:** Schreiner first — richtig?

---

*Dieses Dokument ist eine lebende Vision. Version 2.0 — Stand Februar 2026.*
