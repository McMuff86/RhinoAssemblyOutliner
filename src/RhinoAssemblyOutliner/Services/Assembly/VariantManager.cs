using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// Creates and caches variant block definitions for per-instance component visibility.
/// 
/// When a user hides components on a specific instance, this manager:
/// 1. Computes the VisibilityState (which components are hidden)
/// 2. Checks if a variant definition for that state already exists (deduplication)
/// 3. If not, clones the source definition with only visible components
/// 4. Reassigns the instance to point to the variant definition
/// 
/// Variant definitions are named "__aov_{sourceName}_{hexHash}" and should be
/// filtered from the Outliner display.
/// 
/// Thread-safe: uses ConcurrentDictionary for the definition cache.
/// No RhinoDoc in constructor — passed as parameter for testability.
/// </summary>
public class VariantManager : IVariantManager
{
    /// <summary>
    /// Prefix for variant definition names. Used to identify and filter them.
    /// </summary>
    public const string VariantPrefix = "__aov_";

    /// <summary>
    /// Cache: (sourceDefinitionId, VisibilityState) → variantDefinitionId.
    /// Enables deduplication: multiple instances with the same hidden components
    /// share one variant definition.
    /// </summary>
    private readonly ConcurrentDictionary<(Guid SourceDefId, VisibilityState State), Guid> _cache = new();

    /// <summary>
    /// Reverse mapping: variantDefinitionId → sourceDefinitionId.
    /// Used to look up the original definition from a variant.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Guid> _reverseMap = new();

    /// <summary>
    /// variantDefinitionId → VisibilityState that produced it.
    /// Lets the tree builder know which component indices are hidden when an
    /// instance currently points at a variant.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, VisibilityState> _variantStates = new();
    private readonly IAssemblyDataStore _dataStore;

    /// <summary>
    /// Serializes cache miss handling. Rhino document mutation is not safe to
    /// run concurrently, and ConcurrentDictionary alone cannot protect the
    /// create-and-register sequence.
    /// </summary>
    private readonly object _createLock = new();

    /// <summary>
    /// Creates a variant manager backed by the native assembly data store.
    /// </summary>
    public VariantManager()
        : this(new AssemblyDataStore())
    {
    }

    /// <summary>
    /// Creates a variant manager with an explicit assembly data store.
    /// </summary>
    public VariantManager(IAssemblyDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    /// <inheritdoc />
    public Guid GetOrCreateVariant(RhinoDoc doc, Guid sourceDefinitionId, VisibilityState state)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (state == null) throw new ArgumentNullException(nameof(state));

        // If all visible, no variant needed — use source definition directly
        if (state.IsAllVisible)
            return sourceDefinitionId;

        var key = (sourceDefinitionId, state);

        // Check cache first (fast path, lock-free)
        if (_cache.TryGetValue(key, out var cachedId))
        {
            // Verify the cached definition still exists in the document
            var cachedDef = doc.InstanceDefinitions.FindId(cachedId);
            if (cachedDef != null && !cachedDef.IsDeleted)
                return cachedId;

            // Cached definition was deleted — remove stale entry
            _cache.TryRemove(key, out _);
            _reverseMap.TryRemove(cachedId, out _);
            _variantStates.TryRemove(cachedId, out _);
        }

        lock (_createLock)
        {
            // Another caller may have created the variant while we were waiting.
            if (_cache.TryGetValue(key, out cachedId))
            {
                var cachedDef = doc.InstanceDefinitions.FindId(cachedId);
                if (cachedDef != null && !cachedDef.IsDeleted)
                    return cachedId;

                _cache.TryRemove(key, out _);
                _reverseMap.TryRemove(cachedId, out _);
                _variantStates.TryRemove(cachedId, out _);
            }

            // Create new variant definition
            var variantId = CreateVariantDefinition(doc, sourceDefinitionId, state);

            // Cache it
            _cache[key] = variantId;
            _reverseMap[variantId] = sourceDefinitionId;
            _variantStates[variantId] = state;

            return variantId;
        }
    }

    /// <inheritdoc />
    public VisibilityState? GetVariantState(Guid variantDefinitionId)
    {
        return _variantStates.TryGetValue(variantDefinitionId, out var state) ? state : null;
    }

    /// <inheritdoc />
    public VisibilityState? GetVisibilityStateForInstance(Guid instanceId)
    {
        return _dataStore.GetVisibilityState(instanceId);
    }

    /// <inheritdoc />
    public Guid? GetPersistedSourceDefinitionId(Guid instanceId)
    {
        return _dataStore.GetSourceDefinitionId(instanceId);
    }

    /// <inheritdoc />
    public string? GetPersistedSourceDefinitionName(Guid instanceId)
    {
        return _dataStore.GetSourceDefinitionName(instanceId);
    }

    /// <inheritdoc />
    public void ReassignInstance(RhinoDoc doc, Guid instanceId, VisibilityState state)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (state == null) throw new ArgumentNullException(nameof(state));

        var instanceObj = doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null)
            throw new InvalidOperationException($"Instance {instanceId} not found or is not an InstanceObject.");

        // Determine the source definition. Persisted UserData wins after
        // Save/Load; otherwise fall back to the in-memory reverse map or the
        // current definition for source instances.
        var sourceDef = ResolveSourceDefinitionForInstance(doc, instanceObj);
        if (sourceDef == null || sourceDef.IsDeleted)
            throw new InvalidOperationException($"Source definition for instance {instanceId} not found.");

        var sourceDefId = sourceDef.Id;
        var sourceName = sourceDef.Name ?? string.Empty;

        // Get or create the variant for the desired state
        var variantDefId = GetOrCreateVariant(doc, sourceDefId, state);

        // Find the variant definition
        var variantDef = doc.InstanceDefinitions.FindId(variantDefId);
        if (variantDef == null)
            throw new InvalidOperationException($"Variant definition {variantDefId} not found.");

        // Replace the instance with one pointing to the new definition.
        // Preserve the original transform so position/rotation/scale stay intact.
        var xform = instanceObj.InstanceXform;
        var newGeometry = new InstanceReferenceGeometry(variantDef.Id, xform);

        // Replace(Guid, GeometryBase, bool) — third arg is ignoreModes; false respects layer/lock state.
        if (!doc.Objects.Replace(instanceId, newGeometry, false))
            throw new InvalidOperationException($"Failed to replace instance {instanceId}.");

        if (state.IsAllVisible)
            _dataStore.Remove(instanceId);
        else
            _dataStore.Attach(instanceId, sourceDefId, sourceName, state);

        // Mask the variant name in Rhino's native UI: stamp the source definition
        // name as the InstanceObject's Attributes.Name. Object Properties will then
        // show "MBlock :: block instance" instead of "__aov_MBlock_<hash> :: block
        // instance". This is a cosmetic shim until Sprint 4 stores the source name
        // in ON_AssemblyUserData for full round-trip persistence.
        if (!string.IsNullOrEmpty(sourceName))
        {
            var refreshed = doc.Objects.FindId(instanceId);
            if (refreshed != null && refreshed.Attributes.Name != sourceName)
            {
                var attrs = refreshed.Attributes.Duplicate();
                attrs.Name = sourceName;
                doc.Objects.ModifyAttributes(instanceId, attrs, true);
            }
        }
    }

    /// <inheritdoc />
    public Guid? GetSourceDefinitionId(RhinoDoc doc, Guid variantDefinitionId)
    {
        if (_reverseMap.TryGetValue(variantDefinitionId, out var sourceId))
            return sourceId;

        // Fallback: check by naming convention
        var def = doc.InstanceDefinitions.FindId(variantDefinitionId);
        if (def != null && IsVariantDefinition(def.Name))
        {
            // Try to extract source name from variant name: __aov_{sourceName}_{hash}
            var name = def.Name;
            var withoutPrefix = name.Substring(VariantPrefix.Length);
            var lastUnderscore = withoutPrefix.LastIndexOf('_');
            if (lastUnderscore > 0)
            {
                var sourceName = withoutPrefix.Substring(0, lastUnderscore);
                var sourceDef = doc.InstanceDefinitions.Find(sourceName);
                if (sourceDef != null && !sourceDef.IsDeleted)
                {
                    _reverseMap[variantDefinitionId] = sourceDef.Id;
                    return sourceDef.Id;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsVariantDefinition(string definitionName)
    {
        return !string.IsNullOrEmpty(definitionName) &&
               definitionName.StartsWith(VariantPrefix, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void InvalidateCache(Guid sourceDefinitionId)
    {
        // Remove all cache entries for this source definition
        var keysToRemove = _cache.Keys
            .Where(k => k.SourceDefId == sourceDefinitionId)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out var variantId))
            {
                _reverseMap.TryRemove(variantId, out _);
                _variantStates.TryRemove(variantId, out _);
            }
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _cache.Clear();
        _reverseMap.Clear();
        _variantStates.Clear();
    }

    /// <inheritdoc />
    public int RestorePersistedVariants(RhinoDoc doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (!_dataStore.IsAvailable)
            return 0;

        var instanceIds = doc.Objects
            .GetObjectList(ObjectType.InstanceReference)
            .OfType<InstanceObject>()
            .Select(instance => instance.Id)
            .ToList();

        var restored = 0;
        foreach (var instanceId in instanceIds)
        {
            if (doc.Objects.FindId(instanceId) is not InstanceObject instance)
                continue;

            if (!_dataStore.Has(instance.Id))
                continue;

            var state = _dataStore.GetVisibilityState(instance.Id);
            if (state == null)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: Invalid assembly data on instance {instance.Id}; removing metadata.");
                _dataStore.Remove(instance.Id);
                continue;
            }

            var sourceDef = ResolveSourceDefinitionForInstance(doc, instance);
            if (sourceDef == null || sourceDef.IsDeleted)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: Source definition for instance {instance.Id} not found; removing metadata.");
                _dataStore.Remove(instance.Id);
                continue;
            }

            try
            {
                ReassignInstance(doc, instance.Id, state);
                restored++;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: Failed to restore instance {instance.Id}: {ex.Message}");
            }
        }

        return restored;
    }

    /// <summary>
    /// Creates a new variant definition by cloning the source and keeping only visible components.
    /// </summary>
    private Guid CreateVariantDefinition(RhinoDoc doc, Guid sourceDefinitionId, VisibilityState state)
    {
        var sourceDef = doc.InstanceDefinitions.FindId(sourceDefinitionId);
        if (sourceDef == null || sourceDef.IsDeleted)
            throw new InvalidOperationException($"Source definition {sourceDefinitionId} not found.");

        var sourceObjects = sourceDef.GetObjects();
        if (sourceObjects == null || sourceObjects.Length == 0)
            throw new InvalidOperationException($"Source definition '{sourceDef.Name}' has no objects.");

        // Collect only visible components
        var visibleGeometry = new List<GeometryBase>();
        var visibleAttributes = new List<ObjectAttributes>();

        foreach (int idx in state.VisibleIndices)
        {
            if (idx < sourceObjects.Length)
            {
                var obj = sourceObjects[idx];
                visibleGeometry.Add(obj.Geometry.Duplicate());
                visibleAttributes.Add(obj.Attributes.Duplicate());
            }
        }

        if (visibleGeometry.Count == 0)
        {
            // Edge case: all components hidden. Create with a single point at origin
            // so the definition is valid (Rhino requires at least one object).
            visibleGeometry.Add(new Point(Point3d.Origin));
            visibleAttributes.Add(new ObjectAttributes());
        }

        // Generate variant name
        var variantName = $"{VariantPrefix}{sourceDef.Name}_{state.ToHexHash()}";

        // Check if definition with this name already exists (e.g., from a previous session)
        var existingDef = doc.InstanceDefinitions.Find(variantName);
        if (existingDef != null && !existingDef.IsDeleted)
            return existingDef.Id;

        // Create the variant definition. Description is shown in Rhino's Block
        // Manager — make it clear this is internal and should not be edited.
        int defIndex = doc.InstanceDefinitions.Add(
            variantName,
            $"[Assembly Outliner — do not edit] Auto-generated variant of '{sourceDef.Name}' with {state.HiddenIndices.Count} hidden component(s).",
            Point3d.Origin,
            visibleGeometry,
            visibleAttributes
        );

        if (defIndex < 0)
            throw new InvalidOperationException(
                $"Failed to create variant definition '{variantName}'.");

        return doc.InstanceDefinitions[defIndex].Id;
    }

    private InstanceDefinition? ResolveSourceDefinitionForInstance(RhinoDoc doc, InstanceObject instanceObj)
    {
        var persistedSourceId = _dataStore.GetSourceDefinitionId(instanceObj.Id);
        if (persistedSourceId.HasValue)
        {
            var persistedById = doc.InstanceDefinitions.FindId(persistedSourceId.Value);
            if (persistedById != null && !persistedById.IsDeleted)
                return persistedById;
        }

        var persistedSourceName = _dataStore.GetSourceDefinitionName(instanceObj.Id);
        if (!string.IsNullOrWhiteSpace(persistedSourceName))
        {
            var persistedByName = doc.InstanceDefinitions.Find(persistedSourceName);
            if (persistedByName != null && !persistedByName.IsDeleted)
                return persistedByName;
        }

        var currentDefId = instanceObj.InstanceDefinition.Id;
        var sourceDefId = GetSourceDefinitionId(doc, currentDefId) ?? currentDefId;
        return doc.InstanceDefinitions.FindId(sourceDefId);
    }
}
