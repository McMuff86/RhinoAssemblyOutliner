using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using RhinoAssemblyOutliner.Model;

namespace RhinoAssemblyOutliner.Services;

/// <summary>
/// Service for managing visibility of block instances in the viewport.
/// </summary>
public class VisibilityService
{
    private readonly RhinoDoc _doc;

    public VisibilityService(RhinoDoc doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Toggles visibility of a node and optionally its children.
    /// </summary>
    /// <param name="node">The node to toggle.</param>
    /// <param name="includeChildren">Whether to include child nodes.</param>
    /// <returns>The new visibility state.</returns>
    public bool ToggleVisibility(AssemblyNode node, bool includeChildren = false)
    {
        if (node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty)
        {
            var obj = _doc.Objects.FindId(blockNode.InstanceId);
            if (obj != null)
            {
                bool newState = !IsVisible(obj);
                SetVisibility(obj, newState);
                node.IsVisible = newState;

                if (includeChildren)
                {
                    SetChildrenVisibility(node, newState);
                }

                _doc.Views.Redraw();
                return newState;
            }
        }
        return node.IsVisible;
    }

    /// <summary>
    /// Sets visibility state for a node.
    /// </summary>
    public void SetVisibility(AssemblyNode node, bool visible, bool includeChildren = false)
    {
        if (node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty)
        {
            var obj = _doc.Objects.FindId(blockNode.InstanceId);
            if (obj != null)
            {
                SetVisibility(obj, visible);
                node.IsVisible = visible;
            }
        }

        if (includeChildren)
        {
            SetChildrenVisibility(node, visible);
        }
    }

    /// <summary>
    /// Shows only the specified node, hiding all others.
    /// </summary>
    public void Isolate(AssemblyNode node)
    {
        // Hide all top-level block instances
        foreach (var obj in _doc.Objects.GetObjectList(ObjectType.InstanceReference))
        {
            SetVisibility(obj, false);
        }

        // Show only the selected node and its ancestors
        ShowNodeAndAncestors(node);

        _doc.Views.Redraw();
    }

    /// <summary>
    /// Shows all hidden objects.
    /// </summary>
    public void ShowAll()
    {
        foreach (var obj in _doc.Objects.GetObjectList(ObjectType.InstanceReference))
        {
            if (!IsVisible(obj))
            {
                SetVisibility(obj, true);
            }
        }
        _doc.Views.Redraw();
    }

    /// <summary>
    /// Hides the specified node.
    /// </summary>
    public void Hide(AssemblyNode node, bool includeChildren = true)
    {
        SetVisibility(node, false, includeChildren);
        _doc.Views.Redraw();
    }

    /// <summary>
    /// Shows the specified node.
    /// </summary>
    public void Show(AssemblyNode node, bool includeChildren = true)
    {
        SetVisibility(node, true, includeChildren);
        _doc.Views.Redraw();
    }

    #region Private Helpers

    private bool IsVisible(RhinoObject obj)
    {
        return obj.Visible;
    }

    private void SetVisibility(RhinoObject obj, bool visible)
    {
        if (visible)
        {
            _doc.Objects.Show(obj.Id, ignoreLayerMode: false);
        }
        else
        {
            _doc.Objects.Hide(obj.Id, ignoreLayerMode: false);
        }
    }

    private void SetChildrenVisibility(AssemblyNode node, bool visible)
    {
        foreach (var child in node.Children)
        {
            if (child is BlockInstanceNode blockChild && blockChild.InstanceId != Guid.Empty)
            {
                var obj = _doc.Objects.FindId(blockChild.InstanceId);
                if (obj != null)
                {
                    SetVisibility(obj, visible);
                    child.IsVisible = visible;
                }
            }
            SetChildrenVisibility(child, visible);
        }
    }

    private void ShowNodeAndAncestors(AssemblyNode node)
    {
        if (node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty)
        {
            var obj = _doc.Objects.FindId(blockNode.InstanceId);
            if (obj != null)
            {
                SetVisibility(obj, true);
                node.IsVisible = true;
            }
        }

        // Show parent chain
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is BlockInstanceNode parentBlock && parentBlock.InstanceId != Guid.Empty)
            {
                var obj = _doc.Objects.FindId(parentBlock.InstanceId);
                if (obj != null)
                {
                    SetVisibility(obj, true);
                    parent.IsVisible = true;
                }
            }
            parent = parent.Parent;
        }
    }

    #endregion
}
