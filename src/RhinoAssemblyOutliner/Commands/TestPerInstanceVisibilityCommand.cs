using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoAssemblyOutliner.Services.PerInstanceVisibility;

namespace RhinoAssemblyOutliner.Commands;

/// <summary>
/// Test command for per-instance component visibility.
/// Usage: TestPerInstanceVisibility
/// </summary>
public class TestPerInstanceVisibilityCommand : Command
{
    private static PerInstanceVisibilityService _service;

    public override string EnglishName => "TestPerInstanceVisibility";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Initialize service if needed
        _service ??= new PerInstanceVisibilityService(doc);

        // Select a block instance
        var go = new GetObject();
        go.SetCommandPrompt("Select a block instance");
        go.GeometryFilter = ObjectType.InstanceReference;
        go.Get();

        if (go.CommandResult() != Result.Success)
            return go.CommandResult();

        var instanceObj = go.Object(0).Object() as InstanceObject;
        if (instanceObj == null)
        {
            RhinoApp.WriteLine("No valid block instance selected.");
            return Result.Failure;
        }

        // Get component infos
        var components = _service.GetComponentInfos(instanceObj.Id);
        if (components.Count == 0)
        {
            RhinoApp.WriteLine("Block has no components.");
            return Result.Failure;
        }

        // Show component list
        RhinoApp.WriteLine($"\nBlock '{instanceObj.InstanceDefinition.Name}' has {components.Count} components:");
        foreach (var comp in components)
        {
            var visIcon = comp.IsVisible ? "👁" : "🚫";
            RhinoApp.WriteLine($"  [{comp.Index}] {visIcon} {comp.Name} ({comp.ObjectType}) - Layer: {comp.LayerName}");
        }

        // Ask what to do
        var gi = new GetInteger();
        gi.SetCommandPrompt("\nEnter component index to toggle (-1 to show all, -2 to hide all)");
        gi.SetLowerLimit(-2, false);
        gi.SetUpperLimit(components.Count - 1, false);
        gi.Get();

        if (gi.CommandResult() != Result.Success)
            return gi.CommandResult();

        int choice = gi.Number();

        if (choice == -1)
        {
            // Show all
            _service.ShowAllComponents(instanceObj.Id);
            RhinoApp.WriteLine("All components shown.");
        }
        else if (choice == -2)
        {
            // Hide all
            _service.HideAllComponents(instanceObj.Id);
            RhinoApp.WriteLine("All components hidden.");
        }
        else
        {
            // Toggle specific component
            bool newState = _service.ToggleComponent(instanceObj.Id, choice);
            RhinoApp.WriteLine($"Component [{choice}] is now {(newState ? "visible" : "hidden")}.");
        }

        doc.Views.Redraw();
        return Result.Success;
    }
}
