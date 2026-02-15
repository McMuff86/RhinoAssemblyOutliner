using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for selection sync logic patterns used by SelectionSyncService.
/// Tests pure logic without RhinoCommon.
/// </summary>
public class SelectionSyncLogicTests
{
    private class TestNode
    {
        public Guid Id { get; }
        public string DisplayName { get; set; }
        public TestNode? Parent { get; internal set; }
        public List<TestNode> Children { get; } = new();
        public bool IsSelected { get; set; }

        public TestNode(string name) { Id = Guid.NewGuid(); DisplayName = name; }
        public void AddChild(TestNode c) { c.Parent = this; Children.Add(c); }
        public IEnumerable<TestNode> GetAllDescendants()
        {
            foreach (var c in Children) { yield return c; foreach (var d in c.GetAllDescendants()) yield return d; }
        }
    }

    // --- Sync to viewport: collect selected IDs ---

    [Fact]
    public void SyncToViewport_CollectsSelectedNodeIds()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A") { IsSelected = true };
        var b = new TestNode("B") { IsSelected = false };
        var c = new TestNode("C") { IsSelected = true };
        root.AddChild(a); root.AddChild(b); root.AddChild(c);

        var selectedIds = root.Children.Where(n => n.IsSelected).Select(n => n.Id).ToList();
        Assert.Equal(2, selectedIds.Count);
        Assert.Contains(a.Id, selectedIds);
        Assert.Contains(c.Id, selectedIds);
    }

    // --- Deselect all ---

    [Fact]
    public void DeselectAll_ClearsSelection()
    {
        var root = new TestNode("Root");
        var a = new TestNode("A") { IsSelected = true };
        var b = new TestNode("B") { IsSelected = true };
        root.AddChild(a); root.AddChild(b);

        foreach (var n in new[] { root }.Concat(root.GetAllDescendants()))
            n.IsSelected = false;

        Assert.True(root.GetAllDescendants().All(n => !n.IsSelected));
    }

    // --- Select all instances of same definition (simulated) ---

    [Fact]
    public void SelectByDefinition_SelectsAllMatchingNodes()
    {
        var root = new TestNode("Root");
        var bracket1 = new TestNode("Bracket #1");
        var bracket2 = new TestNode("Bracket #2");
        var bolt1 = new TestNode("Bolt #1");
        root.AddChild(bracket1); root.AddChild(bracket2); root.AddChild(bolt1);

        // Simulate: select all "Bracket" instances
        foreach (var n in root.Children)
            n.IsSelected = n.DisplayName.StartsWith("Bracket");

        Assert.True(bracket1.IsSelected);
        Assert.True(bracket2.IsSelected);
        Assert.False(bolt1.IsSelected);
    }

    // --- Loop prevention flag ---

    [Fact]
    public void IsSyncing_PreventsReentrantSync()
    {
        bool isSyncing = false;
        int syncCount = 0;

        void SyncToViewport()
        {
            if (isSyncing) return;
            isSyncing = true;
            try
            {
                syncCount++;
                // Simulate: viewport change triggers tree sync which triggers viewport sync
                SyncFromViewport();
            }
            finally { isSyncing = false; }
        }

        void SyncFromViewport()
        {
            if (isSyncing) return;
            isSyncing = true;
            try { syncCount++; }
            finally { isSyncing = false; }
        }

        SyncToViewport();
        Assert.Equal(1, syncCount); // only one sync, reentrant was blocked
    }
}
