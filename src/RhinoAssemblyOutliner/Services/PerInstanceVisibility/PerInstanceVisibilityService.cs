using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;

namespace RhinoAssemblyOutliner.Services.PerInstanceVisibility;

/// <summary>
/// Service for managing per-instance component visibility.
/// This is the main API for hiding/showing components within specific block instances.
/// 
/// All rendering is handled by the C++ native conduit (CVisibilityConduit).
/// This service acts as orchestrator: state management, UserData, and P/Invoke calls.
/// If the native DLL is unavailable, the feature is gracefully disabled.
/// </summary>
public class PerInstanceVisibilityService : IDisposable
{
    private readonly RhinoDoc _doc;
    private readonly bool _nativeAvailable;
    private bool _disposed;

    public PerInstanceVisibilityService(RhinoDoc doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));

        // Try to initialize the native conduit
        _nativeAvailable = TryInitNative();

        if (!_nativeAvailable)
        {
            RhinoApp.WriteLine("WARNING: Native visibility DLL not available. Per-instance visibility feature is disabled.");
        }

        // Subscribe to document events
        RhinoDoc.CloseDocument += OnDocumentClose;
        RhinoDoc.BeginOpenDocument += OnBeginOpenDocument;
    }

    /// <summary>
    /// Whether the native visibility system is available and active.
    /// </summary>
    public bool IsNativeAvailable => _nativeAvailable;

    /// <summary>
    /// Hide a specific component within a specific block instance.
    /// </summary>
    /// <param name="instanceId">The block instance GUID</param>
    /// <param name="componentIndex">Index of the component in the definition</param>
    public void HideComponent(Guid instanceId, int componentIndex)
    {
        SetComponentVisibility(instanceId, componentIndex, false);
    }

    /// <summary>
    /// Show a specific component within a specific block instance.
    /// </summary>
    public void ShowComponent(Guid instanceId, int componentIndex)
    {
        SetComponentVisibility(instanceId, componentIndex, true);
    }

    /// <summary>
    /// Toggle visibility of a component within an instance.
    /// </summary>
    /// <returns>New visibility state (true = visible)</returns>
    public bool ToggleComponent(Guid instanceId, int componentIndex)
    {
        if (!_nativeAvailable) return true;

        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return true;

        var visData = GetOrCreateVisibilityData(instanceObj);
        bool newState = visData.ToggleComponentVisibility(componentIndex);

        // Sync to native conduit
        string path = componentIndex.ToString();
        NativeVisibilityInterop.SetComponentVisibility(ref instanceId, path, newState);

        _doc.Views.Redraw();
        return newState;
    }

    /// <summary>
    /// Set visibility for a specific component.
    /// </summary>
    public void SetComponentVisibility(Guid instanceId, int componentIndex, bool visible)
    {
        if (!_nativeAvailable) return;

        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return;

        var visData = GetOrCreateVisibilityData(instanceObj);
        visData.SetComponentVisibility(componentIndex, visible);

        // Sync to native conduit
        string path = componentIndex.ToString();
        NativeVisibilityInterop.SetComponentVisibility(ref instanceId, path, visible);

        _doc.Views.Redraw();
    }

    /// <summary>
    /// Check if a component is visible in a specific instance.
    /// </summary>
    public bool IsComponentVisible(Guid instanceId, int componentIndex)
    {
        if (!_nativeAvailable) return true;

        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return true;

        var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
            as ComponentVisibilityData;
        
        return visData?.IsComponentVisible(componentIndex) ?? true;
    }

    /// <summary>
    /// Get all hidden component indices for an instance.
    /// </summary>
    public IEnumerable<int> GetHiddenComponents(Guid instanceId)
    {
        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return Enumerable.Empty<int>();

        var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
            as ComponentVisibilityData;
        
        return visData?.HiddenComponents ?? Enumerable.Empty<int>();
    }

    /// <summary>
    /// Show all components in an instance (reset to default).
    /// </summary>
    public void ShowAllComponents(Guid instanceId)
    {
        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return;

        var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
            as ComponentVisibilityData;
        
        if (visData != null)
        {
            visData.ShowAllComponents();
        }

        // Reset in native conduit
        if (_nativeAvailable)
        {
            NativeVisibilityInterop.ResetComponentVisibility(ref instanceId);
        }

        _doc.Views.Redraw();
    }

    /// <summary>
    /// Hide all components in an instance.
    /// </summary>
    public void HideAllComponents(Guid instanceId)
    {
        if (!_nativeAvailable) return;

        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return;

        var instanceDef = instanceObj.InstanceDefinition;
        if (instanceDef == null) return;

        var visData = GetOrCreateVisibilityData(instanceObj);
        var defObjects = instanceDef.GetObjects();
        visData.HideAllComponents(defObjects.Length);

        // Sync all to native conduit
        for (int i = 0; i < defObjects.Length; i++)
        {
            string path = i.ToString();
            NativeVisibilityInterop.SetComponentVisibility(ref instanceId, path, false);
        }

        _doc.Views.Redraw();
    }

    /// <summary>
    /// Get component info for an instance (for UI display).
    /// </summary>
    public List<ComponentInfo> GetComponentInfos(Guid instanceId)
    {
        var result = new List<ComponentInfo>();
        
        var instanceObj = _doc.Objects.FindId(instanceId) as InstanceObject;
        if (instanceObj == null) return result;

        var instanceDef = instanceObj.InstanceDefinition;
        if (instanceDef == null) return result;

        var defObjects = instanceDef.GetObjects();
        var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
            as ComponentVisibilityData;

        for (int i = 0; i < defObjects.Length; i++)
        {
            var defObj = defObjects[i];
            result.Add(new ComponentInfo
            {
                Index = i,
                Name = string.IsNullOrEmpty(defObj.Name) ? $"Component {i}" : defObj.Name,
                ObjectType = defObj.ObjectType.ToString(),
                LayerName = _doc.Layers[defObj.Attributes.LayerIndex]?.Name ?? "Unknown",
                IsVisible = visData?.IsComponentVisible(i) ?? true
            });
        }

        return result;
    }

    /// <summary>
    /// Refresh after document changes (e.g., definition edited).
    /// Resyncs all UserData-based visibility state to the native conduit.
    /// </summary>
    public void RefreshAll()
    {
        if (!_nativeAvailable) return;

        // Re-evaluate all instances with visibility data
        foreach (var obj in _doc.Objects.GetObjectList(ObjectType.InstanceReference))
        {
            if (obj is InstanceObject instanceObj)
            {
                var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
                    as ComponentVisibilityData;
                
                if (visData != null && visData.HasHiddenComponents)
                {
                    // Sync hidden components to native
                    var instanceId = instanceObj.Id;
                    foreach (int idx in visData.HiddenComponents)
                    {
                        string path = idx.ToString();
                        NativeVisibilityInterop.SetComponentVisibility(ref instanceId, path, false);
                    }
                }
            }
        }

        _doc.Views.Redraw();
    }

    #region Private Helpers

    private bool TryInitNative()
    {
        try
        {
            if (!NativeVisibilityInterop.IsNativeDllAvailable())
                return false;

            return NativeVisibilityInterop.NativeInit();
        }
        catch (DllNotFoundException ex)
        {
            RhinoApp.WriteLine($"WARNING: Native visibility DLL load failed: {ex.Message}");
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            RhinoApp.WriteLine($"WARNING: Native visibility DLL entry point missing: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"WARNING: Native visibility init failed: {ex.Message}");
            return false;
        }
    }

    private ComponentVisibilityData GetOrCreateVisibilityData(InstanceObject instanceObj)
    {
        var visData = instanceObj.Attributes.UserData.Find(typeof(ComponentVisibilityData)) 
            as ComponentVisibilityData;

        if (visData == null)
        {
            visData = new ComponentVisibilityData();
            instanceObj.Attributes.UserData.Add(visData);
        }

        return visData;
    }

    private void OnDocumentClose(object sender, DocumentEventArgs e)
    {
        if (e.Document?.RuntimeSerialNumber == _doc.RuntimeSerialNumber)
        {
            // Native conduit handles its own cleanup via DocEventHandler
        }
    }

    private void OnBeginOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        // Refresh state when document opens (restores from UserData)
        if (e.Document?.RuntimeSerialNumber == _doc.RuntimeSerialNumber)
        {
            RefreshAll();
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        RhinoDoc.CloseDocument -= OnDocumentClose;
        RhinoDoc.BeginOpenDocument -= OnBeginOpenDocument;

        if (_nativeAvailable)
        {
            try
            {
                NativeVisibilityInterop.NativeCleanup();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"WARNING: Native cleanup failed: {ex.Message}");
            }
        }

        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Info about a component in a block definition.
/// </summary>
public class ComponentInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string ObjectType { get; set; } = "";
    public string LayerName { get; set; } = "";
    public bool IsVisible { get; set; } = true;
}
