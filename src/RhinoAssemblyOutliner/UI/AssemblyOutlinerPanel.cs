using System;
using System.Timers;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;
using Rhino.UI;
using RhinoAssemblyOutliner.Model;

namespace RhinoAssemblyOutliner.UI;

/// <summary>
/// Main dockable panel for the Assembly Outliner.
/// Displays a hierarchical tree view of block instances in the document.
/// </summary>
[System.Runtime.InteropServices.Guid("7E8F9A0B-1C2D-3E4F-5A6B-7C8D9E0F1A2B")]
public class AssemblyOutlinerPanel : Panel, IPanel
{
    private readonly uint _documentSerialNumber;
    private AssemblyTreeView _treeView;
    private DetailPanel _detailPanel;
    private SearchBox _searchBox;
    private DocumentNode _rootNode;
    
    // Event debouncing
    private Timer _refreshTimer;
    private bool _needsRefresh;
    private bool _isSyncingFromViewport;
    private bool _isSyncingFromTree;

    /// <summary>
    /// Gets the panel GUID for registration.
    /// </summary>
    public static Guid PanelId => typeof(AssemblyOutlinerPanel).GUID;

    /// <summary>
    /// Creates a new Assembly Outliner panel.
    /// </summary>
    /// <param name="documentSerialNumber">The document this panel is associated with.</param>
    public AssemblyOutlinerPanel(uint documentSerialNumber)
    {
        _documentSerialNumber = documentSerialNumber;
        
        // Initialize refresh timer (100ms debounce)
        _refreshTimer = new Timer(100);
        _refreshTimer.AutoReset = false;
        _refreshTimer.Elapsed += (s, e) => RefreshTreeDebounced();
        
        Content = BuildUI();
    }

    /// <summary>
    /// Builds the panel UI layout.
    /// </summary>
    private Control BuildUI()
    {
        // Search box at top
        _searchBox = new SearchBox
        {
            PlaceholderText = "Filter blocks..."
        };
        _searchBox.TextChanged += OnSearchTextChanged;

        // Tree view (main content)
        _treeView = new AssemblyTreeView();
        _treeView.SelectionChanged += OnTreeSelectionChanged;
        _treeView.NodeActivated += OnTreeNodeActivated;

        // Detail panel at bottom
        _detailPanel = new DetailPanel();

        // Splitter for tree and detail
        var splitter = new Splitter
        {
            Orientation = Orientation.Vertical,
            Panel1 = _treeView,
            Panel2 = _detailPanel,
            Position = 300,
            FixedPanel = SplitterFixedPanel.Panel2
        };

        // Toolbar
        var toolbar = BuildToolbar();

        // Main layout
        var layout = new DynamicLayout();
        layout.BeginVertical();
        layout.Add(toolbar);
        layout.Add(_searchBox);
        layout.EndVertical();
        layout.Add(splitter, yscale: true);

        return layout;
    }

    /// <summary>
    /// Builds the toolbar with action buttons.
    /// </summary>
    private Control BuildToolbar()
    {
        var refreshButton = new Button { Text = "↻", ToolTip = "Refresh Tree", Width = 30 };
        refreshButton.Click += (s, e) => RefreshTree();

        var expandAllButton = new Button { Text = "⊞", ToolTip = "Expand All", Width = 30 };
        expandAllButton.Click += (s, e) => _treeView.ExpandAll();

        var collapseAllButton = new Button { Text = "⊟", ToolTip = "Collapse All", Width = 30 };
        collapseAllButton.Click += (s, e) => _treeView.CollapseAll();

        var layout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Padding = new Padding(4),
            Items = { refreshButton, expandAllButton, collapseAllButton }
        };

        return layout;
    }

    #region IPanel Implementation

    /// <summary>
    /// Called when the panel becomes visible.
    /// </summary>
    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        SubscribeEvents();
        RefreshTree();
    }

    /// <summary>
    /// Called when the panel is hidden.
    /// </summary>
    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
    {
        // Can pause updates here if needed
    }

    /// <summary>
    /// Called when the panel is closing.
    /// </summary>
    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        UnsubscribeEvents();
        _refreshTimer?.Dispose();
    }

    #endregion

    #region Event Handling

    /// <summary>
    /// Subscribes to Rhino document events.
    /// </summary>
    private void SubscribeEvents()
    {
        RhinoDoc.InstanceDefinitionTableEvent += OnInstanceDefinitionTableEvent;
        RhinoDoc.AddRhinoObject += OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject += OnDeleteRhinoObject;
        RhinoDoc.SelectObjects += OnSelectObjects;
        RhinoDoc.DeselectObjects += OnDeselectObjects;
        RhinoDoc.DeselectAllObjects += OnDeselectAllObjects;
        RhinoDoc.EndOpenDocument += OnEndOpenDocument;
    }

    /// <summary>
    /// Unsubscribes from Rhino document events.
    /// </summary>
    private void UnsubscribeEvents()
    {
        RhinoDoc.InstanceDefinitionTableEvent -= OnInstanceDefinitionTableEvent;
        RhinoDoc.AddRhinoObject -= OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject -= OnDeleteRhinoObject;
        RhinoDoc.SelectObjects -= OnSelectObjects;
        RhinoDoc.DeselectObjects -= OnDeselectObjects;
        RhinoDoc.DeselectAllObjects -= OnDeselectAllObjects;
        RhinoDoc.EndOpenDocument -= OnEndOpenDocument;
    }

    private void OnInstanceDefinitionTableEvent(object sender, InstanceDefinitionTableEventArgs e)
    {
        QueueRefresh();
    }

    private void OnAddRhinoObject(object sender, RhinoObjectEventArgs e)
    {
        if (e.TheObject is InstanceObject)
        {
            QueueRefresh();
        }
    }

    private void OnDeleteRhinoObject(object sender, RhinoObjectEventArgs e)
    {
        if (e.TheObject is InstanceObject)
        {
            QueueRefresh();
        }
    }

    private void OnSelectObjects(object sender, RhinoObjectSelectionEventArgs e)
    {
        if (_isSyncingFromTree) return;

        _isSyncingFromViewport = true;
        try
        {
            foreach (var obj in e.RhinoObjects)
            {
                if (obj is InstanceObject instance)
                {
                    _treeView.SelectNodeByObjectId(instance.Id);
                }
            }
        }
        finally
        {
            _isSyncingFromViewport = false;
        }
    }

    private void OnDeselectObjects(object sender, RhinoObjectSelectionEventArgs e)
    {
        // Could deselect in tree, but might be annoying
    }

    private void OnDeselectAllObjects(object sender, RhinoDeselectAllObjectsEventArgs e)
    {
        if (!_isSyncingFromTree)
        {
            _treeView.ClearSelection();
        }
    }

    private void OnEndOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        RefreshTree();
    }

    private void OnSearchTextChanged(object sender, EventArgs e)
    {
        _treeView.FilterByText(_searchBox.Text);
    }

    private void OnTreeSelectionChanged(object sender, AssemblyNode node)
    {
        // Update detail panel
        _detailPanel.ShowNode(node);

        // Sync selection to viewport
        if (!_isSyncingFromViewport && node is BlockInstanceNode blockNode)
        {
            _isSyncingFromTree = true;
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null && blockNode.InstanceId != Guid.Empty)
                {
                    doc.Objects.UnselectAll();
                    doc.Objects.Select(blockNode.InstanceId, true);
                    doc.Views.Redraw();
                }
            }
            finally
            {
                _isSyncingFromTree = false;
            }
        }
    }

    private void OnTreeNodeActivated(object sender, AssemblyNode node)
    {
        // Double-click: Zoom to object
        if (node is BlockInstanceNode blockNode)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                blockNode.ZoomToInstance(doc);
            }
        }
    }

    #endregion

    #region Tree Management

    /// <summary>
    /// Queues a debounced tree refresh.
    /// </summary>
    private void QueueRefresh()
    {
        _needsRefresh = true;
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    /// <summary>
    /// Performs the debounced refresh on UI thread.
    /// </summary>
    private void RefreshTreeDebounced()
    {
        if (_needsRefresh)
        {
            _needsRefresh = false;
            RhinoApp.InvokeOnUiThread((Action)RefreshTree);
        }
    }

    /// <summary>
    /// Refreshes the tree from the current document.
    /// </summary>
    public void RefreshTree()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var builder = new AssemblyTreeBuilder(doc);
        _rootNode = builder.BuildTree();
        _treeView.LoadTree(_rootNode);
    }

    #endregion
}
