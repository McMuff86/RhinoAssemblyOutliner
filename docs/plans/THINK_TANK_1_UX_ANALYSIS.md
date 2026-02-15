# Think Tank 1: UX & Feature Analysis

> Deep analysis of SolidWorks patterns, Rhino adaptations, and real-world workflows  
> Date: 2026-02-15  
> Status: Complete

---

## 1. SolidWorks FeatureManager Gap Analysis

What SolidWorks does that this plugin **doesn't yet**, ordered by user impact:

| # | SolidWorks Feature | Current Status | User Impact | Effort |
|---|---|---|---|---|
| 1 | **Per-Instance Component Visibility (production)** | PoC done, C++ needed | 🔴 Critical — this is the USP | HIGH |
| 2 | **Named Display States** | Not started | 🔴 High — users need saved visibility presets | MEDIUM |
| 3 | **Keyboard shortcuts (Tab/Shift+Tab)** | Not implemented | 🟠 High — power-user velocity | LOW |
| 4 | **Display Pane** (eye + display mode + transparency columns) | Only eye icon exists | 🟠 High — multi-property quick access | MEDIUM |
| 5 | **Isolate mode with exit button** | Basic isolate exists | 🟡 Medium — needs proper enter/exit flow | LOW |
| 6 | **Show with Dependents** | Missing | 🟡 Medium — essential for nested assemblies | LOW |
| 7 | **Hide with Dependents** | Missing | 🟡 Medium — bulk operation | LOW |
| 8 | **"Show Hidden Components" mode** (inverted view) | Missing | 🟡 Medium — recovery of lost hidden items | MEDIUM |
| 9 | **Suppress/Unsuppress** (unload from memory) | Missing | 🟡 Medium — performance for huge assemblies | HIGH |
| 10 | **Drag & drop reordering** | Missing | 🟢 Low — Rhino blocks don't have order concept | N/A |
| 11 | **Fix/Float indicators** | Missing | 🟢 Low — no constraint system in Rhino | N/A |
| 12 | **Configuration system** | Missing | 🟢 Low — fundamentally different paradigm | N/A |

### Priority Ranking

**P0 — Ship blocker:** #1 (Per-Instance Visibility production quality)  
**P1 — v1.0 must-have:** #3, #5, #6, #7  
**P2 — v1.x:** #2, #4, #8  
**Won't do:** #10, #11, #12 (don't map to Rhino's paradigm)

---

## 2. UX Patterns to Adopt

### 2.1 Eye Icon — The Universal Toggle

SolidWorks, Inventor, Fusion 360 all use the same pattern. We should match it exactly:

```
👁  = Visible (filled eye, full color row)
👁̸  = Hidden (struck-through eye, grayed row text + icon)  
◐  = Mixed (parent with some hidden children)
```

**Interaction:** Single click toggles. No dialog, no confirmation. Instant feedback.

**Current gap:** We have the eye column but lack the grayed-out styling for hidden items and the mixed-state indicator for parents.

### 2.2 Keyboard Shortcuts

Adopt SolidWorks' muscle-memory patterns, adapted for Rhino context:

| Shortcut | Action | Notes |
|---|---|---|
| **H** | Hide selected node(s) | When tree has focus |
| **Shift+H** | Show selected node(s) | |
| **I** | Isolate selected | |
| **Esc** | Exit isolate / clear selection | |
| **Ctrl+H** | Show All | Restore everything |
| **F** | Zoom to selected (Frame) | Rhino convention |
| **Space** | Toggle visibility | Alternative to H |
| **Enter** | Edit block (BlockEdit) | |

### 2.3 Right-Click Context Menu Structure

```
── Visibility ──────────────────
  👁  Show                    (Shift+H)
  👁̸  Hide                    (H)
  🎯 Isolate                  (I)
  🔄 Show All                 (Ctrl+H)
  ── (if has children) ──────
  👁  Show with Dependents
  👁̸  Hide with Dependents
── Selection ───────────────────  
  ✓  Select in Viewport
  📋 Select All Same Definition
── Navigation ──────────────────
  🔍 Zoom to                  (F)
  📌 Set as Assembly Root
── Editing ─────────────────────
  ✏️  Edit Block               (Enter)
  📄 Properties
```

### 2.4 Isolate Mode — Proper Enter/Exit Flow

SolidWorks' isolate has a clear state machine. We need:

```
[Normal] --Select+I--> [Isolated] --Esc/I--> [Normal]
                            │
                     Visual indicator:
                     - Banner: "Isolate Mode — showing 3 of 47 components"
                     - Exit button [✕ Exit Isolate]
                     - Panel border accent color (subtle)
```

**Critical:** Isolate must be **undoable**. Store pre-isolate visibility state and restore on exit.

### 2.5 Visual Feedback Summary

| State | Tree Icon | Tree Text | Viewport |
|---|---|---|---|
| Visible | Full color | Normal | Rendered normally |
| Hidden | Grayed out | Gray, italic | Invisible |
| Isolated (other) | Grayed out | Gray | Invisible |
| Selected | Highlight bg | Bold | Rhino selection highlight |
| Being edited | Blue border | Blue text | BlockEdit mode |
| Mixed children | Half-eye | Normal | Some children hidden |

### 2.6 Rollback Bar Equivalent — Not Applicable

SolidWorks' rollback bar controls feature history (parametric). Rhino blocks are non-parametric. **No equivalent needed.** The closest concept is Assembly Mode (focus on one root), which we already have.

---

## 3. Rhino-Specific Adaptations

### 3.1 Layers — The Elephant in the Room

**SolidWorks:** No layers. Visibility is purely component-based.  
**Rhino:** Everything lives on layers. Layer visibility is the primary control.

**Our approach — Three-tier visibility:**

```
Tier 1: Layer Visibility (Rhino-native, we DON'T touch)
  ↓ must be ON
Tier 2: Instance Visibility (our eye icon — whole block instance)
  ↓ must be ON  
Tier 3: Component Visibility (per-instance sub-component — the killer feature)
  ↓ must be ON
Result: Object is visible only if ALL three tiers say "visible"
```

**UX implication:** If a user hides something via layer and then tries to show it in our tree, it won't appear. We need a tooltip: *"Component is hidden by layer 'Furniture::Hardware'"*

**Display in tree:** Show layer name per instance. If layer is off, show ⚠️ indicator.

### 3.2 BlockEdit vs. Edit-in-Context

| SolidWorks | Rhino | Adaptation |
|---|---|---|
| Edit Component in Context | BlockEdit command | Double-click or Enter in tree → `BlockEdit` |
| Other parts go transparent | Other objects go gray | Same behavior, already native |
| Edit affects only that instance | Edit affects ALL instances of definition | **Critical difference** — must communicate clearly |
| Exit via confirmation corner | Exit via `BlockEdit` dialog | Hook into `RhinoDoc.EndCommand` to refresh tree |

**UX note:** Add a warning badge when entering BlockEdit: *"Changes apply to all X instances of this definition"*

### 3.3 No Configurations — Use Block Definitions Instead

SolidWorks configurations = different geometry variants of same part. Rhino equivalent: **different block definitions.** We don't try to emulate configurations. Instead:

- "Select All Same Definition" groups instances logically
- Users create separate definitions for variants (e.g., `Cabinet_600_WithDoor`, `Cabinet_600_NoDoor`)
- Our per-instance component visibility partially bridges this gap (hide door on one instance)

### 3.4 Named Views vs. Display States

| SolidWorks Display State | Rhino Equivalent | Gap |
|---|---|---|
| Stores: visibility, transparency, appearance per component | Named Views: only camera | **Big gap** |
| Quick switch via double-click | Layer States: stores layer on/off | **Partial** |

**v1 strategy:** Document that users should use **Layer States** for global visibility presets. Our per-instance visibility state saves with the file automatically (UserData).

**v2 strategy:** Build custom "Visibility Snapshots" stored in document UserStrings:
```
Key: "AssemblyOutliner::State::Exploded View"
Value: JSON { hidden_components: { instanceId: [indices...] } }
```

### 3.5 Worksessions — Multi-File Assemblies

SolidWorks assemblies reference external files natively. Rhino equivalent: **Worksessions** + **Linked Blocks**.

**Adaptation:** Linked blocks should show with a 🔗 icon and support the same visibility operations. We already show link type — good. But we should add:
- "Open Source File" context menu for linked blocks
- "Update Linked Block" action
- Visual indicator when linked block is out-of-date

---

## 4. User Workflows

### Workflow 1: Küchenmontage (Schreiner)

**Scenario:** Schreiner designs a complete kitchen in one .3dm file. 12 cabinets, each with hinges, drawers, handles. ~200 block instances.

**Step-by-step UX flow:**

1. Open file → Assembly Outliner auto-populates tree
2. Right-click `Unterschrank_600 #1` → **Set as Assembly Root** (focus)
3. Expand to see: Korpus, Schublade ×2, Griff ×2, Führung ×4
4. Click eye on `Griff_Edelstahl` → hides handles on this instance only
5. **H** key on `Schublade #2` → hide to see inside
6. Work on Korpus → **Shift+H** to show drawer back
7. Switch to Document Mode → see full kitchen
8. **I** (Isolate) on all Oberschränke → only upper cabinets visible
9. Right-click → **Select All Same Definition** on `Scharnier_Blum` → select all 24 hinges
10. Export selection for BOM

**Pain points solved:** No more toggling 15 layers to see inside one cabinet.

### Workflow 2: Metallbau — Stahlkonstruktion

**Scenario:** Metallbauer builds a steel frame structure. Columns, beams, connections, plates. ~500 instances.

1. Open file → Document Mode shows all
2. Assembly Mode → focus on one portal frame
3. Hide Bekleidung (cladding) to see structure
4. Isolate one connection detail for detailing
5. Select All Same `HEA200` → check count for BOM
6. **F** (Zoom to) on a specific beam connection
7. Toggle Bolzen (bolts) visibility for cleaner view while modeling plates

### Workflow 3: Fassadensystem

**Scenario:** Facade with repetitive panel modules. 80 identical panels, each with sub-components (glass, frame, gasket, bracket).

1. Document Mode → overwhelming (80 × 5 = 400 items)
2. Assembly Root → one panel module
3. Per-instance visibility: hide glass on panels being installed (show frame only)
4. Isolate bracket type for structural check
5. Show with Dependents on facade zone → see one complete vertical strip

### Workflow 4: Möbel-Prototyp (Schreiner, single piece)

**Scenario:** Single complex furniture piece — wardrobe with sliding doors, drawers, internal dividers.

1. Document Mode (single root anyway)
2. Per-instance: hide left door to see interior
3. **I** (Isolate) drawer mechanism
4. BlockEdit on drawer to adjust dimensions (affects all instances)
5. Show All → verify complete piece
6. Hide Rückwand for rear view

### Workflow 5: Maschinengehäuse (Metallbauer)

**Scenario:** Machine enclosure with access panels, hinges, locks, cable entry plates.

1. Assembly Root → Gehäuse
2. Hide all Abdeckplatten (cover plates) → see internal structure
3. Isolate one side panel + its hinges for detail drawing
4. Per-instance: one access panel open (door hidden), others closed
5. Select All Same `Kabeldurchführung` → count for order list

---

## 5. MVP vs v2 Feature Matrix

### v1.0 — MUST SHIP

| Feature | Status | Notes |
|---|---|---|
| Hierarchical tree with expand/collapse | ✅ Done | |
| Bidirectional selection (tree ↔ viewport) | ✅ Done | |
| Eye icon visibility toggle (instance level) | ✅ Done | |
| Context menu (hide/show/zoom/select/edit) | ✅ Done | |
| Search/filter in tree | ✅ Done | |
| Assembly Mode (root focus) | ✅ Done | |
| Instance count display | ✅ Done | |
| Layer display per instance | ✅ Done | |
| Link type icons | ✅ Done | |
| **Grayed styling for hidden items** | ❌ TODO | Low effort, high polish |
| **Keyboard shortcuts (H/I/Esc/F)** | ❌ TODO | Low effort, high impact |
| **Isolate with proper enter/exit flow** | ❌ TODO | Medium effort |
| **Show/Hide with Dependents** | ❌ TODO | Low effort |
| **Mixed-state parent indicator (◐)** | ❌ TODO | Low effort |
| **"Show All" action** | ❌ TODO | Trivial |

### v2.0 — NEXT VERSION

| Feature | Priority | Notes |
|---|---|---|
| Per-Instance Component Visibility (C++ production) | P0 | Requires C++ SDK work |
| Named Visibility States (save/restore) | P1 | Custom UserData storage |
| Display Pane (multi-column: eye + transparency + display mode) | P1 | UI expansion |
| "Show Hidden Components" inverted mode | P2 | SolidWorks pattern |
| Lazy loading for 1000+ instances | P2 | Performance |
| BOM export (CSV/Excel) | P2 | High user value |
| Breadcrumb navigation in Assembly Mode | P3 | Polish |
| Recent Assemblies history | P3 | Convenience |
| Ghost/transparent mode for hidden | P3 | Visual option |

### v3.0 — FUTURE

| Feature | Notes |
|---|---|
| Grasshopper API integration | Programmatic visibility control |
| Multi-document support (Worksessions) | Cross-file assemblies |
| Suppress/Unsuppress (unload from memory) | Performance for huge files |
| Custom reference sets (NX-style) | Advanced visibility presets |
| Drag & drop tree reordering | If Rhino API supports it |

---

## 6. Quick Wins

Features that are **easy to implement** but deliver **high perceived value:**

| Quick Win | Effort | Impact | Why |
|---|---|---|---|
| **Keyboard shortcuts (H, Shift+H, I, Esc, F)** | 2-4h | 🔴 Very High | Power users expect this. Just key event handlers on TreeView. |
| **Grayed text/icon for hidden items** | 1-2h | 🟠 High | Pure styling. Makes tree instantly readable. |
| **"Show All" button + Ctrl+H** | 30min | 🟠 High | Single method: iterate all, set visible. |
| **Show/Hide with Dependents** | 2-3h | 🟠 High | Recursive tree walk, already have the tree structure. |
| **Mixed-state parent eye icon (◐)** | 1-2h | 🟡 Medium | Check children visibility, show half-icon. |
| **Isolate banner with exit button** | 1-2h | 🟡 Medium | Just a conditional UI bar at top of panel. |
| **Tooltip showing layer when hidden by layer** | 1h | 🟡 Medium | Reduces confusion. Check layer visibility in tooltip. |
| **Collapse All / Expand All buttons** | 30min | 🟡 Medium | Tree utility. Users expect ⊞/⊟ buttons. |
| **Double-click → BlockEdit** | 30min | 🟡 Medium | Wire up TreeView double-click event. |
| **Status bar: "156 instances, 12 definitions, 3 hidden"** | 1h | 🟢 Nice | Instant document overview. |

**Total quick-win effort: ~12-16 hours for massive UX improvement.**

### Recommended Sprint

Do all quick wins in a single focused session before tackling C++ work. This makes v1.0 feel polished and professional with minimal effort, while C++ per-instance visibility becomes the v2 flagship feature.

---

## Summary

The plugin already has a solid foundation (tree, selection sync, eye toggle, assembly mode, search). The gap to a shippable v1.0 is **small** — mostly keyboard shortcuts, visual polish, and the with-dependents operations. 

The **strategic differentiator** (per-instance component visibility) requires C++ investment but the PoC validates the concept. Ship v1.0 with instance-level visibility (which already works), then deliver component-level visibility in v2 as the killer upgrade.

**Key insight:** Don't try to replicate SolidWorks. Replicate its *interaction patterns* (eye icons, shortcuts, isolate flow) while respecting Rhino's architecture (layers, BlockEdit, no parametrics). Users don't want SolidWorks-in-Rhino; they want Rhino-with-assembly-management.
