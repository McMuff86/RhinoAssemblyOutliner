using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using RhinoAssemblyOutliner.Services.Assembly;

namespace RhinoAssemblyOutliner.Model;

/// <summary>
/// Builds the hierarchical assembly tree from a Rhino document.
/// Traverses block instances recursively to create the full hierarchy.
/// </summary>
public class AssemblyTreeBuilder
{
    private readonly RhinoDoc _doc;
    private readonly Dictionary<int, int> _definitionInstanceCounters;
    private readonly Dictionary<int, int> _definitionTotalCounts;

    /// <summary>
    /// Creates a new tree builder for the given document.
    /// </summary>
    /// <param name="doc">The Rhino document to build the tree from.</param>
    public AssemblyTreeBuilder(RhinoDoc doc)
    {
        _doc = doc;
        _definitionInstanceCounters = new Dictionary<int, int>();
        _definitionTotalCounts = new Dictionary<int, int>();
    }

    // Tracks visited definitions to prevent infinite recursion
    private HashSet<int> _visitedDefinitions;
    
    // Maximum recursion depth to prevent stack overflow
    private const int MaxRecursionDepth = 100;

    /// <summary>
    /// Builds the tree for a specific block instance (Assembly Mode).
    /// </summary>
    /// <param name="assemblyRootId">The GUID of the block instance to use as root.</param>
    /// <returns>The block instance node as root, or null if not found.</returns>
    public BlockInstanceNode BuildTreeFromRoot(Guid assemblyRootId)
    {
        try
        {
            _definitionInstanceCounters.Clear();
            _definitionTotalCounts.Clear();
            _visitedDefinitions = new HashSet<int>();
            
            CalculateTotalInstanceCounts();
            
            var obj = _doc.Objects.FindId(assemblyRootId);
            if (obj is InstanceObject instance)
            {
                return CreateBlockInstanceNode(instance, 0);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Error building tree from root: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Builds the complete assembly tree for the document.
    /// </summary>
    /// <returns>The root document node containing the full hierarchy.</returns>
    public DocumentNode BuildTree()
    {
        try
        {
            // Reset counters
            _definitionInstanceCounters.Clear();
            _definitionTotalCounts.Clear();
            _visitedDefinitions = new HashSet<int>();

            // Pre-calculate total instance counts per definition
            CalculateTotalInstanceCounts();

            // Create root document node
            var rootNode = new DocumentNode(_doc);

            // Get all top-level objects (objects not inside any block)
            var topLevelObjects = GetTopLevelObjects();
            
            rootNode.TopLevelObjectCount = topLevelObjects.Count;
            rootNode.TotalBlockDefinitionCount = _doc.InstanceDefinitions?.ActiveCount ?? 0;
            rootNode.TotalBlockInstanceCount = _definitionTotalCounts.Values.Sum();

            // Process each top-level object
            foreach (var obj in topLevelObjects)
            {
                try
                {
                    var childNode = CreateNodeForObject(obj);
                    if (childNode != null)
                    {
                        rootNode.AddChild(childNode);
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"AssemblyOutliner: Error processing object {obj?.Id}: {ex.Message}");
                }
            }

            return rootNode;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Error building tree: {ex.Message}");
            return new DocumentNode(_doc); // Return empty tree on error
        }
    }

    /// <summary>
    /// Calculates the total instance count for each block definition.
    /// </summary>
    private void CalculateTotalInstanceCounts()
    {
        foreach (var definition in _doc.InstanceDefinitions)
        {
            if (definition.IsDeleted) continue;
            _definitionTotalCounts[definition.Index] = definition.UseCount();
        }
    }

    /// <summary>
    /// Gets all top-level objects in the document (not nested in blocks).
    /// Sorted by Id so that instance numbering stays consistent across rebuilds —
    /// Doc.Objects iteration order is NOT stable after Replace() (which is what
    /// VariantManager.ReassignInstance does on every visibility toggle).
    /// </summary>
    private List<RhinoObject> GetTopLevelObjects()
    {
        var objects = new List<RhinoObject>();

        // Get all objects that are not deleted and not definition geometry
        foreach (var obj in _doc.Objects)
        {
            if (obj.Attributes.IsInstanceDefinitionObject) continue;
            if (obj.IsDeleted) continue;
            objects.Add(obj);
        }

        // Stable order: by Id (Guid). The user perceives a consistent #1, #2, ...
        // mapping that does not jump around when an instance gets reassigned.
        objects.Sort((a, b) => a.Id.CompareTo(b.Id));
        return objects;
    }

    /// <summary>
    /// Creates an appropriate node for a Rhino object.
    /// </summary>
    /// <param name="obj">The Rhino object.</param>
    /// <returns>The created node, or null if the object should be skipped.</returns>
    private AssemblyNode? CreateNodeForObject(RhinoObject obj)
    {
        if (obj is InstanceObject instanceObj)
        {
            return CreateBlockInstanceNode(instanceObj);
        }
        
        // For MVP, we focus on block instances
        // Loose geometry can be added in a future iteration
        return null;
    }

    /// <summary>
    /// Creates a block instance node and recursively processes its children.
    /// </summary>
    /// <param name="instance">The block instance object.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <returns>The created block instance node with children.</returns>
    private BlockInstanceNode? CreateBlockInstanceNode(InstanceObject instance, int depth = 0)
    {
        // Guard against null
        if (instance == null) return null;

        var actualDef = instance.InstanceDefinition;
        if (actualDef == null || actualDef.IsDeleted) return null;

        // If this instance points at a variant definition, present it as the
        // source definition. The variant exists only as an implementation detail
        // of the cloning strategy — users should see and edit the source.
        var sourceDef = ResolveSourceDefinition(actualDef);
        if (sourceDef == null || sourceDef.IsDeleted) return null;

        // Prevent infinite recursion from circular/self-referencing block definitions
        if (depth > MaxRecursionDepth)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Max recursion depth reached for block '{sourceDef.Name}'");
            return null;
        }

        if (!_visitedDefinitions.Add(sourceDef.Index))
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Circular reference detected for block '{sourceDef.Name}', skipping.");
            return null;
        }

        // Increment instance counter for this definition (count by source, not variant,
        // so multiple instances on different variants of the same source still number 1..N).
        if (!_definitionInstanceCounters.ContainsKey(sourceDef.Index))
        {
            _definitionInstanceCounters[sourceDef.Index] = 0;
        }
        _definitionInstanceCounters[sourceDef.Index]++;

        var instanceNumber = _definitionInstanceCounters[sourceDef.Index];
        var totalCount = _definitionTotalCounts.GetValueOrDefault(sourceDef.Index, 1);

        // Create the node using the SOURCE definition info (name, link type, ...).
        BlockInstanceNode node;
        try
        {
            node = new BlockInstanceNode(instance, sourceDef, instanceNumber)
            {
                TotalInstanceCount = totalCount
            };
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Error creating node for '{sourceDef.Name}': {ex.Message}");
            return null;
        }

        // Determine which component indices are hidden on THIS instance.
        // If the instance is on a variant we know about, ask the VariantManager
        // for the state. Otherwise everything is visible (instance is on source).
        var hiddenIndices = ResolveHiddenIndicesForInstance(actualDef, sourceDef);

        // Recursively process nested blocks within this definition
        ProcessDefinitionContents(node, sourceDef, depth + 1, hiddenIndices);

        // Remove from visited so sibling instances of the same definition can be processed
        _visitedDefinitions.Remove(sourceDef.Index);

        return node;
    }

    /// <summary>
    /// Maps a definition that may be a variant (__aov_<source>_<hash>) back to
    /// its source. Falls back to the input if no source is found.
    /// </summary>
    private InstanceDefinition? ResolveSourceDefinition(InstanceDefinition def)
    {
        if (def == null) return null;
        if (!def.Name.StartsWith(VariantManager.VariantPrefix, StringComparison.Ordinal))
            return def;

        // Ask the plugin-wide VariantManager first (authoritative).
        var vm = RhinoAssemblyOutlinerPlugin.Instance?.VariantManager;
        if (vm != null)
        {
            var sourceId = vm.GetSourceDefinitionId(_doc, def.Id);
            if (sourceId.HasValue)
            {
                var byId = _doc.InstanceDefinitions.FindId(sourceId.Value);
                if (byId != null && !byId.IsDeleted) return byId;
            }
        }

        // Fallback: parse "__aov_<sourceName>_<hash>" by name.
        var withoutPrefix = def.Name.Substring(VariantManager.VariantPrefix.Length);
        var lastUnderscore = withoutPrefix.LastIndexOf('_');
        if (lastUnderscore <= 0) return def;
        var sourceName = withoutPrefix.Substring(0, lastUnderscore);
        var byName = _doc.InstanceDefinitions.Find(sourceName);
        if (byName != null && !byName.IsDeleted) return byName;

        return def;
    }

    /// <summary>
    /// Returns the set of source-component indices that are hidden on this
    /// instance. Empty set means all components visible.
    /// </summary>
    private HashSet<int> ResolveHiddenIndicesForInstance(InstanceDefinition actualDef, InstanceDefinition sourceDef)
    {
        if (actualDef == null || actualDef.Id == sourceDef.Id)
            return new HashSet<int>(); // instance on source → nothing hidden

        var vm = RhinoAssemblyOutlinerPlugin.Instance?.VariantManager;
        var state = vm?.GetVariantState(actualDef.Id);
        if (state != null)
            return new HashSet<int>(state.HiddenIndices);

        // Variant exists but we have no in-memory state (e.g. file just opened
        // before ON_UserData persistence ships in Sprint 4). Everything is shown
        // as visible; user can still see the variant's actual rendering in viewport.
        return new HashSet<int>();
    }

    /// <summary>
    /// Processes the contents of a block definition and adds child nodes.
    /// </summary>
    /// <param name="parentNode">The parent node to add children to.</param>
    /// <param name="definition">The block definition to process.</param>
    /// <param name="depth">Current recursion depth.</param>
    private void ProcessDefinitionContents(BlockInstanceNode parentNode, InstanceDefinition definition, int depth = 0, HashSet<int>? hiddenComponentIndices = null)
    {
        if (definition == null) return;

        var objects = definition.GetObjects();
        if (objects == null || objects.Length == 0) return;

        hiddenComponentIndices ??= new HashSet<int>();

        // Resolve the owner instance ID (top-level instance that owns this tree branch)
        var ownerInstanceId = ResolveOwnerInstanceId(parentNode);

        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null || obj.IsDeleted) continue;

            try
            {
                if (obj is InstanceObject nestedInstance)
                {
                    // Filter out nested variant definitions
                    if (nestedInstance.InstanceDefinition != null &&
                        nestedInstance.InstanceDefinition.Name.StartsWith(
                            VariantManager.VariantPrefix, StringComparison.Ordinal))
                        continue;

                    var childNode = CreateBlockInstanceNode(nestedInstance, depth);
                    if (childNode != null)
                    {
                        childNode.ComponentIndex = i;
                        parentNode.AddChild(childNode);
                    }
                }
                else
                {
                    // Non-instance geometry → ComponentNode with eye-icon.
                    // OwnerInstanceId is part of the constructor so the node Id
                    // stays unique across multiple instances of the same definition.
                    var componentNode = new ComponentNode(obj, i, definition.Id, _doc, ownerInstanceId)
                    {
                        IsVisible = !hiddenComponentIndices.Contains(i)
                    };
                    parentNode.AddChild(componentNode);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: Error processing nested object: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Walks up the node tree to find the top-level instance ID (for VariantManager calls).
    /// </summary>
    private static Guid ResolveOwnerInstanceId(BlockInstanceNode node)
    {
        var current = node;
        while (current != null)
        {
            // Top-level instance has ComponentIndex == -1 (not nested)
            if (current.ComponentIndex < 0 && current.InstanceId != Guid.Empty)
                return current.InstanceId;
            current = current.Parent as BlockInstanceNode;
        }
        return node.InstanceId;
    }

    /// <summary>
    /// Refreshes a specific subtree in the assembly hierarchy.
    /// Useful for partial updates when a block is modified.
    /// </summary>
    /// <param name="node">The node to refresh.</param>
    public void RefreshSubtree(BlockInstanceNode node)
    {
        if (node.InstanceId == Guid.Empty) return;

        var obj = _doc.Objects.FindId(node.InstanceId);
        if (obj is not InstanceObject instance) return;

        var definition = instance.InstanceDefinition;
        if (definition == null) return;

        // Clear existing children
        node.ClearChildren();

        // Rebuild the subtree
        ProcessDefinitionContents(node, definition);
    }

    /// <summary>
    /// Finds a node by its Rhino object ID.
    /// </summary>
    /// <param name="root">The root node to search from.</param>
    /// <param name="objectId">The Rhino object ID to find.</param>
    /// <returns>The matching node, or null if not found.</returns>
    public static BlockInstanceNode? FindNodeByObjectId(AssemblyNode root, Guid objectId)
    {
        if (root is BlockInstanceNode blockNode && blockNode.InstanceId == objectId)
        {
            return blockNode;
        }

        foreach (var child in root.Children)
        {
            var found = FindNodeByObjectId(child, objectId);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Finds all nodes that reference a specific block definition.
    /// </summary>
    /// <param name="root">The root node to search from.</param>
    /// <param name="definitionIndex">The block definition index.</param>
    /// <returns>List of all matching nodes.</returns>
    public static List<BlockInstanceNode> FindNodesByDefinition(AssemblyNode root, int definitionIndex)
    {
        var results = new List<BlockInstanceNode>();
        FindNodesByDefinitionRecursive(root, definitionIndex, results);
        return results;
    }

    private static void FindNodesByDefinitionRecursive(
        AssemblyNode node, 
        int definitionIndex, 
        List<BlockInstanceNode> results)
    {
        if (node is BlockInstanceNode blockNode && blockNode.BlockDefinitionIndex == definitionIndex)
        {
            results.Add(blockNode);
        }

        foreach (var child in node.Children)
        {
            FindNodesByDefinitionRecursive(child, definitionIndex, results);
        }
    }
}
