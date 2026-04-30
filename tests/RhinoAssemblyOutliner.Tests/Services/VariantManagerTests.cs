using System.Collections.Concurrent;
using System.Collections.Immutable;
using Xunit;

namespace RhinoAssemblyOutliner.Tests.Services;

/// <summary>
/// Tests for VariantManager logic without RhinoDoc dependency.
/// Uses test doubles that mirror the real VariantManager's pure logic:
/// naming convention, cache behavior, IsVariantDefinition, reverse lookup.
/// </summary>
public class VariantManagerTests
{
    #region Test Doubles

    private sealed class VisibilityState : IEquatable<VisibilityState>
    {
        public ImmutableSortedSet<int> HiddenIndices { get; }
        public int ComponentCount { get; }
        private readonly int _cachedHashCode;

        private VisibilityState(ImmutableSortedSet<int> hiddenIndices, int componentCount)
        {
            HiddenIndices = hiddenIndices;
            ComponentCount = componentCount;
            _cachedHashCode = ComputeHashCode();
        }

        public static VisibilityState AllVisible(int count) =>
            new(ImmutableSortedSet<int>.Empty, count);

        public static VisibilityState Create(IEnumerable<int> hidden, int count) =>
            new(hidden.ToImmutableSortedSet(), count);

        public bool IsAllVisible => HiddenIndices.IsEmpty;

        public string ToHexHash() => GetHashCode().ToString("x8");

        public bool Equals(VisibilityState? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return _cachedHashCode == other._cachedHashCode &&
                   ComponentCount == other.ComponentCount &&
                   HiddenIndices.SetEquals(other.HiddenIndices);
        }

        public override bool Equals(object? obj) => Equals(obj as VisibilityState);
        public override int GetHashCode() => _cachedHashCode;

        private int ComputeHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ComponentCount;
                foreach (int idx in HiddenIndices)
                    hash = hash * 31 + idx;
                return hash;
            }
        }
    }

    /// <summary>
    /// Simulates VariantManager's cache and naming logic without RhinoDoc.
    /// </summary>
    private sealed class TestVariantManager
    {
        public const string VariantPrefix = "__aov_";

        private readonly ConcurrentDictionary<(Guid SourceDefId, VisibilityState State), Guid> _cache = new();
        private readonly ConcurrentDictionary<Guid, Guid> _reverseMap = new();
        private readonly Dictionary<Guid, string> _definitionNames = new();
        private readonly Dictionary<string, Guid> _definitionByName = new();
        private readonly object _lock = new();

        public void RegisterDefinition(Guid id, string name)
        {
            _definitionNames[id] = name;
            _definitionByName[name] = id;
        }

        public Guid GetOrCreateVariant(Guid sourceDefId, VisibilityState state)
        {
            if (state.IsAllVisible) return sourceDefId;

            var key = (sourceDefId, state);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out cached))
                    return cached;

                var sourceName = _definitionNames.GetValueOrDefault(sourceDefId, "Unknown");
                var variantName = $"{VariantPrefix}{sourceName}_{state.ToHexHash()}";

                // Check if already exists by name (saved from a previous session).
                if (_definitionByName.TryGetValue(variantName, out var existingId))
                {
                    _cache[key] = existingId;
                    _reverseMap[existingId] = sourceDefId;
                    return existingId;
                }

                var variantId = Guid.NewGuid();
                RegisterDefinition(variantId, variantName);
                _cache[key] = variantId;
                _reverseMap[variantId] = sourceDefId;
                return variantId;
            }
        }

        public Guid? GetSourceDefinitionId(Guid variantDefId)
        {
            if (_reverseMap.TryGetValue(variantDefId, out var sourceId))
                return sourceId;

            if (!_definitionNames.TryGetValue(variantDefId, out var name))
                return null;

            if (!IsVariantDefinition(name)) return null;

            var withoutPrefix = name.Substring(VariantPrefix.Length);
            var lastUnderscore = withoutPrefix.LastIndexOf('_');
            if (lastUnderscore > 0)
            {
                var sourceName = withoutPrefix.Substring(0, lastUnderscore);
                if (_definitionByName.TryGetValue(sourceName, out var srcId))
                {
                    _reverseMap[variantDefId] = srcId;
                    return srcId;
                }
            }

            return null;
        }

        public bool IsVariantDefinition(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith(VariantPrefix, StringComparison.Ordinal);

        public void InvalidateCache(Guid sourceDefId)
        {
            var keysToRemove = _cache.Keys.Where(k => k.SourceDefId == sourceDefId).ToList();
            foreach (var key in keysToRemove)
            {
                if (_cache.TryRemove(key, out var variantId))
                    _reverseMap.TryRemove(variantId, out _);
            }
        }

        public int CacheCount => _cache.Count;

        public static string BuildVariantName(string sourceName, string hexHash) =>
            $"{VariantPrefix}{sourceName}_{hexHash}";
    }

    #endregion

    private readonly TestVariantManager _manager = new();
    private readonly Guid _motorDefId = Guid.NewGuid();

    public VariantManagerTests()
    {
        _manager.RegisterDefinition(_motorDefId, "Motor_v1");
    }

    // --- Naming Convention ---

    [Fact]
    public void VariantName_FollowsConvention()
    {
        var name = TestVariantManager.BuildVariantName("Motor_v1", "3f8a2b1c");
        Assert.Equal("__aov_Motor_v1_3f8a2b1c", name);
    }

    [Fact]
    public void VariantName_StartsWithPrefix()
    {
        var name = TestVariantManager.BuildVariantName("Anything", "00000000");
        Assert.StartsWith("__aov_", name);
    }

    [Fact]
    public void VariantName_ContainsSourceName()
    {
        var name = TestVariantManager.BuildVariantName("Chair_Assembly", "abcdef01");
        Assert.Contains("Chair_Assembly", name);
    }

    // --- IsVariantDefinition ---

    [Fact]
    public void IsVariantDefinition_WithPrefix_ReturnsTrue()
    {
        Assert.True(_manager.IsVariantDefinition("__aov_Motor_v1_3f8a2b1c"));
    }

    [Fact]
    public void IsVariantDefinition_WithoutPrefix_ReturnsFalse()
    {
        Assert.False(_manager.IsVariantDefinition("Motor_v1"));
    }

    [Fact]
    public void IsVariantDefinition_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(_manager.IsVariantDefinition(null!));
        Assert.False(_manager.IsVariantDefinition(""));
    }

    [Fact]
    public void IsVariantDefinition_PrefixOnly_ReturnsTrue()
    {
        Assert.True(_manager.IsVariantDefinition("__aov_"));
    }

    [Fact]
    public void IsVariantDefinition_SimilarButWrongPrefix_ReturnsFalse()
    {
        Assert.False(_manager.IsVariantDefinition("_aov_Motor"));
        Assert.False(_manager.IsVariantDefinition("__AOV_Motor"));
    }

    // --- Cache Hit / Miss ---

    [Fact]
    public void GetOrCreateVariant_AllVisible_ReturnsSourceId()
    {
        var state = VisibilityState.AllVisible(5);
        var result = _manager.GetOrCreateVariant(_motorDefId, state);
        Assert.Equal(_motorDefId, result);
        Assert.Equal(0, _manager.CacheCount); // not cached
    }

    [Fact]
    public void GetOrCreateVariant_NewState_CreatesVariant()
    {
        var state = VisibilityState.Create(new[] { 1 }, 5);
        var variantId = _manager.GetOrCreateVariant(_motorDefId, state);

        Assert.NotEqual(_motorDefId, variantId);
        Assert.Equal(1, _manager.CacheCount);
    }

    [Fact]
    public void GetOrCreateVariant_SameState_ReturnsCached()
    {
        var state1 = VisibilityState.Create(new[] { 1, 3 }, 5);
        var state2 = VisibilityState.Create(new[] { 3, 1 }, 5); // same logical state

        var id1 = _manager.GetOrCreateVariant(_motorDefId, state1);
        var id2 = _manager.GetOrCreateVariant(_motorDefId, state2);

        Assert.Equal(id1, id2);
        Assert.Equal(1, _manager.CacheCount);
    }

    [Fact]
    public void GetOrCreateVariant_DifferentStates_DifferentVariants()
    {
        var stateA = VisibilityState.Create(new[] { 1 }, 5);
        var stateB = VisibilityState.Create(new[] { 2 }, 5);

        var idA = _manager.GetOrCreateVariant(_motorDefId, stateA);
        var idB = _manager.GetOrCreateVariant(_motorDefId, stateB);

        Assert.NotEqual(idA, idB);
        Assert.Equal(2, _manager.CacheCount);
    }

    [Fact]
    public void GetOrCreateVariant_SameStateDifferentSource_DifferentVariants()
    {
        var chairId = Guid.NewGuid();
        _manager.RegisterDefinition(chairId, "Chair");

        var state = VisibilityState.Create(new[] { 0 }, 3);
        var motorVariant = _manager.GetOrCreateVariant(_motorDefId, state);
        var chairVariant = _manager.GetOrCreateVariant(chairId, state);

        Assert.NotEqual(motorVariant, chairVariant);
    }

    // --- GetSourceDefinitionId (Reverse Lookup) ---

    [Fact]
    public void GetSourceDefinitionId_KnownVariant_ReturnsSource()
    {
        var state = VisibilityState.Create(new[] { 2 }, 5);
        var variantId = _manager.GetOrCreateVariant(_motorDefId, state);

        var sourceId = _manager.GetSourceDefinitionId(variantId);
        Assert.Equal(_motorDefId, sourceId);
    }

    [Fact]
    public void GetSourceDefinitionId_SourceDefinition_ReturnsNull()
    {
        var sourceId = _manager.GetSourceDefinitionId(_motorDefId);
        Assert.Null(sourceId);
    }

    [Fact]
    public void GetSourceDefinitionId_UnknownId_ReturnsNull()
    {
        var sourceId = _manager.GetSourceDefinitionId(Guid.NewGuid());
        Assert.Null(sourceId);
    }

    [Fact]
    public void GetSourceDefinitionId_FallbackByNaming_Works()
    {
        // Simulate a variant created in a previous session (not in reverse map)
        var state = VisibilityState.Create(new[] { 1 }, 5);
        var hexHash = state.ToHexHash();
        var variantName = $"__aov_Motor_v1_{hexHash}";
        var variantId = Guid.NewGuid();
        _manager.RegisterDefinition(variantId, variantName);

        var sourceId = _manager.GetSourceDefinitionId(variantId);
        Assert.Equal(_motorDefId, sourceId);
    }

    // --- InvalidateCache ---

    [Fact]
    public void InvalidateCache_RemovesAllVariantsForSource()
    {
        var stateA = VisibilityState.Create(new[] { 1 }, 5);
        var stateB = VisibilityState.Create(new[] { 2 }, 5);
        _manager.GetOrCreateVariant(_motorDefId, stateA);
        _manager.GetOrCreateVariant(_motorDefId, stateB);
        Assert.Equal(2, _manager.CacheCount);

        _manager.InvalidateCache(_motorDefId);
        Assert.Equal(0, _manager.CacheCount);
    }

    [Fact]
    public void InvalidateCache_DoesNotAffectOtherSources()
    {
        var chairId = Guid.NewGuid();
        _manager.RegisterDefinition(chairId, "Chair");

        _manager.GetOrCreateVariant(_motorDefId, VisibilityState.Create(new[] { 0 }, 3));
        _manager.GetOrCreateVariant(chairId, VisibilityState.Create(new[] { 0 }, 3));
        Assert.Equal(2, _manager.CacheCount);

        _manager.InvalidateCache(_motorDefId);
        Assert.Equal(1, _manager.CacheCount);
    }

    [Fact]
    public void InvalidateCache_AfterInvalidation_ReusesExistingDefinitionByName()
    {
        var state = VisibilityState.Create(new[] { 1 }, 5);
        var id1 = _manager.GetOrCreateVariant(_motorDefId, state);

        _manager.InvalidateCache(_motorDefId);
        var id2 = _manager.GetOrCreateVariant(_motorDefId, state);

        // Invalidation clears in-memory mappings only. If the variant definition
        // still exists in the document, the manager rehydrates the cache by name.
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void InvalidateCache_UnknownSource_NoError()
    {
        _manager.InvalidateCache(Guid.NewGuid()); // should not throw
    }

    // --- Thread Safety (basic) ---

    [Fact]
    public void ConcurrentGetOrCreate_SameState_NoException()
    {
        var state = VisibilityState.Create(new[] { 1 }, 5);
        var results = new ConcurrentBag<Guid>();

        Parallel.For(0, 100, _ =>
        {
            results.Add(_manager.GetOrCreateVariant(_motorDefId, state));
        });

        // All should return the same variant ID
        Assert.Single(results.Distinct());
    }

    [Fact]
    public void ConcurrentInvalidateAndCreate_NoException()
    {
        var state = VisibilityState.Create(new[] { 1 }, 5);

        // Should not throw with concurrent operations
        Parallel.For(0, 50, i =>
        {
            if (i % 3 == 0)
                _manager.InvalidateCache(_motorDefId);
            else
                _manager.GetOrCreateVariant(_motorDefId, state);
        });
    }
}
