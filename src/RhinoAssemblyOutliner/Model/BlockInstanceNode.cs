using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoAssemblyOutliner.Model;

/// <summary>
/// Represents a block instance in the assembly tree.
/// This is the primary node type for displaying block hierarchies.
/// </summary>
public class BlockInstanceNode : AssemblyNode
{
    /// <summary>
    /// The Rhino object ID of the block instance.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// The index of the block definition this instance references.
    /// </summary>
    public int BlockDefinitionIndex { get; }

    /// <summary>
    /// Name of the block definition.
    /// </summary>
    public string DefinitionName { get; }

    /// <summary>
    /// Instance number within the same definition (for display like "BlockName #1").
    /// </summary>
    public int InstanceNumber { get; set; }

    /// <summary>
    /// Index of this object within its parent block definition's object array.
    /// -1 for top-level instances (not inside any block).
    /// Used for per-instance component visibility via native DLL.
    /// </summary>
    public int ComponentIndex { get; set; } = -1;

    /// <summary>
    /// Total count of instances of this definition in the document.
    /// </summary>
    public int TotalInstanceCount { get; set; }

    /// <summary>
    /// The transformation matrix of this instance.
    /// </summary>
    public Transform InstanceTransform { get; }

    /// <summary>
    /// Type of block link (Embedded, Linked, or EmbeddedAndLinked).
    /// </summary>
    public InstanceDefinitionUpdateType LinkType { get; }

    /// <summary>
    /// File path for linked blocks, if applicable.
    /// </summary>
    public string? LinkedFilePath { get; }

    /// <summary>
    /// User-defined key-value attributes (UserText).
    /// </summary>
    public Dictionary<string, string> UserAttributes { get; }

    /// <summary>
    /// Creates a new block instance node.
    /// </summary>
    /// <param name="instance">The Rhino InstanceObject.</param>
    /// <param name="definition">The block definition.</param>
    /// <param name="instanceNumber">Instance number for display.</param>
    public BlockInstanceNode(InstanceObject instance, InstanceDefinition definition, int instanceNumber)
        : base(FormatDisplayName(definition.Name, instanceNumber))
    {
        InstanceId = instance.Id;
        BlockDefinitionIndex = definition.Index;
        DefinitionName = definition.Name;
        InstanceNumber = instanceNumber;
        InstanceTransform = instance.InstanceXform;
        LinkType = definition.UpdateType;
        LinkedFilePath = IsLinkedBlock(definition) ? definition.SourceArchive : null;
        
        // Get user attributes (UserText)
        UserAttributes = new Dictionary<string, string>();
        var userStrings = instance.Attributes.GetUserStrings();
        if (userStrings != null)
        {
            foreach (string key in userStrings.AllKeys)
            {
                UserAttributes[key] = userStrings[key];
            }
        }
        
        // Get layer from the instance
        var doc = RhinoDoc.ActiveDoc;
        if (doc != null && instance.Attributes.LayerIndex >= 0 
            && instance.Attributes.LayerIndex < doc.Layers.Count)
        {
            Layer = doc.Layers[instance.Attributes.LayerIndex];
        }
    }

    /// <summary>
    /// Creates a node from a block definition (for browsing definitions without instance).
    /// </summary>
    /// <param name="definition">The block definition.</param>
    /// <param name="doc">The Rhino document.</param>
    public BlockInstanceNode(InstanceDefinition definition, RhinoDoc doc)
        : base(definition.Name)
    {
        InstanceId = Guid.Empty;
        BlockDefinitionIndex = definition.Index;
        DefinitionName = definition.Name;
        InstanceNumber = 0;
        InstanceTransform = Transform.Identity;
        LinkType = definition.UpdateType;
        LinkedFilePath = IsLinkedBlock(definition) ? definition.SourceArchive : null;
        TotalInstanceCount = definition.UseCount();
        UserAttributes = new Dictionary<string, string>();
    }

    /// <summary>
    /// Formats the display name with instance number.
    /// </summary>
    private static string FormatDisplayName(string definitionName, int instanceNumber)
    {
        return $"{definitionName} #{instanceNumber}";
    }

    /// <summary>
    /// Checks if a block definition is linked (external reference).
    /// </summary>
    private static bool IsLinkedBlock(InstanceDefinition definition)
    {
        return definition.UpdateType == InstanceDefinitionUpdateType.Linked ||
               definition.UpdateType == InstanceDefinitionUpdateType.LinkedAndEmbedded;
    }

    /// <summary>
    /// Gets the icon key based on link type.
    /// </summary>
    public override string GetIconKey()
    {
        return LinkType switch
        {
            InstanceDefinitionUpdateType.Linked => "block_linked",
            InstanceDefinitionUpdateType.LinkedAndEmbedded => "block_linked_embedded",
            _ => "block_embedded"
        };
    }

    /// <summary>
    /// Gets a summary of this block instance.
    /// </summary>
    public override string GetSummary()
    {
        var summary = $"Block: {DefinitionName}\n";
        summary += $"Instance: #{InstanceNumber} of {TotalInstanceCount}\n";
        summary += $"Type: {LinkType}\n";
        
        if (Layer != null)
        {
            summary += $"Layer: {Layer.FullPath}\n";
        }
        
        if (!string.IsNullOrEmpty(LinkedFilePath))
        {
            summary += $"Source: {LinkedFilePath}\n";
        }
        
        summary += $"Children: {Children.Count}\n";
        
        // User attributes
        if (UserAttributes.Count > 0)
        {
            summary += "\n--- User Attributes ---\n";
            foreach (var kvp in UserAttributes)
            {
                summary += $"{kvp.Key}: {kvp.Value}\n";
            }
        }
        
        return summary;
    }

    /// <summary>
    /// Selects this block instance in the Rhino viewport.
    /// </summary>
    /// <param name="doc">The active Rhino document.</param>
    /// <returns>True if selection was successful.</returns>
    public bool SelectInViewport(RhinoDoc doc)
    {
        if (InstanceId == Guid.Empty) return false;
        
        var obj = doc.Objects.FindId(InstanceId);
        if (obj == null) return false;
        
        obj.Select(true);
        doc.Views.Redraw();
        return true;
    }

    /// <summary>
    /// Zooms to this block instance in the viewport.
    /// </summary>
    /// <param name="doc">The active Rhino document.</param>
    /// <returns>True if zoom was successful.</returns>
    public bool ZoomToInstance(RhinoDoc doc)
    {
        if (InstanceId == Guid.Empty) return false;
        
        var obj = doc.Objects.FindId(InstanceId);
        if (obj == null) return false;
        
        var bbox = obj.Geometry.GetBoundingBox(true);
        if (!bbox.IsValid) return false;
        
        var view = doc.Views.ActiveView;
        if (view == null) return false;
        
        view.ActiveViewport.ZoomBoundingBox(bbox);
        doc.Views.Redraw();
        return true;
    }
}
