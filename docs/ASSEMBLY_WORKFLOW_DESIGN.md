# Assembly Workflow Design - Native Per-Instance Component Visibility

**Autor:** Research Subagent  
**Datum:** 2026-02-05  
**Status:** Design Document (Grundlage für Implementation)  
**Letzte Aktualisierung:** 2026-02-05 (Post-CAD-Research Synthesis)

---

## Inhaltsverzeichnis

1. [Technical Deep Dive: Block-Architektur](#1-technical-deep-dive-block-architektur)
2. [C++ Implementation Guide](#2-c-implementation-guide)
3. [UX/Workflow Design](#3-uxworkflow-design)
4. [Integration Architecture (C++ ↔ C#)](#4-integration-architecture-c--c)
5. [Edge Cases & Challenges](#5-edge-cases--challenges)
6. [Implementation Roadmap](#6-implementation-roadmap)
7. [**UX Recommendations (Post-Research)**](#7-ux-recommendations-post-research)

---

## 1. Technical Deep Dive: Block-Architektur

### 1.1 Rhino's Block-System Grundlagen

```
┌─────────────────────────────────────────────────────────────────┐
│                    BLOCK ARCHITECTURE                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  CRhinoInstanceDefinition         CRhinoInstanceObject          │
│  ═══════════════════════          ══════════════════════        │
│  "Block Definition"               "Block Instance"               │
│  (Template/Blueprint)             (Placed Copy with Transform)   │
│                                                                 │
│  ┌─────────────────┐              ┌─────────────────┐           │
│  │ Definition "A"  │              │ Instance #1     │           │
│  │ ┌─────────────┐ │              │ Xform: T1       │──┐        │
│  │ │ Object 0    │ │◄─────────────│ Definition: A   │  │        │
│  │ │ Object 1    │ │              └─────────────────┘  │        │
│  │ │ Object 2    │ │              ┌─────────────────┐  │        │
│  │ │ (nested B)  │ │◄─────────────│ Instance #2     │  │Same    │
│  │ └─────────────┘ │              │ Xform: T2       │──┤Def     │
│  └─────────────────┘              │ Definition: A   │  │        │
│                                   └─────────────────┘  │        │
│                                   ┌─────────────────┐  │        │
│                                   │ Instance #3     │──┘        │
│                                   │ Xform: T3       │           │
│                                   │ Definition: A   │           │
│                                   └─────────────────┘           │
│                                                                 │
│  PROBLEM: Alle Instanzen teilen DIESELBE Definition!            │
│  → Keine native per-instance visibility                         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Nested Blocks (Blocks in Blocks)

Nested Blocks sind Block-Definitionen, die selbst Block-Instanzen enthalten:

```cpp
// Beispiel: Schrank enthält Scharniere
// Definition "Schrank" → Object(0) = Korpus (Brep)
//                      → Object(1) = Scharnier-Instanz
//                      → Object(2) = Scharnier-Instanz 
//                      → Object(3) = Tür (Brep)

// Beim Iterieren:
const CRhinoInstanceDefinition* schrank_def = ...;
for (int i = 0; i < schrank_def->ObjectCount(); i++)
{
    const CRhinoObject* obj = schrank_def->Object(i);
    
    if (obj->ObjectType() == ON::instance_reference)
    {
        // Das ist eine nested Instance!
        const CRhinoInstanceObject* nested = 
            static_cast<const CRhinoInstanceObject*>(obj);
        
        // Deren Definition holen
        const CRhinoInstanceDefinition* nested_def = 
            nested->InstanceDefinition();
        
        // Rekursiv weitermachen...
    }
}
```

### 1.3 Component Addressing (Instance-Path)

Für per-instance visibility müssen wir Komponenten **eindeutig identifizieren**:

```cpp
// Option A: Flacher Index (nur für Top-Level Komponenten)
struct ComponentRef_Flat {
    int component_index;  // Index in idef->Object(i)
};

// Option B: Hierarchischer Pfad (für nested blocks)
struct ComponentRef_Path {
    ON_UUID instance_id;                    // Die konkrete Instanz
    ON_SimpleArray<int> component_path;     // [parent_idx, child_idx, ...]
};

// Beispiel: Schrank → Scharnier → Schraube
// instance_id = {GUID der Schrank-Instanz}
// component_path = [1, 0]  // Scharnier ist Object(1), Schraube ist Object(0)
```

**Empfehlung:** Für MVP mit flachem Index starten, Path-System für v2.

### 1.4 Block-Updates und State-Sync

**Was passiert bei Definition-Änderungen?**

```
┌────────────────────────────────────────────────────────────────┐
│ BLOCK UPDATE SCENARIOS                                         │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ 1. Komponente hinzugefügt:                                     │
│    Definition: [A, B] → [A, B, C]                              │
│    Visibility: {hidden: [1]} → {hidden: [1]}  ✓ OK             │
│                                                                │
│ 2. Komponente entfernt:                                        │
│    Definition: [A, B, C] → [A, C]                              │
│    Visibility: {hidden: [1]} → INVALID! B war Index 1          │
│    → Cleanup nötig: Index-Mapping oder Invalidierung           │
│                                                                │
│ 3. Komponenten neu angeordnet:                                 │
│    Definition: [A, B, C] → [C, A, B]                           │
│    Visibility: {hidden: [1]} → Zeigt auf falsches Object!      │
│    → Indices sind fragil                                       │
│                                                                │
│ LÖSUNG: Object-UUID statt Index verwenden                      │
│         const CRhinoObject* obj = idef->Object(i);             │
│         ON_UUID obj_id = obj->Id();                            │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Robuste Visibility-Storage:**

```cpp
// SCHLECHT: Index-basiert (fragil)
class VisibilityData_Bad : public ON_UserData {
    ON_SimpleArray<int> m_hidden_indices;
};

// GUT: UUID-basiert (robust gegen Änderungen)
class VisibilityData_Good : public ON_UserData {
    ON_UuidList m_hidden_component_ids;
    
    // Helper zum Konvertieren
    bool IsHidden(const CRhinoInstanceDefinition* idef, int index) const {
        const CRhinoObject* obj = idef->Object(index);
        return obj && m_hidden_component_ids.InList(obj->Id());
    }
};
```

---

## 2. C++ Implementation Guide

### 2.1 Display Pipeline Channel-Reihenfolge

```
┌─────────────────────────────────────────────────────────────────┐
│               DISPLAY PIPELINE CHANNELS (Reihenfolge)           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [Begin Drawing of a Frame]                                     │
│     SC_CALCBOUNDINGBOX          ← BBox für Camera/Clipping      │
│     SC_CALCCLIPPINGPLANES                                       │
│     SC_SETUPFRUSTUM                                             │
│     SC_SETUPLIGHTING                                            │
│     SC_INITFRAMEBUFFER                                          │
│     SC_DRAWBACKGROUND                                           │
│     SC_PREDRAWMIDDLEGROUND                                      │
│     SC_PREDRAWOBJECTS                                           │
│                                                                 │
│     ┌─────────────────────────────────────────────────────┐     │
│     │  FOR EACH VISIBLE NON-HIGHLIGHTED OBJECT:           │     │
│     │     SC_OBJECTDISPLAYATTRS  ← Attrs modifizieren     │     │
│     │     SC_PREOBJECTDRAW                                │     │
│     │     SC_DRAWOBJECT          ← ★ HIER INTERCEPTEN ★   │     │
│     │     SC_POSTOBJECTDRAW                               │     │
│     └─────────────────────────────────────────────────────┘     │
│                                                                 │
│     [Rhino draws highlighted objects]                           │
│     SC_PREDRAWTRANSPARENTOBJECTS                                │
│     [Rhino draws transparent objects]                           │
│     SC_POSTDRAWOBJECTS                                          │
│     SC_DRAWFOREGROUND                                           │
│                                                                 │
│     [Highlighted Object Loop - same channels]                   │
│                                                                 │
│     SC_POSTPROCESSFRAMEBUFFER                                   │
│     SC_DRAWOVERLAY                                              │
│  [End of Drawing of a Frame]                                    │
│                                                                 │
│  WICHTIG: SC_DRAWOBJECT kann MEHRFACH pro Object aufgerufen     │
│  werden (z.B. erst shaded mesh, dann isocurves)                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Custom DisplayConduit Implementation

```cpp
// PerInstanceVisibilityConduit.h
#pragma once
#include "stdafx.h"

// Forward declarations
class CComponentVisibilityData;

class CPerInstanceVisibilityConduit : public CRhinoDisplayConduit
{
public:
    CPerInstanceVisibilityConduit();
    virtual ~CPerInstanceVisibilityConduit() = default;
    
    // CRhinoDisplayConduit override
    bool ExecConduit(
        CRhinoDisplayPipeline& dp,
        UINT nChannel,
        bool& bTerminate
    ) override;
    
    // API für C#/extern
    void RegisterInstance(const ON_UUID& instance_id);
    void UnregisterInstance(const ON_UUID& instance_id);
    bool IsRegistered(const ON_UUID& instance_id) const;
    
private:
    // Managed instances (haben custom visibility)
    std::unordered_set<ON_UUID, UUIDHash> m_managed_instances;
    
    // Tracking für dieses Frame (verhindert Doppel-Draw)
    std::unordered_set<ON_UUID, UUIDHash> m_drawn_this_frame;
    
    // Internal methods
    bool DrawInstanceWithHiddenComponents(
        CRhinoDisplayPipeline& dp,
        const CRhinoInstanceObject* iobj,
        const CComponentVisibilityData* vis_data
    );
    
    void DrawSingleComponent(
        CRhinoDisplayPipeline& dp,
        const CRhinoObject* component,
        const ON_Xform& world_xform,
        bool is_selected
    );
    
    void DrawNestedInstance(
        CRhinoDisplayPipeline& dp,
        const CRhinoInstanceObject* nested,
        const ON_Xform& parent_xform,
        const CComponentVisibilityData* parent_vis,
        bool is_selected
    );
};
```

### 2.3 ExecConduit Implementation (Kern-Logik)

```cpp
// PerInstanceVisibilityConduit.cpp

bool CPerInstanceVisibilityConduit::ExecConduit(
    CRhinoDisplayPipeline& dp,
    UINT nChannel,
    bool& bTerminate
)
{
    // Nur SC_DRAWOBJECT interessiert uns
    if (nChannel != CSupportChannels::SC_DRAWOBJECT)
        return true;  // Weiter mit default
    
    // Frame-Tracking reset bei neuem Frame
    // (SC_INITFRAMEBUFFER wäre besser, aber wir sind nicht dort registriert)
    static UINT s_last_frame_number = 0;
    if (dp.FrameNumber() != s_last_frame_number) {
        s_last_frame_number = dp.FrameNumber();
        m_drawn_this_frame.clear();
    }
    
    // Aktuelles Object aus Channel Attributes
    const CRhinoObject* obj = m_pChannelAttrs->m_pObject;
    if (!obj)
        return true;
    
    // Ist es eine Block-Instanz?
    if (obj->ObjectType() != ON::instance_reference)
        return true;  // Kein Block, normal zeichnen
    
    const ON_UUID& obj_id = obj->Id();
    
    // Ist diese Instanz "managed" (hat custom visibility)?
    if (m_managed_instances.find(obj_id) == m_managed_instances.end())
        return true;  // Nicht managed, normal zeichnen
    
    // Schon gezeichnet dieses Frame? (wichtig für multi-pass rendering)
    if (m_drawn_this_frame.find(obj_id) != m_drawn_this_frame.end())
        return false;  // Skip, schon gezeichnet
    
    // Cast zu InstanceObject
    const CRhinoInstanceObject* iobj = 
        static_cast<const CRhinoInstanceObject*>(obj);
    
    // Visibility-Data von UserData holen
    const CComponentVisibilityData* vis_data = 
        CComponentVisibilityData::Get(iobj);
    
    if (!vis_data || !vis_data->HasHiddenComponents()) {
        // Keine hidden components, normal zeichnen lassen
        return true;
    }
    
    // ★ CUSTOM DRAW ★
    bool success = DrawInstanceWithHiddenComponents(dp, iobj, vis_data);
    
    if (success) {
        m_drawn_this_frame.insert(obj_id);
        return false;  // ★ Skip default draw! ★
    }
    
    return true;  // Fallback zu default bei Fehler
}
```

### 2.4 Component Drawing

```cpp
bool CPerInstanceVisibilityConduit::DrawInstanceWithHiddenComponents(
    CRhinoDisplayPipeline& dp,
    const CRhinoInstanceObject* iobj,
    const CComponentVisibilityData* vis_data
)
{
    const CRhinoInstanceDefinition* idef = iobj->InstanceDefinition();
    if (!idef)
        return false;
    
    // World transform dieser Instanz
    ON_Xform world_xform = iobj->InstanceXform();
    
    // Ist das Object selektiert? (für Highlight-Farbe)
    bool is_selected = (iobj->IsSelected() != 0);
    
    // Display Attributes für diese Instanz
    const CDisplayPipelineAttributes* orig_attrs = m_pDisplayAttrs;
    
    // Durch alle Komponenten iterieren
    for (int i = 0; i < idef->ObjectCount(); i++)
    {
        const CRhinoObject* component = idef->Object(i);
        if (!component)
            continue;
        
        // ★ VISIBILITY CHECK ★
        if (vis_data->IsHidden(component->Id()))
            continue;  // Diese Komponente überspringen!
        
        // Komponente zeichnen
        if (component->ObjectType() == ON::instance_reference)
        {
            // Nested block - rekursiv
            const CRhinoInstanceObject* nested = 
                static_cast<const CRhinoInstanceObject*>(component);
            DrawNestedInstance(dp, nested, world_xform, vis_data, is_selected);
        }
        else
        {
            // Reguläre Geometrie
            DrawSingleComponent(dp, component, world_xform, is_selected);
        }
    }
    
    return true;
}

void CPerInstanceVisibilityConduit::DrawSingleComponent(
    CRhinoDisplayPipeline& dp,
    const CRhinoObject* component,
    const ON_Xform& world_xform,
    bool is_selected
)
{
    // Farbe bestimmen
    ON_Color draw_color = ON_UNSET_COLOR;
    if (is_selected) {
        // Selection-Highlight Farbe
        draw_color = RhinoApp().AppSettings().SelectedObjectColor();
    }
    
    // Transform anwenden und zeichnen
    // CRhinoDisplayPipeline hat DrawObject das transforms akzeptiert
    dp.DrawObject(component, &world_xform, draw_color);
}
```

### 2.5 ON_UserData Implementation

```cpp
// ComponentVisibilityData.h
#pragma once
#include "stdafx.h"

// Unique UUID für diese UserData-Klasse
// Generate with: guidgen.exe oder online GUID generator
// {A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
#define COMPONENT_VISIBILITY_DATA_UUID \
    { 0xa1b2c3d4, 0xe5f6, 0x7890, { 0xab, 0xcd, 0xef, 0x12, 0x34, 0x56, 0x78, 0x90 } }

class CComponentVisibilityData : public ON_UserData
{
    ON_OBJECT_DECLARE(CComponentVisibilityData);
    
public:
    static const ON_UUID m_uuid;
    
    CComponentVisibilityData();
    CComponentVisibilityData(const CComponentVisibilityData& src);
    virtual ~CComponentVisibilityData() = default;
    
    CComponentVisibilityData& operator=(const CComponentVisibilityData& src);
    
    // ON_UserData overrides
    ON_UUID UserDataClassUuid() const override { return m_uuid; }
    bool Archive() const override { return true; }  // Persistieren!
    bool Write(ON_BinaryArchive& archive) const override;
    bool Read(ON_BinaryArchive& archive) override;
    bool GetDescription(ON_wString& description) override;
    
    // API
    void HideComponent(const ON_UUID& component_id);
    void ShowComponent(const ON_UUID& component_id);
    void ToggleComponent(const ON_UUID& component_id);
    bool IsHidden(const ON_UUID& component_id) const;
    bool HasHiddenComponents() const;
    int HiddenCount() const;
    
    void GetHiddenIds(ON_SimpleArray<ON_UUID>& ids) const;
    void ClearAll();
    
    // Static helper
    static CComponentVisibilityData* Get(const CRhinoObject* obj);
    static CComponentVisibilityData* GetOrCreate(CRhinoObject* obj);
    
private:
    ON_UuidList m_hidden_component_ids;
};

// Implementation
ON_OBJECT_IMPLEMENT(CComponentVisibilityData, ON_UserData, "ComponentVisibilityData");
const ON_UUID CComponentVisibilityData::m_uuid = COMPONENT_VISIBILITY_DATA_UUID;

CComponentVisibilityData::CComponentVisibilityData()
{
    m_userdata_uuid = m_uuid;
    
    // WICHTIG: Application UUID setzen für Persistierung
    // Sollte die Plugin-GUID sein
    m_application_uuid = RhinoAssemblyOutlinerPlugIn().PlugInID();
}

bool CComponentVisibilityData::Write(ON_BinaryArchive& archive) const
{
    // Version schreiben für zukünftige Kompatibilität
    if (!archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, 1, 0))
        return false;
    
    bool rc = false;
    for (;;)
    {
        // Anzahl hidden components
        int count = m_hidden_component_ids.Count();
        if (!archive.WriteInt(count))
            break;
        
        // UUIDs schreiben
        for (int i = 0; i < count; i++)
        {
            ON_UUID id = ON_nil_uuid;
            m_hidden_component_ids.GetUuids(&id);  // Simplified
            if (!archive.WriteUuid(m_hidden_component_ids[i]))
                break;
        }
        
        rc = true;
        break;
    }
    
    if (!archive.EndWrite3dmChunk())
        rc = false;
    
    return rc;
}

bool CComponentVisibilityData::Read(ON_BinaryArchive& archive)
{
    m_hidden_component_ids.Empty();
    
    int major_version = 0, minor_version = 0;
    if (!archive.BeginRead3dmChunk(TCODE_ANONYMOUS_CHUNK, &major_version, &minor_version))
        return false;
    
    bool rc = false;
    for (;;)
    {
        if (major_version != 1)
            break;
        
        int count = 0;
        if (!archive.ReadInt(&count))
            break;
        
        for (int i = 0; i < count; i++)
        {
            ON_UUID id;
            if (!archive.ReadUuid(id))
                break;
            m_hidden_component_ids.AddUuid(id, true);
        }
        
        rc = true;
        break;
    }
    
    if (!archive.EndRead3dmChunk())
        rc = false;
    
    return rc;
}

// Static helper
CComponentVisibilityData* CComponentVisibilityData::Get(const CRhinoObject* obj)
{
    if (!obj)
        return nullptr;
    return static_cast<CComponentVisibilityData*>(
        obj->GetUserData(m_uuid)
    );
}

CComponentVisibilityData* CComponentVisibilityData::GetOrCreate(CRhinoObject* obj)
{
    if (!obj)
        return nullptr;
    
    CComponentVisibilityData* data = Get(obj);
    if (!data)
    {
        data = new CComponentVisibilityData();
        if (!obj->AttachUserData(data))
        {
            delete data;
            return nullptr;
        }
    }
    return data;
}
```

### 2.6 Exported API für C#

```cpp
// NativeAPI.h
#pragma once

#ifdef RHINOASSEMBLYOUTLINER_EXPORTS
#define NATIVE_API __declspec(dllexport)
#else
#define NATIVE_API __declspec(dllimport)
#endif

extern "C" {

// Conduit Management
NATIVE_API bool EnableVisibilityConduit();
NATIVE_API void DisableVisibilityConduit();
NATIVE_API bool IsConduitEnabled();

// Instance Registration  
NATIVE_API bool RegisterManagedInstance(const ON_UUID* instance_id);
NATIVE_API bool UnregisterManagedInstance(const ON_UUID* instance_id);
NATIVE_API bool IsInstanceManaged(const ON_UUID* instance_id);

// Component Visibility
NATIVE_API bool SetComponentVisibility(
    const ON_UUID* instance_id,
    const ON_UUID* component_id,
    bool visible
);

NATIVE_API bool ToggleComponentVisibility(
    const ON_UUID* instance_id,
    const ON_UUID* component_id
);

NATIVE_API bool IsComponentVisible(
    const ON_UUID* instance_id,
    const ON_UUID* component_id
);

NATIVE_API int GetHiddenComponentCount(const ON_UUID* instance_id);

// Batch operations
NATIVE_API bool ShowAllComponents(const ON_UUID* instance_id);
NATIVE_API bool HideAllComponents(const ON_UUID* instance_id);

// Info
NATIVE_API int GetComponentCount(const ON_UUID* instance_id);
NATIVE_API bool GetComponentInfo(
    const ON_UUID* instance_id,
    int index,
    ON_UUID* out_component_id,
    wchar_t* out_name,
    int name_buffer_size,
    int* out_object_type
);

}  // extern "C"
```

---

## 3. UX/Workflow Design

### 3.1 SolidWorks FeatureManager Referenz

```
┌─────────────────────────────────────────────────────────────────┐
│              SOLIDWORKS FEATUREMANAGER UX                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  VISIBILITY INDICATORS:                                         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ 👁️ Oberschrank_600     ← Sichtbar (filled eye)           │   │
│  │ 👁️ ├─ Seitenwand_L     ← Sichtbar                        │   │
│  │ 👁️ ├─ Seitenwand_R     ← Sichtbar                        │   │
│  │ 〰️ ├─ Rückwand         ← HIDDEN (empty eye / strikeout)  │   │
│  │ 👁️ └─ Scharnier ×2     ← Sichtbar                        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
│  INTERACTION PATTERNS:                                          │
│                                                                 │
│  • Single Click Eye Icon: Toggle visibility                     │
│  • Right-Click → "Hide":  Hide selected                         │
│  • Right-Click → "Show":  Show selected                         │
│  • Right-Click → "Isolate": Hide ALL except selected            │
│  • Tab Key (in context):  Cycle through visibility states       │
│                                                                 │
│  VISUAL FEEDBACK:                                               │
│                                                                 │
│  • Hidden items: Grayed out / italic text / empty eye icon      │
│  • Viewport:     Hidden = invisible (not ghosted)               │
│  • Mixed state:  Parent shows "partial" indicator               │
│                                                                 │
│  KEYBOARD SHORTCUTS:                                            │
│  • H:        Hide selected                                      │
│  • Ctrl+H:   Show hidden (dialog)                               │
│  • Tab:      Cycle visibility                                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 Unser Workflow Design

```
┌─────────────────────────────────────────────────────────────────┐
│              RHINO ASSEMBLY OUTLINER UX                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  PANEL LAYOUT:                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │ 🔍 [Search...]                              [⚙️] [📋]   │    │
│  ├─────────────────────────────────────────────────────────┤    │
│  │                                                         │    │
│  │ 📄 Küche_Assembly.3dm                                   │    │
│  │ │                                                       │    │
│  │ ├─ 👁️ 📦 Oberschrank_600 #1                             │    │
│  │ │   ├─ 👁️ ⬡ Korpus                                      │    │
│  │ │   ├─ 👁️ 📦 Scharnier_Blum ×2                          │    │
│  │ │   │   ├─ 👁️ ⬡ Topf                                    │    │
│  │ │   │   └─ 👁️ ⬡ Arm                                     │    │
│  │ │   ├─ 〰️ ⬡ Rückwand          ← HIDDEN                  │    │
│  │ │   └─ 👁️ ⬡ Tür                                         │    │
│  │ │                                                       │    │
│  │ ├─ 👁️ 📦 Oberschrank_600 #2    ← Gleiche Def, andere    │    │
│  │ │   ├─ 👁️ ⬡ Korpus                Visibility möglich!  │    │
│  │ │   └─ ...                                              │    │
│  │                                                         │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                 │
│  ICONS LEGENDE:                                                 │
│  👁️  = Sichtbar (clickable toggle)                              │
│  〰️  = Hidden (strikethrough eye)                               │
│  📦  = Block Instance                                           │
│  ⬡   = Geometry (Brep, Mesh, etc.)                              │
│  📄  = Document Root                                            │
│                                                                 │
│  CONTEXT MENU (Right-Click auf Komponente):                     │
│  ┌────────────────────────────────┐                             │
│  │ 👁️ Show                        │                             │
│  │ 〰️ Hide                        │                             │
│  │ ─────────────────────────────  │                             │
│  │ 🎯 Isolate                     │ ← Nur diese sichtbar        │
│  │ 🔄 Isolate Off                 │ ← Alles wieder zeigen       │
│  │ ─────────────────────────────  │                             │
│  │ 🔍 Zoom to                     │                             │
│  │ ✏️ Select in Viewport          │                             │
│  │ ─────────────────────────────  │                             │
│  │ 📋 Select All Same Definition  │ ← Alle Instanzen dieser Def │
│  │ ─────────────────────────────  │                             │
│  │ ⚙️ Edit Block                  │ ← BlockEdit starten         │
│  └────────────────────────────────┘                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.3 Operationen im Detail

#### Hide/Show
```
USER ACTION:                   SYSTEM RESPONSE:
─────────────────────────────────────────────────────────────────
Click Eye Icon on            → Toggle visibility für DIESE Instanz
"Rückwand" in Outliner         - UserData auf Instance updaten
                               - Conduit invalidiert Viewport
                               - Icon ändert zu 〰️
                               - Viewport: Komponente verschwindet

Right-Click → "Hide"         → Gleich wie Eye-Click
auf selektierter Komponente

Keyboard "H" mit             → Hide all selected components
Selection im Outliner          (kann mehrere sein)
```

#### Isolate
```
USER ACTION:                   SYSTEM RESPONSE:
─────────────────────────────────────────────────────────────────
Right-Click → "Isolate"      → 1. Alle anderen Komponenten der
auf "Scharnier"                   selben INSTANCE → hidden
                              2. Die isolierte Komponente → visible
                              3. Status "Isolated" merken
                              4. UI zeigt "Isolation Mode" indicator

"Isolate Off" oder ESC       → Alle Komponenten wieder visible
                               (zurück zum vorherigen State)
```

#### Edit In Place (Future)
```
USER ACTION:                   SYSTEM RESPONSE:
─────────────────────────────────────────────────────────────────
Double-Click auf             → 1. BlockEdit Command starten
Block-Instance im Outliner      2. Outliner zeigt Definition-Contents
                                3. Änderungen an Definition
                                4. Exit → alle Instances updaten

Context Menu → "Edit Block"  → Gleich wie Double-Click
```

### 3.4 Visual Feedback States

```
┌─────────────────────────────────────────────────────────────────┐
│              VISUAL STATES                                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  OUTLINER ITEM STATES:                                          │
│                                                                 │
│  ┌─────────────────────────────────────────────────┐            │
│  │ 👁️ Normal_Component                             │ ← Default  │
│  │ 〰️ Hidden_Component      (grayed, italic)       │ ← Hidden   │
│  │ ⚡ Selected_Component    (highlighted bg)       │ ← Selected │
│  │ 🎯 Isolated_Component    (bold, colored)        │ ← Isolated │
│  │ 🔒 Locked_Component      (lock icon overlay)    │ ← Locked   │
│  └─────────────────────────────────────────────────┘            │
│                                                                 │
│  PARENT WITH MIXED CHILDREN:                                    │
│  ┌─────────────────────────────────────────────────┐            │
│  │ ◐ Block_Instance       (half-filled eye)        │            │
│  │ ├─ 👁️ Visible_Child                             │            │
│  │ ├─ 〰️ Hidden_Child                              │            │
│  │ └─ 👁️ Visible_Child                             │            │
│  └─────────────────────────────────────────────────┘            │
│                                                                 │
│  VIEWPORT FEEDBACK:                                             │
│                                                                 │
│  • Hidden Component:     Completely invisible                   │
│  • Selected Component:   Yellow highlight (native Rhino)        │
│  • Hovered (from tree):  Optional: Temporary highlight          │
│                                                                 │
│  OPTION: "Ghost Mode" for hidden (semi-transparent)             │
│  → User preference, default = fully hidden                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. Integration Architecture (C++ ↔ C#)

### 4.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    PLUGIN ARCHITECTURE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    RHINO 8                               │    │
│  │  ┌──────────────────────┐  ┌──────────────────────────┐ │    │
│  │  │                      │  │                          │ │    │
│  │  │  C# Plugin (.rhp)    │  │  C++ Plugin (.rhp)       │ │    │
│  │  │  ══════════════════  │  │  ══════════════════════  │ │    │
│  │  │                      │  │                          │ │    │
│  │  │  • UI (Eto.Forms)    │  │  • DisplayConduit        │ │    │
│  │  │  • Tree View         │  │  • Custom Drawing        │ │    │
│  │  │  • Commands          │  │  • UserData Management   │ │    │
│  │  │  • Event Handling    │  │  • Cache Management      │ │    │
│  │  │                      │  │                          │ │    │
│  │  │       │              │  │       ▲                  │ │    │
│  │  │       │ P/Invoke     │  │       │ Exports          │ │    │
│  │  │       ▼              │  │       │                  │ │    │
│  │  │  ┌────────────────┐  │  │  ┌────────────────────┐  │ │    │
│  │  │  │ NativeWrapper  │◄─┼──┼─►│ extern "C" API     │  │ │    │
│  │  │  └────────────────┘  │  │  └────────────────────┘  │ │    │
│  │  │                      │  │                          │ │    │
│  │  └──────────────────────┘  └──────────────────────────┘ │    │
│  │                                                         │    │
│  │  ┌─────────────────────────────────────────────────────┐│    │
│  │  │              Shared State (per Document)            ││    │
│  │  │  • Managed Instance Registry                        ││    │
│  │  │  • Visibility State (in UserData on Objects)        ││    │
│  │  └─────────────────────────────────────────────────────┘│    │
│  │                                                         │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 C# P/Invoke Wrapper

```csharp
// NativeVisibilityAPI.cs
using System;
using System.Runtime.InteropServices;

namespace RhinoAssemblyOutliner.Native
{
    /// <summary>
    /// P/Invoke wrapper for the C++ native visibility conduit.
    /// </summary>
    public static class NativeVisibilityAPI
    {
        private const string DllName = "RhinoAssemblyOutliner.Native.rhp";
        
        #region Conduit Management
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool EnableVisibilityConduit();
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DisableVisibilityConduit();
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsConduitEnabled();
        
        #endregion
        
        #region Instance Registration
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool RegisterManagedInstance(ref Guid instanceId);
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool UnregisterManagedInstance(ref Guid instanceId);
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsInstanceManaged(ref Guid instanceId);
        
        #endregion
        
        #region Component Visibility
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SetComponentVisibility(
            ref Guid instanceId,
            ref Guid componentId,
            [MarshalAs(UnmanagedType.I1)] bool visible
        );
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ToggleComponentVisibility(
            ref Guid instanceId,
            ref Guid componentId
        );
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsComponentVisible(
            ref Guid instanceId,
            ref Guid componentId
        );
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetHiddenComponentCount(ref Guid instanceId);
        
        #endregion
        
        #region Public API (Thread-Safe, Exception-Safe)
        
        public static bool Enable()
        {
            try { return EnableVisibilityConduit(); }
            catch { return false; }
        }
        
        public static void Disable()
        {
            try { DisableVisibilityConduit(); }
            catch { /* ignore */ }
        }
        
        public static bool SetVisibility(Guid instanceId, Guid componentId, bool visible)
        {
            try 
            { 
                return SetComponentVisibility(ref instanceId, ref componentId, visible); 
            }
            catch { return false; }
        }
        
        public static bool Toggle(Guid instanceId, Guid componentId)
        {
            try 
            { 
                return ToggleComponentVisibility(ref instanceId, ref componentId); 
            }
            catch { return false; }
        }
        
        public static bool IsVisible(Guid instanceId, Guid componentId)
        {
            try 
            { 
                return IsComponentVisible(ref instanceId, ref componentId); 
            }
            catch { return true; }  // Default to visible on error
        }
        
        #endregion
    }
}
```

### 4.3 C# Service Layer

```csharp
// VisibilityService.cs
using Rhino;
using Rhino.DocObjects;
using RhinoAssemblyOutliner.Native;

namespace RhinoAssemblyOutliner.Services
{
    /// <summary>
    /// High-level service for managing per-instance component visibility.
    /// Wraps native API with RhinoCommon integration.
    /// </summary>
    public class VisibilityService : IDisposable
    {
        private readonly RhinoDoc _doc;
        private bool _conduitEnabled = false;
        
        public VisibilityService(RhinoDoc doc)
        {
            _doc = doc;
        }
        
        public void EnsureConduitEnabled()
        {
            if (!_conduitEnabled)
            {
                _conduitEnabled = NativeVisibilityAPI.Enable();
            }
        }
        
        /// <summary>
        /// Hide a component within a specific block instance.
        /// </summary>
        public bool HideComponent(InstanceObject instance, int componentIndex)
        {
            EnsureConduitEnabled();
            
            var idef = instance.InstanceDefinition;
            if (idef == null || componentIndex >= idef.ObjectCount)
                return false;
            
            var component = idef.Object(componentIndex);
            if (component == null)
                return false;
            
            bool success = NativeVisibilityAPI.SetVisibility(
                instance.Id, 
                component.Id, 
                visible: false
            );
            
            if (success)
            {
                _doc.Views.Redraw();
                OnVisibilityChanged?.Invoke(instance.Id, component.Id, false);
            }
            
            return success;
        }
        
        /// <summary>
        /// Show a previously hidden component.
        /// </summary>
        public bool ShowComponent(InstanceObject instance, int componentIndex)
        {
            var idef = instance.InstanceDefinition;
            if (idef == null || componentIndex >= idef.ObjectCount)
                return false;
            
            var component = idef.Object(componentIndex);
            if (component == null)
                return false;
            
            bool success = NativeVisibilityAPI.SetVisibility(
                instance.Id, 
                component.Id, 
                visible: true
            );
            
            if (success)
            {
                _doc.Views.Redraw();
                OnVisibilityChanged?.Invoke(instance.Id, component.Id, true);
            }
            
            return success;
        }
        
        /// <summary>
        /// Isolate: Show only the specified component, hide all others.
        /// </summary>
        public void Isolate(InstanceObject instance, int componentIndex)
        {
            EnsureConduitEnabled();
            
            var idef = instance.InstanceDefinition;
            if (idef == null)
                return;
            
            for (int i = 0; i < idef.ObjectCount; i++)
            {
                var component = idef.Object(i);
                if (component == null) continue;
                
                NativeVisibilityAPI.SetVisibility(
                    instance.Id,
                    component.Id,
                    visible: (i == componentIndex)
                );
            }
            
            _doc.Views.Redraw();
        }
        
        /// <summary>
        /// Show all components of an instance.
        /// </summary>
        public void ShowAll(InstanceObject instance)
        {
            var idef = instance.InstanceDefinition;
            if (idef == null)
                return;
            
            for (int i = 0; i < idef.ObjectCount; i++)
            {
                var component = idef.Object(i);
                if (component == null) continue;
                
                NativeVisibilityAPI.SetVisibility(
                    instance.Id,
                    component.Id,
                    visible: true
                );
            }
            
            _doc.Views.Redraw();
        }
        
        // Event for UI updates
        public event Action<Guid, Guid, bool>? OnVisibilityChanged;
        
        public void Dispose()
        {
            if (_conduitEnabled)
            {
                NativeVisibilityAPI.Disable();
                _conduitEnabled = false;
            }
        }
    }
}
```

### 4.4 Communication Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│              DATA FLOW                                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  USER CLICKS "HIDE" IN OUTLINER                                 │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                            │
│  │   C# UI Layer   │  TreeView.NodeClick event                  │
│  └────────┬────────┘                                            │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                            │
│  │VisibilityService│  HideComponent(instance, componentIndex)   │
│  └────────┬────────┘                                            │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                            │
│  │NativeVisibilityAPI│ P/Invoke: SetComponentVisibility(...)   │
│  └────────┬────────┘                                            │
│           │                                                     │
│           │ ══════ PROCESS BOUNDARY (Managed → Native) ══════   │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                            │
│  │  C++ API Layer  │  SetComponentVisibility()                  │
│  └────────┬────────┘                                            │
│           │                                                     │
│           ├──────────────────────────────────┐                  │
│           │                                  │                  │
│           ▼                                  ▼                  │
│  ┌─────────────────┐                ┌─────────────────┐         │
│  │   Conduit       │                │   UserData      │         │
│  │   Registry      │                │   on Instance   │         │
│  │                 │                │                 │         │
│  │ managed_ids.    │                │ m_hidden_ids.   │         │
│  │   insert(id)    │                │   AddUuid(...)  │         │
│  └─────────────────┘                └─────────────────┘         │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                            │
│  │ Viewport Redraw │  CRhinoDoc::Redraw()                       │
│  └────────┬────────┘                                            │
│           │                                                     │
│           ▼                                                     │
│  ┌─────────────────┐                                            │
│  │  ExecConduit()  │  Conduit intercepts draw, skips component  │
│  └─────────────────┘                                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Edge Cases & Challenges

### 5.1 Known Challenges

```
┌─────────────────────────────────────────────────────────────────┐
│              EDGE CASES & CHALLENGES                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│ 1. SELECTION HANDLING                                           │
│    ──────────────────                                           │
│    Problem:  Wenn wir das Objekt nicht normal zeichnen,         │
│              weiss Rhino nicht wo es für Selection ist.         │
│                                                                 │
│    Lösung:   Block-Instanz bleibt SELEKTIERBAR (wir zeichnen    │
│              nur anders). Selection trifft immer den Block,     │
│              nicht einzelne Komponenten.                        │
│                                                                 │
│    Future:   Sub-object selection könnte mit                    │
│              SC_OBJECTDISPLAYATTRS + Custom hit testing gehen   │
│                                                                 │
│ 2. BOUNDING BOX                                                 │
│    ───────────                                                  │
│    Problem:  Wenn Komponenten hidden sind, ändert sich die      │
│              effektive BBox. Zoom Extents etc. könnten falsch.  │
│                                                                 │
│    Lösung:   SC_CALCBOUNDINGBOX Channel subscriben und          │
│              angepasste BBox berechnen. ODER: ignorieren        │
│              (BBox bleibt wie Original-Block, akzeptabel).      │
│                                                                 │
│ 3. MATERIALS & DISPLAY MODES                                    │
│    ─────────────────────────                                    │
│    Problem:  Komponenten haben Materials, Display Mode          │
│              (Wireframe/Shaded) muss respektiert werden.        │
│                                                                 │
│    Lösung:   DrawObject() mit korrekten Pipeline-Attributen     │
│              aufrufen. dp.DrawingSurfaces() / dp.DrawingWires() │
│              prüfen für aktuellen Modus.                        │
│                                                                 │
│ 4. NESTED BLOCKS MIT EIGENER VISIBILITY                         │
│    ────────────────────────────────────                         │
│    Problem:  Block A enthält Block B. Beide haben custom        │
│              visibility. Wie kombinieren?                       │
│                                                                 │
│    Lösung:   Rekursiv prüfen. Wenn Parent-Komponente hidden,    │
│              sind alle Children auch hidden.                    │
│              Visibility ist "hierarchisch-additiv".             │
│                                                                 │
│ 5. LINKED BLOCKS                                                │
│    ─────────────                                                │
│    Problem:  Linked Blocks (.3dm Reference) - Definition        │
│              kommt aus externer Datei.                          │
│                                                                 │
│    Lösung:   Funktioniert gleich! Definition ist zur Laufzeit   │
│              geladen, UserData ist auf der lokalen INSTANZ.     │
│                                                                 │
│ 6. BLOCK UPDATES / REDEFINITION                                 │
│    ────────────────────────────                                 │
│    Problem:  User ändert Block-Definition (BlockEdit).          │
│              Komponenten-IDs könnten sich ändern.               │
│                                                                 │
│    Lösung:   Event-Handler für RhinoDoc.InstanceDefinition-     │
│              TableEvent. Bei Änderungen:                        │
│              - Prüfen ob hidden IDs noch existieren             │
│              - Ungültige IDs aus UserData entfernen             │
│              - UI refreshen                                     │
│                                                                 │
│ 7. MULTI-PASS RENDERING                                         │
│    ────────────────────                                         │
│    Problem:  SC_DRAWOBJECT wird mehrfach pro Objekt pro Frame   │
│              aufgerufen (Shaded Pass, Wire Pass, etc.)          │
│                                                                 │
│    Lösung:   Per-Frame tracking: m_drawn_this_frame Set.        │
│              Reset bei neuem Frame (check dp.FrameNumber())     │
│                                                                 │
│ 8. PERFORMANCE MIT VIELEN MANAGED INSTANCES                     │
│    ────────────────────────────────────────                     │
│    Problem:  Jede Instanz wird individuell gerendert statt      │
│              mit Rhino's optimiertem Block-Drawing.             │
│                                                                 │
│    Lösung:   - Nur Instanzen mit AKTIVER custom visibility      │
│                als "managed" registrieren                       │
│              - HashSet für O(1) lookups                         │
│              - Display Lists / Caching für Komponenten          │
│                (CRhinoCacheHandle)                              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 Risiko-Matrix

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| C++ kann es auch nicht | Hoch | Niedrig | Früh testen mit Minimal-PoC |
| Ghost Artifacts (wie C#) | Hoch | Niedrig | `return false` in SC_DRAWOBJECT |
| Selection broken | Mittel | Niedrig | Block bleibt selektierbar |
| Performance-Probleme | Mittel | Mittel | Lazy registration, caching |
| Build-Komplexität | Mittel | Mittel | CI/CD, klare Doku |
| Rhino-Version-Inkompatibilität | Mittel | Niedrig | Nur Rhino 8 targeten |

---

## 6. Implementation Roadmap

### Phase 1: C++ Minimal PoC (1 Woche)

**Ziel:** Beweisen dass `return false` in SC_DRAWOBJECT funktioniert

```
□ C++ SDK Setup (VS2022, Rhino 8 SDK)
□ Hello World C++ Plugin
□ Minimal DisplayConduit
  □ SC_DRAWOBJECT subscriben
  □ Hardcoded: Wenn Object.Name == "TestBlock" → return false
  □ Verifizieren: Objekt verschwindet OHNE Ghost Artifacts
□ Custom Draw Test
  □ Statt return false: DrawObject() mit Transform
  □ Verifizieren: Objekt erscheint korrekt
```

### Phase 2: Core Visibility System (2 Wochen)

**Ziel:** Funktionierendes Visibility-System ohne UI

```
□ ON_UserData Implementation
  □ CComponentVisibilityData Klasse
  □ Read/Write für Persistierung
  □ Test: Speichern/Laden funktioniert
□ Conduit mit Visibility-Logik
  □ Managed Instance Registry
  □ Component-Level Visibility Check
  □ Korrekte Multi-Pass Handling
□ Extern C API
  □ SetComponentVisibility()
  □ GetComponentVisibility()
  □ RegisterManagedInstance()
□ Test Command
  □ Hardcoded Test ohne UI
```

### Phase 3: C# Integration (1 Woche)

**Ziel:** C# kann Visibility steuern

```
□ P/Invoke Wrapper
  □ NativeVisibilityAPI.cs
  □ Exception handling
  □ Thread safety
□ VisibilityService
  □ High-level API
  □ Event system für UI updates
□ Integration Tests
  □ Hide/Show funktioniert
  □ Persistierung funktioniert
```

### Phase 4: UI Integration (2 Wochen)

**Ziel:** Outliner mit Visibility-Controls

```
□ Eye Icons im Tree
  □ Click handler
  □ State display
□ Context Menu
  □ Hide/Show/Isolate
□ Keyboard Shortcuts
  □ H = Hide
  □ S = Show
□ Visual Feedback
  □ Grayed items für hidden
  □ Parent partial indicators
```

### Phase 5: Polish & Edge Cases (1 Woche)

```
□ Block Update Handling
□ Nested Block Visibility
□ Performance Optimization
□ Documentation
□ Beta Testing
```

---

## Appendix: Code Snippets aus SDK Samples

### A.1 Highlighting Objects (aus Rhino SDK)

```cpp
// Von: developer.rhino3d.com/guides/cpp/highlighting-objects-in-conduits/

class CTestHighlightCurveConduit : public CRhinoDisplayConduit
{
public:
    CTestHighlightCurveConduit()
        : CRhinoDisplayConduit(CSupportChannels::SC_DRAWOBJECT) {}
    
    bool ExecConduit(
        CRhinoDisplayPipeline& dp,
        UINT nChannel,
        bool& bTerminate
    ) override
    {
        if (nChannel == CSupportChannels::SC_DRAWOBJECT)
        {
            if (m_pChannelAttrs->m_pObject->m_runtime_object_serial_number 
                == m_target_serial_number)
            {
                // Farbe überschreiben
                m_pDisplayAttrs->m_ObjectColor = RGB(255, 105, 180);
            }
        }
        return true;
    }
    
    unsigned int m_target_serial_number;
};
```

### A.2 Dynamic Block Insertion (aus Rhino SDK)

```cpp
// Von: developer.rhino3d.com/guides/cpp/dynamically-inserting-blocks/

void CGetBlockInsertPoint::DynamicDraw(
    HDC hdc,
    CRhinoViewport& vp,
    const ON_3dPoint& pt
)
{
    if (m_idef && m_bDraw)
    {
        CRhinoDisplayPipeline* dp = vp.DisplayPipeline();
        if (dp)
        {
            dp->PushObjectColor(0);
            
            // ★ KRITISCH: DrawObject kann ganze Definition zeichnen! ★
            dp->DrawObject(m_idef, &m_xform);
            
            dp->PopObjectColor();
        }
    }
    CRhinoGetPoint::DynamicDraw(hdc, vp, pt);
}
```

---

## 7. UX Recommendations (Post-Research)

> Basierend auf der Analyse von SolidWorks, Inventor, Fusion 360, CATIA und Siemens NX.  
> Siehe: `research/SYNTHESIS_RECOMMENDATIONS.md` für vollständige Details.

### 7.1 Industrie-Standard Patterns übernehmen

#### Eye Icon Convention (UNIVERSAL)
```
👁️  = Sichtbar (ausgefülltes Auge)
〰️  = Hidden (durchgestrichenes Auge)
◐   = Gemischt (Parent mit hidden + visible children)
```
**Implementierung:** Klickbares Icon in Tree-Spalte, 1-Click Toggle

#### Visual Feedback für Hidden Items
- **Icon:** Ausgegraut (dimmed)
- **Text:** Grau oder kursiv
- **Im Tree belassen** (nicht ausblenden wie SolidWorks' "Show Hidden" Mode)

#### Keyboard Shortcuts (SolidWorks-inspiriert)
| Shortcut | Aktion | Priorität |
|----------|--------|-----------|
| **H** | Hide selected | MVP |
| **Shift+H** | Show selected | MVP |
| **I** | Isolate selected | MVP |
| **Esc** | Exit Isolate | MVP |
| **Tab** | Cycle visibility (future) | v2 |

### 7.2 Context Menu Struktur

```
Right-Click auf Komponente:
├── 👁️ Show
├── 〰️ Hide  
├── ─────────────────────
├── 🎯 Isolate
├── 🔄 Show All Components
├── ─────────────────────
├── 🔍 Zoom to
├── ✏️ Select in Viewport
├── ─────────────────────
├── 📋 Select All Same Definition
└── ⚙️ Edit Block (→ BlockEdit)
```

### 7.3 Isolate Pattern

**Flow:**
1. User selektiert Komponente(n) im Tree
2. Click "Isolate" im Context Menu
3. **Alle ANDEREN** Komponenten der gleichen Instance werden hidden
4. UI zeigt "Isolation Mode" Indikator
5. "Isolate Off" oder **ESC** → Alles wieder sichtbar

**Wichtig:** Isolate-State ist **temporär** (nicht persistiert)

### 7.4 Rhino-Adaptionen

| CAD-Pattern | Rhino-Anpassung |
|-------------|-----------------|
| Display States | Via **Layer States** approximieren (v1), Custom States (v2) |
| Edit in Context | Integration mit `BlockEdit` Command |
| Configurations | Nicht emulieren - ist Grasshopper-Territorium |
| Suppress | Nicht nötig - wir machen nur Visual Hiding |

### 7.5 Zweistufiges Visibility-Modell

```
┌─────────────────────────────────────────┐
│      Layer Visibility (Rhino-native)    │
│              (übergeordnet)             │
└───────────────────┬─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│   Per-Instance Visibility (unser Feature)│
│           (komponenten-level)           │
└─────────────────────────────────────────┘
                    │
                    ▼
            RESULTAT: Sichtbar?
```

**Regel:** Beide müssen "visible" sein für Sichtbarkeit.
- Layer hidden → Komponente hidden (wir können nicht überschreiben)
- Layer visible → Unsere per-instance Visibility entscheidet

### 7.6 Display States für v2

**Konzept: Named Visibility States**

```csharp
public class VisibilityState {
    public string Name { get; set; }
    public Dictionary<Guid, HashSet<Guid>> HiddenComponents { get; set; }
    // Key: Instance ID, Value: Set of hidden component IDs
}
```

**UI:**
- Dropdown in Toolbar: "Default", "Exploded View", "Interior Only"
- "Save Current State" Button
- "Manage States..." Dialog

**Storage:** Document-Level UserData

---

*Dokument erstellt: 2026-02-05*  
*Letzte Aktualisierung: 2026-02-05 (UX Recommendations hinzugefügt)*
