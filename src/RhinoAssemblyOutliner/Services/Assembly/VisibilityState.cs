using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rhino.DocObjects;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Immutable value object representing which components of a block definition
/// are visible or hidden. Used as cache key for variant deduplication.
/// </summary>
public sealed class VisibilityState : IEquatable<VisibilityState>
{
    /// <summary>
    /// Set of component indices that are hidden.
    /// </summary>
    public ImmutableSortedSet<int> HiddenIndices { get; }

    /// <summary>
    /// Total number of components in the source definition.
    /// </summary>
    public int ComponentCount { get; }

    private readonly int _cachedHashCode;

    private VisibilityState(ImmutableSortedSet<int> hiddenIndices, int componentCount)
    {
        HiddenIndices = hiddenIndices;
        ComponentCount = componentCount;
        _cachedHashCode = ComputeHashCode();
    }

    /// <summary>
    /// Create a VisibilityState where all components are visible.
    /// </summary>
    public static VisibilityState FromDefinition(InstanceDefinition def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        int count = def.GetObjects().Length;
        return new VisibilityState(ImmutableSortedSet<int>.Empty, count);
    }

    /// <summary>
    /// Create a VisibilityState with all components visible for a given count.
    /// </summary>
    public static VisibilityState AllVisible(int componentCount)
    {
        if (componentCount < 0) throw new ArgumentOutOfRangeException(nameof(componentCount));
        return new VisibilityState(ImmutableSortedSet<int>.Empty, componentCount);
    }

    /// <summary>
    /// Create a VisibilityState from explicit hidden indices.
    /// </summary>
    public static VisibilityState Create(IEnumerable<int> hiddenIndices, int componentCount)
    {
        if (hiddenIndices == null) throw new ArgumentNullException(nameof(hiddenIndices));
        return new VisibilityState(hiddenIndices.ToImmutableSortedSet(), componentCount);
    }

    /// <summary>
    /// Returns a new state with the given component hidden.
    /// </summary>
    public VisibilityState WithHidden(int componentIndex)
    {
        ValidateIndex(componentIndex);
        return new VisibilityState(HiddenIndices.Add(componentIndex), ComponentCount);
    }

    /// <summary>
    /// Returns a new state with the given component visible.
    /// </summary>
    public VisibilityState WithVisible(int componentIndex)
    {
        ValidateIndex(componentIndex);
        return new VisibilityState(HiddenIndices.Remove(componentIndex), ComponentCount);
    }

    /// <summary>
    /// True if all components are visible (no hidden indices).
    /// </summary>
    public bool IsAllVisible => HiddenIndices.IsEmpty;

    /// <summary>
    /// True if a specific component is visible.
    /// </summary>
    public bool IsVisible(int componentIndex) => !HiddenIndices.Contains(componentIndex);

    /// <summary>
    /// Returns the visible component indices.
    /// </summary>
    public IEnumerable<int> VisibleIndices =>
        Enumerable.Range(0, ComponentCount).Where(i => !HiddenIndices.Contains(i));

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= ComponentCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Component index {index} out of range [0, {ComponentCount})");
    }

    #region Equality

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

    #endregion

    /// <summary>
    /// Returns string like "V:0,2,3|H:1,4" or "ALL_VISIBLE(5)".
    /// </summary>
    public override string ToString()
    {
        if (IsAllVisible)
            return $"ALL_VISIBLE({ComponentCount})";

        var visible = string.Join(",", VisibleIndices);
        var hidden = string.Join(",", HiddenIndices);
        return $"V:{visible}|H:{hidden}";
    }

    /// <summary>
    /// Returns a short hex hash suitable for definition naming.
    /// </summary>
    public string ToHexHash() => GetHashCode().ToString("x8");
}
