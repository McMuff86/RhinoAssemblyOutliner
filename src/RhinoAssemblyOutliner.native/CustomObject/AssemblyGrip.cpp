// AssemblyGrip.cpp — Custom Grip Implementation
// Proof of Concept

#include "stdafx.h"
#include "AssemblyGrip.h"
#include "AssemblyObject.h"

// =============================================================================
// CAssemblyGrip
// =============================================================================

CAssemblyGrip::CAssemblyGrip()
    : CRhinoGripObject()
{
}

CAssemblyGrip::~CAssemblyGrip()
{
}

void CAssemblyGrip::SetGripLocation(const ON_3dPoint& pt)
{
    // CRhinoGripObject speichert die Position als ON_3dPoint
    // Die genaue Methode hängt vom SDK ab:
    // m_grip_location = pt;  // oder SetPoint(pt);
}

void CAssemblyGrip::NewLocation()
{
    // Wird aufgerufen NACHDEM der User den Grip gezogen hat.
    // m_grip_location enthält die neue Position.
    
    if (!m_pOwner)
        return;

    // Delta berechnen: Differenz zwischen alter und neuer Position
    // ON_3dPoint newPt = GripLocation();  // neue Position nach Drag
    // ON_3dPoint oldPt = OriginalGripLocation();

    // Je nach DimensionType die entsprechende Dimension updaten
    switch (m_dimType)
    {
    case DimensionType::Width:
        {
            // Width ändert sich entlang X-Achse
            // double newWidth = newPt.x - m_pOwner->GetOrigin().x;
            // m_pOwner->SetDimensions(newWidth, m_pOwner->GetHeight(), m_pOwner->GetDepth());
        }
        break;

    case DimensionType::Height:
        {
            // Height ändert sich entlang Y-Achse
            // double newHeight = newPt.y - m_pOwner->GetOrigin().y;
            // m_pOwner->SetDimensions(m_pOwner->GetWidth(), newHeight, m_pOwner->GetDepth());
        }
        break;

    case DimensionType::Depth:
        {
            // Depth ändert sich entlang Z-Achse
            // double newDepth = newPt.z - m_pOwner->GetOrigin().z;
            // m_pOwner->SetDimensions(m_pOwner->GetWidth(), m_pOwner->GetHeight(), newDepth);
        }
        break;
    }

    // Brep-Geometrie neu berechnen
    m_pOwner->UpdateBrepFromDimensions();
}

void CAssemblyGrip::Draw(CRhinoDisplayPipeline& dp) const
{
    // Custom Grip-Darstellung: Farbige Quadrate statt Standard-Punkte
    ON_Color gripColor;
    
    switch (m_dimType)
    {
    case DimensionType::Width:
        gripColor = ON_Color(255, 0, 0);   // Rot = X/Width
        break;
    case DimensionType::Height:
        gripColor = ON_Color(0, 255, 0);   // Grün = Y/Height
        break;
    case DimensionType::Depth:
        gripColor = ON_Color(0, 0, 255);   // Blau = Z/Depth
        break;
    }

    // Grip als 6px Quadrat zeichnen
    // dp.DrawPoint(GripLocation(), 6, gripColor);
    
    // Alternative: Rhino-Standard-Grip mit eigener Farbe
    CRhinoGripObject::Draw(dp);
}

bool CAssemblyGrip::GetGripDirections(
    ON_3dVector& vx, ON_3dVector& vy, ON_3dVector& vz) const
{
    // Constraint: Grip nur entlang seiner Achse ziehbar
    switch (m_dimType)
    {
    case DimensionType::Width:
        vx = ON_3dVector::XAxis;
        vy = ON_3dVector::ZeroVector;
        vz = ON_3dVector::ZeroVector;
        return true;

    case DimensionType::Height:
        vx = ON_3dVector::ZeroVector;
        vy = ON_3dVector::YAxis;
        vz = ON_3dVector::ZeroVector;
        return true;

    case DimensionType::Depth:
        vx = ON_3dVector::ZeroVector;
        vy = ON_3dVector::ZeroVector;
        vz = ON_3dVector::ZAxis;
        return true;
    }
    return false;
}

// =============================================================================
// CAssemblyGripArray
// =============================================================================

CAssemblyGripArray::~CAssemblyGripArray()
{
    for (int i = 0; i < m_grips.Count(); i++)
    {
        delete m_grips[i];
    }
    m_grips.Empty();
}

void CAssemblyGripArray::Append(CAssemblyGrip* pGrip)
{
    m_grips.Append(pGrip);
}
