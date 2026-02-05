# AGENTS.md - Multi-Agent Setup for RhinoAssemblyOutliner

> Dokumentation des AI-gestützten Entwicklungsprozesses

## Overview

Dieses Projekt nutzt ein **Multi-Agent System** für effiziente Entwicklung. Jeder Agent hat spezialisierte Aufgaben.

---

## Agent-Rollen

### 🧠 Coordinator (Main Agent)

**Rolle:** Orchestration, Synthese, User-Kommunikation

**Aufgaben:**
- Aufgaben an Subagents delegieren
- Ergebnisse synthetisieren
- Finale Dokumentation erstellen
- Git Commits & Pushes
- User-Fragen beantworten

**Arbeitet in:** Hauptsession mit User

---

### 🔬 Research Agent

**Rolle:** CAD-Industrie Analyse, Best Practices

**Aufgaben:**
- Web-Recherche zu CAD-Systemen
- Feature-Vergleiche erstellen
- UX-Patterns dokumentieren
- Industry Standards identifizieren

**Output:** Markdown-Dokumente in `research/`

**Beispiele:**
- `SOLIDWORKS_ANALYSIS.md` — Deep-Dive SolidWorks FeatureManager
- `CAD_INDUSTRY_ANALYSIS.md` — Vergleich Inventor, Fusion, CATIA, NX

---

### 💻 Coder Agent

**Rolle:** Implementation, Code-Schreiben

**Aufgaben:**
- Feature-Implementation
- Bug-Fixes
- Code-Refactoring
- Unit Tests

**Fokus:**
- C# für UI, Services, Commands
- C++ für DisplayConduit, UserData (future)

---

### 🧪 Tester Agent (Planned)

**Rolle:** Quality Assurance

**Aufgaben:**
- Testpläne erstellen
- Edge Cases identifizieren
- Bug Reports dokumentieren
- Regression Testing

---

### 📝 Docs Agent

**Rolle:** Dokumentation

**Aufgaben:**
- README pflegen
- API-Docs schreiben
- User Guides erstellen
- Changelogs führen

---

## Workflow

```
┌─────────────────────────────────────────────────────────────┐
│                    USER REQUEST                              │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                    COORDINATOR                               │
│              (analysiert, plant, delegiert)                 │
└───────────────────────┬─────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
┌───────────────┐ ┌───────────────┐ ┌───────────────┐
│   RESEARCH    │ │    CODER      │ │     DOCS      │
│    AGENT      │ │    AGENT      │ │    AGENT      │
└───────┬───────┘ └───────┬───────┘ └───────┬───────┘
        │                 │                 │
        ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────────────┐
│                    COORDINATOR                               │
│         (sammelt Ergebnisse, synthetisiert)                 │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              COMMIT & COMMUNICATE                            │
│         (git push, Zusammenfassung an User)                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Parallele Arbeit

Agents können **parallel** arbeiten wenn ihre Tasks unabhängig sind:

```
Session 1: Research SolidWorks    ─────┐
                                       │
Session 2: Research CAD Industry  ─────┼───► Coordinator Synthesis
                                       │
Session 3: Docs Update            ─────┘
```

---

## File Conventions

| Agent | Output Location | Naming |
|-------|-----------------|--------|
| Research | `research/` | `TOPIC_ANALYSIS.md` |
| Coder | `src/` | Standard C#/C++ conventions |
| Docs | `docs/`, root | `FEATURE.md`, `README.md` |
| Coordinator | root | `CLAUDE.md`, `AGENTS.md`, commits |

---

## Session Tracking

Jede Agent-Session wird in `progress.txt` dokumentiert:

```
## Phase X: Feature Name
[x] Task 1 (Agent: Research)
[x] Task 2 (Agent: Coder)
[ ] Task 3 (Agent: Docs)
```

---

## Current Agent Activity (2026-02-05)

| Agent | Status | Current Task |
|-------|--------|--------------|
| Coordinator | ✅ Active | Synthesis & Doc Updates |
| Research (SW) | ✅ Completed | SolidWorks Analysis |
| Research (CAD) | ✅ Completed | Industry Analysis |
| Coder | ⏸️ Paused | Waiting for C++ SDK setup |
| Docs | ✅ Active | Doc Updates |

---

## Communication

- **Subagents → Coordinator:** Via final message in session
- **Coordinator → User:** Via chat response
- **File-based handoff:** Docs in `research/`, `docs/`

---

*Erstellt: 2026-02-05*
