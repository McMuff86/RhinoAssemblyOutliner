# Undo/Redo Manual Test Scenarios

**Sprint:** 3  
**Feature:** UndoHelper for visibility changes  
**Date:** 2026-02-15

---

## Prerequisites

- Open a .3dm file with at least 2 block instances (e.g., "Motor" with Gehaeuse, Welle, Lager components)
- Open the Assembly Outliner panel

---

## Test 1: Hide Component → Undo

1. In the Outliner, click the eye icon on a component (e.g., "Welle") to **hide** it
2. Verify: component disappears from viewport
3. Press **Ctrl+Z**
4. **Expected:** Component reappears. Outliner eye icon shows visible.

## Test 2: Show Component → Undo

1. Hide a component (prerequisite)
2. Click the eye icon again to **show** it
3. Press **Ctrl+Z**
4. **Expected:** Component is hidden again.

## Test 3: Show All → Undo

1. Hide 3 different components across different instances
2. Click **Show All** button
3. Verify: all components visible
4. Press **Ctrl+Z**
5. **Expected:** All 3 components are hidden again (previous state restored)

## Test 4: Isolate → Undo

1. Start with all instances visible
2. Select one instance, click **Isolate**
3. Verify: only selected instance visible
4. Press **Ctrl+Z**
5. **Expected:** All instances visible again

## Test 5: Multiple Changes → Multiple Undo

1. Hide Component A (Ctrl+Z step 1)
2. Hide Component B (Ctrl+Z step 2)
3. Hide Component C (Ctrl+Z step 3)
4. Press **Ctrl+Z** → C reappears
5. Press **Ctrl+Z** → B reappears
6. Press **Ctrl+Z** → A reappears
7. Press **Ctrl+Y** → A hidden again (redo)

## Test 6: Undo Record Naming

1. Perform a Hide operation
2. Open Rhino's Undo dialog (if available) or check command history
3. **Expected:** Undo record shows descriptive name like "Hide Welle" or "Show All"

---

## Notes

- `Objects.Hide()` / `Objects.Show()` are automatically tracked by Rhino's undo system
- `Objects.Replace()` (for variant reassignment) is also auto-tracked
- Native conduit state requires `AddCustomUndoEvent` for proper undo (future enhancement when conduit is used as primary visibility mechanism)
- All bulk operations (Show All, Isolate) produce a **single** undo record
