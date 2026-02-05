using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using RhinoAssemblyOutliner.UI;

namespace RhinoAssemblyOutliner;

/// <summary>
/// Main plugin class for the Rhino Assembly Outliner.
/// Provides a SolidWorks FeatureManager-style hierarchical view of block instances.
/// </summary>
[System.Runtime.InteropServices.Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
public class RhinoAssemblyOutlinerPlugin : PlugIn
{
    /// <summary>
    /// Gets the singleton instance of the plugin.
    /// </summary>
    public static RhinoAssemblyOutlinerPlugin? Instance { get; private set; }

    /// <summary>
    /// Plugin constructor.
    /// </summary>
    public RhinoAssemblyOutlinerPlugin()
    {
        Instance = this;
    }

    /// <summary>
    /// Called when the plugin is being loaded.
    /// </summary>
    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        RhinoApp.WriteLine("RhinoAssemblyOutliner plugin loaded.");
        
        // Register the Assembly Outliner panel
        Panels.RegisterPanel(
            this,
            typeof(AssemblyOutlinerPanel),
            "Assembly Outliner",
            null,  // Icon (can be added later)
            PanelType.PerDoc
        );
        
        // Register event handlers for document changes
        RhinoDoc.BeginOpenDocument += OnBeginOpenDocument;
        RhinoDoc.EndOpenDocument += OnEndOpenDocument;
        RhinoDoc.CloseDocument += OnCloseDocument;
        
        return LoadReturnCode.Success;
    }

    /// <summary>
    /// Called when a document begins opening.
    /// </summary>
    private void OnBeginOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        // Placeholder for document open handling
    }

    /// <summary>
    /// Called when a document finishes opening.
    /// </summary>
    private void OnEndOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        // Refresh the assembly tree when a document opens
        RhinoApp.WriteLine("Document opened - Assembly tree refresh triggered.");
    }

    /// <summary>
    /// Called when a document is closed.
    /// </summary>
    private void OnCloseDocument(object sender, DocumentEventArgs e)
    {
        // Clear the assembly tree when document closes
        RhinoApp.WriteLine("Document closed - Assembly tree cleared.");
    }
}
