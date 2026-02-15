# Think Tank 4: UI Integration Strategy for Per-Instance Visibility

> Date: 2026-02-15 | Sprint: 3-4 Planning | Status: Draft

## 1. Dual Visibility System Problem

### Current State

Two independent visibility systems exist:

| System | Implementation | Scope | Persistence |
|--------|---------------|-------|-------------|
| **Instance Visibility** | C# `VisibilityService` → `RhinoObject.Hide/Show` | Top-level block instances | Rhino-native (.3dm) |
| **Component Visibility** | C++ `NativeVisibilityInterop` → conduit `SC_DRAWOBJECT` | Sub-components within a block definition | `ON_UserData` in .3dm |

### When to Use Which

**Rule: The routing decision must be invisible to the user.** The `VisibilityService` already implements this correctly via `IsComponentNode()`:

- **Top-level instance** (no parent, or parent is DocumentNode) → C# `RhinoObject.Hide/Show`
- **Nested instance or leaf geometry inside a block** → C++ conduit path-based hiding

The user clicks the same eye icon. The service routes internally. This is correct and should remain.

### State Synchronization: Parent Hide + Hidden Children

**Scenario:** User has hidden component `1.0.2` inside instance A via C++ conduit. Then user hides the entire instance A via C# (`RhinoObject.Hide`).

**Current behavior:** Works correctly by accident — Rhino hides the entire object, conduit never fires (nothing to draw). Hidden component state is preserved in `CVisibilityData`.

**Problem arises on Show:** When user shows instance A again, component `1.0.2` should still be hidden. This already works because the conduit re-engages and checks the hidden paths.

**Real problem: Mixed state icon.** When instance A is hidden (C#), the tree shows ◯. But it has hidden children in the C++ layer. When re-shown, the parent should show ◐ (mixed). **Current code only checks `node.IsVisible` on child AssemblyNodes** — it does NOT query `NativeVisibilityInterop.GetHiddenComponentCount()`.

**Fix required:** `GetVisibilityIcon()` in `AssemblyTreeView` and `CheckChildrenVisibility()` must also query the native layer for component-level hidden state when the tree is rebuilt. This means:

```csharp
// In AssemblyTreeView or a helper:
private string GetVisibilityIcon(AssemblyNode node)
{
    if (node is DocumentNode) return "";
    
    // Check if this instance has any hidden components (C++ layer)
    if (node is BlockInstanceNode bn && bn.InstanceId != Guid.Empty)
    {
        int hiddenCount = NativeVisibilityInterop.GetHiddenComponentCount(ref bn.InstanceId);
        if (hiddenCount > 0 && node.IsVisible)
            return "◐"; // Instance visible but has hidden components
    }
    
    // Existing child-walk logic for nested instances...
    if (node.Children.Count == 0)
        return node.IsVisible ? "👁" : "◯";
    // ...
}
```

**Important:** The tree view currently has no reference to `NativeVisibilityInterop`. We need to either:
1. Pass a `Func<Guid, int>` delegate for hidden component count queries (clean, testable)
2. Inject the `VisibilityService` into the tree view (heavier coupling)

**Recommendation:** Option 1. Add `Func<Guid, int> GetHiddenComponentCount` property to `AssemblyTreeView`, set by panel.

### Unification Summary

The user mental model should be: **"Click eye → thing disappears. Click again → it comes back."** No notion of C# vs C++. The dual system is an implementation detail. The only UI surface that leaks the abstraction today is the mixed-state icon, and that's fixable with the approach above.

---

## 2. Tree Node Types for Components

### Current Node Types

```
AssemblyNode (abstract)
├── DocumentNode         — Root, represents the .3dm file
├── BlockInstanceNode    — A block instance (InstanceObject in Rhino)
└── GeometryNode         — Loose geometry (not inside blocks)
```

### What's Missing: ComponentNode

When a user expands a block instance in the tree, they currently see **nested block instances** (child blocks). They do NOT see the **leaf geometry** inside the block definition (meshes, surfaces, curves, etc.).

For per-instance component visibility to be useful in the UI, users need to see and toggle individual geometry pieces inside a block. This requires a new node type:

```
AssemblyNode (abstract)
├── DocumentNode
├── BlockInstanceNode    — Block instance (expandable)
│   ├── BlockInstanceNode    — Nested block (existing)
│   └── ComponentNode (NEW)  — Leaf geometry inside definition
└── GeometryNode
```

### ComponentNode Design

```csharp
public class ComponentNode : AssemblyNode
{
    /// <summary>Index of this object within the parent block definition's object list.</summary>
    public int ComponentIndex { get; set; }
    
    /// <summary>The geometry type (Mesh, Brep, Curve, etc.) for icon display.</summary>
    public ObjectType GeometryType { get; set; }
    
    /// <summary>Object name from the definition geometry (may be empty).</summary>
    public string ObjectName { get; set; }
    
    /// <summary>Layer of the geometry within the definition.</summary>
    public string LayerName { get; set; }
    
    /// <summary>
    /// The full dot-separated path from top-level instance to this component.
    /// Pre-computed during tree building for direct use with C++ API.
    /// Example: "1.0.2" means definition-object-1 → nested-block-0 → component-2.
    /// </summary>
    public string ComponentPath { get; set; }
    
    /// <summary>The top-level instance GUID that owns this component path.</summary>
    public Guid TopLevelInstanceId { get; set; }
}
```

**Key insight:** `BlockInstanceNode` already has `ComponentIndex` for nested blocks. `ComponentNode` extends this to leaf geometry. The `ComponentPath` is pre-computed during tree build so the UI never needs to walk the tree to resolve paths.

### Lazy Loading Strategy

Loading all component nodes for all instances upfront is expensive for large assemblies (10,000+ instances × 50+ components each = 500K nodes). **Lazy loading is essential.**

**Implementation:**

1. During `AssemblyTreeBuilder.BuildTree()`, block instances are created as today — with child block instances only.
2. Each `BlockInstanceNode` gets a `HasComponents` flag set to `true` if its definition contains non-block geometry.
3. The `AssemblyTreeItem` constructor checks `HasComponents`:
   - If true AND children haven't been loaded: add a **sentinel placeholder child** so the expand arrow appears.
   - The placeholder is a single `ComponentNode` with `DisplayName = "Loading..."`.
4. On `TreeGridView.Expanding` event: if the first child is the sentinel, replace it with real `ComponentNode` children by querying the block definition.

```csharp
// In AssemblyTreeView — handle expand event
Expanding += (sender, e) =>
{
    var item = e.Item as AssemblyTreeItem;
    if (item?.Node is BlockInstanceNode bn && bn.HasComponents && !bn.ComponentsLoaded)
    {
        LoadComponentsForNode(bn, item);
        bn.ComponentsLoaded = true;
    }
};
```

**Component loading:**

```csharp
private void LoadComponentsForNode(BlockInstanceNode blockNode, AssemblyTreeItem parentItem)
{
    var doc = GetDoc();
    var definition = doc.InstanceDefinitions[blockNode.BlockDefinitionIndex];
    if (definition == null) return;
    
    // Remove sentinel
    parentItem.Children.Clear();
    
    var objects = definition.GetObjects();
    for (int i = 0; i < objects.Length; i++)
    {
        var obj = objects[i];
        if (obj is InstanceObject) continue; // Already handled as BlockInstanceNode
        
        var component = new ComponentNode
        {
            DisplayName = !string.IsNullOrEmpty(obj.Name) ? obj.Name : $"{obj.ObjectType} [{i}]",
            ComponentIndex = i,
            GeometryType = obj.ObjectType,
            ObjectName = obj.Name,
            LayerName = doc.Layers[obj.Attributes.LayerIndex]?.FullPath ?? "",
            ComponentPath = ResolveComponentPath(blockNode, i),
            TopLevelInstanceId = FindTopLevelInstance(blockNode),
            Parent = blockNode
        };
        
        // Query visibility from C++ layer
        var topId = component.TopLevelInstanceId;
        component.IsVisible = NativeVisibilityInterop.IsComponentVisible(ref topId, component.ComponentPath);
        
        blockNode.Children.Add(component);
        var childItem = new AssemblyTreeItem(component);
        childItem.Parent = parentItem;
        parentItem.Children.Add(childItem);
    }
}
```

### Component Path Mapping

The tree structure directly maps to C++ API paths:

```
📄 Document
└── 📦 Assembly A [instance GUID: abc-123]          ← top-level
    ├── 📦 Sub-Assembly B [index 0 in def]           ← path "0"
    │   ├── 🔶 Bolt [index 0 in sub-def]            ← path "0.0"
    │   ├── 🔶 Nut [index 1 in sub-def]             ← path "0.1"
    │   └── 📦 Sub-Sub C [index 2 in sub-def]       ← path "0.2"
    │       └── 🔶 Washer [index 0 in sub-sub-def]  ← path "0.2.0"
    ├── 🔶 Frame [index 1 in def]                    ← path "1"
    └── 🔶 Cover [index 2 in def]                    ← path "2"
```

The `ComponentPath` is built by walking up from the node to the top-level instance, collecting indices, and joining with dots. This is already implemented in `VisibilityService.ResolveComponentPath()` — we just pre-compute it during tree building for O(1) access.

---

## 3. Eye Icon Behavior for Components

### Click Scenarios

| User Action | What Happens | System Used |
|-------------|-------------|-------------|
| Click eye on **ComponentNode** | `NativeVisibilityInterop.SetComponentVisibility(topId, path, !visible)` | C++ conduit |
| Click eye on **BlockInstanceNode** (top-level, no components expanded) | `RhinoObject.Hide/Show` | C# native |
| Click eye on **BlockInstanceNode** (top-level, has expanded components) | See below | Depends |
| Click eye on **BlockInstanceNode** (nested inside another block) | `NativeVisibilityInterop.SetComponentVisibility` for the block's path | C++ conduit |

### The Tricky Case: Top-Level Instance with Expanded Components

When user clicks the eye on a top-level block instance that has visible component children:

**Option A: Hide entire instance (C# Hide)**
- Pros: Fast, simple, consistent with current behavior
- Cons: Loses per-component state visually (everything disappears), user might expect to toggle individual components

**Option B: Hide all components via C++ conduit**
- Pros: Preserves the "component-level thinking"
- Cons: Slower (N API calls), weird state (instance exists but nothing draws — conduit draws nothing)

**Option C (Recommended): Cascade behavior based on current state**

```
If ALL components visible → Hide entire instance (C# Hide) — fast bulk operation
If SOME components hidden → Hide remaining visible components (C++ conduit) — granular
If ALL components hidden → Show entire instance + reset component visibility — full restore
```

This matches SolidWorks behavior: clicking hide on an assembly hides the whole thing. Clicking hide on a partially-hidden assembly hides what's left. This feels natural.

**Implementation in VisibilityService:**

```csharp
public bool ToggleVisibility(AssemblyNode node, bool includeChildren = false)
{
    if (node is BlockInstanceNode blockNode && !IsComponentNode(blockNode))
    {
        int hiddenCount = _nativeInitialized 
            ? NativeVisibilityInterop.GetHiddenComponentCount(ref blockNode.InstanceId)
            : 0;
        
        if (hiddenCount == 0)
        {
            // All visible → hide entire instance (C#)
            HideInstance(blockNode);
        }
        else if (node.IsVisible)
        {
            // Some hidden, instance visible → hide remaining via C++ or hide entire
            HideInstance(blockNode); // Simple: just hide the whole thing
        }
        else
        {
            // Instance hidden → show and reset all component visibility
            ShowInstance(blockNode);
            NativeVisibilityInterop.ResetComponentVisibility(ref blockNode.InstanceId);
        }
    }
    // ... component path logic unchanged
}
```

### Mixed State Icon Propagation

Three states for eye icon:

| Icon | Meaning |
|------|---------|
| 👁 | All visible (instance + all components) |
| ◯ | All hidden (instance hidden OR all components hidden) |
| ◐ | Mixed (some components hidden, instance visible) |

Propagation rules:
- ComponentNode hidden → parent BlockInstanceNode shows ◐
- All ComponentNodes hidden → parent shows ◯ (or ◐ if instance itself is still "visible" in Rhino)
- Nested block has mixed children → its parent also shows ◐
- ◐ propagates upward until a node where all children agree

---

## 4. Display States Feature Design

### Concept

Display States are **named snapshots of all visibility settings** — both C# instance visibility and C++ component visibility. Equivalent to SolidWorks Display States but simpler (visibility only, no color/transparency initially).

### Data Model

```csharp
public class DisplayState
{
    public string Name { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>Instance GUIDs that are hidden (C# layer).</summary>
    public HashSet<Guid> HiddenInstances { get; set; } = new();
    
    /// <summary>Per-instance hidden component paths (C++ layer).</summary>
    public Dictionary<Guid, HashSet<string>> HiddenComponents { get; set; } = new();
    
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public class DisplayStateManager
{
    public List<DisplayState> States { get; set; } = new();
    public Guid ActiveStateId { get; set; }
    
    public DisplayState CaptureCurrentState(string name, RhinoDoc doc);
    public void ApplyState(DisplayState state, RhinoDoc doc);
    public void UpdateState(DisplayState state, RhinoDoc doc); // overwrite with current
    public void DeleteState(Guid stateId);
}
```

### Storage

Serialized as JSON in **document UserText** under key `RAO_DisplayStates`:

```json
{
  "activeStateId": "...",
  "states": [
    {
      "id": "...",
      "name": "Full Assembly",
      "hiddenInstances": [],
      "hiddenComponents": {},
      "createdAt": "...",
      "modifiedAt": "..."
    },
    {
      "id": "...",
      "name": "Structure Only",
      "hiddenInstances": ["guid1", "guid2"],
      "hiddenComponents": {
        "guid3": ["1.0", "1.1", "2.0.1"]
      }
    }
  ]
}
```

Why UserText over ON_UserData: simpler for document-level data, doesn't require C++ changes, survives copy/paste of entire document, inspectable via Rhino properties.

### UI Design

**Option A: Dropdown in toolbar (Recommended for MVP)**

```
[↻] [⊞] [⊟] [Full Assembly ▾] [💾] [📄 Document]
                    │
                    ├── Full Assembly      ✓
                    ├── Structure Only
                    ├── Exploded View
                    ├── ─────────────────
                    ├── 💾 Save Current As...
                    ├── ✏️ Rename...
                    └── 🗑️ Delete...
```

- Dropdown shows all states, checkmark on active
- Save button (💾) next to dropdown: updates active state with current visibility
- "Save Current As..." opens name dialog
- Switching states applies instantly

**Option B: Panel section (future enhancement)**

A dedicated collapsible section below the tree showing states as a list with thumbnails (viewport snapshots). Overkill for MVP.

### Quick Switch

- **Keyboard:** Ctrl+1 through Ctrl+9 for first 9 display states
- **Toolbar buttons:** Not initially — dropdown is sufficient
- **Command:** `RAO_DisplayState` command with sub-commands: `Save`, `Apply`, `List`, `Delete`

### Default States

On first use (no states saved), auto-create:
- **"Default"** — capture current state as baseline

No other defaults — users create what they need.

---

## 5. Viewport ↔ Tree Bidirectional Selection

### Current State

| Direction | Status | Implementation |
|-----------|--------|---------------|
| Tree → Viewport | ✅ Works | `OnTreeSelectionChanged` → `doc.Objects.Select(blockNode.InstanceId)` |
| Viewport → Tree | ⚠️ Partial | `OnSelectObjects` → `_treeView.SelectNodeByObjectId(instance.Id)` — works for top-level only |

### Problem: Viewport → Tree for Nested Blocks

When user clicks a block instance in the viewport, Rhino selects the **top-level instance** (unless using sub-object selection). The tree correctly highlights this top-level instance.

But the user might want to select a **nested block** or **component** within. Rhino supports sub-object picking via Ctrl+Shift+Click, which can select objects inside a block instance. The challenge: how to map that to a tree node.

### Sub-Object Selection Mapping

When Rhino fires `SelectObjects` with a sub-object selection:

```csharp
private void OnSelectObjects(object sender, RhinoObjectSelectionEventArgs e)
{
    foreach (var obj in e.RhinoObjects)
    {
        if (obj is InstanceObject instance)
        {
            // Check for sub-object selection
            var selectedSubObjects = instance.GetSelectedSubObjects();
            if (selectedSubObjects != null && selectedSubObjects.Length > 0)
            {
                // Map sub-object to component path
                // selectedSubObjects gives ComponentIndex values
                // Walk the tree to find matching ComponentNode
                var path = BuildPathFromSubObjectSelection(instance, selectedSubObjects);
                _treeView.SelectNodeByComponentPath(instance.Id, path);
            }
            else
            {
                // Top-level selection
                _treeView.SelectNodeByObjectId(instance.Id);
            }
        }
    }
}
```

**Challenge:** Rhino's sub-object selection API for blocks is limited. `ObjRef.GeometryComponentIndex` gives you the component index within the immediate definition, but not the full nested path. For nested blocks, we'd need to:

1. Get the picked point/ray from the selection event
2. Do our own hit-testing against the block definition hierarchy
3. Build the path from the hit result

**Pragmatic approach for Sprint 3:**
- Top-level instance selection → tree highlight (already works)
- Add `RhinoDoc.SelectSubObjects` event handling for Ctrl+Shift selections
- For nested blocks: use `RhinoObject.GetSubObjects()` + component index to find the tree node
- Full nested path resolution: Sprint 4 (requires custom hit-testing)

### Tree → Viewport Highlight Enhancement

Current: tree selection selects the object (blue highlight in viewport).

**Enhancement: Hover preview**
- Hovering over a tree node → temporary highlight in viewport (edge highlight, no selection change)
- This requires `RhinoDoc.Objects.DrawHighlight()` or a custom display conduit that draws a wireframe overlay
- SolidWorks calls this "rollover highlighting" — very useful for orientation

**Implementation sketch:**

```csharp
// In AssemblyTreeView — mouse move event
MouseMove += (sender, e) =>
{
    var item = GetItemAtLocation(e.Location) as AssemblyTreeItem;
    if (item?.Node is BlockInstanceNode bn)
    {
        _hoverHighlightService.HighlightInstance(bn.InstanceId);
    }
};
```

The hover highlight conduit would draw the bounding box or wireframe of the hovered instance in a distinct color (orange, like SolidWorks). This is a Sprint 4 feature.

### SelectNodeByComponentPath

New method needed on `AssemblyTreeView`:

```csharp
public void SelectNodeByComponentPath(Guid topLevelId, string componentPath)
{
    // Find the top-level node
    if (!_itemLookup.TryGetValue(topLevelId, out var topItem)) return;
    
    // Ensure components are loaded (trigger lazy load)
    EnsureExpanded(topItem);
    
    // Walk path segments to find the target node
    var pathSegments = componentPath.Split('.');
    var current = topItem;
    foreach (var segment in pathSegments)
    {
        if (!int.TryParse(segment, out int index)) break;
        var child = current.Children.OfType<AssemblyTreeItem>()
            .FirstOrDefault(c => c.Node is ComponentNode cn && cn.ComponentIndex == index
                              || c.Node is BlockInstanceNode bn && bn.ComponentIndex == index);
        if (child == null) break;
        EnsureExpanded(child);
        current = child;
    }
    
    ExpandToItem(current);
    SelectedItem = current;
}
```

---

## 6. Concrete Integration Tasks

Priority-ordered with effort estimates (story points, 1 SP ≈ 2-4 hours).

### Sprint 3 (Core Integration)

| # | Task | Effort | Dependencies |
|---|------|--------|-------------|
| 1 | **Add `ComponentNode` class** to Model layer | 2 SP | None |
| 2 | **Add `HasComponents` / `ComponentsLoaded` flags** to `BlockInstanceNode` | 1 SP | None |
| 3 | **Implement lazy component loading** in `AssemblyTreeView` (Expanding event, sentinel pattern) | 5 SP | #1, #2 |
| 4 | **Pre-compute `ComponentPath`** during tree build in `AssemblyTreeBuilder` | 3 SP | #1 |
| 5 | **Fix mixed-state icon** to query `NativeVisibilityInterop.GetHiddenComponentCount` | 3 SP | #3 |
| 6 | **Route eye-click on ComponentNode** through `VisibilityService` to C++ API | 2 SP | #1, #3 |
| 7 | **Context menu for ComponentNode**: Hide / Show / Isolate Component | 2 SP | #6 |
| 8 | **Cascade toggle logic** for top-level instance with mixed component state | 3 SP | #5, #6 |

**Sprint 3 Total: ~21 SP**

### Sprint 4 (Polish & Display States)

| # | Task | Effort | Dependencies |
|---|------|--------|-------------|
| 9 | **DisplayState data model** + JSON serialization | 3 SP | None |
| 10 | **DisplayStateManager** service (capture/apply/update/delete) | 5 SP | #9 |
| 11 | **Display state dropdown** in toolbar | 3 SP | #10 |
| 12 | **Keyboard shortcuts** Ctrl+1-9 for display state switching | 2 SP | #11 |
| 13 | **Viewport → Tree sub-object selection** (top-level + immediate children) | 5 SP | #3 |
| 14 | **Hover highlight conduit** (rollover preview in viewport) | 5 SP | None |
| 15 | **ComponentNode icons** per geometry type (mesh/brep/curve/extrusion) | 2 SP | #1 |
| 16 | **`RAO_DisplayState` command** (scriptable display state management) | 3 SP | #10 |
| 17 | **Component search/filter** — search box filters components too | 2 SP | #3 |

**Sprint 4 Total: ~30 SP**

### Future (Sprint 5+)

| Task | Effort | Notes |
|------|--------|-------|
| Full nested path hit-testing for deep sub-object selection | 8 SP | Custom ray-cast against block hierarchy |
| Display state thumbnails (viewport snapshot per state) | 5 SP | Capture viewport bitmap on state save |
| Color/transparency per display state | 8 SP | Extends both C# and C++ layers |
| "Ghost mode" for hidden components (semi-transparent instead of invisible) | 5 SP | C++ conduit change: draw with alpha instead of skip |
| Batch component operations (select multiple → hide all) | 3 SP | Multi-select in tree |
| Component properties panel (material, color override per instance) | 8 SP | Major feature |

---

## UX Design Principles

1. **One mental model:** Users think "hide this thing." They never think about C# vs C++.
2. **Progressive disclosure:** Block instances show as expandable nodes. Components appear only when expanded. No information overload.
3. **Consistent icons:** 👁 visible, ◯ hidden, ◐ mixed. Same everywhere.
4. **Fast operations:** Hide/show must be <16ms (one frame). Display state switch <100ms.
5. **Undo support:** All visibility changes should be undoable. C# uses Rhino's undo. C++ needs custom undo records (Sprint 5+).
6. **SolidWorks muscle memory:** H=hide, S=show, I=isolate, Esc=show all. Same shortcuts work on components.
7. **Graceful degradation:** If C++ DLL missing, everything still works — just no component-level visibility. UI adapts (no expand arrow on blocks).
