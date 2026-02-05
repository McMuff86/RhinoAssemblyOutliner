# Per-Instance Component Visibility

## Das Problem

Rhino-Blöcke (InstanceReferences) sind "all or nothing":
- Alle Instanzen teilen dieselbe Definition
- Wenn ein Block 5 Komponenten hat, zeigen ALLE Instanzen alle 5
- Es gibt **keine native API** um Komponenten pro Instanz auszublenden

**Use Case:** Schiebetür-Block mit Griff → bei einer Instanz soll der Griff unsichtbar sein.

## Unser Ansatz: DisplayConduit

### Strategie v1 (gescheitert)
```
1. Instanz mit Hidden Components → Hide() aufrufen
2. DisplayConduit zeichnet nur sichtbare Komponenten
```
**Problem:** Versteckte Objekte sind nicht selektierbar! Rhino's Selection System kennt die Conduit-Geometrie nicht.

### Strategie v2 (funktioniert)
```
1. Instanz bleibt SICHTBAR und SELEKTIERBAR
2. PreDrawObject Channel abfangen
3. e.DrawObject = false → Rhino zeichnet nicht
4. Wir zeichnen nur die sichtbaren Komponenten
```

## Implementation

### Klassen

1. **ComponentVisibilityData** (UserData)
   - Speichert welche Komponenten hidden sind (Set von Indices)
   - Persistiert mit dem Dokument (Read/Write override)

2. **PerInstanceVisibilityConduit** (DisplayConduit)
   - Override `PreDrawObject()` für managed instances
   - `e.DrawObject = false` unterdrückt default rendering
   - Zeichnet Komponenten einzeln mit `DrawBrep()`, `DrawMesh()`, etc.

3. **PerInstanceVisibilityService** (API)
   - `HideComponent(instanceId, componentIndex)`
   - `ShowComponent(instanceId, componentIndex)`
   - `ToggleComponent(instanceId, componentIndex)`
   - `GetComponentInfos(instanceId)` für UI

### Test Command
```
TestPerInstanceVisibility
```
Wähle Block-Instanz → zeigt Komponenten → Index eingeben zum Togglen.

## Learnings

### API Gotchas
- `DrawObjectEventArgs.RhinoObject` nicht `.Object`
- `InstanceDefinition.GetObjects()` gibt Definition-Objekte, nicht Instanz-Objekte
- Transform: `InstanceObject.InstanceXform` für World-Space

### Display Modes
Müssen verschiedene Modes handeln:
- Wireframe: `DrawBrepWires()`, `DrawMeshWires()`
- Shaded: `DrawBrepShaded()`, `DrawMeshShaded()`

### Nested Blocks
Bei verschachtelten Blöcken: Transforms multiplizieren!
```csharp
var combinedXform = nestedRef.Xform * parentXform;
```

### Performance Considerations
- `PreDrawObject` wird für JEDES Objekt aufgerufen
- HashSet für schnellen managed-check
- `_drawnThisFrame` verhindert Doppel-Rendering

## Known Limitations

1. **Material/Color** - Vereinfachte Logik, nicht 100% akkurat
2. **Selection Highlight** - Noch nicht implementiert
3. **Performance** - Bei vielen managed instances evtl. C++ nötig

## Nächste Schritte

- [ ] Integration in Outliner UI (Toggle per Zeile)
- [ ] Material korrekt übernehmen
- [ ] Selection Highlight für managed instances
- [ ] Performance Testing mit 100+ managed instances
- [ ] C++ Fallback evaluieren falls nötig

## Referenzen

- [DisplayConduit Guide](https://developer.rhino3d.com/guides/rhinocommon/display-conduits/)
- [DrawObjectEventArgs](https://mcneel.github.io/rhinocommon-api-docs/api/RhinoCommon/html/T_Rhino_Display_DrawObjectEventArgs.htm)
- [McNeel Forum: Per Block Instance Wishes](https://discourse.mcneel.com/t/per-block-instance-wishes/185033)
