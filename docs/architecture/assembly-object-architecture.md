# Assembly Object Architecture — Custom Object System für Rhino 8

**Datum:** 2026-02-15  
**Status:** DESIGN  
**Autor:** Sentinel (Architecture Subagent)

---

## Executive Summary

Dieses Dokument beschreibt die Architektur eines **Custom Assembly Object Systems** für Rhino 8, das auf `CRhinoObject`-Subclassing (C++) basiert. Es ersetzt den bisherigen DisplayConduit-Ansatz und ergänzt die Definition-Cloning-Strategie aus v3 um ein vollwertiges Object-System mit Configurations, Grips und persistentem State.

### Architektur-Entscheidung: Hybrid-Ansatz

**Entscheidung:** Wir kombinieren Definition-Cloning (bewährt, v3) mit einem leichtgewichtigen Custom Object Wrapper — NICHT volles `CRhinoObject`-Subclassing.

**Begründung:**
- Volles `CRhinoObject`-Subclassing ist extrem riskant (schlecht dokumentiert, Breaking Changes bei Rhino Updates, VisualARQ brauchte Jahre)
- Definition-Cloning funktioniert nachweislich mit Standard-APIs
- Der Custom Object Wrapper steuert nur Metadaten/Configuration, nicht Rendering
- Graceful Degradation: ohne Plugin bleiben normale Blocks erhalten

```
┌─────────────────────────────────────────────────────────────┐
│                    HYBRID ARCHITECTURE                       │
│                                                             │
│  ┌──────────────────┐     ┌──────────────────────────────┐  │
│  │  C++ Native DLL  │     │  C# RhinoCommon Plugin       │  │
│  │                  │     │                              │  │
│  │  • UserData      │◄───►│  • AssemblyManager           │  │
│  │    (persistence) │     │  • ConfigurationService      │  │
│  │  • Event Handler │     │  • VariantManager (v3)       │  │
│  │  • Conduit       │     │  • UI Panel (Eto)            │  │
│  │    (fallback)    │     │  • P/Invoke Bridge           │  │
│  └──────────────────┘     └──────────────────────────────┘  │
│              │                         │                    │
│              └─────────┬───────────────┘                    │
│                        ▼                                    │
│              Rhino Document (.3dm)                           │
│              ├── InstanceDefinitionTable                     │
│              │   ├── "Motor_v1" (original)                  │
│              │   ├── "Motor_v1__aov_3f8a" (variant)         │
│              │   └── ...                                    │
│              ├── ObjectTable                                │
│              │   ├── InstanceObject + ON_UserData            │
│              │   └── ...                                    │
│              └── DocumentUserStrings (global config)        │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. Klassendiagramm

```
┌─────────────────────────────────────────────────────────────────┐
│                        C++ NATIVE LAYER                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ON_AssemblyUserData : ON_UserData                              │
│  ├── m_sourceDefinitionId : ON_UUID                             │
│  ├── m_activeConfigName : ON_wString                            │
│  ├── m_configurations : ON_ClassArray<AssemblyConfig>           │
│  ├── m_hiddenComponentIds : ON_SimpleArray<ON_UUID>             │
│  ├── m_dimensionOverrides : ON_ClassArray<DimensionOverride>    │
│  ├── Read(ON_BinaryArchive&) → bool                            │
│  ├── Write(ON_BinaryArchive&) → bool                           │
│  ├── Archive() → true                                          │
│  └── GetDescription() → "Assembly Outliner Data"               │
│                                                                 │
│  AssemblyConfig (POD struct, inside UserData)                   │
│  ├── name : ON_wString                                          │
│  ├── parentConfigName : ON_wString  (inheritance)               │
│  ├── hiddenComponentIds : ON_SimpleArray<ON_UUID>               │
│  └── dimensionOverrides : ON_ClassArray<DimensionOverride>      │
│                                                                 │
│  DimensionOverride (POD struct)                                 │
│  ├── componentId : ON_UUID                                      │
│  ├── parameterName : ON_wString                                 │
│  └── value : double                                             │
│                                                                 │
│  CVisibilityConduit : CRhinoDisplayConduit  (existing, fallback)│
│  CVisibilityData (existing, thread-safe state)                  │
│  CDocEventHandler : CRhinoEventWatcher (existing, enhanced)     │
│                                                                 │
│  NativeApi.h (P/Invoke bridge — extended)                       │
│  ├── Assembly CRUD                                              │
│  ├── Configuration CRUD                                         │
│  ├── Event callbacks                                            │
│  └── Query functions                                            │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                        C# MANAGED LAYER                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  AssemblyManager (singleton per doc)                            │
│  ├── VariantManager (from v3 — definition cloning)              │
│  ├── ConfigurationService                                       │
│  ├── AssemblyRegistry (tracks all assembly instances)            │
│  └── EventBridge (C++ events → C# handlers)                    │
│                                                                 │
│  VariantManager (from architecture-proposal-v3)                 │
│  ├── SetComponentVisibility(instanceId, componentId, visible)   │
│  ├── GetOrCreateVariant(sourceDefIndex, VisibilityState)        │
│  ├── ReassignInstance(instanceId, newDefIndex)                  │
│  └── DefinitionCache (deduplication)                            │
│                                                                 │
│  ConfigurationService                                           │
│  ├── CreateConfiguration(instanceId, name, parentConfig?)       │
│  ├── ActivateConfiguration(instanceId, configName)              │
│  ├── DeleteConfiguration(instanceId, configName)                │
│  ├── GetConfigurations(instanceId) → List<ConfigInfo>           │
│  └── ApplyConfigToVariant(instanceId, config)                   │
│                                                                 │
│  AssemblyTreeBuilder (existing, enhanced)                        │
│  ├── BuildTree(doc) → tree of AssemblyNode                      │
│  ├── Configurations shown as sub-nodes                          │
│  └── Visibility state reflected in tree                         │
│                                                                 │
│  UI: AssemblyOutlinerPanel (Eto, existing, enhanced)            │
│  ├── AssemblyTreeView                                           │
│  ├── ConfigurationDropdown                                      │
│  └── DetailPanel (properties, overrides)                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Verantwortlichkeiten

| Klasse | Verantwortung |
|--------|---------------|
| `ON_AssemblyUserData` | Persistente Daten auf jedem Assembly-InstanceObject. Überlebt Save/Load, Copy/Paste. Enthält Source-Definition-Referenz, aktive Config, alle Configs. |
| `AssemblyConfig` | Eine benannte Konfiguration: welche Komponenten sichtbar, welche Dimension-Overrides aktiv. |
| `VariantManager` | Erstellt/cached Varianten-Definitionen basierend auf VisibilityState. Kernlogik aus v3. |
| `ConfigurationService` | Verwaltet benannte Configurations. Mapping Config → VisibilityState → Variant. |
| `AssemblyManager` | Orchestrator: koordiniert VariantManager, ConfigService, Events, UI-Updates. |
| `CVisibilityConduit` | Fallback für Fälle wo Definition-Cloning nicht reicht (z.B. Preview während Drag). |
| `CDocEventHandler` | Reagiert auf Doc-Events: BlockEdit, Delete, Undo, Open, Save. |

---

## 2. Datenmodell

### 2.1 Definition vs Instance

```
┌──────────────────────────────────────────────────────────┐
│                  ASSEMBLY DEFINITION                      │
│  (= normale Rhino InstanceDefinition + Konvention)       │
│                                                          │
│  InstanceDefinition "Motor_v1"                           │
│  ├── Gehaeuse (Brep, Id=aaa)                            │
│  ├── Welle (Brep, Id=bbb)                               │
│  ├── Lager_L (nested Block, Id=ccc)                     │
│  └── Lager_R (nested Block, Id=ddd)                     │
│                                                          │
│  Identifizierung: DocumentUserString                     │
│  "RAO::def::{defId}::isAssembly" = "true"               │
│  "RAO::def::{defId}::componentMap" = serialized map      │
│                                                          │
└──────────────────────────────────────────────────────────┘
          │
          │  referenziert von N Instanzen
          ▼
┌──────────────────────────────────────────────────────────┐
│                  ASSEMBLY INSTANCE                        │
│  (= normale Rhino InstanceObject + ON_AssemblyUserData)  │
│                                                          │
│  InstanceObject (in ObjectTable)                         │
│  ├── InstanceXform (Position/Rotation/Scale)             │
│  ├── Attributes (Layer, Color, Name)                     │
│  └── UserData:                                           │
│      └── ON_AssemblyUserData                             │
│          ├── sourceDefinitionId = "Motor_v1".Id          │
│          ├── activeConfigName = "Wartung"                │
│          ├── configurations = [                          │
│          │   { name: "Default", hidden: [] },            │
│          │   { name: "Wartung", hidden: [bbb] },         │
│          │   { name: "Minimal", hidden: [bbb,ccc,ddd] } │
│          │ ]                                             │
│          └── (zeigt auf Variant-Definition wenn aktiv)   │
│                                                          │
│  Aktuell zeigt auf: "Motor_v1__aov_3f8a" (Variant)      │
│  weil Config "Wartung" aktiv → Welle hidden              │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### 2.2 Configurations

Configurations sind **benannte Visibility-Presets** die pro Instance gespeichert werden:

```cpp
// C++ struct (in ON_AssemblyUserData)
struct AssemblyConfig
{
    ON_wString name;                           // "Default", "Wartung", "Explosionsansicht"
    ON_wString parentConfigName;               // Vererbung: "" = kein Parent
    ON_SimpleArray<ON_UUID> hiddenComponentIds; // IDs relativ zur Source-Definition
    // Future: dimension overrides, material overrides, etc.
};
```

**Vererbung:**
```
Base Config: "Default" (alles sichtbar)
    └── Derived: "Wartung" (Welle hidden)
        └── Derived: "Wartung_Detail" (Welle + Gehaeuse hidden)
```

Vererbungslogik: Ein Derived Config **erbt** die Hidden-Liste des Parents und kann zusätzlich Komponenten ein-/ausblenden. Wird der Parent geändert, propagiert es.

### 2.3 Instance-Level vs Definition-Level

| Eigenschaft | Definition-Level | Instance-Level |
|-------------|-----------------|----------------|
| Component-Geometrie | ✅ | ❌ (via Variant-Def) |
| Component-Liste | ✅ | ❌ |
| Component-Namen | ✅ | ❌ |
| Visibility-State | ❌ | ✅ (per Config) |
| Active Configuration | ❌ | ✅ |
| Configuration-Set | ❌ | ✅ |
| Dimension Overrides | ❌ | ✅ (future) |
| Transform | ❌ | ✅ |
| Layer | ❌ | ✅ |

### 2.4 Persistence

**Primär: ON_UserData auf InstanceObject** (C++ Klasse, `Archive()=true`)

```cpp
class ON_AssemblyUserData : public ON_UserData
{
    ON_OBJECT_DECLARE(ON_AssemblyUserData);

public:
    static const ON_UUID Id; // {B1A2C3D4-E5F6-7890-AB12-CD34EF567890}

    ON_AssemblyUserData();

    // ON_UserData overrides
    bool GetDescription(ON_wString& desc) override;
    bool Archive() const override { return true; }
    bool Write(ON_BinaryArchive& archive) const override;
    bool Read(ON_BinaryArchive& archive) override;

    // Transforms with the object (Copy/Paste, Move, etc.)
    bool Transform(const ON_Xform& xform) override { return true; }

    // Data
    ON_UUID m_sourceDefinitionId = ON_nil_uuid;
    ON_wString m_activeConfigName;
    ON_ClassArray<AssemblyConfig> m_configurations;

    // Version for forward/backward compat
    static const int CURRENT_VERSION = 1;
};

// Serialization format:
bool ON_AssemblyUserData::Write(ON_BinaryArchive& archive) const
{
    if (!archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, CURRENT_VERSION, 0))
        return false;

    bool rc = true;
    rc = rc && archive.WriteUuid(m_sourceDefinitionId);
    rc = rc && archive.WriteString(m_activeConfigName);
    rc = rc && archive.WriteInt(m_configurations.Count());
    for (int i = 0; rc && i < m_configurations.Count(); i++)
    {
        rc = rc && archive.WriteString(m_configurations[i].name);
        rc = rc && archive.WriteString(m_configurations[i].parentConfigName);
        rc = rc && archive.WriteInt(m_configurations[i].hiddenComponentIds.Count());
        for (int j = 0; rc && j < m_configurations[i].hiddenComponentIds.Count(); j++)
            rc = rc && archive.WriteUuid(m_configurations[i].hiddenComponentIds[j]);
    }

    if (!archive.EndWrite3dmChunk())
        rc = false;
    return rc;
}
```

**Sekundär: DocumentUserStrings** (globale Assembly-Registry)

```
"RAO::version"                          → "4.0"
"RAO::def::{defId}::isAssembly"         → "true"
"RAO::def::{defId}::componentMap"       → "{id1}:Name1|{id2}:Name2|..."
"RAO::config::autoRefreshOnBlockEdit"   → "true"
```

### 2.5 Graceful Degradation (ohne Plugin)

**Kritische Designregel:** Ein .3dm File das mit dem Plugin gespeichert wurde MUSS ohne Plugin brauchbar sein.

**Was passiert ohne Plugin:**
1. Instanzen mit aktiver Variant-Definition → zeigen die Variant (mit fehlenden Komponenten) — **korrekt aber eingefroren**
2. ON_AssemblyUserData → wird von Rhino als "unknown UserData" ignoriert aber **beibehalten** (round-trip safe)
3. DocumentUserStrings → bleiben erhalten, unsichtbar für User
4. Variant-Definitionen (mit `__aov_` Naming) → sichtbar im Block Manager als normale Definitionen

**"Reset to Blocks" Command:** Vor dem Versand ohne Plugin:
```csharp
// Alle Assembly-Instanzen zurück auf Original-Definition setzen
assemblyManager.ResetAllToOriginalDefinitions();
// Alle Variant-Definitionen löschen
assemblyManager.PurgeVariantDefinitions();
// Optional: UserData entfernen
assemblyManager.StripAllAssemblyUserData();
```

---

## 3. C# ↔ C++ Bridge

### 3.1 P/Invoke API (erweitert)

```c
// ============ NativeApi.h — Extended ============

extern "C"
{
    // --- Existing (from current codebase) ---
    NATIVE_API bool __stdcall NativeInit();
    NATIVE_API void __stdcall NativeCleanup();
    NATIVE_API int  __stdcall GetNativeVersion();
    NATIVE_API bool __stdcall SetComponentVisibility(const ON_UUID* instanceId, const char* path, bool visible);
    NATIVE_API bool __stdcall IsComponentVisible(const ON_UUID* instanceId, const char* path);
    NATIVE_API int  __stdcall GetHiddenComponentCount(const ON_UUID* instanceId);
    NATIVE_API void __stdcall ResetComponentVisibility(const ON_UUID* instanceId);
    NATIVE_API void __stdcall PersistVisibilityState();
    NATIVE_API void __stdcall LoadVisibilityState();

    // --- NEW: Assembly UserData Management ---

    /// Attach ON_AssemblyUserData to an InstanceObject.
    /// sourceDefId: the original (non-variant) definition ID.
    NATIVE_API bool __stdcall AttachAssemblyData(
        const ON_UUID* instanceId,
        const ON_UUID* sourceDefId
    );

    /// Check if an InstanceObject has ON_AssemblyUserData attached.
    NATIVE_API bool __stdcall HasAssemblyData(const ON_UUID* instanceId);

    /// Get the source definition ID from UserData.
    NATIVE_API bool __stdcall GetSourceDefinitionId(
        const ON_UUID* instanceId,
        ON_UUID* outSourceDefId
    );

    // --- NEW: Configuration Management ---

    /// Add a named configuration to an instance's UserData.
    /// hiddenIds: array of component GUIDs to hide.
    NATIVE_API bool __stdcall AddConfiguration(
        const ON_UUID* instanceId,
        const char* configName,
        const char* parentConfigName,  // NULL or "" for no parent
        const ON_UUID* hiddenIds,
        int hiddenCount
    );

    /// Remove a named configuration.
    NATIVE_API bool __stdcall RemoveConfiguration(
        const ON_UUID* instanceId,
        const char* configName
    );

    /// Set the active configuration name.
    NATIVE_API bool __stdcall SetActiveConfiguration(
        const ON_UUID* instanceId,
        const char* configName
    );

    /// Get active configuration name. Returns string length, fills buffer.
    NATIVE_API int __stdcall GetActiveConfiguration(
        const ON_UUID* instanceId,
        char* buffer,
        int bufferSize
    );

    /// Get configuration count for an instance.
    NATIVE_API int __stdcall GetConfigurationCount(const ON_UUID* instanceId);

    /// Get configuration names. Returns count, fills buffer with null-separated names.
    NATIVE_API int __stdcall GetConfigurationNames(
        const ON_UUID* instanceId,
        char* buffer,
        int bufferSize
    );

    /// Get hidden component IDs for a specific configuration.
    NATIVE_API int __stdcall GetConfigurationHiddenIds(
        const ON_UUID* instanceId,
        const char* configName,
        ON_UUID* buffer,
        int maxCount
    );

    // --- NEW: Event Callbacks (C++ → C#) ---

    /// Callback type: notifies C# when assembly state changes.
    typedef void (__stdcall *AssemblyChangedCallback)(
        const ON_UUID* instanceId,
        int changeType   // 0=visibility, 1=config, 2=definition, 3=deleted
    );

    /// Register a callback for assembly change notifications.
    NATIVE_API void __stdcall RegisterAssemblyCallback(AssemblyChangedCallback callback);

    /// Unregister the callback.
    NATIVE_API void __stdcall UnregisterAssemblyCallback();
}
```

### 3.2 C# P/Invoke Wrapper

```csharp
internal static class NativeVisibilityInterop
{
    private const string DllName = "RhinoAssemblyOutliner.native.dll";

    // Callback delegate — must match C++ typedef
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void AssemblyChangedCallback(
        ref Guid instanceId,
        int changeType
    );

    // Keep a static reference to prevent GC collection
    private static AssemblyChangedCallback? _callbackDelegate;

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool AttachAssemblyData(ref Guid instanceId, ref Guid sourceDefId);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool HasAssemblyData(ref Guid instanceId);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool AddConfiguration(
        ref Guid instanceId,
        [MarshalAs(UnmanagedType.LPStr)] string configName,
        [MarshalAs(UnmanagedType.LPStr)] string? parentConfigName,
        [MarshalAs(UnmanagedType.LPArray)] Guid[] hiddenIds,
        int hiddenCount
    );

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool SetActiveConfiguration(
        ref Guid instanceId,
        [MarshalAs(UnmanagedType.LPStr)] string configName
    );

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void RegisterAssemblyCallback(AssemblyChangedCallback callback);

    // ... etc for all functions
}
```

### 3.3 Event-System: C++ → C# → UI

```
┌─────────────┐    Callback    ┌──────────────┐    C# Event    ┌─────────────┐
│  C++ Native  │──────────────►│ EventBridge  │───────────────►│  UI Panel   │
│              │   (P/Invoke)  │  (C#)        │  (INotify)     │  (Eto)      │
│ DocEvents:   │               │              │                │             │
│ • BlockEdit  │               │ Translates:  │                │ Updates:    │
│ • Delete     │               │ • ChangeType │                │ • TreeView  │
│ • Undo       │               │ • InstanceId │                │ • Config DD │
│              │               │ → C# events  │                │ • Detail    │
└─────────────┘               └──────────────┘                └─────────────┘
```

```csharp
public class EventBridge : IDisposable
{
    public event EventHandler<AssemblyChangedEventArgs>? AssemblyChanged;

    private NativeVisibilityInterop.AssemblyChangedCallback _nativeCallback;

    public EventBridge()
    {
        _nativeCallback = OnNativeCallback;
        NativeVisibilityInterop.RegisterAssemblyCallback(_nativeCallback);
    }

    private void OnNativeCallback(ref Guid instanceId, int changeType)
    {
        // Marshal to UI thread
        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            AssemblyChanged?.Invoke(this, new AssemblyChangedEventArgs
            {
                InstanceId = instanceId,
                ChangeType = (AssemblyChangeType)changeType
            });
        }));
    }
}
```

### 3.4 Command Routing: UI → C++ Object

```
User klickt "Hide Component" im Panel
    │
    ▼
AssemblyOutlinerPanel.OnHideComponent(instanceId, componentId)
    │
    ▼
AssemblyManager.SetComponentVisibility(instanceId, componentId, false)
    │
    ├── 1. Update ON_AssemblyUserData (via P/Invoke)
    │       NativeInterop.AddConfiguration(...) or update existing
    │
    ├── 2. Update active Config's hidden list
    │
    ├── 3. VariantManager.SetComponentVisibility(...)
    │       → GetOrCreateVariant() → ReassignInstance()
    │       (This is the actual Rhino object replacement)
    │
    ├── 4. doc.Views.Redraw()
    │
    └── 5. EventBridge fires → UI updates tree state
```

---

## 4. Integration mit Rhino

### 4.1 Custom Object Type Registration

**Entscheidung: Wir registrieren KEINEN Custom Object Type.**

Stattdessen nutzen wir **Standard InstanceObjects + ON_UserData**. Das ist robuster:

- Keine `CRhinoObject`-Subclass nötig
- ON_UserData ist der offizielle Weg, custom Daten an Objekte zu hängen
- Round-trip safe: Rhino ignoriert unbekannte UserData, löscht sie aber nicht
- Kein Risk of Breaking Changes bei Rhino-Updates

```cpp
// Registration: nur die UserData-Klasse registrieren
// In Plugin OnLoadPlugIn:
ON_AssemblyUserData::RegisterClass();
```

### 4.2 File I/O

**Automatisch** durch ON_UserData:
- `Archive() = true` → Rhino serialisiert unsere UserData beim Speichern
- `Read()/Write()` mit versioniertem Chunk-Format → forward/backward compat
- Variant-Definitionen sind normale InstanceDefinitions → werden normal gespeichert

**Restore-Workflow beim Öffnen:**
```
File öffnen
    │
    ▼
CDocEventHandler::OnEndOpenDocument()
    │
    ▼
1. Scanne alle InstanceObjects nach ON_AssemblyUserData
2. Für jede Instanz mit UserData:
   a. Lies sourceDefinitionId → finde Original-Definition
   b. Lies activeConfigName → finde/erstelle passende Variant-Definition
   c. Validiere: ist die Instanz auf der richtigen Variant?
   d. Falls nicht: ReassignInstance()
3. GarbageCollect: lösche Variant-Definitionen ohne Referenzen
```

### 4.3 Undo/Redo

**Gratis durch Rhino:**
- `doc.Objects.Replace()` (für Definition-Reassignment) → automatisch im Undo-Stack
- `InstanceDefinitions.Add()` → automatisch im Undo-Stack

**Zusätzlich nötig:**
```csharp
// Wrap multi-step operations in a single Undo record
uint undoId = doc.BeginUndoRecord("Change Assembly Configuration");
try
{
    // 1. Update UserData
    // 2. Create/find variant definition
    // 3. Reassign instance
}
finally
{
    doc.EndUndoRecord(undoId);
}
```

**Undo-Event-Handler:**
```cpp
// In CDocEventHandler:
void OnUndoRedo(CRhinoDoc& doc, bool bUndo) override
{
    // Nach Undo: Varianten-Definitionen können als Waisen zurückbleiben
    // → Schedule GarbageCollect (delayed, nicht sofort — es könnte ein Redo folgen)
    ScheduleGarbageCollect(doc, 5000 /*ms delay*/);

    // Notify C# über Callback
    FireAssemblyCallback(ON_nil_uuid, CHANGE_UNDO);
}
```

### 4.4 Copy/Paste

**Innerhalb des Dokuments:**
- ON_UserData reist mit dem InstanceObject → Kopie hat gleichen State
- Die Variant-Definition existiert bereits → kein Extra-Aufwand
- SourceDefinitionId bleibt korrekt

**Zwischen Dokumenten:**
- ON_UserData reist mit (Rhino kopiert UserData bei Paste)
- **Problem:** Die Variant-Definition existiert im Ziel-Dokument nicht
- **Lösung:** `OnEndOpenDocument`/Paste-Event → RestoreWorkflow (wie bei File Open)
- Die Original-Definition wird per Name gesucht (nicht per GUID, da GUIDs sich ändern)

```csharp
// Fallback für Cross-Document:
if (sourceDefId not found)
{
    // Suche per Name
    var sourceDef = FindDefinitionByName(userData.SourceDefinitionName);
    if (sourceDef != null)
        userData.SourceDefinitionId = sourceDef.Id;
    else
        // Can't resolve → reset to whatever definition the instance points at
        StripAssemblyData(instance);
}
```

### 4.5 Selection & Object Properties

- Assembly-Instanzen sind normale InstanceObjects → Selection funktioniert normal
- Object Properties Panel: zeigt Standard-Attributes (Layer, Color, Name)
- **Unser Panel** zeigt zusätzlich: Configurations, Component Visibility, Overrides
- **Sub-Object Selection:** Nicht nötig für V1. Future: Custom Grips für Component-Level Selection.

### 4.6 Layers

- Die Instanz selbst liegt auf einem User-Layer (normal)
- Komponenten innerhalb der Definition haben eigene Layer-Zuordnungen
- Layer-Visibility wirkt global auf alle Instanzen (Rhino-Standard)
- **Kein Konflikt** mit unserem System: wir arbeiten auf Definition-Level, nicht Layer-Level

### 4.7 Drag & Drop (Panel → Viewport)

```
User zieht "Motor_v1" aus dem Assembly Panel in den Viewport
    │
    ▼
1. Panel startet Drag: DragDropEffects.Copy
2. DragData: { definitionName: "Motor_v1", configName: "Default" }
    │
    ▼
3. Viewport empfängt Drop:
   a. Erstelle neue InstanceObject an Drop-Position
   b. Hänge ON_AssemblyUserData an (mit Default-Config)
   c. Falls Config nicht "Default": erstelle Variant + Reassign
    │
    ▼
4. UI aktualisiert Tree
```

---

## 5. Configuration System

### 5.1 Definition einer Configuration

```csharp
public class AssemblyConfiguration
{
    public string Name { get; set; }               // "Wartung"
    public string? ParentName { get; set; }         // "Default" or null
    public HashSet<Guid> HiddenComponentIds { get; set; } = new();
    
    // Future extensions:
    // public Dictionary<Guid, MaterialOverride> MaterialOverrides { get; set; }
    // public Dictionary<Guid, double> DimensionOverrides { get; set; }
}
```

**Implizite "Default" Configuration:**
- Jedes Assembly hat immer eine implizite "Default" Config wo alles sichtbar ist
- "Default" wird NICHT explizit gespeichert (spart Platz)
- Wenn `activeConfigName == "" || activeConfigName == "Default"` → Original-Definition

### 5.2 Config-Wechsel

```
User wählt "Wartung" im Config-Dropdown
    │
    ▼
ConfigurationService.ActivateConfiguration(instanceId, "Wartung")
    │
    ├── 1. Lies Config "Wartung" aus UserData
    │       → hiddenIds = [Welle.Id]
    │
    ├── 2. Resolve Inheritance Chain:
    │       "Wartung" inherits from "Default" (alles sichtbar)
    │       → effektiv hidden = [Welle.Id]
    │
    ├── 3. Berechne VisibilityState
    │       state = new VisibilityState(hiddenIds)
    │
    ├── 4. VariantManager.GetOrCreateVariant(sourceDefIdx, state)
    │       → returns variantDefIndex (cached or new)
    │
    ├── 5. VariantManager.ReassignInstance(instanceId, variantDefIndex)
    │       → doc.Objects.Replace(...)
    │
    ├── 6. Update UserData: activeConfigName = "Wartung"
    │
    └── 7. Redraw + Event
```

### 5.3 UI-Darstellung

```
Assembly Outliner Panel
┌─────────────────────────────────────────┐
│ 🔧 Motor_v1 (Instance "Motor_A")       │
│ ┌─────────────────────────────────────┐ │
│ │ Configuration: [Wartung ▼]          │ │
│ │   Default                           │ │
│ │ ● Wartung                           │ │
│ │   Wartung_Detail                    │ │
│ │   Minimal                           │ │
│ │   + New Configuration...            │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ Components:                             │
│ 👁 Gehaeuse          [Brep]    Layer0   │
│ 🚫 Welle             [Brep]    Layer0   │ ← hidden in "Wartung"
│ 👁 Lager_L           [Block]   Layer0   │
│ 👁 Lager_R           [Block]   Layer0   │
│                                         │
│ [Show All] [Hide All] [Reset to Default]│
└─────────────────────────────────────────┘
```

### 5.4 Vererbung

```csharp
public VisibilityState ResolveConfiguration(Guid instanceId, string configName)
{
    var configs = GetAllConfigurations(instanceId);
    var hiddenIds = new HashSet<Guid>();

    // Walk inheritance chain
    string? current = configName;
    var visited = new HashSet<string>(); // cycle protection
    
    while (current != null && !string.IsNullOrEmpty(current) && visited.Add(current))
    {
        var config = configs.FirstOrDefault(c => c.Name == current);
        if (config == null) break;
        
        foreach (var id in config.HiddenComponentIds)
            hiddenIds.Add(id);
        
        current = config.ParentName;
    }
    
    return new VisibilityState(hiddenIds);
}
```

---

## 6. Performance Considerations

### 6.1 Skalierung: 1000+ Instances

| Operation | Kosten | Mitigation |
|-----------|--------|------------|
| Variant-Erstellung | O(n) Geometrie-Kopien | Cache: gleiche States teilen Variant |
| Config-Wechsel | O(1) Replace | Variant schon gecached |
| File Open Restore | O(N) Instanzen scannen | Lazy: nur bei Bedarf Variant erstellen |
| BlockEdit Refresh | O(V) Varianten neu erstellen | Batch: alle Varianten in einem Pass |
| GarbageCollect | O(D) Definitionen prüfen | Delayed, nicht bei jedem Edit |
| UI TreeView | O(N) Nodes | Virtualisierung, Lazy Children |

**Worst-Case Speicher:**
- 1000 Instanzen, 50 verschiedene Configs → 50 Variant-Definitionen
- Jede Variant ≈ 90% Geometrie des Originals → ~45 MB bei 1MB/Definition
- **Akzeptabel** für typische Rhino-Workflows (Architektur-Files haben routinemässig 10k+ Blocks)

### 6.2 Lazy Loading

```csharp
// Variants erst erstellen wenn Instance tatsächlich angezeigt wird
public void EnsureVariantForInstance(Guid instanceId)
{
    var userData = GetAssemblyData(instanceId);
    if (userData == null || userData.ActiveConfigName == "Default")
        return; // nothing to do

    if (!IsOnCorrectVariant(instanceId, userData))
    {
        var state = ResolveConfiguration(instanceId, userData.ActiveConfigName);
        var variantIdx = variantManager.GetOrCreateVariant(userData.SourceDefIndex, state);
        variantManager.ReassignInstance(instanceId, variantIdx);
    }
}
```

### 6.3 Display Mesh Caching

- Variant-Definitionen nutzen Rhinos eingebautes Mesh-Caching
- `dp.DrawObject()` nutzt gecachte Display-Meshes automatisch
- **Kein eigenes Caching nötig** — Rhino handelt das

### 6.4 Batched Operations

```csharp
// "Hide Component in ALL Instances" — eine Variant für alle
public void SetComponentVisibilityGlobal(int sourceDefIndex, Guid componentId, bool visible)
{
    uint undoId = doc.BeginUndoRecord("Change visibility for all instances");

    // Finde alle Instanzen dieser Source-Definition
    var instances = registry.GetInstancesForDefinition(sourceDefIndex);
    
    // Group by current VisibilityState → minimize variant creations
    var groups = instances.GroupBy(i => GetCurrentState(i));
    
    foreach (var group in groups)
    {
        var newState = visible ? group.Key.Show(componentId) : group.Key.Hide(componentId);
        var variantIdx = variantManager.GetOrCreateVariant(sourceDefIndex, newState);
        
        foreach (var instance in group)
        {
            variantManager.ReassignInstance(instance.Id, variantIdx);
            UpdateUserData(instance.Id, newState);
        }
    }
    
    doc.EndUndoRecord(undoId);
}
```

---

## 7. Migration Path

### 7.1 Bestehende Blocks → Assembly Objects

```csharp
public class MigrationService
{
    /// <summary>
    /// Konvertiert einen normalen Block in ein Assembly.
    /// Non-destructive: fügt nur UserData hinzu.
    /// </summary>
    public void ConvertToAssembly(Guid instanceId)
    {
        var instance = doc.Objects.FindId(instanceId) as InstanceObject;
        if (instance == null) return;

        // 1. Mark definition as assembly source
        var defId = instance.InstanceDefinition.Id;
        doc.Strings.SetString($"RAO::def::{defId}::isAssembly", "true");

        // 2. Build component map (Id → Name)
        var components = instance.InstanceDefinition.GetObjects();
        var map = components.Select(c => $"{c.Id:N}:{c.Name ?? c.Attributes.LayerIndex.ToString()}");
        doc.Strings.SetString($"RAO::def::{defId}::componentMap", string.Join("|", map));

        // 3. Attach UserData to instance
        var userData = new ON_AssemblyUserData
        {
            SourceDefinitionId = defId,
            ActiveConfigName = "Default"
        };
        // Via P/Invoke:
        NativeInterop.AttachAssemblyData(ref instanceId, ref defId);
    }

    /// <summary>
    /// Batch: konvertiert alle Instanzen einer Definition.
    /// </summary>
    public void ConvertAllInstances(int definitionIndex) { ... }

    /// <summary>
    /// Rückwärts: Assembly → normaler Block.
    /// </summary>
    public void RevertToBlock(Guid instanceId)
    {
        // 1. Reset auf Original-Definition
        var userData = GetAssemblyData(instanceId);
        if (userData != null)
        {
            var originalDefIdx = FindDefinitionIndex(userData.SourceDefinitionId);
            variantManager.ReassignInstance(instanceId, originalDefIdx);
        }

        // 2. UserData entfernen
        NativeInterop.RemoveAssemblyData(ref instanceId);
    }
}
```

### 7.2 Save As ohne Plugin

Wenn ein User "Save As" macht und das File an jemanden ohne Plugin schickt:

1. **Varianten-Definitionen bleiben** → Instanzen mit aktiver Variant zeigen die Variant-Geometrie (korrekt, aber frozen)
2. **UserData bleibt** → Rhino ignoriert es, löscht es nicht (round-trip safe)
3. **Empfehlung:** "Export for Sharing" Command der `ResetAllToOriginalDefinitions()` + `PurgeVariantDefinitions()` ausführt

```
┌──────────────────────────────────────────────┐
│  "Export for Sharing" Dialog                  │
│                                              │
│  ☐ Reset all assemblies to Default config    │
│  ☐ Remove variant definitions                │
│  ☐ Strip assembly metadata                   │
│  ☐ Keep configurations (for re-import)       │
│                                              │
│  [Export...] [Cancel]                        │
└──────────────────────────────────────────────┘
```

---

## 8. Risiken und Mitigations

### 8.1 Risk Matrix

| # | Risiko | Wahrsch. | Impact | Mitigation |
|---|--------|----------|--------|------------|
| R1 | ON_UserData wird nicht korrekt round-tripped | Niedrig | Hoch | Bewährt: VisualARQ, Grasshopper, andere Plugins nutzen das seit Jahren. Unit-Tests. |
| R2 | BlockEdit invalidiert Varianten | Hoch | Hoch | Event-Handler: `InstanceDefinitionModified` → RefreshAllVariants(). Bereits in v3 gelöst. |
| R3 | Memory-Explosion bei vielen Varianten | Mittel | Mittel | Deduplizierung (gleiche States = 1 Variant), GarbageCollect, Lazy Creation. |
| R4 | Undo-Inkonsistenz | Mittel | Mittel | BeginUndoRecord/EndUndoRecord, Delayed GC, Undo-Event-Handler. |
| R5 | Nested Block Visibility | Hoch | Mittel | V1: nur Top-Level. V2: rekursives Cloning (max 2 Ebenen). |
| R6 | Cross-Document Copy/Paste | Mittel | Mittel | Name-based Fallback für Definition-Lookup. Graceful degradation: strip data if unresolvable. |
| R7 | Rhino Update bricht UserData | Niedrig | Hoch | Versioned chunk format. McNeel garantiert ON_UserData backward compat. |
| R8 | Performance bei 1000+ Config-Wechseln | Niedrig | Niedrig | Variant-Cache, einzelner Replace-Call pro Instance. |
| R9 | Block Manager zeigt Variant-Definitionen | Sicher | Niedrig | Naming convention (`__aov_`). Future: Filter-UI oder hidden Definitionen. |
| R10 | P/Invoke Crashes (C++ Exception leakt nach C#) | Mittel | Hoch | SEH-Handler in allen exported Functions. Try/Catch in C# wrapper. Defensive Null-Checks. |

### 8.2 Fallback-Strategien

**Wenn Definition-Cloning Performance-Probleme hat:**
→ Fallback auf DisplayConduit (bereits implementiert) für Preview, Clone nur bei Save

**Wenn ON_UserData nicht persistiert:**
→ Fallback auf DocumentUserStrings (weniger elegant, aber funktioniert)

**Wenn C++ Native DLL nicht lädt:**
→ Reines C# mit RhinoCommon (VariantManager braucht kein C++, nur UserData-Persistence wäre langsamer)

**Wenn McNeel das Block-System fundamental ändert:**
→ Monitoring von Rhino 9 WIP. Unsere Architektur nutzt nur public APIs. Definition-Cloning ist so fundamental dass es kaum brechen kann.

### 8.3 CRhinoObject Subclassing — Warum NICHT

Die ursprüngliche Frage war ob wir `CRhinoObject` subclassen. **Nein:**

1. **Undokumentiert:** McNeel dokumentiert `CRhinoObject` Subclassing nicht offiziell für Plugins
2. **Fragil:** Interne vtable-Änderungen brechen Binary Compat zwischen Rhino Versions
3. **File Format:** Custom Object Types brauchen Custom Chunk-Handler in .3dm — wenn das Plugin fehlt, ist das Objekt verloren
4. **VisualARQ-Precedent:** VisualARQ macht es, aber mit dediziertem McNeel-Support und jahrelangem Investment
5. **Nicht nötig:** ON_UserData + Definition-Cloning erreicht 95% des gleichen Ergebnisses ohne die Risiken

---

## Implementierungs-Roadmap

### Phase 1: Foundation (2-3 Wochen)
- [ ] `ON_AssemblyUserData` implementieren (C++, Read/Write/Archive)
- [ ] P/Invoke Bridge erweitern (AttachAssemblyData, HasAssemblyData, etc.)
- [ ] VariantManager aus v3 implementieren (C#)
- [ ] DefinitionCache mit Deduplizierung
- [ ] Basic Migration: ConvertToAssembly Command

### Phase 2: Configurations (2 Wochen)
- [ ] ConfigurationService (CRUD)
- [ ] Config-Vererbung (Parent → Derived)
- [ ] UI: Configuration Dropdown im Panel
- [ ] Config-Wechsel → Variant-Assignment

### Phase 3: Integration (2 Wochen)
- [ ] BlockEdit Event-Handler → Variant Refresh
- [ ] Undo/Redo Support (BeginUndoRecord)
- [ ] Copy/Paste (intra- und cross-document)
- [ ] GarbageCollect für Waisen-Varianten
- [ ] "Export for Sharing" Command

### Phase 4: Polish (1-2 Wochen)
- [ ] Batch Operations (global visibility)
- [ ] Drag & Drop aus Panel
- [ ] Performance-Tests mit 1000+ Instances
- [ ] Error Handling / Edge Cases
- [ ] Documentation

### Phase 5: Advanced (Future)
- [ ] Nested Block Visibility (1-2 Ebenen)
- [ ] Dimension Overrides
- [ ] Material Overrides per Config
- [ ] Grasshopper Integration
- [ ] Custom Grips für Component Selection

---

## Appendix: Key Code Snippets

### A. VariantManager Core (C#, from v3)

```csharp
public class VariantManager
{
    private readonly RhinoDoc _doc;
    private readonly DefinitionCache _cache = new();

    public void SetComponentVisibility(Guid instanceId, Guid componentId, bool visible)
    {
        var instance = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instance == null) return;

        var userData = GetAssemblyData(instance);
        var currentState = ResolveCurrentState(userData);
        var newState = visible ? currentState.Show(componentId) : currentState.Hide(componentId);

        if (newState.IsDefault)
        {
            // Back to original definition
            ReassignInstance(instanceId, FindDefIndex(userData.SourceDefinitionId));
        }
        else
        {
            var sourceDefIdx = FindDefIndex(userData.SourceDefinitionId);
            var variantIdx = GetOrCreateVariant(sourceDefIdx, newState);
            ReassignInstance(instanceId, variantIdx);
        }
    }

    private int GetOrCreateVariant(int sourceDefIdx, VisibilityState state)
    {
        var key = new VariantKey(sourceDefIdx, state);
        if (_cache.TryGet(key, out int cached))
            return cached;

        var sourceDef = _doc.InstanceDefinitions[sourceDefIdx];
        var objects = sourceDef.GetObjects();
        var geometries = new List<GeometryBase>();
        var attributes = new List<ObjectAttributes>();

        foreach (var obj in objects)
        {
            if (state.IsVisible(obj.Id))
            {
                geometries.Add(obj.Geometry.Duplicate());
                attributes.Add(obj.Attributes.Duplicate());
            }
        }

        string name = $"{sourceDef.Name}__aov_{state.GetHashCode():x8}";
        int idx = _doc.InstanceDefinitions.Add(name, $"Variant of {sourceDef.Name}",
            sourceDef.BasePoint, geometries, attributes);

        _cache.Add(key, idx);
        return idx;
    }

    private void ReassignInstance(Guid instanceId, int newDefIdx)
    {
        var instance = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instance == null) return;

        var newDef = _doc.InstanceDefinitions[newDefIdx];
        var newGeom = new InstanceReferenceGeometry(newDef.Id, instance.InstanceXform);
        _doc.Objects.Replace(instanceId, newGeom);
    }
}
```

### B. ON_AssemblyUserData Registration (C++)

```cpp
// In Plugin init:
ON_Object::RegisterClass(
    ON_CLASS_ID(ON_AssemblyUserData),
    ON_AssemblyUserData::m_ON_AssemblyUserData_class_rtti
);
```

### C. Variant Naming Convention

```
{OriginalName}__aov_{hash8}

Examples:
  Motor_v1__aov_3f8a2b1c
  Chair_Assembly__aov_a1b2c3d4
  Connector_Type_A__aov_00000000  (all visible = shouldn't exist, but just in case)
```

`__aov_` = **A**ssembly **O**utliner **V**ariant — easily searchable, unlikely to conflict with user names.
