using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Rhino;
using Rhino.DocObjects;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Finds and removes orphaned variant definitions (__aov_*) that are no longer
/// referenced by any instance object in the document.
/// Uses a 5-second debounce timer to avoid premature cleanup (Undo safety).
/// </summary>
public sealed class VariantGarbageCollector : IVariantGarbageCollector, IDisposable
{
    /// <summary>Prefix used by all variant definitions.</summary>
    internal const string VariantPrefix = "__aov_";

    /// <summary>Delay in milliseconds before garbage collection runs.</summary>
    private const double DelayMs = 5000;

    private readonly object _lock = new();
    private Timer? _timer;
    private uint _pendingDocSerialNumber;
    private bool _disposed;

    /// <summary>
    /// Schedule a delayed garbage collection for the given document.
    /// Resets the timer on each call (debounce).
    /// </summary>
    public void ScheduleCollection(RhinoDoc doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));

        lock (_lock)
        {
            if (_disposed) return;

            _pendingDocSerialNumber = doc.RuntimeSerialNumber;

            if (_timer == null)
            {
                _timer = new Timer(DelayMs) { AutoReset = false };
                _timer.Elapsed += OnTimerElapsed;
            }

            // Reset the timer (debounce)
            _timer.Stop();
            _timer.Start();
        }
    }

    /// <summary>
    /// Immediately find and delete all orphaned variant definitions.
    /// </summary>
    /// <returns>Number of definitions deleted.</returns>
    public int CollectNow(RhinoDoc doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));

        var orphans = CollectOrphans(doc);
        if (orphans.Count == 0) return 0;

        foreach (var defIndex in orphans)
        {
            doc.InstanceDefinitions.Delete(defIndex, true, false);
        }

        RhinoApp.WriteLine($"AssemblyOutliner: Cleaned up {orphans.Count} orphan variant definition(s).");
        return orphans.Count;
    }

    /// <summary>
    /// Find all orphaned variant definition indices.
    /// A variant is orphaned when no InstanceObject in the document references it.
    /// </summary>
    internal List<int> CollectOrphans(RhinoDoc doc)
    {
        // Collect all definition indices that are referenced by at least one instance
        var referencedDefIndices = new HashSet<int>();
        foreach (var obj in doc.Objects.GetObjectList(ObjectType.InstanceReference))
        {
            if (obj is InstanceObject inst && inst.InstanceDefinition != null)
            {
                referencedDefIndices.Add(inst.InstanceDefinition.Index);
            }
        }

        var orphanIndices = new List<int>();

        // Check all definitions: if it's a variant and not referenced, it's an orphan
        for (int i = 0; i < doc.InstanceDefinitions.ActiveCount + doc.InstanceDefinitions.Count; i++)
        {
            var def = doc.InstanceDefinitions[i];
            if (def == null || def.IsDeleted) continue;
            if (!IsVariantDefinition(def.Name)) continue;

            if (!referencedDefIndices.Contains(def.Index))
            {
                orphanIndices.Add(def.Index);
            }
        }

        return orphanIndices;
    }

    /// <summary>
    /// Check whether a definition name matches the variant naming convention.
    /// Format: {OriginalName}__aov_{hash8}
    /// </summary>
    internal static bool IsVariantDefinition(string? name)
    {
        return name != null && name.Contains(VariantPrefix);
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        uint docSn;
        lock (_lock)
        {
            docSn = _pendingDocSerialNumber;
        }

        // Marshal to UI thread — Rhino doc access must happen on the main thread
        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            try
            {
                var doc = RhinoDoc.FromRuntimeSerialNumber(docSn);
                if (doc != null)
                {
                    CollectNow(doc);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: GC error: {ex.Message}");
            }
        }));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
