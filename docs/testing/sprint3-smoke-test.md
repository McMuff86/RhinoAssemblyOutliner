# Sprint 3 Smoke Test — Definition Cloning live in Rhino

**Branch:** `feature/cpp-assembly-object`
**Commit:** `82143d7` (Phase A)
**Ziel:** Erstmals den `VariantManager`-Pfad in echtem Rhino 8 ausführen — vor Sprint 3 lief er nie wegen Build-Errors.

Der Test ist ein Smoke Test, kein Vollabdeckungstest. Wir wollen zwei Fragen beantworten:

1. **Bricht etwas?** Crashes, Exceptions, eingefrorenes Rhino, kaputter Block.
2. **Stimmt das mentale Modell?** Variant-Definitionen entstehen, werden geteilt, werden geräumt — und Undo bringt alles zurück.

Wenn beides ✅ ist, ist Sprint 3 echt fertig und Sprint 4 (ON_UserData) kann starten.

---

## 0. Vorbereitung (5 min)

```bash
# In Repo-Root, Bash:
dotnet build
```

Erwartung: 0 Errors. (Warnings sind ok.)

Plugin in Rhino 8 laden:

1. Rhino 8 starten, neues leeres Dokument.
2. Befehl `_-PlugInManager` → Install → wähle:
   `src\RhinoAssemblyOutliner\bin\Debug\net7.0-windows\RhinoAssemblyOutliner.rhp`
3. Falls schon installiert: Plugin auf "Enabled" setzen, oder Rhino neustarten.
4. Befehl `AssemblyOutliner` → Panel sollte dockbar erscheinen.

**Falls das Panel nicht öffnet** → Stop. Lies die Rhino-Command-History; melde mir den Fehler.

---

## 1. Testblock anlegen (5 min)

Ein einfaches Test-Assembly mit ≥3 Komponenten und ≥2 Instanzen ist nötig.

1. Zeichne im aktiven Layer:
   - eine Box (`_Box`) — wird zu *Gehäuse*
   - einen Zylinder (`_Cylinder`) — wird zu *Welle*
   - eine Kugel (`_Sphere`) — wird zu *Lager*
2. Selektiere alle drei → `_-Block` → Name `Motor_v1`, Basispunkt = WCS-Origin, "Convert" wählen (so wird die Geometrie zur Definition).
3. Befehl `_Insert` → `Motor_v1` → an drei Punkten platzieren. Du hast jetzt **3 Instanzen** derselben Definition.
4. Im Outliner-Panel: `Refresh` (oder Panel schliessen/öffnen). Du solltest 3 `Motor_v1`-Knoten sehen, jeder mit 3 Children (Box/Cylinder/Sphere).

**Wenn der Tree leer bleibt oder Children fehlen** → Stop. Screenshot + Rhino-Command-History an mich.

---

## 2. Smoke Test — Per-Instance Visibility (10 min)

Das ist der eigentliche Sprint-3-Pfad: `ComponentNode.IsVisible` togglen → `VariantManager.ReassignInstance` → neue Variant-Definition.

### 2.1 Eine Komponente auf einer Instanz ausblenden

1. Klicke das 👁-Icon neben `Sphere` *unter Instanz #1*.
2. **Erwartet:**
   - Sphere verschwindet im Viewport — **nur an Instanz #1**, nicht an #2 oder #3
   - Im Block Manager (`_BlockManager`) erscheint eine neue Definition mit Namen `__aov_Motor_v1_<8hex>`
   - Outliner: 👁 wird zu ◯ bei Sphere unter Instanz #1; das übergeordnete Motor_v1 zeigt ◐ (mixed)

**Wenn die Sphere an *allen* Instanzen verschwindet** → Variant-Definition wurde nicht erstellt, alle hängen noch an der Source. Bug, Stop.

**Wenn Rhino crasht oder einfriert** → Stop. Cmd History + ggf. Crash-Dump.

### 2.2 Deduplizierung (gleicher State teilt Variante)

3. Blende auf Instanz #2 ebenfalls die Sphere aus (gleiche Komponente).
4. **Erwartet:** Block Manager zeigt **immer noch nur eine** `__aov_…`-Definition. Beide Instanzen zeigen auf dieselbe Variante.

**Wenn zwei `__aov_…` Definitionen mit gleichem Hash entstehen** → Cache funktioniert nicht; Bug.

### 2.3 Verschiedene States = verschiedene Varianten

5. Auf Instanz #3 die *Box* ausblenden (nicht die Sphere).
6. **Erwartet:** Block Manager zeigt jetzt **zwei** `__aov_…`-Definitionen mit verschiedenen Hashes.

### 2.4 Zurück zur Quelle

7. Auf Instanz #1 die Sphere wieder einblenden.
8. **Erwartet:** Instanz #1 zeigt wieder auf die Original-Definition `Motor_v1` (Block Manager: rechtsklick → "Find Instances" prüft das).
9. Nach ca. 5 Sekunden (GC-Debounce) sollte die nicht mehr referenzierte `__aov_…`-Definition aus dem Block Manager verschwinden.

**Wenn die Definition nach 10s noch da ist** → GC läuft nicht. Notiere; nicht kritisch, aber Sprint-3-Bug.

---

## 3. Undo / Redo (5 min)

Der `UndoHelper` umschliesst jede Visibility-Änderung in einem Undo-Record.

1. Mit allen Instanzen sichtbar starten (Klick `Show All` im Panel).
2. Sphere auf Instanz #1 ausblenden.
3. Box auf Instanz #2 ausblenden.
4. Welle auf Instanz #3 ausblenden.
5. **3× Ctrl+Z** drücken.
   - **Erwartet:** Jeder Undo macht *eine* Änderung rückgängig, in umgekehrter Reihenfolge. Nach dem 3. Undo sind alle Komponenten wieder sichtbar.
6. **3× Ctrl+Y** (Redo).
   - **Erwartet:** Die drei Hides werden wieder hergestellt.

**Wenn ein Undo mehrere Schritte rückgängig macht** → Undo-Records sind nicht atomar; Bug.
**Wenn ein Undo Rhino in inkonsistenten Zustand bringt (Variant-Def zeigt auf nichts mehr)** → Notiere genau den Schritt; das ist Sprint-3-Stress-Test-Material.

---

## 4. Save / Load — der bewusste Failure-Test (5 min)

**Wichtig: Hier *muss* etwas brechen.** Das ist Sprint 4 (ON_UserData), und genau deshalb steht Sprint 4 als nächstes auf dem Plan.

1. Stelle sicher: Instanz #1 hat Sphere ausgeblendet (Variant aktiv).
2. `_Save` als `smoke-test.3dm`.
3. Rhino schliessen, neu starten, `smoke-test.3dm` öffnen.
4. **Was du sehen wirst (erwartetes Verhalten heute):**
   - Die Variant-Definition `__aov_Motor_v1_…` ist im Block Manager noch da
   - Instanz #1 zeigt korrekt ohne Sphere (weil sie auf der Variant hängt)
   - **ABER:** Im Outliner zeigt der `ComponentNode` für Sphere wieder 👁 (sichtbar) statt ◯
   - Klick auf den Eye-Toggle könnte das verfälschen oder einen Crash auslösen, weil der `VariantManager`-Cache leer ist

Das ist der erwartete Pre-Sprint-4-Zustand. Notiere: *was genau* schiefläuft (verlorener UI-State, doppelte Variants, Crash beim Toggle nach Reload, …). Diese Notes fliessen direkt in das Sprint-4-Design.

---

## 5. Berichten

Nach dem Test bitte zurückmelden mit:

```
Smoke Test Sprint 3 — Ergebnisse
================================

0. Build/Plugin-Load:    [OK | FAIL: ...]
1. Test-Block angelegt:  [OK | FAIL: ...]
2.1 Single Hide:         [OK | FAIL: ...]
2.2 Dedup:               [OK | FAIL: ...]
2.3 Verschiedene States: [OK | FAIL: ...]
2.4 Show wieder:         [OK | FAIL: ...]
2.4 GC nach 5s:          [OK | FAIL: ...]
3.  Undo/Redo:           [OK | FAIL: ...]
4.  Save/Load:           [erwartete Anomalien beschrieben]

Crashes:                 [keine | Schritt X.Y mit Stack/Cmd-History]
Sonstiges Auffälliges:   [...]
```

Wenn alles bis Schritt 3 grün ist und Schritt 4 nur die *erwarteten* Anomalien zeigt, ist Sprint 3 wirklich fertig und wir können Sprint 4 starten.

---

## Bekannte offene Punkte (du musst sie nicht testen, nur kennen)

- **Nested Blocks:** Blöcke-in-Blöcken laufen heute durch den Legacy-C++-Conduit, nicht durch `VariantManager`. Definition-Cloning für nested Blocks kommt in Sprint 4+. Dein Smoke Test hat nur eine Ebene Tiefe — perfekt.
- **Mixed-State-Icon (◐):** Die UI zeigt es korrekt für die *direkten* Children, aber bei tief verschachtelten Strukturen kann es flackern. Nicht-blocker.
- **Variant-Definitionen im Block Manager sichtbar:** Sie tragen `__aov_`-Prefix. User soll sie nicht editieren. Wir filtern sie aktuell aus dem Outliner heraus, aber der Block Manager ist Rhino-eigen und zeigt sie. Cosmetic.
