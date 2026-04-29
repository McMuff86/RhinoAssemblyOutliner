# ON_UserData Research: ON_AssemblyUserData Implementation for Rhino 8

**Date:** 2026-04-29  
**Sprint:** 4 (Configuration Persistence)  
**Purpose:** Complete technical specification for implementing `ON_AssemblyUserData` class  
**Status:** Ready for implementation

---

## 1. ON_UserData Subclass Mechanics

### 1.1 Required Macros

**In Header (.h):**
```cpp
class CYourUserData : public ON_UserData
{
    ON_OBJECT_DECLARE(CYourUserData);  // Declares m_CYourUserData_rtti, virtual DoesUserDataNeedToBeStored()
    
public:
    // Constructor, destructor, data members, methods...
};
```

**In Implementation (.cpp):**
```cpp
ON_OBJECT_IMPLEMENT(CYourUserData, ON_UserData, "A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78");
```

**Exact Signatures:**
- `ON_OBJECT_DECLARE(ClassName)` — goes immediately after class opening brace
- `ON_OBJECT_IMPLEMENT(ClassName, ParentClass, "GUID-STRING")` — goes at file scope in .cpp, before all methods
- The GUID string is the class ID (not the instance ID) — must be unique per plugin

**Reference:** See `VisibilityUserData.h` (line 17) and `VisibilityUserData.cpp` (line 19)

### 1.2 Mandatory Virtual Methods

All six methods MUST be overridden. Defaults do NOT work:

#### `bool Archive() const override`
- **Purpose:** Tell Rhino whether to serialize this UserData when saving
- **Return `true`** to enable persistence in .3dm files
- **Return `false`** for transient, runtime-only data (rare)
- **Idiom:** `bool Archive() const override { return true; }`
- **Reference:** VisibilityUserData.h:25

#### `bool Write(ON_BinaryArchive& archive) const override`
- **Purpose:** Serialize your data to binary archive
- **Pattern:**
  ```cpp
  bool YourUserData::Write(ON_BinaryArchive& archive) const
  {
      // Always start with BeginWrite3dmChunk for versioning
      if (!archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, 1, 0))
          return false;
  
      bool rc = false;
      for (;;)  // Single-iteration wrapper for early exit via break
      {
          // Write each field in order
          if (!archive.WriteInt(yourInt)) break;
          if (!archive.WriteString(yourString)) break;
          if (!archive.WriteUuid(yourUuid)) break;
          // ... more fields
          
          rc = true;
          break;
      }
  
      if (!archive.EndWrite3dmChunk())
          rc = false;
      return rc;
  }
  ```
- **Key points:**
  - Always wrap in `BeginWrite3dmChunk()` / `EndWrite3dmChunk()` (TCODE_ANONYMOUS_CHUNK is standard for UserData)
  - Version numbers enable forward/backward compatibility
  - Use the for-loop-break pattern to ensure `EndWrite3dmChunk()` is always called
- **Reference:** VisibilityUserData.cpp:34–70

#### `bool Read(ON_BinaryArchive& archive) override`
- **Purpose:** Deserialize your data from binary archive
- **Pattern:**
  ```cpp
  bool YourUserData::Read(ON_BinaryArchive& archive) override
  {
      int major_version = 0, minor_version = 0;
      if (!archive.BeginRead3dmChunk(TCODE_ANONYMOUS_CHUNK, &major_version, &minor_version))
          return false;
  
      bool rc = false;
      Clear();  // Reset state before reading
  
      for (;;)
      {
          // Check version(s) you support
          if (major_version != 1) break;  // Unknown version
  
          // Read fields in same order as Write()
          int value = 0;
          if (!archive.ReadInt(&value)) break;
          yourInt = value;
  
          if (!archive.ReadString(yourString)) break;
          // ... more fields
  
          rc = true;
          break;
      }
  
      if (!archive.EndRead3dmChunk())
          rc = false;
      return rc;
  }
  ```
- **Key points:**
  - Always clear/reset state before reading to prevent partial-read corruption
  - Check version numbers to gracefully ignore chunks from newer plugins
  - Same field order as Write()
- **Reference:** VisibilityUserData.cpp:72–114

#### `bool GetDescription(ON_wString& description) override`
- **Purpose:** Human-readable label for the UserData (shown in Rhino UI/properties)
- **Idiom:**
  ```cpp
  bool YourUserData::GetDescription(ON_wString& description) override
  {
      description = L"Your Plugin Name: Data Description";
      return true;
  }
  ```
- **Reference:** VisibilityUserData.cpp:28–32

#### `bool Transform(const ON_Xform& xform) override`
- **Purpose:** Called after Move, Rotate, Scale, or Copy/Paste of the parent object
- **For geometry-independent metadata:** Simply return `true` without modifying any member state
- **For geometry-dependent data:** Apply the transform and return `true`, or return `false` if transform invalid
- **Idiom (Assembly case):**
  ```cpp
  bool ON_AssemblyUserData::Transform(const ON_Xform& xform) override
  {
      // Assembly metadata (configuration, visibility) is geometry-independent
      // Transform is not needed; return true to let the UserData travel with copies
      return true;
  }
  ```
- **When called:**
  - After user runs Move/Rotate/Scale commands → transforms selected objects
  - After Ctrl+C / Ctrl+V within same document
  - After Paste from another document (if UserData is included in clipboard)
- **Return `true`** = "I handled it correctly" → Rhino allows the copy
- **Return `false`** = "Cannot transform this data" → Rhino may warn or strip the UserData
- **Note:** Rhino calls Transform() **after** attaching UserData to the copy, so the copy always gets the UserData first

### 1.3 Class ID (GUID) and m_userdata_uuid

**Class ID:** The string in `ON_OBJECT_IMPLEMENT()`
- Uniquely identifies the UserData CLASS (not instances)
- Must be unique per plugin (no collisions with other plugins)
- Should be a real UUID, not just a made-up string
- Fixed at compile time; hard to change later without breaking .3dm compatibility
- Example: `"A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78"` for VisibilityUserData

**m_userdata_uuid Member:**
- Set in constructor: `m_userdata_uuid = ClassIdUuid;`
- Rhino uses this to look up the class definition when reading .3dm
- Must match the Class ID from `ON_OBJECT_IMPLEMENT()`
- Reference: VisibilityUserData.cpp:8–11, 23

### 1.4 Plugin UUID (m_application_uuid)

**Purpose:** Mark which plugin owns this UserData (for round-trip safety)

**In Constructor:**
```cpp
m_application_uuid = PluginIdUuid;  // Must be your plugin's GUID
```

**Why it matters:**
- When Rhino opens a .3dm with unknown UserData (plugin not loaded), Rhino stores the `m_application_uuid`
- If the plugin is later loaded, Rhino can restore UserData even if the class wasn't registered at file-load time
- If the plugin is never loaded, the UserData is written back unchanged on Save (round-trip safe)

**Our plugin:** `{68EE26AC-D516-4F50-9DE2-46D105702323}` (see VisibilityUserData.cpp:14)

---

## 2. Attaching UserData to InstanceObject

### 2.1 C++ Attachment

**Method:** `RhinoObject::AttachUserData(ON_UserData* ud)`

```cpp
// On an InstanceObject:
CRhinoObject* pObj = pDoc->LookupObject(instanceId);
if (pObj)
{
    auto pUserData = new ON_AssemblyUserData();
    pUserData->m_userdata_uuid = ON_AssemblyUserData::Id;
    pUserData->m_application_uuid = PluginId;
    
    // Rhino takes ownership; deletes on object destruction or replacement
    pObj->AttachUserData(pUserData);
}
```

**Ownership:** **Rhino owns it.** After `AttachUserData()`, do NOT delete `pUserData`.

**Retrieval:**
- `GetUserData(class_id)` — returns the UserData if attached
- `FirstUserData()` — iterate all UserData on an object

```cpp
const ON_UserData* pData = pObj->GetUserData(class_id);
if (pData)
{
    const ON_AssemblyUserData* pAssembly = 
        static_cast<const ON_AssemblyUserData*>(pData);
    // Use pAssembly...
}
```

### 2.2 Instance vs Definition

**Important Design Rule:** UserData attaches to the **InstanceObject**, NOT the InstanceDefinition.

- Each instance can have different configurations/visibility independently
- Definition-cloning creates new instances that inherit a copy of the UserData
- This is correct for our use case (per-instance assembly state)

### 2.3 Behavior on doc.Objects.Replace()

**Question:** When we call `doc.Objects.Replace(oldInstanceId, newGeometry)` to reassign a block instance to a different definition variant, does UserData survive?

**Answer:** YES, with caveats.
- If old and new geometry are the same type (both InstanceObjects), UserData transfers
- The old InstanceObject is deleted, the new one is created with the old UserData
- **Call `doc.Objects.Replace(id, geom)` which handles this automatically**

```csharp
// C# VariantManager example:
var newDef = doc.InstanceDefinitions[newDefIndex];
var newGeom = new InstanceReferenceGeometry(newDef.Id, instance.InstanceXform);
doc.Objects.Replace(instanceId, newGeom);
// UserData on the old instance survives the replacement!
```

---

## 3. Round-Trip with .3dm

### 3.1 Serialization Flow

1. **User saves file** → Rhino calls `Write()` on all UserData with `Archive() == true`
2. **Each UserData chunk** is stored in the object's record in the .3dm file
3. **File closed and reopened** → Rhino reads chunks
4. **If plugin is loaded:** UserData class is registered → `Read()` is called → data restored
5. **If plugin is NOT loaded:** Chunks are stored as "unknown UserData" → written back unchanged on Save

### 3.2 Unknown UserData Handling

When a file containing our UserData is opened without the plugin:
- Rhino recognizes the chunk type (TCODE_ANONYMOUS_CHUNK)
- Reads it as binary blob (doesn't parse the structure)
- Stores in memory as-is
- On Save, writes the blob back exactly as it was

**Result:** Complete round-trip safety. Even if the plugin is missing, the data is preserved.

### 3.3 Known Issue: Data Loss

**Are there cases where unknown UserData is dropped on save?**

Mostly NO, with one caveat:
- If an object is edited in Rhino (geometry modified) and the chunk format is incompatible with the current Rhino version, it MAY be dropped
- For **metadata-only** UserData (like ours, which doesn't depend on geometry), this risk is near-zero
- **Best practice:** Use versioning in Write/Read chunks (which we do via `BeginWrite3dmChunk(tcode, version_major, version_minor)`)

---

## 4. Transform and Copy/Paste Behavior

### 4.1 When Transform() Is Called

1. **Move/Rotate/Scale:** User selects instance, runs Move command → `Transform()` called with the accumulated xform
2. **Copy/Paste (intra-doc):** User selects, Ctrl+C, Ctrl+V → new object created → UserData copied → `Transform()` called with identity (or paste-point offset)
3. **Copy/Paste (cross-doc):** Copy from Doc A → Paste to Doc B → clipboard includes UserData → `Transform()` at paste point

### 4.2 For Assembly Metadata (Non-Geometric)

**Recommended pattern:**
```cpp
bool ON_AssemblyUserData::Transform(const ON_Xform& xform) override
{
    // Our metadata (configuration, visibility state, source-def reference) 
    // is completely independent of geometry.
    // No transformation needed; return true to allow copies.
    return true;
}
```

**Result:** 
- UserData travels with copies (intra-doc and cross-doc)
- Configuration and visibility are preserved
- No need to recalculate or adjust anything

---

## 5. P/Invoke Marshalling Patterns

### 5.1 ON_UUID ↔ System.Guid

**Binary Layout:** Identical (both 16 bytes, same field order)

**C# → C++:**
```csharp
[DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
public static extern bool AttachAssemblyData(ref Guid instanceId, ref Guid sourceDefId);

// Call:
var instanceId = someGuid;
var sourceDefId = anotherGuid;
NativeInterop.AttachAssemblyData(ref instanceId, ref sourceDefId);
```

**C++ Signature:**
```cpp
NATIVE_API bool __stdcall AttachAssemblyData(
    const ON_UUID* instanceId,
    const ON_UUID* sourceDefId
);
```

**Rule:** `ref Guid` in C# = `const ON_UUID*` in C++ (pointer to on-stack data)

**Validation:** See NativeApi.cpp:12 — static assertion confirms binary compat

### 5.2 String Marshalling (LPStr vs LPWStr)

**UTF-8 strings (C++ code):** `const char*`
```csharp
[DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
public static extern bool AddConfiguration(
    ref Guid instanceId,
    [MarshalAs(UnmanagedType.LPStr)] string configName,  // UTF-8
    [MarshalAs(UnmanagedType.LPStr)] string? parentConfigName,
    [MarshalAs(UnmanagedType.LPArray)] Guid[] hiddenIds,
    int hiddenCount
);
```

**Wide strings (if using ON_wString in C++):** `const wchar_t*`
```csharp
[MarshalAs(UnmanagedType.LPWStr)] string configName  // UTF-16 (Windows wide char)
```

**Who owns the buffer:**
- **Input strings** (C# → C++): C# allocates, marshaller passes pointer, C++ reads only
- **Output strings** (C++ → C#): Use the "buffer pattern" (see 5.3)

### 5.3 Returning Strings by Value

**Pattern: Output Buffer Pattern**

**C++ Signature:**
```cpp
NATIVE_API int __stdcall GetActiveConfiguration(
    const ON_UUID* instanceId,
    char* buffer,              // Output: caller allocates
    int bufferSize             // Max size of buffer
);
// Returns: length of string written (not including null terminator)
// If returned value >= bufferSize, buffer was too small
```

**C++ Implementation:**
```cpp
int __stdcall GetActiveConfiguration(const ON_UUID* instanceId, char* buffer, int bufferSize)
{
    if (!buffer || bufferSize <= 0) return 0;

    auto pUserData = FindUserData(instanceId);
    if (!pUserData) return 0;

    // Convert ON_wString to UTF-8
    ON_String utf8(pUserData->m_activeConfigName);
    int len = static_cast<int>(strlen(utf8));
    
    if (len >= bufferSize) {
        // Buffer too small; return required size
        return len + 1;  // +1 for null terminator
    }

    strcpy_s(buffer, bufferSize, utf8);
    return len;
}
```

**C# Wrapper:**
```csharp
[DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
public static extern int GetActiveConfiguration(
    ref Guid instanceId,
    [MarshalAs(UnmanagedType.LPStr)] StringBuilder buffer,
    int bufferSize
);

// Usage:
var sb = new StringBuilder(256);
int len = NativeInterop.GetActiveConfiguration(ref instanceId, sb, sb.Capacity);
if (len >= sb.Capacity)
{
    // Try again with larger buffer
    sb = new StringBuilder(len + 1);
    NativeInterop.GetActiveConfiguration(ref instanceId, sb, sb.Capacity);
}
string configName = sb.ToString();
```

### 5.4 Error Codes and Return Conventions

**In this codebase:**
- **bool return:** `true` = success, `false` = error (most functions)
- **int return:** 
  - Positive = count or length (valid success)
  - 0 = not found or error
  - Negative = error code (rarely used)

**Rhino SDK convention:** Same as above — bool for success/fail, int for counts.

---

## 6. Existing Examples in the Codebase

### 6.1 VisibilityUserData Analysis

**File Path:** `src/RhinoAssemblyOutliner.native/VisibilityUserData.h/cpp`

**What it Does:**
- Persists per-instance hidden component paths (dot-separated indices)
- Stores visibility state in a set of component paths
- Syncs to/from the in-memory `CVisibilityData` (which is runtime-only)

**Class ID:** `{A7B3C4D5-E6F7-4890-AB12-CD34EF56AB78}`

**Pattern Used:**
- `ON_OBJECT_DECLARE` / `ON_OBJECT_IMPLEMENT` — properly used ✓
- `Archive() = true` — enables serialization ✓
- `Write()` — BeginWrite3dmChunk with version 1, writes count + strings ✓
- `Read()` — BeginRead3dmChunk with version check, reads count + strings ✓
- No `Transform()` override — inherits default (which returns true) ✓
- **GetDescription()** implemented ✓

**Coupling:**
- `CComponentVisibilityData` contains only the hidden paths
- Syncs **from** `CVisibilityData` (thread-safe runtime state) via `SyncFromVisData()`
- Syncs **to** `CVisibilityData` on load via `SyncToVisData()`

**Assessment:** **Well-implemented reference implementation.** Use this pattern for ON_AssemblyUserData.

### 6.2 NativeApi.h/cpp Patterns

**File Path:** `src/RhinoAssemblyOutliner.native/NativeApi.h/cpp`

**Existing P/Invoke signatures:**
- `SetComponentVisibility(const ON_UUID* instanceId, const char* path, bool visible)`
- `IsComponentVisible(const ON_UUID* instanceId, const char* path)`
- `GetComponentState(const ON_UUID* instanceId, const char* path)` → returns int (ComponentState enum)
- `SetComponentState(const ON_UUID* instanceId, const char* path, int state)`
- `GetManagedInstances(ON_UUID* buffer, int maxCount)` → returns count

**Pattern:**
- All functions check `g_initialized` before proceeding
- UUID validation: `static_assert(sizeof(ON_UUID) == 16)` in .cpp
- Null pointer guards: `if (!pDoc || !g_pVisData) return false;`
- Auto-redraw on changes: `RedrawActiveDoc()` called after visibility changes

**Extension Points for ON_AssemblyUserData:**
- Add new functions for Configuration CRUD (AddConfiguration, RemoveConfiguration, SetActiveConfiguration, GetActiveConfiguration)
- Follow same P/Invoke pattern as existing functions
- Consider callback mechanism for C# notifications (architecture doc sketches this)

### 6.3 Architecture Document Sketch

**File Path:** `docs/architecture/assembly-object-architecture.md`

**Section 2.4 (Persistence)** contains a **sketch** of ON_AssemblyUserData.

**Gaps in the Sketch:**
- No Read() implementation provided
- No Constructor shown (needs m_userdata_uuid, m_application_uuid setup)
- No GetDescription() shown
- No Transform() shown (but comment suggests return true)
- AssemblyConfig struct not defined (no ON_OBJECT_DECLARE needed — it's POD)
- Error handling incomplete (for-loop break pattern not shown)

**Assessment:** **The sketch is structurally sound.** Use it as the base, add the missing methods from VisibilityUserData pattern.

---

## 7. Proposed Schema for ON_AssemblyUserData

### 7.1 Minimum Persistent Data per InstanceObject

Based on `VariantManager` architecture and current in-memory state:

1. **source-definition ID** (ON_UUID)
   - Points to the original (non-variant) InstanceDefinition
   - Used on file load to restore correct instance → definition mapping
   - Survives cross-file copy/paste (fallback to name-based lookup if GUID not found)

2. **source-definition NAME** (ON_wString)
   - Fallback for cross-file paste where GUIDs change
   - Makes UserData more portable

3. **active-configuration name** (ON_wString)
   - E.g., "Default", "Warranty", "Minimal"
   - Used to re-apply config on file open

4. **configurations array** (ON_ClassArray<AssemblyConfig>)
   - Each config: name, parentConfigName, hiddenComponentIds
   - Stored as: name, parent, count, then array of component UUIDs

5. **component count** (int) — optional but helpful
   - Total components in source definition
   - Used for validation: if actual count differs, warn user
   - Detects if someone edited the definition while plugin was offline

### 7.2 Binary Chunk Format

```
Chunk: TCODE_ANONYMOUS_CHUNK
Version: 1 (major=1, minor=0 for future extensions)

Layout:
[1] ON_UUID m_sourceDefinitionId (16 bytes)
[2] ON_wString m_sourceDefinitionName (int count + unicode chars)
[3] ON_wString m_activeConfigName (int count + unicode chars)
[4] int configurationCount
    For each configuration:
        [4a] ON_wString name
        [4b] ON_wString parentConfigName
        [4c] int hiddenComponentIdCount
             For each hidden ID:
                 [4c-i] ON_UUID hiddenComponentId (16 bytes)
[5] int componentCount (optional validation field)
```

---

## 8. Summary: Key Takeaways for Implementor

| Topic | Key Rule |
|-------|----------|
| **Macros** | `ON_OBJECT_DECLARE` in .h; `ON_OBJECT_IMPLEMENT(Class, Base, "GUID")` in .cpp at file scope |
| **Class ID** | Unique GUID per UserData class; hardcoded in IMPLEMENT macro; used by Rhino to find class on load |
| **m_userdata_uuid** | Set in constructor = m_userdata_uuid = ClassIdUuid |
| **m_application_uuid** | Set in constructor = m_application_uuid = PluginUuid; used for round-trip if plugin missing |
| **Archive()** | Return `true` for persistent data; enables .3dm serialization |
| **Write()** | Always use BeginWrite3dmChunk/EndWrite3dmChunk; version numbers required; use for-loop-break pattern |
| **Read()** | Call Clear() first; check version; read in same order as Write(); BeginRead3dmChunk/EndRead3dmChunk |
| **GetDescription()** | Return human-readable label; return true |
| **Transform()** | For geometry-independent metadata: return true without modifying state |
| **Attachment** | `RhinoObject::AttachUserData()` — Rhino owns the pointer; don't delete |
| **Retrieval** | `GetUserData(class_id)` or `FirstUserData()` iteration |
| **P/Invoke Guids** | `ref Guid` in C# = `const ON_UUID*` in C++; binary compatible (16 bytes) |
| **P/Invoke Strings** | UTF-8: `LPStr`; output: buffer pattern with length return; caller allocates, passes pointer |
| **Round-Trip** | Unknown UserData is preserved as binary blob; always survives Save/Open even if plugin missing |
| **Copy/Paste** | UserData travels with object; Transform() called; return true to allow copy |
| **Instance vs Definition** | UserData attaches to InstanceObject; each instance has independent state |
| **Replace()** | `doc.Objects.Replace()` preserves UserData on replacement |

---

## 9. References and Further Reading

**In This Codebase:**
- `VisibilityUserData.h/.cpp` — gold standard implementation
- `docs/architecture/assembly-object-architecture.md:§2.4` — persistence sketch
- `NativeApi.h/cpp` — P/Invoke patterns (existing functions to extend)
- `VisibilityData.h` — thread-safe state container (runtime-only; use for reference)

**Rhino SDK & openNURBS:**
- `ON_UserData` base class documentation (openNURBS headers in Rhino SDK)
- `ON_BinaryArchive::BeginWrite3dmChunk()` and friends
- `ON_OBJECT_DECLARE` / `ON_OBJECT_IMPLEMENT` macro definitions
- Class Registration: `ON_Object::RegisterClass()`

**McNeel Resources:**
- Rhino 8 SDK: https://github.com/mcneel/rhino-developer-samples
- openNURBS: https://github.com/mcneel/opennurbs
- Forum: https://discourse.mcneel.com (search "ON_UserData" for examples from other plugins)

**This Codebase (Architecture Decision):**
- Why NOT CRhinoObject subclassing: `docs/architecture/assembly-object-architecture.md:§8.3`
- Why ON_UserData + Definition-Cloning is preferred: same doc

---

**Document Status:** Ready for handoff to Sprint 4 implementation team.  
**Last Updated:** 2026-04-29
