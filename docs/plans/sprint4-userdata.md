# Sprint 4 — `ON_AssemblyUserData` Persistence

**Status:** Plan
**Vorgänger:** Sprint 3 (Definition Cloning, live grün in rc.9)
**Architektur-Referenz:** `docs/architecture/assembly-object-architecture.md`
**Recherche-Anhang:** `docs/research/sprint4-userdata-research.md` (wird vom Explore-Agent geliefert)

---

## Problem

Sprint 3 macht Per-Instance-Visibility funktionieren, aber **alles lebt nur im RAM**. Konkret:

1. **Save/Load bricht.** Beim Reload des `.3dm` ist der `VariantManager._cache` leer. Der Outliner sieht eine Instanz auf einer `__aov_…`-Definition und kann den `VisibilityState` nicht mehr ableiten — die Components werden alle als sichtbar dargestellt obwohl die Variant-Geometrie weniger zeigt.
2. **Object-Properties-Type-Feld zeigt den Variant-Namen.** Rhinos natives `Type:` Feld liest `instance.InstanceDefinition.Name` und zeigt `"__aov_MBlock_<hash>" : block instance`. Unser `Attributes.Name`-Mask greift nur das `Name:`-Feld. Die echte Lösung ist: Source-Name in UserData speichern und im Tree-Builder + Detail-Panel daraus ableiten — so dass Rhinos native UI-Spalten umgangen werden.
3. **Copy/Paste verliert Kontext.** Eine kopierte Instanz behält ihre InstanceDefinition-Referenz aber keine Information darüber, dass sie eine Assembly ist.
4. **Cross-Document-Drop** (z.B. Worksession, drag aus anderem `.3dm`) hat keine Möglichkeit, die Source-Def im Ziel-Dokument wiederzufinden.

---

## Ziel

Persistente Assembly-Metadaten pro `InstanceObject` via `ON_UserData` in C++. Round-trip-safe durch `.3dm` Save/Load. Funktioniert auch wenn das Plugin **nicht** geladen ist (Rhino behält unbekannte UserData stumm).

---

## Schema — was persistiert wird

Pro `InstanceObject` mit aktiver Variant:

```
ON_AssemblyUserData
├── m_classVersion           : int                   // 1, für forward/backward compat
├── m_sourceDefinitionId     : ON_UUID               // Original-Definition (vor Cloning)
├── m_sourceDefinitionName   : ON_wString            // Fallback für cross-doc Lookup
├── m_hiddenComponentIndices : ON_SimpleArray<int>   // serialisierter VisibilityState
└── m_componentCount         : int                   // Total-Count zur Validierung
```

Configurations (Sprint 6) kommen später, sind hier noch *nicht* drin — minimal halten.

---

## Tasks

> Reihenfolge ist sequenziell. Jeder Schritt baut auf dem vorigen auf. Keine Parallelisierung sinnvoll außer der Recherche, die bereits läuft.

### 4.1 — C++ `ON_AssemblyUserData` Klasse (≈ 8h)

**Datei:** `src/RhinoAssemblyOutliner.native/AssemblyUserData.h` + `.cpp`

- `ON_OBJECT_DECLARE(ON_AssemblyUserData)` im Header
- `ON_OBJECT_IMPLEMENT(ON_AssemblyUserData, ON_UserData, "<NEW-CLASS-GUID>")` in `.cpp`
- Plugin-stamped: `m_application_uuid = RhinoAssemblyOutlinerPlugin.GUID`
- `m_userdata_uuid` = eine zweite, neue GUID (UserData-Identifier, distinct from class-id)
- Versionierter `Write(ON_BinaryArchive&)` mit `BeginWrite3dmChunk` + `EndWrite3dmChunk`
- `Read(ON_BinaryArchive&)` symmetrisch
- `Archive() = true`
- `GetDescription(ON_wString&)` → `"Assembly Outliner Data"`
- `Transform(const ON_Xform&) = true` (UserData reist mit Move/Copy)

**Test (manuell):** noch keine, kommt in 4.4.

**Referenz:** existierender `VisibilityUserData.cpp` als Blaupause (laut Recherche-Agent — der prüft auch ob er funktional oder Stub ist).

### 4.2 — P/Invoke Bridge erweitern (≈ 6h)

**Datei:** `src/RhinoAssemblyOutliner.native/NativeApi.h` + `.cpp`

Neue exports:

```c
NATIVE_API bool   __stdcall AttachAssemblyData(const ON_UUID* instanceId, const ON_UUID* sourceDefId, const wchar_t* sourceDefName, const int* hiddenIndices, int hiddenCount, int componentCount);
NATIVE_API bool   __stdcall HasAssemblyData(const ON_UUID* instanceId);
NATIVE_API bool   __stdcall RemoveAssemblyData(const ON_UUID* instanceId);
NATIVE_API bool   __stdcall GetSourceDefinitionId(const ON_UUID* instanceId, ON_UUID* outSourceDefId);
NATIVE_API int    __stdcall GetSourceDefinitionName(const ON_UUID* instanceId, wchar_t* buffer, int bufferSize);
NATIVE_API int    __stdcall GetHiddenComponentIndices(const ON_UUID* instanceId, int* buffer, int maxCount);
NATIVE_API int    __stdcall GetComponentCount(const ON_UUID* instanceId);
```

Jede exported function: SEH wrapper + null-checks + return-bool/int convention konsistent mit dem bestehenden `NativeApi`-Stil.

### 4.3 — C# Wrapper (≈ 4h)

**Datei:** `src/RhinoAssemblyOutliner/Services/PerInstanceVisibility/NativeVisibilityInterop.cs` (oder neue `NativeAssemblyDataInterop.cs` falls sauberer)

`[DllImport]`-Bindings für die 7 neuen Functions. Marshalling:

- `Guid` → `ref Guid` (ON_UUID*)
- `string` → `[MarshalAs(UnmanagedType.LPWStr)] string`
- `int[]` → `[MarshalAs(UnmanagedType.LPArray)] int[]`
- Buffer-Pattern für Output-Strings: `StringBuilder` mit `Capacity`, return-int = written-length

Dünner Wrapper-Layer `AssemblyDataStore` darüber, damit der Rest von C# nicht direkt mit `ref Guid` und Buffern hantiert:

```csharp
public sealed class AssemblyDataStore
{
    public bool Has(Guid instanceId);
    public void Attach(Guid instanceId, Guid sourceDefId, string sourceDefName, VisibilityState state);
    public void Remove(Guid instanceId);
    public Guid? GetSourceDefinitionId(Guid instanceId);
    public string? GetSourceDefinitionName(Guid instanceId);
    public VisibilityState? GetVisibilityState(Guid instanceId);
}
```

### 4.4 — `VariantManager` integriert UserData (≈ 6h)

`ReassignInstance` schreibt nach jedem Replace die UserData:

```csharp
if (state.IsAllVisible)
    _dataStore.Remove(instanceId);     // back to source → no metadata needed
else
    _dataStore.Attach(instanceId, sourceDefId, sourceDef.Name, state);
```

`GetVariantState(variantId)` bleibt für den In-Memory-Fastpath. Aber NEU: ein zweiter Lookup-Pfad pro `instanceId`, der die UserData liest (für post-Reload):

```csharp
public VisibilityState? GetVisibilityStateForInstance(Guid instanceId)
{
    return _dataStore.GetVisibilityState(instanceId);
}
```

`AssemblyTreeBuilder.ResolveHiddenIndicesForInstance` ruft jetzt **zuerst** den DataStore (authoritativ), fällt erst dann auf den InMemory-`_variantStates` zurück.

### 4.5 — Restore-Workflow auf `OnEndOpenDocument` (≈ 6h)

In `RhinoAssemblyOutlinerPlugin.OnEndOpenDocument`:

1. Iteriere alle `InstanceObject`s im neu geladenen Doc.
2. Für jedes Object: `_dataStore.Has(id)` ?
3. Falls ja:
   - Lies `sourceDefId`, `sourceDefName`, `VisibilityState` aus der UserData.
   - Versuche `doc.InstanceDefinitions.FindId(sourceDefId)`. Falls nicht da: `Find(sourceDefName)` als Fallback (cross-doc copy/paste).
   - Falls Source gefunden: `_variantManager.ReassignInstance(...)` — re-creates the variant if needed and rehängt die Instanz.
   - Falls Source nicht gefunden: `_dataStore.Remove(id)` + log warning. Instance bleibt auf der „abgeschnittenen" Variant.
4. Schedule GC.

**Wichtig:** während Restore werden Object-Replace-Events gefeuert — Event-Handler temporär unsubscriben, damit nicht jeder Restore eine GC-Schleife auslöst.

### 4.6 — Properties-Type-Feld fixen (≈ 2h)

Mit UserData kennen wir den Source-Namen unabhängig vom Variant-Namen. Der Outliner kann das Type-Feld im DetailPanel jetzt korrekt zeigen. Das Rhino-native Object-Properties-Panel zeigt weiter den Variant-Namen als `Type` (das ist außerhalb unserer Kontrolle ohne `CRhinoObject`-Subclassing). Aber:

- **Unser DetailPanel** zeigt `Type: <SourceName> (variant active)` statt der nativen Anzeige.
- README + UserGuide dokumentiert: „Rhino's native Object Properties Type-Feld zeigt aus technischen Gründen den internen Variant-Namen — das ist normal und wird in Sprint 5 (Custom-Object) gelöst, falls überhaupt nötig."

### 4.7 — Tests (≈ 6h)

**Logic-Tests (Test-Doubles, kein RhinoDoc):**
- `AssemblyDataStore` round-trip: Attach → Get → Verify
- VisibilityState <→ HiddenIndices serialization
- Source-Lookup-Fallback (id miss → name hit)

**Integration-Test in Rhino (manuell, dokumentiert):**
- Setup: 2× MBlock-Instanz, eine mit hidden Component
- Save → Close Rhino → Reopen → Open .3dm
- Verify: Outliner zeigt korrekten Hidden-State, Viewport zeigt korrekte Geometrie
- Verify: Im Block Manager existieren die `__aov_…`-Definitionen weiterhin und sind referenziert

**Graceful-Degradation-Test (manuell):**
- Plugin disablen via `_PluginManager`
- File öffnen → Variant-Geometrie bleibt sichtbar (frozen), UserData bleibt erhalten
- Plugin wieder enablen → Restore funktioniert

### 4.8 — Copy/Paste (≈ 5h)

- Intra-doc Copy: UserData reist via `Transform()` automatisch mit. Test es.
- Cross-doc Copy: UserData bleibt, aber `sourceDefId` zeigt auf nicht-existente Definition im Ziel-Dokument. Restore-Workflow versucht `FindByName` als Fallback. Falls auch das fehlschlägt: `RemoveAssemblyData` + log + Instance bleibt auf was-immer-die-Definition-jetzt-ist.

### 4.9 — `OnDeleteRhinoObject`-Cleanup (≈ 1h)

Wenn ein InstanceObject gelöscht wird, ist das UserData mit drauf weg (Rhino räumt mit). Nichts zu tun, aber verifizieren dass kein Memory-Leak oder Dangling-Reference entsteht.

### 4.10 — Build-System: native DLL kompilieren (≈ 4h)

Der C++ Native-Build wurde in dieser Session nicht angefasst. Vor Sprint 4:
- VS 2022 + Rhino 8 C++ SDK Setup verifizieren
- `RhinoAssemblyOutliner.native` Projekt baut in Release x64
- `build-yak.ps1` zieht die frische `.Native.dll` mit
- Variant-Lifecycle live in Rhino testen

---

## Aufwand-Summary

| Task | Aufwand |
|------|---------|
| 4.1 ON_AssemblyUserData C++ | 8h |
| 4.2 P/Invoke Bridge | 6h |
| 4.3 C# Wrapper | 4h |
| 4.4 VariantManager-Integration | 6h |
| 4.5 OnEndOpenDocument Restore | 6h |
| 4.6 Type-Feld Fix | 2h |
| 4.7 Tests | 6h |
| 4.8 Copy/Paste | 5h |
| 4.9 OnDelete-Cleanup | 1h |
| 4.10 Build-System | 4h |
| **Total** | **~48h** |

Stimmt grob mit dem SPRINT_PLAN-Schätzwert (54h) überein.

---

## Exit Gate

Demo-Ablauf:

1. Rhino auf, neues Doc, 3× MBlock-Instanzen einfügen
2. Auf Instanz #1 zwei Components ausblenden, auf Instanz #2 eine andere
3. Save als `roundtrip.3dm`
4. Rhino zu, neu auf
5. `roundtrip.3dm` öffnen
6. **Erwartet:**
   - Im Viewport sehen die drei Instanzen genau wie vor dem Save aus
   - Outliner zeigt korrekt welche Components hidden sind pro Instanz
   - Object-Properties-DetailPanel zeigt `MBlock` als Type
7. Eine Instanz auf eine andere Instanz copy/pasten → die Kopie hat denselben Hidden-State

Wenn alle Punkte ✅, ist Sprint 4 fertig.

---

## Was Sprint 4 NICHT umfasst

- Configurations (Named States) — Sprint 6
- Custom Grips für Component-Selection — Sprint 5
- BOM Export — Sprint 7
- Nested Block Visibility — Sprint 8+
- Custom Rhino Object Type — explizit verworfen, siehe `architecture-proposal-v3.md` §8.3

---

## Risiken

| Risiko | Gegenmittel |
|--------|-------------|
| `ON_OBJECT_IMPLEMENT`-Macro fängt Class-ID nicht korrekt ein → UserData wird beim Reload nicht erkannt | Recherche-Agent dokumentiert exakt das Pattern; Existenz-Test in 4.7 fängt's früh |
| Replace() löscht UserData beim Variant-Reassign | Recherche-Agent klärt Ownership; Workaround: Re-Attach nach Replace |
| Restore-Workflow feuert eine GC-Kaskade | Event-Handler temporär unsubscriben während Restore |
| Cross-Doc Source-Resolution scheitert | Graceful: UserData entfernen, Instance bleibt auf Variant, Log-Warnung |
| C++-Build bricht durch Header-Inkompatibilität mit Rhino 8 SDK | Vor 4.1: Build-System verifizieren (Task 4.10 nach vorne ziehen) |
