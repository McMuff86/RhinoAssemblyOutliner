# Contributing to Rhino Assembly Outliner

Thank you for your interest in contributing! This document provides guidelines and setup instructions.

## Development Setup

### Prerequisites

- **Rhino 8** (Windows or Mac)
- **Visual Studio 2022** (Windows) or **JetBrains Rider** (cross-platform)
- **.NET 7.0 SDK** or later
- **Git**

### Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-org/RhinoAssemblyOutliner.git
   cd RhinoAssemblyOutliner
   ```

2. **Open the solution**
   ```bash
   # Windows
   start RhinoAssemblyOutliner.sln
   
   # Or with Rider
   rider RhinoAssemblyOutliner.sln
   ```

3. **Restore NuGet packages**
   Visual Studio/Rider should restore automatically. Manual restore:
   ```bash
   dotnet restore
   ```

4. **Build the plugin**
   ```bash
   dotnet build
   ```

5. **Debug in Rhino**
   - Set `RhinoAssemblyOutliner` as startup project
   - Configure debug settings to launch Rhino 8
   - Press F5 to start debugging

### Project Structure

```
RhinoAssemblyOutliner/
├── RhinoAssemblyOutliner.sln
├── RhinoAssemblyOutliner/
│   ├── RhinoAssemblyOutlinerPlugin.cs  # Plugin entry point
│   ├── Commands/                        # Rhino commands
│   ├── UI/                              # Eto.Forms UI components
│   ├── Model/                           # Data models & tree builder
│   ├── Services/                        # Business logic services
│   └── Resources/                       # Icons and assets
└── docs/                                # Documentation
```

## Coding Standards

### C# Style Guide

We follow the [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) with these additions:

- **Naming**
  - Use `PascalCase` for public members, types, and methods
  - Use `camelCase` for local variables and parameters
  - Use `_camelCase` for private fields
  - Prefix interfaces with `I` (e.g., `ISelectionService`)

- **Files**
  - One class per file
  - File name matches class name
  - Organize with folders matching namespaces

- **Documentation**
  - XML documentation for all public APIs
  - Inline comments for complex logic
  - Keep comments up-to-date with code changes

### Code Example

```csharp
namespace RhinoAssemblyOutliner.Services
{
    /// <summary>
    /// Synchronizes selection between the tree view and Rhino viewport.
    /// </summary>
    public class SelectionSyncService : ISelectionSyncService
    {
        private readonly RhinoDoc _document;
        private bool _isSyncing;

        public SelectionSyncService(RhinoDoc document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Selects the specified block instance in the viewport.
        /// </summary>
        /// <param name="instanceId">The GUID of the block instance.</param>
        public void SelectInViewport(Guid instanceId)
        {
            if (_isSyncing) return;
            
            // Implementation...
        }
    }
}
```

### UI Guidelines (Eto.Forms)

- Keep UI code separate from business logic
- Use data binding where possible
- Test on both Windows and Mac
- Follow Rhino's visual style conventions

## Pull Request Process

### Before You Start

1. **Check existing issues** - Is someone already working on this?
2. **Open an issue** - Discuss larger changes before implementing
3. **Create a branch** - Branch from `main` with a descriptive name

### Branch Naming

```
feature/description    # New features
fix/description        # Bug fixes
docs/description       # Documentation
refactor/description   # Code refactoring
```

### Making Changes

1. **Keep commits atomic** - One logical change per commit
2. **Write clear commit messages**
   ```
   feat: add visibility toggle to tree nodes
   
   - Implement eye icon button in TreeViewItem
   - Add VisibilityService for show/hide logic
   - Wire up bidirectional state sync
   ```
3. **Update documentation** - Keep docs in sync with changes
4. **Add tests** - For new features and bug fixes

### Submitting a PR

1. **Push your branch**
   ```bash
   git push origin feature/your-feature
   ```

2. **Open a Pull Request** on GitHub
   - Fill out the PR template
   - Reference related issues
   - Add screenshots for UI changes

3. **PR Checklist**
   - [ ] Code builds without errors
   - [ ] Follows coding standards
   - [ ] Tests pass (if applicable)
   - [ ] Documentation updated
   - [ ] Tested on Rhino 8

4. **Review Process**
   - Address reviewer feedback
   - Keep PR scope focused
   - Squash commits if requested

### After Merge

- Delete your feature branch
- Close related issues
- Celebrate! 🎉

## Getting Help

- **Issues**: Open a GitHub issue for bugs or feature requests
- **Discussions**: Use GitHub Discussions for questions
- **Rhino Developer Docs**: [developer.rhino3d.com](https://developer.rhino3d.com)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
