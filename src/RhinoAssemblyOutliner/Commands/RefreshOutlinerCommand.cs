using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoAssemblyOutliner.UI;

namespace RhinoAssemblyOutliner.Commands;

/// <summary>
/// Command to refresh the Assembly Outliner tree.
/// </summary>
public class RefreshOutlinerCommand : Command
{
    public static RefreshOutlinerCommand Instance { get; private set; }
    
    public RefreshOutlinerCommand()
    {
        Instance = this;
        // Also register panel here as backup (first registration wins)
        Panels.RegisterPanel(PlugIn, typeof(AssemblyOutlinerPanel), "Assembly Outliner", null);
    }
    
    public override string EnglishName => "AssemblyOutlinerRefresh";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Find the panel and refresh it
        var panel = Panels.GetPanel<AssemblyOutlinerPanel>(doc.RuntimeSerialNumber);
        if (panel != null)
        {
            panel.RefreshTree();
            RhinoApp.WriteLine("Assembly Outliner refreshed.");
        }
        else
        {
            // Try to open the panel first
            Panels.OpenPanel(AssemblyOutlinerPanel.PanelId);
            RhinoApp.WriteLine("Assembly Outliner panel opened.");
        }
        
        return Result.Success;
    }
}
