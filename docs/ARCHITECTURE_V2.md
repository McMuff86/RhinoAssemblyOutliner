# Architecture V2: Hybrid C++/C# with Per-Instance Component Visibility

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Rhino 8                                   │
│                                                                     │
│  ┌───────────────────────────────┐  ┌─────────────────────────────┐ │
│  │      C# Plugin (.rhp)         │  │     C++ Plugin (.rhp)       │ │
│  │                               │  │                             │ │
│  │  ┌─────────────────────────┐  │  │  ┌───────────────────────┐  │ │
│  │  │     UI Layer (Eto)      │  │  │  │  Display Engine       │  │ │
│  │  │  ┌─────────────────┐   │  │  │  │                       │  │ │
│  │  │  │ AssemblyOutliner│   │  │  │  │  CPerInstanceVis-     │  │ │
│  │  │  │ Panel           │   │  │  │  │  ibilityConduit       │  │ │
│  │  │  │  ┌────────────┐ │   │  │  │  │  (SC_DRAWOBJECT)      │  │ │
│  │  │  │  │ TreeView   │ │   │  │  │  │                       │  │ │
│  │  │  │  │ DetailPanel│ │   │  │  │  │  ┌─────────────────┐  │  │ │
│  │  │  │  │ SearchBar  │ │   │  │  │  │  │ Display Cache   │  │  │ │
│  │  │  │  │ StatusBar  │ │   │  │  │  │  │ CRhinoCacheHandle│ │  │ │
│  │  │  │  └────────────┘ │   │  │  │  │  └─────────────────┘  │  │ │
│  │  │  └─────────────────┘   │  │  │  └───────────────────────┘  │ │
│  │  │                        │  │  │                             │ │
│  │  ┌─────────────────────────┐  │  │  ┌───────────────────────┐  │ │
│  │  │   Services Layer        │  │  │  │  Persistence          │  │ │
│  │  │  SelectionSyncService   │  │  │  │                       │  │ │
│  │  │  VisibilityService ─────┼──┼──┼──┤► CComponentVisibility │  │ │
│  │  │  DocumentEventService   │  │  │  │   Data (ON_UserData)  │  │ │
│  │  │  BlockInfoService       │  │  │  │                       │  │ │
│  │  └─────────────────────────┘  │  │  └───────────────────────┘  │ │
│  │  │                        │  │  │                             │ │
│  │  ┌─────────────────────────┐  │  │  ┌───────────────────────┐  │ │
│  │  │   Model Layer           │  │  │  │  extern "C" API       │  │ │
│  │  │  AssemblyTreeBuilder    │  │  │  │  RAO_Initialize()     │  │ │
│  │  │  AssemblyNode (abstract)│  │  │  │  RAO_SetComponent-    │  │ │
│  │  │  BlockInstanceNode      │  │  │  │    Hidden()           │  │ │
│  │  │  DocumentNode           │  │  │  │  RAO_GetHidden-       │  │ │
│  │  │  GeometryNode           │  │  │  │    ComponentIds()     │  │ │
│  │  └─────────────────────────┘  │  │  │  ... (~10 functions)  │  │ │
│  │  │                        │  │  │  └───────────────────────┘  │ │
│  │  ┌─────────────────────────┐  │  │           ▲               │ │
│  │  │   Interop Layer         │  │  │           │               │ │
│  │  │  NativeInterop ─────────┼──┼──┼───────────┘ P/Invoke     │ │
│  │  │  (DllImport + error     │  │  │                           │ │
│  │  │   handling wrappers)    │  │  │                           │ │
│  │  └─────────────────────────┘  │  │                           │ │
│  └───────────────────────────────┘  └─────────────────────────────┘ │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Rhino Core                                │   │
│  │  RhinoDoc  │  Display Pipeline  │  Events  │  Block Defs    │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

## Data Flow: Per-Instance Component Visibility

### Hide Component Flow

```
User clicks eye icon on component in tree
    │
    ▼
AssemblyTreeView.OnVisibilityToggle(componentNode)
    │
    ▼
VisibilityService.SetComponentVisibility(instanceId, componentId, false)
    │
    ▼
NativeInterop.RAO_SetComponentHidden(ref instanceId, ref componentId, true)
    │  P/Invoke boundary
    ▼
C++ RAO_SetComponentHidden()
    ├── Acquire unique_lock on m_mutex
    ├── Find/create CComponentVisibilityData on InstanceObject (ON_UserData)
    ├── Add componentId to m_hidden_component_ids
    ├── Register instance in conduit's managed set
    ├── Invalidate display cache for this instance
    ├── Release lock
    └── RhinoDoc::Redraw()
            │
            ▼
        Display Pipeline runs
            │
            ▼
        CPerInstanceVisibilityConduit::ExecConduit(SC_DRAWOBJECT)
            ├── Acquire shared_lock on m_mutex
            ├── Check: is this instance in managed set? YES
            ├── Suppress normal draw (return false)
            ├── Iterate definition objects
            │   ├── Component A: not hidden → dp.DrawObject(comp, &xform, cache)
            │   ├── Component B: HIDDEN → skip
            │   └── Component C: not hidden → dp.DrawObject(comp, &xform, cache)
            └── Release lock
```

### Document Open Flow

```
Rhino opens .3dm file
    │
    ▼
ON_UserData::Read() deserializes CComponentVisibilityData per instance
    │
    ▼
C# DocumentEventService fires TreeInvalidated
    │
    ▼
AssemblyTreeBuilder.BuildTree()
    │  For each BlockInstance with ON_UserData:
    ▼
NativeInterop.RAO_GetHiddenCount(ref instanceId) → count > 0
    │
    ▼
NativeInterop.RAO_GetHiddenComponentIds(ref instanceId, buffer, size)
    │
    ▼
TreeView shows component nodes with correct visibility state
    │
    ▼
Conduit auto-registers instances (ON_UserData present → managed)
```

## P/Invoke API Surface

```cpp
// ─── Lifecycle ───
bool RAO_Initialize();              // Create conduit, register plugin state
void RAO_Shutdown();                // Cleanup

// ─── Conduit Control ───
bool RAO_EnableConduit();           // Enable display pipeline hook
void RAO_DisableConduit();          // Disable (pass-through)

// ─── Visibility Operations ───
bool RAO_SetComponentHidden(        // Hide/show one component on one instance
    const GUID* instanceId,
    const GUID* componentId,
    bool hidden);

bool RAO_IsComponentHidden(         // Query single component state
    const GUID* instanceId,
    const GUID* componentId);

void RAO_ShowAllComponents(         // Reset instance to all-visible
    const GUID* instanceId);

int RAO_GetHiddenCount(             // Count hidden components
    const GUID* instanceId);

int RAO_GetHiddenComponentIds(      // Get all hidden component UUIDs
    const GUID* instanceId,
    GUID* outBuffer,                // Caller-allocated buffer
    int bufferSize);                // Buffer capacity; returns actual count

// ─── Display ───
void RAO_InvalidateInstance(        // Force cache rebuild + redraw
    const GUID* instanceId);
```

**Calling convention:** `__cdecl` (matches .NET default for P/Invoke).  
**GUID marshalling:** `ref Guid` in C# → `const GUID*` in C++. Binary-compatible, zero-copy.

## Event/Message Flow

```
┌──────────┐     ┌──────────────┐     ┌─────────────┐     ┌──────────┐
│  Rhino   │     │  C# Services │     │  C# UI      │     │  C++ Core│
│  Events  │     │              │     │             │     │          │
└────┬─────┘     └──────┬───────┘     └──────┬──────┘     └────┬─────┘
     │                  │                    │                  │
     │ SelectObjects    │                    │                  │
     ├─────────────────►│ SyncToTree()       │                  │
     │                  ├───────────────────►│ HighlightNode   │
     │                  │                    │                  │
     │ AddRhinoObject   │                    │                  │
     ├─────────────────►│ Debounce(100ms)    │                  │
     │                  ├───────────────────►│ InsertNode       │
     │                  │                    │                  │
     │                  │                    │ User clicks eye  │
     │                  │                    ├─────────────────►│
     │                  │                    │ RAO_SetComponent │
     │                  │                    │ Hidden (P/Invoke)│
     │                  │                    │                  │
     │                  │                    │                  ├──┐
     │                  │                    │                  │  │ Update
     │                  │                    │                  │  │ UserData
     │ Redraw           │                    │                  │◄─┘
     │◄─────────────────┼────────────────────┼──────────────────┤
     │                  │                    │                  │
     │ SC_DRAWOBJECT    │                    │                  │
     ├──────────────────┼────────────────────┼─────────────────►│
     │                  │                    │                  │ Draw with
     │                  │                    │                  │ hidden
     │◄─────────────────┼────────────────────┼──────────────────┤ components
     │                  │                    │                  │ skipped
```

## State Management

### Three-Tier Visibility Model

```
Layer Visibility (Rhino-native — we don't touch)
    │ must be ON
    ▼
Instance Visibility (C# — existing eye toggle, uses RhinoObject.SetObjectHidden)
    │ must be ON
    ▼
Component Visibility (C++ — per-instance sub-component hiding)
    │ must be visible
    ▼
Object renders in viewport
```

### State Locations

```
Runtime State (in-memory):
┌─────────────────────────────────────────────┐
│ C++ Conduit                                 │
│  m_managed_instances: Set<ON_UUID>          │ ← instances with any hidden component
│  m_cache: Map<ON_UUID, InstanceDrawCache>   │ ← GPU display cache per instance
│  m_mutex: shared_mutex                      │ ← thread safety
└─────────────────────────────────────────────┘
┌─────────────────────────────────────────────┐
│ C# Panel                                    │
│  _rootNodes: List<AssemblyNode>             │ ← tree model
│  _itemLookup: Dict<Guid, AssemblyNode>      │ ← fast node lookup by Rhino ID
│  _isolateState: IsolateSnapshot?            │ ← pre-isolate visibility backup
│  _viewMode: DocumentMode | AssemblyMode     │
└─────────────────────────────────────────────┘

Persisted State (in .3dm file):
┌─────────────────────────────────────────────┐
│ ON_UserData on each managed InstanceObject  │
│  m_hidden_component_ids: ON_UuidList        │ ← survives save/load/copy/paste
└─────────────────────────────────────────────┘
┌─────────────────────────────────────────────┐
│ Document UserText                           │
│  "RAO_AssemblyRoot": Guid string            │ ← assembly mode root
└─────────────────────────────────────────────┘
┌─────────────────────────────────────────────┐
│ Plugin Settings (per-user, not per-doc)     │
│  DefaultMode, ShowGeometryNodes, etc.       │
└─────────────────────────────────────────────┘
```

## Graceful Degradation

If C++ plugin is not loaded (e.g., Mac, or missing DLL):

```
NativeInterop.RAO_SetComponentHidden() → catches DllNotFoundException
    → VisibilityService falls back to instance-level visibility only
    → UI hides "component visibility" eye icons
    → Tree still works, selection sync still works, all v1.0 features intact
```
