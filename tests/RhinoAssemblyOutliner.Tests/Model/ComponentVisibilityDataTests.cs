using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for ComponentVisibilityData logic (pure state management).
/// Mirrors the real ComponentVisibilityData class without RhinoCommon UserData.
/// </summary>
public class ComponentVisibilityDataTests
{
    /// <summary>
    /// Test double for ComponentVisibilityData without RhinoCommon dependency.
    /// </summary>
    private class TestComponentVisibilityData
    {
        public HashSet<int> HiddenComponents { get; private set; } = new();
        public bool HasHiddenComponents => HiddenComponents.Count > 0;

        public bool IsComponentVisible(int componentIndex) => !HiddenComponents.Contains(componentIndex);

        public void SetComponentVisibility(int componentIndex, bool visible)
        {
            if (visible) HiddenComponents.Remove(componentIndex);
            else HiddenComponents.Add(componentIndex);
        }

        public bool ToggleComponentVisibility(int componentIndex)
        {
            if (HiddenComponents.Contains(componentIndex))
            {
                HiddenComponents.Remove(componentIndex);
                return true; // now visible
            }
            HiddenComponents.Add(componentIndex);
            return false; // now hidden
        }

        public void ShowAllComponents() => HiddenComponents.Clear();

        public void HideAllComponents(int totalComponents)
        {
            HiddenComponents.Clear();
            for (int i = 0; i < totalComponents; i++) HiddenComponents.Add(i);
        }

        public TestComponentVisibilityData Duplicate()
        {
            return new TestComponentVisibilityData { HiddenComponents = new HashSet<int>(HiddenComponents) };
        }
    }

    // --- Default state ---

    [Fact]
    public void Default_NoHiddenComponents()
    {
        var data = new TestComponentVisibilityData();
        Assert.False(data.HasHiddenComponents);
        Assert.True(data.IsComponentVisible(0));
        Assert.True(data.IsComponentVisible(999));
    }

    // --- SetComponentVisibility ---

    [Fact]
    public void Hide_SingleComponent()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(2, false);

        Assert.True(data.HasHiddenComponents);
        Assert.False(data.IsComponentVisible(2));
        Assert.True(data.IsComponentVisible(0));
        Assert.True(data.IsComponentVisible(1));
    }

    [Fact]
    public void Hide_MultipleComponents()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(0, false);
        data.SetComponentVisibility(3, false);
        data.SetComponentVisibility(5, false);

        Assert.Equal(3, data.HiddenComponents.Count);
        Assert.False(data.IsComponentVisible(0));
        Assert.False(data.IsComponentVisible(3));
        Assert.False(data.IsComponentVisible(5));
        Assert.True(data.IsComponentVisible(1));
    }

    [Fact]
    public void Show_HiddenComponent_MakesVisible()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(1, false);
        data.SetComponentVisibility(1, true);

        Assert.True(data.IsComponentVisible(1));
        Assert.False(data.HasHiddenComponents);
    }

    [Fact]
    public void Show_AlreadyVisibleComponent_NoOp()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(5, true); // already visible
        Assert.False(data.HasHiddenComponents);
    }

    [Fact]
    public void Hide_SameComponentTwice_Idempotent()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(2, false);
        data.SetComponentVisibility(2, false);
        Assert.Single(data.HiddenComponents);
    }

    // --- Toggle ---

    [Fact]
    public void Toggle_VisibleToHidden()
    {
        var data = new TestComponentVisibilityData();
        bool result = data.ToggleComponentVisibility(0);
        Assert.False(result); // now hidden
        Assert.False(data.IsComponentVisible(0));
    }

    [Fact]
    public void Toggle_HiddenToVisible()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(0, false);
        bool result = data.ToggleComponentVisibility(0);
        Assert.True(result); // now visible
        Assert.True(data.IsComponentVisible(0));
    }

    [Fact]
    public void Toggle_ThreeTimes_EndsHidden()
    {
        var data = new TestComponentVisibilityData();
        data.ToggleComponentVisibility(0); // hidden
        data.ToggleComponentVisibility(0); // visible
        data.ToggleComponentVisibility(0); // hidden
        Assert.False(data.IsComponentVisible(0));
    }

    // --- ShowAll / HideAll ---

    [Fact]
    public void ShowAll_ClearsAllHidden()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(0, false);
        data.SetComponentVisibility(1, false);
        data.SetComponentVisibility(2, false);

        data.ShowAllComponents();

        Assert.False(data.HasHiddenComponents);
        Assert.True(data.IsComponentVisible(0));
    }

    [Fact]
    public void HideAll_HidesAllComponents()
    {
        var data = new TestComponentVisibilityData();
        data.HideAllComponents(5);

        Assert.Equal(5, data.HiddenComponents.Count);
        for (int i = 0; i < 5; i++)
            Assert.False(data.IsComponentVisible(i));
        Assert.True(data.IsComponentVisible(5)); // beyond range is visible
    }

    [Fact]
    public void HideAll_ZeroComponents_NoHidden()
    {
        var data = new TestComponentVisibilityData();
        data.HideAllComponents(0);
        Assert.False(data.HasHiddenComponents);
    }

    [Fact]
    public void HideAll_ThenShowAll_RestoresAll()
    {
        var data = new TestComponentVisibilityData();
        data.HideAllComponents(10);
        data.ShowAllComponents();
        Assert.False(data.HasHiddenComponents);
    }

    [Fact]
    public void HideAll_OverwritesPreviousState()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(99, false); // hide an out-of-range component
        data.HideAllComponents(3); // should clear and set 0,1,2

        Assert.Equal(3, data.HiddenComponents.Count);
        Assert.True(data.IsComponentVisible(99)); // 99 was cleared
    }

    // --- Duplicate ---

    [Fact]
    public void Duplicate_IndependentCopy()
    {
        var original = new TestComponentVisibilityData();
        original.SetComponentVisibility(0, false);
        original.SetComponentVisibility(2, false);

        var copy = original.Duplicate();

        Assert.Equal(2, copy.HiddenComponents.Count);
        Assert.False(copy.IsComponentVisible(0));

        // Modify copy, original unaffected
        copy.ShowAllComponents();
        Assert.Equal(2, original.HiddenComponents.Count);
    }

    // --- Edge cases ---

    [Fact]
    public void NegativeIndex_CanBeHidden()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(-1, false);
        Assert.False(data.IsComponentVisible(-1));
    }

    [Fact]
    public void LargeIndex_CanBeHidden()
    {
        var data = new TestComponentVisibilityData();
        data.SetComponentVisibility(int.MaxValue, false);
        Assert.False(data.IsComponentVisible(int.MaxValue));
    }

    [Fact]
    public void ManyComponents_Performance()
    {
        var data = new TestComponentVisibilityData();
        for (int i = 0; i < 10000; i++)
            data.SetComponentVisibility(i, false);

        Assert.Equal(10000, data.HiddenComponents.Count);

        data.ShowAllComponents();
        Assert.False(data.HasHiddenComponents);
    }
}
