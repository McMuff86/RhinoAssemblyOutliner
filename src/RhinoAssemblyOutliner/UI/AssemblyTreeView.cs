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

    public AssemblyTreeView()
    {
        _itemLookup = new Dictionary<Guid, AssemblyTreeItem>();
        
        // Configure tree
        AllowMultipleSelection = false;
        ShowHeader = true;
        Border = BorderType.None;

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
        
        // Context menu
        ContextMenu = BuildContextMenu();

        // Keyboard shortcuts
        KeyDown += OnKeyDown;
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
    /// Updates the visibility icon for a tree item.
    /// </summary>
    private void UpdateVisibilityIcon(AssemblyTreeItem item)
    {
        if (item.Values is object[] values && values.Length > 0)
        {
            values[0] = item.Node.IsVisible ? "👁" : "◯";
        }
        ReloadItem(item);
    }

    /// <summary>
    /// Builds the right-click context menu.
    /// </summary>
    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var selectItem = new ButtonMenuItem { Text = "Select in Viewport" };
        selectItem.Click += (s, e) => SelectCurrentInViewport();

        var selectAllItem = new ButtonMenuItem { Text = "Select All Instances" };
        selectAllItem.Click += (s, e) => SelectAllInstances();

        var zoomItem = new ButtonMenuItem { Text = "Zoom To\tF" };
        zoomItem.Click += (s, e) => ZoomToSelected();

        var separator1 = new SeparatorMenuItem();

        var hideItem = new ButtonMenuItem { Text = "Hide\tH" };
        hideItem.Click += (s, e) => OnHideClicked();

        var showItem = new ButtonMenuItem { Text = "Show\tS" };
        showItem.Click += (s, e) => OnShowClicked();

        var isolateItem = new ButtonMenuItem { Text = "Isolate\tI" };
        isolateItem.Click += (s, e) => OnIsolateClicked();

        var showAllItem = new ButtonMenuItem { Text = "Show All\tCtrl+Shift+H" };
        showAllItem.Click += (s, e) => ShowAllRequested?.Invoke(this, EventArgs.Empty);

        var separator2 = new SeparatorMenuItem();
        
        var setAsRootItem = new ButtonMenuItem { Text = "📌 Set as Assembly Root" };
        setAsRootItem.Click += (s, e) => OnSetAsAssemblyRootClicked();

        var separator3 = new SeparatorMenuItem();

        var expandItem = new ButtonMenuItem { Text = "Expand Children" };
        expandItem.Click += (s, e) => ExpandSelected();

        var collapseItem = new ButtonMenuItem { Text = "Collapse Children" };
        collapseItem.Click += (s, e) => CollapseSelected();

        menu.Items.Add(selectItem);
        menu.Items.Add(selectAllItem);
        menu.Items.Add(zoomItem);
        menu.Items.Add(separator1);
        menu.Items.Add(hideItem);
        menu.Items.Add(showItem);
        menu.Items.Add(isolateItem);
        menu.Items.Add(showAllItem);
        menu.Items.Add(separator2);
        menu.Items.Add(setAsRootItem);
        menu.Items.Add(separator3);
        menu.Items.Add(expandItem);
        menu.Items.Add(collapseItem);

        return menu;
    }

    #region Context Menu Actions

    private void SelectCurrentInViewport()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item?.Node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty)
        {
            var doc = RhinoDoc.ActiveDoc;
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
            var doc = RhinoDoc.ActiveDoc;
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
            var doc = RhinoDoc.ActiveDoc;
            blockNode.ZoomToInstance(doc);
        }
    }

    private void OnHideClicked()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            VisibilityToggleRequested?.Invoke(this, item.Node);
        }
    }

    private void OnShowClicked()
    {
        var item = SelectedItem as AssemblyTreeItem;
        if (item != null)
        {
            VisibilityToggleRequested?.Invoke(this, item.Node);
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

        // Gray out and italicize hidden nodes
        if (!item.Node.IsVisible)
        {
            e.ForegroundColor = Eto.Drawing.Colors.Gray;
            e.Font = new Font(e.Font.Family, e.Font.Size, FontStyle.Italic);
        }
    }

    #endregion
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
        string visibilityIcon = node.IsVisible ? "👁" : "◯";
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
        else if (node is DocumentNode docNode)
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
}
