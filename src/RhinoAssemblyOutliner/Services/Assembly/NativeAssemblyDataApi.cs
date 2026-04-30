using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RhinoAssemblyOutliner.Services.Assembly;

/// <summary>
/// P/Invoke adapter for native ON_AssemblyUserData persistence.
/// </summary>
public sealed class NativeAssemblyDataApi : IAssemblyDataNativeApi
{
    private const string DllName = "RhinoAssemblyOutliner.Native.dll";

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            var pluginDir = Path.GetDirectoryName(typeof(NativeAssemblyDataApi).Assembly.Location);
            return pluginDir != null && File.Exists(Path.Combine(pluginDir, DllName));
        }
    }

    /// <inheritdoc />
    public bool AttachAssemblyData(
        Guid instanceId,
        Guid sourceDefinitionId,
        string sourceDefinitionName,
        int[] hiddenIndices,
        int componentCount)
    {
        return AttachAssemblyDataNative(
            ref instanceId,
            ref sourceDefinitionId,
            sourceDefinitionName,
            hiddenIndices,
            hiddenIndices.Length,
            componentCount);
    }

    /// <inheritdoc />
    public bool HasAssemblyData(Guid instanceId) => HasAssemblyDataNative(ref instanceId);

    /// <inheritdoc />
    public bool RemoveAssemblyData(Guid instanceId) => RemoveAssemblyDataNative(ref instanceId);

    /// <inheritdoc />
    public Guid? GetSourceDefinitionId(Guid instanceId)
    {
        var sourceDefinitionId = Guid.Empty;
        return GetSourceDefinitionIdNative(ref instanceId, ref sourceDefinitionId)
            ? sourceDefinitionId
            : null;
    }

    /// <inheritdoc />
    public string? GetSourceDefinitionName(Guid instanceId)
    {
        var capacity = 256;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var buffer = new StringBuilder(capacity);
            var required = GetSourceDefinitionNameNative(ref instanceId, buffer, buffer.Capacity);
            if (required < 0)
                return null;

            if (required < buffer.Capacity)
                return buffer.ToString();

            capacity = required + 1;
        }

        return null;
    }

    /// <inheritdoc />
    public int GetComponentCount(Guid instanceId) => GetAssemblyComponentCountNative(ref instanceId);

    /// <inheritdoc />
    public int[]? GetHiddenComponentIndices(Guid instanceId, int componentCount)
    {
        if (componentCount < 0)
            return null;

        var buffer = new int[Math.Max(componentCount, 1)];
        var count = GetHiddenComponentIndicesNative(ref instanceId, buffer, buffer.Length);
        if (count < 0)
            return null;

        if (count > buffer.Length)
        {
            buffer = new int[count];
            count = GetHiddenComponentIndicesNative(ref instanceId, buffer, buffer.Length);
            if (count < 0)
                return null;
        }

        return buffer.Take(count).ToArray();
    }

    [DllImport(DllName, EntryPoint = "AttachAssemblyData", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachAssemblyDataNative(
        ref Guid instanceId,
        ref Guid sourceDefId,
        [MarshalAs(UnmanagedType.LPWStr)] string sourceDefName,
        [In] int[] hiddenIndices,
        int hiddenCount,
        int componentCount);

    [DllImport(DllName, EntryPoint = "HasAssemblyData", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HasAssemblyDataNative(ref Guid instanceId);

    [DllImport(DllName, EntryPoint = "RemoveAssemblyData", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveAssemblyDataNative(ref Guid instanceId);

    [DllImport(DllName, EntryPoint = "GetSourceDefinitionId", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSourceDefinitionIdNative(
        ref Guid instanceId,
        ref Guid outSourceDefId);

    [DllImport(DllName, EntryPoint = "GetSourceDefinitionName", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private static extern int GetSourceDefinitionNameNative(
        ref Guid instanceId,
        [Out] StringBuilder buffer,
        int bufferSize);

    [DllImport(DllName, EntryPoint = "GetHiddenComponentIndices", CallingConvention = CallingConvention.StdCall)]
    private static extern int GetHiddenComponentIndicesNative(
        ref Guid instanceId,
        [Out] int[] buffer,
        int maxCount);

    [DllImport(DllName, EntryPoint = "GetAssemblyComponentCount", CallingConvention = CallingConvention.StdCall)]
    private static extern int GetAssemblyComponentCountNative(ref Guid instanceId);
}
