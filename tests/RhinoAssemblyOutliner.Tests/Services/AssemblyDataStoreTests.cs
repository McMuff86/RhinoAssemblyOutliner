using System.Collections.Immutable;
using Xunit;

namespace RhinoAssemblyOutliner.Tests.Services;

public class AssemblyDataStoreTests
{
    private sealed class VisibilityState : IEquatable<VisibilityState>
    {
        public ImmutableSortedSet<int> HiddenIndices { get; }
        public int ComponentCount { get; }

        private VisibilityState(ImmutableSortedSet<int> hiddenIndices, int componentCount)
        {
            HiddenIndices = hiddenIndices;
            ComponentCount = componentCount;
        }

        public static VisibilityState Create(IEnumerable<int> hiddenIndices, int componentCount) =>
            new(hiddenIndices.ToImmutableSortedSet(), componentCount);

        public bool Equals(VisibilityState? other)
        {
            return other != null
                && ComponentCount == other.ComponentCount
                && HiddenIndices.SetEquals(other.HiddenIndices);
        }

        public override bool Equals(object? obj) => Equals(obj as VisibilityState);

        public override int GetHashCode() => HashCode.Combine(ComponentCount, HiddenIndices.Count);
    }

    private interface IAssemblyDataNativeApi
    {
        bool IsAvailable { get; }
        bool AttachAssemblyData(Guid instanceId, Guid sourceDefinitionId, string sourceDefinitionName, int[] hiddenIndices, int componentCount);
        bool HasAssemblyData(Guid instanceId);
        bool RemoveAssemblyData(Guid instanceId);
        Guid? GetSourceDefinitionId(Guid instanceId);
        string? GetSourceDefinitionName(Guid instanceId);
        int GetComponentCount(Guid instanceId);
        int[]? GetHiddenComponentIndices(Guid instanceId, int componentCount);
    }

    private sealed class AssemblyDataStore
    {
        private readonly IAssemblyDataNativeApi _api;
        private bool _disabled;

        public AssemblyDataStore(IAssemblyDataNativeApi api)
        {
            _api = api;
        }

        public bool IsAvailable => !_disabled && _api.IsAvailable;

        public bool Has(Guid instanceId) =>
            TryNative(() => _api.HasAssemblyData(instanceId), false);

        public bool Attach(Guid instanceId, Guid sourceDefinitionId, string sourceDefinitionName, VisibilityState state) =>
            TryNative(
                () => _api.AttachAssemblyData(
                    instanceId,
                    sourceDefinitionId,
                    sourceDefinitionName,
                    state.HiddenIndices.ToArray(),
                    state.ComponentCount),
                false);

        public bool Remove(Guid instanceId) =>
            TryNative(() => _api.RemoveAssemblyData(instanceId), false);

        public Guid? GetSourceDefinitionId(Guid instanceId) =>
            TryNative(() => _api.GetSourceDefinitionId(instanceId), null);

        public string? GetSourceDefinitionName(Guid instanceId) =>
            TryNative(() => _api.GetSourceDefinitionName(instanceId), null);

        public VisibilityState? GetVisibilityState(Guid instanceId)
        {
            return TryNative(() =>
            {
                var componentCount = _api.GetComponentCount(instanceId);
                if (componentCount < 0)
                    return null;

                var hidden = _api.GetHiddenComponentIndices(instanceId, componentCount);
                return hidden == null ? null : VisibilityState.Create(hidden, componentCount);
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
            catch (DllNotFoundException)
            {
                _disabled = true;
                return fallback;
            }
        }
    }

    private sealed record StoredData(
        Guid SourceDefinitionId,
        string SourceDefinitionName,
        int[] HiddenIndices,
        int ComponentCount);

    private sealed class FakeNativeApi : IAssemblyDataNativeApi
    {
        private readonly Dictionary<Guid, StoredData> _data = new();

        public bool IsAvailable { get; set; } = true;
        public bool ThrowDllNotFound { get; set; }

        public bool AttachAssemblyData(
            Guid instanceId,
            Guid sourceDefinitionId,
            string sourceDefinitionName,
            int[] hiddenIndices,
            int componentCount)
        {
            ThrowIfNeeded();
            _data[instanceId] = new StoredData(
                sourceDefinitionId,
                sourceDefinitionName,
                hiddenIndices,
                componentCount);
            return true;
        }

        public bool HasAssemblyData(Guid instanceId)
        {
            ThrowIfNeeded();
            return _data.ContainsKey(instanceId);
        }

        public bool RemoveAssemblyData(Guid instanceId)
        {
            ThrowIfNeeded();
            return _data.Remove(instanceId);
        }

        public Guid? GetSourceDefinitionId(Guid instanceId)
        {
            ThrowIfNeeded();
            return _data.TryGetValue(instanceId, out var data)
                ? data.SourceDefinitionId
                : null;
        }

        public string? GetSourceDefinitionName(Guid instanceId)
        {
            ThrowIfNeeded();
            return _data.TryGetValue(instanceId, out var data)
                ? data.SourceDefinitionName
                : null;
        }

        public int GetComponentCount(Guid instanceId)
        {
            ThrowIfNeeded();
            return _data.TryGetValue(instanceId, out var data)
                ? data.ComponentCount
                : -1;
        }

        public int[]? GetHiddenComponentIndices(Guid instanceId, int componentCount)
        {
            ThrowIfNeeded();
            return _data.TryGetValue(instanceId, out var data)
                ? data.HiddenIndices
                : null;
        }

        private void ThrowIfNeeded()
        {
            if (ThrowDllNotFound)
                throw new DllNotFoundException("Native DLL missing.");
        }
    }

    [Fact]
    public void AttachAndRead_RoundTripsVisibilityState()
    {
        var api = new FakeNativeApi();
        var store = new AssemblyDataStore(api);
        var instanceId = Guid.NewGuid();
        var sourceDefinitionId = Guid.NewGuid();
        var state = VisibilityState.Create(new[] { 1, 3 }, 5);

        var attached = store.Attach(instanceId, sourceDefinitionId, "Motor_v1", state);

        Assert.True(attached);
        Assert.True(store.Has(instanceId));
        Assert.Equal(sourceDefinitionId, store.GetSourceDefinitionId(instanceId));
        Assert.Equal("Motor_v1", store.GetSourceDefinitionName(instanceId));

        var restored = store.GetVisibilityState(instanceId);
        Assert.NotNull(restored);
        Assert.Equal(state.ComponentCount, restored.ComponentCount);
        Assert.Equal(state.HiddenIndices, restored.HiddenIndices);
    }

    [Fact]
    public void Remove_ClearsStoredData()
    {
        var api = new FakeNativeApi();
        var store = new AssemblyDataStore(api);
        var instanceId = Guid.NewGuid();

        store.Attach(instanceId, Guid.NewGuid(), "Motor_v1", VisibilityState.Create(new[] { 0 }, 2));
        Assert.True(store.Remove(instanceId));

        Assert.False(store.Has(instanceId));
        Assert.Null(store.GetVisibilityState(instanceId));
    }

    [Fact]
    public void UnavailableNativeApi_NoOpsWithoutThrowing()
    {
        var api = new FakeNativeApi { IsAvailable = false };
        var store = new AssemblyDataStore(api);

        Assert.False(store.IsAvailable);
        Assert.False(store.Has(Guid.NewGuid()));
        Assert.False(store.Attach(Guid.NewGuid(), Guid.NewGuid(), "Motor_v1", VisibilityState.Create(new[] { 0 }, 2)));
        Assert.Null(store.GetVisibilityState(Guid.NewGuid()));
    }

    [Fact]
    public void NativeFailure_DisablesStore()
    {
        var api = new FakeNativeApi { ThrowDllNotFound = true };
        var store = new AssemblyDataStore(api);

        Assert.False(store.Has(Guid.NewGuid()));
        Assert.False(store.IsAvailable);
    }
}
