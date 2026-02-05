namespace RhinoAssemblyOutliner.Model;

/// <summary>
/// Defines the view mode for the Assembly Outliner.
/// </summary>
public enum OutlinerViewMode
{
    /// <summary>
    /// Shows all blocks in the document.
    /// </summary>
    Document,
    
    /// <summary>
    /// Shows only a selected assembly root and its children.
    /// </summary>
    Assembly
}
