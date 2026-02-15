# Eto.Forms TreeGridView Column Sizing — Research & Fixes

**Date:** 2025-02-15  
**Issue:** Column resize mühsam; muss mehrfach "Collapse All" drücken um Visibility-Spalte vergrössern zu können.

## Root Cause Analysis

### Problem 1: AutoSize on Name column
The `Name` column used `AutoSize = true`. In Eto's TreeGridView, `AutoSize` recalculates column width based on **visible content**. When collapsing nodes, the visible text shrinks → column width shrinks → layout jumps. This forces the user to repeatedly collapse/expand to get stable column widths.

### Problem 2: Visibility column too narrow (30px)
In Eto TreeGridView, **column 0 shares space with the expand/collapse triangle** (the ▶/▼ arrow). With only 30px, the eye icon becomes unclickable or invisible at deeper nesting levels because the triangle indentation eats into the column width.

### Problem 3: No `Resizable` property set
Without explicit `Resizable = true`, columns may not be user-resizable on all platforms (WinForms/WPF behave differently).

### Problem 4: No panel MinimumSize
The panel could be resized so small that columns become unusable.

## Research Sources

- **Eto GitHub Issue #911**: DrawableCell width issues with AutoSize in TreeGridView
- **Eto GitHub Issue #789**: Column 0 rendering issues — width needs explicit setting
- **McNeel Discourse**: TreeGridView column 0 shares space with expand triangle
- **Eto Google Group**: Last column auto-fill — use `AutoSize = false` with fixed width for predictable layout

## Fixes Applied

| Column | Before | After |
|--------|--------|-------|
| Visibility (👁) | `Width = 30`, no Resizable/AutoSize set | `Width = 40`, `Resizable = false`, `AutoSize = false` |
| Name | `AutoSize = true` | `Width = 200`, `AutoSize = false`, `Resizable = true` |
| Layer | `Width = 120` | `Width = 120`, `Resizable = true` |
| Type | `Width = 80` | `Width = 80`, `Resizable = true` |

**Panel:** Added `MinimumSize = new Size(300, 200)` to prevent unusable layouts.

## Key Eto.Forms Insights

1. **Avoid `AutoSize = true`** on TreeGridView columns if content changes dynamically (expand/collapse). Use fixed widths instead.
2. **Column 0 needs extra width** (~40px minimum) because the tree expand/collapse triangle is rendered inside it.
3. **Always set `Resizable` explicitly** — default behavior varies by platform.
4. Eto does not support `MinWidth` on GridColumn — use adequate fixed `Width` as workaround.
