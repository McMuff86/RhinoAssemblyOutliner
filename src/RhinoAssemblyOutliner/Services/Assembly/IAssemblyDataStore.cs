using System;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Stores persisted assembly metadata for block instances.
/// </summary>
public interface IAssemblyDataStore
{
    /// <summary>
    /// Gets whether the backing persistence layer is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns true when persisted assembly data is attached to the instance.
    /// </summary>
    bool Has(Guid instanceId);

    /// <summary>
    /// Attaches persisted assembly data to the instance.
    /// </summary>
    bool Attach(Guid instanceId, Guid sourceDefinitionId, string sourceDefinitionName, VisibilityState state);

    /// <summary>
    /// Removes persisted assembly data from the instance.
    /// </summary>
    bool Remove(Guid instanceId);

    /// <summary>
    /// Gets the persisted source definition id for the instance.
    /// </summary>
    Guid? GetSourceDefinitionId(Guid instanceId);

    /// <summary>
    /// Gets the persisted source definition name for the instance.
    /// </summary>
    string? GetSourceDefinitionName(Guid instanceId);

    /// <summary>
    /// Gets the persisted visibility state for the instance.
    /// </summary>
    VisibilityState? GetVisibilityState(Guid instanceId);
}
