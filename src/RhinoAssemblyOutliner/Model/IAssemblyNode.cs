using System;
using System.Collections.Generic;

namespace RhinoAssemblyOutliner.Model
{
    /// <summary>
    /// Interface for assembly tree nodes, enabling testing without RhinoCommon.
    /// </summary>
    public interface IAssemblyNode
    {
        Guid Id { get; }
        string Name { get; }
        bool IsVisible { get; set; }
        IReadOnlyList<AssemblyNode> Children { get; }
        AssemblyNode Parent { get; set; }
    }
}
