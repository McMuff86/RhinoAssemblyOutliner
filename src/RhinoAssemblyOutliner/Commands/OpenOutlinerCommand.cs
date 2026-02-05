using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoAssemblyOutliner.UI;

namespace RhinoAssemblyOutliner.Commands;

/// <summary>
/// Command to open the Assembly Outliner panel.
/// </summary>
public class OpenOutlinerCommand : Command
{
    public static OpenOutlinerCommand Instance { get; private set; }
    
    public OpenOutlinerCommand()
    {
        Instance = this;
        // Register the panel in the command constructor (this is the correct pattern)
        Panels.RegisterPanel(PlugIn, typeof(AssemblyOutlinerPanel), "Assembly Outliner", null);
    }
    
    public override string EnglishName => "AssemblyOutliner";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var panelId = AssemblyOutlinerPanel.PanelId;
        bool visible = Panels.IsPanelVisible(panelId);
        
        if (visible)
            Panels.ClosePanel(panelId);
        else
            Panels.OpenPanel(panelId);
            
        return Result.Success;
    }
}

/// <summary>
/// Command to refresh the Assembly Outliner tree.
/// </summary>
public class RefreshOutlinerCommand : Command
{
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
            RhinoApp.WriteLine("Assembly Outliner panel is not open.");
        }
        
        return Result.Success;
    }
}
