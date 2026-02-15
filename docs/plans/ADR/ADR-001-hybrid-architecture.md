# ADR-001: Hybrid C++/C# Architecture

**Status:** Accepted  
**Date:** 2026-02-15  
**Context:** Per-instance component visibility requires deeper display pipeline access than RhinoCommon provides.

## Decision

Split the plugin into two binaries:

| Layer | Language | Responsibilities |
|-------|----------|-----------------|
| **UI + Commands + Model** | C# (.rhp) | Eto.Forms panel, tree view, selection sync, commands, tree building, event handling |
| **Display + Persistence** | C++ (.rhp) | Display conduit (SC_DRAWOBJECT), ON_UserData serialization, display cache management |

Communication via **P/Invoke** (`extern "C"` functions, ~10 exports).

## Why C++ for Display

The C# PoC proved three things cannot be solved in managed code:
1. **Ghost artifacts** — `PreDrawObject` + custom draw doesn't integrate with Rhino's screen invalidation regions
2. **No display cache** — C# conduit allocates geometry every frame (1200 allocs/sec per instance at 60fps)
3. **Selection highlight suppressed** — overriding draw in C# loses Rhino's highlight pass

C++ `SC_DRAWOBJECT` channel with `return false` replaces the object's draw call *within* the pipeline, so invalidation, caching, and highlight passes work correctly.

## Why C# for Everything Else

- Eto.Forms is C#-native; building UI in C++ would be painful and fragile
- RhinoCommon provides clean APIs for tree building, selection, events
- Faster iteration cycle for UI changes
- Lower barrier for contributors

## What Lives Where

```
C# Plugin:
  ├── UI/          → Panel, TreeView, DetailPanel, SearchFilterBar
  ├── Commands/    → OpenOutliner, RefreshOutliner
  ├── Model/       → AssemblyNode hierarchy, AssemblyTreeBuilder
  ├── Services/    → SelectionSync, Visibility (delegates to C++), DocumentEvents, BlockInfo
  └── Interop/     → NativeInterop (P/Invoke wrapper with error handling)

C++ Plugin:
  ├── CPerInstanceVisibilityConduit  → SC_DRAWOBJECT interception
  ├── CComponentVisibilityData       → ON_UserData (UUID-based hidden set)
  ├── Display cache management       → CRhinoCacheHandle per managed instance
  └── extern "C" API                 → 10 exported functions
```

## P/Invoke API Surface

```cpp
extern "C" {
    bool RAO_Initialize();
    void RAO_Shutdown();
    bool RAO_EnableConduit();
    void RAO_DisableConduit();
    bool RAO_SetComponentHidden(const GUID* instanceId, const GUID* componentId, bool hidden);
    bool RAO_IsComponentHidden(const GUID* instanceId, const GUID* componentId);
    void RAO_ShowAllComponents(const GUID* instanceId);
    int  RAO_GetHiddenCount(const GUID* instanceId);
    int  RAO_GetHiddenComponentIds(const GUID* instanceId, GUID* outBuffer, int bufferSize);
    void RAO_InvalidateInstance(const GUID* instanceId);
}
```

`System.Guid` and `ON_UUID` are binary-compatible (16 bytes, same layout). No conversion needed.

## Loading Strategy

1. C++ `.rhp` registered as Rhino plugin, loaded by plugin manager
2. C# plugin calls `RAO_Initialize()` on first use
3. All P/Invoke calls wrapped with `DllNotFoundException` / `EntryPointNotFoundException` handlers
4. If C++ plugin unavailable, C# degrades gracefully (instance-level visibility only, no component-level)

## Consequences

- **Build complexity increases** — two projects, two toolchains (MSBuild C# + MSVC v142 C++)
- **Single VS solution** mitigates: C# project depends on C++ project
- **Deployment** — both .rhp files must ship together (Yak package bundles both)
- **Mac support deferred** — C++ SDK is Windows-only initially; C# plugin works on Mac without component visibility

## Alternatives Considered

1. **Pure C#** — Rejected. PoC proved display artifacts unsolvable.
2. **Pure C++** — Rejected. UI development too slow, no Eto.Forms.
3. **COM interop** — Rejected. P/Invoke is simpler, lower overhead, sufficient for this API surface.
