# C++ SDK Research: Per-Instance Component Visibility

## Key Discoveries

### 1. SDK Setup Requirements

**Für Rhino 8 C++ Plugins:**
- Visual Studio 2022 oder 2019
- **MSVC v142 (VS 2019) Toolset** erforderlich
- C++ v14.2 ATL + MFC für v142 build tools
- Windows 10 SDK

**Downloads:**
- [Rhino 8 C++ SDK](https://www.rhino3d.com/download/rhino-sdk/8/latest/)
- [Rhino Visual Studio Extension (VSIX)](https://github.com/mcneel/RhinoVisualStudioExtensions/releases)

---

### 2. Block/Instance Architektur

```cpp
// InstanceDefinition = Block Definition (Template)
// InstanceObject = Block Instance (Placed copy with transform)

const CRhinoInstanceDefinition* idef;  // Die Definition
const CRhinoInstanceObject* iobj;       // Eine Instanz davon

// Definition → Komponenten
int count = idef->ObjectCount();
for (int i = 0; i < count; i++)
{
    const CRhinoObject* component = idef->Object(i);
    // component ist ein Brep, Mesh, Curve, oder nested Instance
}

// Instanz → Transform
ON_Xform xform = iobj->InstanceXform();
```

---

### 3. DisplayPipeline Methoden für Blocks

**KRITISCH:** `CRhinoDisplayPipeline` kann Definitions direkt zeichnen:

```cpp
// Ganze Definition mit Transform zeichnen
dp->DrawObject(idef, &xform);

// Einzelne Geometrie zeichnen
dp->DrawBrep(brep, wireColor, wireDensity, edgeAnalysis, cache);
dp->DrawMesh(mesh, wires, shaded, cache);
dp->DrawCurve(curve, color, thickness, cache);
dp->DrawExtrusion(extrusion, wireColor, wireDensity, edgeAnalysis, cache);
```

**Das ist der Schlüssel!** Wir können:
1. Die Definition iterieren
2. Einzelne Komponenten transformieren
3. Nur sichtbare Komponenten zeichnen

---

### 4. DisplayConduit Channels

```cpp
class CMyConduit : public CRhinoDisplayConduit
{
public:
    CMyConduit() 
        : CRhinoDisplayConduit(
            CSupportChannels::SC_DRAWOBJECT |  // Intercept object drawing
            CSupportChannels::SC_CALCBOUNDINGBOX
        ) {}

    bool ExecConduit(
        CRhinoDisplayPipeline& dp,
        UINT nChannel,
        bool& bTerminate
    ) override
    {
        switch (nChannel)
        {
        case CSupportChannels::SC_DRAWOBJECT:
            // m_pChannelAttrs->m_pObject ist das aktuelle Objekt
            if (IsManaged(m_pChannelAttrs->m_pObject))
            {
                // Custom Draw statt Default
                DrawWithHiddenComponents(dp, m_pChannelAttrs->m_pObject);
                return false; // Skip default draw
            }
            break;
        }
        return true; // Continue default draw
    }
};
```

**Wichtig:** 
- `return false` in `SC_DRAWOBJECT` → überspringt das normale Zeichnen
- Dadurch können wir das Objekt komplett ersetzen!

---

### 5. Per-Instance Data Storage

```cpp
// Custom User Data Klasse
class CComponentVisibilityData : public ON_UserData
{
public:
    ON_SimpleArray<int> m_hidden_indices;
    
    // UUID für diese UserData Klasse
    static ON_UUID m_uuid;
    
    // Serialization
    ON_BOOL32 Write(ON_BinaryArchive& archive) const override;
    ON_BOOL32 Read(ON_BinaryArchive& archive) override;
};

// An Instanz anhängen
CRhinoInstanceObject* iobj = ...;
CComponentVisibilityData* data = new CComponentVisibilityData();
data->m_hidden_indices.Append(5);  // Index 5 verstecken
iobj->AttachUserData(data);

// Von Instanz lesen
CComponentVisibilityData* data = 
    (CComponentVisibilityData*)iobj->GetUserData(CComponentVisibilityData::m_uuid);
```

---

### 6. Proposed Implementation

```cpp
class CPerInstanceVisibilityConduit : public CRhinoDisplayConduit
{
    std::unordered_set<ON_UUID> m_managed_instances;
    
public:
    CPerInstanceVisibilityConduit()
        : CRhinoDisplayConduit(CSupportChannels::SC_DRAWOBJECT) {}
    
    bool ExecConduit(
        CRhinoDisplayPipeline& dp,
        UINT nChannel,
        bool& bTerminate
    ) override
    {
        if (nChannel != CSupportChannels::SC_DRAWOBJECT)
            return true;
        
        const CRhinoObject* obj = m_pChannelAttrs->m_pObject;
        if (!obj || obj->ObjectType() != ON::instance_reference)
            return true;
        
        // Prüfe ob diese Instanz managed ist
        if (m_managed_instances.find(obj->Id()) == m_managed_instances.end())
            return true;
        
        // Get visibility data
        const CRhinoInstanceObject* iobj = static_cast<const CRhinoInstanceObject*>(obj);
        CComponentVisibilityData* visData = ...;
        
        if (!visData || visData->m_hidden_indices.Count() == 0)
            return true;  // Normal zeichnen
        
        // Custom Draw
        const CRhinoInstanceDefinition* idef = iobj->InstanceDefinition();
        ON_Xform xform = iobj->InstanceXform();
        
        for (int i = 0; i < idef->ObjectCount(); i++)
        {
            // Skip hidden components
            if (visData->IsHidden(i))
                continue;
            
            const CRhinoObject* component = idef->Object(i);
            DrawComponent(dp, component, xform);
        }
        
        return false;  // Skip default draw!
    }
    
    void DrawComponent(
        CRhinoDisplayPipeline& dp,
        const CRhinoObject* obj,
        const ON_Xform& xform
    )
    {
        // Transform + Draw basierend auf Objekt-Typ
        switch (obj->ObjectType())
        {
        case ON::brep_object:
            // ...
            break;
        case ON::mesh_object:
            // ...
            break;
        // etc.
        }
    }
};
```

---

### 7. C++/C# Integration Options

#### Option A: P/Invoke (Einfacher)
```cpp
// C++ DLL Export
extern "C" __declspec(dllexport) 
bool __stdcall SetComponentVisibility(
    const ON_UUID* instanceId,
    int componentIndex,
    bool visible
);
```

```csharp
// C# Import
[DllImport("RhinoAssemblyOutliner.Native.rhp")]
public static extern bool SetComponentVisibility(
    ref Guid instanceId,
    int componentIndex,
    [MarshalAs(UnmanagedType.Bool)] bool visible
);
```

#### Option B: COM (Komplexer aber sauberer)
- Definiere Interface in IDL
- Implementiere in C++
- Referenziere in C#

---

### 8. Expected Advantages over C# Approach

| Issue | C# Conduit | C++ Conduit |
|-------|-----------|-------------|
| Ghost Artifacts | ❌ Ja | ✅ Sollte nicht auftreten |
| Selection | ⚠️ Workaround nötig | ✅ Native |
| Display Cache | ❌ Nicht integriert | ✅ Voll integriert |
| Performance | ⚠️ Managed Overhead | ✅ Native Speed |
| BoundingBox | ⚠️ Manuell | ✅ Automatisch |

**Grund für den Unterschied:**
In C++ können wir `return false` in `SC_DRAWOBJECT` um das Default-Rendering zu überspringen. Dies ist eine **echte** Pipeline-Integration, nicht ein "on-top" Draw wie in C#.

---

## Referenzen

- [Installing C++ Tools](https://developer.rhino3d.com/guides/cpp/installing-tools-windows/)
- [Your First C++ Plugin](https://developer.rhino3d.com/guides/cpp/your-first-plugin-windows/)
- [CRhinoDisplayConduit](https://developer.rhino3d.com/api/cpp/class_c_rhino_display_conduit.html)
- [CRhinoDisplayPipeline](https://developer.rhino3d.com/api/cpp/class_c_rhino_display_pipeline.html)
- [CRhinoInstanceDefinition](https://developer.rhino3d.com/api/cpp/class_c_rhino_instance_definition.html)
- [Dynamically Inserting Blocks](https://developer.rhino3d.com/guides/cpp/dynamically-inserting-blocks/)
- [Highlighting Objects in Conduits](https://developer.rhino3d.com/guides/cpp/highlighting-objects-in-conduits/)

---

*Research Stand: 2026-02-05*
