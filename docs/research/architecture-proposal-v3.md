# Architecture Proposal v3: Definition-Cloning für Per-Instance Visibility

**Datum:** 2026-02-15  
**Status:** PROPOSAL  
**Ersetzt:** DisplayConduit-basierter Ansatz (v1/v2)

---

## Kernidee

Statt zur Render-Zeit Objekte auszublenden (DisplayConduit), erstellen wir **Varianten-Definitionen** — geklonte Block-Definitionen mit einem Subset der Original-Objekte. Jede Instanz wird auf die passende Variante umgehängt.

```
Original-Definition "Motor_v1"
├── Gehaeuse (Brep)
├── Welle (Brep)  
├── Lager_L (nested Block)
└── Lager_R (nested Block)

Variante "Motor_v1__var_noWelle"    ← Welle ausgeblendet
├── Gehaeuse (Brep)
├── Lager_L (nested Block)
└── Lager_R (nested Block)

Instanz_A → zeigt auf "Motor_v1"              (alles sichtbar)
Instanz_B → zeigt auf "Motor_v1__var_noWelle"  (Welle hidden)
```

---

## Architektur-Übersicht

```
┌─────────────────────────────────────────────┐
│              AssemblyOutliner                │
│                                             │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐ │
│  │ UI Panel │  │ Variant  │  │ State     │ │
│  │ (Eto)    │←→│ Manager  │←→│ Persister │ │
│  └──────────┘  └──────────┘  └───────────┘ │
│                     │                       │
│              ┌──────┴──────┐                │
│              │ Definition  │                │
│              │ Cache       │                │
│              └─────────────┘                │
└─────────────────────────────────────────────┘
         │
    Rhino Document
    ├── InstanceDefinitionTable (Definitions + Varianten)
    └── ObjectTable (Instanzen → zeigen auf Definitionen)
```

---

## Komponenten

### 1. VariantManager (Kern-Logik)

Verantwortlich für das Erstellen, Verwalten und Zuweisen von Varianten-Definitionen.

```csharp
public class VariantManager
{
    private readonly RhinoDoc _doc;
    private readonly DefinitionCache _cache;
    
    /// <summary>
    /// Setzt die Sichtbarkeit einer Komponente für eine spezifische Instanz.
    /// </summary>
    public void SetComponentVisibility(
        Guid instanceId,           // Die Block-Instanz
        Guid componentObjectId,    // Das Objekt innerhalb der Original-Definition
        bool visible)
    {
        // 1. Aktuellen Visibility-State der Instanz ermitteln
        // 2. Neuen State berechnen
        // 3. Passende Varianten-Definition finden oder erstellen
        // 4. Instanz auf Variante umhängen
    }
    
    /// <summary>
    /// Erstellt eine Varianten-Definition oder gibt eine gecachte zurück.
    /// </summary>
    private int GetOrCreateVariant(
        int sourceDefIndex,
        VisibilityState state)     // Set<Guid> der sichtbaren Objekte
    {
        // Lookup im Cache: gibt es schon eine Variante mit diesem State?
        var cacheKey = new VariantKey(sourceDefIndex, state);
        if (_cache.TryGet(cacheKey, out int variantIndex))
            return variantIndex;
        
        // Neue Variante erstellen
        var sourceDef = _doc.InstanceDefinitions[sourceDefIndex];
        var sourceObjects = sourceDef.GetObjects();
        
        var geometries = new List<GeometryBase>();
        var attributes = new List<ObjectAttributes>();
        
        foreach (var obj in sourceObjects)
        {
            if (state.IsVisible(obj.Id))
            {
                geometries.Add(obj.Geometry.Duplicate());
                attributes.Add(obj.Attributes.Duplicate());
            }
        }
        
        string variantName = GenerateVariantName(sourceDef.Name, state);
        int variantIndex = _doc.InstanceDefinitions.Add(
            variantName,
            $"Variant of {sourceDef.Name}",
            sourceDef.BasePoint,
            geometries,
            attributes
        );
        
        _cache.Add(cacheKey, variantIndex);
        return variantIndex;
    }
    
    /// <summary>
    /// Hängt eine Instanz auf eine andere Definition um.
    /// </summary>
    private void ReassignInstance(Guid instanceId, int newDefIndex)
    {
        var instance = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instance == null) return;
        
        var newDef = _doc.InstanceDefinitions[newDefIndex];
        var newGeom = new InstanceReferenceGeometry(
            newDef.Id,
            instance.InstanceXform  // Transform beibehalten!
        );
        
        _doc.Objects.Replace(instanceId, newGeom);
    }
}
```

### 2. VisibilityState (Immutable Value Object)

```csharp
/// <summary>
/// Beschreibt welche Komponenten einer Definition sichtbar sind.
/// Immutable für Cache-Keys.
/// </summary>
public class VisibilityState : IEquatable<VisibilityState>
{
    // Set der HIDDEN Object-IDs (sparse: nur versteckte speichern)
    private readonly ImmutableHashSet<Guid> _hiddenIds;
    
    public bool IsVisible(Guid objectId) => !_hiddenIds.Contains(objectId);
    
    public VisibilityState Hide(Guid objectId) => 
        new VisibilityState(_hiddenIds.Add(objectId));
    
    public VisibilityState Show(Guid objectId) => 
        new VisibilityState(_hiddenIds.Remove(objectId));
    
    // Equality für Cache-Lookup
    public override int GetHashCode() => /* hash of sorted hidden IDs */;
    public bool Equals(VisibilityState other) => 
        _hiddenIds.SetEquals(other._hiddenIds);
        
    /// <summary>Alle sichtbar = Original-Definition, keine Variante nötig</summary>
    public bool IsDefault => _hiddenIds.Count == 0;
}
```

### 3. DefinitionCache (Varianten-Deduplizierung)

```csharp
public class DefinitionCache
{
    // Key: (SourceDefIndex, VisibilityState) → VariantDefIndex
    private readonly Dictionary<VariantKey, int> _variants = new();
    
    // Reverse: InstanceId → (SourceDefIndex, VisibilityState)
    private readonly Dictionary<Guid, (int SourceDef, VisibilityState State)> _instanceStates = new();
    
    /// <summary>
    /// Findet alle Instanzen mit dem gleichen State → können Variante teilen.
    /// </summary>
    public bool TryGet(VariantKey key, out int variantIndex);
    
    /// <summary>
    /// Räumt nicht mehr referenzierte Varianten-Definitionen auf.
    /// </summary>
    public void GarbageCollect()
    {
        foreach (var (key, defIndex) in _variants.ToList())
        {
            var def = _doc.InstanceDefinitions[defIndex];
            if (def.GetReferences(0).Length == 0)
            {
                _doc.InstanceDefinitions.Delete(defIndex, true, true);
                _variants.Remove(key);
            }
        }
    }
}
```

### 4. StatePersister (Persistierung)

State wird in **DocumentUserStrings** gespeichert, damit er im .3dm File überlebt.

```csharp
public class StatePersister
{
    private const string KEY_PREFIX = "AssemblyOutliner::";
    
    /// <summary>
    /// Speichert den Visibility-State einer Instanz.
    /// Format: "AssemblyOutliner::vis::{instanceId}" → "{hiddenId1},{hiddenId2},..."
    /// </summary>
    public void Save(Guid instanceId, VisibilityState state)
    {
        string key = $"{KEY_PREFIX}vis::{instanceId}";
        string value = string.Join(",", state.HiddenIds.Select(g => g.ToString("N")));
        _doc.Strings.SetString(key, value);
    }
    
    /// <summary>
    /// Beim Plugin-Load: Rekonstruiert alle Varianten aus gespeichertem State.
    /// </summary>
    public void RestoreAll(VariantManager manager)
    {
        // Iteriere über alle gespeicherten States
        // Erstelle Varianten-Definitionen
        // Hänge Instanzen um
    }
    
    /// <summary>
    /// Speichert auch die Mapping-Tabelle: OriginalDef → [ComponentId → ComponentName]
    /// Damit können wir nach einem BlockEdit die Komponenten wieder zuordnen.
    /// </summary>
    public void SaveComponentMap(int sourceDefIndex, Dictionary<Guid, string> componentMap);
}
```

**Alternative: UserData auf InstanceObject**
```csharp
// UserData direkt am Objekt — reist mit dem Objekt mit bei Copy/Paste
public class AssemblyVisibilityData : UserData
{
    public Guid OriginalDefinitionId { get; set; }
    public List<Guid> HiddenComponentIds { get; set; }
    
    // UserData wird automatisch serialisiert/deserialisiert
}
```

**Empfehlung:** UserData auf dem InstanceObject ist besser als DocumentUserStrings, weil:
- Reist mit dem Objekt bei Copy/Paste
- Wird automatisch serialisiert
- Keine Orphan-Einträge bei gelöschten Instanzen

---

## Workflow: Komponente ausblenden

```
User klickt "Eye" Icon bei "Welle" für Instanz_B
    │
    ▼
1. VariantManager.SetComponentVisibility(Instanz_B.Id, Welle.Id, false)
    │
    ▼
2. Berechne neuen VisibilityState für Instanz_B
   state = currentState.Hide(Welle.Id)
    │
    ▼
3. state.IsDefault? 
   ├── JA → Instanz zurück auf Original-Definition umhängen
   └── NEIN → weiter
    │
    ▼
4. Cache-Lookup: Gibt es schon eine Variante mit diesem State?
   ├── JA → variantDefIndex aus Cache
   └── NEIN → Neue Variante erstellen (Clone ohne Welle)
    │
    ▼
5. ReassignInstance(Instanz_B.Id, variantDefIndex)
   = doc.Objects.Replace(id, new InstanceReferenceGeometry(newDefId, xform))
    │
    ▼
6. State persistieren (UserData auf Instanz)
    │
    ▼
7. UI aktualisieren (Redraw)
```

---

## Kritische Szenarien

### BlockEdit (User ändert Original-Definition)

**Problem:** User öffnet BlockEdit auf der Original-Definition und ändert Geometrie. Alle Varianten-Definitionen sind jetzt out-of-sync.

**Lösung:**
1. Event-Handler auf `InstanceDefinitionTable.InstanceDefinitionModified` 
2. Wenn die Source-Definition geändert wird:
   a. Alle Varianten dieser Source-Definition finden (via Cache)
   b. Für jede Variante: Neu erstellen basierend auf der geänderten Source + gespeichertem VisibilityState
   c. Instanzen auf neue Varianten umhängen
   d. Alte Varianten löschen

```csharp
doc.InstanceDefinitions.InstanceDefinitionModified += (sender, e) =>
{
    if (IsSourceDefinition(e.InstanceDefinitionIndex))
    {
        RefreshAllVariants(e.InstanceDefinitionIndex);
    }
};
```

### Nested Block Visibility

**Szenario:** User will Komponente innerhalb eines nested Blocks ausblenden.

**Ansatz (2 Ebenen):**
1. Innere Definition klonen (mit ausgeblendeter Komponente)
2. Äussere Definition klonen (mit Referenz auf die innere Variante)
3. Instanz auf äussere Variante umhängen

**Komplexität:** Exponentiell bei tiefer Verschachtelung! 

**Pragmatische Lösung:** Nur 1 Ebene tief unterstützen (Top-Level Komponenten). Nested Block Visibility als "V2 Feature" markieren.

### Copy/Paste einer Instanz

- UserData reist mit → nach Paste kennt die Kopie ihren Visibility-State
- Beim nächsten `RestoreAll()` oder sofort via Event wird die Variante zugewiesen
- Die Varianten-Definition existiert bereits (gecached) → kein Extra-Aufwand

### Undo

- `doc.Objects.Replace()` wird von Rhino automatisch ins Undo-System aufgenommen
- Bei Undo: Instanz zeigt wieder auf vorherige Definition
- **Problem:** Die Varianten-Definition bleibt als "Waise" zurück
- **Lösung:** GarbageCollect nach Undo-Events, oder: Varianten nicht sofort löschen (Undo-Fenster abwarten)

### Performance bei grossen Assemblies

| Szenario | Definitions | Instanzen | Varianten (worst) | Varianten (dedupliziert) |
|---|---|---|---|---|
| Klein (10 Parts) | 10 | 50 | 50 | ~5-10 |
| Mittel (50 Parts) | 50 | 200 | 200 | ~20-50 |
| Gross (200 Parts) | 200 | 1000 | 1000 | ~50-200 |

**Optimierungen:**
1. **Lazy Creation:** Variante erst erstellen wenn tatsächlich eine Komponente hidden wird
2. **Dedup:** Identische States teilen eine Variante
3. **Batch Operations:** Bei "Hide in all instances" nur EINE Variante erstellen
4. **Garbage Collection:** Nicht-referenzierte Varianten periodisch aufräumen

---

## State-Persistierung: Empfehlung

### UserData auf InstanceObject (Primär)

```csharp
[Serializable]
public class AssemblyOutlinerUserData : UserData
{
    // Die ORIGINAL-Definition (bevor Variante zugewiesen wurde)
    public Guid SourceDefinitionId { get; set; }
    
    // Set der hidden Component Object-IDs (relativ zur Source-Definition)
    public List<Guid> HiddenComponentIds { get; set; } = new();
    
    // Serialisierung
    protected override bool Read(BinaryArchiveReader archive) { ... }
    protected override bool Write(BinaryArchiveWriter archive) { ... }
}
```

### DocumentUserStrings (Sekundär, für globale Config)

```
"AssemblyOutliner::version" → "3.0"
"AssemblyOutliner::config::autoRefreshOnBlockEdit" → "true"
```

### Varianten-Naming Convention

```
"{OriginalName}__aov_{hash8}"
```
- `__aov_` = Assembly Outliner Variant
- `{hash8}` = 8-Zeichen Hash des VisibilityState
- Beispiel: `Motor_v1__aov_3f8a2b1c`

Varianten-Definitionen werden im Block Manager angezeigt, daher sinnvolle Namen.

---

## API Design (Public Interface)

```csharp
public interface IAssemblyOutliner
{
    /// <summary>Setzt Visibility einer Komponente für eine Instanz.</summary>
    void SetComponentVisibility(Guid instanceId, Guid componentId, bool visible);
    
    /// <summary>Setzt Visibility einer Komponente für ALLE Instanzen einer Definition.</summary>
    void SetComponentVisibilityGlobal(int sourceDefIndex, Guid componentId, bool visible);
    
    /// <summary>Gibt den aktuellen Visibility-State einer Instanz zurück.</summary>
    VisibilityState GetVisibilityState(Guid instanceId);
    
    /// <summary>Setzt Instanz zurück auf Original-Definition (alles sichtbar).</summary>
    void ResetInstance(Guid instanceId);
    
    /// <summary>Setzt alle Instanzen zurück.</summary>
    void ResetAll();
    
    /// <summary>Gibt die Komponenten einer Definition zurück (mit aktuellem Visibility-Status).</summary>
    IReadOnlyList<ComponentInfo> GetComponents(Guid instanceId);
}

public class ComponentInfo
{
    public Guid ObjectId { get; set; }
    public string Name { get; set; }        // Layer-Name oder UserString
    public ObjectType GeometryType { get; set; }
    public bool IsVisible { get; set; }
    public bool IsNestedBlock { get; set; }
}
```

---

## Migration von v2 (DisplayConduit)

1. DisplayConduit komplett entfernen
2. Alle bestehenden Visibility-States migrieren (falls persistiert)
3. VariantManager initialisieren
4. Bestehende Instanzen scannen → UserData hinzufügen wo nötig

---

## Risiken und Mitigationen

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|---|---|---|
| BlockEdit invalidiert Varianten | Hoch | Hoch | Event-Handler, automatische Refresh |
| Memory bei vielen Varianten | Mittel | Mittel | Deduplizierung, Lazy Creation, GC |
| Undo-Inkonsistenz | Mittel | Mittel | Undo-Records, GC-Delay |
| Nested Blocks Komplexität | Hoch | Mittel | V1: nur Top-Level, V2: nested |
| Block Manager zeigt Varianten | Sicher | Niedrig | Naming Convention, evtl. Filter-UI |
| Performance bei Batch-Ops | Niedrig | Hoch | Batch API, single undo record |

---

## Implementierungs-Roadmap

### Phase 1: Core (MVP)
- [ ] VariantManager mit SetComponentVisibility
- [ ] DefinitionCache mit Deduplizierung  
- [ ] UserData Persistierung
- [ ] Basic UI (Toggle Visibility im TreeView)
- [ ] Undo support (automatisch via Rhino)

### Phase 2: Robustness
- [ ] BlockEdit Event-Handler
- [ ] Garbage Collection für Waisen-Varianten
- [ ] Copy/Paste Support
- [ ] Batch Operations (Hide in all instances)

### Phase 3: Advanced
- [ ] Nested Block Visibility (1 Ebene)
- [ ] "Configurations" (benannte Visibility-Presets)
- [ ] Import/Export von Configurations
- [ ] Performance-Optimierung für grosse Assemblies

---

## Fazit

**Definition-Cloning ist der einzig gangbare Weg für per-Instance Component Visibility in Rhino.** 

Die Architektur ist:
- **Einfach:** Basiert auf Standard-APIs (`Add`, `Replace`, `ModifyGeometry`)
- **Robust:** Undo kommt gratis, Persistierung via UserData
- **Skalierbar:** Deduplizierung hält die Varianten-Anzahl klein
- **Wartbar:** Kein C++ nötig, reines RhinoCommon

Der DisplayConduit war der falsche Ansatz — er kämpft gegen Rhinos Rendering-Architektur. Definition-Cloning arbeitet MIT dem System, nicht dagegen.
