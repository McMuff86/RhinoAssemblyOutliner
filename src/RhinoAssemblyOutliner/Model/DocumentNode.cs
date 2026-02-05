using Rhino;

namespace RhinoAssemblyOutliner.Model;

/// <summary>
/// Represents the root document node in the assembly tree.
/// Contains all top-level objects including block instances and loose geometry.
/// </summary>
public class DocumentNode : AssemblyNode
{
    /// <summary>
    /// The document ID this node represents.
    /// </summary>
    public uint DocumentSerialNumber { get; }

    /// <summary>
    /// The file path of the document.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Indicates whether the document has unsaved changes.
    /// </summary>
    public bool IsModified { get; private set; }

    /// <summary>
    /// Total number of block instances in the document.
    /// </summary>
    public int TotalBlockInstanceCount { get; set; }

    /// <summary>
    /// Total number of block definitions in the document.
    /// </summary>
    public int TotalBlockDefinitionCount { get; set; }

    /// <summary>
    /// Total number of top-level objects (including loose geometry).
    /// </summary>
    public int TopLevelObjectCount { get; set; }

    /// <summary>
    /// Creates a new document node from a Rhino document.
    /// </summary>
    /// <param name="doc">The Rhino document.</param>
    public DocumentNode(RhinoDoc doc) 
        : base(GetDocumentDisplayName(doc))
    {
        DocumentSerialNumber = doc.RuntimeSerialNumber;
        FilePath = string.IsNullOrEmpty(doc.Path) ? null : doc.Path;
        IsModified = doc.Modified;
    }

    /// <summary>
    /// Gets the display name for the document.
    /// </summary>
    private static string GetDocumentDisplayName(RhinoDoc doc)
    {
        if (string.IsNullOrEmpty(doc.Name))
        {
            return "Untitled";
        }
        
        // Show filename without path
        return Path.GetFileName(doc.Path) ?? doc.Name;
    }

    /// <summary>
    /// Updates the document state (e.g., modified flag).
    /// </summary>
    /// <param name="doc">The Rhino document.</param>
    public void UpdateState(RhinoDoc doc)
    {
        IsModified = doc.Modified;
        DisplayName = GetDocumentDisplayName(doc) + (IsModified ? " *" : "");
    }

    /// <summary>
    /// Gets the icon key for document nodes.
    /// </summary>
    public override string GetIconKey()
    {
        return "document";
    }

    /// <summary>
    /// Gets a summary of the document.
    /// </summary>
    public override string GetSummary()
    {
        var summary = $"Document: {DisplayName}\n";
        
        if (!string.IsNullOrEmpty(FilePath))
        {
            summary += $"Path: {FilePath}\n";
        }
        
        summary += $"\nStatistics:\n";
        summary += $"  Block Definitions: {TotalBlockDefinitionCount}\n";
        summary += $"  Block Instances: {TotalBlockInstanceCount}\n";
        summary += $"  Top-Level Objects: {TopLevelObjectCount}\n";
        summary += $"\nStatus: {(IsModified ? "Modified" : "Saved")}";
        
        return summary;
    }
}
