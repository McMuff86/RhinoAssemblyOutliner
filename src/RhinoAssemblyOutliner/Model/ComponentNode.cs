using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;

namespace RhinoAssemblyOutliner.Model;

/// <summary>
/// Represents an individual geometry object (component) inside a block definition.
/// These are non-block objects like Breps, Meshes, Curves, etc.
/// Each component gets an eye-icon for per-instance visibility toggling via VariantManager.
/// </summary>
public class ComponentNode : AssemblyNode
{
    /// <summary>
    /// The Rhino object ID of the component geometry within the definition.
    /// </summary>
    public Guid ObjectId { get; }

    /// <summary>
    /// Index of this object within the parent block definition's GetObjects() array.
    /// Used by VisibilityState to track which components are hidden.
    /// </summary>
    public int ComponentIndex { get; }

    /// <summary>
    /// The type of geometry (Brep, Mesh, Curve, etc.).
    /// </summary>
    public ObjectType GeometryType { get; }

    /// <summary>
    /// The definition ID of the parent block that contains this component.
    /// Needed for VariantManager operations.
    /// </summary>
    public Guid ParentDefinitionId { get; }

    /// <summary>
    /// The instance ID of the top-level block instance that this component belongs to.
    /// Needed for ReassignInstance calls.
    /// </summary>
    public Guid OwnerInstanceId { get; set; }

    /// <summary>
    /// Creates a new component node.
    /// </summary>
    /// <param name="obj">The Rhino geometry object from the definition.</param>
    /// <param name="componentIndex">Index within definition.GetObjects().</param>
    /// <param name="parentDefinitionId">ID of the containing block definition.</param>
    /// <param name="doc">The Rhino document (for layer lookup).</param>
    public ComponentNode(RhinoObject obj, int componentIndex, Guid parentDefinitionId, RhinoDoc doc)
        : base(CreateComponentId(parentDefinitionId, componentIndex), FormatName(obj, componentIndex))
    {
        ObjectId = obj.Id;
        ComponentIndex = componentIndex;
        GeometryType = obj.ObjectType;
        ParentDefinitionId = parentDefinitionId;

        // Get layer
        if (doc != null && obj.Attributes.LayerIndex >= 0
            && obj.Attributes.LayerIndex < doc.Layers.Count)
        {
            Layer = doc.Layers[obj.Attributes.LayerIndex];
        }
    }

    /// <summary>
    /// Creates a deterministic GUID for a component node based on definition + index.
    /// </summary>
    private static Guid CreateComponentId(Guid parentDefId, int index)
    {
        var bytes = parentDefId.ToByteArray();
        // Mix in the component index
        var indexBytes = BitConverter.GetBytes(index);
        bytes[12] = indexBytes[0];
        bytes[13] = indexBytes[1];
        bytes[14] = indexBytes[2];
        bytes[15] = 0xC0; // "COmponent" marker
        return new Guid(bytes);
    }

    /// <summary>
    /// Formats the display name for a component.
    /// </summary>
    private static string FormatName(RhinoObject obj, int index)
    {
        // Prefer the object name, fall back to type + index
        if (!string.IsNullOrEmpty(obj.Name))
            return obj.Name;

        string typeName = obj.ObjectType switch
        {
            ObjectType.Brep => "Brep",
            ObjectType.Extrusion => "Extrusion",
            ObjectType.Mesh => "Mesh",
            ObjectType.Curve => "Curve",
            ObjectType.Surface => "Surface",
            ObjectType.Point => "Point",
            ObjectType.SubD => "SubD",
            ObjectType.Light => "Light",
            ObjectType.Annotation => "Annotation",
            ObjectType.Hatch => "Hatch",
            _ => obj.ObjectType.ToString()
        };

        return $"{typeName} [{index}]";
    }

    /// <inheritdoc />
    public override string GetIconKey() => "component";

    /// <inheritdoc />
    public override string GetSummary()
    {
        var summary = $"Component: {DisplayName}\n";
        summary += $"Type: {GeometryType}\n";
        summary += $"Index: {ComponentIndex}\n";
        if (Layer != null)
            summary += $"Layer: {Layer.FullPath}\n";
        summary += $"Visible: {IsVisible}\n";
        return summary;
    }
}
