using System;
using Rhino;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Manages variant block definitions created for per-instance component visibility.
/// Each unique VisibilityState produces a cloned definition with only the visible components.
/// </summary>
public interface IVariantManager
{
    /// <summary>
    /// Gets or creates a variant definition for the given source definition and visibility state.
    /// If a variant with the same state already exists, returns the cached definition ID.
    /// If the state is AllVisible, returns the source definition ID unchanged.
    /// </summary>
    Guid GetOrCreateVariant(RhinoDoc doc, Guid sourceDefinitionId, VisibilityState state);

    /// <summary>
    /// Reassigns an instance to use the variant definition matching the given visibility state.
    /// Replaces the instance object with a new InstanceReferenceGeometry pointing to the variant.
    /// </summary>
    void ReassignInstance(RhinoDoc doc, Guid instanceId, VisibilityState state);

    /// <summary>
    /// Gets the original source definition ID for a variant definition.
    /// Returns null if the definition is not a known variant.
    /// </summary>
    Guid? GetSourceDefinitionId(RhinoDoc doc, Guid variantDefinitionId);

    /// <summary>
    /// Checks if a definition name matches the variant naming convention (__aov_ prefix).
    /// </summary>
    bool IsVariantDefinition(string definitionName);

    /// <summary>
    /// Invalidates all cached variants for a source definition.
    /// Call this when the source definition is modified (e.g., after BlockEdit).
    /// </summary>
    void InvalidateCache(Guid sourceDefinitionId);
}
