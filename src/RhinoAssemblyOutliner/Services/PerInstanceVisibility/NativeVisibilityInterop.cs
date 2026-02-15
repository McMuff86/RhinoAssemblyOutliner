using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RhinoAssemblyOutliner.Services.PerInstanceVisibility;

/// <summary>
/// P/Invoke declarations for the C++ native visibility DLL.
/// The native DLL must be placed next to the .rhp plugin file.
/// API v2: uses dot-separated path strings instead of flat int indices.
/// </summary>
public static class NativeVisibilityInterop
{
    private const string DllName = "RhinoAssemblyOutliner.Native.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool NativeInit();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void NativeCleanup();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetComponentVisibility(
        ref Guid instanceId,
        [MarshalAs(UnmanagedType.LPStr)] string componentPath,
        [MarshalAs(UnmanagedType.Bool)] bool visible
    );

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsComponentVisible(
        ref Guid instanceId,
        [MarshalAs(UnmanagedType.LPStr)] string componentPath
    );

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int GetHiddenComponentCount(ref Guid instanceId);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void ResetComponentVisibility(ref Guid instanceId);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void SetDebugLogging([MarshalAs(UnmanagedType.Bool)] bool enabled);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int GetNativeVersion();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void PersistVisibilityState();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void LoadVisibilityState();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int GetManagedInstances(
        [In, Out] Guid[] buffer,
        int maxCount
    );

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsConduitEnabled();

    /// <summary>
    /// Set the state of a component within a block instance.
    /// </summary>
    /// <param name="instanceId">The block instance UUID.</param>
    /// <param name="path">Dot-separated component path (e.g. "0", "1.0.2").</param>
    /// <param name="state">0=Visible, 1=Hidden, 2=Suppressed, 3=Transparent.</param>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetComponentState(
        ref Guid instanceId,
        [MarshalAs(UnmanagedType.LPStr)] string path,
        int state
    );

    /// <summary>
    /// Get the state of a component within a block instance.
    /// </summary>
    /// <returns>0=Visible, 1=Hidden, 2=Suppressed, 3=Transparent.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int GetComponentState(
        ref Guid instanceId,
        [MarshalAs(UnmanagedType.LPStr)] string path
    );

    /// <summary>
    /// Check if the native DLL exists next to the plugin.
    /// </summary>
    public static bool IsNativeDllAvailable()
    {
        var pluginDir = Path.GetDirectoryName(typeof(NativeVisibilityInterop).Assembly.Location);
        if (pluginDir == null) return false;
        return File.Exists(Path.Combine(pluginDir, DllName));
    }
}
