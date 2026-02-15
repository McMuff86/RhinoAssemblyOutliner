using System;
using System.Collections.Generic;
using Rhino;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Wraps visibility changes in Rhino undo records so that
/// Ctrl+Z / Ctrl+Y works correctly for all assembly operations.
/// 
/// <para>
/// Rhino automatically tracks document changes (Objects.Hide, Objects.Show,
/// Objects.Replace, InstanceDefinitions.Add) inside an undo record.
/// For native conduit state (private plugin data), we use AddCustomUndoEvent.
/// </para>
/// 
/// <para>Usage (IDisposable pattern):</para>
/// <code>
/// using (UndoHelper.CreateScope(doc, "Hide Sphere in Motor_v1 #1"))
/// {
///     visibilityService.Hide(node);
/// }
/// </code>
/// </summary>
public static class UndoHelper
{
    /// <summary>
    /// Begins a named undo record. All document changes until EndRecord
    /// are grouped into a single Ctrl+Z step.
    /// </summary>
    /// <returns>The undo record serial number (0 if failed).</returns>
    public static uint BeginRecord(RhinoDoc doc, string description)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (string.IsNullOrEmpty(description)) description = "Assembly Outliner";
        return doc.BeginUndoRecord(description);
    }

    /// <summary>
    /// Ends an undo record previously started with BeginRecord.
    /// </summary>
    public static bool EndRecord(RhinoDoc doc, uint recordId)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (recordId == 0) return false;
        return doc.EndUndoRecord(recordId);
    }

    /// <summary>
    /// Creates a disposable undo scope. The record is automatically
    /// ended when the scope is disposed.
    /// </summary>
    public static IDisposable CreateScope(RhinoDoc doc, string description)
    {
        return new UndoScope(doc, description);
    }

    /// <summary>
    /// Adds a custom undo event for native conduit visibility state.
    /// When Rhino undoes this, the callback restores the previous state.
    /// 
    /// <para>IMPORTANT: The callback must NOT change any Rhino document state.
    /// It should only restore private plugin data (native conduit state).</para>
    /// </summary>
    public static void AddNativeStateUndoEvent(
        RhinoDoc doc,
        string description,
        Dictionary<Guid, List<string>> previousHiddenPaths)
    {
        if (doc == null || previousHiddenPaths == null) return;

        var snapshot = new NativeVisibilitySnapshot(previousHiddenPaths);
        doc.AddCustomUndoEvent(description, OnUndoNativeState, snapshot);
    }

    private static void OnUndoNativeState(object sender, Rhino.Commands.CustomUndoEventArgs e)
    {
        // Save current state for redo
        var currentSnapshot = CaptureCurrentNativeState(e.Document);
        e.Document.AddCustomUndoEvent("Assembly Outliner Redo", OnUndoNativeState, currentSnapshot);

        // Restore previous state
        if (e.Tag is NativeVisibilitySnapshot snapshot)
        {
            RestoreNativeState(e.Document, snapshot);
        }
    }

    private static NativeVisibilitySnapshot CaptureCurrentNativeState(RhinoDoc doc)
    {
        // Capture current native conduit state for all instances
        var state = new Dictionary<Guid, List<string>>();

        foreach (var obj in doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.InstanceReference))
        {
            var id = obj.Id;
            int count = PerInstanceVisibility.NativeVisibilityInterop.GetHiddenComponentCount(ref id);
            if (count > 0)
            {
                // We store the instance ID as marker; actual paths are managed by native side
                state[id] = new List<string> { $"__count:{count}" };
            }
        }

        return new NativeVisibilitySnapshot(state);
    }

    private static void RestoreNativeState(RhinoDoc doc, NativeVisibilitySnapshot snapshot)
    {
        // Reset all native visibility, then re-apply snapshot
        foreach (var obj in doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.InstanceReference))
        {
            var id = obj.Id;
            PerInstanceVisibility.NativeVisibilityInterop.ResetComponentVisibility(ref id);
        }

        foreach (var kvp in snapshot.HiddenPaths)
        {
            var instanceId = kvp.Key;
            foreach (var path in kvp.Value)
            {
                if (path.StartsWith("__count:")) continue;
                PerInstanceVisibility.NativeVisibilityInterop.SetComponentVisibility(
                    ref instanceId, path, false);
            }
        }

        doc.Views.Redraw();
    }

    #region Nested Types

    /// <summary>
    /// Snapshot of native conduit visibility state for undo/redo.
    /// </summary>
    private class NativeVisibilitySnapshot
    {
        public Dictionary<Guid, List<string>> HiddenPaths { get; }

        public NativeVisibilitySnapshot(Dictionary<Guid, List<string>> hiddenPaths)
        {
            // Deep copy
            HiddenPaths = new Dictionary<Guid, List<string>>();
            foreach (var kvp in hiddenPaths)
            {
                HiddenPaths[kvp.Key] = new List<string>(kvp.Value);
            }
        }
    }

    /// <summary>
    /// IDisposable wrapper around BeginUndoRecord / EndUndoRecord.
    /// </summary>
    private sealed class UndoScope : IDisposable
    {
        private readonly RhinoDoc _doc;
        private readonly uint _recordId;
        private bool _disposed;

        public UndoScope(RhinoDoc doc, string description)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _recordId = doc.BeginUndoRecord(description ?? "Assembly Outliner");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_recordId != 0)
            {
                _doc.EndUndoRecord(_recordId);
            }
        }
    }

    #endregion
}
