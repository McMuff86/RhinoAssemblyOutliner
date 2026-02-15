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

## Phase 2: C++ Core Implementation ✅ DONE

All core C++ components implemented as of 2026-02-15:

### 2.1 Visibility Data Management ✅
- `CVisibilityData` — thread-safe state store with `CRITICAL_SECTION` + RAII `CAutoLock`
- `ComponentState` enum: `CS_VISIBLE(0)`, `CS_HIDDEN(1)`, `CS_SUPPRESSED(2)`, `CS_TRANSPARENT(3)`
- Path-based addressing (dot-separated indices: `"0"`, `"1.0"`, `"1.0.2"`)
- `CVisibilitySnapshot` — lock-free per-frame snapshot pattern for zero contention during render
- `HasHiddenDescendants` optimization — precomputed parent prefix set for O(1) lookups
- Custom `ON_UUID_Hash` / `ON_UUID_Equal` for `std::unordered_map` keys

### 2.2 Custom Display Conduit ✅
- `CVisibilityConduit` — 4-channel conduit:
  - **SC_PREDRAWOBJECTS** — takes snapshot once per frame
  - **SC_DRAWOBJECT** — suppresses managed instances, redraws visible components with path filtering
  - **SC_POSTDRAWOBJECTS** — selection highlights via `DrawObject` (no per-frame heap allocs)
  - **SC_CALCBOUNDINGBOX** — correct ZoomExtents using only visible components (suppressed excluded)
- Recursive nested block handling (`DrawNestedFiltered`) with max depth 32
- `CS_TRANSPARENT` rendering with alpha overlay (~30% opacity)
- Component color resolution (object/layer/parent color sources)

### 2.3 API für C# Integration ✅
- 14 exported `extern "C" __stdcall` functions (see `API_REFERENCE.md`)
- API version 4 (ComponentState enum + conduit improvements)
- `CComponentVisibilityData` (ON_UserData) for .3dm persistence
- `CDocEventHandler` for auto-sync on open/save/close/delete

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

## What's Next

### CRhinoCacheHandle Integration
- Cache display lists per managed instance to avoid reprocessing geometry every frame
- Key performance optimization for 100+ managed instances

### SRWLOCK Migration
- Replace `CRITICAL_SECTION` with `SRWLOCK` for reader/writer separation
- Multiple render threads can snapshot concurrently; only state mutations take exclusive lock

### Display States
- Rich display modes per ComponentState (e.g., wireframe-only for transparent, custom materials)
- Integration with Rhino display modes (shaded, rendered, etc.)

### Validation Testing
- Runtime testing in Rhino 8 with real assemblies (pending Windows build)
- Performance profiling with 100+ managed instances

---

*Erstellt: 2026-02-05 nach C# PoC Erkenntnissen*  
*Updated: 2026-02-15 — Phase 2 complete, snapshot pattern, ComponentState enum, 4-channel conduit*
