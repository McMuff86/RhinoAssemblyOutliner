# Refactoring Checklist

Priority-ordered list of code quality fixes identified in Think Tank 2 analysis.  
**Do items 1–5 in Sprint 1/2 (before C++ work). Items 6–8 in Sprint 4.**

---

## 1. ⚡ Fix `AssemblyNode.Id` — Use Rhino Object IDs

**Current:** `Id = Guid.NewGuid()` generates ephemeral ID every rebuild.  
**Problem:** `_itemLookup` keyed on random GUIDs. `SelectNodeByObjectId` does O(n) full tree search.  
**Fix:**

```csharp
// BlockInstanceNode: use the Rhino instance ID
public BlockInstanceNode(InstanceObject instance, ...) {
    Id = instance.Id;  // Stable across rebuilds
}
// DocumentNode: use runtime serial number as deterministic GUID
public DocumentNode(RhinoDoc doc, ...) {
    Id = GuidFromInt(doc.RuntimeSerialNumber);
}
```

**Then:** `_itemLookup[rhinoObjectId]` is O(1) for selection sync.

**Effort:** 1h | **Impact:** Fixes selection sync performance, enables proper node identity.

---

## 2. ⚡ Fix `VisibilityService` Document Reference Leak

**Current:** `EnsureVisibilityService()` creates service with `RhinoDoc.ActiveDoc` once, never recreates on doc change.  
**Problem:** Stale doc reference after document switch.  
**Fix:**

```csharp
private void OnDocumentChanged(object sender, DocumentOpenEventArgs e) {
    _visibilityService?.Dispose();
    _visibilityService = null;  // Recreated lazily on next use
}
```

Also: capture `doc.RuntimeSerialNumber` at construction, validate before use.

**Effort:** 30min | **Impact:** Prevents stale-doc bugs.

---

## 3. ⚡ Replace `ObservableCollection` with `List<T>` on `AssemblyNode.Children`

**Current:** `ObservableCollection<AssemblyNode>` fires `CollectionChanged` on every add.  
**Problem:** Tree is rebuilt from scratch (not incrementally updated), so change notifications are wasted overhead.  
**Fix:** Change to `List<AssemblyNode>`. The UI calls `LoadTree()` which recreates `TreeGridItem` objects anyway.

**Effort:** 15min | **Impact:** Eliminates unnecessary allocations during tree building.

---

## 4. ⚡ Fix Duplicate Panel Registration

**Current:** Both `OpenOutlinerCommand` and `RefreshOutlinerCommand` call `Panels.RegisterPanel()`.  
**Fix:** Move registration to `RhinoAssemblyOutlinerPlugin.OnLoad()`. Remove from commands.

**Effort:** 15min | **Impact:** Code hygiene.

---

## 5. ⚡ Add `IDisposable` to Panel + Centralize `RhinoDoc` Access

**Panel:** Create `System.Timers.Timer` but relies on `PanelClosing` for cleanup.  
**Fix:** Implement `IDisposable`, dispose timer in both `Dispose()` and `PanelClosing`.

**RhinoDoc access:** Scattered `RhinoDoc.ActiveDoc` calls in Panel, TreeView, DetailPanel.  
**Fix:** Pass `RhinoDoc` explicitly from Panel to children. Store `RuntimeSerialNumber` and validate:

```csharp
private RhinoDoc GetDoc() {
    var doc = RhinoDoc.FromRuntimeSerialNumber(_docSerialNumber);
    if (doc == null) throw new InvalidOperationException("Document closed");
    return doc;
}
```

**Effort:** 1h | **Impact:** No leaks, no null-doc crashes.

---

## 6. 🔧 Remove Unused Cycle Detection (`_visitedDefinitions`)

**Current:** `AssemblyTreeBuilder` declares `_visitedDefinitions` HashSet but never checks it. `MaxRecursionDepth = 100` is the only guard.  
**Fix:** Either implement proper cycle detection:

```csharp
if (!_visitedDefinitions.Add(definition.Index)) return null; // cycle
try { /* process children */ }
finally { _visitedDefinitions.Remove(definition.Index); }
```

Or remove the unused field entirely and keep `MaxRecursionDepth` as the simple guard (sufficient for real-world files).

**Recommendation:** Implement properly. Real cost is 3 lines. Catches cycles at depth 1 instead of 100.

**Effort:** 15min | **Impact:** Correct cycle handling for pathological files.

---

## 7. 🔧 Fix Geometry Duplication in C# Conduit

**Current (PoC):** `var dupGeom = geom.Duplicate(); dupGeom.Transform(xform);` — allocates new geometry every frame.  
**Problem:** 1200 allocations/second per managed instance at 60fps.  
**Fix:** This is moot once C++ conduit replaces C# PoC. But if C# conduit persists for any reason:

```csharp
// Cache transformed geometry, invalidate on transform change
private Dictionary<Guid, CachedComponentGeometry> _geometryCache;
```

**Effort:** N/A (replaced by C++ in Sprint 3) | **Impact:** PoC-only issue.

---

## 8. 🔧 Fix String-Based Display Mode Detection

**Current:** `viewportMode.EnglishName.ToLower().Contains("wireframe")` — brittle, locale-dependent.  
**Fix:** Use `DisplayModeDescription.Id` comparison against known GUIDs:

```csharp
private static readonly Guid WireframeId = new Guid("...");
bool isWireframe = viewport.DisplayMode.Id == WireframeId;
```

Or use `DisplayModeDescription.WireframeId` if RhinoCommon exposes it.

**Effort:** 30min | **Impact:** Won't break on localized Rhino installs.

---

## Summary

| # | Item | Sprint | Effort | Priority |
|---|------|--------|--------|----------|
| 1 | AssemblyNode.Id → Rhino IDs | 1 | 1h | Critical |
| 2 | VisibilityService doc leak | 2 | 30min | High |
| 3 | ObservableCollection → List | 1 | 15min | Medium |
| 4 | Duplicate panel registration | 2 | 15min | Low |
| 5 | IDisposable + centralize doc | 2 | 1h | High |
| 6 | Cycle detection | 2 | 15min | Low |
| 7 | Geometry duplication (PoC) | N/A | — | Replaced by C++ |
| 8 | String-based display mode | 2 | 30min | Medium |
