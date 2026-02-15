using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using RhinoAssemblyOutliner.Model;
using RhinoAssemblyOutliner.Services.PerInstanceVisibility;

namespace RhinoAssemblyOutliner.Services;

/// <summary>
/// Service for managing visibility of block instances in the viewport.
/// Top-level instances use standard Rhino show/hide.
/// Nested components use native C++ per-instance visibility conduit with path-based addressing.
/// </summary>
public class VisibilityService
{
    private readonly uint _docSerialNumber;
    private bool _nativeInitialized;

    public VisibilityService(RhinoDoc doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        _docSerialNumber = doc.RuntimeSerialNumber;
        InitializeNative();
    }

    /// <summary>
    /// Gets the document by serial number. Returns null if document was closed.
    /// </summary>
    private RhinoDoc GetDoc()
    {
        return RhinoDoc.FromRuntimeSerialNumber(_docSerialNumber);
    }

    /// <summary>
    /// Toggles visibility of a node and optionally its children.
    /// </summary>
    /// <param name="node">The node to toggle.</param>
    /// <param name="includeChildren">Whether to include child nodes.</param>
    /// <returns>The new visibility state.</returns>
    public bool ToggleVisibility(AssemblyNode node, bool includeChildren = false)
    {
        if (node is BlockInstanceNode blockNode)
        {
            // Component inside a block → use native per-instance visibility
            if (IsComponentNode(blockNode))
            {
                return ToggleComponentVisibility(blockNode);
            }

            // Top-level instance → use standard Rhino show/hide
            if (blockNode.InstanceId != Guid.Empty)
            {
                var obj = GetDoc()?.Objects.FindId(blockNode.InstanceId);
                if (obj != null)
                {
                    bool newState = !IsVisible(obj);
                    SetVisibility(obj, newState);
                    node.IsVisible = newState;

                    if (includeChildren)
                    {
                        SetChildrenVisibility(node, newState);
                    }

                    GetDoc()?.Views.Redraw();
                    return newState;
                }
            }
        }
        return node.IsVisible;
    }

    /// <summary>
    /// Sets visibility state for a node.
    /// </summary>
    public void SetVisibility(AssemblyNode node, bool visible, bool includeChildren = false)
    {
        if (node is BlockInstanceNode blockNode)
        {
            if (IsComponentNode(blockNode))
            {
                SetComponentVisibility(blockNode, visible);
            }
            else if (blockNode.InstanceId != Guid.Empty)
            {
                var obj = GetDoc()?.Objects.FindId(blockNode.InstanceId);
                if (obj != null)
                {
                    SetVisibility(obj, visible);
                    node.IsVisible = visible;
                }
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
        var doc = GetDoc();
        if (doc == null) return;

        // Hide all top-level block instances
        foreach (var obj in doc.Objects.GetObjectList(ObjectType.InstanceReference))
        {
            SetVisibility(obj, false);
        }

        // Show only the selected node and its ancestors
        ShowNodeAndAncestors(node);

        doc.Views.Redraw();
    }

    /// <summary>
    /// Shows all hidden objects and resets native conduit state.
    /// </summary>
    public void ShowAll()
    {
        var doc = GetDoc();
        if (doc == null) return;

        foreach (var obj in doc.Objects.GetObjectList(ObjectType.InstanceReference))
        {
            if (!IsVisible(obj))
            {
                SetVisibility(obj, true);
            }

            // Also reset any per-instance component visibility
            if (_nativeInitialized)
            {
                var id = obj.Id;
                if (NativeVisibilityInterop.GetHiddenComponentCount(ref id) > 0)
                {
                    NativeVisibilityInterop.ResetComponentVisibility(ref id);
                }
            }
        }
        doc.Views.Redraw();
    }

    /// <summary>
    /// Hides the specified node.
    /// </summary>
    public void Hide(AssemblyNode node, bool includeChildren = true)
    {
        SetVisibility(node, false, includeChildren);
        GetDoc()?.Views.Redraw();
    }

    /// <summary>
    /// Shows the specified node.
    /// </summary>
    public void Show(AssemblyNode node, bool includeChildren = true)
    {
        SetVisibility(node, true, includeChildren);
        GetDoc()?.Views.Redraw();
    }

    #region Native Per-Instance Visibility

    private void InitializeNative()
    {
        if (_nativeInitialized) return;

        if (!NativeVisibilityInterop.IsNativeDllAvailable())
        {
            RhinoApp.WriteLine("AssemblyOutliner: Native DLL not found — per-instance component visibility disabled.");
            return;
        }

        if (NativeVisibilityInterop.NativeInit())
        {
            _nativeInitialized = true;
            RhinoApp.WriteLine($"AssemblyOutliner: Native visibility module v{NativeVisibilityInterop.GetNativeVersion()} loaded.");
        }
        else
        {
            RhinoApp.WriteLine("AssemblyOutliner: NativeInit() failed.");
        }
    }

    /// <summary>
    /// Check if a node is a component inside a block (has ComponentIndex and a parent instance).
    /// </summary>
    private bool IsComponentNode(BlockInstanceNode node)
    {
        return node.ComponentIndex >= 0 && node.Parent is BlockInstanceNode;
    }

    /// <summary>
    /// Resolves the path from a component node up to the top-level document instance.
    /// Returns (topLevelGuid, componentPath) where componentPath is e.g. "1.0.2".
    /// Walks up the tree collecting ComponentIndex values, then reverses to build the path.
    /// </summary>
    private (Guid topLevelId, string componentPath) ResolveComponentPath(BlockInstanceNode node)
    {
        var indices = new List<int>();
        var current = node;

        // Walk up collecting ComponentIndex values until we reach a top-level node
        while (current != null && current.ComponentIndex >= 0)
        {
            indices.Add(current.ComponentIndex);
            current = current.Parent as BlockInstanceNode;
        }

        // current is now the top-level document instance (ComponentIndex == -1)
        if (current == null || current.InstanceId == Guid.Empty)
            return (Guid.Empty, string.Empty);

        // Reverse: we collected bottom-up, path is top-down
        indices.Reverse();
        string path = string.Join(".", indices);

        return (current.InstanceId, path);
    }

    private bool ToggleComponentVisibility(BlockInstanceNode blockNode)
    {
        if (!_nativeInitialized) return blockNode.IsVisible;

        var (topLevelId, componentPath) = ResolveComponentPath(blockNode);
        if (topLevelId == Guid.Empty || string.IsNullOrEmpty(componentPath))
            return blockNode.IsVisible;

        bool currentlyVisible = NativeVisibilityInterop.IsComponentVisible(ref topLevelId, componentPath);
        bool newVisible = !currentlyVisible;
        NativeVisibilityInterop.SetComponentVisibility(ref topLevelId, componentPath, newVisible);
        blockNode.IsVisible = newVisible;

        GetDoc()?.Views.Redraw();
        return newVisible;
    }

    private void SetComponentVisibility(BlockInstanceNode blockNode, bool visible)
    {
        if (!_nativeInitialized) return;

        var (topLevelId, componentPath) = ResolveComponentPath(blockNode);
        if (topLevelId == Guid.Empty || string.IsNullOrEmpty(componentPath))
            return;

        NativeVisibilityInterop.SetComponentVisibility(ref topLevelId, componentPath, visible);
        blockNode.IsVisible = visible;
    }

    #endregion

    #region Private Helpers

    private bool IsVisible(RhinoObject obj)
    {
        return obj.Visible;
    }

    private void SetVisibility(RhinoObject obj, bool visible)
    {
        if (visible)
        {
            GetDoc()?.Objects.Show(obj.Id, ignoreLayerMode: false);
        }
        else
        {
            GetDoc()?.Objects.Hide(obj.Id, ignoreLayerMode: false);
        }
    }

    private void SetChildrenVisibility(AssemblyNode node, bool visible)
    {
        foreach (var child in node.Children)
        {
            if (child is BlockInstanceNode blockChild)
            {
                if (IsComponentNode(blockChild))
                {
                    SetComponentVisibility(blockChild, visible);
                }
                else if (blockChild.InstanceId != Guid.Empty)
                {
                    var obj = GetDoc()?.Objects.FindId(blockChild.InstanceId);
                    if (obj != null)
                    {
                        SetVisibility(obj, visible);
                        child.IsVisible = visible;
                    }
                }
            }
            SetChildrenVisibility(child, visible);
        }
    }

    private void ShowNodeAndAncestors(AssemblyNode node)
    {
        if (node is BlockInstanceNode blockNode && blockNode.InstanceId != Guid.Empty)
        {
            var obj = GetDoc()?.Objects.FindId(blockNode.InstanceId);
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
                var obj = GetDoc()?.Objects.FindId(parentBlock.InstanceId);
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
