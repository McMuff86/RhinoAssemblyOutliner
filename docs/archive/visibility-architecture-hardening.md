---
name: Visibility Architecture Hardening
overview: Den bestehenden C++ Display Conduit Ansatz haerten und die bekannten Probleme (Ghost-Artifacts, Display-Cache-Bypass, fehlende Selection-Highlights) systematisch loesen. Zusaetzlich das Datenmodell nach dem Fusion 360 Dual-Property-Pattern verbessern.
todos:
  - id: phase1-model
    content: "Phase 1: Fusion 360 Dual-Property Pattern (IsLightBulbOn + IsEffectivelyVisible) in AssemblyNode einfuehren"
    status: pending
  - id: phase2-conduit-channels
    content: "Phase 2a: C++ Conduit um SC_OBJECTCULLING und SC_POSTDRAWOBJECTS erweitern"
    status: pending
  - id: phase2-cache
    content: "Phase 2b: Display-Cache-Invalidierung verbessern (ReplaceObject statt Conduit-Toggle)"
    status: pending
  - id: phase3-userdata
    content: "Phase 3: VisibilityStateUserData fuer Persistenz implementieren"
    status: pending
  - id: phase4-cleanup
    content: "Phase 4: Debug-Logging aus TreeBuilder entfernen"
    status: pending
isProject: false
---

# Per-Instance Visibility Architecture: Hardening Plan

## Forschungsergebnis

Rhino hat **keine native API** fuer per-Instance Block Component Visibility. Alle gaengigen Alternativen (ModifyGeometry, Clone Definitions, Layer Visibility) funktionieren entweder nur global (alle Instanzen betroffen) oder brechen die Block-Semantik.

Der **C++ Display Conduit ist der einzig gangbare Weg** fuer echte per-Instance Visibility. Die aktuellen Probleme sind loesbare Engineering-Challenges.

### Wie SolidWorks/Fusion 360 es machen

SolidWorks und Fusion 360 besitzen die **gesamte Render-Pipeline** und setzen Visibility als einfache Daten-Property um:

- **SolidWorks:** `IComponent2.Visible` boolean, plus "Display States" fuer mehrere Konfigurationen
- **Fusion 360:** Dual-Property `IsLightBulbOn` (lokale Absicht) + `IsVisible` (effektiv, berechnet aus Parent-Chain)
- Beide: Renderer liest die Property und skippt versteckte Komponenten **vor** dem Zeichnen

Wir koennen die Render-Pipeline nicht besitzen, aber das **Datenmodell** nach diesem Vorbild gestalten.

## Aktuelle Probleme (Diagnose)

1. **Ghost/Partial Visibility:** Versteckte Objekte sind noch "halb sichtbar" -- der Conduit zeichnet Komponenten manuell via `dp.DrawObject()`, aber Rhino's eigene Render-Passes (Wireframe-Overlay, Selection-Highlights, Shadow-Pass) werden nicht abgefangen

2. **Display-Cache-Bypass:** Ohne Selektion nutzt Rhino cached Display-Listen und ruft den Conduit moeglicherweise nicht auf

3. **State-Reset bei Deselektion:** Visibility-State im Outliner springt zurueck, weil der Conduit nicht konsistent feuert und der C#-State und C++-State out-of-sync geraten

## Architektur-Aenderungen

### Phase 1: Datenmodell verbessern (C#)

Fusion 360 Dual-Property-Pattern in `AssemblyNode` einfuehren:

- `IsLightBulbOn` -- boolean, lokale Absicht des Users (per Node)
- `IsEffectivelyVisible` -- computed property: `IsLightBulbOn && Parent.IsEffectivelyVisible`
- Trennung von **User-Intent** und **Render-State** verhindert State-Desync

Betroffene Files:
- [src/RhinoAssemblyOutliner/Model/AssemblyNode.cs](src/RhinoAssemblyOutliner/Model/AssemblyNode.cs) -- Neue Properties
- [src/RhinoAssemblyOutliner/Services/VisibilityService.cs](src/RhinoAssemblyOutliner/Services/VisibilityService.cs) -- Logik anpassen
- [src/RhinoAssemblyOutliner/UI/AssemblyTreeView.cs](src/RhinoAssemblyOutliner/UI/AssemblyTreeView.cs) -- Icon-Logik fuer inherited visibility

### Phase 2: C++ Conduit haerten

**Problem:** `SC_DRAWOBJECT` allein reicht nicht, da Rhino mehrere Render-Passes hat.

Loesung: Mehrere Conduit-Channels nutzen:

```
SC_CALCBOUNDINGBOX   -- BBox korrekt halten
SC_OBJECTCULLING     -- Managed Objects nicht cullen lassen
SC_DRAWOBJECT        -- Hauptlogik: suppress + re-draw (bestehend)
SC_POSTDRAWOBJECTS   -- Selection-Highlights fuer sichtbare Komponenten nachzeichnen
```

Betroffene Files:
- [src/RhinoAssemblyOutliner.native/VisibilityConduit.h](src/RhinoAssemblyOutliner.native/VisibilityConduit.h)
- [src/RhinoAssemblyOutliner.native/VisibilityConduit.cpp](src/RhinoAssemblyOutliner.native/VisibilityConduit.cpp)

**Display-Cache-Invalidierung:** Statt Conduit-Toggle (aktueller Hack) eine gezieltere Methode verwenden. Option: Nach Visibility-Aenderung das betroffene Object per `CRhinoDoc::ReplaceObject()` mit sich selbst ersetzen -- das invalidiert den Cache fuer genau dieses Objekt.

Betroffene Files:
- [src/RhinoAssemblyOutliner.native/NativeApi.cpp](src/RhinoAssemblyOutliner.native/NativeApi.cpp) -- `RedrawActiveDoc()` verbessern

### Phase 3: State-Persistenz via UserData

Per-Instance Visibility-State als `UserData` auf dem `InstanceObject` speichern, damit:
- State ueberlebt Save/Load
- State ueberlebt Undo/Redo
- C# und C++ lesen denselben Ground-Truth

Neue Files:
- `src/RhinoAssemblyOutliner/Model/VisibilityStateUserData.cs` -- Custom UserData Klasse
- C++ Seite: Liest UserData beim Conduit-Aufruf statt eigener Map

### Phase 4: Diagnostik-Tooling entfernen

Debug-Logging aus `AssemblyTreeBuilder.ProcessDefinitionContents` entfernen (aktuell temporaer eingebaut).

## Priorisierung

- **Phase 1** (Datenmodell) -- behebt State-Desync, niedrige Komplexitaet
- **Phase 2** (Conduit Hardening) -- behebt Ghost-Artifacts und Cache-Issues, mittlere Komplexitaet
- **Phase 3** (Persistenz) -- behebt Save/Load, mittlere Komplexitaet
- **Phase 4** (Cleanup) -- Housekeeping

## Nicht im Scope

- "Suppress" Feature (wie SolidWorks) -- spaeter, braucht Geometry-Unload
- "Display States" (wie SolidWorks) -- spaeter, mehrere Visibility-Konfigurationen
- Mac-Kompatibilitaet des Conduits
