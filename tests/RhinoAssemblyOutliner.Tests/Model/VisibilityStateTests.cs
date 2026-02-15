using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for visibility state logic, mirroring the C++ VisibilityData patterns
/// used by the native DLL. Tests pure path-based visibility management without
/// requiring the native DLL or RhinoCommon.
/// </summary>
public class VisibilityStateTests
{
    /// <summary>
    /// Pure-logic visibility state tracker that mirrors the native DLL's behavior.
    /// Manages hidden component paths per instance (identified by Guid).
    /// </summary>
    private class VisibilityState
    {
        // instanceId -> set of hidden component paths
        private readonly Dictionary<Guid, HashSet<string>> _hiddenPaths = new();

        public void Hide(Guid instanceId, string path)
        {
            if (!_hiddenPaths.TryGetValue(instanceId, out var set))
            {
                set = new HashSet<string>();
                _hiddenPaths[instanceId] = set;
            }
            set.Add(path);
        }

        public void Show(Guid instanceId, string path)
        {
            if (_hiddenPaths.TryGetValue(instanceId, out var set))
                set.Remove(path);
        }

        public bool IsVisible(Guid instanceId, string path)
        {
            return !_hiddenPaths.TryGetValue(instanceId, out var set) || !set.Contains(path);
        }

        public int GetHiddenCount(Guid instanceId)
        {
            return _hiddenPaths.TryGetValue(instanceId, out var set) ? set.Count : 0;
        }

        public void ResetInstance(Guid instanceId)
        {
            _hiddenPaths.Remove(instanceId);
        }

        public bool HasHiddenDescendants(Guid instanceId, string parentPath)
        {
            if (!_hiddenPaths.TryGetValue(instanceId, out var set))
                return false;
            var prefix = string.IsNullOrEmpty(parentPath) ? "" : parentPath + ".";
            return set.Any(p => p.StartsWith(prefix) && p != parentPath);
        }
    }

    private readonly VisibilityState _state = new();
    private readonly Guid _instance1 = Guid.NewGuid();
    private readonly Guid _instance2 = Guid.NewGuid();

    // --- Path-based component addressing ---

    [Fact]
    public void PathAddressing_TopLevelComponent()
    {
        _state.Hide(_instance1, "0");
        Assert.False(_state.IsVisible(_instance1, "0"));
        Assert.True(_state.IsVisible(_instance1, "1"));
    }

    [Fact]
    public void PathAddressing_NestedComponent()
    {
        _state.Hide(_instance1, "1.0");
        Assert.False(_state.IsVisible(_instance1, "1.0"));
        Assert.True(_state.IsVisible(_instance1, "1"));
        Assert.True(_state.IsVisible(_instance1, "0"));
    }

    [Fact]
    public void PathAddressing_DeeplyNested()
    {
        _state.Hide(_instance1, "1.0.2");
        Assert.False(_state.IsVisible(_instance1, "1.0.2"));
        Assert.True(_state.IsVisible(_instance1, "1.0"));
        Assert.True(_state.IsVisible(_instance1, "1"));
    }

    // --- HasHiddenDescendants (prefix matching) ---

    [Fact]
    public void HasHiddenDescendants_ReturnsTrueWhenChildHidden()
    {
        _state.Hide(_instance1, "1.0.2");
        Assert.True(_state.HasHiddenDescendants(_instance1, "1.0"));
        Assert.True(_state.HasHiddenDescendants(_instance1, "1"));
    }

    [Fact]
    public void HasHiddenDescendants_ReturnsFalseWhenNoChildrenHidden()
    {
        _state.Hide(_instance1, "2.0");
        Assert.False(_state.HasHiddenDescendants(_instance1, "1"));
    }

    [Fact]
    public void HasHiddenDescendants_EmptyParentPath_MatchesAll()
    {
        _state.Hide(_instance1, "0");
        Assert.True(_state.HasHiddenDescendants(_instance1, ""));
    }

    [Fact]
    public void HasHiddenDescendants_ExactMatch_NotCountedAsDescendant()
    {
        _state.Hide(_instance1, "1.0");
        // "1.0" is not a descendant of "1.0" (it IS "1.0")
        Assert.False(_state.HasHiddenDescendants(_instance1, "1.0"));
    }

    // --- ResetInstance ---

    [Fact]
    public void ResetInstance_ClearsAllPaths()
    {
        _state.Hide(_instance1, "0");
        _state.Hide(_instance1, "1.0");
        _state.Hide(_instance1, "2.3.1");

        _state.ResetInstance(_instance1);

        Assert.Equal(0, _state.GetHiddenCount(_instance1));
        Assert.True(_state.IsVisible(_instance1, "0"));
        Assert.True(_state.IsVisible(_instance1, "1.0"));
        Assert.True(_state.IsVisible(_instance1, "2.3.1"));
    }

    [Fact]
    public void ResetInstance_DoesNotAffectOtherInstances()
    {
        _state.Hide(_instance1, "0");
        _state.Hide(_instance2, "0");

        _state.ResetInstance(_instance1);

        Assert.True(_state.IsVisible(_instance1, "0"));
        Assert.False(_state.IsVisible(_instance2, "0"));
    }

    // --- Hide/Show/Hide roundtrip ---

    [Fact]
    public void HideShowHide_Roundtrip()
    {
        Assert.True(_state.IsVisible(_instance1, "0"));

        _state.Hide(_instance1, "0");
        Assert.False(_state.IsVisible(_instance1, "0"));

        _state.Show(_instance1, "0");
        Assert.True(_state.IsVisible(_instance1, "0"));

        _state.Hide(_instance1, "0");
        Assert.False(_state.IsVisible(_instance1, "0"));
    }

    [Fact]
    public void Show_AlreadyVisible_NoEffect()
    {
        _state.Show(_instance1, "0"); // no-op
        Assert.True(_state.IsVisible(_instance1, "0"));
        Assert.Equal(0, _state.GetHiddenCount(_instance1));
    }

    // --- Multiple instances independently managed ---

    [Fact]
    public void MultipleInstances_IndependentVisibility()
    {
        _state.Hide(_instance1, "0");
        _state.Hide(_instance2, "1");

        Assert.False(_state.IsVisible(_instance1, "0"));
        Assert.True(_state.IsVisible(_instance1, "1"));
        Assert.True(_state.IsVisible(_instance2, "0"));
        Assert.False(_state.IsVisible(_instance2, "1"));
    }

    [Fact]
    public void MultipleInstances_SamePathDifferentState()
    {
        _state.Hide(_instance1, "1.0.2");

        Assert.False(_state.IsVisible(_instance1, "1.0.2"));
        Assert.True(_state.IsVisible(_instance2, "1.0.2"));
    }

    // --- Empty path handling ---

    [Fact]
    public void EmptyPath_CanBeHidden()
    {
        _state.Hide(_instance1, "");
        Assert.False(_state.IsVisible(_instance1, ""));
        Assert.Equal(1, _state.GetHiddenCount(_instance1));
    }

    [Fact]
    public void EmptyPath_DoesNotAffectOtherPaths()
    {
        _state.Hide(_instance1, "");
        Assert.True(_state.IsVisible(_instance1, "0"));
    }

    // --- Very deep nesting paths ---

    [Fact]
    public void DeepNesting_EightLevels()
    {
        var deepPath = "0.1.2.3.4.5.6.7";
        _state.Hide(_instance1, deepPath);

        Assert.False(_state.IsVisible(_instance1, deepPath));
        Assert.True(_state.IsVisible(_instance1, "0.1.2.3.4.5.6"));
        Assert.True(_state.HasHiddenDescendants(_instance1, "0.1.2.3"));
    }

    [Fact]
    public void DeepNesting_MultiplePathsAtDifferentDepths()
    {
        _state.Hide(_instance1, "0");
        _state.Hide(_instance1, "1.0");
        _state.Hide(_instance1, "1.0.2.3.4.5.6.7");

        Assert.Equal(3, _state.GetHiddenCount(_instance1));
        Assert.True(_state.HasHiddenDescendants(_instance1, "1.0.2"));
    }

    // --- Hidden count ---

    [Fact]
    public void GetHiddenCount_TracksCorrectly()
    {
        Assert.Equal(0, _state.GetHiddenCount(_instance1));

        _state.Hide(_instance1, "0");
        Assert.Equal(1, _state.GetHiddenCount(_instance1));

        _state.Hide(_instance1, "1");
        Assert.Equal(2, _state.GetHiddenCount(_instance1));

        _state.Show(_instance1, "0");
        Assert.Equal(1, _state.GetHiddenCount(_instance1));
    }

    [Fact]
    public void Hide_SamePathTwice_CountsOnce()
    {
        _state.Hide(_instance1, "0");
        _state.Hide(_instance1, "0");
        Assert.Equal(1, _state.GetHiddenCount(_instance1));
    }
}
