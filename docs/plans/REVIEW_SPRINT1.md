# Sprint 1 Code Review

**Date:** 2026-02-15  
**Branch:** `nightly/15-02-sprint1-refactor`  
**Reviewer:** Code Review Agent  
**Commits:** 6 commits covering refactoring checklist items 1-3 + Sprint 1 UX tasks

---

## ✅ Approved

### ✅ AssemblyNode.Id → Rhino Object IDs (Checklist #1)
- `BlockInstanceNode` now uses `instance.Id` — correct, stable across rebuilds
- `DocumentNode` uses `GuidFromSerialNumber(doc.RuntimeSerialNumber)` with `0xD0` marker byte — deterministic, collision-free
- Definition-only nodes use `GuidFromDefinitionIndex()` with `0xDE` marker byte — good
- `SelectNodeByObjectId` is now O(1) via direct `_itemLookup` — major perf improvement
- Old `FindNodeByObjectId` O(n) tree walk removed ✅

### ✅ ObservableCollection → List<T> (Checklist #3)
- Clean replacement. No missed references found in the diff.

### ✅ VisibilityService Document Reference Leak (Checklist #2)
- `_doc` field replaced with `_docSerialNumber` (uint) — correct
- `GetDoc()` resolves via `RhinoDoc.FromRuntimeSerialNumber()` — safe if doc closed (returns null)
- `EnsureVisibilityService()` in Panel now recreates service when `doc.RuntimeSerialNumber` changes — fixes the stale-doc bug
- `_visibilityServiceDocSerial` tracking field added ✅

### ✅ Status Bar
- Shows `"{total} instances | {hidden} hidden"` — clean and useful
- Updates on visibility toggle, isolate, show all, and tree refresh ✅

### ✅ Hidden Item Styling
- `OnCellFormatting` sets gray foreground + italic font for `!IsVisible` nodes ✅

### ✅ Context Menu Shortcut Labels
- Menu items show shortcut hints (`Hide\tH`, `Show\tS`, etc.) ✅

---

## ⚠️ Concerns

### ⚠️ Keyboard Shortcuts — Deviations from Sprint Plan

The Sprint Plan / TT1 specified:
| Planned | Implemented | Issue |
|---------|-------------|-------|
| `H` → Hide | `H` → Hide | ✅ |
| `Shift+H` → Show | `S` → Show | ⚠️ Different key. Users from SolidWorks expect Shift+H |
| `Ctrl+H` → Show All | `Ctrl+Shift+H` → Show All | ⚠️ `Ctrl+H` is Rhino's "Hide" command — good to avoid, but document the rationale |
| `Esc` → Exit isolate | **Not implemented** | ⚠️ Missing |
| `Enter` → BlockEdit | **Not implemented** | ⚠️ Missing |
| `Space` → Toggle | `Space` → Toggle | ✅ |
| `F` → Zoom | `F` → Zoom | ✅ |
| `I` → Isolate | `I` → Isolate | ✅ |

**Suggestion:** Add `Esc` (exit isolate / clear selection) and `Enter` (BlockEdit) before merge. Also add `Shift+H` as an alias for Show alongside `S`.

### ⚠️ OnCellFormatting Performance — Font Allocation

```csharp
e.Font = new Font(e.Font.Family, e.Font.Size, FontStyle.Italic);
```

This creates a **new Font object per cell per paint** for hidden items. With 200 hidden items visible on screen at 60fps, that's 12,000 Font allocations/second. Eto.Forms may cache internally, but it's safer to:

**Fix:** Cache the italic font:
```csharp
private Font _hiddenFont;
private Font GetHiddenFont(Font baseFont)
{
    if (_hiddenFont == null || _hiddenFont.Family != baseFont.Family || _hiddenFont.Size != baseFont.Size)
        _hiddenFont = new Font(baseFont.Family, baseFont.Size, FontStyle.Italic);
    return _hiddenFont;
}
```

### ⚠️ `GetDoc()` Called Multiple Times per Operation

In `VisibilityService`, methods like `ToggleVisibility` call `GetDoc()` multiple times (e.g., line finding object + line calling Redraw). Each call does a dictionary lookup. Not a perf issue but:

1. The doc could theoretically close between calls (extremely unlikely but possible)
2. Cleaner pattern: `var doc = GetDoc(); if (doc == null) return;` at method start

Some methods already do this (e.g., `Isolate`, `ShowAll`). Others don't (e.g., `ToggleVisibility` calls `GetDoc()?.Objects` inline). **Make it consistent.**

### ⚠️ `_needsRefresh` Still Not Thread-Safe

The refactoring checklist and ADR-005 both call out `_needsRefresh` as a race condition (read/written from timer thread + UI thread without synchronization). **This was not fixed in Sprint 1.**

**Fix:** Use `Interlocked`:
```csharp
private int _needsRefresh; // 0 or 1
// Set: Interlocked.Exchange(ref _needsRefresh, 1);
// Test-and-clear: if (Interlocked.CompareExchange(ref _needsRefresh, 0, 1) == 1) { ... }
```

### ⚠️ Panel Still Uses `RhinoDoc.ActiveDoc` Directly

`OnZoomToRequested` uses `RhinoDoc.ActiveDoc` instead of the serial-number pattern. The Panel itself still has scattered `RhinoDoc.ActiveDoc` calls (not visible in the diff but the old code remains). This is inconsistent with the VisibilityService fix.

---

## ❌ Must-Fix Before Merge

### ❌ Missing: Esc Key Handler

`Esc` is critical for exiting isolate mode. Without it, users get stuck. The Sprint Plan lists this as part of Task 1.1 (keyboard shortcuts). Add:
```csharp
case Keys.Escape:
    ShowAllRequested?.Invoke(this, EventArgs.Empty); // or dedicated ExitIsolateRequested
    e.Handled = true;
    break;
```

### ❌ Missing: Enter Key → BlockEdit

Sprint Plan Task 1.9 specifies double-click and Enter to start BlockEdit. Neither is implemented. Enter is trivial:
```csharp
case Keys.Enter:
    if (node is BlockInstanceNode bn && bn.InstanceId != Guid.Empty)
    {
        RhinoApp.RunScript($"_-BlockEdit SelId {bn.InstanceId}", false);
        e.Handled = true;
    }
    break;
```

---

## 💡 Suggestions (Nice-to-Have)

1. **Font disposal** — `_hiddenFont` should be disposed when the TreeView is disposed. Eto Font implements IDisposable.

2. **Status bar: definition count** — The Sprint Plan asks for "156 instances, 12 definitions, 3 hidden". Currently only instances and hidden count are shown. Add definition count for completeness.

3. **`GuidFromDefinitionIndex` collision space** — The marker byte approach (`0xDE`, `0xD0`) is clever but fragile. If a real Rhino GUID happened to have `0xDE` at byte 15 with matching low bytes, there'd be a collision. Extremely unlikely but consider using a proper UUID v5 namespace hash for deterministic GUIDs.

4. **Keyboard shortcut documentation** — Add a comment block or constant class listing all shortcuts for maintainability.

5. **Mixed-state parent eye icon (◐)** — Sprint Plan Task 1.3. Not in this branch. Consider adding in a follow-up commit.

---

## TODO: Fixes Before Merge

- [ ] **BLOCKING:** Add `Esc` key handler (exit isolate / show all)
- [ ] **BLOCKING:** Add `Enter` key handler (BlockEdit on selected block)
- [ ] Add `Shift+H` as alias for Show (alongside `S`)
- [ ] Cache italic font in `OnCellFormatting` to avoid per-paint allocations
- [ ] Fix `_needsRefresh` thread safety with `Interlocked`
- [ ] Make `GetDoc()` usage consistent in VisibilityService (single call at method start)
- [ ] Replace `RhinoDoc.ActiveDoc` in `OnZoomToRequested` with serial-number pattern

---

**Overall Assessment:** Solid refactoring work. The three checklist items (Id fix, ObservableCollection, VisibilityService leak) are correctly implemented. The UX additions (shortcuts, styling, status bar) are good. Two missing keyboard shortcuts (Esc, Enter) are blocking. Everything else is minor.
