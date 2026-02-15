# Architecture V2: Hybrid C++/C# with Per-Instance Component Visibility

> Last updated: 2026-02-15 (Sprint 1 complete, Sprint 3 C++ work in progress)

## Overview

RhinoAssemblyOutliner is a hybrid C#/C++ Rhino 8 plugin. The **C# plugin** (.rhp) provides the UI, tree model, and services layer using Eto.Forms and RhinoCommon. The **C++ native DLL** handles display pipeline interception (SC_DRAWOBJECT) for per-instance component visibility, data persistence via ON_UserData, and document lifecycle events.

Communication between the two layers uses a **P/Invoke bridge** — 12 exported `extern "C"` functions with `__stdcall` calling convention.

---

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Rhino 8                                   │
│                                                                     │
│  ┌───────────────────────────────┐  ┌─────────────────────────────┐ │
│  │      C# Plugin (.rhp)         │  │   C++ Native DLL (.dll)     │ │
│  │                               │  │                             │ │
│  │  ┌─────────────────────────┐  │  │  ┌───────────────────────┐  │ │
│  │  │     UI Layer (Eto)      │  │  │  │  Display Engine       │  │ │
│  │  │  ┌─────────────────┐   │  │  │  │                       │  │ │
│  │  │  │ AssemblyOutliner│   │  │  │  │  CVisibilityConduit   │  │ │
│  │  │  │ Panel           │   │  │  │  │  (SC_DRAWOBJECT)      │  │ │
│  │  │  │  ┌────────────┐ │   │  │  │  │  - Path-based filter  │  │ │
│  │  │  │  │ TreeView   │ │   │  │  │  │  - Recursive nested   │  │ │
│  │  │  │  │ DetailPanel│ │   │  │  │  │    block draw         │  │ │
│  │  │  │  │ SearchBar  │ │   │  │  │  │  - Component color    │  │ │
│  │  │  │  │ StatusBar  │ │   │  │  │  │    resolution         │  │ │
│  │  │  │  └────────────┘ │   │  │  │  └───────────────────────┘  │ │
│  │  │  └─────────────────┘   │  │  │                             │ │
│  │  │                        │  │  │  ┌───────────────────────┐  │ │
│  │  ┌─────────────────────────┐  │  │  │  State Management     │  │ │
│  │  │   Services Layer        │  │  │  │                       │  │ │
│  │  │  SelectionSyncService   │  │  │  │  CVisibilityData      │  │ │
│  │  │  VisibilityService ─────┼──┼──┼──┤  - CRITICAL_SECTION   │  │ │
│  │  │  DocumentEventService   │  │  │  │  - UUID→Set<path>     │  │ │
│  │  │  BlockInfoService       │  │  │  │  - Managed instances   │  │ │
│  │  └─────────────────────────┘  │  │  └───────────────────────┘  │ │
│  │  │                        │  │  │                             │ │
│  │  ┌─────────────────────────┐  │  │  ┌───────────────────────┐  │ │
│  │  │   Model Layer           │  │  │  │  Persistence          │  │ │
│  │  │  AssemblyTreeBuilder    │  │  │  │                       │  │ │
│  │  │  AssemblyNode (abstract)│  │  │  │  CComponentVisibility │  │ │
│  │  │  BlockInstanceNode      │  │  │  │  Data (ON_UserData)   │  │ │
│  │  │  DocumentNode           │  │  │  │  - Write/Read 3dm     │  │ │
│  │  │  GeometryNode           │  │  │  │  - Chunked format v1  │  │ │
│  │  └─────────────────────────┘  │  │  └───────────────────────┘  │ │
│  │  │                        │  │  │                             │ │
│  │  ┌─────────────────────────┐  │  │  ┌───────────────────────┐  │ │
│  │  │   Interop Layer         │  │  │  │  Document Events      │  │ │
│  │  │  NativeInterop ─────────┼──┼──┼──┤  CDocEventHandler     │  │ │
│  │  │  (DllImport + error     │  │  │  │  (CRhinoEventWatcher) │  │ │
│  │  │   handling wrappers)    │  │  │  │  - OnEndOpenDocument   │  │ │
│  │  └─────────────────────────┘  │  │  │  - OnBeginSaveDocument│  │ │
│  │  │                        │  │  │  │  - OnCloseDocument     │  │ │
│  └───────────────────────────────┘  │  │  - OnDeleteObject     │  │ │
│                                     │  └───────────────────────┘  │ │
│                                     │                             │ │
│                                     │  ┌───────────────────────┐  │ │
│                                     │  │  extern "C" API       │  │ │
│                                     │  │  (12 functions)       │  │ │
│                                     │  │  See API_REFERENCE.md │  │ │
│                                     │  └───────────────────────┘  │ │
│                                     └─────────────────────────────┘ │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Rhino Core                                │   │
│  │  RhinoDoc  │  Display Pipeline  │  Events  │  Block Defs    │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## C# Plugin Structure

### UI Layer (`UI/`)
- **AssemblyOutlinerPanel** — Dockable Rhino panel (Eto.Forms), hosts tree, detail panel, search bar, status bar, toolbar
- **AssemblyTreeView** — TreeGridView with eye-icon visibility column, drag-drop, keyboard shortcuts
- **DetailPanel** — Shows selected item properties (definition, layer, link type, UserText)
- **SearchFilterBar** — Case-insensitive filter on tree nodes

### Model Layer (`Model/`)
- **AssemblyNode** (abstract) — Base tree node with Id, Name, Children, visibility state
  - **DocumentNode** — Root node representing the active document
  - **BlockInstanceNode** — Block instance with definition info, nesting, instance numbering
  - **GeometryNode** — Loose geometry at top level
- **AssemblyTreeBuilder** — Recursively builds tree from RhinoDoc, groups by definition, assigns instance numbers
- Node IDs use stable Rhino Object GUIDs (`instance.Id`), with synthetic GUIDs for doc/definition-only nodes

### Services Layer (`Services/`)
- **SelectionSyncService** — Bidirectional sync between tree selection and viewport selection (debounced)
- **VisibilityService** — Manages show/hide/isolate using `RhinoObject.SetObjectHidden`; tracks doc via `RuntimeSerialNumber` (not direct reference — avoids stale-doc leak)
- **DocumentEventService** — Listens to Rhino events (add/delete/modify object), debounces tree rebuilds
- **BlockInfoService** — Queries block definitions, link types, instance counts

---

## C++ Native DLL Structure

### Source Files

| File | Purpose |
|------|---------|
| `NativeApi.h/.cpp` | 12 exported `extern "C"` functions — the P/Invoke surface |
| `VisibilityConduit.h/.cpp` | `CRhinoDisplayConduit` subclass intercepting SC_DRAWOBJECT |
| `VisibilityData.h` | Thread-safe state store: maps instance UUID → set of hidden component paths |
| `VisibilityUserData.h/.cpp` | `ON_UserData` subclass for .3dm persistence of hidden paths |
| `DocEventHandler.h/.cpp` | `CRhinoEventWatcher` for document lifecycle (open/save/close/delete) |

### VisibilityConduit (`VisibilityConduit.h/.cpp`)

Intercepts `SC_DRAWOBJECT` in the Rhino display pipeline. For each managed block instance:
1. Suppresses the default draw
2. Iterates definition objects
3. Skips components whose path is in the hidden set
4. Draws visible components with the instance transform via `dp.DrawObject()`
5. Handles nested blocks recursively with path-based filtering (`DrawNestedFiltered`)
6. Resolves component colors correctly
7. Max nesting depth: 32

### VisibilityData (`VisibilityData.h`)

Header-only, thread-safe state store using `CRITICAL_SECTION` with RAII `CAutoLock`.

- **Storage:** `unordered_map<ON_UUID, unordered_set<string>>` — instance ID → hidden component paths
- **Path format:** Dot-separated indices, e.g. `"0"`, `"1.0"`, `"1.0.2"` for nested blocks
- **Key methods:** `SetComponentHidden`, `SetComponentVisible`, `IsComponentHidden`, `HasHiddenDescendants`, `IsManaged`, `GetHiddenPaths`, `GetManagedInstanceIds`, `ClearAll`
- Custom `ON_UUID_Hash` and `ON_UUID_Equal` functors for UUID keys

### VisibilityUserData (`VisibilityUserData.h/.cpp`)

`ON_UserData`-derived class that persists hidden component paths in .3dm files.

- **UUID:** `{A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78}`
- **Archive format:** Chunked (TCODE_ANONYMOUS_CHUNK v1.0) — count + N strings
- **Data:** `unordered_set<string> HiddenPaths`
- **Sync helpers:**
  - `SyncFromVisData()` — copies hidden paths from CVisibilityData for saving
  - `SyncToVisData()` — restores hidden paths into CVisibilityData on load
- `Archive() = true` — data survives save/load
- `m_userdata_copycount = 1` — data copies with the object (copy/paste, duplicate)

### DocEventHandler (`DocEventHandler.h/.cpp`)

`CRhinoEventWatcher`-derived class handling document lifecycle:

| Event | Behavior |
|-------|----------|
| `OnEndOpenDocument` | Iterates all instance references, finds those with `CComponentVisibilityData` UserData, syncs hidden paths into `CVisibilityData` |
| `OnBeginSaveDocument` | Iterates managed instances, creates/updates `CComponentVisibilityData` UserData on each, removes empty UserData |
| `OnCloseDocument` | Calls `CVisibilityData::ClearAll()` to reset in-memory state |
| `OnDeleteObject` | If deleted object is a managed instance reference, removes its entry from `CVisibilityData` |

Registered and enabled in constructor. Self-registers via `CRhinoEventWatcher::Register()`.

---

## P/Invoke Bridge

**Calling convention:** `__stdcall` (matches .NET default for P/Invoke)  
**GUID marshalling:** `ref Guid` in C# → `const ON_UUID*` in C++. Binary-compatible, zero-copy.  
**String marshalling:** `string` in C# → `const char*` in C++ (ANSI/UTF-8 for path strings)  
**API version:** `NATIVE_API_VERSION = 3` (persistence + extended API)

The C# `NativeInterop` class wraps all 12 functions with error handling (catches `DllNotFoundException` for graceful degradation on platforms without the native DLL).

See [API_REFERENCE.md](API_REFERENCE.md) for complete function documentation.

---

## Data Flow: Per-Instance Component Visibility

### Hide Component Flow

```
User clicks eye icon on component in tree
    │
    ▼
AssemblyTreeView.OnVisibilityToggle(componentNode)
    │
    ▼
VisibilityService → NativeInterop.SetComponentVisibility(instanceId, path, false)
    │  P/Invoke boundary
    ▼
C++ SetComponentVisibility()
    ├── Validate params + state
    ├── CVisibilityData::SetComponentHidden(instanceId, path)
    │     └── CAutoLock(CRITICAL_SECTION)
    │     └── m_data[instanceId].insert(path)
    ├── RedrawActiveDoc()
    └── return true
            │
            ▼
        Display Pipeline runs
            │
            ▼
        CVisibilityConduit::ExecConduit(SC_DRAWOBJECT)
            ├── Check: is this instance managed? YES
            ├── Suppress normal draw
            ├── Iterate definition objects with index
            │   ├── path "0": not hidden → DrawComponent(dp, comp, xform)
            │   ├── path "1": HIDDEN → skip
            │   ├── path "2": nested block, has hidden descendants?
            │   │   └── YES → DrawNestedFiltered(dp, nested, xform, id, "2", depth+1)
            │   └── path "3": not hidden → DrawComponent(dp, comp, xform)
            └── Done
```

### Document Persistence Flow

```
Save: OnBeginSaveDocument
    ├── Get managed instance IDs from CVisibilityData
    ├── For each: create CComponentVisibilityData (ON_UserData)
    ├── SyncFromVisData() → copy hidden paths
    ├── Attach to object attributes
    └── ModifyObjectAttributes()

Open: OnEndOpenDocument
    ├── Iterate all instance_reference objects
    ├── Check for CComponentVisibilityData UserData
    ├── SyncToVisData() → restore hidden paths into CVisibilityData
    └── Conduit automatically draws with correct visibility
```

---

## State Management

### Three-Tier Visibility Model

```
Layer Visibility (Rhino-native — we don't touch)
    │ must be ON
    ▼
Instance Visibility (C# — existing eye toggle, uses RhinoObject.SetObjectHidden)
    │ must be ON
    ▼
Component Visibility (C++ — per-instance sub-component hiding via path)
    │ must be visible
    ▼
Object renders in viewport
```

### State Locations

| Location | What | Lifetime |
|----------|------|----------|
| `CVisibilityData` (C++ heap) | Instance UUID → hidden paths map | Runtime only |
| `CComponentVisibilityData` (ON_UserData) | Hidden paths per instance | Persisted in .3dm |
| `_rootNodes` / `_itemLookup` (C# panel) | Tree model, fast O(1) lookup by Rhino ID | Runtime only |
| `_isolateState` (C# panel) | Pre-isolate visibility backup | Runtime only |
| Document UserText `RAO_AssemblyRoot` | Assembly mode root GUID | Persisted in .3dm |
| Plugin Settings | DefaultMode, ShowGeometryNodes | Per-user |

---

## Graceful Degradation

If C++ DLL is not loaded (e.g., Mac, or missing DLL):

```
NativeInterop.SetComponentVisibility() → catches DllNotFoundException
    → VisibilityService falls back to instance-level visibility only
    → UI hides "component visibility" eye icons
    → Tree still works, selection sync still works, all v1.0 features intact
```

---

## Thread Safety

- **C++ side:** `CRITICAL_SECTION` with RAII `CAutoLock` in `CVisibilityData` — protects all reads/writes from concurrent render thread and UI thread access
- **C++ API:** Each exported function calls `AFX_MANAGE_STATE(AfxGetStaticModuleState())` for MFC state management
- **C# side:** UI operations marshalled to main thread; Rhino events debounced (100ms) to avoid rapid rebuilds
