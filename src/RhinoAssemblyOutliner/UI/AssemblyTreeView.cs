using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoAssemblyOutliner.Model;
using RhinoAssemblyOutliner.Services;

namespace RhinoAssemblyOutliner.UI;

/// <summary>
/// Custom tree view component for displaying the assembly hierarchy.
/// </summary>
public class AssemblyTreeView : TreeGridView
{
    private DocumentNode _rootNode;
    private Dictionary<Guid, AssemblyTreeItem> _itemLookup;
    private string _filterText;
    private Font _hiddenFont;

    /// <summary>
    /// Func to resolve the active document. Set by parent panel to centralize doc access.
    /// </summary>
    public Func<RhinoDoc> GetDoc { get; set; } = () => RhinoDoc.ActiveDoc;

    /// <summary>
    /// Raised when selection changes.
    /// </summary>
    public new event EventHandler<AssemblyNode> SelectionChanged;

    /// <summary>
    /// Raised when a node is activated (double-clicked).
    /// </summary>
    public event EventHandler<AssemblyNode> NodeActivated;

    /// <summary>
    /// Raised when visibility toggle is requested.
    /// </summary>
    public event EventHandler<AssemblyNode> VisibilityToggleRequested;

    /// <summary>
    /// Raised when isolate is requested.
    /// </summary>
    public event EventHandler<AssemblyNode> IsolateRequested;

    /// <summary>
    /// Raised when show all is requested.
    /// </summary>
    public event EventHandler ShowAllRequested;

    /// <summary>
    /// Raised when "Set as Assembly Root" is requested.
    /// </summary>
    public event EventHandler<BlockInstanceNode> SetAsAssemblyRootRequested;

    /// <summary>
    /// Raised when hide is requested for the selected node.
    /// </summary>
    public event EventHandler<AssemblyNode> HideRequested;

    /// <summary>
    /// Raised when show is requested for the selected node.
    /// </summary>
    public event EventHandler<AssemblyNode> ShowRequested;

    /// <summary>
    /// Raised when zoom-to-selected is requested.
    /// </summary>
    public event EventHandler<AssemblyNode> ZoomToRequested;

    /// <summary>
    /// Raised when "Hide with Children" is requested.
    /// </summary>
    public event EventHandler<AssemblyNode> HideWithChildrenRequested;

    /// <summary>
    /// Raised when "Show with Children" is requested.
    /// </summary>
    public event EventHandler<AssemblyNode> ShowWithChildrenRequested;

    /// <summary>
    /// Raised when a top-level item is reordered via drag and drop.
    /// </summary>
    public event EventHandler<(int fromIndex, int toIndex)> ItemReordered;

    // Drag state
    private AssemblyTreeItem _dragSourceItem;

    public AssemblyTreeView()
    {
        _itemLookup = new Dictionary<Guid, AssemblyTreeItem>();
        
        // Configure tree
        AllowMultipleSelection = false;
        ShowHeader = true;
        Border = BorderType.None;
        AllowDrop = true;

        // Define columns
        // Visibility toggle column (eye icon)
        var visibilityColumn = new GridColumn
        {
            HeaderText = "👁",
            DataCell = new TextBoxCell(0),  // Shows 👁 or 👁‍🗨 based on visibility
            Width = 30,
            Editable = false
        };
        Columns.Add(visibilityColumn);

        Columns.Add(new GridColumn
        {
            HeaderText = "Name",
            DataCell = new TextBoxCell(1),
            AutoSize = true,
            Editable = false
        });

        Columns.Add(new GridColumn
        {
            HeaderText = "Layer",
            DataCell = new TextBoxCell(2),
            Width = 120
        });

        Columns.Add(new GridColumn
        {
            HeaderText = "Type",
            DataCell = new TextBoxCell(3),
            Width = 80
        });

        // Wire up events
        SelectedItemChanged += OnSelectedItemChanged;
        Activated += OnActivated;
        CellFormatting += OnCellFormatting;
        CellClick += OnCellClick;
        CellDoubleClick += OnCellDoubleClick;
        
        // Context menu
        ContextMenu = BuildContextMenu();

        // Keyboard shortcuts
        KeyDown += OnKeyDown;

        // Drag & drop for reordering
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMoveForDrag;
        DragOver += OnDragOver;
        DragDrop += OnDragDrop;
    }

    /// <summary>
    /// Handles keyboard shortcuts when tree has focus.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var item = SelectedItem as AssemblyTreeItem;
        var node = item?.Node;

        switch (e.Key)
        {
            case Keys.Delete:
            case Keys.Backspace:
                // Delete/Backspace → Hide selected
                if (node != null)
                {
                    HideRequested?.Invoke(this, node);
                    e.Handled = true;
                }
                break;

            case Keys.H:
                if (e.Modifiers == (Keys.Control | Keys.Shift))
                {
                    // Ctrl+Shift+H → Show All
                    ShowAllRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                else if (e.Modifiers == Keys.None)
                {
                    // H → Hide selected
                    if (node != null)
                    {
                        HideRequested?.Invoke(this, node);
                        e.Handled = true;
                    }
                }
                break;

            case Keys.S:
                if (e.Modifiers == Keys.None && node != null)
                {
                    // S → Show selected
                    ShowRequested?.Invoke(this, node);
                    e.Handled = true;
                }
                break;

            case Keys.H when e.Modifiers == Keys.Shift:
                if (node != null)
                {
                    // Shift+H → Show selected (SolidWorks convention)
                    ShowRequested?.Invoke(this, node);
                    e.Handled = true;
                }
                break;

            case Keys.I:
                if (e.Modifiers == Keys.None && node != null)
                {
                    // I → Isolate selected
                    IsolateRequested?.Invoke(this, node);
                    e.Handled = true;
                }
                break;

            case Keys.Space:
                if (e.Modifiers == Keys.None && node != null)
                {
                    // Space → Toggle visibility
                    VisibilityToggleRequested?.Invoke(this, node);
                    e.Handled = true;
                }
                break;

            case Keys.F:
                if (e.Modifiers == Keys.None && node != null)
                {
                    // F → Zoom to selected
                    ZoomToRequested?.Invoke(this, node);
                    e.Handled = true;
                }
                break;

            case Keys.Escape:
                // Esc → Exit isolate mode / show all
                ShowAllRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;

            case Keys.Enter:
                // Enter → Open BlockEdit on selected block instance
                if (node is BlockInstanceNode bn && bn.InstanceId != Guid.Empty)
                {
                    // Select the instance first so BlockEdit targets it
                    var doc = GetDoc();
                    if (doc != null)
                    {
                        doc.Objects.UnselectAll();
                        doc.Objects.Select(bn.InstanceId, true);
                        doc.Views.Redraw();
                    }
                    RhinoApp.RunScript("_-BlockEdit", false);
                    e.Handled = true;
                }
                break;
        }
    }

    /// <summary>
    /// Handles cell clicks, including visibility toggle.
    /// </summary>
    private void OnCellClick(object sender, GridCellMouseEventArgs e)
    {
        // Column 0 is the visibility toggle
        if (e.Column == 0 && e.Item is AssemblyTreeItem item)
        {
            // Toggle visibility via event
            VisibilityToggleRequested?.Invoke(this, item.Node);
            
            // Update the icon
            UpdateVisibilityIcon(item);
        }
    }

    /// <summary>
    /// Handles double-click to enter BlockEdit on block instance nodes.
    /// </summary>
    private void OnCellDoubleClick(object sender, GridCellMouseEventArgs e)
    {
        var item = e.Item as AssemblyTreeItem;
        if (item?.Node is BlockInstanceNode bn && bn.InstanceId != Guid.Empty)
        {
            var doc = GetDoc();
            if (doc != null)
            {
                doc.Objects.UnselectAll();
                doc.Objects.Select(bn.InstanceId, true);
                doc.Views.Redraw();
            }
            RhinoApp.RunScript("_-BlockEdit", false);
        }
    }

    /// <summary>
    /// Determines the visibility state of a node considering its children.
    /// Returns: "👁" (all visible), "◯" (all hidden), or "◐" (mixed).
    /// </summary>
    private string GetVisibilityIcon(AssemblyNode node)
    {
        if (node is DocumentNode) return "";
        
        if (node.Children.Count == 0)
            return node.IsVisible ? "👁" : "◯";

        bool anyVisible = false;
        bool anyHidden = false;
        CheckChildrenVisibility(node, ref anyVisible, ref anyHidden);

        if (anyVisible && anyHidden)
            return "◐";
        return node.IsVisible ? "👁" : "◯";
    }

    /// <summary>
    /// Recursively checks whether descendants include both visible and hidden nodes.
    /// </summary>
    private void CheckChildrenVisibility(AssemblyNode node, ref bool anyVisible, ref bool anyHidden)
    {
        foreach (var child in node.Children)
        {
            if (child.IsVisible)
                anyVisible = true;
            else
                anyHidden = true;

            if (anyVisible && anyHidden) return; // Early exit

            if (child.Children.Count > 0)
                CheckChildrenVisibility(child, ref anyVisible, ref anyHidden);

            if (anyVisible && anyHidden) return;
        }
    }

    /// <summary>
    /// Updates the visibility icon for a tree item and its ancestors.
    /// </summary>
    private void UpdateVisibilityIcon(AssemblyTreeItem item)
    {
        if (item.Values is object[] values && values.Length > 0)
        {
            values[0] = GetVisibilityIcon(item.Node);
        }
        ReloadItem(item);

        // Update parent icons (mixed state may have changed)
        var parent = item.Parent as AssemblyTreeItem;
        while (parent != null)
        {
            if (parent.Values is object[] parentValues && parentValues.Length > 0)
            {
                parentValues[0] = GetVisibilityIcon(parent.Node);
            }
            ReloadItem(parent);
            parent = parent.Parent as AssemblyTreeItem;
        }
    }

    /// <summary>
    /// Builds the right-click context menu.
    /// </summary>
    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        // === Visibility Section ===
        var hideItem = new ButtonMenuItem { Text = "Hide\tH" };
        hideItem.Click += (s, e) => OnHideClicked();

        var showItem = new ButtonMenuItem { Text = "Show\tS" };
        showItem.Click += (s, e) => OnShowClicked();

        var hideWithChildrenItem = new ButtonMenuItem { Text = "Hide with Dependents" };
        hideWithChildrenItem.Click += (s, e) =>
        {
            var n = (SelectedItem as AssemblyTreeItem)?.Node;
            if (n != null) HideWithChildrenRequested?.Invoke(this, n);
        };

        var showWithChildrenItem = new ButtonMenuItem { Text = "Show with Dependents" };
        showWithChildrenItem.Click += (s, e) =>
        {
            var n = (SelectedItem as AssemblyTreeItem)?.Node;
            if (n != null) ShowWithChildrenRequested?.Invoke(this, n);
        };

        var isolateItem = new ButtonMenuItem { Text = "Isolate\tI" };
        isolateItem.Click += (s, e) => OnIsolateClicked();

        var showAllItem = new ButtonMenuItem { Text = "Show All\tCtrl+Shift+H" };
        showAllItem.Click += (s, e) => ShowAllRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(hideItem);
        menu.Items.Add(showItem);
        menu.Items.Add(hideWithChildrenItem);
        menu.Items.Add(showWithChildrenItem);
        menu.Items.Add(isolateItem);
        menu.Items.Add(showAllItem);

        // === Navigation Section ===
        menu.Items.Add(new SeparatorMenuItem());

        var zoomItem = new ButtonMenuItem { Text = "Zoom To\tF" };
        zoomItem.Click += (s, e) => ZoomToSelected();

        var selectItem = new ButtonMenuItem { Text = "Select in Viewport" };
        selectItem.Click += (s, e) => SelectCurrentInViewport();

        menu.Items.Add(zoomItem);
        menu.Items.Add(selectItem);

        // === Editing Section ===
        menu.Items.Add(new SeparatorMenuItem());

        var blockEditItem = new ButtonMenuItem { Text = "BlockEdit\tEnter" };
        blockEditItem.Click += (s, e) =>
        {
            var item = SelectedItem as AssemblyTreeItem;
            if (item?.Node is BlockInstanceNode bn && bn.InstanceId != Guid.Empty)
            {
                var doc = GetDoc();
                if (doc != null)
                {
                    doc.Objects.UnselectAll();
                    doc.Objects.Select(bn.InstanceId, true);
                    doc.Views.Redraw();
                }
                RhinoApp.RunScript("_-BlockEdit", false);
            }
        };

        var setAsRootItem = new ButtonMenuItem { Text = "Set as Assembly Root" };
        setAsRootItem.Click += (s, e) => OnSetAsAssemblyRootClicked();

        menu.Items.Add(blockEditItem);
        menu.Items.Add(setAsRootItem);

        return menu;
    }

    #region Context Menu Actions

    private void SelectCurrentInViewport()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item?.Node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty)
        {
            var doc = GetDoc();
            if (doc != null)
            {
                doc.Objects.UnselectAll();
                doc.Objects.Select(blockNode.InstanceId, true);
                doc.Views.Redraw();
            }
        }
    }

    private void SelectAllInstances()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item?.Node is BlockInstanceNode blockNode)
        {
            var doc = GetDoc();
            if (doc != null)
            {
                var definition = doc.InstanceDefinitions[blockNode.BlockDefinitionIndex];
                if (definition != null && !definition.IsDeleted)
                {
                    doc.Objects.UnselectAll();
                    var instances = definition.GetReferences(1);
                    foreach (var instance in instances)
                    {
                        doc.Objects.Select(instance.Id, true);
                    }
                    doc.Views.Redraw();
                }
            }
        }
    }

    private void ZoomToSelected()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item?.Node is BlockInstanceNode blockNode)
        {
            var doc = GetDoc();
            blockNode.ZoomToInstance(doc);
        }
    }

    private void OnHideClicked()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            HideRequested?.Invoke(this, item.Node);
        }
    }

    private void OnShowClicked()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            ShowRequested?.Invoke(this, item.Node);
        }
    }

    private void OnIsolateClicked()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            IsolateRequested?.Invoke(this, item.Node);
        }
    }

    private void ExpandSelected()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            ExpandItemRecursive(item);
            ReloadData();
        }
    }

    private void CollapseSelected()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            CollapseItemRecursive(item);
            ReloadData();
        }
    }

    private void ExpandItemRecursive(TreeGridItem item)
    {
        item.Expanded = true;
        foreach (var child in item.Children.OfType<TreeGridItem>())
        {
            ExpandItemRecursive(child);
        }
    }

    private void CollapseItemRecursive(TreeGridItem item)
    {
        item.Expanded = false;
        foreach (var child in item.Children.OfType<TreeGridItem>())
        {
            CollapseItemRecursive(child);
        }
    }

    private void OnSetAsAssemblyRootClicked()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item?.Node is BlockInstanceNode blockNode)
        {
            SetAsAssemblyRootRequested?.Invoke(this, blockNode);
        }
    }

    #endregion

    /// <summary>
    /// Loads the tree from a document root node.
    /// </summary>
    public void LoadTree(DocumentNode rootNode)
    {
        _rootNode = rootNode;
        _itemLookup.Clear();

        var collection = new TreeGridItemCollection();

        // Add document root
        var rootItem = new AssemblyTreeItem(rootNode);
        RegisterItemRecursive(rootItem);
        collection.Add(rootItem);

        DataStore = collection;

        // Expand root by default
        rootItem.Expanded = true;
    }
    
    /// <summary>
    /// Loads the tree from a block instance node (Assembly Mode).
    /// </summary>
    public void LoadTreeFromBlock(BlockInstanceNode blockNode)
    {
        _rootNode = null; // No document root in assembly mode
        _itemLookup.Clear();

        var collection = new TreeGridItemCollection();

        // Add block as root
        var rootItem = new AssemblyTreeItem(blockNode);
        RegisterItemRecursive(rootItem);
        collection.Add(rootItem);

        DataStore = collection;

        // Expand root by default
        rootItem.Expanded = true;
    }

    /// <summary>
    /// Registers items in the lookup dictionary for fast access.
    /// </summary>
    private void RegisterItemRecursive(AssemblyTreeItem item)
    {
        _itemLookup[item.Node.Id] = item;

        foreach (var child in item.Children.OfType<AssemblyTreeItem>())
        {
            RegisterItemRecursive(child);
        }
    }

    /// <summary>
    /// Selects a node by its Rhino object ID.
    /// Since AssemblyNode.Id now equals the Rhino object ID, this is O(1).
    /// </summary>
    public void SelectNodeByObjectId(Guid objectId)
    {
        // Direct O(1) lookup — node Id matches Rhino object Id
        if (_itemLookup.TryGetValue(objectId, out var item))
        {
            ExpandToItem(item);
            SelectedItem = item;
        }
    }

    /// <summary>
    /// Expands all parent nodes to make an item visible.
    /// </summary>
    private void ExpandToItem(AssemblyTreeItem item)
    {
        var parentItem = item.Parent as AssemblyTreeItem;
        while (parentItem != null)
        {
            parentItem.Expanded = true;
            parentItem = parentItem.Parent as AssemblyTreeItem;
        }
        ReloadData();
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        UnselectAll();
    }

    /// <summary>
    /// Expands all nodes in the tree.
    /// </summary>
    public void ExpandAll()
    {
        ExpandAllRecursive(DataStore as TreeGridItemCollection);
        ReloadData();
    }

    private void ExpandAllRecursive(TreeGridItemCollection items)
    {
        if (items == null) return;

        foreach (var item in items.OfType<TreeGridItem>())
        {
            item.Expanded = true;
            ExpandAllRecursive(item.Children);
        }
    }

    /// <summary>
    /// Collapses all nodes in the tree.
    /// </summary>
    public void CollapseAll()
    {
        CollapseAllRecursive(DataStore as TreeGridItemCollection);
        ReloadData();
    }

    private void CollapseAllRecursive(TreeGridItemCollection items)
    {
        if (items == null) return;

        foreach (var item in items.OfType<TreeGridItem>())
        {
            item.Expanded = false;
            CollapseAllRecursive(item.Children);
        }
    }

    /// <summary>
    /// Filters the tree by text.
    /// </summary>
    public void FilterByText(string text)
    {
        _filterText = text?.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(_filterText))
        {
            // Show all
            if (_rootNode != null)
                LoadTree(_rootNode);
        }
        else
        {
            // Rebuild with filter
            LoadTreeFiltered();
        }
    }

    private void LoadTreeFiltered()
    {
        if (_rootNode == null) return;

        var collection = new TreeGridItemCollection();
        var filteredRoot = CreateFilteredItem(_rootNode);
        
        if (filteredRoot != null)
        {
            collection.Add(filteredRoot);
            _itemLookup.Clear();
            RegisterItemRecursive(filteredRoot);
        }

        DataStore = collection;
    }

    private AssemblyTreeItem CreateFilteredItem(AssemblyNode node)
    {
        // Check if this node or any descendant matches
        bool matches = node.DisplayName.ToLowerInvariant().Contains(_filterText);
        var matchingChildren = new List<AssemblyTreeItem>();

        foreach (var child in node.Children)
        {
            var filteredChild = CreateFilteredItem(child);
            if (filteredChild != null)
            {
                matchingChildren.Add(filteredChild);
            }
        }

        // Include this node if it matches or has matching children
        if (matches || matchingChildren.Count > 0)
        {
            var item = new AssemblyTreeItem(node, includeChildren: false);
            foreach (var child in matchingChildren)
            {
                item.Children.Add(child);
                child.Parent = item;
            }
            item.Expanded = true;  // Expand filtered results
            return item;
        }

        return null;
    }

    #region Drag & Drop Reorder

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Buttons == MouseButtons.Primary)
        {
            _dragSourceItem = SelectedItem as AssemblyTreeItem;
            // Only allow dragging top-level items (direct children of DocumentNode)
            if (_dragSourceItem != null)
            {
                var parent = _dragSourceItem.Parent as AssemblyTreeItem;
                if (parent?.Node is not DocumentNode)
                    _dragSourceItem = null;
            }
        }
    }

    private void OnMouseMoveForDrag(object sender, MouseEventArgs e)
    {
        if (_dragSourceItem != null && e.Buttons == MouseButtons.Primary)
        {
            var data = new DataObject();
            data.SetString(_dragSourceItem.Node.Id.ToString(), "AssemblyNodeId");
            DoDragDrop(data, DragEffects.Move);
            _dragSourceItem = null;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        // Allow drop only on top-level siblings
        var targetItem = SelectedItem as AssemblyTreeItem;
        if (targetItem != null)
        {
            var parent = targetItem.Parent as AssemblyTreeItem;
            if (parent?.Node is DocumentNode)
            {
                e.Effects = DragEffects.Move;
                return;
            }
        }
        e.Effects = DragEffects.None;
    }

    private void OnDragDrop(object sender, DragEventArgs e)
    {
        var targetItem = SelectedItem as AssemblyTreeItem;
        if (targetItem == null) return;

        var nodeIdStr = e.Data.GetString("AssemblyNodeId");
        if (string.IsNullOrEmpty(nodeIdStr) || !Guid.TryParse(nodeIdStr, out var sourceNodeId))
            return;

        if (!_itemLookup.TryGetValue(sourceNodeId, out var sourceItem))
            return;

        // Both must be top-level children of the document root
        var sourceParent = sourceItem.Parent as AssemblyTreeItem;
        var targetParent = targetItem.Parent as AssemblyTreeItem;
        if (sourceParent?.Node is not DocumentNode root || targetParent?.Node != root)
            return;

        int fromIndex = root.Children.IndexOf(sourceItem.Node);
        int toIndex = root.Children.IndexOf(targetItem.Node);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            return;

        // Reorder in model
        var child = root.Children[fromIndex];
        root.Children.RemoveAt(fromIndex);
        root.Children.Insert(toIndex, child);

        // Reload tree UI
        LoadTree(_rootNode);

        // Notify panel to persist order
        ItemReordered?.Invoke(this, (fromIndex, toIndex));
    }

    #endregion

    #region Event Handlers

    private void OnSelectedItemChanged(object sender, EventArgs e)
    {
        var item = SelectedItem as AssemblyTreeItem;
        SelectionChanged?.Invoke(this, item?.Node);
    }

    private void OnActivated(object sender, EventArgs e)
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            NodeActivated?.Invoke(this, item.Node);
        }
    }

    private void OnCellFormatting(object sender, GridCellFormatEventArgs e)
    {
        // Could add custom formatting based on node type
        var item = e.Item as AssemblyTreeItem;
        if (item == null) return;

        // Gray out and italicize hidden nodes (font cached to avoid per-cell allocation)
        if (!item.Node.IsVisible)
        {
            e.ForegroundColor = Eto.Drawing.Colors.Gray;
            if (e.Font == null) return;
            if (_hiddenFont == null || _hiddenFont.Family != e.Font.Family || _hiddenFont.Size != e.Font.Size)
            {
                _hiddenFont?.Dispose();
                _hiddenFont = new Font(e.Font.Family, e.Font.Size, FontStyle.Italic);
            }
            e.Font = _hiddenFont;
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hiddenFont?.Dispose();
            _hiddenFont = null;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Tree grid item wrapping an AssemblyNode.
/// </summary>
public class AssemblyTreeItem : TreeGridItem
{
    public AssemblyNode Node { get; }

    public AssemblyTreeItem(AssemblyNode node, bool includeChildren = true)
    {
        Node = node;

        // Set values for columns
        string visibilityIcon = GetInitialVisibilityIcon(node);
        string typeIcon = "";
        string layerName = "";
        string typeName = "";

        if (node is BlockInstanceNode blockNode)
        {
            layerName = blockNode.Layer?.FullPath ?? "";
            typeName = blockNode.LinkType.ToString();
            
            // Block type icons
            typeIcon = blockNode.LinkType switch
            {
                Rhino.DocObjects.InstanceDefinitionUpdateType.Linked => "🔗",           // Linked block
                Rhino.DocObjects.InstanceDefinitionUpdateType.LinkedAndEmbedded => "📎", // Linked & Embedded
                _ => "📦"  // Embedded (default)
            };
        }
        else if (node is DocumentNode)
        {
            typeName = "Document";
            typeIcon = "📄";
            visibilityIcon = "";  // No visibility toggle for document
        }

        // Format: "Icon Name #n" for display
        string displayName = string.IsNullOrEmpty(typeIcon) 
            ? node.DisplayName 
            : $"{typeIcon} {node.DisplayName}";

        Values = new object[]
        {
            visibilityIcon,
            displayName,
            layerName,
            typeName
        };

        // Add children
        if (includeChildren)
        {
            foreach (var child in node.Children)
            {
                var childItem = new AssemblyTreeItem(child);
                childItem.Parent = this;
                Children.Add(childItem);
            }
        }

        Expanded = false;
    }

    /// <summary>
    /// Determines the initial visibility icon for a node, including mixed state for parents.
    /// </summary>
    private static string GetInitialVisibilityIcon(AssemblyNode node)
    {
        if (node is DocumentNode) return "";
        if (node.Children.Count == 0)
            return node.IsVisible ? "👁" : "◯";

        bool anyVisible = false;
        bool anyHidden = false;
        CheckVisibilityRecursive(node, ref anyVisible, ref anyHidden);

        if (anyVisible && anyHidden) return "◐";
        return node.IsVisible ? "👁" : "◯";
    }

    private static void CheckVisibilityRecursive(AssemblyNode node, ref bool anyVisible, ref bool anyHidden)
    {
        foreach (var child in node.Children)
        {
            if (child.IsVisible) anyVisible = true;
            else anyHidden = true;
            if (anyVisible && anyHidden) return;
            if (child.Children.Count > 0)
                CheckVisibilityRecursive(child, ref anyVisible, ref anyHidden);
            if (anyVisible && anyHidden) return;
        }
    }
}
