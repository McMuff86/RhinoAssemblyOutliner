// NativeApi.h : Exported C API for P/Invoke from C# plugin

#pragma once

#ifdef NATIVE_API_EXPORTS
#define NATIVE_API __declspec(dllexport)
#else
#define NATIVE_API __declspec(dllimport)
#endif

extern "C"
{
	/// Initialize the native module (call from C# OnLoadPlugIn)
	NATIVE_API bool __stdcall NativeInit();

	/// Cleanup the native module (call from C# OnUnloadPlugIn)
	NATIVE_API void __stdcall NativeCleanup();

	/// Hide or show a component within a specific block instance.
	/// componentPath is a dot-separated index string, e.g. "0", "1.0", "1.0.2"
	NATIVE_API bool __stdcall SetComponentVisibility(
		const ON_UUID* instanceId,
		const char* componentPath,
		bool visible
	);

	/// Query whether a component is visible for a specific instance.
	/// componentPath is a dot-separated index string.
	NATIVE_API bool __stdcall IsComponentVisible(
		const ON_UUID* instanceId,
		const char* componentPath
	);

	/// Get the number of hidden components for a specific instance
	NATIVE_API int __stdcall GetHiddenComponentCount(
		const ON_UUID* instanceId
	);

	/// Reset all hidden components for a specific instance
	NATIVE_API void __stdcall ResetComponentVisibility(
		const ON_UUID* instanceId
	);

	/// Enable or disable debug logging to Rhino command line
	NATIVE_API void __stdcall SetDebugLogging(bool enabled);

	/// Return the native DLL version for compatibility checks
	NATIVE_API int __stdcall GetNativeVersion();

	/// Save visibility state to UserData on all managed instances
	NATIVE_API void __stdcall PersistVisibilityState();

	/// Load visibility state from UserData on all instances in active doc
	NATIVE_API void __stdcall LoadVisibilityState();

	/// Get all managed instance IDs (returns count, fills buffer up to maxCount)
	NATIVE_API int __stdcall GetManagedInstances(ON_UUID* buffer, int maxCount);

	/// Check if native conduit is currently enabled
	NATIVE_API bool __stdcall IsConduitEnabled();
}
