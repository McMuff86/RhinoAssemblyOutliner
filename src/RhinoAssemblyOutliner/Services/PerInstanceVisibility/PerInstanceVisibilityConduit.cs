using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoAssemblyOutliner.Services.PerInstanceVisibility;

/// <summary>
/// DisplayConduit that enables per-instance component visibility.
/// 
/// Strategy:
/// 1. Instances with hidden components are set to Hidden mode (native Rhino)
/// 2. This conduit draws only the visible components with proper transforms
/// 3. Visibility state is stored in ComponentVisibilityData UserData
/// </summary>
public class PerInstanceVisibilityConduit : DisplayConduit
{
    private readonly RhinoDoc _doc;
    
    // Cache of managed instances (those with hidden components)
    private readonly HashSet<Guid> _managedInstances = new();
    
    // Cache for display meshes per definition component (for performance)
    private readonly Dictionary<Guid, DisplayMeshCache> _meshCache = new();

    public PerInstanceVisibilityConduit(RhinoDoc doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Register an instance as managed (has hidden components).
    /// The instance will be hidden and we'll draw it custom.
    /// </summary>
    public void RegisterManagedInstance(Guid instanceId)
    {
        _managedInstances.Add(instanceId);
        
        // Hide the original instance
        var obj = _doc.Objects.FindId(instanceId);
        if (obj != null && obj.Visible)
        {
            _doc.Objects.Hide(instanceId, ignoreLayerMode: true);
        }
    }

    /// <summary>
    /// Unregister an instance (all components visible again).
    /// </summary>
    public void UnregisterManagedInstance(Guid instanceId)
    {
        _managedInstances.Remove(instanceId);
        
        // Show the original instance again
        var obj = _doc.Objects.FindId(instanceId);
        if (obj != null && !obj.Visible)
        {
            _doc.Objects.Show(instanceId, ignoreLayerMode: true);
        }
    }

    /// <summary>
    /// Check if an instance is being managed by this conduit.
    /// </summary>
    public bool IsManaged(Guid instanceId) => _managedInstances.Contains(instanceId);

    /// <summary>
    /// Clear all managed instances.
    /// </summary>
    public void ClearAllManaged()
    {
        foreach (var id in _managedInstances)
        {
            _doc.Objects.Show(id, ignoreLayerMode: true);
        }
        _managedInstances.Clear();
    }

    /// <summary>
    /// Invalidate mesh cache for a definition.
    /// Call when definition changes.
    /// </summary>
    public void InvalidateCache(Guid definitionId)
    {
        _meshCache.Remove(definitionId);
    }

    public void InvalidateAllCaches()
    {
        _meshCache.Clear();
    }

    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
        base.CalculateBoundingBox(e);

        // Include bounding boxes of all managed instances
        foreach (var instanceId in _managedInstances)
        {
            var obj = _doc.Objects.FindId(instanceId) as InstanceObject;
            if (obj == null) continue;

            var bbox = obj.Geometry.GetBoundingBox(true);
            e.IncludeBoundingBox(bbox);
        }
    }

    protected override void PreDrawObjects(DrawEventArgs e)
    {
        base.PreDrawObjects(e);

        foreach (var instanceId in _managedInstances)
        {
            DrawManagedInstance(e, instanceId);
        }
    }

    private void DrawManagedInstance(DrawEventArgs e, Guid instanceId)
    {
        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return;

        var instanceDef = instanceObj.InstanceDefinition;
        if (instanceDef == null) return;

        // Get visibility data
        var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
            as ComponentVisibilityData;
        
        if (visData == null || !visData.HasHiddenComponents)
        {
            // No hidden components - shouldn't be managed, but draw anyway
            DrawFullInstance(e, instanceObj);
            return;
        }

        // Get instance transform
        var xform = instanceObj.InstanceXform;

        // Get definition objects
        var defObjects = instanceDef.GetObjects();

        for (int i = 0; i < defObjects.Length; i++)
        {
            // Skip hidden components
            if (!visData.IsComponentVisible(i))
                continue;

            var defObj = defObjects[i];
            DrawDefinitionObject(e, defObj, xform, instanceObj.Attributes);
        }
    }

    private void DrawFullInstance(DrawEventArgs e, InstanceObject instanceObj)
    {
        var instanceDef = instanceObj.InstanceDefinition;
        if (instanceDef == null) return;

        var xform = instanceObj.InstanceXform;
        var defObjects = instanceDef.GetObjects();

        foreach (var defObj in defObjects)
        {
            DrawDefinitionObject(e, defObj, xform, instanceObj.Attributes);
        }
    }

    private void DrawDefinitionObject(DrawEventArgs e, RhinoObject defObj, Transform xform, ObjectAttributes instanceAttrs)
    {
        var geom = defObj.Geometry;
        if (geom == null) return;

        // Duplicate and transform the geometry
        var dupGeom = geom.Duplicate();
        dupGeom.Transform(xform);

        // Determine display attributes
        var attrs = defObj.Attributes;
        var color = GetDisplayColor(attrs, instanceAttrs);
        var material = GetDisplayMaterial(attrs, color);

        // Draw based on geometry type
        switch (dupGeom)
        {
            case Mesh mesh:
                DrawMesh(e, mesh, material, color);
                break;

            case Brep brep:
                DrawBrep(e, brep, material, color);
                break;

            case Curve curve:
                e.Display.DrawCurve(curve, color);
                break;

            case Rhino.Geometry.Point point:
                e.Display.DrawPoint(point.Location, color);
                break;

            case Extrusion extrusion:
                var extBrep = extrusion.ToBrep();
                if (extBrep != null)
                    DrawBrep(e, extBrep, material, color);
                break;

            // Handle nested blocks recursively
            case InstanceReferenceGeometry nestedRef:
                DrawNestedInstance(e, nestedRef, xform, instanceAttrs);
                break;

            default:
                // Fallback: try to draw as wireframe
                e.Display.DrawObject(defObj, xform);
                break;
        }
    }

    private void DrawMesh(DrawEventArgs e, Mesh mesh, DisplayMaterial material, Color color)
    {
        // Draw shaded in shaded modes, wireframe in wireframe
        var viewportMode = e.Display.Viewport.DisplayMode;
        
        if (viewportMode.EnglishName.ToLower().Contains("wireframe"))
        {
            e.Display.DrawMeshWires(mesh, color);
        }
        else
        {
            e.Display.DrawMeshShaded(mesh, material);
            e.Display.DrawMeshWires(mesh, Color.FromArgb(50, color));
        }
    }

    private void DrawBrep(DrawEventArgs e, Brep brep, DisplayMaterial material, Color color)
    {
        var viewportMode = e.Display.Viewport.DisplayMode;

        if (viewportMode.EnglishName.ToLower().Contains("wireframe"))
        {
            e.Display.DrawBrepWires(brep, color);
        }
        else
        {
            e.Display.DrawBrepShaded(brep, material);
            e.Display.DrawBrepWires(brep, Color.FromArgb(50, color));
        }
    }

    private void DrawNestedInstance(DrawEventArgs e, InstanceReferenceGeometry nestedRef, 
        Transform parentXform, ObjectAttributes instanceAttrs)
    {
        // Get the nested definition
        var nestedDef = _doc.InstanceDefinitions.FindId(nestedRef.ParentIdefId);
        if (nestedDef == null) return;

        // Combine transforms
        var combinedXform = nestedRef.Xform * parentXform;

        // Draw nested definition objects
        var defObjects = nestedDef.GetObjects();
        foreach (var defObj in defObjects)
        {
            DrawDefinitionObject(e, defObj, combinedXform, instanceAttrs);
        }
    }

    private Color GetDisplayColor(ObjectAttributes defAttrs, ObjectAttributes instanceAttrs)
    {
        // Simplified color resolution
        // TODO: Full implementation should handle ColorSource, Layer colors, etc.
        
        if (defAttrs.ColorSource == ObjectColorSource.ColorFromObject)
            return defAttrs.ObjectColor;
        
        if (defAttrs.ColorSource == ObjectColorSource.ColorFromLayer)
        {
            var layer = _doc.Layers.FindIndex(defAttrs.LayerIndex);
            if (layer != null)
                return layer.Color;
        }

        // Fallback
        return Color.Gray;
    }

    private DisplayMaterial GetDisplayMaterial(ObjectAttributes attrs, Color fallbackColor)
    {
        // Create a simple display material
        var material = new DisplayMaterial();
        material.Diffuse = fallbackColor;
        material.Specular = Color.White;
        material.Shine = 0.5;
        return material;
    }

    /// <summary>
    /// Cache for display meshes to improve performance.
    /// </summary>
    private class DisplayMeshCache
    {
        public Dictionary<int, Mesh[]> ComponentMeshes { get; } = new();
        public DateTime Created { get; } = DateTime.Now;
    }
}
