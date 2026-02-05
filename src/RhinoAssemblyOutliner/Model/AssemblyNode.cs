using System.Collections.ObjectModel;
using Rhino.DocObjects;

namespace RhinoAssemblyOutliner.Model;

/// <summary>
/// Base class for all nodes in the assembly tree.
/// Represents a hierarchical element that can contain children.
/// </summary>
public abstract class AssemblyNode
{
    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Display name shown in the tree view.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Parent node in the hierarchy. Null for root nodes.
    /// </summary>
    public AssemblyNode? Parent { get; internal set; }

    /// <summary>
    /// Child nodes contained within this node.
    /// </summary>
    public ObservableCollection<AssemblyNode> Children { get; }

    /// <summary>
    /// Indicates whether this node is currently visible in the viewport.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Indicates whether this node is expanded in the tree view.
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Indicates whether this node is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// The layer this node belongs to, if applicable.
    /// </summary>
    public Layer? Layer { get; set; }

    /// <summary>
    /// Depth level in the hierarchy (0 = root).
    /// </summary>
    public int Depth => Parent?.Depth + 1 ?? 0;

    /// <summary>
    /// Creates a new assembly node.
    /// </summary>
    /// <param name="displayName">Display name for the node.</param>
    protected AssemblyNode(string displayName)
    {
        Id = Guid.NewGuid();
        DisplayName = displayName;
        Children = new ObservableCollection<AssemblyNode>();
    }

    /// <summary>
    /// Adds a child node to this node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    public void AddChild(AssemblyNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>
    /// Removes a child node from this node.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    /// <returns>True if the child was found and removed.</returns>
    public bool RemoveChild(AssemblyNode child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all child nodes.
    /// </summary>
    public void ClearChildren()
    {
        foreach (var child in Children)
        {
            child.Parent = null;
        }
        Children.Clear();
    }

    /// <summary>
    /// Gets all descendant nodes recursively.
    /// </summary>
    /// <returns>Enumerable of all descendant nodes.</returns>
    public IEnumerable<AssemblyNode> GetAllDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetAllDescendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Gets the icon key for this node type.
    /// </summary>
    /// <returns>Icon identifier string.</returns>
    public abstract string GetIconKey();

    /// <summary>
    /// Gets a summary description of this node.
    /// </summary>
    /// <returns>Summary text for display in detail panel.</returns>
    public abstract string GetSummary();
}
