using System;
using Rhino;
using Rhino.PlugIns;
using RhinoAssemblyOutliner.Services.Assembly;
using RhinoAssemblyOutliner.Services.PerInstanceVisibility;

namespace RhinoAssemblyOutliner;

/// <summary>
/// Main plugin class for the Rhino Assembly Outliner.
/// Provides a SolidWorks FeatureManager-style hierarchical view of block instances.
/// </summary>
[System.Runtime.InteropServices.Guid("68EE26AC-D516-4F50-9DE2-46D105702323")]
public class RhinoAssemblyOutlinerPlugin : PlugIn
{
    /// <summary>
    /// Gets the singleton instance of the plugin.
    /// </summary>
    public static RhinoAssemblyOutlinerPlugin? Instance { get; private set; }

    /// <summary>Garbage collector for orphaned variant definitions.</summary>
    private VariantGarbageCollector? _garbageCollector;

    /// <summary>Event handler for assembly-related document events.</summary>
    private AssemblyEventHandler? _assemblyEventHandler;

    /// <summary>Single VariantManager shared by all panels and the tree builder.</summary>
    private VariantManager? _variantManager;

    /// <summary>
    /// Plugin constructor.
    /// </summary>
    public RhinoAssemblyOutlinerPlugin()
    {
        Instance = this;
    }

    /// <summary>Exposes the garbage collector for external use.</summary>
    public IVariantGarbageCollector? GarbageCollector => _garbageCollector;

    /// <summary>Exposes the event handler for external subscription.</summary>
    public AssemblyEventHandler? AssemblyEventHandler => _assemblyEventHandler;

    /// <summary>
    /// The plugin-wide VariantManager. All visibility-toggle paths and the
    /// tree builder MUST go through this instance so the variant cache and
    /// state map are consistent.
    /// </summary>
    public IVariantManager VariantManager => _variantManager ??= new VariantManager();

    /// <summary>
    /// Called when the plugin is being loaded.
    /// </summary>
    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        RhinoApp.WriteLine("RhinoAssemblyOutliner plugin loaded.");
        
        // Panel is registered in OpenOutlinerCommand constructor
        EnsureNativeModuleLoaded();

        // --- Sprint 3: Assembly lifecycle services ---
        _variantManager = new VariantManager();
        _garbageCollector = new VariantGarbageCollector();
        _assemblyEventHandler = new AssemblyEventHandler(_garbageCollector);
        _assemblyEventHandler.VariantRefreshRequired += OnVariantRefreshRequired;
        _assemblyEventHandler.CacheClearRequired += OnCacheClearRequired;
        _assemblyEventHandler.Subscribe();
        
        // Register event handlers for document changes
        RhinoDoc.BeginOpenDocument += OnBeginOpenDocument;
        RhinoDoc.EndOpenDocument += OnEndOpenDocument;
        RhinoDoc.CloseDocument += OnCloseDocument;
        
        return LoadReturnCode.Success;
    }

    private static void EnsureNativeModuleLoaded()
    {
        if (!NativeVisibilityInterop.IsNativeDllAvailable())
        {
            RhinoApp.WriteLine("AssemblyOutliner: Native module not found; persistence will be unavailable.");
            return;
        }

        try
        {
            var version = NativeVisibilityInterop.GetNativeVersion();
            RhinoApp.WriteLine($"AssemblyOutliner: Native module v{version} loaded.");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Failed to load native module: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when a document begins opening.
    /// </summary>
    private void OnBeginOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        // Placeholder for document open handling
    }

    /// <summary>
    /// Called when a document finishes opening.
    /// </summary>
    private void OnEndOpenDocument(object sender, DocumentOpenEventArgs e)
    {
        if (e.Document == null)
        {
            RhinoApp.WriteLine("Document opened - Assembly tree refresh triggered.");
            return;
        }

        var restored = 0;
        using (_assemblyEventHandler?.Suspend())
        {
            restored = VariantManager.RestorePersistedVariants(e.Document);
        }

        if (restored > 0)
        {
            RhinoApp.WriteLine($"AssemblyOutliner: Restored {restored} persisted assembly instance(s).");
            _garbageCollector?.ScheduleCollection(e.Document);
        }
        else
        {
            RhinoApp.WriteLine("Document opened - Assembly tree refresh triggered.");
        }
    }

    /// <summary>
    /// Called when a document is closed.
    /// </summary>
    private void OnCloseDocument(object sender, DocumentEventArgs e)
    {
        // Clear the assembly tree when document closes
        RhinoApp.WriteLine("Document closed - Assembly tree cleared.");
    }

    private void OnVariantRefreshRequired(object? sender, VariantRefreshEventArgs e)
    {
        _variantManager?.InvalidateCache(e.SourceDefinitionId);
    }

    private void OnCacheClearRequired(object? sender, EventArgs e)
    {
        _variantManager?.ClearCache();
    }

    /// <summary>
    /// Called when the plugin is being unloaded.
    /// Cleans up native resources and unsubscribes all events.
    /// </summary>
    protected override void OnShutdown()
    {
        // --- Sprint 3: Clean up assembly lifecycle services ---
        if (_assemblyEventHandler != null)
        {
            _assemblyEventHandler.VariantRefreshRequired -= OnVariantRefreshRequired;
            _assemblyEventHandler.CacheClearRequired -= OnCacheClearRequired;
        }

        _assemblyEventHandler?.Dispose();
        _assemblyEventHandler = null;

        _garbageCollector?.Dispose();
        _garbageCollector = null;

        // Unsubscribe all event handlers to prevent event leaks on static events
        RhinoDoc.BeginOpenDocument -= OnBeginOpenDocument;
        RhinoDoc.EndOpenDocument -= OnEndOpenDocument;
        RhinoDoc.CloseDocument -= OnCloseDocument;

        // Clean up native DLL resources (conduit, vis data, event handler)
        if (NativeVisibilityInterop.IsNativeDllAvailable())
        {
            try
            {
                NativeVisibilityInterop.NativeCleanup();
                RhinoApp.WriteLine("AssemblyOutliner: Native module cleaned up.");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"AssemblyOutliner: NativeCleanup failed: {ex.Message}");
            }
        }

        base.OnShutdown();
    }
}
