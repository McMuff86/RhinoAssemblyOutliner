using Xunit;

namespace RhinoAssemblyOutliner.Tests.UI;

/// <summary>
/// Tests for keyboard shortcut mapping logic.
/// Verifies that key combinations map to the correct actions.
/// </summary>
public class KeyboardShortcutMappingTests
{
    [Flags]
    private enum Modifiers { None = 0, Ctrl = 1, Shift = 2, Alt = 4 }

    private enum OutlinerAction
    {
        Hide, Show, ShowAll, Isolate, ExitIsolate,
        ToggleVisibility, ZoomToSelection, BlockEdit,
        MoveUp, MoveDown, SelectAll, Delete, Rename
    }

    private record ShortcutBinding(string Key, Modifiers Modifiers, OutlinerAction Action);

    private static readonly ShortcutBinding[] Bindings = new[]
    {
        new ShortcutBinding("H", Modifiers.None, OutlinerAction.Hide),
        new ShortcutBinding("H", Modifiers.Shift, OutlinerAction.Show),
        new ShortcutBinding("H", Modifiers.Ctrl | Modifiers.Shift, OutlinerAction.ShowAll),
        new ShortcutBinding("I", Modifiers.None, OutlinerAction.Isolate),
        new ShortcutBinding("Escape", Modifiers.None, OutlinerAction.ExitIsolate),
        new ShortcutBinding("Space", Modifiers.None, OutlinerAction.ToggleVisibility),
        new ShortcutBinding("F", Modifiers.None, OutlinerAction.ZoomToSelection),
        new ShortcutBinding("Enter", Modifiers.None, OutlinerAction.BlockEdit),
        new ShortcutBinding("Up", Modifiers.Ctrl, OutlinerAction.MoveUp),
        new ShortcutBinding("Down", Modifiers.Ctrl, OutlinerAction.MoveDown),
        new ShortcutBinding("A", Modifiers.Ctrl, OutlinerAction.SelectAll),
        new ShortcutBinding("Delete", Modifiers.None, OutlinerAction.Delete),
        new ShortcutBinding("F2", Modifiers.None, OutlinerAction.Rename),
    };

    private static OutlinerAction? Resolve(string key, Modifiers mods)
    {
        return Bindings.FirstOrDefault(b => b.Key == key && b.Modifiers == mods)?.Action;
    }

    [Theory]
    [InlineData("H", Modifiers.None, OutlinerAction.Hide)]
    [InlineData("H", Modifiers.Shift, OutlinerAction.Show)]
    [InlineData("H", Modifiers.Ctrl | Modifiers.Shift, OutlinerAction.ShowAll)]
    [InlineData("I", Modifiers.None, OutlinerAction.Isolate)]
    [InlineData("Escape", Modifiers.None, OutlinerAction.ExitIsolate)]
    [InlineData("Space", Modifiers.None, OutlinerAction.ToggleVisibility)]
    [InlineData("F", Modifiers.None, OutlinerAction.ZoomToSelection)]
    [InlineData("Enter", Modifiers.None, OutlinerAction.BlockEdit)]
    [InlineData("Up", Modifiers.Ctrl, OutlinerAction.MoveUp)]
    [InlineData("Down", Modifiers.Ctrl, OutlinerAction.MoveDown)]
    [InlineData("A", Modifiers.Ctrl, OutlinerAction.SelectAll)]
    [InlineData("Delete", Modifiers.None, OutlinerAction.Delete)]
    [InlineData("F2", Modifiers.None, OutlinerAction.Rename)]
    public void Shortcut_MapsToCorrectAction(string key, Modifiers mods, OutlinerAction expected)
    {
        Assert.Equal(expected, Resolve(key, mods));
    }

    [Theory]
    [InlineData("X", Modifiers.None)]
    [InlineData("H", Modifiers.Alt)]
    [InlineData("H", Modifiers.Ctrl)]
    [InlineData("Z", Modifiers.Ctrl)]
    public void UnboundKey_ReturnsNull(string key, Modifiers mods)
    {
        Assert.Null(Resolve(key, mods));
    }

    [Fact]
    public void AllBindings_HaveUniqueKeyModCombination()
    {
        var combos = Bindings.Select(b => (b.Key, b.Modifiers)).ToList();
        Assert.Equal(combos.Count, combos.Distinct().Count());
    }

    [Fact]
    public void AllActions_HaveAtLeastOneBinding()
    {
        var boundActions = Bindings.Select(b => b.Action).ToHashSet();
        foreach (OutlinerAction action in Enum.GetValues<OutlinerAction>())
            Assert.Contains(action, boundActions);
    }

    [Fact]
    public void HKey_HasThreeBindings_WithDifferentModifiers()
    {
        var hBindings = Bindings.Where(b => b.Key == "H").ToList();
        Assert.Equal(3, hBindings.Count);
    }
}
