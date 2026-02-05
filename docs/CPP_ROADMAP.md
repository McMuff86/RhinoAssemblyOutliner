# C++ SDK Roadmap: Per-Instance Component Visibility

## Warum C++?

Der C# DisplayConduit-Ansatz hat fundamentale Limitationen bewiesen (siehe [PER_INSTANCE_VISIBILITY.md](./PER_INSTANCE_VISIBILITY.md)):
- Ghost Artifacts bei Transforms
- Keine Integration mit Rhino's Display Cache
- Screen Invalidation funktioniert nicht

**C++ bietet tieferen Zugriff auf:**
- `CRhinoDisplayPipeline` internals
- `CRhinoDisplayConduit` mit mehr Channels
- Direct OpenGL/DirectX access (falls nötig)
- Native Display List Manipulation

---

## Architektur: Hybrid C++/C# Plugin

```
┌─────────────────────────────────────────────────────┐
│                  Rhino 8                            │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌─────────────────────┐  ┌─────────────────────┐  │
│  │   C# Plugin (UI)    │  │   C++ Plugin (Core) │  │
│  │                     │  │                     │  │
│  │  - Outliner Panel   │  │  - Display Conduit  │  │
│  │  - Commands         │◄─┤  - Block Rendering  │  │
│  │  - Tree View        │  │  - Visibility Logic │  │
│  │  - Selection Sync   │  │  - Cache Management │  │
│  │                     │  │                     │  │
│  └─────────────────────┘  └─────────────────────┘  │
│           │                        │                │
│           └────────┬───────────────┘                │
│                    │                                │
│           P/Invoke oder COM                         │
│                                                     │
└─────────────────────────────────────────────────────┘
```

---

## Phase 1: Research & Setup

### 1.1 C++ SDK Setup
- [ ] Rhino 8 C++ SDK installieren
- [ ] Visual Studio C++ Projekt Template
- [ ] Build-Konfiguration für .rhp Output
- [ ] Hello World C++ Plugin testen

### 1.2 Display Pipeline Research
- [ ] `CRhinoDisplayConduit` Channels verstehen
- [ ] `SC_PREDRAWOBJECT` vs `SC_DRAWOBJECT` testen
- [ ] Block-Rendering Flow analysieren
- [ ] Source: `CRhinoInstanceObject::Draw()` verstehen

### 1.3 Key Questions zu beantworten
- [ ] Kann man in C++ das Zeichnen einzelner Block-Komponenten intercepten?
- [ ] Gibt es `CRhinoInstanceObject::DrawComponent(index)` oder ähnlich?
- [ ] Wie handled Rhino Block Display Caching?
- [ ] Kann man Instance-spezifische Display Lists erstellen?

---

## Phase 2: C++ Core Implementation

### 2.1 Visibility Data Management
```cpp
// Konzept: Custom User Data für Instances
class CComponentVisibilityData : public ON_UserData
{
public:
    ON_SimpleArray<int> m_hidden_components;
    
    // Serialization
    bool Write(ON_BinaryArchive&) const override;
    bool Read(ON_BinaryArchive&) override;
};
```

### 2.2 Custom Display Conduit
```cpp
class CPerInstanceVisibilityConduit : public CRhinoDisplayConduit
{
public:
    CPerInstanceVisibilityConduit();
    
    // Override drawing
    bool ExecConduit(
        CRhinoDisplayPipeline& dp,
        UINT nActiveChannel,
        bool& bTerminate
    ) override;

private:
    void DrawInstanceWithHiddenComponents(
        CRhinoDisplayPipeline& dp,
        const CRhinoInstanceObject& instance,
        const CComponentVisibilityData& visData
    );
};
```

### 2.3 API für C# Integration
```cpp
// Exported functions für P/Invoke
extern "C" {
    __declspec(dllexport) bool SetComponentVisibility(
        ON_UUID instanceId, 
        int componentIndex, 
        bool visible
    );
    
    __declspec(dllexport) bool IsComponentVisible(
        ON_UUID instanceId,
        int componentIndex
    );
    
    __declspec(dllexport) int GetHiddenComponentCount(
        ON_UUID instanceId
    );
}
```

---

## Phase 3: C# Integration

### 3.1 P/Invoke Wrapper
```csharp
public static class PerInstanceVisibilityNative
{
    [DllImport("RhinoAssemblyOutliner.Native.rhp")]
    public static extern bool SetComponentVisibility(
        Guid instanceId, 
        int componentIndex, 
        bool visible
    );
    
    // ... weitere Funktionen
}
```

### 3.2 UI Integration
- [ ] Toggle Button pro Komponente im Outliner
- [ ] Context Menu "Hide Component" / "Show Component"
- [ ] Visual Feedback (Icon) für hidden state

---

## Ressourcen

### Rhino C++ SDK
- [SDK Download](https://www.rhino3d.com/developers/)
- [C++ API Reference](https://developer.rhino3d.com/api/cpp/)
- [SDK Samples](https://github.com/mcneel/rhino-developer-samples)

### Relevante Klassen
- `CRhinoDisplayConduit` - Display Pipeline Hooks
- `CRhinoDisplayPipeline` - Rendering Pipeline
- `CRhinoInstanceObject` - Block Instance
- `CRhinoInstanceDefinition` - Block Definition
- `ON_UserData` - Custom Data Storage

### Forum Threads
- [Per Block Instance Wishes](https://discourse.mcneel.com/t/per-block-instance-wishes/185033)
- [DisplayConduit Cannot Hide Block](https://discourse.mcneel.com/t/displayconduit-cannot-hide-block-instance-definition/183638)

---

## Risiken & Mitigation

| Risiko | Impact | Mitigation |
|--------|--------|------------|
| C++ SDK kann es auch nicht | Hoch | Früh testen, McNeel Forum fragen |
| Build-Komplexität | Mittel | CI/CD Setup, klare Doku |
| C#/C++ Interface Bugs | Mittel | Extensive Testing, Error Handling |
| Rhino Version Compatibility | Mittel | Nur Rhino 8 targeten initially |

---

## Timeline (Schätzung)

| Phase | Aufwand | Beschreibung |
|-------|---------|--------------|
| 1.1-1.3 | 2-3 Tage | Setup & Research |
| 2.1-2.3 | 1-2 Wochen | C++ Core Implementation |
| 3.1-3.2 | 3-5 Tage | C# Integration |
| Testing | 1 Woche | Edge Cases, Performance |

**Total: ~3-4 Wochen** für Production-Ready Feature

---

## Nächste Aktion

1. **Rhino C++ SDK herunterladen und installieren**
2. **Hello World C++ Plugin erstellen**
3. **CRhinoDisplayConduit Beispiel aus SDK Samples testen**
4. **Experiment: Block-Rendering intercepten**

---

*Erstellt: 2026-02-05 nach C# PoC Erkenntnissen*
