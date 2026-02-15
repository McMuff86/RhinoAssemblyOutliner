using Rhino;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Finds and removes orphaned variant definitions (__aov_*) that are no longer
/// referenced by any instance in the document.
/// </summary>
public interface IVariantGarbageCollector
{
    /// <summary>
    /// Schedule a delayed garbage collection (5s debounce to allow for Undo).
    /// </summary>
    void ScheduleCollection(RhinoDoc doc);

    /// <summary>
    /// Immediately collect orphaned variant definitions.
    /// </summary>
    /// <returns>Number of orphan definitions deleted.</returns>
    int CollectNow(RhinoDoc doc);
}
