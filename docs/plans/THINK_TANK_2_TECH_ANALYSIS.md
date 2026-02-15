# Think Tank 2: Deep Technical Architecture Analysis

**Date:** 2026-02-15  
**Scope:** Production readiness assessment for RhinoAssemblyOutliner  
**Status:** Complete

---

## Table of Contents

1. [Current Code Quality Assessment](#1-current-code-quality-assessment)
2. [C++/C# Hybrid Architecture](#2-cc-hybrid-architecture)
3. [Per-Instance Visibility Production Plan](#3-per-instance-visibility-production-plan)
4. [Performance Architecture](#4-performance-architecture)
5. [Thread Safety](#5-thread-safety)
6. [Data Persistence](#6-data-persistence)
7. [Testing Strategy](#7-testing-strategy)
8. [Risk Assessment](#8-risk-assessment)

---

## 1. Current Code Quality Assessment

### What's Solid ✅

**Model Layer** — Clean, well-structured:
- `AssemblyNode` abstract base is a good foundation with proper parent-child relationships
- `BlockInstanceNode` captures all relevant Rhino data (transform, link type, user attributes)
- `DocumentNode` provides clean document-level metadata
- `AssemblyTreeBuilder` has proper recursion guards (`MaxRecursionDepth = 100`, `_visitedDefinitions`)
- Good separation between data model and UI

**Event Debouncing** — The panel correctly debounces document events with a 100ms `System.Timers.Timer`, preventing UI thrashing during batch operations.

**Selection Sync** — Bidirectional sync with `_isSyncingFromViewport` / `_isSyncingFromTree` flags prevents infinite loops. This is the correct pattern.

**Error Handling** — Consistent try-catch with `RhinoApp.WriteLine` logging throughout tree building. Gracefully returns empty trees on failure.

### What Needs Refactoring ⚠️

#### 1.1 `AssemblyNode.Id` is a Random Guid — Not the Rhino Object ID

```csharp
// Current: generates a NEW Guid every time
public AssemblyNode(string displayName)
{
    Id = Guid.NewGuid();  // ← Problem!
```

This means `_itemLookup` in `AssemblyTreeView` is keyed on ephemeral IDs that change every rebuild. `SelectNodeByObjectId` works around this by doing a full tree search via `FindNodeByObjectId`, but it's O(n) on every viewport selection event.

**Fix:** For `BlockInstanceNode`, use `InstanceId` as the lookup key. For `DocumentNode`, use `doc.RuntimeSerialNumber`-derived key. The base `Id` should be the stable Rhino identity.

#### 1.2 `_visitedDefinitions` HashSet is Never Used

The `AssemblyTreeBuilder` declares `_visitedDefinitions` but never checks it during recursion. The `MaxRecursionDepth` catches self-referencing blocks, but only after 100 stack frames. A proper cycle detection would be:

```csharp
private BlockInstanceNode? CreateBlockInstanceNode(InstanceObject instance, int depth = 0)
{
    // ...
    if (!_visitedDefinitions.Add(definition.Index))
    {
        // Already visiting this definition — cycle detected
        return null; // or create a placeholder node
    }
    try { /* process children */ }
    finally { _visitedDefinitions.Remove(definition.Index); }
}
```

#### 1.3 `ObservableCollection` on `AssemblyNode.Children` — Unnecessary Overhead

`ObservableCollection<T>` fires `CollectionChanged` events on every add/remove. Since the tree is rebuilt from scratch (not incrementally updated), a plain `List<T>` would be cheaper. The UI doesn't bind to collection changes — it calls `LoadTree()` which recreates all `TreeGridItem` objects anyway.

#### 1.4 Duplicate Panel Registration

Both `OpenOutlinerCommand` and `RefreshOutlinerCommand` call `Panels.RegisterPanel(...)`. While "first registration wins," this is a code smell. Move registration to `RhinoAssemblyOutlinerPlugin.OnLoad()`.

#### 1.5 `VisibilityService` Created Lazily Per-Panel, Never Disposed

```csharp
private void EnsureVisibilityService()
{
    var doc = RhinoDoc.ActiveDoc;
    if (doc != null && _visibilityService == null)
        _visibilityService = new VisibilityService(doc);
}
```

If the active document changes, this service still holds the old doc reference. There's no cleanup in `PanelClosing`.

#### 1.6 `ComponentVisibilityData` Uses Index-Based Storage

The C# PoC stores hidden components by `int componentIndex`. As documented in ASSEMBLY_WORKFLOW_DESIGN.md §1.4, this is fragile — indices shift when block definitions are edited. The C++ implementation correctly plans to use UUIDs. **Do not carry the index-based approach forward.**

#### 1.7 `PerInstanceVisibilityConduit` — Geometry Duplication Per Frame

```csharp
var dupGeom = geom.Duplicate();
dupGeom.Transform(xform);
```

This allocates new geometry objects **every frame** for every visible component of every managed instance. For a block with 20 components drawn at 60fps, that's 1200 allocations/second per instance. This is the primary cause of the PoC's performance issues beyond the ghost artifacts.

#### 1.8 Missing `IDisposable` on Panel

`AssemblyOutlinerPanel` creates a `System.Timers.Timer` but only disposes it in `PanelClosing`. If the panel is garbage collected without `PanelClosing` being called, the timer leaks. The panel should implement `IDisposable`.

### Anti-Patterns Found

| Issue | Location | Severity |
|-------|----------|----------|
| Static mutable state | `TestPerInstanceVisibilityCommand._service` | Medium — lives forever, holds doc reference |
| `RhinoDoc.ActiveDoc` usage scattered | Panel, TreeView, DetailPanel | Medium — can be null, can change |
| `new event` keyword hiding base | `AssemblyTreeView.SelectionChanged` | Low — works but confusing |
| String-based display mode detection | `viewportMode.EnglishName.ToLower().Contains("wireframe")` | Medium — brittle |
| No null check on `definition.GetReferences(1)` | Multiple locations | Low |

---

## 2. C++/C# Hybrid Architecture

### 2.1 Architecture Overview

```
┌────────────────────────────────┐     ┌────────────────────────────────┐
│     C# Plugin (.rhp/.dll)      │     │     C++ Plugin (.rhp/.dll)     │
│                                │     │                                │
│  UI (Eto.Forms)                │     │  CPerInstanceVisibilityConduit │
│  Commands                      │     │  CComponentVisibilityData      │
│  Model (AssemblyNode tree)     │     │  (ON_UserData)                 │
│  Services                      │     │  Display Cache Management      │
│  NativeInterop (P/Invoke)  ────┼─────┤► extern "C" API               │
│                                │     │                                │
└────────────────────────────────┘     └────────────────────────────────┘
```

### 2.2 P/Invoke Bridge Design

**Critical Design Decision: Separate DLL vs. Rhino Plugin**

The C++ component is a `.rhp` file (Rhino plugin). P/Invoke via `[DllImport]` works with `.rhp` files since they're standard DLLs. However, the DLL must be loaded before P/Invoke calls work.

**Loading Strategy:**
1. C++ `.rhp` is registered as a Rhino plugin and loaded by Rhino's plugin manager
2. C# plugin calls `RhinoApp.GetPlugInObject()` or uses a known-loaded check before first P/Invoke
3. Alternatively, export an `Initialize()` function that the C# side calls first

**Recommended API surface** (minimal, stable):

```cpp
extern "C" {
    // Lifecycle
    __declspec(dllexport) bool __cdecl RAO_Initialize();
    __declspec(dllexport) void __cdecl RAO_Shutdown();
    
    // Conduit
    __declspec(dllexport) bool __cdecl RAO_EnableConduit();
    __declspec(dllexport) void __cdecl RAO_DisableConduit();
    
    // Visibility (all ops take instance GUID + component GUID)
    __declspec(dllexport) bool __cdecl RAO_SetComponentHidden(
        const GUID* instanceId, const GUID* componentId, bool hidden);
    __declspec(dllexport) bool __cdecl RAO_IsComponentHidden(
        const GUID* instanceId, const GUID* componentId);
    __declspec(dllexport) void __cdecl RAO_ShowAllComponents(const GUID* instanceId);
    __declspec(dllexport) int  __cdecl RAO_GetHiddenCount(const GUID* instanceId);
    
    // Batch: get all hidden component GUIDs for an instance
    __declspec(dllexport) int __cdecl RAO_GetHiddenComponentIds(
        const GUID* instanceId, GUID* outBuffer, int bufferSize);
    
    // Force redraw
    __declspec(dllexport) void __cdecl RAO_InvalidateInstance(const GUID* instanceId);
}
```

**C# wrapper:**

```csharp
internal static class NativeInterop
{
    private const string DllName = "RhinoAssemblyOutliner.native.rhp";
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool RAO_SetComponentHidden(
        ref Guid instanceId, ref Guid componentId, 
        [MarshalAs(UnmanagedType.I1)] bool hidden);
    // ...
}
```

### 2.3 Data Structure Marshalling

**GUID/ON_UUID:** `System.Guid` and `ON_UUID` are binary-compatible (both 16 bytes, same layout). Pass by `ref` in C# → pointer in C++. No conversion needed.

**Strings:** Avoid passing strings across the boundary. If needed, use `wchar_t*` with `CharSet.Unicode`. But the API above avoids strings entirely — component names are looked up on the C# side via RhinoCommon.

**Arrays:** For `RAO_GetHiddenComponentIds`, use the "call twice" pattern: first call with `bufferSize=0` returns count, second call with allocated buffer fills it. Or combine with `RAO_GetHiddenCount`.

### 2.4 Lifecycle Management

```
C# Plugin OnLoad
    → RAO_Initialize() — creates conduit, registers with Rhino
    → RAO_EnableConduit() — enables display pipeline hook

Document Open
    → C# scans for instances with UserData
    → For each: RAO_SetComponentHidden for each hidden component
    
User hides component via UI
    → C# calls RAO_SetComponentHidden
    → C++ updates UserData on instance object
    → C++ registers instance as managed in conduit
    → C++ calls RhinoDoc::Redraw()
    
Document Save
    → UserData serialized automatically by Rhino (ON_UserData::Write)
    
C# Plugin OnUnload
    → RAO_DisableConduit()
    → RAO_Shutdown()
```

**Key point:** The C++ side owns all state. The C# side is purely a UI/command layer that calls into C++. This avoids state synchronization bugs.

### 2.5 Error Handling Across the Boundary

Every P/Invoke call should be wrapped:

```csharp
public static bool SetComponentHidden(Guid instanceId, Guid componentId, bool hidden)
{
    try
    {
        return NativeInterop.RAO_SetComponentHidden(ref instanceId, ref componentId, hidden);
    }
    catch (DllNotFoundException)
    {
        RhinoApp.WriteLine("RAO: Native plugin not loaded");
        return false;
    }
    catch (EntryPointNotFoundException)
    {
        RhinoApp.WriteLine("RAO: Native API version mismatch");
        return false;
    }
}
```

On the C++ side, every exported function should have a top-level try-catch to prevent native exceptions from propagating to managed code (which would crash Rhino).

---

## 3. Per-Instance Visibility Production Plan

### 3.1 Ghost Artifacts — Root Cause and Fix

**Root cause (C# PoC):** `e.DrawObject = false` in `PreDrawObject` prevents Rhino from drawing, but Rhino's screen invalidation doesn't know about our custom-drawn geometry. When the object moves, Rhino invalidates only the region where the *original* object was, not where our conduit drew.

**C++ Fix:** In `SC_DRAWOBJECT`, `return false` actually replaces the object's draw call within Rhino's display pipeline. The pipeline tracks what was drawn and where, so invalidation works correctly. This is a fundamentally different mechanism than the C# `PreDrawObject` approach.

**Verification steps:**
1. Create minimal C++ conduit that intercepts a block and draws it shifted 10 units
2. Move the block → verify no ghost at old position
3. Rotate viewport → verify no artifacts

### 3.2 Selection Highlight — Implementation Plan

**Problem:** When we override drawing via `SC_DRAWOBJECT` returning false, we also suppress Rhino's selection highlight (the yellow wireframe overlay).

**Solution:** Check `m_pChannelAttrs->m_pObject->IsSelected()` in the conduit. If selected, draw with highlight color:

```cpp
bool ExecConduit(CRhinoDisplayPipeline& dp, UINT nChannel, bool& bTerminate)
{
    // ... (intercept and custom draw)
    
    bool isSelected = (m_pChannelAttrs->m_pObject->IsSelected() != 0);
    bool isHighlighted = m_pChannelAttrs->m_bDrawObject; // check highlight pass
    
    for (int i = 0; i < idef->ObjectCount(); i++)
    {
        if (IsHidden(instanceId, componentId)) continue;
        
        const CRhinoObject* comp = idef->Object(i);
        if (isSelected)
        {
            // Draw with selection color
            ON_Color selColor = RhinoApp().AppSettings().SelectedObjectColor();
            DrawComponentHighlighted(dp, comp, xform, selColor);
        }
        else
        {
            DrawComponent(dp, comp, xform);
        }
    }
    return false;
}
```

**Additionally,** subscribe to `SC_DRAWOBJECT` on both the normal pass and the highlighted object pass. Rhino draws highlighted objects in a separate loop — your conduit must handle both.

### 3.3 Display Cache Integration

**Problem:** Rhino caches display meshes and display lists per object. Our custom draw bypasses this cache, meaning:
- No GPU-side mesh caching
- Full CPU-side geometry processing every frame
- Slower than Rhino's native block drawing

**Solution — Use `CRhinoObjectDrawCache`:**

```cpp
class CPerInstanceVisibilityConduit : public CRhinoDisplayConduit
{
    // Per managed-instance cache of component draw data
    struct InstanceDrawCache {
        ON_UUID instanceId;
        std::vector<CRhinoCacheHandle*> componentCaches;
        bool dirty = true;
    };
    std::unordered_map<ON_UUID, InstanceDrawCache, UUIDHash> m_cache;
    
    void DrawComponent(CRhinoDisplayPipeline& dp, 
                       const CRhinoObject* comp,
                       const ON_Xform& xform,
                       CRhinoCacheHandle*& cache)
    {
        // dp.DrawObject with cache handle allows Rhino to 
        // reuse GPU-uploaded mesh data
        dp.DrawObject(comp, &xform, nullptr, cache);
    }
};
```

**Cache invalidation triggers:**
- Block definition changed → clear all caches for that definition
- Visibility state changed → mark instance cache dirty
- Document close → clear all caches

### 3.4 Material & Display Mode Correctness

The C# PoC had simplified material handling. In C++:

```cpp
void DrawComponent(CRhinoDisplayPipeline& dp,
                   const CRhinoObject* comp,
                   const ON_Xform& xform)
{
    // Use dp.DrawObject — it handles:
    // - Display mode (wireframe/shaded/rendered)
    // - Materials
    // - Colors (by object, by layer, by parent)
    // - Wireframe density
    // - Edge display
    dp.DrawObject(comp, &xform);
}
```

**`dp.DrawObject(const CRhinoObject*, const ON_Xform*)`** is the key — it replicates exactly what Rhino would do, including respecting the current display mode. This avoids the entire mess of per-geometry-type drawing the C# PoC had to do.

### 3.5 Nested Block Visibility

For MVP, treat nested blocks atomically — if a nested block instance is hidden, all its contents are hidden. Don't support hiding individual components *within* a nested instance from the parent level.

For v2, implement path-based addressing:
```cpp
struct ComponentPath {
    ON_SimpleArray<ON_UUID> path; // [instance_uuid, nested_instance_uuid, component_uuid]
};
```

---

## 4. Performance Architecture

### 4.1 Scale Targets

| Scale | Instances | Managed (w/ hidden) | Target |
|-------|-----------|---------------------|--------|
| Small | < 100 | < 10 | Instant, no optimization needed |
| Medium | 100–1,000 | < 50 | < 50ms tree rebuild |
| Large | 1,000–10,000 | < 200 | < 200ms tree rebuild, lazy loading |
| XL | 10,000+ | < 500 | Virtualized tree, background building |

### 4.2 Lazy Loading Strategy

**Current:** `BuildTree()` traverses *all* blocks recursively and creates *all* nodes upfront. For 10,000+ instances with deep nesting, this is O(n*d) where d is average nesting depth.

**Proposed lazy approach:**

```csharp
public class LazyBlockInstanceNode : AssemblyNode
{
    private bool _childrenLoaded = false;
    private readonly InstanceDefinition _definition;
    
    public override IEnumerable<AssemblyNode> Children
    {
        get
        {
            if (!_childrenLoaded)
            {
                LoadChildren();
                _childrenLoaded = true;
            }
            return _children;
        }
    }
    
    private void LoadChildren()
    {
        // Only load when node is expanded in tree
        var objects = _definition.GetObjects();
        foreach (var obj in objects)
        {
            if (obj is InstanceObject nested)
                _children.Add(new LazyBlockInstanceNode(nested));
        }
    }
}
```

**Tree only loads top-level instances initially.** Children load on expand. This gives O(1) initial load regardless of total instance count.

### 4.3 Virtualized Tree (Eto.Forms Constraints)

Eto's `TreeGridView` doesn't support virtualization natively. Options:

1. **Paginated loading** — Load first 500 top-level items, "Load More..." button
2. **Flat list mode** — For documents > 5000 instances, switch to a flat `GridView` with virtual scrolling (Eto supports this via `ICollection` data binding)
3. **WebView-based tree** — Replace Eto tree with an HTML/JS tree (e.g., jstree) in a WebView. Full virtualization, better performance. Higher development cost.

**Recommendation:** Start with lazy loading (#4.2). Add pagination for XL documents. WebView is a v2 option.

### 4.4 Incremental Updates

Currently, any document change triggers a full tree rebuild via `QueueRefresh()`. This is wasteful.

**Proposed incremental strategy:**

```csharp
private void OnAddRhinoObject(object sender, RhinoObjectEventArgs e)
{
    if (e.TheObject is InstanceObject instance)
    {
        // Don't rebuild entire tree — just add a node
        var parentNode = FindParentNode(instance);
        var newNode = CreateNodeForInstance(instance);
        parentNode?.AddChild(newNode);
        _treeView.InsertNode(newNode);
    }
}

private void OnDeleteRhinoObject(object sender, RhinoObjectEventArgs e)
{
    if (e.TheObject is InstanceObject)
    {
        var node = FindNodeByInstanceId(e.TheObject.Id);
        node?.Parent?.RemoveChild(node);
        _treeView.RemoveNode(node);
    }
}
```

**For block definition changes** (which affect all instances), a full subtree rebuild for affected definitions is necessary — but not a full document rebuild.

### 4.5 Event Debouncing Improvements

Current: Single 100ms timer for all events. 

**Proposed: Tiered debouncing:**

| Event | Debounce | Reason |
|-------|----------|--------|
| Selection change | 0ms (immediate) | User expects instant feedback |
| Object add/delete | 100ms | Batch operations common |
| Definition change | 250ms | BlockEdit sends many events |
| Document open | 500ms | Large documents, many events |

### 4.6 Caching Strategy

```
Document Cache (per RhinoDoc)
├── Definition Instance Count Cache
│   └── Dictionary<int defIndex, int count>
│   └── Invalidated on: instance add/delete, definition change
├── Tree Node Cache  
│   └── Dictionary<Guid instanceId, AssemblyNode>
│   └── Invalidated on: instance add/delete, definition change
└── Component Info Cache
    └── Dictionary<Guid instanceId, List<ComponentInfo>>
    └── Invalidated on: definition change
```

### 4.7 Display Pipeline Performance (C++)

For the conduit, the performance concern is: **how many instances are "managed" (have custom visibility)?**

- Unmanaged instances: Zero overhead. The conduit checks `m_managed_instances.find(id)` — a HashSet lookup is O(1).
- Managed instances: Must iterate definition objects and skip hidden ones. This is O(k) per instance per frame where k = component count.

**For 200 managed instances with 20 components each at 60fps:** 200 × 20 = 4000 component draws per frame. With `dp.DrawObject()` using cached display data, this is fast. The `dp.DrawObject()` per-component approach is essentially what Rhino does internally for blocks — iterate components and draw with transform.

**Optimization if needed:** Pre-build a "filtered definition" display list per managed instance. Only rebuild when visibility changes. This reduces per-frame work to a single DrawObject call per instance.

---

## 5. Thread Safety

### 5.1 Rhino's Threading Model

**Rules:**
1. **RhinoCommon API calls must happen on the UI thread** (the main Rhino thread)
2. `RhinoDoc` operations are NOT thread-safe
3. Display pipeline callbacks (conduit) run on the **display thread** — NOT the UI thread
4. Rhino events (`SelectObjects`, `AddRhinoObject`, etc.) fire on the UI thread
5. `System.Timers.Timer` callbacks fire on a **ThreadPool thread**

### 5.2 Current Threading Issues

**Issue 1: Timer callback on wrong thread**
```csharp
// This fires on ThreadPool thread!
_refreshTimer.Elapsed += (s, e) => RefreshTreeDebounced();

private void RefreshTreeDebounced()
{
    if (_needsRefresh)
    {
        _needsRefresh = false;
        RhinoApp.InvokeOnUiThread((Action)RefreshTree); // ← Correct marshalling
    }
}
```
The marshalling to UI thread is correct, but `_needsRefresh` is read/written from multiple threads without synchronization. Use `volatile` or `Interlocked`.

**Issue 2: `PerInstanceVisibilityConduit` thread safety**

The conduit's `PreDrawObject` runs on the display thread. `_managedInstances` (HashSet) and `_drawnThisFrame` (HashSet) are read on the display thread and written from the UI thread (when registering/unregistering instances). This is a **data race**.

**Fix for C++ conduit:**
```cpp
class CPerInstanceVisibilityConduit : public CRhinoDisplayConduit
{
    mutable std::shared_mutex m_mutex;
    std::unordered_set<ON_UUID, UUIDHash> m_managed_instances;
    
    // Read path (display thread) — shared lock
    bool ExecConduit(...) override
    {
        std::shared_lock lock(m_mutex);
        if (m_managed_instances.find(id) == m_managed_instances.end())
            return true;
        // ... draw
    }
    
    // Write path (UI thread) — exclusive lock
    void RegisterInstance(const ON_UUID& id)
    {
        std::unique_lock lock(m_mutex);
        m_managed_instances.insert(id);
    }
};
```

Use `std::shared_mutex` (reader-writer lock) since reads vastly outnumber writes.

**Issue 3: C# PerInstanceVisibilityService accesses doc from event handlers**

`OnBeginOpenDocument` receives an event with `e.Document` but the service was constructed with a specific `_doc`. If the event fires for a different document, accessing `_doc` from the event handler is correct (no-op check), but the check `e.Document?.RuntimeSerialNumber == _doc.RuntimeSerialNumber` should use `_doc.RuntimeSerialNumber` captured at construction time, not accessed lazily (in case `_doc` is disposed).

### 5.3 Thread Safety Rules for Production

| Operation | Thread | Sync Mechanism |
|-----------|--------|---------------|
| UI updates (tree, panel) | UI thread only | `RhinoApp.InvokeOnUiThread` |
| RhinoDoc read/write | UI thread only | Event handlers already on UI thread |
| Conduit read (ExecConduit) | Display thread | `shared_lock` on managed set |
| Conduit write (register/unregister) | UI thread | `unique_lock` on managed set |
| P/Invoke calls | UI thread preferred | Marshal if needed |
| Tree building (future: async) | Background → marshal results | `Task.Run` + `InvokeOnUiThread` |

### 5.4 GlimpseAI Crash Pattern Avoidance

GlimpseAI (referenced in project context) likely crashed from:
1. Accessing RhinoDoc from background threads
2. Modifying display conduit state during rendering
3. Event handler re-entrancy (event triggers code that triggers another event)

**Mitigation:**
- All RhinoDoc access through a single-threaded dispatcher
- Conduit state protected by reader-writer lock
- Event handlers use `_isProcessing` guard flags (already partially implemented)
- Never call `doc.Views.Redraw()` from a conduit callback (deadlock risk)

---

## 6. Data Persistence

### 6.1 What Needs Persisting

| Data | Scope | Frequency | Format |
|------|-------|-----------|--------|
| Hidden component set per instance | Per-instance | Every toggle | ON_UserData |
| Assembly root selection | Per-document | Rarely | Document UserText |
| Named visibility states | Per-document | Rarely | Document UserText |
| UI preferences (panel size, mode) | Global | Rarely | Plugin settings |

### 6.2 Per-Instance Visibility → ON_UserData (C++)

**This is the correct choice.** ON_UserData:
- Persists with the .3dm file automatically
- Travels with copy/paste, import/export
- Has well-defined serialization (Binary Archive)
- Lives on the specific InstanceObject, so there's no mapping table to maintain

**Implementation** (from ASSEMBLY_WORKFLOW_DESIGN.md §2.5, validated):
```cpp
class CComponentVisibilityData : public ON_UserData
{
    ON_UuidList m_hidden_component_ids;  // UUID-based, robust to definition edits
    
    bool Archive() const override { return true; }
    bool Write(ON_BinaryArchive&) const override;
    bool Read(ON_BinaryArchive&) override;
};
```

**Why NOT index-based (as in C# PoC):** Block definitions can be edited, reordering or removing components. UUIDs are stable across edits.

### 6.3 Assembly Roots → Document UserText

```csharp
// Store
doc.Strings.SetString("RAO_AssemblyRoot", assemblyRootId.ToString());

// Retrieve
var rootIdStr = doc.Strings.GetValue("RAO_AssemblyRoot");
if (Guid.TryParse(rootIdStr, out var rootId)) { ... }
```

Document UserText is the simplest persistence for document-level settings. It persists in .3dm and is human-readable.

### 6.4 Named Visibility States → Document UserData

For v2, named states need to store a mapping of `{instanceId → Set<componentId>}` per state. This is too complex for UserText. Use `DocumentData` (RhinoCommon's document-level UserData equivalent):

```csharp
public class VisibilityStatesData : DocumentData
{
    public Dictionary<string, Dictionary<Guid, HashSet<Guid>>> States { get; set; }
    // stateName → { instanceId → { hidden componentIds } }
}
```

### 6.5 User Preferences → Plugin Settings

```csharp
// Rhino's built-in settings mechanism
var settings = RhinoAssemblyOutlinerPlugin.Instance.Settings;
settings.SetInteger("DefaultMode", (int)OutlinerViewMode.Document);
settings.SetBool("ShowGeometryNodes", false);
```

Persists per-user, not per-document. Correct for preferences.

---

## 7. Testing Strategy

### 7.1 Unit Tests — Model Layer

The model layer (`AssemblyNode`, `BlockInstanceNode`, `DocumentNode`, `AssemblyTreeBuilder`) is **mostly testable without Rhino** if we extract an interface for the Rhino document dependency.

**Current blocker:** `AssemblyTreeBuilder` takes `RhinoDoc` directly. `BlockInstanceNode` takes `InstanceObject` and `InstanceDefinition`.

**Strategy: Test what's pure, mock what's Rhino:**

```csharp
// Testable without Rhino:
[Test] public void AssemblyNode_AddChild_SetsParent() { ... }
[Test] public void AssemblyNode_GetAllDescendants_ReturnsRecursive() { ... }
[Test] public void AssemblyNode_RemoveChild_ClearsParent() { ... }
[Test] public void AssemblyNode_Depth_CalculatesCorrectly() { ... }

// Needs mock or real Rhino (integration test):
[Test] public void TreeBuilder_BuildTree_HandlesEmptyDoc() { ... }
[Test] public void TreeBuilder_BuildTree_HandlesNestedBlocks() { ... }
```

**Framework:** NUnit or xUnit. For Rhino-dependent tests, use `RhinoInside` (headless Rhino for testing) or McNeel's test framework.

### 7.2 Integration Tests — Rhino API

Use [Rhino.Testing](https://github.com/mcneel/rhino.testing) or RhinoInside:

```csharp
[RhinoTest]
public void PerInstanceVisibility_HideComponent_PersistsInFile()
{
    // Create a block with 3 components
    var doc = RhinoDoc.CreateHeadless();
    var blockId = CreateTestBlock(doc, 3);
    var instanceId = InsertBlock(doc, blockId);
    
    // Hide component 1
    service.SetComponentVisibility(instanceId, componentIds[1], false);
    
    // Save and reload
    doc.Write3dmFile(tempPath);
    var doc2 = RhinoDoc.Open(tempPath);
    
    // Verify
    Assert.IsFalse(service.IsComponentVisible(instanceId, componentIds[1]));
}
```

### 7.3 Display Conduit Testing

Display conduits are inherently visual. Automated testing options:

1. **Snapshot comparison** — Render viewport to bitmap, compare with reference image (fragile, last resort)
2. **State verification** — Don't test what's drawn; test that the conduit *would* draw correctly:
   ```csharp
   // Test that conduit correctly skips hidden components
   [Test]
   public void Conduit_SkipsHiddenComponents()
   {
       conduit.RegisterManagedInstance(instanceId);
       SetHidden(instanceId, component2Id);
       
       var drawnComponents = conduit.GetDrawnComponentsForTest(instanceId);
       Assert.That(drawnComponents, Does.Not.Contain(component2Id));
   }
   ```
3. **Manual testing matrix** — Document a test matrix for QA:

| Scenario | Expected |
|----------|----------|
| Hide component → Rotate view | No ghost artifacts |
| Hide component → Select instance | Yellow highlight on visible components only |
| Hide component → Save → Reopen | Component still hidden |
| Hide component → BlockEdit → Exit | Verify visibility data integrity |
| 50 managed instances → Rotate view | Smooth (> 30fps) |

### 7.4 Testing the P/Invoke Bridge

```csharp
[Test]
public void PInvoke_GuidMarshalling_RoundTrips()
{
    var guid = Guid.NewGuid();
    NativeInterop.RAO_SetComponentHidden(ref guid, ref componentGuid, true);
    Assert.IsTrue(NativeInterop.RAO_IsComponentHidden(ref guid, ref componentGuid));
}
```

Test with edge cases: `Guid.Empty`, very many calls (stress), concurrent calls.

---

## 8. Risk Assessment

### Risk 1: C++ `SC_DRAWOBJECT` Cannot Suppress Block Component Drawing (CRITICAL)

**Probability:** Low (20%)  
**Impact:** Project-killing — entire per-instance visibility approach fails

**Evidence for optimism:** The C++ SDK documentation and samples show `return false` in `SC_DRAWOBJECT` suppressing object drawing. The approach works for simple objects. The question is whether Rhino has special-case handling for blocks that bypasses the conduit.

**Mitigation:**
- **Week 1 priority:** Build minimal C++ PoC that intercepts a block's draw call and replaces it with per-component drawing
- **Fallback A:** Use `SC_PREDRAWOBJECT` to hide the block, then `SC_POSTDRAWOBJECTS` to draw custom geometry (similar to C# approach but with cache integration)
- **Fallback B:** Use `CRhinoObject::SetCustomDrawHandler()` if available in SDK
- **Fallback C:** Ask McNeel directly (developer forum, Steve Baer is responsive)

### Risk 2: Performance Degradation with Many Managed Instances (HIGH)

**Probability:** Medium (40%)  
**Impact:** Moderate — feature works but unusable in large assemblies

Custom-drawing blocks per-component is inherently slower than Rhino's optimized block instancing (which uses GPU instancing for identical definitions).

**Mitigation:**
- Limit managed instances to those with *actual* hidden components (not all instances)
- Implement display cache (`CRhinoCacheHandle`) for managed instances
- Profile early with 100+ managed instances
- If needed: batch identical visibility states and use a single cached display list
- Absolute last resort: limit feature to max N managed instances per document

### Risk 3: Build System Complexity — Two Languages, Two Build Systems (MEDIUM)

**Probability:** High (70%)  
**Impact:** Moderate — slows development, blocks contributors

C# uses MSBuild/dotnet. C++ uses MSBuild with MSVC v142. Both need Rhino SDK. CI/CD must build both and package together.

**Mitigation:**
- Single Visual Studio solution with both projects
- C++ project has pre-build step that checks SDK installation
- MSBuild: C# project depends on C++ project, copies output DLL to output dir
- Document build setup thoroughly in CONTRIBUTING.md
- GitHub Actions CI with Rhino SDK installed (McNeel provides this)

### Risk 4: UserData Corruption on Block Definition Edit (MEDIUM)

**Probability:** Medium (50%)  
**Impact:** Moderate — stale hidden-component references, wrong components hidden

When a block definition is edited (BlockEdit), component UUIDs may change if objects are deleted and re-created.

**Mitigation:**
- Listen to `RhinoDoc.InstanceDefinitionTableEvent`
- On definition change: validate all hidden UUIDs against current definition
- Remove stale UUIDs (component no longer exists)
- Optionally: try to match by name/type if UUID changed (heuristic)
- Store component name alongside UUID as fallback identifier

### Risk 5: Rhino SDK Version Incompatibility (LOW-MEDIUM)

**Probability:** Low (15%)  
**Impact:** High — plugin doesn't load on some Rhino 8 versions

Rhino 8 C++ SDK requires MSVC v142 toolset. SDK updates may change class layouts or virtual function tables.

**Mitigation:**
- Pin to specific Rhino 8 SDK version
- Test on Rhino 8 SR (Service Release) updates before releasing
- Use only stable, documented API — avoid internal/undocumented functions
- Keep C++ surface area minimal (conduit + userdata + exports only)

---

## Summary: Priority Action Items

1. **Immediate (Week 1):** Build C++ minimal PoC — confirm `SC_DRAWOBJECT` return false works for blocks
2. **Week 2:** Implement `CComponentVisibilityData` (ON_UserData) with UUID-based storage and serialization
3. **Week 2-3:** Implement `CPerInstanceVisibilityConduit` with selection highlight and cache handles
4. **Week 3:** Build P/Invoke bridge, integrate with existing C# UI
5. **Week 4:** Refactor C# model layer (fix `AssemblyNode.Id`, remove `ObservableCollection`, add lazy loading)
6. **Week 4-5:** Thread safety audit, performance testing with 100+ managed instances
7. **Ongoing:** Testing matrix execution, edge case handling

**The single most important thing to validate first:** Can the C++ conduit intercept block drawing and replace it with per-component drawing without artifacts? Everything else builds on this.
