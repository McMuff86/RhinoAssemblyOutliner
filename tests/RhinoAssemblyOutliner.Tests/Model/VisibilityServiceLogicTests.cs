using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for visibility service logic: hide/show/isolate/showAll patterns.
/// Uses a pure-logic test double that mirrors VisibilityService behavior
/// without RhinoCommon dependencies.
/// </summary>
public class VisibilityServiceLogicTests
{
    private class TestNode
    {
        public Guid Id { get; }
        public string DisplayName { get; set; }
        public TestNode? Parent { get; internal set; }
        public List<TestNode> Children { get; } = new();
        public bool IsVisible { get; set; } = true;
        public int ComponentIndex { get; set; } = -1;

        public TestNode(string name) : this(Guid.NewGuid(), name) { }
        public TestNode(Guid id, string name) { Id = id; DisplayName = name; }

        public void AddChild(TestNode child) { child.Parent = this; Children.Add(child); }
        public IEnumerable<TestNode> GetAllDescendants()
        {
            foreach (var c in Children) { yield return c; foreach (var d in c.GetAllDescendants()) yield return d; }
        }
    }

    /// <summary>
    /// Test double for VisibilityService that works on TestNodes.
    /// </summary>
    private class TestVisibilityService
    {
        private readonly TestNode _root;

        public TestVisibilityService(TestNode root) { _root = root; }

        public void Hide(TestNode node, bool includeChildren = true)
        {
            node.IsVisible = false;
            if (includeChildren)
                foreach (var c in node.GetAllDescendants()) c.IsVisible = false;
        }

        public void Show(TestNode node, bool includeChildren = true)
        {
            node.IsVisible = true;
            if (includeChildren)
                foreach (var c in node.GetAllDescendants()) c.IsVisible = true;
        }

        public bool Toggle(TestNode node)
        {
            node.IsVisible = !node.IsVisible;
            return node.IsVisible;
        }

        public void Isolate(TestNode node)
        {
            // Hide everything
            _root.IsVisible = false;
            foreach (var d in _root.GetAllDescendants()) d.IsVisible = false;

            // Show the node and ancestors
            node.IsVisible = true;
            var current = node.Parent;
            while (current != null)
            {
                current.IsVisible = true;
                current = current.Parent;
            }

            // Show descendants
            foreach (var d in node.GetAllDescendants()) d.IsVisible = true;
        }

        public void ShowAll()
        {
            _root.IsVisible = true;
            foreach (var d in _root.GetAllDescendants()) d.IsVisible = true;
        }

        public bool IsMixedState(TestNode node)
        {
            if (!node.Children.Any()) return false;
            bool anyVisible = node.Children.Any(c => c.IsVisible);
            bool anyHidden = node.Children.Any(c => !c.IsVisible);
            return anyVisible && anyHidden;
        }
    }

    private TestNode BuildTree()
    {
        var root = new TestNode(Guid.Empty, "Document");
        var asm1 = new TestNode("Asm1"); var asm2 = new TestNode("Asm2");
        var p1 = new TestNode("Part1"); var p2 = new TestNode("Part2"); var p3 = new TestNode("Part3");
        var sub = new TestNode("SubPart");
        root.AddChild(asm1); root.AddChild(asm2);
        asm1.AddChild(p1); asm1.AddChild(p2);
        asm2.AddChild(p3);
        p2.AddChild(sub);
        return root;
    }

    // --- Hide ---

    [Fact]
    public void Hide_NodeAndChildren_AllHidden()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];

        svc.Hide(asm1);

        Assert.False(asm1.IsVisible);
        Assert.True(asm1.GetAllDescendants().All(n => !n.IsVisible));
    }

    [Fact]
    public void Hide_NodeOnly_ChildrenStillVisible()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];

        svc.Hide(asm1, includeChildren: false);

        Assert.False(asm1.IsVisible);
        Assert.True(asm1.Children.All(c => c.IsVisible));
    }

    [Fact]
    public void Hide_LeafNode()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var p1 = root.Children[0].Children[0];

        svc.Hide(p1);

        Assert.False(p1.IsVisible);
        Assert.True(root.IsVisible);
        Assert.True(root.Children[0].IsVisible);
    }

    // --- Show ---

    [Fact]
    public void Show_AfterHide_RestoresVisibility()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];

        svc.Hide(asm1);
        svc.Show(asm1);

        Assert.True(asm1.IsVisible);
        Assert.True(asm1.GetAllDescendants().All(n => n.IsVisible));
    }

    [Fact]
    public void Show_NodeOnly_ChildrenStayHidden()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];

        svc.Hide(asm1);
        svc.Show(asm1, includeChildren: false);

        Assert.True(asm1.IsVisible);
        Assert.True(asm1.Children.All(c => !c.IsVisible));
    }

    // --- Toggle ---

    [Fact]
    public void Toggle_FlipsState()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var p1 = root.Children[0].Children[0];

        Assert.True(p1.IsVisible);
        Assert.False(svc.Toggle(p1));
        Assert.True(svc.Toggle(p1));
    }

    [Fact]
    public void Toggle_TwiceRestoresOriginal()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var node = root.Children[1];

        svc.Toggle(node);
        svc.Toggle(node);

        Assert.True(node.IsVisible);
    }

    // --- Isolate ---

    [Fact]
    public void Isolate_OnlyTargetAndAncestorsVisible()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var p1 = root.Children[0].Children[0]; // Asm1 -> Part1

        svc.Isolate(p1);

        Assert.True(root.IsVisible);
        Assert.True(root.Children[0].IsVisible); // Asm1 (ancestor)
        Assert.True(p1.IsVisible);
        Assert.False(root.Children[1].IsVisible); // Asm2 hidden
        Assert.False(root.Children[0].Children[1].IsVisible); // Part2 hidden (sibling)
    }

    [Fact]
    public void Isolate_TargetDescendantsVisible()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];

        svc.Isolate(asm1);

        Assert.True(asm1.IsVisible);
        Assert.True(asm1.GetAllDescendants().All(n => n.IsVisible));
        Assert.False(root.Children[1].IsVisible); // Asm2 hidden
    }

    [Fact]
    public void Isolate_ThenShowAll_RestoresEverything()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        svc.Isolate(root.Children[0].Children[0]);
        svc.ShowAll();

        var all = new[] { root }.Concat(root.GetAllDescendants());
        Assert.True(all.All(n => n.IsVisible));
    }

    // --- ShowAll ---

    [Fact]
    public void ShowAll_AfterMultipleHides()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        svc.Hide(root.Children[0]);
        svc.Hide(root.Children[1]);

        svc.ShowAll();

        var all = new[] { root }.Concat(root.GetAllDescendants());
        Assert.True(all.All(n => n.IsVisible));
    }

    [Fact]
    public void ShowAll_OnAlreadyAllVisible_NoChange()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        svc.ShowAll(); // no-op

        var all = new[] { root }.Concat(root.GetAllDescendants());
        Assert.True(all.All(n => n.IsVisible));
    }

    // --- Mixed State ---

    [Fact]
    public void MixedState_SomeChildrenHidden()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];

        asm1.Children[0].IsVisible = false; // Part1 hidden
        // Part2 still visible

        Assert.True(svc.IsMixedState(asm1));
    }

    [Fact]
    public void MixedState_AllVisible_NotMixed()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        Assert.False(svc.IsMixedState(root.Children[0]));
    }

    [Fact]
    public void MixedState_AllHidden_NotMixed()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var asm1 = root.Children[0];
        foreach (var c in asm1.Children) c.IsVisible = false;
        Assert.False(svc.IsMixedState(asm1));
    }

    [Fact]
    public void MixedState_LeafNode_NeverMixed()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);
        var leaf = root.Children[0].Children[0];
        Assert.False(svc.IsMixedState(leaf));
    }

    // --- Dependent visibility (hide parent hides children) ---

    [Fact]
    public void HideParent_ChildrenEffectivelyHidden()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        svc.Hide(root.Children[0]); // hide Asm1

        // Check all descendants are hidden
        Assert.True(root.Children[0].GetAllDescendants().All(n => !n.IsVisible));
    }

    [Fact]
    public void ShowParent_RestoringChildren()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        svc.Hide(root.Children[0]);
        svc.Show(root.Children[0]);

        Assert.True(root.Children[0].GetAllDescendants().All(n => n.IsVisible));
    }

    // --- Multiple isolate calls ---

    [Fact]
    public void Isolate_CalledTwice_SecondOverridesFirst()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        svc.Isolate(root.Children[0]); // isolate Asm1
        svc.Isolate(root.Children[1]); // isolate Asm2

        Assert.False(root.Children[0].IsVisible); // Asm1 now hidden
        Assert.True(root.Children[1].IsVisible);   // Asm2 visible
    }

    // --- Bulk operations ---

    [Fact]
    public void HideMultipleNodes()
    {
        var root = BuildTree();
        var svc = new TestVisibilityService(root);

        var toHide = new[] { root.Children[0].Children[0], root.Children[1].Children[0] };
        foreach (var n in toHide) svc.Hide(n);

        Assert.False(toHide[0].IsVisible);
        Assert.False(toHide[1].IsVisible);
        Assert.True(root.Children[0].IsVisible); // parents still visible
        Assert.True(root.Children[1].IsVisible);
    }
}
