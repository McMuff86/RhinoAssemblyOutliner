# Contributing to RhinoAssemblyOutliner

Thank you for your interest in contributing! This guide covers the development setup, project structure, conventions, and testing.

---

## Prerequisites

- **Rhino 8** (Windows) — required for debugging
- **Visual Studio 2022** or **JetBrains Rider**
- **.NET 7.0 SDK** (included with Rhino 8)
- **Rhino 8 C++ SDK** (only for native component work) — [Download](https://rhino3d.com/download/rhino-sdk/8/latest/)
- **MSVC v142 toolset** (only for C++ — install via VS Installer)

---

## Building from Source

### C# Plugin (main plugin)

```bash
git clone https://github.com/your-org/RhinoAssemblyOutliner.git
cd RhinoAssemblyOutliner

dotnet restore
dotnet build
```

The built `.rhp` file will be in `src/RhinoAssemblyOutliner/bin/Debug/net7.0/`.

### C++ Native Plugin (per-instance component visibility)

> Only needed if working on the C++ display conduit. The C# plugin works standalone without it.

1. Install the Rhino 8 C++ SDK
2. Open `RhinoAssemblyOutliner.sln` in Visual Studio 2022
3. Set build configuration to **Release x64**
4. Build the `RhinoAssemblyOutliner.Native` project
5. The C# project automatically copies the native DLL to its output directory

### Debugging in Rhino

1. In Visual Studio/Rider, set the C# plugin project as startup project
2. Configure the debug target:
   - **Executable:** `C:\Program Files\Rhino 8\System\Rhino.exe`
   - **Arguments:** `/nosplash`
3. Set a breakpoint and press F5
4. In Rhino, run `AssemblyOutliner` to open the panel

---

## Project Structure

```
RhinoAssemblyOutliner/
├── src/
│   ├── RhinoAssemblyOutliner/          # C# Plugin (.rhp)
│   │   ├── Commands/                    # Rhino commands (OpenOutliner, Refresh)
│   │   ├── Model/                       # Data model (AssemblyNode, TreeBuilder)
│   │   ├── Services/                    # Business logic
│   │   │   ├── SelectionSyncService     # Tree ↔ viewport sync
│   │   │   ├── VisibilityService        # Show/hide/isolate
│   │   │   └── BlockInfoService         # Block definition queries
│   │   ├── UI/                          # Eto.Forms UI
│   │   │   ├── AssemblyOutlinerPanel    # Main dockable panel
│   │   │   ├── AssemblyTreeView         # Tree grid view
│   │   │   └── DetailPanel              # Properties panel
│   │   ├── Properties/                  # Assembly info
│   │   └── RhinoAssemblyOutlinerPlugin.cs  # Plugin entry point
│   └── Native/                          # C++ Plugin (.rhp)
│       ├── VisibilityConduit.h/cpp      # SC_DRAWOBJECT conduit
│       ├── VisibilityData.h             # Thread-safe state management
│       ├── ComponentVisibilityData.h/cpp # ON_UserData persistence
│       └── NativeApi.h/cpp              # extern "C" P/Invoke exports
├── docs/
│   ├── USER_GUIDE.md                    # End-user documentation
│   ├── ARCHITECTURE_V2.md               # Technical architecture
│   └── plans/                           # Design documents & ADRs
├── research/                            # SDK research & analysis
├── CHANGELOG.md                         # Version history
├── manifest.yml                         # Yak package manifest
└── progress.txt                         # Development progress tracker
```

---

## Coding Conventions

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `AssemblyTreeBuilder` |
| Methods | PascalCase | `BuildTree()` |
| Private fields | `_camelCase` | `_visibilityService` |
| Local variables | camelCase | `blockNode` |
| Constants | PascalCase | `MaxRecursionDepth` |
| C++ classes | `C` prefix + PascalCase | `CPerInstanceVisibilityConduit` |
| C++ exports | `RAO_` prefix | `RAO_SetComponentHidden()` |

### Threading Rules

1. **All RhinoCommon API calls must happen on the UI thread** — use `RhinoApp.InvokeOnUiThread()` when marshalling from background threads
2. **Display conduit callbacks run on the display thread** — never call RhinoDoc methods from conduit code
3. **Use `_isSyncingFrom*` flags** to prevent infinite loops in bidirectional selection sync
4. **C++ conduit state** protected by `std::shared_mutex` (shared lock for reads, unique lock for writes)
5. **Timer callbacks** fire on ThreadPool threads — always marshal to UI thread before touching UI

### Commit Message Format

```
type: short description

Optional longer explanation.
```

**Types:**
- `feat:` — new feature
- `fix:` — bug fix
- `refactor:` — code restructuring (no behavior change)
- `docs:` — documentation only
- `test:` — adding/updating tests
- `chore:` — build, CI, tooling
- `nightly:` — work done during automated night sessions

**Examples:**
```
feat: add keyboard shortcuts for visibility toggle
fix: prevent stale doc reference in VisibilityService
docs: update user guide with assembly mode section
refactor: replace ObservableCollection with List in AssemblyNode
```

---

## Testing

### Unit Tests (Model Layer)

The model layer (`AssemblyNode`, tree operations) can be tested without Rhino:

```bash
dotnet test
```

### Integration Tests (Rhino API)

Integration tests require Rhino. Use [Rhino.Testing](https://github.com/mcneel/rhino.testing) or RhinoInside:

```csharp
[RhinoTest]
public void TreeBuilder_HandlesNestedBlocks() { ... }
```

### Manual Testing Matrix

When testing display-related changes, verify these scenarios:

| Scenario | Expected Result |
|----------|----------------|
| Toggle visibility on instance | Object appears/disappears immediately |
| Isolate → work → Show All | All objects return to pre-isolate state |
| Select in viewport | Corresponding tree node highlights |
| Select in tree | Object highlights in viewport |
| Large document (500+ instances) | Tree builds in <200ms |
| Keyboard shortcut H/S/I/Space/F | Actions trigger on focused tree node |

### Test Files

Place test `.3dm` files in a `tests/fixtures/` directory. Include:
- Simple document (1 block, no nesting)
- Nested blocks (3 levels deep)
- Large document (100+ instances)
- Linked blocks
- Self-referencing blocks (cycle detection test)

---

## Pull Request Process

1. Create a feature branch from `main`: `git checkout -b feat/my-feature`
2. Make your changes with appropriate tests
3. Ensure `dotnet build` succeeds with no warnings
4. Write a clear commit message following the format above
5. Open a PR with a description of what and why

---

## Architecture

See [ARCHITECTURE_V2.md](ARCHITECTURE_V2.md) for the full technical architecture, including the C++/C# hybrid design, data flow diagrams, and state management.

For design decisions, see the ADRs in [docs/plans/ADR/](plans/ADR/).
