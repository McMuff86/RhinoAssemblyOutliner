using Xunit;

namespace RhinoAssemblyOutliner.Tests.Integration;

/// <summary>
/// Extended edge case tests for component path operations.
/// </summary>
public class ComponentPathEdgeCaseTests
{
    private static string BuildPath(string parent, int index) =>
        string.IsNullOrEmpty(parent) ? index.ToString() : $"{parent}.{index}";

    private static int[] ParsePath(string path) =>
        string.IsNullOrEmpty(path) ? Array.Empty<int>() : path.Split('.').Select(int.Parse).ToArray();

    private static bool IsDescendantOf(string candidate, string ancestor)
    {
        if (string.IsNullOrEmpty(ancestor))
            return !string.IsNullOrEmpty(candidate);
        return candidate.StartsWith(ancestor + ".") && candidate.Length > ancestor.Length;
    }

    private static string GetParentPath(string path)
    {
        var lastDot = path.LastIndexOf('.');
        return lastDot < 0 ? "" : path[..lastDot];
    }

    private static int GetDepthFromPath(string path) =>
        string.IsNullOrEmpty(path) ? 0 : path.Count(c => c == '.') + 1;

    // --- GetParentPath ---

    [Theory]
    [InlineData("0", "")]
    [InlineData("1.0", "1")]
    [InlineData("1.0.2", "1.0")]
    [InlineData("0.1.2.3.4", "0.1.2.3")]
    public void GetParentPath_ReturnsCorrectParent(string path, string expected)
    {
        Assert.Equal(expected, GetParentPath(path));
    }

    // --- GetDepthFromPath ---

    [Theory]
    [InlineData("", 0)]
    [InlineData("0", 1)]
    [InlineData("1.0", 2)]
    [InlineData("1.0.2", 3)]
    [InlineData("0.1.2.3.4.5.6.7", 8)]
    public void GetDepthFromPath_ReturnsCorrectDepth(string path, int expected)
    {
        Assert.Equal(expected, GetDepthFromPath(path));
    }

    // --- Large indices ---

    [Fact]
    public void BuildPath_LargeIndices()
    {
        var path = BuildPath("999", 1000);
        Assert.Equal("999.1000", path);
        var parsed = ParsePath(path);
        Assert.Equal(new[] { 999, 1000 }, parsed);
    }

    // --- Path building from component indices (simulates ResolveComponentPath) ---

    [Fact]
    public void ResolveComponentPath_SimulatedWalkUp()
    {
        // Simulate: SubPart(componentIndex=2) -> Assembly(componentIndex=0) -> TopLevel(componentIndex=-1)
        var indices = new List<int>();
        // Walk up collecting indices
        indices.Add(2); // SubPart
        indices.Add(0); // Assembly
        // Stop at top-level (componentIndex=-1)

        indices.Reverse();
        var path = string.Join(".", indices);
        Assert.Equal("0.2", path);
    }

    [Fact]
    public void ResolveComponentPath_SingleLevel()
    {
        var indices = new List<int> { 3 };
        indices.Reverse();
        Assert.Equal("3", string.Join(".", indices));
    }

    [Fact]
    public void ResolveComponentPath_FourLevels()
    {
        var indices = new List<int> { 7, 3, 1, 0 }; // bottom-up
        indices.Reverse();
        Assert.Equal("0.1.3.7", string.Join(".", indices));
    }

    // --- Sibling detection ---

    [Fact]
    public void SiblingsShareParentPath()
    {
        var sibling1 = "1.0";
        var sibling2 = "1.1";
        var sibling3 = "1.2";

        Assert.Equal(GetParentPath(sibling1), GetParentPath(sibling2));
        Assert.Equal(GetParentPath(sibling2), GetParentPath(sibling3));
    }

    [Fact]
    public void NonSiblings_DifferentParentPaths()
    {
        Assert.NotEqual(GetParentPath("1.0"), GetParentPath("2.0"));
    }

    // --- Ancestor chain ---

    [Fact]
    public void IsDescendantOf_FullAncestorChain()
    {
        var path = "0.1.2.3";
        Assert.True(IsDescendantOf(path, "0"));
        Assert.True(IsDescendantOf(path, "0.1"));
        Assert.True(IsDescendantOf(path, "0.1.2"));
        Assert.False(IsDescendantOf(path, "0.1.2.3"));
    }

    // --- Numeric prefix collision prevention ---

    [Theory]
    [InlineData("10.0", "1", false)]  // "10" != "1"
    [InlineData("1.0", "1", true)]
    [InlineData("100", "10", false)]
    [InlineData("10.5", "10", true)]
    public void IsDescendantOf_NumericPrefixSafety(string candidate, string ancestor, bool expected)
    {
        Assert.Equal(expected, IsDescendantOf(candidate, ancestor));
    }
}
