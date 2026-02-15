using Xunit;

namespace RhinoAssemblyOutliner.Tests.Integration;

/// <summary>
/// Tests for dot-separated component path building and parsing.
/// These paths are used by the native DLL API to address nested components
/// within block instances (e.g., "1.0.2" = child 1 → child 0 → child 2).
/// </summary>
public class ComponentPathTests
{
    /// <summary>
    /// Builds a dot-separated path by appending an index to a parent path.
    /// </summary>
    private static string BuildPath(string parentPath, int index)
    {
        return string.IsNullOrEmpty(parentPath)
            ? index.ToString()
            : $"{parentPath}.{index}";
    }

    /// <summary>
    /// Parses a dot-separated path into an array of integer indices.
    /// </summary>
    private static int[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Array.Empty<int>();
        return path.Split('.').Select(int.Parse).ToArray();
    }

    /// <summary>
    /// Checks if candidatePath is a descendant of (starts with) ancestorPath.
    /// </summary>
    private static bool IsDescendantOf(string candidatePath, string ancestorPath)
    {
        if (string.IsNullOrEmpty(ancestorPath))
            return !string.IsNullOrEmpty(candidatePath);
        return candidatePath.StartsWith(ancestorPath + ".")
            && candidatePath.Length > ancestorPath.Length;
    }

    // --- BuildPath ---

    [Fact]
    public void BuildPath_EmptyParent_ReturnsIndex()
    {
        Assert.Equal("0", BuildPath("", 0));
    }

    [Fact]
    public void BuildPath_SingleLevel_AppendsIndex()
    {
        Assert.Equal("1.0", BuildPath("1", 0));
    }

    [Fact]
    public void BuildPath_TwoLevels_AppendsIndex()
    {
        Assert.Equal("1.0.2", BuildPath("1.0", 2));
    }

    [Fact]
    public void BuildPath_DeepNesting()
    {
        var path = BuildPath(BuildPath(BuildPath(BuildPath("", 0), 1), 2), 3);
        Assert.Equal("0.1.2.3", path);
    }

    // --- ParsePath ---

    [Fact]
    public void ParsePath_SingleIndex()
    {
        Assert.Equal(new[] { 0 }, ParsePath("0"));
    }

    [Fact]
    public void ParsePath_ThreeLevels()
    {
        Assert.Equal(new[] { 1, 0, 2 }, ParsePath("1.0.2"));
    }

    [Fact]
    public void ParsePath_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(ParsePath(""));
    }

    [Fact]
    public void ParsePath_DeepPath()
    {
        var result = ParsePath("0.1.2.3.4.5.6.7");
        Assert.Equal(8, result.Length);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, result);
    }

    // --- Roundtrip ---

    [Fact]
    public void BuildAndParse_Roundtrip()
    {
        var path = BuildPath(BuildPath("", 3), 7);
        var parsed = ParsePath(path);
        Assert.Equal(new[] { 3, 7 }, parsed);
    }

    // --- Path comparison and prefix matching ---

    [Fact]
    public void IsDescendantOf_DirectChild()
    {
        Assert.True(IsDescendantOf("1.0", "1"));
    }

    [Fact]
    public void IsDescendantOf_DeeplyNested()
    {
        Assert.True(IsDescendantOf("1.0.2.3", "1.0"));
    }

    [Fact]
    public void IsDescendantOf_SamePath_ReturnsFalse()
    {
        Assert.False(IsDescendantOf("1.0", "1.0"));
    }

    [Fact]
    public void IsDescendantOf_Sibling_ReturnsFalse()
    {
        Assert.False(IsDescendantOf("2.0", "1"));
    }

    [Fact]
    public void IsDescendantOf_EmptyAncestor_MatchesAll()
    {
        Assert.True(IsDescendantOf("0", ""));
        Assert.True(IsDescendantOf("1.2.3", ""));
    }

    [Fact]
    public void IsDescendantOf_EmptyCandidate_ReturnsFalse()
    {
        Assert.False(IsDescendantOf("", "1"));
        Assert.False(IsDescendantOf("", ""));
    }

    [Fact]
    public void IsDescendantOf_PartialNumericMatch_ReturnsFalse()
    {
        // "10" should NOT be a descendant of "1" (different component)
        Assert.False(IsDescendantOf("10", "1"));
    }

    [Fact]
    public void PathEquality_SamePathsAreEqual()
    {
        Assert.Equal("1.0.2", "1.0.2");
    }

    [Fact]
    public void PathComparison_DifferentPathsNotEqual()
    {
        Assert.NotEqual("1.0.2", "1.0.3");
    }
}
