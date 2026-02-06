using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoAssemblyOutliner.Services.PerInstanceVisibility;

namespace RhinoAssemblyOutliner.Commands;

/// <summary>
/// Test command for the C++ native per-instance component visibility.
/// Usage: TestNativeVisibility
/// Supports path-based component addressing for deep nesting.
/// </summary>
public class TestNativeVisibilityCommand : Command
{
    private static bool _nativeInitialized;

    public override string EnglishName => "TestNativeVisibility";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Check native DLL availability
        if (!NativeVisibilityInterop.IsNativeDllAvailable())
        {
            RhinoApp.WriteLine("ERROR: RhinoAssemblyOutliner.Native.dll not found next to plugin.");
            RhinoApp.WriteLine("Copy it from: src\\RhinoAssemblyOutliner.native\\x64\\Release\\");
            return Result.Failure;
        }

        // Initialize native module if needed
        if (!_nativeInitialized)
        {
            if (!NativeVisibilityInterop.NativeInit())
            {
                RhinoApp.WriteLine("ERROR: NativeInit() failed.");
                return Result.Failure;
            }
            int version = NativeVisibilityInterop.GetNativeVersion();
            RhinoApp.WriteLine($"Native visibility module initialized (version {version}).");
            _nativeInitialized = true;
        }

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

        var instanceId = instanceObj.Id;
        var instanceDef = instanceObj.InstanceDefinition;
        var defObjects = instanceDef.GetObjects();

        // Show component list with current visibility state (recursive)
        RhinoApp.WriteLine($"\nBlock '{instanceDef.Name}' has {defObjects.Length} components:");
        RhinoApp.WriteLine($"  (Hidden count from native: {NativeVisibilityInterop.GetHiddenComponentCount(ref instanceId)})");

        PrintComponents(doc, defObjects, "", "  ");

        // Ask what to do
        var gs = new GetString();
        gs.SetCommandPrompt("\nEnter component path to toggle (e.g. '0', '1.0'), 'debug' to toggle logging, or 'reset' to reset all");
        gs.Get();

        if (gs.CommandResult() != Result.Success)
            return gs.CommandResult();

        string input = gs.StringResult().Trim();

        if (string.Equals(input, "reset", StringComparison.OrdinalIgnoreCase))
        {
            NativeVisibilityInterop.ResetComponentVisibility(ref instanceId);
            RhinoApp.WriteLine("All components reset to visible.");
        }
        else if (string.Equals(input, "debug", StringComparison.OrdinalIgnoreCase))
        {
            // Toggle debug logging
            NativeVisibilityInterop.SetDebugLogging(true);
            RhinoApp.WriteLine("Debug logging ENABLED. Run command again and toggle a component to see output.");
            RhinoApp.WriteLine("Re-run with 'debug' to see the toggle effect (logging stays on until cleanup).");
        }
        else
        {
            // Treat input as a component path
            bool currentlyVisible = NativeVisibilityInterop.IsComponentVisible(ref instanceId, input);
            bool newVisible = !currentlyVisible;
            bool success = NativeVisibilityInterop.SetComponentVisibility(ref instanceId, input, newVisible);

            if (success)
                RhinoApp.WriteLine($"Component path \"{input}\" is now {(newVisible ? "visible" : "hidden")}.");
            else
                RhinoApp.WriteLine($"Failed to set visibility for path \"{input}\".");
        }

        return Result.Success;
    }

    /// <summary>
    /// Recursively prints components with their paths and visibility state.
    /// </summary>
    private static void PrintComponents(RhinoDoc doc, RhinoObject[] objects, string parentPath, string indent)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            string path = string.IsNullOrEmpty(parentPath) ? i.ToString() : $"{parentPath}.{i}";
            var name = string.IsNullOrEmpty(obj.Name) ? $"Component" : obj.Name;

            // For the test command, we check against the selected instance (not available here)
            // So just show the path and type
            string layerName = "?";
            if (obj.Attributes.LayerIndex >= 0 && obj.Attributes.LayerIndex < doc.Layers.Count)
                layerName = doc.Layers[obj.Attributes.LayerIndex]?.Name ?? "?";

            RhinoApp.WriteLine($"{indent}[{path}] {name} ({obj.ObjectType}) - Layer: {layerName}");

            // Recurse into nested blocks
            if (obj is InstanceObject nestedInstance)
            {
                var nestedDef = nestedInstance.InstanceDefinition;
                if (nestedDef != null)
                {
                    var nestedObjects = nestedDef.GetObjects();
                    if (nestedObjects != null && nestedObjects.Length > 0)
                    {
                        PrintComponents(doc, nestedObjects, path, indent + "  ");
                    }
                }
            }
        }
    }
}
