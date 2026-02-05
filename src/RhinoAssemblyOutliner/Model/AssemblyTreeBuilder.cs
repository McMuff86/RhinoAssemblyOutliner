using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;

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
    /// </summary>
    private List<RhinoObject> GetTopLevelObjects()
    {
        var objects = new List<RhinoObject>();
        
        // Get all objects that are not deleted and not definition geometry
        foreach (var obj in _doc.Objects)
        {
            // Skip objects that are part of block definitions
            if (obj.Attributes.IsInstanceDefinitionObject) continue;
            
            // Skip deleted objects
            if (obj.IsDeleted) continue;
            
            objects.Add(obj);
        }

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
        
        var definition = instance.InstanceDefinition;
        if (definition == null || definition.IsDeleted) return null;

        // Prevent infinite recursion (self-referencing blocks)
        if (depth > MaxRecursionDepth)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Max recursion depth reached for block '{definition.Name}'");
            return null;
        }

        // Increment instance counter for this definition
        if (!_definitionInstanceCounters.ContainsKey(definition.Index))
        {
            _definitionInstanceCounters[definition.Index] = 0;
        }
        _definitionInstanceCounters[definition.Index]++;
        
        var instanceNumber = _definitionInstanceCounters[definition.Index];
        var totalCount = _definitionTotalCounts.GetValueOrDefault(definition.Index, 1);

        // Create the node
        BlockInstanceNode node;
        try
        {
            node = new BlockInstanceNode(instance, definition, instanceNumber)
            {
                TotalInstanceCount = totalCount
            };
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Error creating node for '{definition.Name}': {ex.Message}");
            return null;
        }

        // Recursively process nested blocks within this definition
        ProcessDefinitionContents(node, definition, depth + 1);

        return node;
    }

    /// <summary>
    /// Processes the contents of a block definition and adds child nodes.
    /// </summary>
    /// <param name="parentNode">The parent node to add children to.</param>
    /// <param name="definition">The block definition to process.</param>
    /// <param name="depth">Current recursion depth.</param>
    private void ProcessDefinitionContents(BlockInstanceNode parentNode, InstanceDefinition definition, int depth = 0)
    {
        if (definition == null) return;
        
        var objects = definition.GetObjects();
        if (objects == null || objects.Length == 0) return;

        foreach (var obj in objects)
        {
            if (obj == null || obj.IsDeleted) continue;
            
            try
            {
                if (obj is InstanceObject nestedInstance)
                {
                    var childNode = CreateBlockInstanceNode(nestedInstance, depth);
                    if (childNode != null)
                    {
                        parentNode.AddChild(childNode);
                    }
                }
                // Note: For MVP, we skip loose geometry inside blocks
                // This can be extended to include GeometryNode in future iterations
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: Error processing nested object: {ex.Message}");
            }
        }
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
