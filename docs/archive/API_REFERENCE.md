# Native API Reference

> C exports from `RhinoAssemblyOutliner.native.dll`  
> API Version: **4** (ComponentState enum + conduit improvements)  
> Calling convention: `__stdcall`  
> Header: `NativeApi.h`

---

## Lifecycle

### `NativeInit`

```cpp
bool __stdcall NativeInit();
```

Initialize the native module. Creates `CVisibilityData`, `CVisibilityConduit`, and `CDocEventHandler`. Enables the conduit on the active document. Safe to call multiple times (no-op after first init).

- **Returns:** `true` on success, `true` if already initialized
- **Call from:** C# `OnLoadPlugIn()`
- **Note:** Requires an active `RhinoDoc` — will crash if called before any document is open

---

### `NativeCleanup`

```cpp
void __stdcall NativeCleanup();
```

Cleanup the native module. Disables and deletes the conduit, event handler, and visibility data. No-op if not initialized.

- **Call from:** C# `OnUnloadPlugIn()`

---

## Visibility Operations

### `SetComponentVisibility`

```cpp
bool __stdcall SetComponentVisibility(
    const ON_UUID* instanceId,   // Rhino object GUID of the block instance
    const char*    componentPath, // Dot-separated component index path
    bool           visible        // true = show, false = hide
);
```

Hide or show a specific component within a block instance. Triggers an immediate viewport redraw.

- **componentPath format:** `"0"`, `"1"`, `"1.0"`, `"1.0.2"` — dot-separated indices into nested block definitions
- **Returns:** `true` on success, `false` if not initialized or null params
- **Thread safety:** Acquires CRITICAL_SECTION internally
- **C# marshalling:** `ref Guid instanceId`, `string componentPath`, `bool visible`

---

### `IsComponentVisible`

```cpp
bool __stdcall IsComponentVisible(
    const ON_UUID* instanceId,
    const char*    componentPath
);
```

Query whether a component is currently visible for a given instance.

- **Returns:** `true` if visible (or if not initialized / null params — fail-open)
- **C# marshalling:** `ref Guid instanceId`, `string componentPath`

---

### `GetHiddenComponentCount`

```cpp
int __stdcall GetHiddenComponentCount(
    const ON_UUID* instanceId
);
```

Get the number of hidden component paths for a specific instance.

- **Returns:** Count of hidden paths, or `0` if not managed / not initialized

---

### `ResetComponentVisibility`

```cpp
void __stdcall ResetComponentVisibility(
    const ON_UUID* instanceId
);
```

Show all components for a specific instance (remove all hidden paths). Triggers redraw.

- **Effect:** Instance is no longer "managed" by the conduit after this call

---

## Persistence

### `PersistVisibilityState`

```cpp
void __stdcall PersistVisibilityState();
```

Save current in-memory visibility state to ON_UserData on all managed instances in the active document. Attaches `CComponentVisibilityData` to each object's attributes. Removes UserData from instances with no hidden components.

- **When to call:** Before save, or manually. Also called automatically by `CDocEventHandler::OnBeginSaveDocument`.
- **Note:** Modifies object attributes via `ModifyObjectAttributes` — creates undo records

---

### `LoadVisibilityState`

```cpp
void __stdcall LoadVisibilityState();
```

Load visibility state from ON_UserData on all instance references in the active document into in-memory `CVisibilityData`. Also called automatically by `CDocEventHandler::OnEndOpenDocument`.

- **When to call:** After document open, or manually to re-sync

---

## Query

### `GetManagedInstances`

```cpp
int __stdcall GetManagedInstances(
    ON_UUID* buffer,   // Caller-allocated buffer for instance UUIDs (nullable)
    int      maxCount  // Buffer capacity
);
```

Get all instance IDs currently managed by the visibility system.

- **Returns:** Total count of managed instances (regardless of buffer size)
- **Usage pattern:** Call with `buffer=NULL, maxCount=0` to get count, allocate, call again
- **C# marshalling:** `Guid[] buffer`, `int maxCount`

---

### `IsConduitEnabled`

```cpp
bool __stdcall IsConduitEnabled();
```

Check whether the display conduit is currently active.

- **Returns:** `true` if conduit is enabled, `false` if not initialized or disabled

---

## Diagnostics

### `SetDebugLogging`

```cpp
void __stdcall SetDebugLogging(bool enabled);
```

Enable or disable debug output to the Rhino command line from the conduit.

- **Default:** disabled

---

### `GetNativeVersion`

```cpp
int __stdcall GetNativeVersion();
```

Get the native API version number for compatibility checks.

- **Returns:** `4` (current version)
- **Version history:**
  - 1: Basic conduit + visibility
  - 2: Extended query API
  - 3: Persistence (UserData) + doc events
  - 4: ComponentState enum + snapshot pattern + conduit improvements

---

## Component State

### `SetComponentState`

```cpp
bool __stdcall SetComponentState(
    const ON_UUID* instanceId,   // Rhino object GUID of the block instance
    const char*    path,         // Dot-separated component index path
    int            state         // ComponentState enum value
);
```

Set the state of a specific component within a block instance. Replaces simple show/hide with a richer state model. Triggers an immediate viewport redraw.

- **state values:**
  - `0` = **Visible** — normal rendering (default)
  - `1` = **Hidden** — visually hidden, still contributes to bounding box and BOM
  - `2` = **Suppressed** — excluded from rendering, bounding box, BOM, and export
  - `3` = **Transparent** — drawn with ~30% alpha transparency
- **Returns:** `true` on success, `false` if not initialized, null params, or invalid state value
- **Thread safety:** Acquires CRITICAL_SECTION internally
- **C# marshalling:** `ref Guid instanceId`, `string path`, `int state`

---

### `GetComponentState`

```cpp
int __stdcall GetComponentState(
    const ON_UUID* instanceId,
    const char*    path
);
```

Query the current state of a component within a block instance.

- **Returns:** `ComponentState` enum value (0–3), defaults to `0` (Visible) if not found or not initialized
- **C# marshalling:** `ref Guid instanceId`, `string path`

---

### ComponentState Enum

| Value | Name | Rendering | BBox | BOM | Description |
|-------|------|-----------|------|-----|-------------|
| 0 | `CS_VISIBLE` | ✅ Normal | ✅ | ✅ | Default state |
| 1 | `CS_HIDDEN` | ❌ Hidden | ✅ | ✅ | Visual-only hide |
| 2 | `CS_SUPPRESSED` | ❌ Hidden | ❌ | ❌ | Structural exclusion |
| 3 | `CS_TRANSPARENT` | ✅ Alpha | ✅ | ✅ | Semi-transparent (~30% opacity) |

---

## Global State

The native DLL maintains three global singletons created by `NativeInit()`:

| Object | Type | Purpose |
|--------|------|---------|
| `g_pVisData` | `CVisibilityData*` | Thread-safe visibility state store |
| `g_pConduit` | `CVisibilityConduit*` | Display pipeline conduit |
| `g_pDocEventHandler` | `CDocEventHandler*` | Document lifecycle events |

All are destroyed by `NativeCleanup()`.

---

## Error Handling

- All functions check `g_initialized` and return safe defaults (`false`, `0`, or no-op) if not initialized
- Null pointer parameters return safe defaults (fail-open for queries, fail-silent for mutations)
- Every function calls `AFX_MANAGE_STATE(AfxGetStaticModuleState())` for correct MFC state
- C# side should catch `DllNotFoundException` for graceful degradation without native DLL
