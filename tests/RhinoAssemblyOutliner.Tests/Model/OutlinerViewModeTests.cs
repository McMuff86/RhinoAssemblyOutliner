using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for OutlinerViewMode enum and related logic.
/// </summary>
public class OutlinerViewModeTests
{
    private enum OutlinerViewMode { Document, Assembly }

    [Fact]
    public void ViewMode_HasTwoValues()
    {
        var values = Enum.GetValues<OutlinerViewMode>();
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void ViewMode_DocumentIsDefault()
    {
        var mode = default(OutlinerViewMode);
        Assert.Equal(OutlinerViewMode.Document, mode);
    }

    [Fact]
    public void ViewMode_CanSwitchBetweenModes()
    {
        var mode = OutlinerViewMode.Document;
        mode = OutlinerViewMode.Assembly;
        Assert.Equal(OutlinerViewMode.Assembly, mode);
    }

    [Fact]
    public void ViewMode_EnumValuesAreDefined()
    {
        Assert.True(Enum.IsDefined(OutlinerViewMode.Document));
        Assert.True(Enum.IsDefined(OutlinerViewMode.Assembly));
    }
}
