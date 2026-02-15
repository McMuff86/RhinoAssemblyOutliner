# Rhino Assembly Outliner — User Guide

**Version 0.1.0** | Rhino 8 Plugin | © 2026 Muff Software

---

## Table of Contents

1. [Installation](#1-installation)
2. [Getting Started](#2-getting-started)
3. [Visibility Management](#3-visibility-management)
4. [Navigation](#4-navigation)
5. [Organisation & Reordering](#5-organisation--reordering)
6. [Keyboard Shortcuts](#6-keyboard-shortcuts)
7. [Status Bar](#7-status-bar)
8. [Testing Guide](#8-testing-guide)
9. [Troubleshooting & FAQ](#9-troubleshooting--faq)

---

## 1. Installation

### Via Yak Package Manager (Recommended)

1. Open Rhino 8.
2. Type `_PackageManager` in the command line.
3. Search for **RhinoAssemblyOutliner**.
4. Click **Install** and restart Rhino.

### Manual Installation

1. Download the latest `.rhp` file from the [GitHub Releases](https://github.com/McMuff86/RhinoAssemblyOutliner/releases) page.
2. In Rhino, go to **Tools → Options → Plug-ins**.
3. Click **Install…** and select the downloaded `.rhp` file.
4. Restart Rhino when prompted.

### Opening the Panel

Once installed, open the Assembly Outliner in one of these ways:

- **Command:** Type `AssemblyOutliner` in the Rhino command line.
- **Menu:** The command toggles the panel — run it once to open, again to close.

The panel docks like any standard Rhino panel. Drag it to the left or right sidebar for a persistent layout.

---

## 2. Getting Started

### What Is the Assembly Outliner?

The Assembly Outliner is a **SolidWorks FeatureManager–style tree view** for Rhino 8. It displays all block instances in your document as a navigable hierarchy, making it easy to manage complex assemblies with deeply nested blocks.

### The Panel Layout

```
┌─────────────────────────────────┐
│ ↻  ⊞  ⊟  [📄 Document ▾]      │  ← Toolbar
├─────────────────────────────────┤
│ 🔍 ISOLATED: PartName          │  ← Isolate Banner (when active)
├─────────────────────────────────┤
│ Filter blocks...                │  ← Search / Filter
├─────────────────────────────────┤
│ 👁 │ Name           │ Layer │ T │  ← Tree View
│    │ 📄 MyFile.3dm  │       │   │
│ 👁 │  📦 Frame #1   │ Main  │ E │
│ 👁 │   📦 Bolt #1   │ HW    │ E │
│ ◯ │   📦 Bolt #2   │ HW    │ E │
│ 👁 │  🔗 Motor #1   │ Mech  │ L │
├─────────────────────────────────┤
│ Block: Frame                    │  ← Detail Panel
│ Instance: #1 of 3              │
│ Type: Static                    │
│ Layer: Main                     │
│ Children: 4                     │
│ [Select All Instances] [Zoom To]│
├─────────────────────────────────┤
│ 2 definitions | 5 instances     │  ← Status Bar
└─────────────────────────────────┘
```

### Tree Columns

| Column | Description |
|--------|-------------|
| **👁** | Visibility toggle — click to show/hide |
| **Name** | Block name with type icon and instance number (e.g. `📦 Frame #1`) |
| **Layer** | The Rhino layer the instance lives on |
| **Type** | Block type: `Static` (embedded), `Linked`, or `LinkedAndEmbedded` |

### Block Type Icons

| Icon | Meaning |
|------|---------|
| 📄 | Document root node |
| 📦 | Embedded block (standard) |
| 🔗 | Linked block (external file reference) |
| 📎 | Linked & Embedded block |

### Document Mode vs Assembly Mode

The outliner has two view modes, selectable from the **dropdown in the toolbar**:

- **📄 Document Mode** (default) — Shows **all** top-level block instances in the document and their full nested hierarchy.
- **📦 Assembly Mode** — Shows only a **single block instance** and its children. Use this to focus on one assembly without clutter.

**To enter Assembly Mode:**
1. Right-click any block instance in the tree.
2. Select **Set as Assembly Root**.
3. The dropdown updates to show the assembly name.

**To return to Document Mode:**
- Select **📄 Document** from the dropdown.

### Understanding the Block Hierarchy

Rhino blocks can be nested: a block instance can contain other block instances, forming a tree. The outliner displays this hierarchy with indentation.

```
📄 CarAssembly.3dm
 └─ 📦 Car #1
     ├─ 📦 Chassis #1
     │   ├─ 📦 FrontAxle #1
     │   └─ 📦 RearAxle #1
     ├─ 🔗 Engine #1          ← Linked from external file
     └─ 📦 Body #1
```

Instance numbers (`#1`, `#2`, …) distinguish multiple instances of the same block definition.

---

## 3. Visibility Management

The outliner provides fine-grained control over which block instances are visible in the viewport.

### Show / Hide a Single Instance

- **Click the 👁 column** on any row to toggle its visibility.
- **Keyboard:** Select a node and press `Space` to toggle, `H` to hide, or `S` to show.

Visible instances show **👁**. Hidden instances show **◯** and their name appears *grayed out and italic* in the tree.

### Mixed Visibility State (◐)

When a parent block contains a mix of visible and hidden children, it displays the **◐** (half-circle) icon. This tells you at a glance that some but not all descendants are visible.

```
◐  📦 Chassis #1        ← mixed: some children hidden
 👁  📦 FrontAxle #1
 ◯   📦 RearAxle #1     ← hidden
```

Toggling a parent with mixed state will apply the new state to the parent instance itself. Use **Hide/Show with Dependents** to affect all children.

### Hide / Show with Dependents

To hide or show a node **and all of its children** at once:

- **Right-click → Hide with Dependents** — Hides the node and every descendant.
- **Right-click → Show with Dependents** — Shows the node and every descendant.

### Show All

Reveals all hidden instances at once.

- **Keyboard:** `Ctrl+Shift+H`
- **Right-click → Show All**
- **Keyboard:** `Escape` (also exits Isolate Mode)

### Isolate Mode

Isolate mode hides **everything except** the selected node (and its descendants and ancestors), letting you focus on a single part.

- **Keyboard:** Select a node, press `I`
- **Right-click → Isolate**

When Isolate Mode is active:

1. A blue **🔍 ISOLATED: PartName** banner appears at the top.
2. All other instances are hidden.
3. The status bar shows the isolation state.

**To exit Isolate Mode:**
- Click **✕ Exit Isolate** on the banner.
- Press `Escape`.
- Use `Ctrl+Shift+H` (Show All).

> **Important:** Exiting Isolate Mode **restores** the exact visibility state from before isolation. No visibility information is lost.

### Per-Instance Component Visibility (Advanced)

For components *nested inside* a block definition (not top-level instances), the outliner uses a native C++ module for **per-instance component visibility**. This means you can hide a bolt inside one copy of a chassis without affecting the same bolt in another copy.

This feature requires the native DLL to be present. If it is not found, a message appears in the Rhino command line and nested component visibility falls back to standard behavior.

---

## 4. Navigation

### Selection Sync

Selection is **bidirectional** between the outliner and the Rhino viewport:

- **Tree → Viewport:** Clicking a node in the tree selects the corresponding block instance in the viewport.
- **Viewport → Tree:** Selecting a block instance in the viewport highlights it in the tree (auto-expanding parents if needed).

### Zoom to Fit

Focus the viewport camera on a specific block instance:

- **Keyboard:** Select a node, press `F`
- **Right-click → Zoom To**
- **Detail Panel:** Click the **Zoom To** button

### BlockEdit

Double-click or press Enter on a block instance to enter Rhino's **BlockEdit** mode for that block:

- **Double-click** any block instance row.
- **Keyboard:** Select a node, press `Enter`.
- **Right-click → BlockEdit**

The instance is automatically selected in the viewport before BlockEdit opens.

### Select in Viewport

- **Right-click → Select in Viewport** — Selects the block instance in the viewport without zooming.

### Search / Filter

Type in the **filter box** at the top to search by block name. The tree shows only matching nodes (and their parent chain). Clear the filter to restore the full tree.

---

## 5. Organisation & Reordering

### Reorder with Keyboard (Recommended)

Move top-level items up or down in the tree:

- **Ctrl+Up** — Move selected item up.
- **Ctrl+Down** — Move selected item down.

Only top-level items (direct children of the document root) can be reordered.

### Drag & Drop Reordering

You can also drag and drop top-level items to reorder them.

> **Note:** Due to a limitation in the Eto framework, the drop target is based on the *selected row* rather than the row directly under the cursor. For reliable reordering, prefer `Ctrl+Up` / `Ctrl+Down`.

### Expand / Collapse

| Action | How |
|--------|-----|
| Expand All | Click **⊞** in the toolbar |
| Collapse All | Click **⊟** in the toolbar |
| Expand/Collapse single node | Click the tree arrow, or use the standard tree expand/collapse |

### Refresh

Click **↻** in the toolbar to manually refresh the tree. The tree also refreshes automatically when:

- Block instances are added or deleted.
- Block definitions change.
- A new document is opened.

Refreshes are **debounced** (100ms) to avoid flickering during rapid changes.

### Context Menu (Right-Click)

| Menu Item | Shortcut | Description |
|-----------|----------|-------------|
| **Hide** | `H` | Hide the selected instance |
| **Show** | `S` | Show the selected instance |
| **Hide with Dependents** | — | Hide instance and all descendants |
| **Show with Dependents** | — | Show instance and all descendants |
| **Isolate** | `I` | Isolate: show only this instance |
| **Show All** | `Ctrl+Shift+H` | Show all hidden instances / exit isolate |
| *separator* | | |
| **Zoom To** | `F` | Zoom viewport to fit this instance |
| **Select in Viewport** | — | Select this instance in the viewport |
| *separator* | | |
| **BlockEdit** | `Enter` | Open BlockEdit for this instance |
| **Set as Assembly Root** | — | Switch to Assembly Mode with this as root |

### Detail Panel

The bottom panel shows properties of the selected node:

- **Block name** and instance number
- **Type** (Embedded / Linked / Linked+Embedded)
- **Layer** path
- **Source file** (for linked blocks)
- **Child count**
- **User Attributes** (UserText key-value pairs)
- **Buttons:** *Select All Instances* (selects all instances of the same definition) and *Zoom To*

---

## 6. Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `H` | Hide selected instance |
| `Shift+H` | Show selected instance |
| `S` | Show selected instance |
| `I` | Isolate selected instance |
| `Space` | Toggle visibility of selected instance |
| `F` | Zoom viewport to selected instance |
| `Enter` | Open BlockEdit on selected block |
| `Delete` / `Backspace` | Hide selected instance |
| `Escape` | Exit Isolate Mode / Show All |
| `Ctrl+Shift+H` | Show All |
| `Ctrl+Up` | Move selected item up (top-level only) |
| `Ctrl+Down` | Move selected item down (top-level only) |

---

## 7. Status Bar

The status bar at the bottom displays live statistics:

```
2 definitions | 5 instances | 1 hidden
```

| Field | Meaning |
|-------|---------|
| **X definitions** | Number of unique block definitions represented |
| **Y instances** | Total number of block instances in the tree |
| **Z hidden** | Number of currently hidden instances (only shown if > 0) |
| **🔍 ISOLATED: Name** | Shown when Isolate Mode is active |

---

## 8. Testing Guide

### Test Setup: Creating a Test Assembly in Rhino

Follow these steps to create a block hierarchy suitable for testing all features:

1. **Create base geometry:**
   - Draw a box (`_Box`) — this will become "Frame".
   - Draw a cylinder (`_Cylinder`) — this will become "Bolt".
   - Draw a sphere (`_Sphere`) — this will become "Motor".

2. **Create block definitions:**
   - Select the cylinder → `_Block` → Name: `Bolt` → OK.
   - Select the sphere → `_Block` → Name: `Motor` → OK.

3. **Build a nested assembly:**
   - Insert two instances of "Bolt" near the box (`_Insert` → `Bolt`).
   - Select the box + both bolt instances → `_Block` → Name: `Chassis` → OK.

4. **Create the top-level assembly:**
   - Insert "Motor" once.
   - Select the Chassis instance + Motor instance → `_Block` → Name: `Car` → OK.
   - Insert a second "Car" instance (`_Insert` → `Car`).

5. **Add a linked block (optional):**
   - Save a separate `.3dm` file with geometry.
   - In your test file: `_Insert` → browse to the saved file → insert as **Linked**.

6. **Open the outliner:**
   - Type `AssemblyOutliner` → the tree should show:

```
📄 Untitled
 ├─ 📦 Car #1
 │   ├─ 📦 Chassis #1
 │   │   ├─ 📦 Bolt #1
 │   │   └─ 📦 Bolt #2
 │   └─ 📦 Motor #1
 ├─ 📦 Car #2
 │   ├─ 📦 Chassis #1
 │   │   ├─ 📦 Bolt #1
 │   │   └─ 📦 Bolt #2
 │   └─ 📦 Motor #1
 └─ 🔗 ExternalPart #1      (if linked block added)
```

### Feature Test Checklist

Use this checklist to verify all features. Mark each as ✅ Pass or ❌ Fail.

#### Panel & Tree

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 1 | Type `AssemblyOutliner` | Panel opens; tree shows all top-level blocks | ☐ |
| 2 | Type `AssemblyOutliner` again | Panel closes | ☐ |
| 3 | Click ↻ Refresh | Tree reloads with current document state | ☐ |
| 4 | Add a new block instance in Rhino | Tree updates automatically | ☐ |
| 5 | Delete a block instance in Rhino | Tree updates automatically | ☐ |
| 6 | Open a new document | Tree refreshes to show new document | ☐ |

#### Visibility

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 7 | Click 👁 column on an instance | Icon toggles to ◯; instance hidden in viewport | ☐ |
| 8 | Click ◯ column on hidden instance | Icon toggles to 👁; instance visible again | ☐ |
| 9 | Hide one child of a parent | Parent shows ◐ (mixed state) | ☐ |
| 10 | Select node, press `H` | Instance hidden | ☐ |
| 11 | Select node, press `S` | Instance shown | ☐ |
| 12 | Select node, press `Space` | Visibility toggles | ☐ |
| 13 | Press `Ctrl+Shift+H` | All instances become visible | ☐ |
| 14 | Right-click → Hide with Dependents | Node and all children hidden | ☐ |
| 15 | Right-click → Show with Dependents | Node and all children shown | ☐ |
| 16 | Hidden node appearance | Name is gray and italic in tree | ☐ |

#### Isolate Mode

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 17 | Select node, press `I` | Only selected node visible; blue banner appears | ☐ |
| 18 | Click ✕ Exit Isolate | Previous visibility state fully restored | ☐ |
| 19 | Press `Escape` in isolate mode | Exits isolate, restores state | ☐ |
| 20 | Isolate a nested child | Child + parent chain visible, siblings hidden | ☐ |

#### Navigation

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 21 | Click a node in tree | Instance selected in viewport | ☐ |
| 22 | Select a block in viewport | Corresponding node highlighted in tree | ☐ |
| 23 | Select node, press `F` | Viewport zooms to fit instance | ☐ |
| 24 | Double-click a block node | BlockEdit opens for that instance | ☐ |
| 25 | Select node, press `Enter` | BlockEdit opens for that instance | ☐ |
| 26 | Right-click → Select in Viewport | Instance selected, no zoom | ☐ |

#### Search & Filter

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 27 | Type "Bolt" in filter box | Only Bolt nodes shown (with parent chain) | ☐ |
| 28 | Clear filter box | Full tree restored | ☐ |
| 29 | Filter with no matches | Tree is empty | ☐ |

#### View Modes

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 30 | Right-click → Set as Assembly Root | Tree shows only that block's hierarchy | ☐ |
| 31 | Dropdown shows assembly name | Assembly name appears with 📦 icon | ☐ |
| 32 | Select 📄 Document from dropdown | Returns to full document tree | ☐ |

#### Reordering

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 33 | Select top-level item, `Ctrl+Up` | Item moves up one position | ☐ |
| 34 | Select top-level item, `Ctrl+Down` | Item moves down one position | ☐ |
| 35 | Try `Ctrl+Up` on first item | Nothing happens (no crash) | ☐ |
| 36 | Drag and drop a top-level item | Item reordered (note: uses selected row as target) | ☐ |

#### Expand / Collapse

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 37 | Click ⊞ Expand All | All tree nodes expanded | ☐ |
| 38 | Click ⊟ Collapse All | All tree nodes collapsed | ☐ |

#### Detail Panel

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 39 | Select a block node | Detail panel shows name, type, layer, child count | ☐ |
| 40 | Select a linked block | Detail panel shows source file path | ☐ |
| 41 | Click "Select All Instances" | All instances of that definition selected in viewport | ☐ |
| 42 | Click "Zoom To" | Viewport zooms to instance | ☐ |
| 43 | Deselect all | Detail panel shows "No selection" | ☐ |

#### Status Bar

| # | Test | Expected Result | Pass/Fail |
|---|------|-----------------|-----------|
| 44 | Open outliner with blocks | Shows "X definitions \| Y instances" | ☐ |
| 45 | Hide some instances | Shows "Z hidden" count | ☐ |
| 46 | Enter isolate mode | Shows "🔍 ISOLATED: Name" | ☐ |

---

## 9. Troubleshooting & FAQ

### The panel doesn't open

- Make sure the plugin is loaded: **Tools → Options → Plug-ins** → check that "Rhino Assembly Outliner" is listed and enabled.
- Try typing `_PlugInManager` and loading it manually.
- Restart Rhino after first installation.

### The tree is empty

- The outliner only shows **block instances**. Loose geometry (curves, surfaces, meshes) is not displayed.
- Make sure your document contains at least one block instance (`_Insert` or `_Block`).

### "Native DLL not found" message

This means the optional native C++ module for per-instance component visibility is not installed. **Top-level visibility works normally.** Only nested component-level visibility is affected. This is expected if you installed the managed plugin only.

### Selection sync doesn't work

- Selection sync only works for **block instances** (InstanceObject), not for other geometry types.
- If you select multiple objects in the viewport, only the last block instance is highlighted in the tree (single selection mode).

### Drag & drop reordering feels unreliable

This is a known limitation of the Eto framework's TreeGridView — the drop target is the *selected row*, not the row under the cursor. **Use `Ctrl+Up` / `Ctrl+Down` instead** for reliable reordering.

### Isolate mode didn't restore my visibility state

This can happen if:
- The document was modified externally during isolation.
- The plugin was unloaded while isolate was active.

Use `Ctrl+Shift+H` (Show All) to reset all visibility.

### Performance with large assemblies

- The tree refreshes with a **100ms debounce**, so rapid changes (e.g. batch imports) won't cause flickering.
- Very deep hierarchies (>100 levels) are truncated to prevent stack overflow.
- Circular block references are detected and skipped with a warning in the command line.

### How do I report a bug?

Open an issue at [github.com/McMuff86/RhinoAssemblyOutliner/issues](https://github.com/McMuff86/RhinoAssemblyOutliner/issues) with:
1. Rhino version (`_SystemInfo`)
2. Steps to reproduce
3. Expected vs actual behavior
4. Screenshot if applicable

---

*Assembly Outliner is open source under MIT license. Contributions welcome!*
