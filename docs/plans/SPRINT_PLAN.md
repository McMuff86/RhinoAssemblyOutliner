# Sprint Plan: RhinoAssemblyOutliner v1.0 → v2.0

## Strategy

Ship v1.0 C#-only with polished UX (quick wins). Then tackle C++ per-instance component visibility for v2.0.

---

## Sprint 1: v1.0-rc — UX Polish (C# only) ✅ DONE

**Duration:** 1 week  
**Status:** ✅ COMPLETED (2026-02-15)  
**Goal:** All TT1 quick wins implemented. Plugin feels professional.

| # | Task | Effort | Depends On | Acceptance Criteria |
|---|------|--------|-----------|-------------------|
| 1.1 | **Keyboard shortcuts** (H, Shift+H, I, Esc, F, Ctrl+H, Space, Enter) | 3h | — | All shortcuts work when tree has focus. Tooltips show shortcut keys. |
| 1.2 | **Grayed styling for hidden items** — gray text, gray icon, italic | 1.5h | — | Hidden nodes visually distinct. Toggling updates immediately. |
| 1.3 | **Mixed-state parent eye icon (◐)** | 1.5h | — | Parent with some hidden children shows half-eye. Updates on child toggle. |
| 1.4 | **Show All action** + Ctrl+H shortcut | 30min | 1.1 | All nodes visible after action. Works from any state including isolate. |
| 1.5 | **Show with Dependents** (recursive show) | 2h | — | Right-click → Show with Dependents shows node + all descendants. |
| 1.6 | **Hide with Dependents** (recursive hide) | 1h | 1.5 | Same as above but hide. Shares recursive helper. |
| 1.7 | **Isolate enter/exit flow** — banner, exit button, Esc to exit, state restore | 3h | 1.1 | Banner shows "Isolate Mode — N of M visible". Esc restores pre-isolate state. |
| 1.8 | **Collapse All / Expand All** toolbar buttons | 30min | — | Buttons in toolbar. Work on current tree. |
| 1.9 | **Double-click → BlockEdit** | 30min | — | Double-click on block instance node starts BlockEdit command. |
| 1.10 | **Status bar** — "156 instances, 12 definitions, 3 hidden" | 1h | — | Updates on tree rebuild and visibility changes. |
| 1.11 | **Context menu restructure** per TT1 §2.3 | 1.5h | 1.5, 1.6 | Menu has Visibility, Selection, Navigation, Editing sections. |

**Total estimated: ~16 hours**

**Sprint 1 Deliverables:**
- All 11 tasks completed and reviewed
- Refactoring checklist items 1-3 done (stable node IDs, doc reference leak fix, ObservableCollection removal)
- Code review passed (see `docs/plans/REVIEW_SPRINT1.md`)

---

## Sprint 2: v1.0 — Release (~40% complete)

**Duration:** 1 week  
**Status:** 🔄 IN PROGRESS (~40%)  
**Goal:** Stable release on Yak package manager.

| # | Task | Effort | Depends On | Acceptance Criteria |
|---|------|--------|-----------|-------------------|
| 2.1 | **Bug bash** — systematic testing of all features | 4h | Sprint 1 | All features work without crashes. |
| 2.2 | **Fix VisibilityService doc reference leak** — recreate on doc change | 1h | — | Service handles document switches. No stale doc references. |
| 2.3 | **Fix duplicate panel registration** — move to Plugin.OnLoad() | 30min | — | Registration in one place only. |
| 2.4 | **IDisposable on panel** — proper timer cleanup | 30min | — | Timer disposed on panel GC. |
| 2.5 | **Unit tests for model layer** (AssemblyNode, tree operations) | 4h | — | ≥80% coverage on model classes. Pure logic, no Rhino dependency. |
| 2.6 | **Plugin icon** (256×256 PNG) | 1h | — | Icon shows in Rhino panel list. |
| 2.7 | **README with screenshots** | 2h | 2.1 | Clear install instructions, feature overview, screenshots. |
| 2.8 | **Yak package build script** | 2h | 2.6 | `yak build` produces installable package. |
| 2.9 | **Test on clean Rhino 8 install** | 2h | 2.8 | Plugin loads, all features work, no missing dependencies. |
| 2.10 | **Publish v1.0 to Yak** | 1h | 2.9 | Package available via `_PackageManager`. |

**Total estimated: ~18 hours**

---

## Sprint 3: v2.0-alpha — C++ Validation (started early)

**Duration:** 2 weeks  
**Status:** 🔄 IN PROGRESS — C++ foundation built ahead of schedule  
**Goal:** Confirm SC_DRAWOBJECT approach works for blocks. This is the #1 project risk.

**Early progress (done during Sprint 1/2 refactoring):**
- ✅ 3.1: C++ SDK setup, VS project, build config (`build-native.ps1`), PlatformToolset v143
- ✅ 3.5: `CComponentVisibilityData` ON_UserData with chunked Write/Read serialization
- ✅ 3.6: P/Invoke bridge — all 14 extern C functions implemented, NativeApi.h/.cpp complete
- ✅ DocEventHandler: auto-sync on open/save/close/delete (not in original sprint plan)
- ✅ VisibilityConduit: SC_DRAWOBJECT interception with path-based filtering + nested block recursion

**Sprint 3 C++ improvements (2026-02-15):**
- ✅ ComponentState enum — 4-state model (Visible/Hidden/Suppressed/Transparent) replacing boolean visibility
- ✅ SetComponentState / GetComponentState API functions
- ✅ Snapshot pattern — `CVisibilitySnapshot` for lock-free per-frame rendering
- ✅ SC_CALCBOUNDINGBOX — correct ZoomExtents (suppressed components excluded)
- ✅ SC_POSTDRAWOBJECTS — selection highlights via DrawObject (no heap allocs)
- ✅ SC_PREDRAWOBJECTS — frame-start snapshot acquisition
- ✅ HasHiddenDescendants optimization — O(1) prefix lookup via precomputed set
- ✅ API version bumped to 4
- ⏳ 3.2-3.4: Validation testing in Rhino (pending Windows build + runtime test)
- ⏳ 3.7: End-to-end integration smoke test

| # | Task | Effort | Depends On | Acceptance Criteria |
|---|------|--------|-----------|-------------------|
| 3.1 | **C++ SDK setup** — install SDK, VS project, build config, hello world .rhp | 1d | — | C++ plugin loads in Rhino 8, prints to command line. |
| 3.2 | **SC_DRAWOBJECT validation** — minimal conduit that intercepts a block instance and draws it shifted 10 units | 2d | 3.1 | Block appears shifted. No ghost at original position. Rotate viewport — no artifacts. |
| 3.3 | **Per-component draw test** — intercept block, iterate definition objects, draw each with instance transform, skip one | 2d | 3.2 | One component hidden. Others render correctly in all display modes (wireframe, shaded, rendered). |
| 3.4 | **Selection highlight test** — verify selected managed instance shows yellow highlight | 1d | 3.3 | Select block → yellow wireframe on visible components. |
| 3.5 | **CComponentVisibilityData** — ON_UserData with UUID list, Write/Read serialization | 1d | 3.1 | Save file → reopen → hidden component data intact. |
| 3.6 | **P/Invoke bridge** — implement 10 extern C functions, C# NativeInterop wrapper | 2d | 3.3, 3.5 | C# can call all native functions. GUID marshalling works. Error handling for missing DLL. |
| 3.7 | **Integration smoke test** — C# UI calls C++ to hide a component, viewport updates | 1d | 3.6 | End-to-end: click eye in tree → component disappears in viewport via C++ conduit. |

**Total estimated: ~10 working days**

**EXIT GATE:** If 3.2 fails (SC_DRAWOBJECT doesn't work for blocks), execute fallback plan from ADR-002 before proceeding.

---

## Sprint 4: v2.0 — Full Per-Instance Component Visibility

**Duration:** 2 weeks  
**Goal:** Production-quality component visibility integrated into the outliner UI.

| # | Task | Effort | Depends On | Acceptance Criteria |
|---|------|--------|-----------|-------------------|
| 4.1 | **Display cache integration** — CRhinoCacheHandle per managed instance | 2d | Sprint 3 | Managed instances don't re-process geometry every frame. Measurable FPS improvement. |
| 4.2 | **Thread safety** — shared_mutex on conduit state, Interlocked on C# flags | 1d | Sprint 3 | No crashes under rapid toggle + viewport rotation. |
| 4.3 | **Definition change handler** — validate UUIDs on BlockEdit exit, prune stale | 1d | 3.5 | Edit block definition → stale hidden UUIDs removed. Valid ones survive. |
| 4.4 | **Tree UI for component visibility** — expand block to see components, eye icon per component | 2d | Sprint 3 | Component nodes appear under block instances. Eye toggle works per-component. |
| 4.5 | **Mixed state indicators** — parent shows ◐ when some components hidden | 1d | 4.4 | Visual indicator propagates up tree correctly. |
| 4.6 | **Performance testing** — 100+ managed instances, profile, optimize | 2d | 4.1 | >30fps with 100 managed instances (20 components each) during viewport rotation. |
| 4.7 | **Edge case testing** — copy/paste managed instance, undo/redo, linked blocks, nested blocks | 2d | 4.3 | All edge cases documented and handled. No crashes, no data loss. |
| 4.8 | **Build system** — single solution builds both projects, C# copies C++ output | 1d | — | `dotnet build` (or VS Build) produces both .rhp files in output directory. |
| 4.9 | **Yak package v2.0** — bundle both .rhp files | 1d | 4.8 | Package installs both plugins. Component visibility works out of box. |

**Total estimated: ~13 working days**

---

## Milestone Summary

| Milestone | Date (est.) | Deliverable |
|-----------|------------|-------------|
| v1.0-rc | Week 1 end | ✅ All quick wins, polished C# plugin |
| v1.0 | Week 2 end | 🔄 ~40% — bug bash, tests, packaging |
| v2.0-alpha | Week 4 end | 🔄 C++ foundation built, validation pending |
| v2.0 | Week 6 end | Full component visibility, Yak v2 release |
