# Changelog

All notable changes to RhinoAssemblyOutliner will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added (2026-04-30 — Sprint 4 Persistence Foundation)
- Native `ON_AssemblyUserData` class for persisted per-instance assembly metadata
- P/Invoke exports for attaching, removing, and reading source definition + hidden component state
- Managed `AssemblyDataStore` facade with graceful fallback when the native DLL is unavailable
- `VariantManager` writes assembly metadata on component-visibility reassignment and restores it after document open
- Assembly tree builder reads persisted instance state before falling back to in-memory variant state
- Unit-test baseline cleaned up to 315/315 passing

### Fixed
- Native C++ project now builds against the installed Rhino 8 SDK by replacing stale document user-string and display-pipeline API calls
- `VariantManager.GetOrCreateVariant` serializes cache miss creation to avoid duplicate variants under concurrent access

### Fixed (2026-04-29 — Phase A: Build Cleanup)
- `VariantGarbageCollector` Timer ambiguity (`System.Timers.Timer` vs `System.Threading.Timer`) resolved via aliased `using`
- `KeyboardShortcutMappingTests` enums (`Modifiers`, `OutlinerAction`) made `public` so they can appear in `[Theory]` method signatures
- `VariantManager.ReassignInstance` used `variantDef.Index` (int) where `InstanceReferenceGeometry` expects a `Guid`; switched to `variantDef.Id`. Also corrected the `Objects.Replace` call to the 3-arg `Replace(Guid, GeometryBase, bool)` overload (RhinoCommon 8 has no 2-arg `GeometryBase` variant — only typed ones for Brep/Curve/Mesh/etc.)
- `VisibilityService` referenced `node.Name` which does not exist on `AssemblyNode`; switched to `node.DisplayName`
- `VisibilityService` ctor parameter `IVariantManager variantManager = null` made nullable to match its default

### Documentation
- `docs/plans/visibility-architecture-hardening.md` archived → `docs/archive/`. The doc proposed hardening the C++ DisplayConduit and contradicts the current v3 architecture (Definition Cloning supersedes the conduit)
- `VisibilityService` xmldoc and a Legacy region clearly mark the native-conduit code path as Pre-v3 / to be removed when nested-block cloning lands

### Known
- 309/311 tests pass. Two preexisting test bugs remain (unrelated to Phase A):
  - `AssemblyNodeEdgeCaseTests.Reparenting_MoveSubtree` — duplicate `Assert.Equal` lines, the first asserts `3` and the second `4` for the same value
  - `VariantManagerTests.InvalidateCache_AfterInvalidation_NewVariantCreated` — test-double generates IDs deterministically from hash, so re-creating the same variant returns the same Guid; the real `VariantManager` would create a fresh `InstanceDefinition` (new Guid)

---

## [Unreleased] - Sprint 2 Complete + Architecture v3

### Added
- Per-Instance Component Visibility Architecture v3 (Definition Cloning)
- Custom C++ Object Research (CRhinoBrepObject + ON_UserData + Custom Grips)
- VisualARQ Reverse Engineering Study
- SolidWorks Configuration System Analysis
- Product Vision v2 ("BlockForge")
- Assembly Object Architecture Design
- Custom Object Feasibility Study + Prototype Code
- User Guide with 46-point testing checklist
- 97 new unit tests (8 test files)
- Plugin icons (256/48/24px)
- GitHub Actions CI workflow
- Eto UI column sizing fixes

### Changed
- Removed C# DisplayConduit (C++ conduit only → will be replaced by Definition Cloning)
- Yak build script improved (native DLL bundling, version override, icon)
- All 5 review blockers fixed (B1-B5)
- Documentation audit fixes (README, CLAUDE.md, CONTRIBUTING, ARCHITECTURE)

### Architecture Decision
- **DisplayConduit approach abandoned** — Rhino renders block instances atomically
- **New approach: Definition Cloning + ON_UserData + Custom Grips (C++)**
- Inspired by VisualARQ architecture (Standard geometry + UserData)

---

## [2.0.0-alpha.2] — 2026-02-15

### Added
- **ComponentState enum** — 4-state model replacing boolean visibility: Visible(0), Hidden(1), Suppressed(2), Transparent(3)
- **SetComponentState / GetComponentState** — new API functions for rich state control
- **Snapshot pattern** — `CVisibilitySnapshot` taken once per frame at SC_PREDRAWOBJECTS, eliminates lock contention during rendering
- **SC_CALCBOUNDINGBOX** channel — correct ZoomExtents with only visible components (suppressed excluded from bbox)
- **SC_POSTDRAWOBJECTS** channel — selection highlights via `DrawObject` (no per-frame heap allocations, no manual edge extraction)
- **SC_PREDRAWOBJECTS** channel — frame-start snapshot acquisition
- **HasHiddenDescendants optimization** — precomputed parent prefix set for O(1) nested block recursion decisions
- **Transparent rendering** — CS_TRANSPARENT draws components with ~30% alpha opacity

### Changed
- **Native API version** bumped to 4 (from 3)
- **CVisibilityData** now stores `ComponentState` per path instead of simple hidden set
- **CVisibilityConduit** expanded from 1 channel (SC_DRAWOBJECT) to 4 channels (PREDRAW/DRAWOBJECT/POSTDRAW/CALCBBOX)
- **Thread model** — single lock per frame via snapshot instead of per-object per-channel locking

---

## [1.0.0-rc1] — 2026-02-15

### Added
- **Hierarchical tree view** — recursive block instance visualization with expand/collapse
- **Bidirectional selection sync** — click in tree ↔ select in viewport (with 100ms debouncing)
- **Visibility toggle** — eye icon column to show/hide instances
- **Show/Hide with Dependents** — recursive visibility for node + all descendants
- **Mixed-state parent icon** (◐) when some children are hidden
- **Show All** action (Space) to reset all visibility
- **Isolate mode** with banner ("Isolate Mode — N of M visible") and exit button
- **Assembly Mode** — Set as Assembly Root to focus on a single sub-assembly
- **Mode dropdown** — switch between Document Mode and Assembly Mode
- **Search & filter** — filter tree by object/definition name (case-insensitive)
- **Context menu** — organized into Visibility, Selection, Navigation, and Editing sections
- **Detail panel** — selected item properties and user attributes (UserText)
- **Keyboard shortcuts** — H (hide), S (show), I (isolate), Space (show all), F (zoom), Del (delete), Enter (block edit)
- **Grayed/italic styling** for hidden items in the tree
- **Collapse All / Expand All** toolbar buttons
- **Double-click** on block instance → BlockEdit
- **Status bar** — "N instances, N definitions, N hidden"
- **Instance count display** — how many instances of each definition exist
- **Layer display** — layer assignment shown per instance
- **Link type icons** — Embedded (📦), Linked (🔗), LinkedAndEmbedded (📎)
- **Event debouncing** — 100ms debounce on document events to prevent UI thrashing
- **Error handling** — graceful degradation with try-catch and recursion limits
- **Per-Instance Component Visibility PoC** — C# DisplayConduit proof-of-concept (validated approach)
- **C++ native DLL scaffold** — VisibilityConduit with P/Invoke exports (7 exports verified)

---

## Planned

### [2.0.0] — Per-Instance Component Visibility
- C++ DisplayConduit — SC_DRAWOBJECT interception for per-component drawing
- ON_UserData persistence — hidden component UUIDs saved in .3dm file
- P/Invoke bridge — C#/C++ integration
- Component tree nodes — expand block instances to see individual components
- Per-component eye icon — hide components within a single instance only
