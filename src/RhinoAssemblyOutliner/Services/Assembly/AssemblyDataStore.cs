using System;
using System.Linq;
using System.Runtime.InteropServices;
using Rhino;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// High-level persistence store for per-instance assembly metadata.
/// </summary>
public sealed class AssemblyDataStore : IAssemblyDataStore
{
    private readonly IAssemblyDataNativeApi _api;
    private bool _disabled;

    /// <summary>
    /// Creates a store backed by the native assembly-data API.
    /// </summary>
    public AssemblyDataStore()
        : this(new NativeAssemblyDataApi())
    {
    }

    /// <summary>
    /// Creates a store with an explicit native API adapter.
    /// </summary>
    public AssemblyDataStore(IAssemblyDataNativeApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <inheritdoc />
    public bool IsAvailable => !_disabled && _api.IsAvailable;

    /// <inheritdoc />
    public bool Has(Guid instanceId)
    {
        return TryNative(() => _api.HasAssemblyData(instanceId), false);
    }

    /// <inheritdoc />
    public bool Attach(
        Guid instanceId,
        Guid sourceDefinitionId,
        string sourceDefinitionName,
        VisibilityState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var hiddenIndices = state.HiddenIndices.ToArray();
        return TryNative(
            () => _api.AttachAssemblyData(
                instanceId,
                sourceDefinitionId,
                sourceDefinitionName ?? string.Empty,
                hiddenIndices,
                state.ComponentCount),
            false);
    }

    /// <inheritdoc />
    public bool Remove(Guid instanceId)
    {
        return TryNative(() => _api.RemoveAssemblyData(instanceId), false);
    }

    /// <inheritdoc />
    public Guid? GetSourceDefinitionId(Guid instanceId)
    {
        return TryNative(() => _api.GetSourceDefinitionId(instanceId), null);
    }

    /// <inheritdoc />
    public string? GetSourceDefinitionName(Guid instanceId)
    {
        return TryNative(() => _api.GetSourceDefinitionName(instanceId), null);
    }

    /// <inheritdoc />
    public VisibilityState? GetVisibilityState(Guid instanceId)
    {
        return TryNative(() =>
        {
            var componentCount = _api.GetComponentCount(instanceId);
            if (componentCount < 0)
                return null;

            var hiddenIndices = _api.GetHiddenComponentIndices(instanceId, componentCount);
            return hiddenIndices == null
                ? null
                : VisibilityState.Create(hiddenIndices, componentCount);
        }, null);
    }

    private T TryNative<T>(Func<T> action, T fallback)
    {
        if (!IsAvailable)
            return fallback;

        try
        {
            return action();
        }
        catch (Exception ex) when (IsNativeFailure(ex))
        {
            _disabled = true;
            SafeLog($"AssemblyOutliner: native assembly data store disabled: {ex.Message}");
            return fallback;
        }
    }

    private static bool IsNativeFailure(Exception ex)
    {
        return ex is DllNotFoundException
            or EntryPointNotFoundException
            or BadImageFormatException
            or SEHException;
    }

    private static void SafeLog(string message)
    {
        try
        {
            RhinoApp.WriteLine(message);
        }
        catch
        {
            // Unit tests may run without a live Rhino application.
        }
    }
}
