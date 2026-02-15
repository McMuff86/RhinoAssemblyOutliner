# Code Review — Sprint 2

**Date:** 2026-02-15  
**Branch:** `nightly/15-02-sprint1-refactor`  
**Reviewer:** Sentinel (automated)  
**Scope:** Full C# + C++ codebase

---

## 🔴 Blocker

### B1 — P/Invoke Signature Mismatch: `ref Guid` vs `const ON_UUID*`
**Files:** `NativeVisibilityInterop.cs` (all DllImports), `NativeApi.h`/`NativeApi.cpp`  
**Problem:** C# declares `ref Guid instanceId` which marshals as a pointer to a managed Guid. The C++ side receives `const ON_UUID* instanceId`. `System.Guid` and `ON_UUID` are both 16-byte structs with identical layout — this works **only** because their memory layouts happen to match. However, `ON_UUID` on some Rhino SDK versions may have different packing or alignment. More critically, `ref Guid` pins the managed object — if the GC moves it during the native call (shouldn't happen with pinning, but the contract is fragile).  
**Fix:** Either explicitly use `[MarshalAs(UnmanagedType.LPStruct)]` or pass as `IntPtr` with manual marshaling. Alternatively, validate with a static assert in C++ that `sizeof(ON_UUID) == 16` and `offsetof` matches Guid layout.

### B2 — Duplicate `RAO_DOC_KEY` Static Variable → Linker ODR Violation
**File:** `NativeApi.cpp`, ~line 248 (second declaration)  
**Problem:** `RAO_DOC_KEY` is defined as `static const wchar_t*` twice in the same translation unit — once near the top (via `DocEventHandler.cpp` forward decl pattern) and again at line ~248 in `NativeApi.cpp`. This is a **compilation error** (redefinition of `RAO_DOC_KEY`).  
**Fix:** Remove the second `static const wchar_t* RAO_DOC_KEY = L"RAO_VisibilityState";` in `NativeApi.cpp` around line 248.

### B3 — `NativeCleanup()` Never Called → Memory Leak + Conduit Leak
**Files:** `RhinoAssemblyOutlinerPlugin.cs`, `NativeVisibilityInterop.cs`  
**Problem:** `NativeInit()` is called in `TestNativeVisibilityCommand` and `VisibilityService.InitializeNative()`, but `NativeCleanup()` is **never** called. The plugin's `OnLoad` doesn't init, and there's no `OnUnload` override that calls `NativeCleanup()`. This leaks `g_pVisData`, `g_pConduit`, and `g_pDocEventHandler` on plugin unload, and the conduit remains active.  
**Fix:** Override `OnUnloadPlugIn()` in `RhinoAssemblyOutlinerPlugin` and call `NativeVisibilityInterop.NativeCleanup()`.

### B4 — Plugin Event Handlers Never Unsubscribed → Event Leak
**File:** `RhinoAssemblyOutlinerPlugin.cs`, ~line 33-35  
**Problem:** `OnLoad` subscribes to `RhinoDoc.BeginOpenDocument`, `EndOpenDocument`, `CloseDocument` but there's no `OnUnload` that unsubscribes. These are static events — subscribing keeps the plugin instance alive and the handlers fire even after "unload".  
**Fix:** Add `protected override void OnUnloadPlugIn()` that unsubscribes all three events.

### B5 — `PerInstanceVisibilityService` Static Field → Stale Service After Doc Close
**File:** `TestPerInstanceVisibilityCommand.cs`, ~line 16-17  
**Problem:** `_service` and `_serviceDocSerial` are static fields. If the doc is closed and a new one opened with the same serial number (unlikely but possible), the old conduit reference is stale. More importantly, `_service` subscribes to `RhinoDoc.CloseDocument` — when the command is re-run with a new doc, `_service?.Dispose()` is called but only after the new doc is already open, meaning the event handler fires for the wrong doc.  
**Fix:** Use the document serial number properly, or reset `_service` on `CloseDocument` events.

---

## 🟡 Warning

### W1 — `PerInstanceVisibilityConduit` + Native `CVisibilityConduit` — Two Competing Conduits
**Files:** `PerInstanceVisibilityConduit.cs`, `VisibilityConduit.cpp`  
**Problem:** There are **two separate visibility systems**: the C# `PerInstanceVisibilityConduit` (using `UserData`-based `ComponentVisibilityData`) and the C++ `CVisibilityConduit` (using `CVisibilityData`). They use completely different data stores and could conflict if both are active. `VisibilityService` uses the native path; `PerInstanceVisibilityService` uses the C# path. If both are instantiated, they'll fight over draw suppression.  
**Fix:** Decide on one system. The native C++ conduit is the correct long-term choice. Mark the C# `PerInstanceVisibilityConduit` and `PerInstanceVisibilityService` as deprecated or remove them. At minimum, add a guard so they can't both be active.

### W2 — `AssemblyOutlinerPanel.Dispose()` Hides Base `Panel.Dispose(bool)`
**File:** `AssemblyOutlinerPanel.cs`, ~line 195  
**Problem:** `public new void Dispose()` hides the base `Panel.Dispose()`. This means if someone calls `((Panel)panel).Dispose()`, the cleanup code (unsubscribe, timer stop) is **not** called. The `new` keyword is a code smell here.  
**Fix:** Override `Dispose(bool disposing)` instead:
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing && !_disposed) { ... }
    base.Dispose(disposing);
}
```

### W3 — `_refreshTimer` Null After Dispose → Possible NRE
**File:** `AssemblyOutlinerPanel.cs`, ~line 200, 445  
**Problem:** `Dispose()` sets `_refreshTimer = null`, but `QueueRefresh()` accesses `_refreshTimer.Stop()` / `.Start()` without null check. If a Rhino event fires after dispose (which can happen during shutdown), this throws NRE.  
**Fix:** Add null check in `QueueRefresh()` or use `_refreshTimer?.Stop()`.

### W4 — Thread Safety: `RefreshTreeDebounced` Cross-Thread UI Access
**File:** `AssemblyOutlinerPanel.cs`, ~line 451  
**Problem:** `System.Timers.Timer.Elapsed` fires on a **thread pool thread**, but `RefreshTreeDebounced()` calls `RhinoApp.InvokeOnUiThread()` which is correct — however, `Interlocked.CompareExchange` + `InvokeOnUiThread` has a race: if two timer callbacks fire in quick succession, the second one sees `_needsRefresh == 0` and skips, but the first `InvokeOnUiThread` hasn't executed yet. This is mostly harmless (just a missed refresh) but worth noting.  
**Fix:** Acceptable as-is for debouncing; document the intentional behavior.

### W5 — `IAssemblyNode.Parent` — Non-Nullable Interface vs Nullable Implementation
**File:** `IAssemblyNode.cs`, ~line 20; `AssemblyNode.cs`, ~line 36-37  
**Problem:** The interface declares `AssemblyNode? Parent { get; set; }` (nullable), but the explicit implementation `AssemblyNode IAssemblyNode.Parent { get => Parent!; ... }` uses null-forgiving operator. If `Parent` is null (root nodes), accessing via interface throws NRE.  
**Fix:** Make the interface property nullable: `AssemblyNode? Parent { get; set; }` (it already has `?` — remove the `!` in the explicit implementation).

### W6 — `ComponentVisibilityData._componentVisibility` — Dead Field
**File:** `ComponentVisibilityData.cs`, ~line 14  
**Problem:** `private Dictionary<int, bool> _componentVisibility` is initialized, duplicated in `OnDuplicate`, but **never read or written** outside of duplication. All actual logic uses `HiddenComponents` HashSet. Dead code.  
**Fix:** Remove `_componentVisibility` field entirely.

### W7 — `DetailPanel` Uses `RhinoDoc.ActiveDoc` Instead of Panel's Doc Reference
**File:** `DetailPanel.cs`, ~line 89, 101  
**Problem:** `OnSelectAllClick` and `OnZoomClick` use `RhinoDoc.ActiveDoc` directly, while the rest of the plugin uses `_documentSerialNumber` + `RhinoDoc.FromRuntimeSerialNumber()`. In multi-doc scenarios this could target the wrong document.  
**Fix:** Pass doc serial number or a `Func<RhinoDoc>` to `DetailPanel`.

### W8 — `SelectionSyncService` — Constructed But Never Used
**File:** `SelectionSyncService.cs`  
**Problem:** `SelectionSyncService` is a complete implementation, but it's **never instantiated** anywhere. The panel does its own inline selection sync. Dead code.  
**Fix:** Either integrate it into the panel or remove it.

### W9 — `AssemblyTreeBuilder._visitedDefinitions` — Instance Reuse Issue
**File:** `AssemblyTreeBuilder.cs`, ~line 77  
**Problem:** `_visitedDefinitions` is a field reinitialized in `BuildTree()` / `BuildTreeFromRoot()`, but `RefreshSubtree()` never initializes it. If `RefreshSubtree()` is called before `BuildTree()`, `_visitedDefinitions` is null → NRE.  
**Fix:** Initialize `_visitedDefinitions` in the constructor or at declaration, or add null check in `RefreshSubtree`.

### W10 — `ON_UUID_Hash` — Potential UB on 32-bit Platforms
**File:** `VisibilityData.h`, ~line 34-38  
**Problem:** `reinterpret_cast<const size_t*>(&id)` and accessing `p[1]` assumes `size_t` is 8 bytes. On 32-bit builds, `size_t` is 4 bytes, so `p[1]` only reads the second 4 bytes, not bytes 8-15. The hash quality degrades but doesn't crash. However, the multiplication constant `0x9e3779b97f4a7c15ULL` truncates silently.  
**Fix:** Add `static_assert(sizeof(size_t) >= 8)` or provide a 32-bit fallback. Since Rhino 8 is x64-only, this is low risk.

### W11 — `CAutoLock` Used With `mutable CRITICAL_SECTION` in `const` Methods
**File:** `VisibilityData.h`, ~line 145+  
**Problem:** `GetState`, `IsComponentHidden`, `HasHiddenDescendants`, `TakeSnapshot` etc. are `const` methods that take `CAutoLock lock(m_cs)`. This works because `m_cs` is `mutable`, but `CAutoLock` takes `CRITICAL_SECTION&` (non-const ref). The `mutable` makes this compile, but it's a design smell — reader locks vs writer locks would be more appropriate for this read-heavy pattern.  
**Fix:** Consider `SRWLOCK` with `AcquireSRWLockShared`/`Exclusive` for better read concurrency. Low priority since contention is likely minimal.

### W12 — `VisibilityConduit` — `CS_TRANSPARENT` Treated Same as Visible
**File:** `VisibilityConduit.cpp`, ~line 110-118, ~line 195  
**Problem:** Multiple `TODO` comments noting that `CS_TRANSPARENT` is drawn identically to `CS_VISIBLE`. The state exists in the enum and data structures but has no visual effect. Users might set components to transparent and see no change.  
**Fix:** Either implement transparency (display mode override or custom material) or remove `CS_TRANSPARENT` from the enum until it's implemented. At minimum, document the limitation.

### W13 — `PerInstanceVisibilityConduit.DrawNestedInstance` — Transform Order May Be Wrong
**File:** `PerInstanceVisibilityConduit.cs`, ~line 207  
**Problem:** `var combinedXform = nestedRef.Xform * parentXform;` — matrix multiplication order matters. In Rhino, the convention is `parentXform * childXform` (parent first). The C++ code uses `parentXform * pNestedInstance->InstanceXform()`. The C# conduit has them reversed.  
**Fix:** Change to `parentXform * nestedRef.Xform`. (This conduit should be deprecated per W1, but fix if kept.)

---

## 🟢 Info

### I1 — `OpenOutlinerCommand` and `RefreshOutlinerCommand` Both Register Panel
**Files:** `OpenOutlinerCommand.cs`, `RefreshOutlinerCommand.cs`  
**Problem:** Both constructors call `Panels.RegisterPanel()`. The comment says "first registration wins" which is correct — this is harmless but redundant.  
**Fix:** Register in only one place (e.g., `OnLoad` or `OpenOutlinerCommand`).

### I2 — `GetComponentColor` in `VisibilityConduit.cpp` — Never Called
**File:** `VisibilityConduit.cpp` / `.h`  
**Problem:** `GetComponentColor()` is implemented but never called from anywhere. It was likely intended for manual wireframe drawing but `DrawObject` handles colors internally.  
**Fix:** Remove or mark as future-use.

### I3 — Test Classes Use `TestNode` Instead of Real `AssemblyNode`
**File:** `AssemblyNodeTests.cs`  
**Problem:** Tests duplicate the node logic in a `TestNode` class rather than testing the actual `AssemblyNode`. If `AssemblyNode` behavior diverges from `TestNode`, tests pass but code is wrong.  
**Fix:** If possible, test against `AssemblyNode` subclasses. The current approach is reasonable for avoiding RhinoCommon dependency in tests, but document the limitation.

### I4 — `KeyboardShortcutTests` — All Tests Are `Assert.True(true)`
**File:** `KeyboardShortcutTests.cs`  
**Problem:** These are documentation-only placeholder tests. They always pass and test nothing.  
**Fix:** Either convert to actual integration tests or move to a markdown doc. Not harmful.

### I5 — `AssemblyNode.AddChild` Doesn't Remove from Previous Parent
**File:** `AssemblyNode.cs`, ~line 75  
**Problem:** Calling `AddChild` on a new parent doesn't remove the child from its old parent's `Children` list. The test `AddChild_OverwritesPreviousParentReference` explicitly documents this behavior. This can lead to a node appearing in two parents' child lists.  
**Fix:** Add `child.Parent?.Children.Remove(child)` before `child.Parent = this`. Low priority if reparenting isn't used.

### I6 — `BlockInstanceNode.GuidFromDefinitionIndex` — Collision Risk
**File:** `BlockInstanceNode.cs`, ~line 103  
**Problem:** Creates a Guid from definition index with `bytes[15] = 0xDE`. This is a synthetic ID that could theoretically collide with real Rhino object IDs (astronomically unlikely).  
**Fix:** Use a proper namespace UUID (UUID v5) for deterministic IDs. Low priority.

### I7 — `VisibilityUserData` — `HiddenPaths` Only Stores Hidden/Suppressed, Not Transparent
**File:** `VisibilityUserData.cpp`, `SyncFromVisData`  
**Problem:** `GetHiddenPaths` only returns `CS_HIDDEN` and `CS_SUPPRESSED` states. `CS_TRANSPARENT` components are not persisted via UserData. If the UserData path is used for persistence (it exists alongside the doc-string approach), transparent state is lost.  
**Fix:** Either extend to serialize all non-visible states or document that UserData path is deprecated in favor of doc-string serialization.

### I8 — Missing `using System;` in Test Files
**Files:** `ComponentPathTests.cs`, `VisibilityStateTests.cs`  
**Problem:** Tests use `Array.Empty<int>()` and `Guid.NewGuid()` but rely on implicit global usings. If `<ImplicitUsings>` is disabled in the test project, these won't compile.  
**Fix:** Add explicit `using System;` and `using System.Linq;` to test files for robustness.

---

## Summary

| Severity | Count | Key Themes |
|----------|-------|------------|
| 🔴 Blocker | 5 | P/Invoke safety, memory leaks, event leaks, duplicate symbol |
| 🟡 Warning | 13 | Dual conduit systems, dead code, thread safety, dispose patterns |
| 🟢 Info | 8 | Redundancies, placeholder tests, minor correctness |

**Top 3 Priorities Before v1.0:**
1. Fix B2 (won't compile) and B3/B4 (resource leaks)
2. Resolve W1 — pick one visibility system (native C++) and deprecate the other
3. Fix W2/W3 — proper dispose pattern to avoid crashes during shutdown
