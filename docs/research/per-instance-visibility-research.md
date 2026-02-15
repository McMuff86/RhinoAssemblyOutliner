# Per-Instance Component Visibility in Rhino 8 — Research Report

**Datum:** 2026-02-15  
**Kontext:** Assembly Outliner Plugin, SolidWorks-artiger Ansatz  
**Problem:** DisplayConduit kann nicht auf Komponenten innerhalb von Block-Instanzen zugreifen — Rhino rendert Blöcke atomar.

---

## 1. Rhino Block/Instance API — Deep Dive

### Fundamentales Datenmodell

Rhino's Block-System basiert auf zwei Konzepten:

- **InstanceDefinition** (`Rhino.DocObjects.InstanceDefinition`): Eine "Vorlage" in der `InstanceDefinitionTable`. Enthält eine Liste von Geometrie-Objekten (RhinoObjects) plus Metadaten (Name, Beschreibung, Basepoint). Lebt im Dokument, ist NICHT direkt in der Szene sichtbar.

- **InstanceObject** (`Rhino.DocObjects.InstanceObject`): Eine "Instanz" in der `ObjectTable`. Referenziert eine InstanceDefinition via `InstanceDefinition`-Property + `InstanceXform` (Transform-Matrix). Das ist das sichtbare Objekt in der Szene.

- **InstanceReferenceGeometry** (`Rhino.Geometry.InstanceReferenceGeometry`): Die Geometrie-Klasse die ein InstanceObject enthält. Constructor: `InstanceReferenceGeometry(Guid instanceDefinitionId, Transform transform)`.

### Kritische Erkenntnis: Alle Instanzen einer Definition sind identisch

Rhino behandelt **alle Instanzen derselben Definition als identisch** — nur die Transform unterscheidet sie. Es gibt KEINE per-Instance Overrides für Geometrie-Sichtbarkeit. Das ist fundamental anders als SolidWorks.

### InstanceDefinitionTable API

```csharp
// Definition erstellen
int idefIndex = doc.InstanceDefinitions.Add(
    name,           // string
    description,    // string  
    basePoint,      // Point3d
    geometry,       // IEnumerable<GeometryBase>
    attributes      // IEnumerable<ObjectAttributes>
);

// Definition-Geometrie KOMPLETT ersetzen
bool success = doc.InstanceDefinitions.ModifyGeometry(
    idefIndex,      // int
    newGeometry,    // IEnumerable<GeometryBase>
    newAttributes   // IEnumerable<ObjectAttributes>
);
// ACHTUNG: Ersetzt die GESAMTE Geometrie — alle Instanzen betroffen!

// Metadaten ändern (Name, Description)
doc.InstanceDefinitions.Modify(idef, newName, newDescription, quiet);

// Löschen (löscht auch alle Instanzen!)
doc.InstanceDefinitions.Delete(idefIndex, deleteReferences: true, quiet: false);

// Finden
var idef = doc.InstanceDefinitions.Find(name);
var idef = doc.InstanceDefinitions.FindId(guid);

// Undo support eingebaut:
doc.InstanceDefinitions.UndoModify(idefIndex);
```

### InstanceDefinition.GetObjects()

Gibt `RhinoObject[]` zurück — die Objekte die die Definition ausmachen. Diese Objekte:
- Haben eigene `Attributes` (Layer, Color, etc.)
- Haben eigene `Id` (Guid)
- Können selbst `InstanceObject` sein (= nested Blocks!)
- Sind **NICHT** in der normalen ObjectTable sichtbar
- Können nicht direkt modifiziert werden — nur via `ModifyGeometry()` der gesamten Definition

### Nested Blocks

Nested Blocks werden über `InstanceReferenceGeometry` innerhalb einer Definition realisiert:
- Definition A enthält Geometrie + eine `InstanceReferenceGeometry` die auf Definition B zeigt
- Wenn Definition B geändert wird, ändert sich automatisch auch A (transitiv)
- `InstanceDefinition.GetObjects()` gibt für nested blocks `InstanceObject`-Einträge zurück
- `InstanceDefinition.UsesDefinition(otherIndex)` prüft Abhängigkeiten

### Instanz einer anderen Definition zuweisen (SCHLÜSSEL-OPERATION!)

```csharp
// Eine Instanz von Definition A auf Definition B umhängen:
var oldInstance = ...; // InstanceObject
var newIRefGeom = new InstanceReferenceGeometry(
    newDefinitionId,           // Guid der neuen Definition
    oldInstance.InstanceXform  // Transform beibehalten
);
doc.Objects.Replace(oldInstance.Id, newIRefGeom);
```

**Das ist die zentrale Operation für per-Instance Visibility!** Man kann eine Instanz jederzeit auf eine andere Definition umhängen, wobei Position/Rotation/Scale erhalten bleiben.

---

## 2. Block-Definition Cloning Strategie

### Wie klont man eine Definition?

Es gibt kein `Clone()` auf InstanceDefinition. Man muss manuell:

```csharp
var sourceDef = doc.InstanceDefinitions[sourceIndex];
var objects = sourceDef.GetObjects();

var geometries = new List<GeometryBase>();
var attributes = new List<ObjectAttributes>();

foreach (var obj in objects)
{
    geometries.Add(obj.Geometry.Duplicate());
    attributes.Add(obj.Attributes.Duplicate());
}

// Neue Definition mit Subset der Geometrie (= Variante)
int variantIndex = doc.InstanceDefinitions.Add(
    variantName,
    sourceDef.Description,
    sourceDef.BasePoint,
    geometries,  // kann hier Objekte weglassen = "hidden"
    attributes
);
```

### Selektives Entfernen = Visibility

Um Komponente X "auszublenden":
1. Klone die Definition
2. Lasse Objekt X beim Klonen weg
3. Hänge die Instanz auf die neue Definition um

### Nested Blocks beim Klonen

Wenn die Quelldefinition nested Blocks enthält:
- Die `InstanceReferenceGeometry`-Objekte müssen mit kopiert werden
- Sie referenzieren die GLEICHE innere Definition (shared reference) — das ist gut!
- Wenn eine innere Definition selbst Varianten braucht, muss man auch dort klonen (rekursiv)

### Performance: 100+ Instanzen mit eigener Variante

**Worst Case:** N Instanzen × M Visibility-Zustände = bis zu N eigene Definitionen

**Realität:**
- Eine Definition ist "leichtgewichtig" — sie speichert nur Referenzen auf Geometrie-Kopien
- Geometrie selbst ist kopiert (deep copy via `Duplicate()`), aber Rhino ist effizient damit
- **Deduplizierung möglich:** Wenn 50 von 100 Instanzen den gleichen Visibility-State haben, brauchen sie nur EINE gemeinsame Varianten-Definition
- **Schätzung:** Eine Definition mit 20 Objekten ≈ 1-5 KB Overhead (Metadaten). Die Geometrie-Kopien sind teurer, aber bei Blöcken typischerweise klein (Referenz-Geometrie, keine High-Poly Meshes)

**Benchmark-Empfehlung:** Bei 100+ Varianten sollte man messen. Aber Rhinos Block-System ist genau dafür designed (tausende Definitionen sind normal in Architektur-Files).

### Memory-Kosten

- Jede Varianten-Definition dupliziert die Geometrie der sichtbaren Objekte
- Nested Block-Referenzen sind billig (nur ein Guid + Transform)
- Für einen Block mit 10 Breps à 50KB: ~500KB pro Variante → 100 Varianten = ~50MB
- **Mitigation:** Varianten nur bei Bedarf erstellen ("lazy"), identische States zusammenlegen

### Undo-Integration

- `InstanceDefinitions.Add()` und `Objects.Replace()` sind standard Rhino-Operationen
- Sie werden automatisch ins Undo-System aufgenommen
- **ABER:** Man muss aufpassen, dass Undo korrekt rückgängig macht:
  - Bei Undo muss die Instanz wieder auf die Original-Definition zeigen
  - `doc.InstanceDefinitions.UndoModify()` existiert
  - Custom Undo-Records über `doc.BeginUndoRecord()` / `doc.EndUndoRecord()` möglich

---

## 3. Alternative Ansätze

### 3.1 DisplayConduit (aktueller Ansatz — GESCHEITERT)

**Problem:** Rhino zeichnet Block-Instanzen als eine einzige Draw-Operation. Der DisplayConduit bekommt `DrawObject()`-Callbacks pro `InstanceObject`, aber NICHT pro Unter-Objekt innerhalb des Blocks. Man kann:
- Die gesamte Instanz ausblenden ✓
- Die gesamte Instanz anders einfärben ✓
- Einzelne Komponenten INNERHALB der Instanz ausblenden ✗

Es gibt keinen Hook um in den Block-Render-Vorgang einzugreifen.

### 3.2 Layer-basierter Ansatz

**Fakt:** Objekte innerhalb einer Block-Definition haben Layer-Zuordnungen. Wenn der Layer ausgeschaltet wird, sind diese Objekte in ALLEN Instanzen unsichtbar.

**Vorteile:**
- Einfach, kein Cloning nötig
- Natives Rhino-Verhalten

**Nachteile:**
- Wirkt GLOBAL auf alle Instanzen — kein per-Instance Control
- Verbraucht Layer für jede Komponente
- Konflikte mit dem regulären Layer-Management des Users

**Für per-Instance:** Kombination Layer + Cloning theoretisch möglich (verschiedene Definitionen referenzieren Objekte auf verschiedenen Layern), aber unnötig komplex. Reines Definition-Cloning ist einfacher.

### 3.3 Object.Hide() innerhalb von Definitionen

Objekte innerhalb einer Definition können theoretisch `IsHidden=true` haben. **ABER:**
- `ModifyGeometry()` ersetzt ALLES — man kann nicht einzelne Attribute ändern
- Selbst wenn man hidden Objekte in die Definition packt: sie sind in ALLEN Instanzen hidden
- Kein per-Instance Nutzen

### 3.4 Wie machen es andere Plugins?

**VisualARQ:**
- Nutzt eigene Custom Objects (abgeleitet von RhinoObject)
- Implementiert eigenes Display via CustomMeshProvider
- Hat "Styles" die verschiedene Konfigurationen definieren
- Jede Instanz kann einem Style zugewiesen werden
- **Nicht direkt auf Block-System gebaut** — VisualARQ-Objekte sind Rhino-Blöcke, aber mit komplett eigener Render-Pipeline

**Elefront:**
- Grasshopper-basiertes Block-Management
- "Push Definitions" Workflow: Grasshopper erzeugt/aktualisiert Block-Definitionen in Rhino
- Kein per-Instance Visibility — arbeitet auf Definition-Ebene
- Nutzt `InstanceDefinitionTable.Add()` und `ModifyGeometry()`

**Human Plugin:**
- Ähnlich wie Elefront, Grasshopper-fokussiert
- Block-Instanzen via Grasshopper platzieren/modifizieren
- Kein per-Instance Visibility Feature

### 3.5 Custom RhinoObject / CustomMeshProvider

Theoretisch könnte man ein komplett eigenes Custom Object erstellen:
- Erbt von `RhinoObject`
- Implementiert eigenes Rendering
- Kann per-Instance State haben

**Problem:** Extrem aufwändig, schlecht dokumentiert, fragil. VisualARQ macht das mit einem Team und Jahren Entwicklung.

### 3.6 SubObject Materials / Display Attributes

`RhinoObject.HasSubobjectMaterials` existiert, aber:
- Bezieht sich auf SubD/Mesh-Faces, nicht auf Block-Komponenten
- Nicht nutzbar für Visibility-Control

---

## 4. Layer-basierter Ansatz (Detail)

### Layer-Verhalten in Blöcken

- Objekte in einer Block-Definition behalten ihre Layer-Zuordnung
- Wenn Layer X ausgeschaltet → alle Objekte auf Layer X in ALLEN Instanzen dieser Definition unsichtbar
- Die Instanz selbst hat auch einen Layer — aber der beeinflusst NUR die Instanz als Ganzes, nicht die Inhalte

### Per-Instance via Layer?

**Nicht möglich** ohne Definition-Cloning:
- Layer-Visibility ist global
- Man müsste für jede Instanz-Variante separate Layer erstellen UND separate Definitionen

### Layer als Identifier

Layer KÖNNEN nützlich sein um Komponenten zu **identifizieren** (Layer-Name als Komponenten-Name), auch wenn Layer nicht für per-Instance Visibility taugen.

---

## 5. RhinoCommon vs C++ SDK

### Was hat C++ was RhinoCommon nicht hat?

**InstanceDefinition-Bereich:**
- `CRhinoInstanceDefinition::GetInstanceDefinition()` vs `InstanceDefinition` — äquivalent
- `CRhinoInstanceDefinitionTable::ModifyInstanceDefinitionGeometry()` — gleich wie `ModifyGeometry()`
- `ON_InstanceDefinition` (OpenNURBS) — die Basis-Klasse, in RhinoCommon als `InstanceDefinitionGeometry` exponiert

**Relevante C++ exklusive Features:**
- `CRhinoDoc::m_instance_definition_table` — direkter Table-Zugriff, aber `doc.InstanceDefinitions` in RhinoCommon ist equivalent
- `CRhinoInstanceObject::GetPieceList()` — gibt die "aufgelösten" Teile einer Instanz zurück (Geometrie + Transforms). In RhinoCommon als `InstanceObject.GetSubObjects()` verfügbar
- `CRhinoDisplayPipeline` hat etwas tieferen Zugriff auf Draw-Calls, aber auch dort kein per-Component Hook in Blöcken

**Fazit:** Für die Definition-Cloning Strategie gibt es **keinen relevanten Vorteil** des C++ SDK gegenüber RhinoCommon. Alle nötigen APIs sind in RhinoCommon verfügbar.

### Undokumentierte APIs?

- `RhinoObject.m_IsInstanceDefinitionObject` — internes Flag, nicht public
- `InstanceDefinitionTable` hat interne Caches die bei `ModifyGeometry()` automatisch invalidiert werden
- Keine bekannten "geheimen" APIs für per-Instance Block Overrides

---

## 6. SolidWorks Vergleich

### SolidWorks "Suppress Component"

In SolidWorks:
- Ein Assembly ist eine Datei die Referenzen auf Part-Dateien enthält
- Jede Part-Referenz hat einen "Suppressed" State (resolved/suppressed/lightweight)
- **Configurations:** Ein Assembly kann mehrere Configurations haben, jede mit eigenem Suppress-State pro Komponente
- Technisch: Die Configuration speichert einen State-Vektor (welche Komponenten aktiv sind)
- Die 3D-Engine rendert nur die "resolved" Komponenten

### Mapping auf Rhino

| SolidWorks | Rhino Equivalent |
|---|---|
| Assembly File | InstanceDefinition (Top-Level) |
| Part Reference | Objekt innerhalb der Definition |
| Instance | InstanceObject |
| Configuration | **Varianten-Definition** (geklonte Definition mit Subset der Objekte) |
| Suppress Component | Objekt nicht in Varianten-Definition aufnehmen |

### Kann Rhino das nachbilden?

**Ja, via Definition-Cloning!**

Die "Configuration" in SolidWorks entspricht einer Varianten-Definition in Rhino. Statt eines State-Vektors erstellt man eine neue Definition die nur die sichtbaren Komponenten enthält.

**Limitation:** In SolidWorks sind Configurations "billig" (nur ein Bit-Vektor). In Rhino muss die Geometrie dupliziert werden → teurer, aber machbar.

---

## 7. UI Research (Bonus)

### Eto.Forms TreeGridView/GridView Column Resize

**Bekannte Probleme:**
- `GridColumn.Width` funktioniert, aber Min/Max Constraints sind buggy
- `GridColumn.AutoSize` kann Layout-Glitches verursachen
- Auf macOS anders als auf Windows

**Best Practices:**
- Feste `Width` setzen, `AutoSize = false`
- `MinWidth` über manuelles Event-Handling erzwingen:
  ```csharp
  gridView.ColumnHeaderClick += (s, e) => {
      if (e.Column.Width < MinColumnWidth)
          e.Column.Width = MinColumnWidth;
  };
  ```
- Für TreeGridView: `Columns` Collection sortieren, erste Spalte (Tree-Column) nie zu schmal
- `SizeChanged` Event der Form nutzen um Spaltenbreiten proportional anzupassen

### Bessere Column-Config Patterns

```csharp
public class ColumnConfig
{
    public string Id { get; set; }
    public string Header { get; set; }
    public int MinWidth { get; set; } = 50;
    public int DefaultWidth { get; set; } = 100;
    public bool Resizable { get; set; } = true;
    public bool Visible { get; set; } = true;
}
```

State in `doc.Strings` persistieren (DocumentUserStrings).

---

## Zusammenfassung der Findings

1. **DisplayConduit ist eine Sackgasse** — kann nicht auf Block-Komponenten zugreifen
2. **Definition-Cloning ist der richtige Weg** — erstelle Varianten-Definitionen mit Subset der Objekte
3. **`Objects.Replace()` mit neuer `InstanceReferenceGeometry`** ist die Schlüssel-Operation um Instanzen umzuhängen
4. **Deduplizierung ist kritisch** — gleiche Visibility-States sollten eine gemeinsame Definition teilen
5. **Layer-basiert ist nur global** — nicht für per-Instance nutzbar
6. **RhinoCommon reicht** — kein C++ SDK nötig
7. **Undo ist "gratis"** — Rhino trackt `Add()` und `Replace()` automatisch
