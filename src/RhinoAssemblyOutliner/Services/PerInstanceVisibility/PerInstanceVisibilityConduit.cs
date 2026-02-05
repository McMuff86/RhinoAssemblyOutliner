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
/// Strategy v2 (REVISED - keeps objects selectable):
/// 1. Instances with hidden components remain VISIBLE and SELECTABLE
/// 2. PreDrawObject intercepts drawing and skips default rendering
/// 3. We draw only the visible components ourselves
/// 4. Visibility state stored in ComponentVisibilityData UserData
/// 
/// Key insight: Don't hide objects! Intercept their draw call instead.
/// This keeps Rhino's selection system working.
/// </summary>
public class PerInstanceVisibilityConduit : DisplayConduit
{
    private readonly RhinoDoc _doc;
    
    // Cache of managed instances (those with hidden components)
    private readonly HashSet<Guid> _managedInstances = new();
    
    // Track which instances we've already drawn this frame (avoid double-draw)
    private readonly HashSet<Guid> _drawnThisFrame = new();

    public PerInstanceVisibilityConduit(RhinoDoc doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// Register an instance as managed (has hidden components).
    /// Object stays visible and selectable - we just intercept its drawing.
    /// </summary>
    public void RegisterManagedInstance(Guid instanceId)
    {
        _managedInstances.Add(instanceId);
        // NOTE: We do NOT hide the object anymore!
        // It stays visible for selection, we just override how it's drawn.
    }

    /// <summary>
    /// Unregister an instance (all components visible again).
    /// </summary>
    public void UnregisterManagedInstance(Guid instanceId)
    {
        _managedInstances.Remove(instanceId);
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
        _managedInstances.Clear();
    }

    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
        base.CalculateBoundingBox(e);
        // BoundingBox is automatically correct since objects aren't hidden
    }

    protected override void PreDrawObjects(DrawEventArgs e)
    {
        base.PreDrawObjects(e);
        // Clear the "drawn this frame" tracker at the start of each frame
        _drawnThisFrame.Clear();
    }

    /// <summary>
    /// Intercept drawing of individual objects.
    /// For managed instances, we skip the default draw and do our own.
    /// </summary>
    protected override void PreDrawObject(DrawObjectEventArgs e)
    {
        base.PreDrawObject(e);

        // Check if this is a managed instance
        if (e.RhinoObject == null || !_managedInstances.Contains(e.RhinoObject.Id))
            return;

        // Don't draw the same instance twice in one frame
        if (_drawnThisFrame.Contains(e.RhinoObject.Id))
        {
            e.DrawObject = false;
            return;
        }

        // Skip Rhino's default drawing - we'll do it ourselves
        e.DrawObject = false;
        _drawnThisFrame.Add(e.RhinoObject.Id);

        // Draw our custom version with hidden components filtered out
        DrawManagedInstance(e, e.RhinoObject.Id);
    }

    private void DrawManagedInstance(DrawObjectEventArgs e, Guid instanceId)
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
            // No hidden components - let Rhino draw it normally
            // (This shouldn't happen if properly managed, but safety first)
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
            DrawDefinitionObject(e.Display, defObj, xform, instanceObj.Attributes);
        }
    }

    private void DrawDefinitionObject(DisplayPipeline display, RhinoObject defObj, 
        Transform xform, ObjectAttributes instanceAttrs)
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
                DrawMesh(display, mesh, material, color);
                break;

            case Brep brep:
                DrawBrep(display, brep, material, color);
                break;

            case Curve curve:
                display.DrawCurve(curve, color);
                break;

            case Rhino.Geometry.Point point:
                display.DrawPoint(point.Location, color);
                break;

            case Extrusion extrusion:
                var extBrep = extrusion.ToBrep();
                if (extBrep != null)
                    DrawBrep(display, extBrep, material, color);
                break;

            // Handle nested blocks recursively
            case InstanceReferenceGeometry nestedRef:
                DrawNestedInstance(display, nestedRef, xform, instanceAttrs);
                break;

            default:
                // Fallback: try to draw with transform
                display.DrawObject(defObj, xform);
                break;
        }
    }

    private void DrawMesh(DisplayPipeline display, Mesh mesh, DisplayMaterial material, Color color)
    {
        var viewportMode = display.Viewport.DisplayMode;
        
        if (viewportMode.EnglishName.ToLower().Contains("wireframe"))
        {
            display.DrawMeshWires(mesh, color);
        }
        else
        {
            display.DrawMeshShaded(mesh, material);
            display.DrawMeshWires(mesh, Color.FromArgb(50, color));
        }
    }

    private void DrawBrep(DisplayPipeline display, Brep brep, DisplayMaterial material, Color color)
    {
        var viewportMode = display.Viewport.DisplayMode;

        if (viewportMode.EnglishName.ToLower().Contains("wireframe"))
        {
            display.DrawBrepWires(brep, color);
        }
        else
        {
            display.DrawBrepShaded(brep, material);
            display.DrawBrepWires(brep, Color.FromArgb(50, color));
        }
    }

    private void DrawNestedInstance(DisplayPipeline display, InstanceReferenceGeometry nestedRef, 
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
            DrawDefinitionObject(display, defObj, combinedXform, instanceAttrs);
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
}
