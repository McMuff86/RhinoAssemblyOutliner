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

    /// <summary>
    /// Clears all in-memory variant mappings.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Returns the VisibilityState that produced a given variant definition,
    /// or null if the variant is unknown to this manager (e.g. loaded from a
    /// .3dm file before ON_UserData persistence is implemented).
    /// </summary>
    VisibilityState? GetVariantState(Guid variantDefinitionId);

    /// <summary>
    /// Returns the persisted VisibilityState for a specific instance, if one exists.
    /// </summary>
    VisibilityState? GetVisibilityStateForInstance(Guid instanceId);

    /// <summary>
    /// Returns the persisted source definition id for a specific instance, if one exists.
    /// </summary>
    Guid? GetPersistedSourceDefinitionId(Guid instanceId);

    /// <summary>
    /// Returns the persisted source definition name for a specific instance, if one exists.
    /// </summary>
    string? GetPersistedSourceDefinitionName(Guid instanceId);

    /// <summary>
    /// Rehydrates saved assembly metadata after a document is opened.
    /// </summary>
    int RestorePersistedVariants(RhinoDoc doc);
}
