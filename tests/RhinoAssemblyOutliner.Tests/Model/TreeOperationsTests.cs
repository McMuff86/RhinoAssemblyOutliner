using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for tree operations: find, filter, expand/collapse, flatten.
/// Mirrors operations from AssemblyTreeBuilder and UI interactions.
/// </summary>
public class TreeOperationsTests
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
        public int ComponentIndex { get; set; } = -1;

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

    // --- Helper: Build test tree ---
    // doc -> Assembly1 -> PartA, PartB -> SubPart1
    //     -> Assembly2 -> PartC
    //     -> LoosePart

    private static TestNode BuildSampleTree()
    {
        var doc = new TestNode(Guid.Empty, "Model.3dm");
        var asm1 = new TestNode("Assembly1 #1");
        var partA = new TestNode("PartA #1");
        var partB = new TestNode("PartB #1");
        var sub1 = new TestNode("SubPart1 #1");
        var asm2 = new TestNode("Assembly2 #1");
        var partC = new TestNode("PartC #1");
        var loose = new TestNode("LoosePart");

        doc.AddChild(asm1); doc.AddChild(asm2); doc.AddChild(loose);
        asm1.AddChild(partA); asm1.AddChild(partB);
        partB.AddChild(sub1);
        asm2.AddChild(partC);

        return doc;
    }

    // --- Find by ID ---

    private static TestNode? FindById(TestNode root, Guid id)
    {
        if (root.Id == id) return root;
        foreach (var child in root.Children)
        {
            var found = FindById(child, id);
            if (found != null) return found;
        }
        return null;
    }

    [Fact]
    public void FindById_RootNode_Found()
    {
        var root = BuildSampleTree();
        Assert.Same(root, FindById(root, root.Id));
    }

    [Fact]
    public void FindById_DeepNode_Found()
    {
        var root = BuildSampleTree();
        var sub1 = root.GetAllDescendants().First(n => n.DisplayName == "SubPart1 #1");
        Assert.Same(sub1, FindById(root, sub1.Id));
    }

    [Fact]
    public void FindById_NonExistent_ReturnsNull()
    {
        var root = BuildSampleTree();
        Assert.Null(FindById(root, Guid.NewGuid()));
    }

    // --- Find by name (filter/search) ---

    private static IEnumerable<TestNode> FindByName(TestNode root, string query, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (root.DisplayName.Contains(query, comparison))
            yield return root;
        foreach (var child in root.Children)
            foreach (var match in FindByName(child, query, caseSensitive))
                yield return match;
    }

    [Fact]
    public void FindByName_ExactMatch()
    {
        var root = BuildSampleTree();
        var results = FindByName(root, "PartA #1").ToList();
        Assert.Single(results);
        Assert.Equal("PartA #1", results[0].DisplayName);
    }

    [Fact]
    public void FindByName_PartialMatch()
    {
        var root = BuildSampleTree();
        var results = FindByName(root, "Part").ToList();
        // PartA, PartB, SubPart1, PartC, LoosePart
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void FindByName_CaseInsensitive()
    {
        var root = BuildSampleTree();
        var results = FindByName(root, "assembly").ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FindByName_CaseSensitive_NoMatch()
    {
        var root = BuildSampleTree();
        var results = FindByName(root, "assembly", caseSensitive: true).ToList();
        Assert.Empty(results);
    }

    [Fact]
    public void FindByName_EmptyQuery_MatchesAll()
    {
        var root = BuildSampleTree();
        var results = FindByName(root, "").ToList();
        Assert.Equal(8, results.Count); // root + 7 descendants
    }

    [Fact]
    public void FindByName_NoMatch()
    {
        var root = BuildSampleTree();
        Assert.Empty(FindByName(root, "NonExistent"));
    }

    // --- Flatten tree (visible nodes for rendering) ---

    private static List<TestNode> FlattenVisible(TestNode root)
    {
        var result = new List<TestNode>();
        FlattenVisibleRecursive(root, result);
        return result;
    }

    private static void FlattenVisibleRecursive(TestNode node, List<TestNode> result)
    {
        result.Add(node);
        if (!node.IsExpanded) return;
        foreach (var child in node.Children)
            FlattenVisibleRecursive(child, result);
    }

    [Fact]
    public void FlattenVisible_AllExpanded_ReturnsAll()
    {
        var root = BuildSampleTree();
        var flat = FlattenVisible(root);
        Assert.Equal(8, flat.Count);
    }

    [Fact]
    public void FlattenVisible_CollapseAssembly1_HidesChildren()
    {
        var root = BuildSampleTree();
        var asm1 = root.Children[0];
        asm1.IsExpanded = false;

        var flat = FlattenVisible(root);
        // root + asm1(collapsed) + asm2 + partC + loose = 5
        Assert.Equal(5, flat.Count);
        Assert.DoesNotContain(flat, n => n.DisplayName == "PartA #1");
        Assert.DoesNotContain(flat, n => n.DisplayName == "PartB #1");
        Assert.DoesNotContain(flat, n => n.DisplayName == "SubPart1 #1");
    }

    [Fact]
    public void FlattenVisible_CollapseRoot_OnlyRoot()
    {
        var root = BuildSampleTree();
        root.IsExpanded = false;
        var flat = FlattenVisible(root);
        Assert.Single(flat);
        Assert.Same(root, flat[0]);
    }

    [Fact]
    public void FlattenVisible_PreservesOrder()
    {
        var root = BuildSampleTree();
        var flat = FlattenVisible(root);
        var names = flat.Select(n => n.DisplayName).ToList();
        Assert.Equal("Model.3dm", names[0]);
        Assert.Equal("Assembly1 #1", names[1]);
        Assert.Equal("PartA #1", names[2]);
    }

    // --- Collapse/Expand All ---

    [Fact]
    public void CollapseAll_ThenExpandAll()
    {
        var root = BuildSampleTree();

        // Collapse all
        void CollapseAll(TestNode n) { n.IsExpanded = false; foreach (var c in n.Children) CollapseAll(c); }
        CollapseAll(root);
        Assert.Single(FlattenVisible(root));

        // Expand all
        void ExpandAll(TestNode n) { n.IsExpanded = true; foreach (var c in n.Children) ExpandAll(c); }
        ExpandAll(root);
        Assert.Equal(8, FlattenVisible(root).Count);
    }

    // --- Drag & Drop Reorder ---

    private static bool MoveUp(TestNode node)
    {
        if (node.Parent == null) return false;
        var siblings = node.Parent.Children;
        int idx = siblings.IndexOf(node);
        if (idx <= 0) return false;
        siblings.RemoveAt(idx);
        siblings.Insert(idx - 1, node);
        return true;
    }

    private static bool MoveDown(TestNode node)
    {
        if (node.Parent == null) return false;
        var siblings = node.Parent.Children;
        int idx = siblings.IndexOf(node);
        if (idx < 0 || idx >= siblings.Count - 1) return false;
        siblings.RemoveAt(idx);
        siblings.Insert(idx + 1, node);
        return true;
    }

    [Fact]
    public void MoveUp_FirstChild_ReturnsFalse()
    {
        var root = BuildSampleTree();
        Assert.False(MoveUp(root.Children[0]));
    }

    [Fact]
    public void MoveUp_SecondChild_Succeeds()
    {
        var root = BuildSampleTree();
        var asm2 = root.Children[1];
        Assert.True(MoveUp(asm2));
        Assert.Same(asm2, root.Children[0]);
    }

    [Fact]
    public void MoveDown_LastChild_ReturnsFalse()
    {
        var root = BuildSampleTree();
        var last = root.Children[^1];
        Assert.False(MoveDown(last));
    }

    [Fact]
    public void MoveDown_FirstChild_Succeeds()
    {
        var root = BuildSampleTree();
        var first = root.Children[0];
        Assert.True(MoveDown(first));
        Assert.Same(first, root.Children[1]);
    }

    [Fact]
    public void MoveUp_RootNode_ReturnsFalse()
    {
        var root = BuildSampleTree();
        Assert.False(MoveUp(root));
    }

    [Fact]
    public void Reorder_MultipleMovesPreserveTree()
    {
        var root = BuildSampleTree();
        // Original: asm1, asm2, loose
        var asm1 = root.Children[0];
        var asm2 = root.Children[1];
        var loose = root.Children[2];

        MoveDown(asm1); // asm2, asm1, loose
        MoveDown(asm1); // asm2, loose, asm1
        Assert.Same(asm2, root.Children[0]);
        Assert.Same(loose, root.Children[1]);
        Assert.Same(asm1, root.Children[2]);

        // Children of asm1 should be intact
        Assert.Equal(2, asm1.Children.Count);
    }

    // --- Drag & Drop: Reparent ---

    private static void Reparent(TestNode node, TestNode newParent, int insertIndex = -1)
    {
        node.Parent?.Children.Remove(node);
        node.Parent = newParent;
        if (insertIndex >= 0 && insertIndex < newParent.Children.Count)
            newParent.Children.Insert(insertIndex, node);
        else
            newParent.Children.Add(node);
    }

    [Fact]
    public void Reparent_MoveNodeToAnotherParent()
    {
        var root = BuildSampleTree();
        var asm1 = root.Children[0];
        var asm2 = root.Children[1];
        var partA = asm1.Children[0];

        Reparent(partA, asm2);

        Assert.Single(asm1.Children); // only PartB remains
        Assert.Equal(2, asm2.Children.Count); // PartC + PartA
        Assert.Same(asm2, partA.Parent);
    }

    [Fact]
    public void Reparent_InsertAtIndex()
    {
        var root = BuildSampleTree();
        var asm1 = root.Children[0];
        var loose = root.Children[2];

        Reparent(loose, asm1, 0);

        Assert.Same(loose, asm1.Children[0]);
        Assert.Equal(3, asm1.Children.Count);
        Assert.Equal(2, root.Children.Count);
    }

    // --- Filter visible nodes ---

    [Fact]
    public void FilterByVisibility_HiddenNodesExcluded()
    {
        var root = BuildSampleTree();
        root.Children[0].IsVisible = false; // hide Assembly1

        var visibleTopLevel = root.Children.Where(c => c.IsVisible).ToList();
        Assert.Equal(2, visibleTopLevel.Count);
    }

    [Fact]
    public void FilterByVisibility_RecursivelyHideSubtree()
    {
        var root = BuildSampleTree();
        void HideSubtree(TestNode n) { n.IsVisible = false; foreach (var c in n.Children) HideSubtree(c); }

        HideSubtree(root.Children[0]);
        var allVisible = root.GetAllDescendants().Where(n => n.IsVisible).ToList();
        // asm2(visible), partC(visible), loose(visible) = 3
        Assert.Equal(3, allVisible.Count);
    }

    // --- Path from node to root ---

    [Fact]
    public void GetPathToRoot_ReturnsCorrectAncestors()
    {
        var root = BuildSampleTree();
        var sub1 = root.GetAllDescendants().First(n => n.DisplayName == "SubPart1 #1");

        var path = new List<string>();
        var current = sub1;
        while (current != null)
        {
            path.Add(current.DisplayName);
            current = current.Parent;
        }
        path.Reverse();

        Assert.Equal(new[] { "Model.3dm", "Assembly1 #1", "PartB #1", "SubPart1 #1" }, path);
    }

    // --- Count operations ---

    [Fact]
    public void CountByDepth()
    {
        var root = BuildSampleTree();
        var allNodes = new[] { root }.Concat(root.GetAllDescendants()).ToList();
        var byDepth = allNodes.GroupBy(n => n.Depth).OrderBy(g => g.Key).ToList();

        Assert.Equal(0, byDepth[0].Key); Assert.Single(byDepth[0]);      // root
        Assert.Equal(1, byDepth[1].Key); Assert.Equal(3, byDepth[1].Count()); // asm1, asm2, loose
        Assert.Equal(2, byDepth[2].Key); Assert.Equal(3, byDepth[2].Count()); // partA, partB, partC
        Assert.Equal(3, byDepth[3].Key); Assert.Single(byDepth[3]);      // sub1
    }

    // --- Sibling operations ---

    [Fact]
    public void GetSiblings_ReturnsOtherChildrenOfParent()
    {
        var root = BuildSampleTree();
        var asm1 = root.Children[0];

        var siblings = root.Children.Where(c => c != asm1).ToList();
        Assert.Equal(2, siblings.Count);
    }

    [Fact]
    public void GetSiblings_RootHasNoSiblings()
    {
        var root = BuildSampleTree();
        Assert.Null(root.Parent);
    }

    // --- IsLeaf check ---

    [Fact]
    public void IsLeaf_TrueForChildlessNode()
    {
        var root = BuildSampleTree();
        var loose = root.Children[2];
        Assert.Empty(loose.Children);
    }

    [Fact]
    public void IsLeaf_FalseForParent()
    {
        var root = BuildSampleTree();
        Assert.NotEmpty(root.Children);
    }
}
