// AssemblyObject.h — Minimal Custom Rhino Object for Assembly Outliner
// Proof of Concept — zeigt die Architektur, muss nicht kompilieren
#pragma once

#include "stdafx.h"

// ============================================================================
// WICHTIG: CRhinoObject ist die Basisklasse für ALLE Rhino-Dokument-Objekte.
// Ein Custom Object erlaubt uns:
//   - Eigene Geometrie-Darstellung (Draw)
//   - Eigene Grips (Custom Grips für Dimensionen)
//   - Eigene Selektion/Picking-Logik
//   - Eigene Serialisierung (Read/Write für 3dm)
//
// ARCHITEKTUR-ENTSCHEID:
//   Wir leiten NICHT direkt von CRhinoObject ab, sondern von einer
//   spezifischeren Klasse wie CRhinoBrepObject oder CRhinoGroupObject.
//   Grund: CRhinoObject allein hat keine Geometrie — wir brauchen eine
//   "Träger-Geometrie" (z.B. BoundingBox-Brep) für Pick/Select/Draw.
//
//   Alternative: CRhinoObject + eigene ON_Geometry-Subklasse
//   → Mehr Kontrolle, aber VIEL mehr Aufwand (Serialisierung, Mesh-Erzeugung etc.)
// ============================================================================

// UUID für unseren Custom Object Type
// Generiert mit guidgen.exe — MUSS einzigartig sein
// {A7E3F2B1-4C5D-6E7F-8A9B-0C1D2E3F4A5B}
static const ON_UUID AssemblyObjectTypeId = 
{ 0xa7e3f2b1, 0x4c5d, 0x6e7f, { 0x8a, 0x9b, 0x0c, 0x1d, 0x2e, 0x3f, 0x4a, 0x5b } };


// =============================================================================
// Ansatz A: Custom Object basierend auf CRhinoBrepObject
// =============================================================================
// Pro: Erbt Pick, Draw, Mesh-Erzeugung, Serialisierung
// Con: "Faked" Geometrie — Assembly ist eigentlich eine Gruppe von Objekten
//
// Dies ist der EMPFOHLENE Ansatz für den Prototyp.
// =============================================================================

class CAssemblyObject : public CRhinoBrepObject
{
    DECLARE_SERIAL(CAssemblyObject)

public:
    CAssemblyObject();
    CAssemblyObject(const CAssemblyObject& src);
    virtual ~CAssemblyObject();

    CAssemblyObject& operator=(const CAssemblyObject& src);

    // --- Identifikation ---
    
    // Gibt unseren Custom Type UUID zurück
    ON_UUID ModelObjectId() const override;

    // Beschreibung für Properties-Panel, What-Command etc.
    const wchar_t* ShortDescription(bool bPlural) const override;

    // Object Type für Filter/Selektion
    // Wir geben polysrf_object zurück damit Standard-Selektionen funktionieren
    ON::object_type ObjectType() const override;

    // --- Drawing ---
    
    // Custom Draw: Zeichnet die Assembly-Bounding-Box + Outline
    void Draw(CRhinoDisplayPipeline& dp) const override;

    // Wireframe-Darstellung
    void DrawV6(
        CRhinoObjectDrawContext* drawContext
    ) const override;

    // --- Bounding Box ---
    ON_BoundingBox BoundingBox() const override;
    ON_BoundingBox BoundingBox(const CRhinoViewport* pViewport) const override;

    // --- Grips ---
    
    // Aktiviert Custom Grips wenn der User "PointsOn" macht
    void EnableGrips(bool bGripsOn) override;

    // --- Custom Methoden ---
    
    // Member-Objekte der Assembly (Referenzen via UUID)
    void AddMemberObject(ON_UUID objectId);
    void RemoveMemberObject(ON_UUID objectId);
    const ON_SimpleArray<ON_UUID>& GetMemberObjects() const;

    // Assembly-Dimensionen (für Grips)
    double GetWidth() const { return m_width; }
    double GetHeight() const { return m_height; }
    double GetDepth() const { return m_depth; }
    void SetDimensions(double w, double h, double d);

    // Bounding-Brep aus Dimensionen neu berechnen
    void UpdateBrepFromDimensions();

    // --- Serialisierung ---
    
    // Für .3dm File I/O — Custom Data wird als UserData gespeichert
    // CRhinoBrepObject kümmert sich um die Brep-Geometrie
    // Unsere Zusatzdaten (Member-Liste, Dimensionen) kommen via ON_UserData

protected:
    // Assembly-Member (UUIDs der enthaltenen Objekte)
    ON_SimpleArray<ON_UUID> m_memberObjects;

    // Assembly-Dimensionen
    double m_width = 0.0;
    double m_height = 0.0;
    double m_depth = 0.0;

    // Origin/Insertion Point
    ON_3dPoint m_origin = ON_3dPoint::Origin;
};


// =============================================================================
// Ansatz B (Alternativ): Reiner Custom Object von CRhinoObject
// =============================================================================
// NICHT empfohlen für Prototyp — zu viel Boilerplate.
// Dokumentiert hier für Vollständigkeit.
//
// class CAssemblyObjectPure : public CRhinoObject
// {
//     // Müsste implementieren:
//     // - ObjectType() → ON::object_type (welcher?)
//     // - Geometry() → const ON_Geometry* (eigene ON_Geometry-Subklasse!)
//     // - Draw() → komplett custom
//     // - Pick() → eigene Hit-Test-Logik
//     // - SnapTo() → eigene Snap-Punkte
//     // - MeshObjects() → Mesh für Shaded-Display
//     // - DuplicateObject() → Deep Copy
//     // - Read/Write → Serialisierung
//     //
//     // Das sind ~15+ Overrides. Für Prototyp nicht sinnvoll.
// };
