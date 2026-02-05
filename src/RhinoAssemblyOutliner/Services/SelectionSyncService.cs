using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using RhinoAssemblyOutliner.Model;

namespace RhinoAssemblyOutliner.Services;

/// <summary>
/// Service for synchronizing selection between the tree view and Rhino viewport.
/// Handles bidirectional sync with loop prevention.
/// </summary>
public class SelectionSyncService
{
    private readonly RhinoDoc _doc;
    private bool _isSyncing;

    /// <summary>
    /// Raised when viewport selection changes.
    /// </summary>
    public event EventHandler<IEnumerable<Guid>> ViewportSelectionChanged;

    public SelectionSyncService(RhinoDoc doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Gets whether a sync operation is currently in progress.
    /// </summary>
    public bool IsSyncing => _isSyncing;

    /// <summary>
    /// Selects objects in the viewport based on tree selection.
    /// </summary>
    /// <param name="nodes">Nodes selected in the tree.</param>
    public void SyncToViewport(IEnumerable<AssemblyNode> nodes)
    {
        if (_isSyncing) return;

        _isSyncing = true;
        try
        {
            _doc.Objects.UnselectAll();

            var ids = nodes
                .OfType<BlockInstanceNode>()
                .Where(n => n.InstanceId != Guid.Empty)
                .Select(n => n.InstanceId)
                .ToList();

            foreach (var id in ids)
            {
                _doc.Objects.Select(id, select: true);
            }

            _doc.Views.Redraw();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Selects a single object in the viewport.
    /// </summary>
    public void SelectInViewport(Guid objectId, bool addToSelection = false)
    {
        if (_isSyncing) return;

        _isSyncing = true;
        try
        {
            if (!addToSelection)
            {
                _doc.Objects.UnselectAll();
            }

            _doc.Objects.Select(objectId, select: true);
            _doc.Views.Redraw();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Gets currently selected block instance IDs from the viewport.
    /// </summary>
    public IEnumerable<Guid> GetSelectedBlockInstanceIds()
    {
        return _doc.Objects
            .GetSelectedObjects(includeLights: false, includeGrips: false)
            .OfType<InstanceObject>()
            .Select(obj => obj.Id);
    }

    /// <summary>
    /// Selects all instances of a block definition.
    /// </summary>
    public void SelectAllInstancesOfDefinition(int definitionIndex)
    {
        if (_isSyncing) return;

        _isSyncing = true;
        try
        {
            _doc.Objects.UnselectAll();

            var definition = _doc.InstanceDefinitions[definitionIndex];
            if (definition != null && !definition.IsDeleted)
            {
                var instances = definition.GetReferences(1); // 1 = active document
                foreach (var instance in instances)
                {
                    _doc.Objects.Select(instance.Id, select: true);
                }
            }

            _doc.Views.Redraw();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Zooms the viewport to fit the specified objects.
    /// </summary>
    public void ZoomToObjects(IEnumerable<Guid> objectIds)
    {
        var bbox = Rhino.Geometry.BoundingBox.Empty;

        foreach (var id in objectIds)
        {
            var obj = _doc.Objects.FindId(id);
            if (obj?.Geometry != null)
            {
                bbox.Union(obj.Geometry.GetBoundingBox(true));
            }
        }

        if (bbox.IsValid)
        {
            var view = _doc.Views.ActiveView;
            view?.ActiveViewport.ZoomBoundingBox(bbox);
            _doc.Views.Redraw();
        }
    }

    /// <summary>
    /// Notifies about viewport selection change.
    /// Call this from Rhino selection events.
    /// </summary>
    public void OnViewportSelectionChanged(IEnumerable<RhinoObject> selectedObjects)
    {
        if (_isSyncing) return;

        var blockIds = selectedObjects
            .OfType<InstanceObject>()
            .Select(obj => obj.Id);

        ViewportSelectionChanged?.Invoke(this, blockIds);
    }
}
