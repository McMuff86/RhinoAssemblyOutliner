# Architecture Overview

This document describes the high-level architecture of the Rhino Assembly Outliner plugin.

## System Context

```mermaid
graph TB
    subgraph "Rhino 8"
        RH[Rhino Application]
        DOC[RhinoDoc]
        VP[Viewport]
    end
    
    subgraph "Assembly Outliner Plugin"
        PLUGIN[RhinoAssemblyOutlinerPlugin]
        PANEL[AssemblyOutlinerPanel]
        SERVICES[Services Layer]
    end
    
    USER((User))
    
    USER -->|Interacts| PANEL
    USER -->|3D Navigation| VP
    PLUGIN -->|Registers| RH
    PANEL -->|Docked in| RH
    SERVICES -->|Reads/Writes| DOC
    VP -->|Selection Events| SERVICES
    SERVICES -->|Updates| VP
```

## Component Architecture

```mermaid
graph TB
    subgraph "UI Layer (Eto.Forms)"
        PANEL[AssemblyOutlinerPanel]
        TREE[AssemblyTreeView]
        DETAIL[DetailPanel]
        SEARCH[SearchFilterBar]
    end
    
    subgraph "Model Layer"
        BUILDER[AssemblyTreeBuilder]
        ANODE[AssemblyNode]
        BNODE[BlockInstanceNode]
        GNODE[GeometryNode]
        DNODE[DocumentNode]
    end
    
    subgraph "Services Layer"
        SYNC[SelectionSyncService]
        VIS[VisibilityService]
        EVENTS[DocumentEventService]
        INFO[BlockInfoService]
    end
    
    subgraph "RhinoCommon API"
        RDOC[RhinoDoc]
        ROBJ[RhinoObject]
        RBLOCK[BlockInstance]
    end
    
    PANEL --> TREE
    PANEL --> DETAIL
    PANEL --> SEARCH
    
    TREE --> BUILDER
    BUILDER --> ANODE
    ANODE --> BNODE
    ANODE --> GNODE
    ANODE --> DNODE
    
    TREE --> SYNC
    TREE --> VIS
    PANEL --> EVENTS
    DETAIL --> INFO
    
    SYNC --> RDOC
    VIS --> ROBJ
    EVENTS --> RDOC
    INFO --> RBLOCK
```

## Data Flow

### Tree Building

```mermaid
sequenceDiagram
    participant UI as AssemblyTreeView
    participant Builder as AssemblyTreeBuilder
    participant Doc as RhinoDoc
    participant Node as AssemblyNode
    
    UI->>Builder: BuildTree()
    Builder->>Doc: GetObjects()
    Doc-->>Builder: RhinoObject[]
    
    loop For each object
        alt Is BlockInstance
            Builder->>Builder: ProcessBlockInstance()
            Builder->>Node: Create BlockInstanceNode
            Builder->>Builder: ProcessChildren(recursive)
        else Is Geometry
            Builder->>Node: Create GeometryNode
        end
    end
    
    Builder-->>UI: DocumentNode (tree root)
    UI->>UI: Render TreeView
```

### Bidirectional Selection Sync

```mermaid
sequenceDiagram
    participant VP as Viewport
    participant Sync as SelectionSyncService
    participant Tree as AssemblyTreeView
    participant User as User
    
    Note over VP,User: Viewport → Tree
    User->>VP: Select object
    VP->>Sync: SelectionChanged event
    Sync->>Sync: Find matching node
    Sync->>Tree: ExpandToNode()
    Sync->>Tree: SelectNode()
    
    Note over VP,User: Tree → Viewport
    User->>Tree: Click node
    Tree->>Sync: OnNodeSelected()
    Sync->>VP: SelectObject()
    Sync->>VP: ZoomToObject()
```

### Visibility Toggle

```mermaid
sequenceDiagram
    participant User as User
    participant Tree as AssemblyTreeView
    participant Vis as VisibilityService
    participant Doc as RhinoDoc
    participant VP as Viewport
    
    User->>Tree: Click visibility icon
    Tree->>Vis: ToggleVisibility(node)
    
    alt Single Object
        Vis->>Doc: SetObjectHidden(guid, state)
    else Block Instance (recursive)
        Vis->>Vis: GetAllChildren(node)
        loop For each child
            Vis->>Doc: SetObjectHidden(guid, state)
        end
    end
    
    Vis->>VP: Redraw()
    Vis->>Tree: UpdateNodeState()
```

## Class Diagram

```mermaid
classDiagram
    class AssemblyNode {
        <<abstract>>
        +Guid Id
        +string DisplayName
        +bool IsVisible
        +AssemblyNode Parent
        +List~AssemblyNode~ Children
        +GetContextMenuItems()
    }
    
    class DocumentNode {
        +string FilePath
        +RhinoDoc Document
    }
    
    class BlockInstanceNode {
        +Guid DefinitionId
        +string DefinitionName
        +int InstanceNumber
        +string Layer
        +BlockLinkType LinkType
        +Transform Transform
    }
    
    class GeometryNode {
        +ObjectType GeometryType
        +string Layer
    }
    
    AssemblyNode <|-- DocumentNode
    AssemblyNode <|-- BlockInstanceNode
    AssemblyNode <|-- GeometryNode
    
    class AssemblyTreeBuilder {
        -RhinoDoc _document
        +BuildTree() DocumentNode
        -ProcessObject(RhinoObject) AssemblyNode
        -ProcessBlockInstance(BlockInstance) BlockInstanceNode
    }
    
    AssemblyTreeBuilder ..> DocumentNode : creates
    AssemblyTreeBuilder ..> BlockInstanceNode : creates
    AssemblyTreeBuilder ..> GeometryNode : creates
```

## Services

```mermaid
classDiagram
    class ISelectionSyncService {
        <<interface>>
        +SelectInViewport(Guid)
        +SelectInTree(Guid)
        +OnViewportSelectionChanged(EventArgs)
    }
    
    class IVisibilityService {
        <<interface>>
        +ToggleVisibility(AssemblyNode)
        +ShowOnly(AssemblyNode)
        +ShowAll()
    }
    
    class IDocumentEventService {
        <<interface>>
        +Subscribe(RhinoDoc)
        +Unsubscribe()
        +event TreeInvalidated
    }
    
    class IBlockInfoService {
        <<interface>>
        +GetBlockInfo(Guid) BlockInfo
        +GetInstanceCount(Guid) int
        +GetAllInstances(Guid) List~Guid~
    }
    
    class SelectionSyncService {
        -RhinoDoc _document
        -bool _isSyncing
    }
    
    class VisibilityService {
        -RhinoDoc _document
    }
    
    class DocumentEventService {
        -RhinoDoc _document
    }
    
    class BlockInfoService {
        -RhinoDoc _document
    }
    
    ISelectionSyncService <|.. SelectionSyncService
    IVisibilityService <|.. VisibilityService
    IDocumentEventService <|.. DocumentEventService
    IBlockInfoService <|.. BlockInfoService
```

## Plugin Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Loading: Rhino starts
    Loading --> Loaded: OnLoad()
    Loaded --> PanelClosed: Default state
    
    PanelClosed --> PanelOpen: User opens panel
    PanelOpen --> PanelClosed: User closes panel
    
    PanelOpen --> TreeBuilding: Document opened
    TreeBuilding --> TreeReady: Build complete
    TreeReady --> TreeBuilding: Document changed
    TreeReady --> PanelOpen: Document closed
    
    Loaded --> Unloading: Rhino closes
    Unloading --> [*]: OnUnload()
```

## Key Design Decisions

### 1. Eto.Forms for UI
- Cross-platform compatibility (Windows/Mac)
- Native Rhino integration
- Consistent look with Rhino panels

### 2. Service Layer Pattern
- Separates UI from business logic
- Easier testing and maintenance
- Clear responsibilities

### 3. Recursive Tree Building
- Handles arbitrary nesting depth
- Lazy loading for performance (future)
- Mirrors actual document structure

### 4. Event-Driven Sync
- Responds to Rhino document events
- Bidirectional selection sync
- Minimal polling

## Performance Considerations

- **Large Documents**: Implement virtual scrolling for 1000+ nodes
- **Deep Nesting**: Consider lazy loading child nodes
- **Frequent Updates**: Debounce document change events
- **Selection Sync**: Use flags to prevent circular updates

## Future Extensions

- BOM (Bill of Materials) export
- Custom node icons per block type
- Saved tree configurations
- Grasshopper integration (read-only)
