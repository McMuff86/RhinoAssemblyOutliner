# RhinoAssemblyOutliner – Test Plan

## 1. Übersicht

Dieses Dokument definiert die Teststrategie für das RhinoAssemblyOutliner Plugin.

### 1.1 Ziele

- Sicherstellung der korrekten Block-Hierarchie-Traversierung
- Validierung der bidirektionalen Selektion
- Performance-Garantien bei grossen Dokumenten
- Robustheit bei Edge Cases (leere Dokumente, tiefe Verschachtelungen)

### 1.2 Test-Pyramide

```
         /  \
        / UI \        ← Manuelle Tests (Rhino Integration)
       /------\
      / Integr. \     ← Integration mit RhinoCommon Mocks
     /------------\
    /   Unit Tests  \  ← Isolierte Logik-Tests
   /------------------\
```

---

## 2. Unit Test Strategie

### 2.1 Framework

- **Test Framework:** xUnit 2.x
- **Mocking:** Moq
- **Assertions:** FluentAssertions
- **Coverage Tool:** Coverlet

### 2.2 Testbare Komponenten

Da RhinoCommon einen laufenden Rhino-Host benötigt, isolieren wir die Logik durch Abstraktion:

| Komponente | Testbar | Strategie |
|------------|---------|-----------|
| `AssemblyTreeBuilder` | ✅ | Interface für Block-Daten |
| `AssemblyNode` (Model) | ✅ | Pure C# Klassen |
| `SelectionSyncService` | ⚠️ | Mock für `RhinoDoc` |
| `VisibilityService` | ⚠️ | Mock für `RhinoObject` |
| UI (Eto.Forms) | ❌ | Manuell testen |

### 2.3 Abstraktions-Interfaces

```csharp
// Ermöglicht Unit Tests ohne RhinoCommon Runtime
public interface IBlockDataProvider
{
    IEnumerable<IBlockInstance> GetTopLevelInstances();
    IBlockDefinition GetDefinition(Guid definitionId);
    IEnumerable<IBlockInstance> GetNestedInstances(Guid parentDefinitionId);
}

public interface IBlockInstance
{
    Guid Id { get; }
    string Name { get; }
    Guid DefinitionId { get; }
    bool IsVisible { get; }
    string LayerName { get; }
}

public interface IBlockDefinition
{
    Guid Id { get; }
    string Name { get; }
    BlockLinkType LinkType { get; }
    IEnumerable<Guid> NestedDefinitionIds { get; }
}
```

---

## 3. Test-Szenarien

### 3.1 Block-Hierarchie Traversierung

**Ziel:** Validierung der rekursiven Baum-Konstruktion

| Test ID | Szenario | Erwartung |
|---------|----------|-----------|
| TRV-001 | Flache Block-Liste (keine Verschachtelung) | Alle Instanzen auf Root-Ebene |
| TRV-002 | Ein Block mit einer Child-Instanz | Parent → Child Hierarchie |
| TRV-003 | Mehrere Instanzen derselben Definition | Korrekte Nummerierung (#1, #2, ...) |
| TRV-004 | Zirkuläre Referenz-Erkennung | Exception oder graceful handling |
| TRV-005 | Block-Definition ohne Instanzen | Nicht im Baum angezeigt |

```csharp
[Fact]
public void BuildTree_WithFlatBlocks_ReturnsAllAtRootLevel()
{
    // Arrange
    var provider = CreateMockProvider(new[] { blockA, blockB, blockC });
    var builder = new AssemblyTreeBuilder(provider);
    
    // Act
    var tree = builder.Build();
    
    // Assert
    tree.RootNodes.Should().HaveCount(3);
    tree.RootNodes.Should().AllSatisfy(n => n.Children.Should().BeEmpty());
}
```

### 3.2 Verschachtelte Blöcke (3+ Ebenen)

**Ziel:** Korrekte Tiefentraversierung

| Test ID | Szenario | Erwartung |
|---------|----------|-----------|
| NEST-001 | 3 Ebenen: A → B → C | Pfad korrekt: A/B/C |
| NEST-002 | 5 Ebenen Verschachtelung | Alle Ebenen traversiert |
| NEST-003 | Mehrere Children pro Ebene | Breiten-Traversierung korrekt |
| NEST-004 | Gemischte Tiefen (A→B, C→D→E) | Unterschiedliche Tiefen im selben Baum |
| NEST-005 | Dieselbe Definition auf mehreren Ebenen | Jede Instanz separat angezeigt |

```csharp
[Fact]
public void BuildTree_With3LevelNesting_CreatesCorrectHierarchy()
{
    // Arrange: Schrank → Schublade → Griff
    var provider = CreateNestedMockProvider(depth: 3);
    var builder = new AssemblyTreeBuilder(provider);
    
    // Act
    var tree = builder.Build();
    
    // Assert
    var schrank = tree.RootNodes.First();
    var schublade = schrank.Children.First();
    var griff = schublade.Children.First();
    
    schrank.Name.Should().Contain("Schrank");
    schublade.Name.Should().Contain("Schublade");
    griff.Name.Should().Contain("Griff");
    griff.Children.Should().BeEmpty();
}
```

### 3.3 Leeres Dokument

**Ziel:** Graceful handling von Edge Cases

| Test ID | Szenario | Erwartung |
|---------|----------|-----------|
| EMPTY-001 | Keine Block-Instanzen | Leerer Baum, keine Exception |
| EMPTY-002 | Keine Block-Definitionen | Leerer Baum |
| EMPTY-003 | Nur lose Geometrie (keine Blöcke) | Root zeigt nur "Loose Geometry" |
| EMPTY-004 | Null Document | ArgumentNullException |

```csharp
[Fact]
public void BuildTree_WithEmptyDocument_ReturnsEmptyTree()
{
    // Arrange
    var provider = CreateEmptyMockProvider();
    var builder = new AssemblyTreeBuilder(provider);
    
    // Act
    var tree = builder.Build();
    
    // Assert
    tree.RootNodes.Should().BeEmpty();
    tree.TotalNodeCount.Should().Be(0);
}
```

### 3.4 Performance: 1000+ Block-Instanzen

**Ziel:** Skalierbarkeit nachweisen

| Test ID | Szenario | Erwartung |
|---------|----------|-----------|
| PERF-001 | 1000 flache Instanzen | Build < 100ms |
| PERF-002 | 1000 Instanzen, 3 Ebenen tief | Build < 200ms |
| PERF-003 | 5000 Instanzen | Build < 500ms |
| PERF-004 | Inkrementelles Update (1 neu) | Update < 10ms |
| PERF-005 | Memory bei 10000 Nodes | < 50 MB zusätzlich |

```csharp
[Fact]
public void BuildTree_With1000Instances_CompletesUnder100ms()
{
    // Arrange
    var provider = CreateLargeMockProvider(instanceCount: 1000);
    var builder = new AssemblyTreeBuilder(provider);
    var sw = Stopwatch.StartNew();
    
    // Act
    var tree = builder.Build();
    sw.Stop();
    
    // Assert
    sw.ElapsedMilliseconds.Should().BeLessThan(100);
    tree.TotalNodeCount.Should().Be(1000);
}

[Fact]
public void BuildTree_With1000NestedInstances_CompletesUnder200ms()
{
    // Arrange: 1000 Instanzen auf 3 Ebenen verteilt
    var provider = CreateNestedLargeMockProvider(
        instanceCount: 1000, 
        maxDepth: 3
    );
    var builder = new AssemblyTreeBuilder(provider);
    var sw = Stopwatch.StartNew();
    
    // Act
    var tree = builder.Build();
    sw.Stop();
    
    // Assert
    sw.ElapsedMilliseconds.Should().BeLessThan(200);
}
```

### 3.5 Bidirektionale Selektion

**Ziel:** Sync zwischen Baum und Viewport

| Test ID | Szenario | Erwartung |
|---------|----------|-----------|
| SEL-001 | Node im Baum klicken | Objekt im Viewport selektiert |
| SEL-002 | Objekt im Viewport selektieren | Node im Baum highlighted |
| SEL-003 | Multi-Select im Baum | Alle Objekte selektiert |
| SEL-004 | Viewport-Selektion löschen | Baum-Highlight entfernt |
| SEL-005 | "Alle gleichen selektieren" | Alle Instanzen der Definition |
| SEL-006 | Verschachtelte Instanz selektieren | Parent bleibt unselektiert |

```csharp
[Fact]
public void SelectionSync_WhenTreeNodeSelected_SelectsViewportObject()
{
    // Arrange
    var mockDoc = CreateMockRhinoDoc();
    var syncService = new SelectionSyncService(mockDoc);
    var node = new BlockInstanceNode { ObjectId = Guid.NewGuid() };
    
    // Act
    syncService.OnTreeSelectionChanged(new[] { node });
    
    // Assert
    mockDoc.Verify(d => d.Objects.Select(node.ObjectId), Times.Once);
}

[Fact]
public void SelectionSync_SelectAllSame_SelectsAllInstancesOfDefinition()
{
    // Arrange
    var definitionId = Guid.NewGuid();
    var instances = Enumerable.Range(0, 5)
        .Select(_ => new BlockInstanceNode { DefinitionId = definitionId })
        .ToList();
    var syncService = new SelectionSyncService(mockDoc);
    
    // Act
    syncService.SelectAllInstancesOf(definitionId);
    
    // Assert
    mockDoc.Verify(d => d.Objects.Select(It.IsAny<Guid>()), Times.Exactly(5));
}
```

### 3.6 Visibility Toggle

**Ziel:** Ein-/Ausblenden von Objekten

| Test ID | Szenario | Erwartung |
|---------|----------|-----------|
| VIS-001 | Toggle einzelne Instanz | Nur diese Instanz betroffen |
| VIS-002 | Toggle Parent mit Children | Alle Children auch togglen |
| VIS-003 | Hidden Parent, Visible Child | Child-Visibility respektieren |
| VIS-004 | Isolieren einer Instanz | Nur diese sichtbar |
| VIS-005 | "Alle anzeigen" Reset | Alle vorher versteckten wieder sichtbar |
| VIS-006 | Visibility State persistieren | Nach Reload korrekt |

```csharp
[Fact]
public void VisibilityToggle_WhenParentHidden_HidesAllChildren()
{
    // Arrange
    var parent = new BlockInstanceNode 
    { 
        Children = new[] { childA, childB, childC } 
    };
    var visService = new VisibilityService(mockDoc);
    
    // Act
    visService.SetVisibility(parent, visible: false, recursive: true);
    
    // Assert
    parent.IsVisible.Should().BeFalse();
    parent.Children.Should().AllSatisfy(c => c.IsVisible.Should().BeFalse());
}

[Fact]
public void Isolate_OnlySelectedNodeVisible()
{
    // Arrange
    var allNodes = new[] { nodeA, nodeB, nodeC };
    var visService = new VisibilityService(mockDoc);
    
    // Act
    visService.Isolate(nodeB);
    
    // Assert
    nodeA.IsVisible.Should().BeFalse();
    nodeB.IsVisible.Should().BeTrue();
    nodeC.IsVisible.Should().BeFalse();
}
```

---

## 4. Integration Tests

### 4.1 RhinoInside.Resolver Tests

Für echte RhinoCommon Integration ohne GUI:

```csharp
// Erfordert RhinoInside NuGet Package
[Collection("RhinoInside")]
public class RhinoIntegrationTests
{
    [Fact]
    public void LoadRhino3dmFile_ExtractsBlockHierarchy()
    {
        // Arrange
        RhinoInside.Resolver.Initialize();
        var doc = File3dm.Read("TestData/nested-blocks.3dm");
        
        // Act
        var provider = new Rhino3dmBlockDataProvider(doc);
        var builder = new AssemblyTreeBuilder(provider);
        var tree = builder.Build();
        
        // Assert
        tree.RootNodes.Should().NotBeEmpty();
    }
}
```

### 4.2 Test-Daten Files

```
tests/
└── TestData/
    ├── empty-document.3dm
    ├── flat-blocks.3dm
    ├── nested-3-levels.3dm
    ├── nested-5-levels.3dm
    ├── 1000-instances.3dm
    └── circular-reference.3dm
```

---

## 5. Manuelle Test-Checkliste

Für UI-Features die nicht automatisiert testbar sind:

### 5.1 Panel Integration

- [ ] Panel öffnet sich via Command
- [ ] Panel ist dockbar (links, rechts)
- [ ] Panel persistiert Position nach Rhino-Neustart
- [ ] Panel scrollt bei vielen Nodes

### 5.2 Visuelle Tests

- [ ] Icons werden korrekt angezeigt
- [ ] Einrückung entspricht Hierarchie-Tiefe
- [ ] Selection-Highlight ist sichtbar
- [ ] Suchfilter-Highlighting

### 5.3 Event Handling

- [ ] Tree aktualisiert bei Block-Erstellung
- [ ] Tree aktualisiert bei Block-Löschung
- [ ] Tree aktualisiert bei Undo/Redo
- [ ] Kein Flackern bei Massenoperationen

---

## 6. CI/CD Integration

### 6.1 GitHub Actions Workflow

```yaml
name: Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal
```

### 6.2 Coverage Threshold

- **Minimum Coverage:** 70% für `/Model/` und `/Services/`
- **Performance Tests:** Nur auf Dedicated Runner (nicht bei jedem PR)

---

## 7. Test-Priorisierung

### Phase 1 (MVP)
1. ✅ Block-Hierarchie Traversierung (TRV-*)
2. ✅ Verschachtelte Blöcke (NEST-*)
3. ✅ Leeres Dokument (EMPTY-*)

### Phase 2 (Pre-Release)
4. ✅ Bidirektionale Selektion (SEL-*)
5. ✅ Visibility Toggle (VIS-*)

### Phase 3 (Optimization)
6. ✅ Performance Tests (PERF-*)
7. ✅ Integration Tests
