# Rhino Assembly Outliner – Spezifikation & Vergleich

## 1. Konzept

Ein SolidWorks-FeatureManager-artiger **Assembly Outliner** für Rhino 8, der Block-Hierarchien, Verschachtelungen und Komponentenstatus in einer persistenten, dockbaren Baumstruktur darstellt.

---

## 2. Vergleich: SolidWorks FeatureManager vs. Rhino Block Manager

### 2.1 SolidWorks FeatureManager – Was er kann

Der FeatureManager ist das zentrale Navigations- und Verwaltungswerkzeug in SolidWorks. Er ist **immer sichtbar** als dockbares Panel auf der linken Seite.

**Baumstruktur (Assembly Tree)**

Der FeatureManager zeigt eine vollständige, hierarchische Ansicht der Baugruppe:

```
📄 Küche_Montage (Baugruppe)
├─ 🔧 Verknüpfungen
│   ├─ Konzentrisch1 (Scharnier ↔ Seitenwand)
│   └─ Deckungsgleich1 (Boden ↔ Korpus)
├─ 📦 Oberschrank_600 (f) ×3
│   ├─ 📦 Scharnier_Blum_110 ×2
│   │   ├─ ⬡ Topf
│   │   └─ ⬡ Arm
│   ├─ ⬡ Seitenwand_Links
│   ├─ ⬡ Seitenwand_Rechts
│   ├─ ⬡ Boden
│   └─ ⬡ Rückwand_HDF
├─ 📦 Unterschrank_600 (-) ×4
│   └─ ...
└─ 📦 Arbeitsplatte_L
```

### 2.2 Rhino 8 Block Manager – Aktueller Stand

Der Block Manager zeigt eine **flache Liste von Block-Definitionen** – nicht die tatsächliche Instanz-Hierarchie im Dokument.

**Was er NICHT kann (= Lücken)**

| Feature | SolidWorks | Rhino Block Manager |
|---------|-----------|-------------------|
| Hierarchischer Instanz-Baum | ✅ Vollständig | ❌ Nur flache Definitionsliste |
| Verschachtelungskontext | ✅ Zeigt Parent → Child | ❌ Keine Verschachtelungsansicht |
| Bidirektionale Selektion | ✅ Klick ↔ Viewport | ⚠️ Nur Definition → Instanzen |
| BOM-Export aus Baum | ✅ Nativ | ❌ Nicht vorhanden |

---

## 3. Was unser Assembly Outliner leisten soll

### 3.1 Kern-Scope (MVP)

Wir bauen den **fehlenden hierarchischen Instanz-Baum** für Rhino.

**Scope-Abgrenzung**: Wir implementieren NICHT das SolidWorks Constraint-System.

### 3.2 Feature-Übersicht

**Navigation & Visualisierung**
- Hierarchischer Baum aller Block-Instanzen (rekursiv verschachtelt)
- Instanz-Anzeige (nicht nur Definitionen)
- Instanz-Count pro Definition
- Layer-Zuordnung pro Instanz
- Link-Typ Anzeige (Embedded, Linked, EmbeddedAndLinked)
- Suchfilter im Baum

**Interaktion**
- Bidirektionale Selektion: Baum ↔ Viewport
- Sichtbarkeits-Toggle pro Eintrag (Auge-Icon)
- Kontextmenü: Selektieren, Isolieren, Ausblenden, Block Editieren, Zoom
- "Alle gleichen selektieren" (alle Instanzen einer Definition)

---

## 4. Architektur

### 4.1 User Interface Architecture

- **Framework:** Eto.Forms (cross-platform UI framework integrated in Rhino 8)
- **Panel Type:** Dockable Rhino Panel using `Rhino.UI.Panel` base class

### 4.2 Plugin-Struktur (C# / RhinoCommon)

```
RhinoAssemblyOutliner/
├── RhinoAssemblyOutliner.sln
├── RhinoAssemblyOutliner/
│   ├── RhinoAssemblyOutlinerPlugin.cs
│   ├── Commands/
│   │   ├── OpenOutlinerCommand.cs
│   │   └── RefreshOutlinerCommand.cs
│   ├── UI/
│   │   ├── AssemblyOutlinerPanel.cs
│   │   ├── AssemblyTreeView.cs
│   │   ├── DetailPanel.cs
│   │   └── SearchFilterBar.cs
│   ├── Model/
│   │   ├── AssemblyTreeBuilder.cs
│   │   ├── AssemblyNode.cs
│   │   ├── BlockInstanceNode.cs
│   │   ├── GeometryNode.cs
│   │   └── DocumentNode.cs
│   ├── Services/
│   │   ├── SelectionSyncService.cs
│   │   ├── VisibilityService.cs
│   │   ├── DocumentEventService.cs
│   │   └── BlockInfoService.cs
│   └── Resources/
│       └── Icons/
```

---

## 5. Design-Entscheidungen

### Geklärt

1. **Top-Level Darstellung:** ✅ Auch lose Geometrie anzeigen
2. **Gruppierung / Instanz-Logik:** ✅ Gruppierung nach Objekt mit Instanz-Nummerierung
3. **Naming:** ✅ `Definition-Name #n`

### Offen

4. **Performance-Schwelle:** 🔲 Wird während Implementierung ermittelt
5. **Block Edit Integration:** 🔲 Anbindung wird während Implementierung entschieden

---

## 6. Abgrenzung: Was wir NICHT bauen

- **Kein Constraint/Mate-System**
- **Kein Ersatz für Block Edit New**
- **Kein Ersatz für den Layer Manager**
- **Keine Grasshopper-Runtime-Integration**
