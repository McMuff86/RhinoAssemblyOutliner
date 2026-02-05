using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using RhinoAssemblyOutliner.Model;

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
    public event EventHandler<AssemblyNode> SelectionChanged;

    /// <summary>
    /// Raised when a node is activated (double-clicked).
    /// </summary>
    public event EventHandler<AssemblyNode> NodeActivated;

    public AssemblyTreeView()
    {
        _itemLookup = new Dictionary<Guid, AssemblyTreeItem>();
        
        // Configure tree
        AllowMultipleSelection = false;
        ShowHeader = true;
        Border = BorderType.None;

        // Define columns
        Columns.Add(new GridColumn
        {
            HeaderText = "Name",
            DataCell = new TextBoxCell(0),
            AutoSize = true,
            Editable = false
        });

        Columns.Add(new GridColumn
        {
            HeaderText = "Layer",
            DataCell = new TextBoxCell(1),
            Width = 120
        });

        Columns.Add(new GridColumn
        {
            HeaderText = "Type",
            DataCell = new TextBoxCell(2),
            Width = 80
        });

        // Wire up events
        SelectedItemChanged += OnSelectedItemChanged;
        Activated += OnActivated;
        CellFormatting += OnCellFormatting;
    }

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
    /// </summary>
    public void SelectNodeByObjectId(Guid objectId)
    {
        // Find the node
        var blockNode = AssemblyTreeBuilder.FindNodeByObjectId(_rootNode, objectId);
        if (blockNode == null) return;

        // Find the tree item
        if (_itemLookup.TryGetValue(blockNode.Id, out var item))
        {
            // Expand parents
            ExpandToItem(item);

            // Select the item
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

        // Example: Gray out hidden nodes
        if (!item.Node.IsVisible)
        {
            e.ForegroundColor = Eto.Drawing.Colors.Gray;
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
        string layerName = "";
        string typeName = "";

        if (node is BlockInstanceNode blockNode)
        {
            layerName = blockNode.Layer?.FullPath ?? "";
            typeName = blockNode.LinkType.ToString();
        }
        else if (node is DocumentNode docNode)
        {
            typeName = "Document";
        }

        Values = new object[]
        {
            node.DisplayName,
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
