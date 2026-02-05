using System;
using Eto.Drawing;
using Eto.Forms;
using RhinoAssemblyOutliner.Model;

namespace RhinoAssemblyOutliner.UI;

/// <summary>
/// Panel showing detailed properties of the selected node.
/// </summary>
public class DetailPanel : Panel
{
    private Label _titleLabel;
    private TextArea _detailsText;
    private Button _selectAllButton;
    private Button _zoomButton;
    private AssemblyNode _currentNode;

    public DetailPanel()
    {
        Content = BuildUI();
    }

    private Control BuildUI()
    {
        _titleLabel = new Label
        {
            Text = "No selection",
            Font = SystemFonts.Bold()
        };

        _detailsText = new TextArea
        {
            ReadOnly = true,
            Wrap = true,
            Height = 100
        };

        _selectAllButton = new Button
        {
            Text = "Select All Instances",
            Enabled = false
        };
        _selectAllButton.Click += OnSelectAllClick;

        _zoomButton = new Button
        {
            Text = "Zoom To",
            Enabled = false
        };
        _zoomButton.Click += OnZoomClick;

        var buttonLayout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { _selectAllButton, _zoomButton }
        };

        var layout = new DynamicLayout
        {
            Padding = new Padding(8)
        };
        layout.Add(_titleLabel);
        layout.AddSpace();
        layout.Add(_detailsText, yscale: true);
        layout.AddSpace();
        layout.Add(buttonLayout);

        return layout;
    }

    /// <summary>
    /// Shows details for the specified node.
    /// </summary>
    public void ShowNode(AssemblyNode node)
    {
        _currentNode = node;

        if (node == null)
        {
            _titleLabel.Text = "No selection";
            _detailsText.Text = "";
            _selectAllButton.Enabled = false;
            _zoomButton.Enabled = false;
            return;
        }

        _titleLabel.Text = node.DisplayName;
        _detailsText.Text = node.GetSummary();

        bool isBlockInstance = node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty;
        _selectAllButton.Enabled = isBlockInstance;
        _zoomButton.Enabled = isBlockInstance;
    }

    private void OnSelectAllClick(object sender, EventArgs e)
    {
        if (_currentNode is not BlockInstanceNode blockNode) return;

        var doc = Rhino.RhinoDoc.ActiveDoc;
        if (doc == null) return;

        // Find all instances of this definition
        var instances = AssemblyTreeBuilder.FindNodesByDefinition(
            GetRootNode(_currentNode),
            blockNode.BlockDefinitionIndex
        );

        // Select them all
        doc.Objects.UnselectAll();
        foreach (var instance in instances)
        {
            if (instance.InstanceId != Guid.Empty)
            {
                doc.Objects.Select(instance.InstanceId, true);
            }
        }
        doc.Views.Redraw();
    }

    private void OnZoomClick(object sender, EventArgs e)
    {
        if (_currentNode is BlockInstanceNode blockNode)
        {
            var doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                blockNode.ZoomToInstance(doc);
            }
        }
    }

    /// <summary>
    /// Gets the root node by traversing up the parent chain.
    /// </summary>
    private AssemblyNode GetRootNode(AssemblyNode node)
    {
        while (node.Parent != null)
        {
            node = node.Parent;
        }
        return node;
    }
}
