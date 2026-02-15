using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Handles Rhino document events relevant to the assembly/variant system:
/// - InstanceDefinitionTable changes (BlockEdit → refresh variants)
/// - Object deletion (→ trigger GC for orphaned variants)
/// - Document close (→ clear caches)
/// 
/// All handlers are instance methods (no static event handlers).
/// Subscribe in OnLoad, unsubscribe in OnShutdown.
/// </summary>
public sealed class AssemblyEventHandler : IDisposable
{
    private readonly IVariantGarbageCollector _gc;
    private bool _subscribed;
    private bool _disposed;

    /// <summary>
    /// Fired when a source definition has been modified (e.g. after BlockEdit)
    /// and all its variant definitions need refreshing.
    /// </summary>
    public event EventHandler<VariantRefreshEventArgs>? VariantRefreshRequired;

    /// <summary>
    /// Fired when caches should be cleared (document close).
    /// </summary>
    public event EventHandler? CacheClearRequired;

    public AssemblyEventHandler(IVariantGarbageCollector gc)
    {
        _gc = gc ?? throw new ArgumentNullException(nameof(gc));
    }

    /// <summary>
    /// Subscribe to all relevant Rhino document events.
    /// Call this from PlugIn.OnLoad().
    /// </summary>
    public void Subscribe()
    {
        if (_subscribed) return;

        RhinoDoc.InstanceDefinitionTableEvent += OnInstanceDefinitionTableEvent;
        RhinoDoc.DeleteRhinoObject += OnDeleteRhinoObject;
        RhinoDoc.CloseDocument += OnCloseDocument;
        RhinoDoc.EndOpenDocument += OnEndOpenDocument;

        _subscribed = true;
        RhinoApp.WriteLine("AssemblyOutliner: Event handlers subscribed.");
    }

    /// <summary>
    /// Unsubscribe from all Rhino document events.
    /// Call this from PlugIn.OnShutdown().
    /// </summary>
    public void Unsubscribe()
    {
        if (!_subscribed) return;

        RhinoDoc.InstanceDefinitionTableEvent -= OnInstanceDefinitionTableEvent;
        RhinoDoc.DeleteRhinoObject -= OnDeleteRhinoObject;
        RhinoDoc.CloseDocument -= OnCloseDocument;
        RhinoDoc.EndOpenDocument -= OnEndOpenDocument;

        _subscribed = false;
        RhinoApp.WriteLine("AssemblyOutliner: Event handlers unsubscribed.");
    }

    /// <summary>
    /// Handles InstanceDefinitionTable events.
    /// When a source definition is modified (BlockEdit complete), all its
    /// variant definitions must be refreshed.
    /// </summary>
    private void OnInstanceDefinitionTableEvent(object sender, InstanceDefinitionTableEventArgs e)
    {
        if (e == null) return;

        // We care about Modified events — this fires when BlockEdit completes
        if (e.EventType != InstanceDefinitionTableEventType.Modified)
            return;

        var def = e.NewState;
        if (def == null) return;

        // Skip variant definitions themselves — we only care about source definitions
        if (VariantGarbageCollector.IsVariantDefinition(def.Name))
            return;

        RhinoApp.WriteLine($"AssemblyOutliner: Source definition '{def.Name}' modified — scheduling variant refresh.");

        VariantRefreshRequired?.Invoke(this, new VariantRefreshEventArgs
        {
            Document = e.Document,
            SourceDefinitionId = def.Id,
            SourceDefinitionIndex = def.Index,
            SourceDefinitionName = def.Name
        });

        // Also schedule GC — BlockEdit may have changed component structure
        if (e.Document != null)
        {
            _gc.ScheduleCollection(e.Document);
        }
    }

    /// <summary>
    /// When an instance object is deleted, schedule GC to clean up
    /// any variant definitions that may have become orphaned.
    /// </summary>
    private void OnDeleteRhinoObject(object sender, RhinoObjectEventArgs e)
    {
        if (e?.TheObject is not InstanceObject) return;

        var doc = e.TheObject.Document;
        if (doc != null)
        {
            _gc.ScheduleCollection(doc);
        }
    }

    /// <summary>
    /// On document close, signal that caches should be cleared.
    /// </summary>
    private void OnCloseDocument(object sender, DocumentEventArgs e)
    {
        RhinoApp.WriteLine("AssemblyOutliner: Document closing — clearing caches.");
        CacheClearRequired?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// On document open complete, run GC to clean up any stale variants
    /// that may have been saved without proper references.
    /// </summary>
    private void OnEndOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        if (e.Document != null)
        {
            _gc.ScheduleCollection(e.Document);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unsubscribe();
    }
}

/// <summary>
/// Event args for when variant definitions need to be refreshed
/// after a source definition was modified.
/// </summary>
public class VariantRefreshEventArgs : EventArgs
{
    /// <summary>The document containing the definition.</summary>
    public RhinoDoc? Document { get; init; }

    /// <summary>The GUID of the source (non-variant) definition that was modified.</summary>
    public Guid SourceDefinitionId { get; init; }

    /// <summary>The index of the source definition in the InstanceDefinitionTable.</summary>
    public int SourceDefinitionIndex { get; init; }

    /// <summary>The name of the source definition.</summary>
    public string SourceDefinitionName { get; init; } = "";
}
