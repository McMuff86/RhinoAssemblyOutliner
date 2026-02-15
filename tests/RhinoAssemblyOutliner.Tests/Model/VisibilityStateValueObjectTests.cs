using System.Collections.Immutable;
using Xunit;

namespace RhinoAssemblyOutliner.Tests.Model;

/// <summary>
/// Tests for the VisibilityState value object (immutable, hashable, equatable).
/// Uses a local test double mirroring Services.Assembly.VisibilityState since
/// the test project doesn't reference RhinoCommon.
/// </summary>
public class VisibilityStateValueObjectTests
{
    #region Test Double

    /// <summary>
    /// Mirrors RhinoAssemblyOutliner.Services.Assembly.VisibilityState — pure logic only.
    /// </summary>
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

        public static VisibilityState AllVisible(int componentCount)
        {
            if (componentCount < 0) throw new ArgumentOutOfRangeException(nameof(componentCount));
            return new VisibilityState(ImmutableSortedSet<int>.Empty, componentCount);
        }

        public static VisibilityState Create(IEnumerable<int> hiddenIndices, int componentCount)
        {
            if (hiddenIndices == null) throw new ArgumentNullException(nameof(hiddenIndices));
            var set = hiddenIndices.ToImmutableSortedSet();
            return new VisibilityState(set, componentCount);
        }

        public VisibilityState WithHidden(int componentIndex)
        {
            ValidateIndex(componentIndex);
            return new VisibilityState(HiddenIndices.Add(componentIndex), ComponentCount);
        }

        public VisibilityState WithVisible(int componentIndex)
        {
            ValidateIndex(componentIndex);
            return new VisibilityState(HiddenIndices.Remove(componentIndex), ComponentCount);
        }

        public bool IsAllVisible => HiddenIndices.IsEmpty;

        public bool IsVisible(int componentIndex) => !HiddenIndices.Contains(componentIndex);

        public IEnumerable<int> VisibleIndices =>
            Enumerable.Range(0, ComponentCount).Where(i => !HiddenIndices.Contains(i));

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= ComponentCount)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Component index {index} out of range [0, {ComponentCount})");
        }

        public bool Equals(VisibilityState? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (_cachedHashCode != other._cachedHashCode) return false;
            if (ComponentCount != other.ComponentCount) return false;
            return HiddenIndices.SetEquals(other.HiddenIndices);
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

        public static bool operator ==(VisibilityState? left, VisibilityState? right) =>
            left?.Equals(right) ?? right is null;
        public static bool operator !=(VisibilityState? left, VisibilityState? right) =>
            !(left == right);

        public override string ToString()
        {
            if (IsAllVisible) return $"ALL_VISIBLE({ComponentCount})";
            var visible = string.Join(",", VisibleIndices);
            var hidden = string.Join(",", HiddenIndices);
            return $"V:{visible}|H:{hidden}";
        }

        public string ToHexHash() => GetHashCode().ToString("x8");
    }

    #endregion

    // --- Construction ---

    [Fact]
    public void AllVisible_CreatesStateWithNoHidden()
    {
        var state = VisibilityState.AllVisible(5);
        Assert.True(state.IsAllVisible);
        Assert.Equal(5, state.ComponentCount);
        Assert.Empty(state.HiddenIndices);
    }

    [Fact]
    public void Create_WithHiddenIndices_SetsCorrectly()
    {
        var state = VisibilityState.Create(new[] { 1, 3 }, 5);
        Assert.False(state.IsAllVisible);
        Assert.Equal(2, state.HiddenIndices.Count);
        Assert.Contains(1, state.HiddenIndices);
        Assert.Contains(3, state.HiddenIndices);
    }

    [Fact]
    public void AllVisible_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VisibilityState.AllVisible(-1));
    }

    [Fact]
    public void Create_NullIndices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VisibilityState.Create(null!, 5));
    }

    [Fact]
    public void AllVisible_ZeroComponents_IsValid()
    {
        var state = VisibilityState.AllVisible(0);
        Assert.True(state.IsAllVisible);
        Assert.Equal(0, state.ComponentCount);
    }

    // --- Immutability ---

    [Fact]
    public void WithHidden_ReturnsNewInstance()
    {
        var original = VisibilityState.AllVisible(5);
        var modified = original.WithHidden(2);

        Assert.NotSame(original, modified);
        Assert.True(original.IsAllVisible);
        Assert.False(modified.IsAllVisible);
    }

    [Fact]
    public void WithVisible_ReturnsNewInstance()
    {
        var state = VisibilityState.Create(new[] { 1, 2 }, 5);
        var modified = state.WithVisible(1);

        Assert.NotSame(state, modified);
        Assert.Equal(2, state.HiddenIndices.Count);
        Assert.Equal(1, modified.HiddenIndices.Count);
    }

    [Fact]
    public void WithHidden_AlreadyHidden_ReturnsDifferentInstanceSameState()
    {
        var state = VisibilityState.Create(new[] { 2 }, 5);
        var modified = state.WithHidden(2);

        Assert.NotSame(state, modified);
        Assert.Equal(state, modified);
    }

    [Fact]
    public void WithVisible_AlreadyVisible_ReturnsDifferentInstanceSameState()
    {
        var state = VisibilityState.AllVisible(5);
        var modified = state.WithVisible(2);

        Assert.NotSame(state, modified);
        Assert.Equal(state, modified);
    }

    // --- Equality ---

    [Fact]
    public void Equal_SameHiddenIndicesAndCount_AreEqual()
    {
        var a = VisibilityState.Create(new[] { 1, 3 }, 5);
        var b = VisibilityState.Create(new[] { 3, 1 }, 5); // different order
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void NotEqual_DifferentHiddenIndices()
    {
        var a = VisibilityState.Create(new[] { 1 }, 5);
        var b = VisibilityState.Create(new[] { 2 }, 5);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void NotEqual_DifferentComponentCount()
    {
        var a = VisibilityState.Create(new[] { 1 }, 5);
        var b = VisibilityState.Create(new[] { 1 }, 10);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equal_BothAllVisible_SameCount()
    {
        var a = VisibilityState.AllVisible(5);
        var b = VisibilityState.AllVisible(5);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NotEqual_Null()
    {
        var a = VisibilityState.AllVisible(5);
        Assert.False(a.Equals(null));
        Assert.False(a == null);
        Assert.True(a != null);
    }

    [Fact]
    public void Equal_ReferenceEquality()
    {
        var a = VisibilityState.AllVisible(5);
        Assert.True(a.Equals(a));
    }

    [Fact]
    public void NullEqualsNull()
    {
        VisibilityState? a = null;
        VisibilityState? b = null;
        Assert.True(a == b);
    }

    // --- HashCode ---

    [Fact]
    public void HashCode_EqualStates_SameHash()
    {
        var a = VisibilityState.Create(new[] { 1, 3 }, 5);
        var b = VisibilityState.Create(new[] { 3, 1 }, 5);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void HashCode_DifferentStates_LikelyDifferentHash()
    {
        var a = VisibilityState.Create(new[] { 1 }, 5);
        var b = VisibilityState.Create(new[] { 2 }, 5);
        // Not guaranteed but should be different for well-distributed hash
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void HashCode_UsableAsDictionaryKey()
    {
        var dict = new Dictionary<VisibilityState, string>();
        var key1 = VisibilityState.Create(new[] { 1, 3 }, 5);
        var key2 = VisibilityState.Create(new[] { 3, 1 }, 5); // same state
        var key3 = VisibilityState.Create(new[] { 2 }, 5);

        dict[key1] = "variant_a";
        Assert.True(dict.ContainsKey(key2)); // deduplication works
        Assert.Equal("variant_a", dict[key2]);

        dict[key3] = "variant_b";
        Assert.Equal(2, dict.Count);
    }

    // --- IsVisible / VisibleIndices ---

    [Fact]
    public void IsVisible_HiddenComponent_ReturnsFalse()
    {
        var state = VisibilityState.Create(new[] { 2 }, 5);
        Assert.False(state.IsVisible(2));
    }

    [Fact]
    public void IsVisible_VisibleComponent_ReturnsTrue()
    {
        var state = VisibilityState.Create(new[] { 2 }, 5);
        Assert.True(state.IsVisible(0));
        Assert.True(state.IsVisible(1));
        Assert.True(state.IsVisible(3));
        Assert.True(state.IsVisible(4));
    }

    [Fact]
    public void VisibleIndices_ReturnsCorrectSet()
    {
        var state = VisibilityState.Create(new[] { 1, 3 }, 5);
        Assert.Equal(new[] { 0, 2, 4 }, state.VisibleIndices.ToArray());
    }

    [Fact]
    public void VisibleIndices_AllHidden_Empty()
    {
        var state = VisibilityState.Create(new[] { 0, 1, 2 }, 3);
        Assert.Empty(state.VisibleIndices);
    }

    // --- IsAllVisible ---

    [Fact]
    public void IsAllVisible_TrueWhenNoHidden()
    {
        Assert.True(VisibilityState.AllVisible(5).IsAllVisible);
    }

    [Fact]
    public void IsAllVisible_FalseWhenAnyHidden()
    {
        Assert.False(VisibilityState.Create(new[] { 0 }, 5).IsAllVisible);
    }

    [Fact]
    public void IsAllVisible_AfterShowingAllHidden()
    {
        var state = VisibilityState.Create(new[] { 2 }, 5).WithVisible(2);
        Assert.True(state.IsAllVisible);
    }

    // --- ToString ---

    [Fact]
    public void ToString_AllVisible_ShowsCount()
    {
        var state = VisibilityState.AllVisible(5);
        Assert.Equal("ALL_VISIBLE(5)", state.ToString());
    }

    [Fact]
    public void ToString_WithHidden_ShowsVisibleAndHidden()
    {
        var state = VisibilityState.Create(new[] { 1, 4 }, 5);
        Assert.Equal("V:0,2,3|H:1,4", state.ToString());
    }

    [Fact]
    public void ToString_AllHidden_EmptyVisible()
    {
        var state = VisibilityState.Create(new[] { 0, 1 }, 2);
        Assert.Equal("V:|H:0,1", state.ToString());
    }

    // --- ToHexHash ---

    [Fact]
    public void ToHexHash_Returns8CharHex()
    {
        var state = VisibilityState.Create(new[] { 1 }, 5);
        var hash = state.ToHexHash();
        Assert.Equal(8, hash.Length);
        Assert.Matches("^[0-9a-f]{8}$", hash);
    }

    [Fact]
    public void ToHexHash_EqualStates_SameHash()
    {
        var a = VisibilityState.Create(new[] { 1, 3 }, 5);
        var b = VisibilityState.Create(new[] { 3, 1 }, 5);
        Assert.Equal(a.ToHexHash(), b.ToHexHash());
    }

    // --- Edge Cases ---

    [Fact]
    public void WithHidden_IndexOutOfRange_Throws()
    {
        var state = VisibilityState.AllVisible(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.WithHidden(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.WithHidden(-1));
    }

    [Fact]
    public void WithVisible_IndexOutOfRange_Throws()
    {
        var state = VisibilityState.AllVisible(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.WithVisible(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.WithVisible(-1));
    }

    [Fact]
    public void ZeroComponents_WithHidden_Throws()
    {
        var state = VisibilityState.AllVisible(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.WithHidden(0));
    }

    [Fact]
    public void Create_DuplicateIndices_Deduplicated()
    {
        var state = VisibilityState.Create(new[] { 1, 1, 1 }, 5);
        Assert.Single(state.HiddenIndices);
    }

    [Fact]
    public void ChainedWithHidden_AccumulatesCorrectly()
    {
        var state = VisibilityState.AllVisible(5)
            .WithHidden(0)
            .WithHidden(2)
            .WithHidden(4);

        Assert.Equal(3, state.HiddenIndices.Count);
        Assert.Equal(new[] { 1, 3 }, state.VisibleIndices.ToArray());
    }
}
