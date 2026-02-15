using System;
using System.Collections.Generic;

namespace RhinoAssemblyOutliner.Model
{
    /// <summary>
    /// Interface for assembly tree nodes, enabling testing without RhinoCommon.
    /// </summary>
    public interface IAssemblyNode
    {
        /// <summary>Unique identifier for this node.</summary>
        Guid Id { get; }

        /// <summary>Display name of the node.</summary>
        string Name { get; }

        /// <summary>Whether this node is visible in the viewport.</summary>
        bool IsVisible { get; set; }

        /// <summary>Child nodes in the assembly hierarchy.</summary>
        IReadOnlyList<AssemblyNode> Children { get; }

        /// <summary>Parent node in the hierarchy.</summary>
        AssemblyNode? Parent { get; set; }
    }
}
