# Feasibility Report: Custom Rhino Object für Assembly Outliner

**Datum:** 2026-02-15  
**Branch:** nightly/15-02-sprint1-refactor  
**Status:** ✅ Machbar — mit Einschränkungen

---

## 1. Zusammenfassung

Ein Custom Rhino Object in C++ ist **machbar und die richtige Architektur** für das Assembly-Konzept. Der empfohlene Ansatz ist eine **CRhinoBrepObject-Subklasse** (nicht reines CRhinoObject), da dies 80% der nötigen Infrastruktur (Draw, Pick, Serialize, Mesh) erbt.

**Geschätzter Aufwand:** 3-5 Tage für funktionierenden Prototyp, 2-3 Wochen für Production-Ready.

---

## 2. Architektur-Entscheid

### Empfohlen: CRhinoBrepObject-Subklasse

```
CRhinoObject
  └── CRhinoBrepObject
        └── CAssemblyObject  ← Unsere Klasse
```

**Warum CRhinoBrepObject und nicht CRhinoObject direkt?**

| Aspekt | CRhinoObject (rein) | CRhinoBrepObject |
|--------|-------------------|------------------|
| Draw | Komplett selbst | Erbt Wireframe + Shaded |
| Pick/Select | Komplett selbst | Erbt Brep-Picking |
| Serialisierung | Eigene ON_Geometry | Erbt Brep-I/O |
| Mesh für Shaded | MeshObjects() override | Automatisch |
| Overrides nötig | ~15+ | ~5-6 |
| Aufwand | 3-4 Wochen | 3-5 Tage |

**Konzept:** Die Assembly bekommt eine unsichtbare/transparente Bounding-Box als Brep. Die eigentlichen Member-Objekte (Bauteile) bleiben separate Rhino-Objekte, referenziert via UUID-Liste im CAssemblyObject.

### Alternative: UserData-Only (kein Custom Object)

Statt eines Custom Objects könnten wir auch ON_UserData an bestehende Gruppen/Objekte hängen. **Nachteil:** Keine Custom Grips, kein eigenes Draw, keine eigene Selektion.

---

## 3. Custom Grips — Feasibility

### CRhinoGripObject-Subklasse: ✅ Voll unterstützt

Die Rhino C++ SDK hat explizite Unterstützung für Custom Grips:
- `GRIP_TYPE::custom_grip` (Wert 1000) — für Plugin-definierte Grips
- `GRIP_TYPE::custom_nodragline_grip` (1001) — ohne Drag-Linie
- `NewLocation()` Override — wird nach Grip-Drag aufgerufen
- Custom Draw möglich

**Constraint-System:** Grips können auf Achsen beschränkt werden (Width nur X, Height nur Y, etc.).

### Offene Fragen bei Grips:
- Wie genau registriert man Custom Grips bei `EnableGrips()`?
- Wie funktioniert das Undo/Redo bei Grip-Drag?
- Performance bei vielen Assemblies mit aktiven Grips?

---

## 4. Registration & Object Factory

### Kein explizites "RegisterCustomObject" nötig

Anders als bei .NET/RhinoCommon gibt es in der C++ SDK **kein Object Factory Pattern**. Custom Objects werden einfach:

1. **Instanziiert** (via `new CAssemblyObject()`)
2. **Zum Dokument hinzugefügt** (via `CRhinoDoc::AddObject()`)
3. **Identifiziert** über `ModelObjectId()` UUID

### Serialisierung (3dm-Dateien)

**RISIKO:** Dies ist der kritischste Punkt. Beim Speichern/Laden einer .3dm-Datei:
- Die Brep-Geometrie wird automatisch von CRhinoBrepObject serialisiert
- Unsere Custom-Daten (Member-UUIDs, Dimensionen) müssen via **ON_UserData** gespeichert werden
- Beim Laden muss Rhino wissen, wie es ein CAssemblyObject rekonstruiert → **DECLARE_SERIAL / IMPLEMENT_SERIAL** Makros (MFC)

**Risiko-Bewertung:** Wenn die .3dm-Datei ohne unser Plugin geladen wird, wird das Objekt als normales Brep geladen. Die Custom-UserData geht nicht verloren (OpenNURBS speichert unbekannte UserData), aber die Assembly-Logik fehlt.

---

## 5. SDK Setup & Konfiguration

### Bestehende vcxproj-Analyse

Das bestehende Projekt ist **korrekt konfiguriert**:
- ✅ `Rhino.Cpp.PlugInComponent.props` wird importiert (enthält alle Include-Pfade und Libs)
- ✅ `RhinoSdkPath` korrekt über Registry oder Fallback auf `C:\Program Files\Rhino 8 SDK\`
- ✅ MFC Dynamic Linking aktiviert
- ✅ PlatformToolset v143 (VS 2022)
- ✅ x64 Only (Rhino 8 ist 64-bit only)
- ✅ `stdafx.h` inkludiert `RhinoSdk.h` und `RhRdkHeaders.h`

### Benötigte Header (bereits via RhinoSdk.h verfügbar)

```
rhinoSdkObject.h      — CRhinoObject, CRhinoBrepObject
rhinoSdkGrips.h       — CRhinoGripObject
rhinoSdkDisplayPipeline.h — CRhinoDisplayPipeline
rhinoSdkDoc.h         — CRhinoDoc
opennurbs.h           — ON_Brep, ON_BoundingBox, ON_UUID, etc.
```

### Benötigte Libs (automatisch via PropertySheet)

```
rdk_rhXX.lib
rhino8_rhXX.lib
opennurbs_rhXX.lib
```

### Was fehlt im vcxproj:

Für die Custom Object Files müssen wir hinzufügen:
```xml
<ClCompile Include="CustomObject\AssemblyObject.cpp" />
<ClCompile Include="CustomObject\AssemblyGrip.cpp" />
<ClInclude Include="CustomObject\AssemblyObject.h" />
<ClInclude Include="CustomObject\AssemblyGrip.h" />
```

---

## 6. Technische Risiken

### 🔴 Hoch
1. **Serialisierung / Roundtrip:** Custom Object muss korrekt in .3dm gespeichert und geladen werden. Wenn das Plugin nicht geladen ist, geht Custom-Logik verloren.
2. **MFC DECLARE_SERIAL:** Funktioniert dies mit CRhinoBrepObject-Subklassen? Nicht explizit in Docs.

### 🟡 Mittel
3. **Grip-Registration:** Die genaue API für `EnableGrips()` mit Custom Grips ist spärlich dokumentiert. Möglicherweise braucht es `CRhinoObjectGrips`-Klasse statt direktem Array.
4. **Undo/Redo:** Custom Objects müssen korrekt mit Rhino's Undo-System interagieren.
5. **Copy/Paste:** DuplicateObject() muss Deep Copy aller Custom-Daten machen.

### 🟢 Niedrig
6. **Draw Performance:** BrepObject-Draw ist gut optimiert, Custom Overlay minimal.
7. **API-Kompatibilität:** Rhino 8 SDK ist stabil, keine Breaking Changes seit Rhino 7.

---

## 7. McNeel Samples — Recherche-Ergebnis

### Gefundene relevante Samples:
- **mcneel/rhino-developer-samples** (Branch `8`, Ordner `cpp/`) — Haupt-Sample-Repo
- **Rhino4Samples_CPP**, **Rhino5Samples_CPP**, **Rhino6Samples_CPP** — ältere Versionen
- **Kein dediziertes "SampleCustomObject"** gefunden — Custom Objects sind selten in Samples

### Relevante Discourse-Posts:
- "[C++ SDK] Custom grip shapes" — bestätigt dass Custom Grips funktionieren
- Developer API Docs für CRhinoGripObject zeigen `custom_grip` Type

### Empfehlung:
- McNeel Developer Forum (discourse.mcneel.com) für spezifische Fragen nutzen
- **Keine spezielle Developer License nötig** — Rhino 8 SDK ist frei verfügbar
- McNeel Support ist sehr responsive auf dem Developer Forum

---

## 8. Prototype Code

Erstellt in `src/RhinoAssemblyOutliner.native/CustomObject/`:

| Datei | Beschreibung |
|-------|-------------|
| `AssemblyObject.h` | CRhinoBrepObject-Subklasse mit Dimensionen + Member-Liste |
| `AssemblyObject.cpp` | Implementation inkl. Draw, BBox, Grips, Member-Verwaltung |
| `AssemblyGrip.h` | CRhinoGripObject-Subklasse mit Achsen-Constraint |
| `AssemblyGrip.cpp` | Implementation inkl. NewLocation, Custom Draw |

---

## 9. Nächste Schritte

1. **Files ins vcxproj aufnehmen** und Kompilierung testen
2. **ON_UserData-Klasse** für Assembly-Daten schreiben (Serialisierung)
3. **Command schreiben** (`CCommandCreateAssembly`) der ein CAssemblyObject erzeugt
4. **Grip-Registration** im SDK verifizieren (ggf. McNeel Forum fragen)
5. **Roundtrip-Test:** Assembly erstellen → speichern → laden → prüfen

---

## 10. Fazit

**Die Machbarkeit ist gegeben.** Der CRhinoBrepObject-Ansatz ist solide und das bestehende Projekt ist korrekt konfiguriert. Die grössten Risiken liegen in der Serialisierung und der Grip-Registration, beides lösbar mit SDK-Dokumentation und McNeel Forum Support.

**Empfehlung:** Prototyp starten, frühzeitig den Serialisierungs-Roundtrip testen.
