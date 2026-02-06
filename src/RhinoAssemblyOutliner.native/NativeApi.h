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

	/// Hide or show a component within a specific block instance
	NATIVE_API bool __stdcall SetComponentVisibility(
		const ON_UUID* instanceId,
		int componentIndex,
		bool visible
	);

	/// Query whether a component is visible for a specific instance
	NATIVE_API bool __stdcall IsComponentVisible(
		const ON_UUID* instanceId,
		int componentIndex
	);

	/// Get the number of hidden components for a specific instance
	NATIVE_API int __stdcall GetHiddenComponentCount(
		const ON_UUID* instanceId
	);

	/// Reset all hidden components for a specific instance
	NATIVE_API void __stdcall ResetComponentVisibility(
		const ON_UUID* instanceId
	);

	/// Return the native DLL version for compatibility checks
	NATIVE_API int __stdcall GetNativeVersion();
}
