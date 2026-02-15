using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;
using Rhino.UI;
using RhinoAssemblyOutliner.Model;
using RhinoAssemblyOutliner.Services;

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
    private VisibilityService _visibilityService;
    private DropDown _modeDropdown;
    private Label _modeLabel;
    
    // View mode state
    private OutlinerViewMode _currentMode = OutlinerViewMode.Document;
    private Guid _assemblyRootId = Guid.Empty;
    private string _assemblyRootName = "";
    
    // Event debouncing
    private Label _statusBar;
    private System.Timers.Timer _refreshTimer;
    private int _needsRefresh; // 0 or 1, accessed via Interlocked
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
        _refreshTimer = new System.Timers.Timer(100);
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
        _treeView.VisibilityToggleRequested += OnVisibilityToggleRequested;
        _treeView.IsolateRequested += OnIsolateRequested;
        _treeView.ShowAllRequested += OnShowAllRequested;
        _treeView.SetAsAssemblyRootRequested += OnSetAsAssemblyRootRequested;
        _treeView.HideRequested += OnHideRequested;
        _treeView.ShowRequested += OnShowRequested;
        _treeView.ZoomToRequested += OnZoomToRequested;
        _treeView.HideWithChildrenRequested += OnHideWithChildrenRequested;
        _treeView.ShowWithChildrenRequested += OnShowWithChildrenRequested;

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

        // Status bar
        _statusBar = new Label
        {
            Text = "0 instances",
            TextColor = Colors.Gray,
            Font = SystemFonts.Label(SystemFonts.Default().Size - 1)
        };
        var statusPanel = new Panel
        {
            Content = _statusBar,
            Padding = new Padding(6, 2)
        };

        // Main layout
        var layout = new DynamicLayout();
        layout.BeginVertical();
        layout.Add(toolbar);
        layout.Add(_searchBox);
        layout.EndVertical();
        layout.Add(splitter, yscale: true);
        layout.Add(statusPanel);

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

        // Mode dropdown
        _modeDropdown = new DropDown { Width = 140 };
        _modeDropdown.Items.Add("📄 Document");
        _modeDropdown.SelectedIndex = 0;
        _modeDropdown.SelectedIndexChanged += OnModeDropdownChanged;
        
        // Mode label (shows assembly name when in Assembly mode)
        _modeLabel = new Label { Text = "", TextColor = Colors.Gray };

        var layout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Padding = new Padding(4),
            Items = { refreshButton, expandAllButton, collapseAllButton, _modeDropdown, _modeLabel }
        };

        return layout;
    }
    
    /// <summary>
    /// Handles mode dropdown change.
    /// </summary>
    private void OnModeDropdownChanged(object sender, EventArgs e)
    {
        if (_modeDropdown.SelectedIndex == 0)
        {
            // Document mode
            SetDocumentMode();
        }
        // Other items are assembly roots - handled in the item data
    }
    
    /// <summary>
    /// Sets the outliner to Document mode (show all blocks).
    /// </summary>
    public void SetDocumentMode()
    {
        _currentMode = OutlinerViewMode.Document;
        _assemblyRootId = Guid.Empty;
        _assemblyRootName = "";
        _modeLabel.Text = "";
        _modeDropdown.SelectedIndex = 0;
        RefreshTree();
    }
    
    /// <summary>
    /// Sets the outliner to Assembly mode with the specified root block.
    /// </summary>
    public void SetAssemblyMode(Guid rootId, string rootName)
    {
        _currentMode = OutlinerViewMode.Assembly;
        _assemblyRootId = rootId;
        _assemblyRootName = rootName;
        _modeLabel.Text = $"→ {rootName}";
        
        // Add to dropdown if not already there
        string itemText = $"📦 {rootName}";
        bool found = false;
        for (int i = 1; i < _modeDropdown.Items.Count; i++)
        {
            if (_modeDropdown.Items[i].Text == itemText)
            {
                _modeDropdown.SelectedIndex = i;
                found = true;
                break;
            }
        }
        if (!found)
        {
            _modeDropdown.Items.Add(itemText);
            _modeDropdown.SelectedIndex = _modeDropdown.Items.Count - 1;
        }
        
        RefreshTree();
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

    private void OnVisibilityToggleRequested(object sender, AssemblyNode node)
    {
        EnsureVisibilityService();
        _visibilityService?.ToggleVisibility(node);
        _treeView.ReloadData();
        UpdateStatusBar(_rootNode);
    }

    private void OnIsolateRequested(object sender, AssemblyNode node)
    {
        EnsureVisibilityService();
        _visibilityService?.Isolate(node);
        _treeView.ReloadData();
        UpdateStatusBar(_rootNode);
    }

    private void OnShowAllRequested(object sender, EventArgs e)
    {
        EnsureVisibilityService();
        _visibilityService?.ShowAll();
        RefreshTree();
    }
    
    private void OnHideRequested(object sender, AssemblyNode node)
    {
        EnsureVisibilityService();
        _visibilityService?.Hide(node);
        _treeView.ReloadData();
        UpdateStatusBar(_rootNode);
    }

    private void OnShowRequested(object sender, AssemblyNode node)
    {
        EnsureVisibilityService();
        _visibilityService?.Show(node);
        _treeView.ReloadData();
        UpdateStatusBar(_rootNode);
    }

    private void OnZoomToRequested(object sender, AssemblyNode node)
    {
        if (node is BlockInstanceNode blockNode)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                blockNode.ZoomToInstance(doc);
            }
        }
    }

    private void OnHideWithChildrenRequested(object sender, AssemblyNode node)
    {
        EnsureVisibilityService();
        _visibilityService?.Hide(node, includeChildren: true);
        _treeView.ReloadData();
        UpdateStatusBar(_rootNode);
    }

    private void OnShowWithChildrenRequested(object sender, AssemblyNode node)
    {
        EnsureVisibilityService();
        _visibilityService?.Show(node, includeChildren: true);
        _treeView.ReloadData();
        UpdateStatusBar(_rootNode);
    }

    private void OnSetAsAssemblyRootRequested(object sender, BlockInstanceNode blockNode)
    {
        if (blockNode != null && blockNode.InstanceId != Guid.Empty)
        {
            SetAssemblyMode(blockNode.InstanceId, blockNode.DefinitionName);
        }
    }

    private uint _visibilityServiceDocSerial;

    private void EnsureVisibilityService()
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        // Recreate if doc changed or not yet created
        if (_visibilityService == null || _visibilityServiceDocSerial != doc.RuntimeSerialNumber)
        {
            _visibilityService = new VisibilityService(doc);
            _visibilityServiceDocSerial = doc.RuntimeSerialNumber;
        }
    }

    #endregion

    #region Tree Management

    /// <summary>
    /// Updates the status bar with current counts.
    /// </summary>
    private void UpdateStatusBar(AssemblyNode root)
    {
        if (root == null)
        {
            _statusBar.Text = "0 instances";
            return;
        }

        var allNodes = root.GetAllDescendants().OfType<BlockInstanceNode>().ToList();
        int total = allNodes.Count;
        int hidden = allNodes.Count(n => !n.IsVisible);
        // Count isolated: if any are visible and some hidden, we're in isolate-like state
        // Simple approach: show hidden count
        var parts = new List<string> { $"{total} instances" };
        if (hidden > 0) parts.Add($"{hidden} hidden");
        _statusBar.Text = string.Join(" | ", parts);
    }

    /// <summary>
    /// Queues a debounced tree refresh.
    /// </summary>
    private void QueueRefresh()
    {
        Interlocked.Exchange(ref _needsRefresh, 1);
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    /// <summary>
    /// Performs the debounced refresh on UI thread.
    /// </summary>
    private void RefreshTreeDebounced()
    {
        if (Interlocked.CompareExchange(ref _needsRefresh, 0, 1) == 1)
        {
            RhinoApp.InvokeOnUiThread((Action)RefreshTree);
        }
    }

    /// <summary>
    /// Refreshes the tree from the current document.
    /// </summary>
    public void RefreshTree()
    {
        try
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            var builder = new AssemblyTreeBuilder(doc);
            
            if (_currentMode == OutlinerViewMode.Assembly && _assemblyRootId != Guid.Empty)
            {
                // Assembly mode - show only the selected root and its children
                var assemblyRoot = builder.BuildTreeFromRoot(_assemblyRootId);
                if (assemblyRoot != null)
                {
                    _treeView.LoadTreeFromBlock(assemblyRoot);
                    UpdateStatusBar(assemblyRoot);
                }
                else
                {
                    // Root not found - switch back to document mode
                    RhinoApp.WriteLine("AssemblyOutliner: Assembly root not found, switching to Document mode.");
                    SetDocumentMode();
                }
            }
            else
            {
                // Document mode - show all blocks
                _rootNode = builder.BuildTree();
                if (_rootNode != null)
                {
                    _treeView.LoadTree(_rootNode);
                    UpdateStatusBar(_rootNode);
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Error refreshing tree: {ex.Message}");
        }
    }

    #endregion
}
