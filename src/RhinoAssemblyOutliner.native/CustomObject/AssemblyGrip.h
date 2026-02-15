// AssemblyGrip.h — Custom Grip für Assembly-Dimensionen
// Proof of Concept

#pragma once

#include "stdafx.h"

class CAssemblyObject; // Forward declaration

// =============================================================================
// CAssemblyGrip — Ein Grip der eine Assembly-Dimension steuert
// =============================================================================
// Rhino Grips sind CRhinoGripObject-Subklassen.
// Wenn der User einen Grip zieht, wird NewLocation() aufgerufen,
// und wir können die zugehörige Dimension aktualisieren.
//
// GRIP_TYPE: Wir verwenden custom_grip (1000) für unsere Grips.
// =============================================================================

class CAssemblyGrip : public CRhinoGripObject
{
public:
    enum class DimensionType
    {
        Width,
        Height,
        Depth
    };

    CAssemblyGrip();
    virtual ~CAssemblyGrip();

    // --- Grip Configuration ---
    
    void SetDimensionType(DimensionType type) { m_dimType = type; }
    DimensionType GetDimensionType() const { return m_dimType; }

    void SetOwnerObject(CAssemblyObject* pOwner) { m_pOwner = pOwner; }

    void SetGripLocation(const ON_3dPoint& pt);

    // --- CRhinoGripObject Overrides ---

    // Grip-Typ: custom_grip für unsere Assembly-Grips
    GRIP_TYPE GripType() const override { return custom_grip; }

    // Wird aufgerufen wenn der Grip gezogen wird
    // Hier aktualisieren wir die Assembly-Dimension
    void NewLocation() override;

    // Custom Draw: Grips als farbige Quadrate statt Standard-Punkte
    void Draw(CRhinoDisplayPipeline& dp) const override;

    // Constraint: Grip nur entlang einer Achse ziehbar
    // (Width-Grip nur in X, Height-Grip nur in Y, etc.)
    bool GetGripDirections(
        ON_3dVector& vx, 
        ON_3dVector& vy, 
        ON_3dVector& vz
    ) const;

protected:
    DimensionType m_dimType = DimensionType::Width;
    CAssemblyObject* m_pOwner = nullptr;
};


// =============================================================================
// CAssemblyGripArray — Container für die Grips einer Assembly
// =============================================================================
// Rhino erwartet dass Grips als Array übergeben werden.

class CAssemblyGripArray
{
public:
    CAssemblyGripArray() = default;
    ~CAssemblyGripArray();

    void Append(CAssemblyGrip* pGrip);
    int Count() const { return m_grips.Count(); }
    CAssemblyGrip* operator[](int i) { return m_grips[i]; }

private:
    ON_SimpleArray<CAssemblyGrip*> m_grips;
};
