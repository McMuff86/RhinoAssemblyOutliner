# Rhino C++ SDK — Custom Object Types Research

> **Datum:** 2026-02-15  
> **Ziel:** Alles was nötig ist um eigene Rhino-Objekttypen in C++ zu erstellen  
> **Quellen:** McNeel Developer Docs, C++ API Reference, McNeel Forum, GitHub Samples

---

## Inhaltsverzeichnis

1. [CRhinoObject Subclassing](#1-crhinoobject-subclassing)
2. [ON_Geometry Subclass](#2-on_geometry-subclass)
3. [Custom Grips (CRhinoGripObject)](#3-custom-grips-crhinogripobject)
4. [Custom Properties Panel](#4-custom-properties-panel)
5. [Custom Display](#5-custom-display)
6. [Praktische Beispiele & Samples](#6-praktische-beispiele--samples)
7. [Rhino 8 Spezifika](#7-rhino-8-spezifika)
8. [Architektur-Empfehlung für RhinoAssemblyOutliner](#8-architektur-empfehlung)

---

## 1. CRhinoObject Subclassing

### 1.1 Grundkonzept

`CRhinoObject` ist die Basisklasse für **alle** Runtime-Objekte in Rhino. Definiert in `rhinoSdkObject.h`.

**Klassenhierarchie:**
```
ON_Object
  └── CRhinoObject
        ├── CRhinoPointObject
        ├── CRhinoCurveObject
        ├── CRhinoBrepObject
        ├── CRhinoMeshObject
        ├── CRhinoSubDObject
        ├── CRhinoInstanceObject
        ├── CRhinoAnnotationObject
        └── ... (dein Custom Object)
```

### 1.2 Wichtige Erkenntnis: Man subclassed CRhinoObject NICHT direkt

**⚠️ KRITISCH:** In der Praxis erstellt man in Rhino **keine** komplett neuen Objekttypen durch direktes Subclassing von `CRhinoObject`. Stattdessen gibt es zwei bewährte Strategien:

#### Strategie A: Bestehenden Typ ableiten + UserData
```cpp
// Leite von einem bestehenden Typ ab (z.B. CRhinoBrepObject)
// und nutze ON_UserData für custom data
class CMyWallObject : public CRhinoBrepObject
{
public:
    // Override virtuelle Methoden
    void Draw(CRhinoDisplayPipeline& dp) const override;
    ON_BoundingBox BoundingBox() const override;
    CRhinoObject* DuplicateRhinoObject() const override;
    
    // Custom Grips
    void EnableGrips(bool bGripsOn) override;
    bool EnableCustomGrips(CRhinoObjectGrips* custom_grips) override;
};
```

#### Strategie B: Standard-Objekt + UserData + DisplayConduit (VisualARQ-Ansatz)
```cpp
// Nutze Standard-Breps/Meshes als Geometrie-Container
// Hänge UserData an für custom properties
// Nutze DisplayConduit für erweiterte Darstellung
// Nutze Custom Grips für spezielle Interaktion
```

**Strategie B ist der empfohlene Weg** und wird von VisualARQ, Lands Design und ähnlichen Plugins verwendet.

### 1.3 Virtuelle Methoden zum Überschreiben

#### `ObjectType()` — **REQUIRED** (pure virtual)
```cpp
ON::object_type ObjectType() const override
{
    // Muss einen gültigen ON::object_type zurückgeben
    // Für custom objects typischerweise den Typ der unterliegenden Geometrie
    return ON::object_type::polysrf_filter; // z.B. für Brep-basierte Objekte
}
```

#### `Draw()` — Zeichnet das Objekt
```cpp
virtual void Draw(class CRhinoDisplayPipeline& dp) const;
```
- Wird aufgerufen wenn das Objekt gezeichnet werden muss
- `CRhinoDisplayPipeline` stellt alle Zeichenfunktionen bereit
- Kann die Basisklassen-Implementation aufrufen und zusätzliches zeichnen

```cpp
void CMyObject::Draw(CRhinoDisplayPipeline& dp) const
{
    // Erst die Standard-Geometrie zeichnen
    __super::Draw(dp);
    
    // Dann custom stuff
    dp.DrawPoint(m_labelPoint, CRhinoPointObject::point_style::ps_ActivePoint, 
                  5, ON_Color(255, 0, 0));
    dp.Draw3dText(m_label, ON_Color(0,0,0), m_labelPlane);
}
```

#### `DrawV6()` — Modernere Draw-Methode (Rhino 6+)
```cpp
virtual void DrawV6(class CRhinoObjectDrawContext* draw_context) const RHINO_NOEXCEPT;
```
- Neuere API mit `CRhinoObjectDrawContext`
- Bevorzugt für Rhino 6/7/8

#### `Pick()` — Object Selection
```cpp
virtual int Pick(const CRhinoPickContext& pick_context, 
                 CRhinoObjRefArray& pick_list) const;
```
- Bestimmt ob und wie das Objekt bei Klick selektiert wird
- Gibt Anzahl der Picks zurück
- `CRhinoPickContext` enthält Pick-Linie, Toleranz, etc.

```cpp
int CMyObject::Pick(const CRhinoPickContext& pc, CRhinoObjRefArray& pick_list) const
{
    // Custom picking logic
    int rc = 0;
    
    // Teste ob der Pick-Ray die Geometrie trifft
    // Fallback auf Standard-Implementierung:
    rc = __super::Pick(pc, pick_list);
    
    return rc;
}
```

#### `SnapTo()` — Object Snap Support
```cpp
virtual bool SnapTo(const CRhinoSnapContext& snap_context, 
                    CRhinoSnapEvent& snap_event) const;
```
- "Low level tool for internal use only" laut Doku
- Ermöglicht custom snap points
- Standardmäßig nutzt Rhino die Geometrie für Snaps

#### `BoundingBox()` — Begrenzungsrahmen
```cpp
virtual ON_BoundingBox BoundingBox() const;
virtual ON_BoundingBox BoundingBox(const class CRhinoViewport* pViewport) const;
```
- Wird für Clipping, Display, Zoom Extents etc. genutzt
- Muss alles enthalten was gezeichnet wird (inkl. Annotationen etc.)

```cpp
ON_BoundingBox CMyObject::BoundingBox() const
{
    ON_BoundingBox bbox = __super::BoundingBox();
    // Erweitere um custom display elements
    bbox.Union(m_labelBBox);
    return bbox;
}
```

#### `GetGrips()` — Grip-Punkte abfragen
```cpp
virtual int GetGrips(ON_SimpleArray<CRhinoGripObject*>& grip_list) const;
```

#### `EnableGrips()` / `EnableCustomGrips()` — Grips aktivieren
```cpp
virtual void EnableGrips(bool bGripsOn);
virtual bool EnableCustomGrips(CRhinoObjectGrips* custom_grips);
```

#### `DuplicateRhinoObject()` — Kopie erstellen
```cpp
virtual CRhinoObject* DuplicateRhinoObject() const;
```
- Wird für Copy/Paste, Undo, etc. genutzt
- **Muss** überschrieben werden wenn man custom state hat

#### Weitere wichtige Overrides:
```cpp
virtual void AddToDocNotification();     // Nach dem Hinzufügen zum Doc
virtual void DeleteFromDocNotification(); // Vor dem Löschen aus Doc
virtual void BeginTransform(...);         // Vor einer Transformation
virtual void EndTransform(...);           // Nach einer Transformation
virtual bool PrepareToWrite(int archive_3dm_version); // Vor dem Speichern
virtual int CreateMeshes(...);            // Render-Mesh erstellen
virtual bool IsMeshable(ON::mesh_type);   // Kann gemesht werden?
virtual bool IsSolid() const;             // Ist das Objekt solid?
virtual ON_wString OnDoubleClick(...);    // Doppelklick-Aktion
```

### 1.4 Registrierung bei Rhino

Es gibt **keinen** expliziten "register custom object type" Mechanismus mit UUID. Stattdessen:

1. **Plugin lädt** → Custom Object Klassen sind verfügbar
2. **Objekte werden via CRhinoDoc::AddObject() hinzugefügt**
3. **Für Serialisierung: ON_UserData mit ON_OBJECT_DECLARE/IMPLEMENT Makros**

Die UUID-Registrierung erfolgt über `ON_UserData`, nicht über den Objekttyp selbst.

### 1.5 Serialization — UserData-Ansatz

Rhino serialisiert Objekte automatisch basierend auf ihrem Geometrie-Typ. Custom Data wird über **ON_UserData** persistiert:

```cpp
class CWallUserData : public ON_UserData
{
    ON_OBJECT_DECLARE(CWallUserData);  // Required macro

public:
    static ON_UUID Id();
    
    CWallUserData();
    ~CWallUserData();
    
    // Serialization
    bool Archive() const override { return true; }
    bool Write(ON_BinaryArchive& archive) const override;
    bool Read(ON_BinaryArchive& archive) override;
    
    // Custom Data
    double m_width = 0.2;   // Wandstärke
    double m_height = 2.8;  // Wandhöhe
    ON_wString m_wallType;
    int m_wallStyle = 0;
};

// In CPP:
ON_OBJECT_IMPLEMENT(CWallUserData, ON_UserData, "XXXXXXXX-XXXX-...");

ON_UUID CWallUserData::Id()
{
    return ON_CLASS_ID(CWallUserData);
}

CWallUserData::CWallUserData()
{
    m_userdata_uuid = CWallUserData::Id();
    m_application_uuid = MyPlugIn().PlugInID();  // Plugin UUID!
    m_userdata_copycount = 1;  // Enable copying
}

bool CWallUserData::Write(ON_BinaryArchive& archive) const
{
    int minor_version = 0;
    bool rc = archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, 1, minor_version);
    if (!rc) return false;
    
    for (;;)
    {
        rc = archive.WriteDouble(m_width);
        if (!rc) break;
        rc = archive.WriteDouble(m_height);
        if (!rc) break;
        rc = archive.WriteString(m_wallType);
        if (!rc) break;
        rc = archive.WriteInt(m_wallStyle);
        if (!rc) break;
        break;
    }
    
    if (!archive.EndWrite3dmChunk()) rc = false;
    return rc;
}

bool CWallUserData::Read(ON_BinaryArchive& archive)
{
    int major_version = 0, minor_version = 0;
    bool rc = archive.BeginRead3dmChunk(TCODE_ANONYMOUS_CHUNK, 
                                         &major_version, &minor_version);
    if (!rc) return false;
    
    for (;;)
    {
        rc = (1 == major_version);
        if (!rc) break;
        
        rc = archive.ReadDouble(&m_width);
        if (!rc) break;
        rc = archive.ReadDouble(&m_height);
        if (!rc) break;
        rc = archive.ReadString(m_wallType);
        if (!rc) break;
        rc = archive.ReadInt(&m_wallStyle);
        if (!rc) break;
        break;
    }
    
    if (!archive.EndRead3dmChunk()) rc = false;
    return rc;
}
```

#### UserData anhängen:
```cpp
// An Geometrie anhängen (persistiert in 3dm!)
CWallUserData* pUD = new CWallUserData();
pUD->m_width = 0.3;
pUD->m_height = 3.0;

if (!pRhinoObject->AttachGeometryUserData(pUD))
{
    delete pUD; // Bereits vorhanden
}

// UserData abrufen:
CWallUserData* pUD = CWallUserData::Cast(
    pRhinoObject->Geometry()->GetUserData(CWallUserData::Id())
);
```

#### Drei Orte für UserData auf CRhinoObject:

| Ort | Methode | Persistiert in 3dm? | Kopiert? |
|-----|---------|---------------------|----------|
| Geometrie | `AttachGeometryUserData()` | ✅ Ja | ✅ Ja |
| Attributes | `AttachAttributeUserData()` | ✅ Ja | ✅ Ja |
| CRhinoObject selbst | `AttachUserData()` | ❌ Nein | ❌ Nein |

### 1.6 Document User Data (Plugin-Level)

Für globale Plugin-Daten (Styles, Konfiguration):

```cpp
// In Plugin-Klasse:
BOOL CallWriteDocument(const CRhinoFileWriteOptions& options) override
{
    return TRUE; // Ja, wir wollen speichern
}

BOOL WriteDocument(CRhinoDoc& doc, ON_BinaryArchive& archive,
                   const CRhinoFileWriteOptions& options) override
{
    // Wall Styles, global config etc. speichern
    archive.BeginWrite3dmChunk(TCODE_ANONYMOUS_CHUNK, 1, 0);
    // ... write data ...
    archive.EndWrite3dmChunk();
    return TRUE;
}

BOOL ReadDocument(CRhinoDoc& doc, ON_BinaryArchive& archive,
                  const CRhinoFileReadOptions& options) override
{
    // Wall Styles etc. laden
    int major, minor;
    archive.BeginRead3dmChunk(TCODE_ANONYMOUS_CHUNK, &major, &minor);
    // ... read data ...
    archive.EndRead3dmChunk();
    return TRUE;
}
```

---

## 2. ON_Geometry Subclass

### 2.1 Braucht man eine eigene Geometrie-Klasse?

**Kurze Antwort: NEIN.** Und das ist auch nicht empfohlen.

**Gründe:**
- Eigene `ON_Geometry`-Subklassen werden vom 3dm-Format **nicht nativ unterstützt**
- Rhino's File I/O kennt nur die eingebauten Geometrie-Typen
- Copy/Paste zwischen Rhino-Instanzen würde nicht funktionieren
- Andere Plugins könnten die Geometrie nicht lesen

### 2.2 Empfohlener Ansatz: Bestehende Geometrie wrappen

```cpp
class CWallObject
{
public:
    // Nutze Standard-Brep als Geometrie
    ON_Brep* CreateWallBrep(const ON_Line& centerline, 
                             double width, double height);
    
    // Oder bei komplexen Wänden: Mesh
    ON_Mesh* CreateWallMesh(...);
    
    // Die Geometrie wird als normales Brep/Mesh im Rhino Doc gespeichert
    // Custom Data kommt via UserData
};
```

### 2.3 Copy/Paste, Undo, File I/O Verhalten

Wenn man Standard-Geometrie + UserData nutzt:

| Operation | Verhalten |
|-----------|-----------|
| **Copy/Paste** (intern) | ✅ UserData wird kopiert (wenn `m_userdata_copycount >= 1`) |
| **Copy/Paste** (zwischen Instanzen) | ✅ Wenn Plugin in beiden geladen |
| **Undo/Redo** | ✅ Rhino handled das automatisch |
| **File Save/Open** | ✅ UserData wird mit gespeichert |
| **Export (STEP, IGES etc.)** | ⚠️ Geometrie ja, UserData nein |
| **SendTo / OLE** | ⚠️ Geometrie ja, UserData eventuell |

### 2.4 Geometrie-Update Pattern

```cpp
void UpdateWallGeometry(CRhinoDoc& doc, const CRhinoObject* pOldObj,
                        double newWidth, double newHeight)
{
    // 1. UserData vom alten Objekt holen
    const CWallUserData* pOldUD = GetWallUserData(pOldObj);
    
    // 2. Neue Geometrie erstellen
    ON_Brep* pNewBrep = CreateWallBrep(pOldUD->m_centerline, newWidth, newHeight);
    
    // 3. Neues Objekt erstellen
    CRhinoBrepObject* pNewObj = new CRhinoBrepObject();
    pNewObj->SetBrep(pNewBrep);
    
    // 4. UserData kopieren und updaten
    CWallUserData* pNewUD = new CWallUserData(*pOldUD);
    pNewUD->m_width = newWidth;
    pNewUD->m_height = newHeight;
    pNewObj->AttachGeometryUserData(pNewUD);
    
    // 5. Im Doc ersetzen (mit Undo Support)
    doc.ReplaceObject(CRhinoObjRef(pOldObj), pNewObj);
}
```

---

## 3. Custom Grips (CRhinoGripObject / CRhinoObjectGrips)

### 3.1 Architektur

Es gibt zwei zusammengehörige Klassen:

- **`CRhinoObjectGrips`** — Container-Klasse die alle Grips eines Objekts verwaltet
- **`CRhinoGripObject`** — Einzelner Grip-Punkt (ist selbst ein CRhinoObject!)

```
CRhinoObject
  └── CRhinoGripObject    // Ein einzelner Grip-Punkt
  
CRhinoObjectGrips          // Verwaltet eine Sammlung von CRhinoGripObjects
  m_grip_list[]            // Array von CRhinoGripObject*
  m_owner_object           // Das Objekt dem die Grips gehören
```

### 3.2 Custom Grips erstellen

```cpp
// 1. Custom Grip-Klasse (optional, für custom behavior)
class CWallGrip : public CRhinoGripObject
{
public:
    enum GripType { 
        kLength, kWidth, kHeight, kMove 
    };
    GripType m_gripType;
    
    // Optional: Custom drawing
    void Draw(CRhinoDisplayPipeline& dp) const override
    {
        // Custom grip shape (z.B. Pfeil statt Punkt)
        // Note: Standard-Grips werden als kleine Quadrate gezeichnet
        __super::Draw(dp);
    }
};

// 2. Custom Grips-Container
class CWallGrips : public CRhinoObjectGrips
{
public:
    CWallGrips();
    ~CWallGrips();
    
    // Erstellt die Grips basierend auf der Wand-Geometrie
    bool CreateGrips(const CRhinoObject* pWallObj);
    
    // WICHTIG: Wird aufgerufen nachdem Grips bewegt wurden
    // Muss neues Objekt erstellen
    CRhinoObject* NewObject() override;
    
    // Optional: Custom Drawing während Grip-Drag
    void Draw(CRhinoDrawGripsSettings& dgs) override;
    
    // Optional: Mesh-Update während Drag
    void UpdateMesh(ON::mesh_type mesh_type_hint) override;

private:
    ON_SimpleArray<CWallGrip*> m_wall_grips;
};
```

### 3.3 Grips-Container Implementation

```cpp
CWallGrips::CWallGrips()
    : CRhinoObjectGrips(CRhinoGripObject::custom_grip)
{
    // Eindeutige ID für diese Grip-Art
    // {XXXXXXXX-XXXX-...}
    m_grips_id = { 0x12345678, 0x1234, ... };
}

bool CWallGrips::CreateGrips(const CRhinoObject* pWallObj)
{
    const CWallUserData* pUD = GetWallUserData(pWallObj);
    if (!pUD) return false;
    
    // Grip-Punkte berechnen basierend auf Wand-Parametern
    ON_3dPoint basePt = pUD->m_basePt;
    ON_3dVector dir = pUD->m_direction;
    double length = pUD->m_length;
    double width = pUD->m_width;
    double height = pUD->m_height;
    
    // Längen-Grip am Ende der Wand
    CWallGrip* pLenGrip = new CWallGrip();
    pLenGrip->m_gripType = CWallGrip::kLength;
    pLenGrip->m_base_point = basePt + dir * length;
    pLenGrip->m_grip_index = m_grip_list.Count();
    m_grip_list.Append(pLenGrip);
    m_wall_grips.Append(pLenGrip);
    
    // Breiten-Grip seitlich
    CWallGrip* pWidthGrip = new CWallGrip();
    pWidthGrip->m_gripType = CWallGrip::kWidth;
    ON_3dVector perp = ON_CrossProduct(dir, ON_3dVector::ZAxis);
    perp.Unitize();
    pWidthGrip->m_base_point = basePt + dir * (length/2) + perp * (width/2);
    pWidthGrip->m_grip_index = m_grip_list.Count();
    m_grip_list.Append(pWidthGrip);
    m_wall_grips.Append(pWidthGrip);
    
    // Höhen-Grip oben
    CWallGrip* pHeightGrip = new CWallGrip();
    pHeightGrip->m_gripType = CWallGrip::kHeight;
    pHeightGrip->m_base_point = basePt + dir * (length/2) + ON_3dVector::ZAxis * height;
    pHeightGrip->m_grip_index = m_grip_list.Count();
    m_grip_list.Append(pHeightGrip);
    m_wall_grips.Append(pHeightGrip);
    
    return true;
}

CRhinoObject* CWallGrips::NewObject()
{
    // Wird aufgerufen nach Grip-Drag!
    // Erstelle neues Objekt basierend auf neuen Grip-Positionen
    
    CRhinoObject* pOwner = m_owner_object;
    if (!pOwner) return nullptr;
    
    const CWallUserData* pOldUD = GetWallUserData(pOwner);
    if (!pOldUD) return nullptr;
    
    // Neue Dimensionen aus Grip-Positionen berechnen
    double newLength = pOldUD->m_length;
    double newWidth = pOldUD->m_width;
    double newHeight = pOldUD->m_height;
    
    for (int i = 0; i < m_wall_grips.Count(); i++)
    {
        CWallGrip* pGrip = m_wall_grips[i];
        if (!pGrip->m_bGripMoved) continue;
        
        ON_3dPoint newLoc = pGrip->GripLocation();
        
        switch (pGrip->m_gripType)
        {
        case CWallGrip::kLength:
            newLength = (newLoc - pOldUD->m_basePt).Length();
            break;
        case CWallGrip::kWidth:
            // Berechne neue Breite aus seitlicher Verschiebung
            newWidth = /* ... */;
            break;
        case CWallGrip::kHeight:
            newHeight = newLoc.z - pOldUD->m_basePt.z;
            break;
        }
    }
    
    // Neues Brep erstellen
    ON_Brep* pNewBrep = CreateWallBrep(pOldUD->m_basePt, 
                                        pOldUD->m_direction,
                                        newLength, newWidth, newHeight);
    
    CRhinoBrepObject* pNewObj = new CRhinoBrepObject();
    pNewObj->SetBrep(pNewBrep);
    
    // UserData kopieren und updaten
    CWallUserData* pNewUD = new CWallUserData(*pOldUD);
    pNewUD->m_length = newLength;
    pNewUD->m_width = newWidth;
    pNewUD->m_height = newHeight;
    pNewObj->AttachGeometryUserData(pNewUD);
    
    return pNewObj;
}

CWallGrips::~CWallGrips()
{
    // WICHTIG: Grips müssen manuell freigegeben werden!
    // m_grip_list ist im Destruktor bereits leer
    for (int i = 0; i < m_wall_grips.Count(); i++)
        delete m_wall_grips[i];
}
```

### 3.4 Grips aktivieren

```cpp
// Im Command oder Event Handler:
void EnableWallGrips(CRhinoDoc& doc, const CRhinoObject* pObj)
{
    CWallGrips* pGrips = new CWallGrips();
    if (pGrips->CreateGrips(pObj))
    {
        // EnableCustomGrips übernimmt Ownership
        const_cast<CRhinoObject*>(pObj)->EnableCustomGrips(pGrips);
    }
    else
    {
        delete pGrips;
    }
}
```

### 3.5 Custom Grip Shapes (Zeichnung)

```cpp
// In CRhinoGripObject::Draw oder CRhinoObjectGrips::Draw
void CWallGrips::Draw(CRhinoDrawGripsSettings& dgs)
{
    // Custom drawing: z.B. Pfeile statt Punkte
    for (int i = 0; i < m_wall_grips.Count(); i++)
    {
        CWallGrip* pGrip = m_wall_grips[i];
        ON_3dPoint pt = pGrip->GripLocation();
        
        // Zeichne je nach Grip-Typ unterschiedlich
        switch (pGrip->m_gripType)
        {
        case CWallGrip::kLength:
            // Horizontaler Pfeil
            dgs.m_dp.DrawDirectionArrow(pt, pGrip->m_direction, ON_Color(255,0,0));
            break;
        case CWallGrip::kHeight:
            // Vertikaler Pfeil
            dgs.m_dp.DrawDirectionArrow(pt, ON_3dVector::ZAxis, ON_Color(0,0,255));
            break;
        }
    }
    
    // Standard-Grips zeichnen
    __super::Draw(dgs);
}
```

### 3.6 Grips und Persistenz

**⚠️ WICHTIG:** Custom Grips werden **NICHT** in 3dm-Dateien gespeichert!

- Grips sind reine Runtime-Objekte
- Beim Öffnen einer Datei müssen Grips neu erstellt werden
- Lösung: Überschreibe `EnableGrips()` auf dem Objekt, um automatisch Custom Grips zu erstellen
- Oder: Nutze `CRhinoEventWatcher` um beim Laden die Grips wiederherzustellen

```cpp
// Im Custom Object oder via Event Watcher:
void CMyObject::EnableGrips(bool bGripsOn)
{
    if (bGripsOn)
    {
        CWallGrips* pGrips = new CWallGrips();
        if (pGrips->CreateGrips(this))
            EnableCustomGrips(pGrips);
        else
            delete pGrips;
    }
    else
    {
        __super::EnableGrips(false);
    }
}
```

### 3.7 VisualARQ-Style Grips (Dimension-Änderung)

VisualARQ nutzt genau dieses Pattern:
1. Standard-Brep als Geometrie
2. UserData für Wand-Parameter (Länge, Höhe, Breite)
3. Custom Grips an strategischen Punkten
4. `NewObject()` regeneriert die Geometrie basierend auf neuen Grip-Positionen
5. Custom Drawing zeigt Dimensionen/Pfeile an den Grips

---

## 4. Custom Properties Panel

### 4.1 Rhino 6+ API: CRhinoObjectPropertiesDialogPageEx / TRhinoPropertiesPage

In Rhino 6+ wurde die Properties-API grundlegend überarbeitet.

#### Registration im Plugin:
```cpp
void CMyPlugIn::AddPagesToObjectPropertiesDialog(
    CRhinoPropertiesPanelPageCollection& collection)
{
    // Singleton-Page hinzufügen
    collection.Add(&m_wall_properties_page);
}
```

#### Properties Page Klasse (MFC-basiert):
```cpp
#include "rhinoSdkTMfcPages.h"

class CWallPropertiesPage : public TRhinoPropertiesPage<CDialog>
{
    DECLARE_DYNAMIC(CWallPropertiesPage)

public:
    CWallPropertiesPage();
    virtual ~CWallPropertiesPage();

    // Required overrides
    const wchar_t* EnglishTitle() const override { return L"Wall"; }
    const wchar_t* LocalTitle() const override { return L"Wand"; }
    HICON Icon() const override;
    
    // Wann soll die Page angezeigt werden?
    // Nur wenn ein Wand-Objekt selektiert ist
    bool ShouldDisplay(const CRhinoPropertiesPanelPageEventArgs& e) const override
    {
        // Prüfe ob selektierte Objekte UserData haben
        for (int i = 0; i < e.ObjectCount(); i++)
        {
            const CRhinoObject* pObj = e.Object(i);
            if (GetWallUserData(pObj))
                return true;
        }
        return false;
    }
    
    // Page Update wenn Selektion sich ändert
    void UpdatePage(CRhinoPropertiesPanelPageEventArgs& e) override
    {
        // UI mit aktuellen Wand-Daten füllen
        const CRhinoObject* pObj = e.Object(0);
        const CWallUserData* pUD = GetWallUserData(pObj);
        if (pUD)
        {
            SetDlgItemDouble(IDC_WIDTH, pUD->m_width);
            SetDlgItemDouble(IDC_HEIGHT, pUD->m_height);
        }
    }
    
    // Änderungen anwenden
    bool Apply(CRhinoPropertiesPanelPageEventArgs& e) override
    {
        double newWidth = GetDlgItemDouble(IDC_WIDTH);
        double newHeight = GetDlgItemDouble(IDC_HEIGHT);
        // Update objects...
        return true;
    }
    
    CRhinoCommand::result RunScript(CRhinoPropertiesPanelPageEventArgs& e) override
    {
        return CRhinoCommand::success;
    }
};
```

### 4.2 Eto.Forms in C++ Properties Pages?

**Nein, nicht direkt.** Die C++ SDK Properties Pages sind MFC-basiert (Windows).

Optionen:
1. **MFC Dialoge** — Native Windows UI, voll unterstützt
2. **WPF via C++/CLI** — Möglich aber komplex
3. **Hybrid-Ansatz:** C++ Plugin stellt Daten bereit, C# Plugin liefert die UI (via Eto.Forms)
4. **In Rhino 8:** RhinoCommon Properties Pages können Eto.Forms nutzen (C# Seite)

### 4.3 Rhino 5 → 6+ Migration

Die alte API (`CRhinoObjectPropertiesDialogPage`) wurde ersetzt durch Template-basierte Klassen:

```
Rhino 5: CRhinoObjectPropertiesDialogPage → CRhinoObjectPropertiesDialogPageEx
Rhino 6+: TRhinoPropertiesPage<CDialog>  (Template mit MFC CDialog)
```

**Wichtige Änderungen:**
- `EnglishPageTitle()` → `EnglishTitle() const`
- `OnActivate()` → `UpdatePage(EventArgs&)`
- `OnApply()` → `Apply(EventArgs&)`
- `AddPagesToObjectPropertiesDialog(ON_SimpleArray<...>&)` → `AddPagesToObjectPropertiesDialog(CRhinoPropertiesPanelPageCollection&)`

---

## 5. Custom Display

### 5.1 Draw() Override vs DisplayConduit

| Feature | `CRhinoObject::Draw()` Override | `CRhinoDisplayConduit` |
|---------|-------------------------------|----------------------|
| **Scope** | Nur für DIESES Objekt | Global, alle Views |
| **Lifecycle** | Nur wenn Objekt sichtbar | Immer wenn enabled |
| **Selection** | Objekt ist selektierbar | Gezeichnetes ist nicht selektierbar |
| **Performance** | Optimal (nur wenn nötig) | Kann Performance beeinflussen |
| **Use Case** | Custom Darstellung eines Objekts | Overlay, Annotationen, Preview |
| **Display Modes** | Bekommt aktiven Mode automatisch | Muss Mode selbst prüfen |

### 5.2 Draw() Override — Details

```cpp
void CMyObject::Draw(CRhinoDisplayPipeline& dp) const
{
    // Zugriff auf Display-Attribute
    const CDisplayPipelineAttributes* pAttrs = dp.DisplayAttrs();
    
    // Prüfe Display-Mode
    bool bShaded = dp.ObjectsShouldDrawShadedMeshes();
    bool bWireframe = dp.ObjectsShouldDrawWires();
    
    if (bShaded)
    {
        // Shaded Darstellung: Mesh zeichnen
        const ON_Mesh* pMesh = GetRenderMesh();
        if (pMesh)
        {
            ON_Color color = ObjectDrawColor();
            ON_Material mat;
            mat.SetDiffuse(color);
            dp.DrawShadedMesh(*pMesh, &mat);
        }
    }
    
    if (bWireframe || dp.InterruptDrawing())
    {
        // Wireframe: Kanten zeichnen
        ON_Color wireColor = ObjectDrawColor();
        dp.DrawBrep(*m_pBrep, wireColor, 1);
    }
    
    // Custom Annotationen immer zeichnen
    DrawDimensionLabels(dp);
}
```

### 5.3 Selektives Zeichnen von Teilen

```cpp
void CMyObject::Draw(CRhinoDisplayPipeline& dp) const
{
    // Nur bestimmte Teile zeichnen basierend auf Zustand
    if (m_showOuterShell)
        dp.DrawBrep(*m_outerBrep, ON_Color(200,200,200), 1);
    
    if (m_showInnerStructure)
        dp.DrawBrep(*m_innerBrep, ON_Color(100,100,255), 1);
    
    if (m_showDimensions)
        DrawDimensions(dp);
}

// Sub-Object Drawing
void CMyObject::DrawSubObject(CRhinoDisplayPipeline& dp, 
                               ON_COMPONENT_INDEX ci) const
{
    // Einzelne Komponenten zeichnen (für Sub-Object Selection)
}

void CMyObject::DrawHighlightedSubObjects(CRhinoDisplayPipeline& dp) const
{
    // Highlighted Sub-Objects speziell zeichnen
}
```

### 5.4 Display Modes

Das `CRhinoDisplayPipeline` Objekt stellt Infos über den aktiven Display Mode bereit:

```cpp
void CMyObject::Draw(CRhinoDisplayPipeline& dp) const
{
    // Display Mode abfragen
    ON_UUID displayModeId = dp.DisplayAttrs()->m_uuid;
    
    // Standard Display Modes:
    // ON_StandardDisplayModeId::Wireframe
    // ON_StandardDisplayModeId::Shaded
    // ON_StandardDisplayModeId::Rendered
    // ON_StandardDisplayModeId::Ghosted
    // ON_StandardDisplayModeId::XRay
    // ON_StandardDisplayModeId::Technical
    // ON_StandardDisplayModeId::Artistic
    // ON_StandardDisplayModeId::Pen
    
    if (displayModeId == ON_StandardDisplayModeId::Wireframe)
    {
        DrawWireframe(dp);
    }
    else if (displayModeId == ON_StandardDisplayModeId::Shaded ||
             displayModeId == ON_StandardDisplayModeId::Rendered)
    {
        DrawShaded(dp);
        DrawWireframe(dp); // Kanten auch in Shaded
    }
}
```

### 5.5 CRhinoDisplayConduit (für zusätzliche Darstellung)

```cpp
class CWallDisplayConduit : public CRhinoDisplayConduit
{
public:
    CWallDisplayConduit()
        : CRhinoDisplayConduit(
            CSupportChannels::SC_CALCBOUNDINGBOX |
            CSupportChannels::SC_DRAWOBJECT |
            CSupportChannels::SC_DRAWOVERLAY)
    {}
    
    bool ExecConduit(CRhinoDisplayPipeline& dp, UINT channel, bool& terminate) override
    {
        switch (channel)
        {
        case CSupportChannels::SC_CALCBOUNDINGBOX:
            // Bounding Box erweitern für custom display
            m_pChannelAttrs->m_BoundingBox.Union(m_customBBox);
            break;
            
        case CSupportChannels::SC_DRAWOBJECT:
            // Vor/nach dem Zeichnen eines Objekts
            // Kann zusätzliche Dinge zeichnen
            break;
            
        case CSupportChannels::SC_DRAWOVERLAY:
            // Overlay zeichnen (über allem)
            dp.Draw2dText(L"Wall Info", ON_Color(255,0,0), 
                          ON_2dPoint(10, 10), false);
            break;
        }
        return true;
    }
};

// Aktivieren:
CWallDisplayConduit conduit;
conduit.Enable(RhinoApp().ActiveDoc()->RuntimeSerialNumber());
```

---

## 6. Praktische Beispiele & Samples

### 6.1 McNeel GitHub Samples

**Repository:** https://github.com/mcneel/rhino-developer-samples

Branches: `6`, `7`, `8` (jeweils für Rhino-Version)

Relevante C++ Samples im `cpp/` Ordner:
- **SampleCustomGrips** — Custom Grip Implementation
- **SampleUserData** — ON_UserData Beispiel  
- **SampleSharedUserData** — UserData zwischen Plugins teilen
- **SampleMigration** — Properties Page Migration (Rhino 5→6)
- **SampleDisplayConduit** — Display Conduit Beispiel
- **SampleObjectPropertiesPage** — Object Properties Panel

### 6.2 Key Samples

#### SampleCustomGrips
- Zeigt wie man `CRhinoObjectGrips` subclassed
- Custom `CRhinoGripObject` mit eigenem Verhalten
- `NewObject()` Override für Geometrie-Update nach Grip-Drag
- **Link:** https://github.com/mcneel/rhino-developer-samples/tree/8/cpp/SampleCustomGrips

#### SampleSharedUserData
- Drei Projekte: CoreLib (shared DLL), Plugin1, Plugin2
- Zeigt wie UserData zwischen Plugins geteilt wird
- Vollständige Read/Write Implementation
- **Link:** https://github.com/mcneel/rhino-developer-samples/tree/8/cpp/SampleSharedUserDataCoreLib

### 6.3 Open-Source Rhino C++ Plugins

Leider sind die meisten kommerziellen C++ Plugins (VisualARQ, RhinoCAM, V-Ray) **nicht Open Source**.

Nützliche Ressourcen:
- **openNURBS** (Open Source): https://github.com/mcneel/opennurbs — Die Geometrie-Bibliothek
- **McNeel Forum (Rhino Developer)**: https://discourse.mcneel.com/c/rhino-developer/2 — Viele Code-Snippets
- **Rhino C++ API Docs**: https://developer.rhino3d.com/api/cpp/

### 6.4 Forum-Threads mit relevanten Code-Beispielen

- **Custom Grip Shapes:** https://discourse.mcneel.com/t/c-sdk-custom-grip-shapes/6213
- **Freeing Custom Grips (Memory):** https://discourse.mcneel.com/t/c-sdk-freeing-custom-grips/6742
- **Custom Grips on InstanceObject:** https://discourse.mcneel.com/t/custom-grips-on-instanceobject/140991
- **Save Custom Grips to 3dm:** https://discourse.mcneel.com/t/how-to-save-custom-grips-object-to-3dm-file/40420

---

## 7. Rhino 8 Spezifika

### 7.1 .NET 7/8 + C++ Interop

Rhino 8 nutzt **.NET Core Runtime** (nicht mehr .NET Framework). Die C++ SDK Architektur:

```
┌─────────────────────────────────┐
│  RhinoCommon (.NET / C#)        │  ← Eto.Forms, cross-platform
├─────────────────────────────────┤
│  C API Bridge (P/Invoke)        │  ← Flat C functions
├─────────────────────────────────┤
│  Rhino C++ SDK                  │  ← Dein C++ Plugin lebt hier
├─────────────────────────────────┤
│  openNURBS (C++)                │  ← Geometrie-Kern
└─────────────────────────────────┘
```

- C++ Plugins kommunizieren **direkt** mit dem C++ SDK
- RhinoCommon nutzt P/Invoke über eine C-API Schicht
- C++ ↔ C# Kommunikation: Über **shared UserData** oder **Plugin Events**
- `Rhino.Runtime.Interop` Klasse enthält Marshalling-Methoden

### 7.2 C++ ↔ RhinoCommon Brücke

```cpp
// Von C++ aus RhinoCommon aufrufen (via Hosting):
// Normalerweise nicht nötig — C++ SDK ist vollständig

// Von RhinoCommon aus auf C++ UserData zugreifen:
// File3dmObject.TryReadUserData() in RhinoCommon
```

Für **Hybrid-Plugins** (C++ Core + C# UI):
1. C++ Plugin exportiert Funktionen über eine DLL
2. C# Plugin nutzt P/Invoke um diese aufzurufen
3. Oder: Communication über UserData auf Objekten
4. Oder: Custom Events über `CRhinoEventWatcher`

### 7.3 Rhino 8 SDK Änderungen

Das Rhino 8 C++ SDK ist eine **Erweiterung** des Rhino 6 SDK:
- Rhino 6, 7, 8 haben identische `RHINO_SDK_WINDOWS_VERSION`
- Rhino 8 lädt Rhino 6 Plugins
- Neue Klassen/Methoden sind additiv

**Neue relevante Features in Rhino 8:**
- `CRhinoObjectDrawContext` für moderneres Drawing
- Verbesserte SubD-Unterstützung
- Neue Display Pipeline Features
- `std::shared_ptr<const ON_Mesh>` für Mesh-Zugriff (memory-safe)

### 7.4 Build-Anforderungen Rhino 8

- **Visual Studio 2022** (v143 Toolset)
- **C++ SDK Installer** von McNeel
- **Windows only** (kein Mac C++ SDK!)
- 64-bit only

---

## 8. Architektur-Empfehlung für RhinoAssemblyOutliner

### 8.1 Empfohlene Architektur

```
┌─────────────────────────────────────────────┐
│  Standard Rhino Brep/Mesh Objects           │
│  + ON_UserData (AssemblyPartUserData)       │
│  + ON_UserData (AssemblyRelationUserData)   │
├─────────────────────────────────────────────┤
│  CRhinoObjectGrips Subclass                 │
│  → Dimension Grips (Länge, Breite, Höhe)    │
│  → Connection Points                         │
├─────────────────────────────────────────────┤
│  CRhinoDisplayConduit                        │
│  → Assembly Annotations                      │
│  → Connection Lines                          │
│  → Explosion View                            │
├─────────────────────────────────────────────┤
│  TRhinoPropertiesPage<CDialog>              │
│  → Part Properties (Material, Dimensions)    │
│  → Assembly Properties (BOM, Connections)    │
├─────────────────────────────────────────────┤
│  Document UserData (Plugin Level)            │
│  → Assembly Styles                           │
│  → Material Library                          │
│  → Global Configuration                      │
└─────────────────────────────────────────────┘
```

### 8.2 Warum NICHT CRhinoObject subclassen

1. Geometrie-Typ muss bekannt sein → Standard-Brep ist besser
2. File I/O funktioniert automatisch
3. Export (STEP etc.) exportiert die Geometrie korrekt
4. Andere Plugins können die Objekte verarbeiten
5. Copy/Paste, Undo, Redo funktionieren automatisch

### 8.3 Empfohlener Workflow

1. **Plugin initialisieren** → Document UserData laden, Styles registrieren
2. **Objekt erstellen** → Standard-Brep + AssemblyUserData anhängen
3. **Grips aktivieren** → Via `EnableGrips()` Override oder Event
4. **Properties anzeigen** → Via Properties Page (ShouldDisplay prüft UserData)
5. **Custom Display** → DisplayConduit für Annotationen
6. **Speichern** → Automatisch via UserData::Write()
7. **Laden** → Automatisch via UserData::Read(), Grips via Event wiederherstellen

---

## Referenzen

| Ressource | URL |
|-----------|-----|
| C++ API Reference | https://developer.rhino3d.com/api/cpp/ |
| C++ Guides | https://developer.rhino3d.com/guides/cpp/ |
| CRhinoObject Class | https://developer.rhino3d.com/api/cpp/class_c_rhino_object.html |
| CRhinoObjectGrips Class | https://developer.rhino3d.com/api/cpp/class_c_rhino_object_grips.html |
| CRhinoGripObject Class | https://developer.rhino3d.com/api/cpp/class_c_rhino_grip_object.html |
| CRhinoDisplayConduit Class | https://developer.rhino3d.com/api/cpp/class_c_rhino_display_conduit.html |
| ON_UserData Guide | https://developer.rhino3d.com/guides/cpp/user-data/ |
| Properties Page Migration | https://developer.rhino3d.com/guides/cpp/migrate-properties-pages-windows/ |
| Custom Grip Picking Guide | https://developer.rhino3d.com/guides/cpp/custom-picking-grip-objects/ |
| GitHub Samples (Branch 8) | https://github.com/mcneel/rhino-developer-samples/tree/8/cpp |
| Technology Overview | https://developer.rhino3d.com/guides/general/rhino-technology-overview/ |
| McNeel Developer Forum | https://discourse.mcneel.com/c/rhino-developer/2 |
