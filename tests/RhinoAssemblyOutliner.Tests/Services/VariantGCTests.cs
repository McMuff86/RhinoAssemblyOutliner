using Xunit;

namespace RhinoAssemblyOutliner.Tests.Services;

/// <summary>
/// Tests for VariantGarbageCollector logic.
/// Uses test doubles since RhinoDoc is not available in tests.
/// Tests orphan detection, naming convention, debounce, and dispose pattern.
/// </summary>
public class VariantGCTests
{
    #region Test Doubles

    private sealed class FakeDefinition
    {
        public int Index { get; init; }
        public string Name { get; init; } = "";
        public bool IsDeleted { get; set; }
    }

    private sealed class FakeInstance
    {
        public Guid Id { get; init; }
        public int DefinitionIndex { get; init; }
    }

    /// <summary>
    /// Mirrors VariantGarbageCollector orphan detection logic.
    /// </summary>
    private sealed class TestGarbageCollector : IDisposable
    {
        internal const string VariantPrefix = "__aov_";
        private const int DefaultDelayMs = 5000;

        private readonly object _lock = new();
        private Timer? _timer;
        private bool _disposed;
        private int _scheduledCount;

        public int ScheduledCount => _scheduledCount;
        public bool IsDisposed => _disposed;

        public void ScheduleCollection(int delayMs = DefaultDelayMs)
        {
            lock (_lock)
            {
                if (_disposed) return;
                Interlocked.Increment(ref _scheduledCount);

                _timer?.Dispose();
                _timer = new Timer(_ => { }, null, delayMs, Timeout.Infinite);
            }
        }

        public List<int> CollectOrphans(
            List<FakeDefinition> definitions,
            List<FakeInstance> instances)
        {
            var referencedIndices = new HashSet<int>(instances.Select(i => i.DefinitionIndex));
            var orphans = new List<int>();

            foreach (var def in definitions)
            {
                if (def.IsDeleted) continue;
                if (!IsVariantDefinition(def.Name)) continue;
                if (!referencedIndices.Contains(def.Index))
                    orphans.Add(def.Index);
            }

            return orphans;
        }

        internal static bool IsVariantDefinition(string? name) =>
            name != null && name.Contains(VariantPrefix);

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }

    #endregion

    private readonly TestGarbageCollector _gc = new();

    // --- Orphan Detection ---

    [Fact]
    public void CollectOrphans_NoVariants_ReturnsEmpty()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "Motor_v1" },
            new() { Index = 1, Name = "Chair" },
        };
        var instances = new List<FakeInstance>
        {
            new() { Id = Guid.NewGuid(), DefinitionIndex = 0 },
        };

        var orphans = _gc.CollectOrphans(defs, instances);
        Assert.Empty(orphans);
    }

    [Fact]
    public void CollectOrphans_ReferencedVariant_NotOrphan()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "Motor_v1" },
            new() { Index = 1, Name = "__aov_Motor_v1_3f8a2b1c" },
        };
        var instances = new List<FakeInstance>
        {
            new() { Id = Guid.NewGuid(), DefinitionIndex = 1 },
        };

        var orphans = _gc.CollectOrphans(defs, instances);
        Assert.Empty(orphans);
    }

    [Fact]
    public void CollectOrphans_UnreferencedVariant_IsOrphan()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "Motor_v1" },
            new() { Index = 1, Name = "__aov_Motor_v1_3f8a2b1c" },
        };
        var instances = new List<FakeInstance>
        {
            new() { Id = Guid.NewGuid(), DefinitionIndex = 0 },
        };

        var orphans = _gc.CollectOrphans(defs, instances);
        Assert.Single(orphans);
        Assert.Equal(1, orphans[0]);
    }

    [Fact]
    public void CollectOrphans_MixedOrphansAndReferenced()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "Motor_v1" },
            new() { Index = 1, Name = "__aov_Motor_v1_aaaa0001" },
            new() { Index = 2, Name = "__aov_Motor_v1_aaaa0002" },
            new() { Index = 3, Name = "__aov_Motor_v1_aaaa0003" },
        };
        var instances = new List<FakeInstance>
        {
            new() { Id = Guid.NewGuid(), DefinitionIndex = 0 },
            new() { Id = Guid.NewGuid(), DefinitionIndex = 2 }, // only variant 2 referenced
        };

        var orphans = _gc.CollectOrphans(defs, instances);
        Assert.Equal(2, orphans.Count);
        Assert.Contains(1, orphans);
        Assert.Contains(3, orphans);
    }

    [Fact]
    public void CollectOrphans_DeletedDefinition_Skipped()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "Motor_v1" },
            new() { Index = 1, Name = "__aov_Motor_v1_aaaa0001", IsDeleted = true },
        };
        var instances = new List<FakeInstance>();

        var orphans = _gc.CollectOrphans(defs, instances);
        Assert.Empty(orphans);
    }

    [Fact]
    public void CollectOrphans_NoInstances_AllVariantsAreOrphans()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "__aov_A_00000001" },
            new() { Index = 1, Name = "__aov_B_00000002" },
        };

        var orphans = _gc.CollectOrphans(defs, new List<FakeInstance>());
        Assert.Equal(2, orphans.Count);
    }

    [Fact]
    public void CollectOrphans_NonVariantUnreferenced_NotOrphan()
    {
        var defs = new List<FakeDefinition>
        {
            new() { Index = 0, Name = "Motor_v1" }, // not a variant
        };

        var orphans = _gc.CollectOrphans(defs, new List<FakeInstance>());
        Assert.Empty(orphans); // non-variants are never orphans
    }

    [Fact]
    public void CollectOrphans_EmptyDocument_ReturnsEmpty()
    {
        var orphans = _gc.CollectOrphans(new List<FakeDefinition>(), new List<FakeInstance>());
        Assert.Empty(orphans);
    }

    // --- IsVariantDefinition ---

    [Theory]
    [InlineData("__aov_Motor_v1_3f8a2b1c", true)]
    [InlineData("Motor_v1__aov_hash", true)]  // contains prefix
    [InlineData("Motor_v1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("__AOV_Motor", false)] // case-sensitive
    public void IsVariantDefinition_CorrectlyIdentifies(string? name, bool expected)
    {
        Assert.Equal(expected, TestGarbageCollector.IsVariantDefinition(name));
    }

    // --- Timer / Debounce ---

    [Fact]
    public void ScheduleCollection_IncrementsCount()
    {
        _gc.ScheduleCollection(100);
        Assert.Equal(1, _gc.ScheduledCount);

        _gc.ScheduleCollection(100);
        Assert.Equal(2, _gc.ScheduledCount);
    }

    [Fact]
    public void ScheduleCollection_AfterDispose_Ignored()
    {
        _gc.Dispose();
        _gc.ScheduleCollection(100); // should not throw
        Assert.Equal(0, _gc.ScheduledCount);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_SetsDisposedFlag()
    {
        _gc.Dispose();
        Assert.True(_gc.IsDisposed);
    }

    [Fact]
    public void Dispose_MultipleCalls_NoException()
    {
        _gc.Dispose();
        _gc.Dispose(); // second call should not throw
    }

    // --- Thread Safety ---

    [Fact]
    public void ConcurrentScheduleAndDispose_NoException()
    {
        var gc = new TestGarbageCollector();

        Parallel.For(0, 100, i =>
        {
            if (i == 50) gc.Dispose();
            else gc.ScheduleCollection(100);
        });
        
        gc.Dispose(); // ensure cleanup
    }

    [Fact]
    public void ConcurrentOrphanDetection_ThreadSafe()
    {
        var defs = Enumerable.Range(0, 100)
            .Select(i => new FakeDefinition { Index = i, Name = $"__aov_Test_{i:x8}" })
            .ToList();
        var instances = new List<FakeInstance>
        {
            new() { Id = Guid.NewGuid(), DefinitionIndex = 50 },
        };

        var results = new List<List<int>>();
        Parallel.For(0, 10, _ =>
        {
            var orphans = _gc.CollectOrphans(defs, instances);
            lock (results) results.Add(orphans);
        });

        // All results should be identical
        Assert.All(results, r => Assert.Equal(99, r.Count));
    }
}
