using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoAssemblyOutliner.Commands;

/// <summary>
/// Diagnostic command. Pick a block instance in the viewport and dump
/// what the AssemblyTreeBuilder would see for it: definition name,
/// object count, type of each child, IsInstanceDefinitionObject flag.
/// Used to debug "0 Children" issues in the outliner.
/// </summary>
public class InspectBlockDefinitionCommand : Command
{
    public override string EnglishName => "AssemblyOutlinerInspectBlock";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var go = new GetObject();
        go.SetCommandPrompt("Pick a block instance to inspect");
        go.GeometryFilter = ObjectType.InstanceReference;
        go.SubObjectSelect = false;
        go.Get();
        if (go.CommandResult() != Result.Success) return go.CommandResult();

        var objref = go.Object(0);
        var instance = objref.Object() as InstanceObject;
        if (instance == null)
        {
            RhinoApp.WriteLine("Inspect: not an InstanceObject.");
            return Result.Failure;
        }

        var def = instance.InstanceDefinition;
        RhinoApp.WriteLine("--- Inspect ---");
        RhinoApp.WriteLine($"Instance Id:       {instance.Id}");
        RhinoApp.WriteLine($"Definition Name:   '{def.Name}'");
        RhinoApp.WriteLine($"Definition Index:  {def.Index}");
        RhinoApp.WriteLine($"Definition Id:     {def.Id}");
        RhinoApp.WriteLine($"UpdateType:        {def.UpdateType}");
        RhinoApp.WriteLine($"IsDeleted:         {def.IsDeleted}");
        RhinoApp.WriteLine($"UseCount():        {def.UseCount()}");

        var objs = def.GetObjects();
        int n = objs?.Length ?? 0;
        RhinoApp.WriteLine($"GetObjects().Length: {n}");

        if (objs != null)
        {
            for (int i = 0; i < objs.Length; i++)
            {
                var o = objs[i];
                if (o == null) { RhinoApp.WriteLine($"  [{i}] <null>"); continue; }
                bool isDef = o.Attributes.IsInstanceDefinitionObject;
                RhinoApp.WriteLine($"  [{i}] type={o.ObjectType} isDefObj={isDef} deleted={o.IsDeleted} name='{o.Name}' id={o.Id}");
            }
        }

        RhinoApp.WriteLine("--- /Inspect ---");
        return Result.Success;
    }
}
