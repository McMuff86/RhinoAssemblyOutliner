using System;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Thin abstraction over the native assembly-data P/Invoke API.
/// </summary>
public interface IAssemblyDataNativeApi
{
    /// <summary>
    /// Gets whether the native DLL can be found next to the managed plugin.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Attaches assembly data to an instance object.
    /// </summary>
    bool AttachAssemblyData(
        Guid instanceId,
        Guid sourceDefinitionId,
        string sourceDefinitionName,
        int[] hiddenIndices,
        int componentCount);

    /// <summary>
    /// Returns true when assembly data exists on an instance object.
    /// </summary>
    bool HasAssemblyData(Guid instanceId);

    /// <summary>
    /// Removes assembly data from an instance object.
    /// </summary>
    bool RemoveAssemblyData(Guid instanceId);

    /// <summary>
    /// Gets the stored source definition id.
    /// </summary>
    Guid? GetSourceDefinitionId(Guid instanceId);

    /// <summary>
    /// Gets the stored source definition name.
    /// </summary>
    string? GetSourceDefinitionName(Guid instanceId);

    /// <summary>
    /// Gets the stored source component count.
    /// </summary>
    int GetComponentCount(Guid instanceId);

    /// <summary>
    /// Gets the stored hidden component indices.
    /// </summary>
    int[]? GetHiddenComponentIndices(Guid instanceId, int componentCount);
}
