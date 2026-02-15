using System.Collections.Concurrent;
using System.Collections.Immutable;
using Xunit;

namespace RhinoAssemblyOutliner.Tests.Integration;

/// <summary>
/// Integration-pattern tests for assembly visibility flows.
/// Tests the full logical flow without RhinoDoc using coordinated test doubles
/// that mirror the real services' interactions.
/// </summary>
public class AssemblyVisibilityFlowTests
{
    #region Test Doubles

    private sealed class VisibilityState : IEquatable<VisibilityState>
    {
        public ImmutableSortedSet<int> HiddenIndices { get; }
        public int ComponentCount { get; }
        private readonly int _cachedHashCode;

        private VisibilityState(ImmutableSortedSet<int> hidden, int count)
        {
            HiddenIndices = hidden;
            ComponentCount = count;
            _cachedHashCode = ComputeHash();
        }

        public static VisibilityState AllVisible(int count) => new(ImmutableSortedSet<int>.Empty, count);
        public static VisibilityState Create(IEnumerable<int> hidden, int count) =>
            new(hidden.ToImmutableSortedSet(), count);

        public VisibilityState WithHidden(int idx) => new(HiddenIndices.Add(idx), ComponentCount);
        public VisibilityState WithVisible(int idx) => new(HiddenIndices.Remove(idx), ComponentCount);
        public bool IsAllVisible => HiddenIndices.IsEmpty;
        public string ToHexHash() => GetHashCode().ToString("x8");

        public bool Equals(VisibilityState? o) =>
            o is not null && _cachedHashCode == o._cachedHashCode &&
            ComponentCount == o.ComponentCount && HiddenIndices.SetEquals(o.HiddenIndices);
        public override bool Equals(object? obj) => Equals(obj as VisibilityState);
        public override int GetHashCode() => _cachedHashCode;
        private int ComputeHash()
        {
            unchecked { int h = 17; h = h * 31 + ComponentCount; foreach (var i in HiddenIndices) h = h * 31 + i; return h; }
        }
    }

    private sealed class FakeDefinition
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; init; } = "";
        public int Index { get; init; }
        public int ComponentCount { get; init; }
    }

    private sealed class FakeInstance
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid DefinitionId { get; set; }
        public Guid SourceDefinitionId { get; init; }
    }

    /// <summary>
    /// Orchestrates the full visibility change flow using in-memory state.
    /// </summary>
    private sealed class AssemblyFlowSimulator
    {
        private readonly Dictionary<Guid, FakeDefinition> _definitions = new();
        private readonly Dictionary<Guid, FakeInstance> _instances = new();
        private readonly ConcurrentDictionary<(Guid, VisibilityState), Guid> _variantCache = new();
        private int _nextDefIndex;

        public FakeDefinition AddDefinition(string name, int componentCount)
        {
            var def = new FakeDefinition { Name = name, Index = _nextDefIndex++, ComponentCount = componentCount };
            _definitions[def.Id] = def;
            return def;
        }

        public FakeInstance AddInstance(FakeDefinition sourceDef)
        {
            var inst = new FakeInstance { Id = Guid.NewGuid(), DefinitionId = sourceDef.Id, SourceDefinitionId = sourceDef.Id };
            _instances[inst.Id] = inst;
            return inst;
        }

        /// <summary>Visibility change flow: compute state → get/create variant → reassign.</summary>
        public Guid ChangeVisibility(Guid instanceId, VisibilityState newState)
        {
            var inst = _instances[instanceId];
            var sourceDefId = inst.SourceDefinitionId;

            if (newState.IsAllVisible)
            {
                inst.DefinitionId = sourceDefId;
                return sourceDefId;
            }

            var key = (sourceDefId, newState);
            if (!_variantCache.TryGetValue(key, out var variantId))
            {
                var sourceDef = _definitions[sourceDefId];
                var variantDef = new FakeDefinition
                {
                    Name = $"__aov_{sourceDef.Name}_{newState.ToHexHash()}",
                    Index = _nextDefIndex++,
                    ComponentCount = sourceDef.ComponentCount - newState.HiddenIndices.Count
                };
                _definitions[variantDef.Id] = variantDef;
                variantId = variantDef.Id;
                _variantCache[key] = variantId;
            }

            inst.DefinitionId = variantId;
            return variantId;
        }

        /// <summary>Show all: reset all instances of a source def to the original.</summary>
        public void ShowAll(Guid sourceDefId)
        {
            foreach (var inst in _instances.Values.Where(i => i.SourceDefinitionId == sourceDefId))
                inst.DefinitionId = sourceDefId;
        }

        /// <summary>Isolate: show only one instance, hide rest.</summary>
        public void IsolateInstance(Guid instanceId, VisibilityState isolatedState, VisibilityState hiddenState)
        {
            var inst = _instances[instanceId];
            var sourceDefId = inst.SourceDefinitionId;

            foreach (var other in _instances.Values.Where(i => i.SourceDefinitionId == sourceDefId))
            {
                if (other.Id == instanceId)
                    ChangeVisibility(other.Id, isolatedState);
                else
                    ChangeVisibility(other.Id, hiddenState);
            }
        }

        public FakeInstance GetInstance(Guid id) => _instances[id];
        public FakeDefinition GetDefinition(Guid id) => _definitions[id];
        public int VariantCacheCount => _variantCache.Count;

        public List<int> CollectOrphanVariants()
        {
            var referencedDefIds = new HashSet<Guid>(_instances.Values.Select(i => i.DefinitionId));
            return _definitions.Values
                .Where(d => d.Name.Contains("__aov_") && !referencedDefIds.Contains(d.Id))
                .Select(d => d.Index)
                .ToList();
        }
    }

    #endregion

    // --- Visibility Change Flow ---

    [Fact]
    public void VisibilityChange_HideComponent_InstancePointsToVariant()
    {
        var sim = new AssemblyFlowSimulator();
        var motorDef = sim.AddDefinition("Motor_v1", 4);
        var instance = sim.AddInstance(motorDef);

        var state = VisibilityState.AllVisible(4).WithHidden(1);
        var variantId = sim.ChangeVisibility(instance.Id, state);

        Assert.NotEqual(motorDef.Id, variantId);
        Assert.Equal(variantId, sim.GetInstance(instance.Id).DefinitionId);
    }

    [Fact]
    public void VisibilityChange_ShowAllComponents_ReturnsToSource()
    {
        var sim = new AssemblyFlowSimulator();
        var motorDef = sim.AddDefinition("Motor_v1", 4);
        var instance = sim.AddInstance(motorDef);

        // Hide then show all
        sim.ChangeVisibility(instance.Id, VisibilityState.AllVisible(4).WithHidden(1));
        sim.ChangeVisibility(instance.Id, VisibilityState.AllVisible(4));

        Assert.Equal(motorDef.Id, sim.GetInstance(instance.Id).DefinitionId);
    }

    [Fact]
    public void VisibilityChange_TwoInstancesSameState_ShareVariant()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst1 = sim.AddInstance(def);
        var inst2 = sim.AddInstance(def);

        var state = VisibilityState.Create(new[] { 1, 3 }, 4);
        var variant1 = sim.ChangeVisibility(inst1.Id, state);
        var variant2 = sim.ChangeVisibility(inst2.Id, state);

        Assert.Equal(variant1, variant2); // deduplication
        Assert.Equal(1, sim.VariantCacheCount);
    }

    [Fact]
    public void VisibilityChange_TwoInstancesDifferentState_DifferentVariants()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst1 = sim.AddInstance(def);
        var inst2 = sim.AddInstance(def);

        var variant1 = sim.ChangeVisibility(inst1.Id, VisibilityState.Create(new[] { 1 }, 4));
        var variant2 = sim.ChangeVisibility(inst2.Id, VisibilityState.Create(new[] { 2 }, 4));

        Assert.NotEqual(variant1, variant2);
        Assert.Equal(2, sim.VariantCacheCount);
    }

    // --- Show All Flow ---

    [Fact]
    public void ShowAll_ResetsAllInstancesToSource()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst1 = sim.AddInstance(def);
        var inst2 = sim.AddInstance(def);
        var inst3 = sim.AddInstance(def);

        sim.ChangeVisibility(inst1.Id, VisibilityState.Create(new[] { 0 }, 4));
        sim.ChangeVisibility(inst2.Id, VisibilityState.Create(new[] { 1 }, 4));
        sim.ChangeVisibility(inst3.Id, VisibilityState.Create(new[] { 2 }, 4));

        sim.ShowAll(def.Id);

        Assert.Equal(def.Id, sim.GetInstance(inst1.Id).DefinitionId);
        Assert.Equal(def.Id, sim.GetInstance(inst2.Id).DefinitionId);
        Assert.Equal(def.Id, sim.GetInstance(inst3.Id).DefinitionId);
    }

    [Fact]
    public void ShowAll_CreatesOrphanVariants()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst = sim.AddInstance(def);

        sim.ChangeVisibility(inst.Id, VisibilityState.Create(new[] { 0 }, 4));
        sim.ShowAll(def.Id);

        var orphans = sim.CollectOrphanVariants();
        Assert.Single(orphans); // the variant is now unreferenced
    }

    [Fact]
    public void ShowAll_DoesNotAffectOtherDefinitions()
    {
        var sim = new AssemblyFlowSimulator();
        var motorDef = sim.AddDefinition("Motor", 4);
        var chairDef = sim.AddDefinition("Chair", 3);
        var motorInst = sim.AddInstance(motorDef);
        var chairInst = sim.AddInstance(chairDef);

        var chairState = VisibilityState.Create(new[] { 1 }, 3);
        sim.ChangeVisibility(motorInst.Id, VisibilityState.Create(new[] { 0 }, 4));
        var chairVariant = sim.ChangeVisibility(chairInst.Id, chairState);

        sim.ShowAll(motorDef.Id);

        Assert.Equal(motorDef.Id, sim.GetInstance(motorInst.Id).DefinitionId);
        Assert.Equal(chairVariant, sim.GetInstance(chairInst.Id).DefinitionId); // unchanged
    }

    // --- Isolate Flow ---

    [Fact]
    public void Isolate_ShowsOneHidesRest()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst1 = sim.AddInstance(def);
        var inst2 = sim.AddInstance(def);
        var inst3 = sim.AddInstance(def);

        var visibleState = VisibilityState.AllVisible(4); // isolated instance shows all
        var hiddenState = VisibilityState.Create(new[] { 0, 1, 2, 3 }, 4); // others all hidden

        sim.IsolateInstance(inst2.Id, visibleState, hiddenState);

        // inst2 should be on source (all visible)
        Assert.Equal(def.Id, sim.GetInstance(inst2.Id).DefinitionId);
        // inst1 and inst3 should be on all-hidden variant
        Assert.NotEqual(def.Id, sim.GetInstance(inst1.Id).DefinitionId);
        Assert.Equal(sim.GetInstance(inst1.Id).DefinitionId, sim.GetInstance(inst3.Id).DefinitionId);
    }

    [Fact]
    public void Isolate_ThenShowAll_RestoresEverything()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst1 = sim.AddInstance(def);
        var inst2 = sim.AddInstance(def);

        sim.IsolateInstance(inst1.Id,
            VisibilityState.AllVisible(4),
            VisibilityState.Create(new[] { 0, 1, 2, 3 }, 4));

        sim.ShowAll(def.Id);

        Assert.Equal(def.Id, sim.GetInstance(inst1.Id).DefinitionId);
        Assert.Equal(def.Id, sim.GetInstance(inst2.Id).DefinitionId);
    }

    // --- Complex Scenarios ---

    [Fact]
    public void MultipleDefinitions_IndependentVariants()
    {
        var sim = new AssemblyFlowSimulator();
        var motor = sim.AddDefinition("Motor", 4);
        var chair = sim.AddDefinition("Chair", 3);

        var motorInst = sim.AddInstance(motor);
        var chairInst = sim.AddInstance(chair);

        sim.ChangeVisibility(motorInst.Id, VisibilityState.Create(new[] { 0 }, 4));
        sim.ChangeVisibility(chairInst.Id, VisibilityState.Create(new[] { 0 }, 3));

        Assert.Equal(2, sim.VariantCacheCount);
        Assert.NotEqual(
            sim.GetInstance(motorInst.Id).DefinitionId,
            sim.GetInstance(chairInst.Id).DefinitionId);
    }

    [Fact]
    public void VariantName_ContainsSourceNameAndHash()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor_v1", 4);
        var inst = sim.AddInstance(def);

        var state = VisibilityState.Create(new[] { 1 }, 4);
        var variantId = sim.ChangeVisibility(inst.Id, state);

        var variantDef = sim.GetDefinition(variantId);
        Assert.StartsWith("__aov_Motor_v1_", variantDef.Name);
        Assert.Contains(state.ToHexHash(), variantDef.Name);
    }

    [Fact]
    public void SequentialVisibilityChanges_OnlyLatestVariantReferenced()
    {
        var sim = new AssemblyFlowSimulator();
        var def = sim.AddDefinition("Motor", 4);
        var inst = sim.AddInstance(def);

        sim.ChangeVisibility(inst.Id, VisibilityState.Create(new[] { 0 }, 4));
        sim.ChangeVisibility(inst.Id, VisibilityState.Create(new[] { 1 }, 4));
        sim.ChangeVisibility(inst.Id, VisibilityState.Create(new[] { 2 }, 4));

        // Only the last variant is referenced; first two are orphans
        var orphans = sim.CollectOrphanVariants();
        Assert.Equal(2, orphans.Count);
    }
}
