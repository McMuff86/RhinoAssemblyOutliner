# ADR-005: Threading Model

**Status:** Accepted  
**Date:** 2026-02-15

## Context: Rhino's Threading Rules

1. RhinoCommon API calls → **UI thread only**
2. `RhinoDoc` operations → **not thread-safe**
3. Display conduit callbacks → **display thread** (NOT UI thread)
4. Rhino events (SelectObjects, etc.) → **UI thread**
5. `System.Timers.Timer` callbacks → **ThreadPool thread**

## Decision

### C# Side

| Operation | Thread | Sync Mechanism |
|-----------|--------|---------------|
| UI updates (tree, panel) | UI thread | `RhinoApp.InvokeOnUiThread` |
| RhinoDoc read/write | UI thread | Already on UI thread via event handlers |
| Timer callbacks | ThreadPool → marshal | `Interlocked` for flags, `InvokeOnUiThread` for actions |
| P/Invoke calls | UI thread | Call from UI thread; never from timer/background |

**Fix current `_needsRefresh` race:**
```csharp
// Current: unsynchronized bool read/written from multiple threads
// Fix: use Interlocked
private int _needsRefresh; // 0 or 1
Interlocked.Exchange(ref _needsRefresh, 1); // set
if (Interlocked.CompareExchange(ref _needsRefresh, 0, 1) == 1) { ... } // test-and-clear
```

### C++ Side

| Operation | Thread | Sync Mechanism |
|-----------|--------|---------------|
| ExecConduit (read managed set) | Display thread | `std::shared_lock<std::shared_mutex>` |
| RegisterInstance / SetHidden (write) | UI thread (via P/Invoke) | `std::unique_lock<std::shared_mutex>` |
| Cache invalidation | UI thread | Under unique_lock |

```cpp
class CPerInstanceVisibilityConduit : public CRhinoDisplayConduit {
    mutable std::shared_mutex m_mutex;
    std::unordered_map<ON_UUID, ManagedInstanceState, UUIDHash> m_state;
    
    // Display thread — high frequency, read-only
    bool ExecConduit(...) override {
        std::shared_lock lock(m_mutex);
        // ... read m_state
    }
    
    // UI thread — low frequency, write
    void SetComponentHidden(const ON_UUID& inst, const ON_UUID& comp, bool hidden) {
        std::unique_lock lock(m_mutex);
        // ... modify m_state
    }
};
```

`shared_mutex` chosen because reads (every frame, display thread) vastly outnumber writes (user clicks, UI thread).

### Cross-Boundary Rules

1. **Never call P/Invoke from a timer callback** — always marshal to UI thread first
2. **Never call `doc.Views.Redraw()` from a conduit callback** — deadlock risk
3. **Never access `RhinoDoc.ActiveDoc` from display thread** — use captured serial number
4. **Guard against re-entrancy** — event handlers use `_isProcessing` flags (already partially implemented)

### GlimpseAI Crash Pattern Avoidance

Known crash patterns to avoid:
- Accessing RhinoDoc from background threads → all doc access via UI thread
- Modifying conduit state during rendering → shared_mutex guards
- Event re-entrancy → `_isSyncing` guard flags

## Consequences

- Display thread never blocks on UI thread (shared lock only)
- UI thread may briefly block on display thread for writes (acceptable — writes are rare)
- All state mutations happen on UI thread, making reasoning simple
- `_needsRefresh` race condition eliminated via Interlocked
