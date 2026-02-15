# Changelog

All notable changes to RhinoAssemblyOutliner will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
