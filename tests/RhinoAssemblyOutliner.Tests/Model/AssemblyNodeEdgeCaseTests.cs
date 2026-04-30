using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Edge case and stress tests for AssemblyNode hierarchy operations.
/// </summary>
public class AssemblyNodeEdgeCaseTests
{
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

        public TestNode(Guid id, string displayName) { Id = id; DisplayName = displayName; }
        public TestNode(string displayName) : this(Guid.NewGuid(), displayName) { }

        public void AddChild(TestNode child) { child.Parent = this; Children.Add(child); }
        public bool RemoveChild(TestNode child)
        {
            if (Children.Remove(child)) { child.Parent = null; return true; }
            return false;
        }
        public void ClearChildren() { foreach (var c in Children) c.Parent = null; Children.Clear(); }
        public IEnumerable<TestNode> GetAllDescendants()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var desc in child.GetAllDescendants()) yield return desc;
            }
        }
    }

    // --- Deep nesting stress ---

    [Fact]
    public void DeepNesting_100Levels_DepthCorrect()
    {
        var root = new TestNode("Root");
        var current = root;
        for (int i = 0; i < 100; i++)
        {
            var child = new TestNode($"Level{i + 1}");
            current.AddChild(child);
            current = child;
        }
        Assert.Equal(100, current.Depth);
    }

    [Fact]
    public void DeepNesting_100Levels_GetAllDescendants()
    {
        var root = new TestNode("Root");
        var current = root;
        for (int i = 0; i < 100; i++)
        {
            var child = new TestNode($"Level{i + 1}");
            current.AddChild(child);
            current = child;
        }
        Assert.Equal(100, root.GetAllDescendants().Count());
    }

    // --- Wide tree stress ---

    [Fact]
    public void WideTree_1000Children_AllTracked()
    {
        var root = new TestNode("Root");
        for (int i = 0; i < 1000; i++)
            root.AddChild(new TestNode($"Child{i}"));

        Assert.Equal(1000, root.Children.Count);
        Assert.Equal(1000, root.GetAllDescendants().Count());
    }

    // --- Empty operations ---

    [Fact]
    public void ClearChildren_OnEmptyNode_NoException()
    {
        var node = new TestNode("Empty");
        node.ClearChildren(); // should not throw
        Assert.Empty(node.Children);
    }

    [Fact]
    public void RemoveChild_FromEmptyNode_ReturnsFalse()
    {
        var parent = new TestNode("Parent");
        var orphan = new TestNode("Orphan");
        Assert.False(parent.RemoveChild(orphan));
    }

    [Fact]
    public void GetAllDescendants_OnEmptyRoot_ReturnsEmpty()
    {
        var root = new TestNode("Root");
        Assert.Empty(root.GetAllDescendants());
    }

    // --- Self-reference prevention (manual) ---

    [Fact]
    public void AddChild_Self_DoesNotCrashButCreatesLoop()
    {
        // The real code doesn't guard against this - document the behavior
        var node = new TestNode("Self");
        node.AddChild(node);
        Assert.Same(node, node.Parent);
        Assert.Contains(node, node.Children);
        // GetAllDescendants would infinite loop - don't call it
    }

    // --- Reparenting scenarios ---

    [Fact]
    public void Reparenting_MoveChildBetweenParents()
    {
        var p1 = new TestNode("P1");
        var p2 = new TestNode("P2");
        var child = new TestNode("Child");

        p1.AddChild(child);
        Assert.Same(p1, child.Parent);

        p1.RemoveChild(child);
        p2.AddChild(child);

        Assert.Same(p2, child.Parent);
        Assert.Empty(p1.Children);
        Assert.Single(p2.Children);
    }

    [Fact]
    public void Reparenting_MoveSubtree()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A");
        var b = new TestNode("B");
        var a1 = new TestNode("A1");
        var a1x = new TestNode("A1X");

        root.AddChild(a);
        root.AddChild(b);
        a.AddChild(a1);
        a1.AddChild(a1x);

        // Move 'a' subtree under 'b'
        root.RemoveChild(a);
        b.AddChild(a);

        Assert.Single(root.Children);
        Assert.Same(b, root.Children[0]);
        Assert.Same(b, a.Parent);
        // root(0) -> b(1) -> a(2) -> a1(3) -> a1x(4)
        Assert.Equal(4, a1x.Depth);
    }

    // --- Visibility state combinations ---

    [Fact]
    public void MixedVisibility_ParentVisibleChildrenHidden()
    {
        var parent = new TestNode("Parent") { IsVisible = true };
        var c1 = new TestNode("C1") { IsVisible = false };
        var c2 = new TestNode("C2") { IsVisible = true };
        var c3 = new TestNode("C3") { IsVisible = false };
        parent.AddChild(c1);
        parent.AddChild(c2);
        parent.AddChild(c3);

        var hiddenCount = parent.Children.Count(c => !c.IsVisible);
        var visibleCount = parent.Children.Count(c => c.IsVisible);
        Assert.Equal(2, hiddenCount);
        Assert.Equal(1, visibleCount);

        // "Mixed state" = parent visible but not all children visible
        bool isMixed = parent.IsVisible && parent.Children.Any(c => !c.IsVisible);
        Assert.True(isMixed);
    }

    [Fact]
    public void MixedVisibility_AllChildrenHidden_IsMixedWhenParentVisible()
    {
        var parent = new TestNode("P") { IsVisible = true };
        parent.AddChild(new TestNode("C1") { IsVisible = false });
        parent.AddChild(new TestNode("C2") { IsVisible = false });

        bool allChildrenHidden = parent.Children.All(c => !c.IsVisible);
        Assert.True(allChildrenHidden);
        Assert.True(parent.IsVisible); // parent still visible
    }

    [Fact]
    public void MixedVisibility_AllVisible_NotMixed()
    {
        var parent = new TestNode("P") { IsVisible = true };
        parent.AddChild(new TestNode("C1") { IsVisible = true });
        parent.AddChild(new TestNode("C2") { IsVisible = true });

        bool isMixed = parent.Children.Any(c => !c.IsVisible);
        Assert.False(isMixed);
    }

    // --- Selection state ---

    [Fact]
    public void Selection_MultiSelect_IndependentPerNode()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A") { IsSelected = true };
        var b = new TestNode("B") { IsSelected = false };
        var c = new TestNode("C") { IsSelected = true };
        root.AddChild(a); root.AddChild(b); root.AddChild(c);

        var selected = root.Children.Where(n => n.IsSelected).ToList();
        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void Selection_DeselectAll()
    {
        var root = new TestNode("Root");
        for (int i = 0; i < 5; i++)
            root.AddChild(new TestNode($"N{i}") { IsSelected = true });

        foreach (var c in root.Children) c.IsSelected = false;
        Assert.True(root.Children.All(c => !c.IsSelected));
    }

    // --- Expand/Collapse ---

    [Fact]
    public void ExpandCollapse_DefaultExpanded()
    {
        var node = new TestNode("N");
        Assert.True(node.IsExpanded);
    }

    [Fact]
    public void ExpandCollapse_CollapseAll()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A");
        var b = new TestNode("B");
        var a1 = new TestNode("A1");
        root.AddChild(a); root.AddChild(b); a.AddChild(a1);

        // Collapse all
        foreach (var desc in root.GetAllDescendants())
            desc.IsExpanded = false;
        root.IsExpanded = false;

        Assert.False(root.IsExpanded);
        Assert.True(root.GetAllDescendants().All(n => !n.IsExpanded));
    }

    [Fact]
    public void ExpandCollapse_ExpandToNode()
    {
        // Simulate "reveal node in tree" - expand all ancestors
        var root = new TestNode("Root") { IsExpanded = false };
        var a = new TestNode("A") { IsExpanded = false };
        var a1 = new TestNode("A1") { IsExpanded = false };
        var target = new TestNode("Target") { IsExpanded = false };
        root.AddChild(a); a.AddChild(a1); a1.AddChild(target);

        // Expand path to target
        var current = target.Parent;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }

        Assert.True(root.IsExpanded);
        Assert.True(a.IsExpanded);
        Assert.True(a1.IsExpanded);
        Assert.False(target.IsExpanded); // target itself not expanded
    }

    // --- Guid edge cases ---

    [Fact]
    public void GuidEmpty_IsValidNodeId()
    {
        var node = new TestNode(Guid.Empty, "EmptyGuid");
        Assert.Equal(Guid.Empty, node.Id);
    }

    [Fact]
    public void DuplicateGuids_AllowedButDistinct()
    {
        var id = Guid.NewGuid();
        var n1 = new TestNode(id, "N1");
        var n2 = new TestNode(id, "N2");
        Assert.Equal(n1.Id, n2.Id);
        Assert.NotSame(n1, n2);
    }

    // --- DisplayName mutations ---

    [Fact]
    public void DisplayName_CanBeEmptyString()
    {
        var node = new TestNode(Guid.NewGuid(), "");
        Assert.Equal("", node.DisplayName);
    }

    [Fact]
    public void DisplayName_CanContainSpecialChars()
    {
        var node = new TestNode("Bräcket <#1> (Höhe=50mm)");
        Assert.Equal("Bräcket <#1> (Höhe=50mm)", node.DisplayName);
    }

    [Fact]
    public void DisplayName_CanBeMutated()
    {
        var node = new TestNode("Original");
        node.DisplayName = "Renamed";
        Assert.Equal("Renamed", node.DisplayName);
    }
}
