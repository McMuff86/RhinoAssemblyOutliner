# VisualARQ Reverse Engineering — Custom Object System

> **Datum:** 15.02.2026  
> **Ziel:** Architektur von VisualARQ als Blueprint für eigenes Custom Object System verstehen

---

## 1. Architektur-Übersicht

### Wie sind Wall, Door, Window etc. implementiert?

VisualARQ nutzt **nicht** direkte `CRhinoObject`-Subclasses für seine Objekte. Stattdessen basiert die Architektur auf:

1. **Standard Rhino-Geometrie** (Brep/Mesh/Extrusion) als visueller Träger
2. **UserData auf Geometry/Attributes** für parametrische Daten (persists durch Copy, Transform, File IO)
3. **Document User Data** für Style-Definitionen und globale Plugin-Daten
4. **VisualARQ.Script.dll** als API-Schicht über den internen Strukturen

### Technische Basis

- VisualARQ-Objekte sind intern **Breps/Extrusions** mit angehängtem `ON_UserData`
- Die UserData wird an `CRhinoObject.Geometry` oder `CRhinoObject.Attributes` angehängt (nicht an `CRhinoObject` selbst — das würde nicht in .3dm gespeichert!)
- Jedes Objekt hat eine **Style-ID** (GUID) die auf eine Style-Definition verweist
- Style-Definitionen werden als **Document User Data** gespeichert (via `CRhinoPlugIn::WriteDocument`/`ReadDocument`)

### Rhino SDK Custom Object Mechanismen (die VisualARQ nutzen könnte)

Rhino bietet `Custom*Object`-Klassen in `Rhino.DocObjects.Custom`:
- `CustomBrepObject`
- `CustomCurveObject`  
- `CustomMeshObject`

Diese erlauben:
- Eigene Grips (Control Points)
- Eigene Draw-Methoden
- Eigene Geometry-Regeneration
- UserData-basierte Parametrik

**Wahrscheinlichkeit:** VisualARQ nutzt diese `Custom*Object`-Subclasses für die Rhino-Integration, kombiniert mit UserData für Persistenz.

---

## 2. Object Lifecycle

### Erstellung
1. User ruft Command (z.B. `vaWall`) auf
2. Insert Dialog zeigt Style-Auswahl + Parameter-Preview (2D/3D)
3. User klickt Start-/Endpunkt
4. VisualARQ erzeugt Brep-Geometrie basierend auf Style-Definition + User-Input
5. UserData mit Style-ID + Instance-Parametern wird angehängt
6. Objekt wird in Rhino Document eingefügt

### Speichern/Laden (3dm-Kompatibilität)
- **Geometry UserData** (ON_UserData mit `Archive()=true`) wird automatisch in .3dm gespeichert
- **Document UserData** (Style-Definitionen) wird über Plugin's `WriteDocument()`/`ReadDocument()` gespeichert
- Beim Laden: Rhino erkennt Plugin-ID in den UserData → lädt VisualARQ automatisch → `ReadDocument()` stellt Styles wieder her

### Fallback ohne VisualARQ
- Die **Geometrie** (Brep) bleibt sichtbar — es ist ja native Rhino-Geometrie
- **Parametrische Funktionalität geht verloren** — keine Grips, kein Style-Editing
- UserData bleibt im File erhalten (Rhino löscht fremde UserData nicht)
- Bekannter Bug-Fix: "Fixed a crash when opening a 3DM with erased groups in a Rhino instance without VisualARQ loaded"
- → Rhino zeigt die Objekte als "dumme" Breps/Meshes an

### Copy/Paste zwischen Dokumenten
- UserData auf Geometry/Attributes wird automatisch mitkopiert (Rhino-Feature)
- Style-Definitionen müssen separat übertragen werden (vermutlich über Document UserData Merge)

---

## 3. Grips / Control Points

### Wall Control Points (aus offizieller Doku)

Walls haben **differenzierte Grip-Typen**:

| Grip | Funktion | Viewport |
|------|----------|----------|
| **Start-Punkt** | Wall-Insertionspunkt, Stretch + Richtung | Alle |
| **End-Punkt** | Richtung + Stretch | Alle |
| **Pfeil nach oben** | Höhe ändern | Nicht in Top |
| **Pfeil nach unten** | Wall-Pfad vertikal verschieben | Nicht in Top |
| **Horizontale Pfeile** | Stretch horizontal (nur bei Grad-1-Kurven) | Alle |
| **Kurven-Kontrollpunkte** | Pfad-Modifikation (bei Walls aus Kurven) | Alle |

### Implementierungs-Prinzip
- Grips sind direkt mit parametrischen Properties verknüpft
- "Control arrow upwards" → modifiziert `Height` Property
- "Horizontal arrows" → modifiziert `Length` implizit
- Start/End-Punkte → modifizieren die Path Curve
- **Aktivierung** wie bei standard Rhino-Objekten (PointsOn / F10)

### Door/Window Grips
- Position in der Wand (entlang Wall-Path)
- Breite/Höhe des Opening-Profils
- Sind über Host-Wall-Referenz verknüpft (lesen Wall-Thickness, Wall-Height)

---

## 4. Style/Type-System

### Architektur
- **Style** = Typ-Definition mit Standard-Parametern
- **Instance** = konkretes Objekt im Modell, referenziert Style

### Wall-Style Beispiel
Ein Wall-Style definiert:
- **Schichten** (Layers): Jede mit Material, Dicke, Funktion
- **Default-Höhe**
- **Display-Einstellungen** (Isocurves, Plan-Darstellung)
- **Wall-Joint-Typ** (Miter/Butt/None)

### Style vs Instance Properties

| Property | Style-Level | Instance-Override |
|----------|:-----------:|:-----------------:|
| Schicht-Aufbau | ✅ | ❌ |
| Default-Höhe | ✅ | ✅ (pro Wand änderbar) |
| Layer Thickness | ✅ | ✅ (pro Wand änderbar) |
| Layer Top/Bottom Offset | ✅ | ✅ (pro Wand änderbar) |
| Name | — | ✅ |
| Elevation | — | ✅ |
| Path Curve | — | ✅ |
| Alignment Offset | — | ✅ |

### Change Style
- Einfach Style-ID auf der Instance ändern → Geometrie wird regeneriert
- Vergleichbar mit SolidWorks Configurations, aber einfacher: keine Feature-Tree-Abhängigkeiten

### Grasshopper Styles (ab VisualARQ 2)
- **Revolutionäres Feature:** Style-Geometry wird durch .gh-Definition definiert
- Input-Parameter → werden zu editierbaren Properties
- Output-Geometrie → wird zu Model/Plan/Preview-Representation
- Wizard mappt GH-Inputs auf VisualARQ-Konzepte (Path, Height, Profile etc.)
- Unterstützt: Wall, Curtain Wall, Beam, Column, Door, Window, Opening, Stair, Railing, Slab, Roof, Furniture, Element, Annotation

---

## 5. Properties Panel Integration

### Rhino Properties Panel
- VisualARQ injiziert eine **eigene Section** in Rhinos Properties Panel
- Sichtbar wenn ein VisualARQ-Objekt selektiert ist
- Zeigt: General, Display, Geometry, Location, Intersections

### Property-Hierarchie
1. **Read-Only (berechnet):** Volume, Area, Length, Thickness
2. **Style-Level (mit Instance-Override):** Height, Layer Thickness, Layer Offsets
3. **Instance-Only:** Name, Description, Elevation, Path Curve, Alignment Offset
4. **Custom Parameters:** Via `vaAddDocumentParameter` API — frei definierbar, pro Objekt setzbar

### API-Zugang
```csharp
using va = VisualARQ.Script;

// Parameter erstellen
var priceId = va.AddDocumentParameter("Price", va.ParameterType.Currency, "Costs");

// Wert auf Objekt setzen
va.SetParameterValue(priceId, elementId, 100.0);
```

---

## 6. VisualARQ + Grasshopper

### Bidirektionale Integration
1. **VisualARQ → GH:** Alle VA-Objekte als GH-Components verfügbar (Deconstruct, Query)
2. **GH → VisualARQ:** Grasshopper Styles — GH-Definitionen erzeugen VA-Objekte
3. **GH-Components:** Create + Deconstruct für alle Objekttypen
4. **Pipeline Component:** Kann Block Definitions/Instances abfragen (ab v3)

### Grasshopper Style Workflow
1. GH-Definition erstellen (.gh) mit Input/Output Parametern
2. In VisualARQ Style-Dialog: "New > Grasshopper Style"
3. Wizard mappt: Inputs → Properties, Outputs → Geometry-Components
4. Jeder Input wird editierbarer Parameter (Style oder Instance-Level)
5. Output-Geometrie wird Representation (Model/Plan/Preview)

### Einschränkung
- VisualARQ Script API **nicht für GH empfohlen** — arbeitet mit Document-Resident Objects, GH mit In-Memory Geometry
- Manipulation würde bei jedem Parameterwechsel neues Objekt erzeugen
- Grasshopper scripting support "may be added in the future"

### Was wir daraus lernen
- Grasshopper Styles = unser **GH-Definition-als-Style** Ansatz
- Extrem mächtig: User können ohne Plugin-Code eigene parametrische Objekte erstellen
- Wir sollten ähnliches bieten: Assembly-Definitionen aus GH

---

## 7. Lessons Learned / Limitationen

### Was funktioniert gut
- **Style-System** ist intuitiv und mächtig
- **Grasshopper Styles** = Killer-Feature, unlimitierte Erweiterbarkeit
- **Properties Panel Integration** nahtlos in Rhino
- **Fallback** (Geometrie bleibt ohne Plugin sichtbar)
- **Grips** direkt an parametrische Properties gebunden
- **Wall-Joints** automatisch berechnet

### Bekannte Probleme
- **Performance** bei vielen Grasshopper-Style-Objekten (jedes evaluiert GH-Definition)
- **API noch WIP** — nicht alle Features exponiert
- **GH-Scripting nicht unterstützt** — Document-Resident vs In-Memory Konflikt
- **Grasshopper Style Walls:** Butt-Joint nicht unterstützt
- **Copy/Paste:** Style-Definitionen müssen manuell übertragen werden
- **Crash-Risiko** bei Files mit VA-Daten ohne VA-Plugin

### Was wir anders machen würden
1. **Robusteres Fallback:** Proxy-Objekt-Pattern statt "dummes Brep" (wie Revit Families)
2. **Style-Transfer:** Automatisches Einbetten von Style-Definitionen bei Copy/Paste
3. **GH-Integration:** Von Anfang an GH-kompatible API (nicht nachträglich)
4. **Lightweight Instances:** Nicht jedes Objekt braucht volle Brep-Regeneration
5. **Versionierung:** Style-Definitionen mit Versionshistorie

---

## 8. Vergleich mit anderen Systemen

### Lands Design (Asuni)
- Gleicher Hersteller wie VisualARQ
- Sehr wahrscheinlich **identische Architektur** (UserData + Document Data + Custom Objects)
- Bestätigt: Nutzt gleiche Grasshopper-Style-Infrastruktur
- Beweist: Das Pattern ist generisch genug für verschiedene Domänen (Architektur, Landschaft)

### Rhino Custom Object SDK
- `CustomBrepObject`, `CustomCurveObject`, `CustomMeshObject` — native Subclasses
- UserData auf Geometry (persistiert in .3dm) vs auf RhinoObject (nicht persistiert)
- **3 Attachment-Punkte:** Geometry, Attributes, RhinoObject (nur erste zwei persistieren)

### Revit/Tekla (zum Vergleich)
- **Revit:** Eigenes Datenformat, Families als parametrische Typen, keine Fallback-Geometrie
- **Tekla:** Custom Components mit eigenem API, proprietäres Format
- **Vorteil VisualARQ:** Baut auf offenem .3dm Format auf, Geometrie überlebt Plugin-Deinstallation

### PanelingTools (McNeel)
- Simpler: Arbeitet hauptsächlich mit Grid-Patterns auf Flächen
- Kein echtes Custom-Object-System, mehr ein Transform-Tool
- Keine eigenen Object-Types

---

## 9. Blueprint für unser System

### Architektur-Empfehlung basierend auf VisualARQ

```
┌─────────────────────────────────────────┐
│           Assembly Style (Document)      │
│  - Style-ID (GUID)                      │
│  - Default Parameters                   │
│  - Component-Definitionen               │
│  - GH-Definition (optional)             │
│  - Representations (Model/Plan/Preview) │
└──────────────┬──────────────────────────┘
               │ referenziert
┌──────────────▼──────────────────────────┐
│        Assembly Instance (Object)        │
│  - CustomBrepObject Subclass            │
│  - UserData auf Geometry:               │
│    · Style-ID                           │
│    · Instance-Overrides                 │
│    · Component-Tree (Parent/Children)   │
│  - Custom Grips → mapped to Properties  │
│  - Fallback: Brep bleibt ohne Plugin    │
└─────────────────────────────────────────┘
```

### Key Decisions
1. **UserData auf Geometry** (nicht Attributes, nicht RhinoObject) → maximale Persistenz
2. **Document User Data** für Styles/Types → Plugin ReadDocument/WriteDocument
3. **Custom*Object Subclasses** für Grips und Drawing
4. **Grasshopper-Style-Support** als Stretch Goal
5. **Proxy-Pattern** für robustes Fallback ohne Plugin

### Nächste Schritte
- [ ] Prototype: Einfaches CustomBrepObject mit UserData
- [ ] Prototype: Document Data für Style-Definitionen
- [ ] Prototype: Custom Grips die Properties modifizieren
- [ ] Evaluate: Performance von UserData vs Dictionary-based Storage
