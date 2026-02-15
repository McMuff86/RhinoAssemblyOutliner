# Roadmap — BlockForge (RhinoAssemblyOutliner)

> *BlockForge — das Assembly-System für Rhino.*

---

## Phase 1: Assembly Outliner ✅ DONE

Dockbares Panel mit hierarchischer Block-Navigation, Visibility Toggle, Selection Sync, Assembly Mode.

**Status:** Sprint 1+2 complete. 97 Tests, CI, Plugin Icons, User Guide.

---

## Phase 2: Per-Instance Visibility via Definition Cloning

Echte Per-Instance Component Visibility durch Definition Cloning (VariantManager). Jede Instanz kann individuelle Sichtbarkeiten haben — ohne dass andere Instanzen derselben Definition betroffen sind.

**Kern:** `VariantManager` erstellt deduplizierte Variant-Definitionen. Naming: `{Name}__aov_{hash}`.

**Sprint 3** — Definition Cloning MVP (C#)

---

## Phase 3: C++ Persistence + Custom Grips

ON_UserData (C++) für persistente Assembly-Daten. Custom Grips für Viewport-Interaktion. Properties Panel.

**Kern:** `ON_AssemblyUserData` auf InstanceObjects. P/Invoke Bridge. CAssemblyGrip.

**Sprint 4** — ON_UserData + Persistence  
**Sprint 5** — Custom Grips + Properties Panel

---

## Phase 4: Configuration System

Named Configurations pro Instance (wie SolidWorks Configurations). Vererbung, Batch-Operations.

**Sprint 6** — Configuration System

---

## Phase 5: BOM + Food4Rhino Release 🚀

BOM Export (CSV/Excel), "Export for Sharing", Yak Package, Food4Rhino Listing.

**Sprint 7** — BOM Export + Release  
**Ziel:** Q3 2026

---

## Phase 6: Grasshopper Integration + Advanced Features

Grasshopper Components, Nested Block Visibility, Dimension/Material Overrides, Assembly Constraints, Varianten-Manager, Drawing Integration, Explosionsdarstellung.

**Sprint 8+** — priorisiert nach User-Feedback

---

## Vision

**BlockForge** wird das erste Assembly-Management-System für Rhino:
- Per-Instance Intelligence auf Rhino Blocks
- Configurations, Custom Grips, Properties
- BOM Export, Cutting Lists
- Domain-agnostisch: Möbelbau, Metallbau, Produktdesign

*"SolidWorks Assembly Management — inside Rhino."*
