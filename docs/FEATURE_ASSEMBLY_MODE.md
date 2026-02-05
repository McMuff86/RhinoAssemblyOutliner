# Feature: Assembly Mode

**Status:** Konzept  
**Datum:** 2025-02-05  
**Autor:** Feature Design (Subagent)

---

## 1. Übersicht

### Problem
Aktuell zeigt der Assembly Outliner **alle** Blöcke im Dokument an. Bei komplexen Dokumenten mit mehreren unabhängigen Baugruppen (z.B. mehrere Möbelstücke) wird die Ansicht unübersichtlich.

### Lösung
Zwei Modi implementieren:

| Modus | Beschreibung | Use Case |
|-------|--------------|----------|
| **Document Mode** | Zeigt alle Top-Level Blöcke (aktuelles Verhalten) | Gesamtübersicht, kleine Dokumente |
| **Assembly Mode** | Zeigt nur einen ausgewählten "Root Block" und dessen Kinder | Fokussiertes Arbeiten an einer Baugruppe |

---

## 2. UX-Konzept

### 2.1 Modus-Umschaltung

**Empfehlung: Toggle-Button in Toolbar mit Dropdown**

```
┌─────────────────────────────────────────────────┐
│ ↻  ⊞  ⊟  │  📄 Document ▾  │  [Filter blocks...]│
└─────────────────────────────────────────────────┘
```

**Interaktion:**
- Klick auf Button → Dropdown öffnet sich
- Dropdown zeigt:
  - `📄 Document Mode` (alle Blöcke)
  - `📦 Assembly Mode` → Submenu mit verfügbaren Root-Blöcken
  - `─────────────────`
  - `📦 Recent Assemblies` (letzte 3-5 verwendete)

**Alternativen (nicht empfohlen):**
- ❌ Tabs: Verbraucht vertikalen Platz, weniger flexibel
- ❌ Separater Dropdown: Erzeugt zwei UI-Elemente statt einem

### 2.2 Assembly Root auswählen

**Primär: Rechtsklick-Kontextmenü im TreeView**

```
Right-click on "Cabinet_600 #1"
┌──────────────────────────────┐
│ ✓ Select in Viewport         │
│   Zoom to Block              │
│   ─────────────────────────  │
│ 📌 Set as Assembly Root      │ ← NEU
│ 📌 Open in Assembly Mode     │ ← NEU (wechselt Modus + setzt Root)
│   ─────────────────────────  │
│   Edit Block                 │
│   Select All Same            │
└──────────────────────────────┘
```

**Sekundär: Viewport-Selection**
- Block im Viewport selektieren
- Toolbar-Dropdown → "Use Selection as Root"

**Tertiär: Dropdown-Liste**
- Im Modus-Dropdown alle Top-Level Blöcke anzeigen
- Gruppiert nach Block-Definition-Name

### 2.3 Visual Feedback

**Im Assembly Mode:**
- Header zeigt aktuellen Root: `📦 Assembly: Cabinet_600 #1`
- Breadcrumb-Leiste (optional): `Document > Cabinet_600 #1`
- Subtiler Farbakzent (z.B. linker Rand des Panels)

**Zustandsanzeige im TreeView:**
- Assembly Root hat spezielles Icon: `📌` oder `🏠`
- Im Document Mode: Markierte "Assembly Roots" mit kleinem Indikator

---

## 3. Persistenz

### 3.1 Session-basiert (empfohlen für MVP)

**Keine Persistenz im Dokument:**
- Aktueller Modus + Root nur während der Session
- Beim Neuladen: Standard = Document Mode

**Vorteile:**
- Einfach zu implementieren
- Keine Dokument-Modifikation
- Kein "stale state" bei gelöschten Blöcken

### 3.2 Dokument-basiert (optional, Phase 2)

**UserText auf Block-Instanz:**
```
Key: "AssemblyOutliner::IsAssemblyRoot"
Value: "true"
```

**Document UserStrings:**
```
Key: "AssemblyOutliner::DefaultAssemblyRoot"
Value: "<GUID der Instanz>"
```

**Vorteile:**
- Persistiert über Sessions
- Kann zwischen Team-Mitgliedern geteilt werden
- "Assembly"-Konzept wird Teil des Dokuments

**Nachteile:**
- Komplexität (Cleanup bei gelöschten Blöcken)
- Ändert das Dokument (dirty flag)

### 3.3 Empfehlung

**Phase 1 (MVP):** Session-basiert, keine Persistenz  
**Phase 2:** Optional: "Pin as Assembly Root" mit UserText-Persistenz

---

## 4. Implementierung

### 4.1 Model-Erweiterungen

```csharp
// Neues Enum für den Anzeigemodus
public enum OutlinerViewMode
{
    Document,   // Alle Top-Level Blöcke
    Assembly    // Nur ein Root-Block + Kinder
}

// Erweiterung des TreeBuilders
public class AssemblyTreeBuilder
{
    public OutlinerViewMode ViewMode { get; set; } = OutlinerViewMode.Document;
    public Guid? AssemblyRootId { get; set; }
    
    public DocumentNode BuildTree()
    {
        if (ViewMode == OutlinerViewMode.Assembly && AssemblyRootId.HasValue)
        {
            return BuildAssemblyTree(AssemblyRootId.Value);
        }
        return BuildDocumentTree();
    }
    
    private DocumentNode BuildAssemblyTree(Guid rootId)
    {
        // 1. Finde die Block-Instanz
        // 2. Erstelle einen "virtuellen" DocumentNode
        // 3. Füge nur diesen Block + Kinder hinzu
    }
}
```

### 4.2 UI-Erweiterungen

```csharp
// AssemblyOutlinerPanel.cs
private OutlinerViewMode _viewMode = OutlinerViewMode.Document;
private Guid? _assemblyRootId;

private Control BuildModeDropdown()
{
    var dropdown = new DropDown();
    dropdown.Items.Add("📄 Document Mode");
    dropdown.Items.Add("📦 Assembly Mode...");
    dropdown.SelectedIndexChanged += OnModeChanged;
    return dropdown;
}

// Kontextmenü-Erweiterung
private void AddContextMenuItems(ContextMenu menu, AssemblyNode node)
{
    if (node is BlockInstanceNode blockNode)
    {
        menu.Items.Add(new ButtonMenuItem
        {
            Text = "📌 Set as Assembly Root",
            Command = new Command((s, e) => SetAssemblyRoot(blockNode))
        });
    }
}
```

### 4.3 State Management

```csharp
// Neuer Service oder Teil des Panels
public class OutlinerStateService
{
    public event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;
    
    public OutlinerViewMode ViewMode { get; private set; }
    public BlockInstanceNode? AssemblyRoot { get; private set; }
    public Stack<BlockInstanceNode> RecentAssemblies { get; } = new(5);
    
    public void SetDocumentMode()
    {
        ViewMode = OutlinerViewMode.Document;
        AssemblyRoot = null;
        ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs(ViewMode));
    }
    
    public void SetAssemblyMode(BlockInstanceNode root)
    {
        ViewMode = OutlinerViewMode.Assembly;
        AssemblyRoot = root;
        RecentAssemblies.Push(root);
        ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs(ViewMode, root));
    }
}
```

---

## 5. Edge Cases

### 5.1 Assembly Root wird gelöscht
- Automatisch zurück zu Document Mode
- Notification: "Assembly root was deleted. Switched to Document Mode."

### 5.2 Assembly Root wird verschachtelt (in anderen Block)
- Assembly Mode bleibt aktiv
- Root ist nun "orphaned" – Warnung anzeigen?

### 5.3 Dokument wechseln
- State pro Dokument speichern (`Dictionary<uint, OutlinerState>`)
- Oder: Immer zu Document Mode zurück

### 5.4 Mehrere Instanzen desselben Blocks
- User wählt **Instanz**, nicht Definition
- Instanzen mit gleichem Namen unterscheidbar machen: `Cabinet_600 #1`, `Cabinet_600 #2`

---

## 6. Mockups

### Document Mode (aktuell)
```
┌─ Assembly Outliner ──────────────────┐
│ ↻ ⊞ ⊟  │ 📄 Document ▾ │ [Filter...] │
├──────────────────────────────────────┤
│ 📄 Kitchen_Project.3dm               │
│ ├─ 📦 UpperCabinet_600 #1           │
│ │   ├─ 📦 Hinge_Blum #1             │
│ │   └─ 📦 Hinge_Blum #2             │
│ ├─ 📦 UpperCabinet_600 #2           │
│ ├─ 📦 LowerCabinet_900 #1           │
│ │   ├─ 📦 Drawer_800 #1             │
│ │   └─ 📦 Drawer_800 #2             │
│ └─ 📦 Countertop #1                 │
└──────────────────────────────────────┘
```

### Assembly Mode
```
┌─ Assembly Outliner ──────────────────┐
│ ↻ ⊞ ⊟  │ 📦 LowerCabinet_900 ▾│[...]│
├──────────────────────────────────────┤
│ 📌 LowerCabinet_900 #1  [← Document] │
│ ├─ 📦 Drawer_800 #1                 │
│ │   ├─ 📦 Handle_Chrome #1          │
│ │   └─ 📦 Slider_Rail #1            │
│ └─ 📦 Drawer_800 #2                 │
│     ├─ 📦 Handle_Chrome #2          │
│     └─ 📦 Slider_Rail #2            │
│                                      │
│ [← Back to Document Mode]            │
└──────────────────────────────────────┘
```

---

## 7. Roadmap

### Phase 1 (MVP)
- [ ] Mode-Toggle in Toolbar (Document / Assembly)
- [ ] Rechtsklick → "Open in Assembly Mode"
- [ ] Zurück-Button zu Document Mode
- [ ] Session-basierter State

### Phase 2
- [ ] Recent Assemblies History
- [ ] Keyboard Shortcuts (z.B. `Esc` = Back to Document)
- [ ] Breadcrumb-Navigation

### Phase 3
- [ ] UserText-Persistenz für markierte Roots
- [ ] "Favorite Assemblies" die persistieren
- [ ] Multi-Select: Mehrere Roots gleichzeitig anzeigen?

---

## 8. Offene Fragen

1. **Sollen "leere" Blöcke (ohne Kinder) als Assembly Root wählbar sein?**
   - Technisch ja, aber wenig sinnvoll
   - Empfehlung: Erlauben, aber keine spezielle Behandlung

2. **Was passiert bei Linked Blocks (externe Referenzen)?**
   - Sollten normal funktionieren
   - Testen ob `InstanceDefinition.GetObjects()` bei Linked Blocks funktioniert

3. **Soll der Filter im Assembly Mode nur innerhalb des Subtrees suchen?**
   - Empfehlung: Ja, konsistent mit dem fokussierten Modus

---

## 9. Referenzen

- SolidWorks FeatureManager: Unterstützt "Isolate" aber keinen expliziten Assembly Mode
- Fusion 360: Browser zeigt immer die aktive Komponente im Kontext
- Inventor: Assembly-zentriert, separates Part-Editing

**Unser Ansatz** ist näher an Fusion 360: Kontextuelles Arbeiten, schneller Wechsel zwischen Übersicht und Fokus.
