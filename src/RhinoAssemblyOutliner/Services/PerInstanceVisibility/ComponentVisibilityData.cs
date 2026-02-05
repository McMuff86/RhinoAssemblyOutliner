using System;
using System.Collections.Generic;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;

namespace RhinoAssemblyOutliner.Services.PerInstanceVisibility;

/// <summary>
/// UserData attached to InstanceObject to store per-instance component visibility.
/// Key = ComponentIndex (index in InstanceDefinition.GetObjects())
/// Value = true if visible, false if hidden
/// </summary>
public class ComponentVisibilityData : UserData
{
    // Dictionary storing visibility state per component index
    // If a component is not in this dict, it's considered visible (default)
    private Dictionary<int, bool> _componentVisibility = new();
    
    /// <summary>
    /// Set of component indices that are hidden.
    /// </summary>
    public HashSet<int> HiddenComponents { get; private set; } = new();

    /// <summary>
    /// Whether this instance has any hidden components (needs custom drawing).
    /// </summary>
    public bool HasHiddenComponents => HiddenComponents.Count > 0;

    public override string Description => "Per-instance component visibility data";

    /// <summary>
    /// Check if a component is visible.
    /// </summary>
    public bool IsComponentVisible(int componentIndex)
    {
        return !HiddenComponents.Contains(componentIndex);
    }

    /// <summary>
    /// Set visibility for a component.
    /// </summary>
    public void SetComponentVisibility(int componentIndex, bool visible)
    {
        if (visible)
        {
            HiddenComponents.Remove(componentIndex);
        }
        else
        {
            HiddenComponents.Add(componentIndex);
        }
    }

    /// <summary>
    /// Toggle visibility for a component.
    /// </summary>
    public bool ToggleComponentVisibility(int componentIndex)
    {
        if (HiddenComponents.Contains(componentIndex))
        {
            HiddenComponents.Remove(componentIndex);
            return true; // now visible
        }
        else
        {
            HiddenComponents.Add(componentIndex);
            return false; // now hidden
        }
    }

    /// <summary>
    /// Show all components.
    /// </summary>
    public void ShowAllComponents()
    {
        HiddenComponents.Clear();
    }

    /// <summary>
    /// Hide all components.
    /// </summary>
    public void HideAllComponents(int totalComponents)
    {
        HiddenComponents.Clear();
        for (int i = 0; i < totalComponents; i++)
        {
            HiddenComponents.Add(i);
        }
    }

    protected override void OnDuplicate(UserData source)
    {
        if (source is ComponentVisibilityData src)
        {
            HiddenComponents = new HashSet<int>(src.HiddenComponents);
            _componentVisibility = new Dictionary<int, bool>(src._componentVisibility);
        }
    }

    public override bool ShouldWrite => HasHiddenComponents;

    protected override bool Read(BinaryArchiveReader archive)
    {
        try
        {
            var dict = archive.ReadDictionary();
            HiddenComponents.Clear();
            
            if (dict.TryGetInteger("Count", out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    if (dict.TryGetInteger($"H{i}", out int hiddenIndex))
                    {
                        HiddenComponents.Add(hiddenIndex);
                    }
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected override bool Write(BinaryArchiveWriter archive)
    {
        try
        {
            var dict = new Rhino.Collections.ArchivableDictionary();
            dict.Set("Count", HiddenComponents.Count);
            
            int i = 0;
            foreach (var hiddenIndex in HiddenComponents)
            {
                dict.Set($"H{i}", hiddenIndex);
                i++;
            }
            
            archive.WriteDictionary(dict);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
