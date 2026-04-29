using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using RhinoAssemblyOutliner.Model;
using RhinoAssemblyOutliner.Services.Assembly;
using RhinoAssemblyOutliner.Services.PerInstanceVisibility;

namespace RhinoAssemblyOutliner.Services;

/// <summary>
/// Service for managing visibility of block instances in the viewport.
///
/// Visibility paths (current architecture, v3 — Definition Cloning):
///   * Top-level <see cref="BlockInstanceNode"/> → standard Rhino Objects.Hide/Show
///   * <see cref="ComponentNode"/> (non-block geometry inside a definition) → <see cref="IVariantManager"/>
///
/// Legacy path (Sprint 2, Pre-v3 — to be removed when nested-block cloning lands in Sprint 4+):
///   * Nested <see cref="BlockInstanceNode"/> with <c>ComponentIndex &gt;= 0</c>
///     → C++ DisplayConduit via <see cref="PerInstanceVisibility.NativeVisibilityInterop"/>.
///   The native conduit had known issues (ghost artifacts, missing selection highlights,
///   display-cache bypass). Definition Cloning supersedes it; this branch stays only as a
///   fallback for nested blocks until the recursive cloning strategy is implemented.
/// </summary>
public class VisibilityService
{
    private readonly uint _docSerialNumber;
    private bool _nativeInitialized;
    private readonly IVariantManager _variantManager;

    /// <summary>
    /// Raised after a variant reassignment so the UI can refresh.
    /// </summary>
    public event Action TreeRefreshNeeded;

    public VisibilityService(RhinoDoc doc, IVariantManager? variantManager = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        _docSerialNumber = doc.RuntimeSerialNumber;
        _variantManager = variantManager ?? new VariantManager();
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
        var doc = GetDoc();
        if (doc == null) return node.IsVisible;

        // ComponentNode: toggle via VariantManager (definition cloning)
        if (node is ComponentNode compNode)
        {
            return ToggleComponentNodeVisibility(compNode, doc);
        }

        string desc = node.IsVisible
            ? $"Hide {node.DisplayName}"
            : $"Show {node.DisplayName}";

        using (UndoHelper.CreateScope(doc, desc))
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
                    var obj = doc.Objects.FindId(blockNode.InstanceId);
                    if (obj != null)
                    {
                        bool newState = !IsVisible(obj);
                        SetVisibility(obj, newState);
                        node.IsVisible = newState;

                        if (includeChildren)
                        {
                            SetChildrenVisibility(node, newState);
                        }

                        doc.Views.Redraw();
                        return newState;
                    }
                }
            }
        }
        return node.IsVisible;
    }

    /// <summary>
    /// Sets visibility state for a node (wraps in undo record).
    /// </summary>
    public void SetVisibility(AssemblyNode node, bool visible, bool includeChildren = false)
    {
        var doc = GetDoc();
        if (doc == null) return;

        string desc = visible ? $"Show {node.DisplayName}" : $"Hide {node.DisplayName}";
        using (UndoHelper.CreateScope(doc, desc))
        {
            SetVisibilityInternal(node, visible, includeChildren);
        }
    }

    /// <summary>
    /// Internal: sets visibility without creating its own undo record.
    /// Caller is responsible for undo scoping.
    /// </summary>
    private void SetVisibilityInternal(AssemblyNode node, bool visible, bool includeChildren = false)
    {
        if (node is ComponentNode compNode)
        {
            var doc = GetDoc();
            if (doc != null)
                ApplyComponentVisibility(compNode, visible, doc);
            return;
        }

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

        using (UndoHelper.CreateScope(doc, $"Isolate {node.DisplayName}"))
        {
            // Hide all top-level block instances
            foreach (var obj in doc.Objects.GetObjectList(ObjectType.InstanceReference))
            {
                SetVisibility(obj, false);
            }

            // Show only the selected node and its ancestors
            ShowNodeAndAncestors(node);

            doc.Views.Redraw();
        }
    }

    /// <summary>
    /// Shows all hidden objects and resets native conduit state.
    /// </summary>
    public void ShowAll()
    {
        var doc = GetDoc();
        if (doc == null) return;

        using (UndoHelper.CreateScope(doc, "Show All"))
        {
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
    }

    /// <summary>
    /// Hides the specified node.
    /// </summary>
    public void Hide(AssemblyNode node, bool includeChildren = true)
    {
        var doc = GetDoc();
        if (doc == null) return;

        using (UndoHelper.CreateScope(doc, $"Hide {node.DisplayName}"))
        {
            SetVisibilityInternal(node, false, includeChildren);
            doc.Views.Redraw();
        }
    }

    /// <summary>
    /// Shows the specified node.
    /// </summary>
    public void Show(AssemblyNode node, bool includeChildren = true)
    {
        var doc = GetDoc();
        if (doc == null) return;

        using (UndoHelper.CreateScope(doc, $"Show {node.DisplayName}"))
        {
            SetVisibilityInternal(node, true, includeChildren);
            doc.Views.Redraw();
        }
    }

    #region Component Node Visibility (via VariantManager / Definition Cloning)

    /// <summary>
    /// Toggles visibility of a ComponentNode via the VariantManager.
    /// Builds a VisibilityState from all sibling components and reassigns the owning instance.
    /// </summary>
    private bool ToggleComponentNodeVisibility(ComponentNode compNode, RhinoDoc doc)
    {
        bool newVisible = !compNode.IsVisible;
        
        using (UndoHelper.CreateScope(doc, newVisible ? $"Show {compNode.DisplayName}" : $"Hide {compNode.DisplayName}"))
        {
            ApplyComponentVisibility(compNode, newVisible, doc);
        }

        return newVisible;
    }

    /// <summary>
    /// Applies visibility change on a ComponentNode by building the full VisibilityState
    /// from all sibling components and calling VariantManager.ReassignInstance().
    /// </summary>
    private void ApplyComponentVisibility(ComponentNode compNode, bool visible, RhinoDoc doc)
    {
        // Update the node state first
        compNode.IsVisible = visible;

        // Find the parent BlockInstanceNode
        var parentBlock = compNode.Parent as BlockInstanceNode;
        if (parentBlock == null) return;

        // Find the source definition
        var instanceObj = doc.Objects.FindId(compNode.OwnerInstanceId) as InstanceObject;
        if (instanceObj == null) return;

        var currentDefId = instanceObj.InstanceDefinition.Id;
        var sourceDefId = _variantManager.GetSourceDefinitionId(doc, currentDefId) ?? currentDefId;
        var sourceDef = doc.InstanceDefinitions.FindId(sourceDefId);
        if (sourceDef == null) return;

        // Build VisibilityState from all sibling ComponentNodes
        var allComponents = parentBlock.Children;
        int componentCount = sourceDef.GetObjects().Length;
        var hiddenIndices = new List<int>();

        foreach (var child in allComponents)
        {
            if (child is ComponentNode cn && !cn.IsVisible)
            {
                hiddenIndices.Add(cn.ComponentIndex);
            }
        }

        var state = VisibilityState.Create(hiddenIndices, componentCount);

        // Reassign via VariantManager
        _variantManager.ReassignInstance(doc, compNode.OwnerInstanceId, state);

        // Update parent mixed state
        UpdateParentVisibilityState(parentBlock);

        doc.Views.Redraw();
        TreeRefreshNeeded?.Invoke();
    }

    /// <summary>
    /// Updates the parent node's IsVisible based on children (for mixed state display).
    /// If all children are visible → parent visible. If all hidden → parent hidden.
    /// Mixed → parent stays visible (the UI shows ◐ based on children check).
    /// </summary>
    private void UpdateParentVisibilityState(AssemblyNode parent)
    {
        if (parent.Children.Count == 0) return;

        bool allVisible = parent.Children.All(c => c.IsVisible);
        bool allHidden = parent.Children.All(c => !c.IsVisible);

        if (allHidden)
            parent.IsVisible = false;
        else
            parent.IsVisible = true;
    }

    #endregion

    #region Legacy: Native Per-Instance Visibility (DisplayConduit, Pre-v3)

    // The methods in this region drive the C++ DisplayConduit for nested block instances.
    // Definition Cloning (VariantManager) will replace this when recursive cloning ships
    // in Sprint 4+. Do not extend this code path — add new visibility logic via VariantManager.

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
