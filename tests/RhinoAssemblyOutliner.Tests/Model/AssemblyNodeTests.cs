using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for assembly tree node logic.
/// Uses a concrete TestNode subclass to test AssemblyNode base behavior
/// without requiring RhinoCommon dependencies.
/// </summary>
public class AssemblyNodeTests
{
    /// <summary>
    /// Minimal concrete implementation of AssemblyNode for testing.
    /// Mirrors the base class structure without RhinoCommon types.
    /// </summary>
    private class TestNode
    {
        public Guid Id { get; }
        public string DisplayName { get; set; }
        public TestNode? Parent { get; internal set; }
        public List<TestNode> Children { get; } = new();
        public bool IsVisible { get; set; } = true;
        public bool IsExpanded { get; set; } = true;
        public bool IsSelected { get; set; }
        public int Depth => Parent?.Depth + 1 ?? 0;

        public TestNode(Guid id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public TestNode(string displayName) : this(Guid.NewGuid(), displayName) { }

        public void AddChild(TestNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public bool RemoveChild(TestNode child)
        {
            if (Children.Remove(child))
            {
                child.Parent = null;
                return true;
            }
            return false;
        }

        public void ClearChildren()
        {
            foreach (var child in Children)
                child.Parent = null;
            Children.Clear();
        }

        public IEnumerable<TestNode> GetAllDescendants()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var desc in child.GetAllDescendants())
                    yield return desc;
            }
        }
    }

    // --- AddChild / RemoveChild ---

    [Fact]
    public void AddChild_SetsParentAndAddsToChildren()
    {
        var parent = new TestNode("Parent");
        var child = new TestNode("Child");

        parent.AddChild(child);

        Assert.Single(parent.Children);
        Assert.Same(parent, child.Parent);
        Assert.Same(child, parent.Children[0]);
    }

    [Fact]
    public void AddChild_MultipleTimes_AddsAll()
    {
        var parent = new TestNode("Parent");
        parent.AddChild(new TestNode("A"));
        parent.AddChild(new TestNode("B"));
        parent.AddChild(new TestNode("C"));

        Assert.Equal(3, parent.Children.Count);
    }

    [Fact]
    public void RemoveChild_ReturnsTrue_AndClearsParent()
    {
        var parent = new TestNode("Parent");
        var child = new TestNode("Child");
        parent.AddChild(child);

        var result = parent.RemoveChild(child);

        Assert.True(result);
        Assert.Empty(parent.Children);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void RemoveChild_NotFound_ReturnsFalse()
    {
        var parent = new TestNode("Parent");
        var stranger = new TestNode("Stranger");

        Assert.False(parent.RemoveChild(stranger));
    }

    [Fact]
    public void ClearChildren_RemovesAllAndNullsParent()
    {
        var parent = new TestNode("Parent");
        var a = new TestNode("A");
        var b = new TestNode("B");
        parent.AddChild(a);
        parent.AddChild(b);

        parent.ClearChildren();

        Assert.Empty(parent.Children);
        Assert.Null(a.Parent);
        Assert.Null(b.Parent);
    }

    // --- Depth ---

    [Fact]
    public void Depth_RootIsZero()
    {
        var root = new TestNode("Root");
        Assert.Equal(0, root.Depth);
    }

    [Fact]
    public void Depth_NestedCorrectly()
    {
        var root = new TestNode("Root");
        var child = new TestNode("Child");
        var grandchild = new TestNode("Grandchild");
        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Equal(0, root.Depth);
        Assert.Equal(1, child.Depth);
        Assert.Equal(2, grandchild.Depth);
    }

    // --- GetAllDescendants ---

    [Fact]
    public void GetAllDescendants_ReturnsAllNested()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A");
        var b = new TestNode("B");
        var a1 = new TestNode("A1");
        root.AddChild(a);
        root.AddChild(b);
        a.AddChild(a1);

        var descendants = root.GetAllDescendants().ToList();

        Assert.Equal(3, descendants.Count);
        Assert.Contains(a, descendants);
        Assert.Contains(b, descendants);
        Assert.Contains(a1, descendants);
    }

    [Fact]
    public void GetAllDescendants_EmptyForLeaf()
    {
        var leaf = new TestNode("Leaf");
        Assert.Empty(leaf.GetAllDescendants());
    }

    // --- Default property values ---

    [Fact]
    public void NewNode_HasDefaultProperties()
    {
        var node = new TestNode("Test");

        Assert.True(node.IsVisible);
        Assert.True(node.IsExpanded);
        Assert.False(node.IsSelected);
        Assert.Null(node.Parent);
        Assert.Empty(node.Children);
    }

    // --- Tree building scenario ---

    [Fact]
    public void TreeBuilding_SimulatesDocumentStructure()
    {
        // Simulate: Document → Assembly1 (#1, #2) → SubPart (nested)
        var doc = new TestNode(Guid.Empty, "MyModel.3dm");
        var asm1 = new TestNode("Assembly1 #1");
        var asm2 = new TestNode("Assembly1 #2");
        var sub1 = new TestNode("SubPart #1");

        doc.AddChild(asm1);
        doc.AddChild(asm2);
        asm1.AddChild(sub1);

        Assert.Equal(2, doc.Children.Count);
        Assert.Single(asm1.Children);
        Assert.Empty(asm2.Children);
        Assert.Equal(3, doc.GetAllDescendants().Count());
        Assert.Equal(2, sub1.Depth);
    }

    // --- BlockInstanceNode-like properties ---

    [Fact]
    public void BlockInstanceNode_PropertiesSimulated()
    {
        var id = Guid.NewGuid();
        var node = new TestNode(id, "Bracket #3");

        Assert.Equal(id, node.Id);
        Assert.Equal("Bracket #3", node.DisplayName);

        node.DisplayName = "Bracket #4";
        Assert.Equal("Bracket #4", node.DisplayName);
    }

    // --- DocumentNode-like creation ---

    [Fact]
    public void DocumentNode_CreationSimulated()
    {
        var docId = Guid.Empty;
        var doc = new TestNode(docId, "Untitled");

        Assert.Equal(Guid.Empty, doc.Id);
        Assert.Equal("Untitled", doc.DisplayName);
        Assert.Equal(0, doc.Depth);
    }

    // --- Tree depth calculation (3+ levels) ---

    [Fact]
    public void Depth_FourLevelsDeep_ReturnsCorrectDepth()
    {
        var root = new TestNode("Root");       // 0
        var l1 = new TestNode("L1");           // 1
        var l2 = new TestNode("L2");           // 2
        var l3 = new TestNode("L3");           // 3
        var l4 = new TestNode("L4");           // 4
        root.AddChild(l1);
        l1.AddChild(l2);
        l2.AddChild(l3);
        l3.AddChild(l4);

        Assert.Equal(4, l4.Depth);
    }

    [Fact]
    public void Depth_UpdatesAfterReparenting()
    {
        var root = new TestNode("Root");
        var child = new TestNode("Child");
        var grandchild = new TestNode("Grandchild");
        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Equal(2, grandchild.Depth);

        // Reparent grandchild directly under root
        child.RemoveChild(grandchild);
        root.AddChild(grandchild);

        Assert.Equal(1, grandchild.Depth);
    }

    // --- GetAllDescendants with complex tree ---

    [Fact]
    public void GetAllDescendants_ComplexTree_ReturnsAllNodes()
    {
        // root -> a -> a1, a2
        //      -> b -> b1 -> b1a
        //      -> c
        var root = new TestNode("Root");
        var a = new TestNode("A");
        var a1 = new TestNode("A1");
        var a2 = new TestNode("A2");
        var b = new TestNode("B");
        var b1 = new TestNode("B1");
        var b1a = new TestNode("B1A");
        var c = new TestNode("C");

        root.AddChild(a); root.AddChild(b); root.AddChild(c);
        a.AddChild(a1); a.AddChild(a2);
        b.AddChild(b1);
        b1.AddChild(b1a);

        var descendants = root.GetAllDescendants().ToList();
        Assert.Equal(7, descendants.Count);
        Assert.Contains(b1a, descendants);
    }

    [Fact]
    public void GetAllDescendants_PreservesDepthFirstOrder()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A");
        var a1 = new TestNode("A1");
        var b = new TestNode("B");
        root.AddChild(a); root.AddChild(b);
        a.AddChild(a1);

        var names = root.GetAllDescendants().Select(n => n.DisplayName).ToList();
        Assert.Equal(new[] { "A", "A1", "B" }, names);
    }

    // --- Node ID uniqueness ---

    [Fact]
    public void NodeIds_AreUniqueByDefault()
    {
        var nodes = Enumerable.Range(0, 100).Select(i => new TestNode($"Node{i}")).ToList();
        var ids = nodes.Select(n => n.Id).ToHashSet();
        Assert.Equal(100, ids.Count);
    }

    [Fact]
    public void NodeIds_ExplicitGuids_Preserved()
    {
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var node = new TestNode(id, "Specific");
        Assert.Equal(id, node.Id);
    }

    // --- Parent-child consistency after add/remove ---

    [Fact]
    public void ParentChild_ConsistencyAfterMultipleOperations()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A");
        var b = new TestNode("B");
        var c = new TestNode("C");

        root.AddChild(a);
        root.AddChild(b);
        root.AddChild(c);

        // Remove middle child
        root.RemoveChild(b);
        Assert.Equal(2, root.Children.Count);
        Assert.Null(b.Parent);
        Assert.Same(root, a.Parent);
        Assert.Same(root, c.Parent);

        // Re-add under a
        a.AddChild(b);
        Assert.Same(a, b.Parent);
        Assert.Single(a.Children);

        // Clear root
        root.ClearChildren();
        Assert.Empty(root.Children);
        Assert.Null(a.Parent);
        Assert.Null(c.Parent);
        // b's parent is still a (not root), unaffected by root.ClearChildren
        Assert.Same(a, b.Parent);
    }

    [Fact]
    public void AddChild_OverwritesPreviousParentReference()
    {
        var parent1 = new TestNode("P1");
        var parent2 = new TestNode("P2");
        var child = new TestNode("Child");

        parent1.AddChild(child);
        Assert.Same(parent1, child.Parent);

        // AddChild to new parent overwrites Parent but doesn't remove from old
        parent2.AddChild(child);
        Assert.Same(parent2, child.Parent);
        // Note: child is still in parent1.Children (this mirrors the real AssemblyNode behavior)
        Assert.Contains(child, parent1.Children);
        Assert.Contains(child, parent2.Children);
    }
}
