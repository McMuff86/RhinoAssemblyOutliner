// AssemblyObject.cpp — Implementation des minimalen Custom Assembly Objects
// Proof of Concept — zeigt die Architektur

#include "stdafx.h"
#include "AssemblyObject.h"
#include "AssemblyGrip.h"

IMPLEMENT_SERIAL(CAssemblyObject, CRhinoBrepObject, 1)

// =============================================================================
// Konstruktoren
// =============================================================================

CAssemblyObject::CAssemblyObject()
    : CRhinoBrepObject()
    , m_width(100.0)
    , m_height(50.0)
    , m_depth(30.0)
    , m_origin(ON_3dPoint::Origin)
{
    // Initiale Brep-Box erstellen
    UpdateBrepFromDimensions();
}

CAssemblyObject::CAssemblyObject(const CAssemblyObject& src)
    : CRhinoBrepObject(src)
    , m_memberObjects(src.m_memberObjects)
    , m_width(src.m_width)
    , m_height(src.m_height)
    , m_depth(src.m_depth)
    , m_origin(src.m_origin)
{
}

CAssemblyObject::~CAssemblyObject()
{
}

CAssemblyObject& CAssemblyObject::operator=(const CAssemblyObject& src)
{
    if (this != &src)
    {
        CRhinoBrepObject::operator=(src);
        m_memberObjects = src.m_memberObjects;
        m_width = src.m_width;
        m_height = src.m_height;
        m_depth = src.m_depth;
        m_origin = src.m_origin;
    }
    return *this;
}

// =============================================================================
// Identifikation
// =============================================================================

ON_UUID CAssemblyObject::ModelObjectId() const
{
    return AssemblyObjectTypeId;
}

const wchar_t* CAssemblyObject::ShortDescription(bool bPlural) const
{
    return bPlural ? L"Assembly objects" : L"Assembly object";
}

ON::object_type CAssemblyObject::ObjectType() const
{
    // Gibt polysrf zurück — damit funktionieren Standard-Selektionsfilter
    // und das Objekt verhält sich "wie ein Brep" für Rhino-Commands
    return ON::polysrf_filter;
}

// =============================================================================
// Drawing
// =============================================================================

void CAssemblyObject::Draw(CRhinoDisplayPipeline& dp) const
{
    // 1. Basis-Brep zeichnen (Wireframe der Box)
    CRhinoBrepObject::Draw(dp);

    // 2. Custom Overlay: Assembly-Dimensionen als Text
    //    (Optional — zeigt die Möglichkeit von Custom Drawing)
    ON_Color assemblyColor(0, 120, 215); // Assembly-Blau
    
    // Dimension-Lines an den Kanten zeichnen
    ON_3dPoint p0 = m_origin;
    ON_3dPoint p1 = m_origin + ON_3dVector(m_width, 0, 0);
    ON_3dPoint p2 = m_origin + ON_3dVector(m_width, m_height, 0);
    ON_3dPoint p3 = m_origin + ON_3dVector(0, m_height, 0);

    // Hervorhebung: Dickere Linien für Assembly-Outline
    dp.DrawLine(p0, p1, assemblyColor, 2);
    dp.DrawLine(p1, p2, assemblyColor, 2);
    dp.DrawLine(p2, p3, assemblyColor, 2);
    dp.DrawLine(p3, p0, assemblyColor, 2);
}

void CAssemblyObject::DrawV6(CRhinoObjectDrawContext* drawContext) const
{
    // V6+ Drawing Pipeline — wird in Rhino 8 bevorzugt
    // Fallback auf Draw() wenn nicht spezifisch implementiert
    CRhinoBrepObject::DrawV6(drawContext);
}

// =============================================================================
// Bounding Box
// =============================================================================

ON_BoundingBox CAssemblyObject::BoundingBox() const
{
    ON_BoundingBox bbox;
    bbox.m_min = m_origin;
    bbox.m_max = m_origin + ON_3dVector(m_width, m_height, m_depth);
    return bbox;
}

ON_BoundingBox CAssemblyObject::BoundingBox(const CRhinoViewport* pViewport) const
{
    // Viewport-unabhängig — gleiche Box
    return BoundingBox();
}

// =============================================================================
// Grips
// =============================================================================

void CAssemblyObject::EnableGrips(bool bGripsOn)
{
    if (bGripsOn)
    {
        // Custom Grips erstellen — je einen pro Dimension
        // Die Grips sitzen an den Mittelpunkten der Kantenflächen
        CAssemblyGripArray* pGrips = new CAssemblyGripArray();

        // Width-Grip (rechte Fläche Mitte)
        CAssemblyGrip* pWidthGrip = new CAssemblyGrip();
        pWidthGrip->SetGripLocation(
            m_origin + ON_3dVector(m_width, m_height * 0.5, m_depth * 0.5)
        );
        pWidthGrip->SetDimensionType(CAssemblyGrip::DimensionType::Width);
        pWidthGrip->SetOwnerObject(this);
        pGrips->Append(pWidthGrip);

        // Height-Grip (obere Fläche Mitte)
        CAssemblyGrip* pHeightGrip = new CAssemblyGrip();
        pHeightGrip->SetGripLocation(
            m_origin + ON_3dVector(m_width * 0.5, m_height, m_depth * 0.5)
        );
        pHeightGrip->SetDimensionType(CAssemblyGrip::DimensionType::Height);
        pHeightGrip->SetOwnerObject(this);
        pGrips->Append(pHeightGrip);

        // Depth-Grip (vordere Fläche Mitte)
        CAssemblyGrip* pDepthGrip = new CAssemblyGrip();
        pDepthGrip->SetGripLocation(
            m_origin + ON_3dVector(m_width * 0.5, m_height * 0.5, m_depth)
        );
        pDepthGrip->SetDimensionType(CAssemblyGrip::DimensionType::Depth);
        pDepthGrip->SetOwnerObject(this);
        pGrips->Append(pDepthGrip);

        // Grips registrieren
        // HINWEIS: Die genaue API für Custom Grip Registration ist SDK-version-abhängig
        // In Rhino 8 wird CRhinoObject::SetGrips() oder NewGrips() verwendet
        // TODO: Genaue Methode im SDK verifizieren
    }
    else
    {
        // Grips entfernen — Basis-Implementierung reicht
        CRhinoBrepObject::EnableGrips(false);
    }
}

// =============================================================================
// Member-Verwaltung
// =============================================================================

void CAssemblyObject::AddMemberObject(ON_UUID objectId)
{
    m_memberObjects.Append(objectId);
}

void CAssemblyObject::RemoveMemberObject(ON_UUID objectId)
{
    for (int i = m_memberObjects.Count() - 1; i >= 0; i--)
    {
        if (ON_UuidCompare(m_memberObjects[i], objectId) == 0)
        {
            m_memberObjects.Remove(i);
            break;
        }
    }
}

const ON_SimpleArray<ON_UUID>& CAssemblyObject::GetMemberObjects() const
{
    return m_memberObjects;
}

void CAssemblyObject::SetDimensions(double w, double h, double d)
{
    m_width = w;
    m_height = h;
    m_depth = d;
    UpdateBrepFromDimensions();
}

// =============================================================================
// Brep-Geometrie aus Dimensionen erzeugen
// =============================================================================

void CAssemblyObject::UpdateBrepFromDimensions()
{
    // Box-Brep erstellen — die "visuelle Hülle" der Assembly
    ON_3dPoint corners[8];
    corners[0] = m_origin;
    corners[1] = m_origin + ON_3dVector(m_width, 0, 0);
    corners[2] = m_origin + ON_3dVector(m_width, m_height, 0);
    corners[3] = m_origin + ON_3dVector(0, m_height, 0);
    corners[4] = m_origin + ON_3dVector(0, 0, m_depth);
    corners[5] = m_origin + ON_3dVector(m_width, 0, m_depth);
    corners[6] = m_origin + ON_3dVector(m_width, m_height, m_depth);
    corners[7] = m_origin + ON_3dVector(0, m_height, m_depth);

    // ON_Brep::CreateFromBox() erzeugt einen Brep-Quader
    ON_Brep* pBrep = ON_Brep::New();
    if (pBrep)
    {
        // Hinweis: Die genaue API hängt von der openNURBS-Version ab
        // ON_BrepBox() oder manuelles Erstellen der 6 Flächen
        // Für den Prototyp nehmen wir an, dass eine Helper-Funktion existiert
        
        // Pseudo-Code:
        // RhinoCreateBox(corners, pBrep);
        
        // Brep dem Objekt zuweisen
        SetBrep(pBrep);
    }
}
