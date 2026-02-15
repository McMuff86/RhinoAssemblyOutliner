# ADR-004: Performance Strategy

**Status:** Accepted  
**Date:** 2026-02-15

## Scale Targets

| Scale | Instances | Managed (with hidden) | Tree rebuild target |
|-------|-----------|----------------------|-------------------|
| Small | < 100 | < 10 | Instant |
| Medium | 100–1,000 | < 50 | < 50ms |
| Large | 1,000–10,000 | < 200 | < 200ms |
| XL | 10,000+ | < 500 | < 500ms with lazy loading |

## Decision: Four-Pronged Approach

### 1. Lazy Tree Loading

**Current:** `BuildTree()` traverses all blocks recursively, creates all nodes upfront — O(n×d).

**New:** Top-level instances load immediately. Children load on expand.

```csharp
public class LazyBlockInstanceNode : AssemblyNode {
    private bool _childrenLoaded = false;
    public override IEnumerable<AssemblyNode> Children {
        get {
            if (!_childrenLoaded) { LoadChildren(); _childrenLoaded = true; }
            return _children;
        }
    }
}
```

Initial load becomes O(top-level count) regardless of total instances or nesting depth.

### 2. Tiered Event Debouncing

Replace single 100ms timer with event-type-specific debouncing:

| Event | Debounce | Reason |
|-------|----------|--------|
| Selection change | 0ms (immediate) | User expects instant feedback |
| Object add/delete | 100ms | Batch operations common |
| Definition change | 250ms | BlockEdit sends many events |
| Document open | 500ms | Large documents flood events |

### 3. Incremental Tree Updates

**Current:** Any document change → full tree rebuild.

**New:** Targeted updates:
- Instance added → insert node under parent
- Instance deleted → remove node
- Definition changed → rebuild subtrees for affected definition only
- Full rebuild only on document open/close

### 4. Display Pipeline Optimization (C++)

- **Unmanaged instances:** Zero overhead — O(1) HashSet miss in conduit
- **Managed instances:** `dp.DrawObject()` with `CRhinoCacheHandle` — GPU mesh reuse
- **Cache invalidation:** Only on definition change or visibility state change
- **Optimization path:** If 200+ managed instances become slow, pre-build filtered display lists per unique visibility state

### Caching Architecture

```
Document Cache (per RhinoDoc)
├── Definition Instance Count: Dict<int defIndex, int count>
├── Tree Node Lookup: Dict<Guid instanceId, AssemblyNode>
└── Component Info: Dict<Guid instanceId, List<ComponentInfo>>
All invalidated on: instance add/delete, definition change
```

### What We Don't Do (Yet)

- **Virtualized tree** — Eto's TreeGridView doesn't support it. Lazy loading + pagination ("Load more...") for XL documents instead.
- **Background tree building** — Adds threading complexity. Only if lazy loading proves insufficient.
- **WebView-based tree** — Full virtualization possible but high dev cost. v3 option.

## Consequences

- Initial panel open is fast regardless of document size
- Common operations (selection, single hide/show) feel instant
- Large document editing (add 100 blocks) doesn't thrash UI
- Memory footprint proportional to expanded nodes, not total nodes
