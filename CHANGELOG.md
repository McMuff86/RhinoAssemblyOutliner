# Changelog

All notable changes to RhinoAssemblyOutliner will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased] — v0.2.0-dev (Sprint 1: UX Polish)

### Added
- Keyboard shortcuts: H (hide), S (show), I (isolate), Space (show all), F (zoom to), Del (delete), Enter (block edit)
- Grayed/italic styling for hidden items in the tree
- Mixed-state parent eye icon (◐) when some children are hidden
- Show All action (Space) to reset all visibility
- Show with Dependents — recursive show for node + all descendants
- Hide with Dependents — recursive hide for node + all descendants
- Isolate mode with banner ("Isolate Mode — N of M visible") and exit button
- Collapse All / Expand All toolbar buttons
- Double-click on block instance → BlockEdit
- Status bar: "N instances, N definitions, N hidden"
- Restructured context menu with Visibility, Selection, Navigation, Editing sections

### Changed
- Context menu reorganized per UX analysis recommendations

---

## [0.1.0] — 2026-02-06

Initial release with core assembly outliner functionality.

### Added
- **Hierarchical tree view** — recursive block instance visualization with expand/collapse
- **Bidirectional selection sync** — click in tree ↔ select in viewport (with debouncing)
- **Visibility toggle** — eye icon column to show/hide instances
- **Search & filter** — filter tree by object/definition name (case-insensitive)
- **Context menu** — Select, Select All Same, Zoom To, Isolate, Hide, Edit Block, Properties
- **Detail panel** — shows selected item properties and user attributes (UserText)
- **Assembly Mode** — Set as Assembly Root to focus on a single sub-assembly
- **Mode dropdown** — switch between Document Mode and Assembly Mode
- **Instance count** — shows how many instances of each definition exist
- **Layer display** — layer assignment shown per instance
- **Link type icons** — Embedded (📦), Linked (🔗), LinkedAndEmbedded (📎)
- **Event debouncing** — 100ms debounce on document events to prevent UI thrashing
- **Error handling** — graceful degradation with try-catch and recursion limits
- **Panel icon** — manifest.yml prepared for Yak packaging
- Per-Instance Component Visibility PoC (C# DisplayConduit proof-of-concept, validated approach)
- C++ native DLL scaffold with VisibilityConduit and P/Invoke exports (7 exports verified)

---

## Planned

### [1.0.0] — Sprint 2: Stable Release

- Bug bash and systematic testing
- Fix VisibilityService document reference leak
- Fix duplicate panel registration
- IDisposable on panel for proper timer cleanup
- Unit tests for model layer (≥80% coverage)
- Plugin icon (256×256 PNG)
- README with screenshots
- Yak package build script
- Tested on clean Rhino 8 install
- Published to Yak Package Manager

### [2.0.0] — Sprint 3-4: Per-Instance Component Visibility

- **C++ DisplayConduit** — SC_DRAWOBJECT interception for per-component drawing
- **ON_UserData persistence** — hidden component UUIDs saved in .3dm file
- **P/Invoke bridge** — C#/C++ integration (10 extern "C" functions)
- **Component tree nodes** — expand block instances to see individual components
- **Per-component eye icon** — hide components within a single instance only
- **Display cache integration** — CRhinoCacheHandle for GPU-cached rendering
- **Thread-safe conduit** — std::shared_mutex for display thread safety
- **Definition change handler** — validate UUIDs on BlockEdit exit
- **Performance target** — >30fps with 100 managed instances
- **Graceful degradation** — C# plugin works without C++ (instance-level visibility only)
