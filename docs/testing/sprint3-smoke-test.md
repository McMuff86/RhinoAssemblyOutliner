# Sprint 3/4 Rhino Smoke Test - Definition Cloning + Persistence Roundtrip

**Branch:** `main`  
**Ziel:** Den produktionskritischen Pfad in Rhino 8 validieren: Definition Cloning aus Sprint 3 plus `ON_AssemblyUserData` Save/Load-Roundtrip aus Sprint 4.

Der Test ist ein manueller Smoke- und Roundtrip-Test, kein Vollabdeckungstest. Er beantwortet drei Fragen:

1. **Läuft der VariantManager-Pfad live in Rhino?** Per-Instance Visibility erzeugt und teilt Variant-Definitionen korrekt.
2. **Persistiert Sprint 4 den State?** Nach Save, Rhino schließen und Reopen zeigen Viewport und Outliner denselben Hidden-State.
3. **Degradiert das File sauber?** Ohne Plugin bleibt die gespeicherte Variant-Geometrie sichtbar und das File bleibt öffnungsfähig.

Wenn dieser Test grün ist, ist die Save/Load-Persistence production-validiert genug für die nächste Sprint-Arbeit.

---

## 0. Build und Artefakte prüfen

In Repo-Root:

```powershell
dotnet build
```

Erwartung: 0 Errors. Warnings sind nur dann ein Stopper, wenn sie neue Persistence-, P/Invoke- oder Native-Copy-Probleme betreffen.

Native Release Build, falls MSBuild/VS 2022 verfügbar ist:

```powershell
msbuild src\RhinoAssemblyOutliner.native\RhinoAssemblyOutliner.native.vcxproj /p:Configuration=Release /p:Platform=x64
dotnet build
```

Der zweite `dotnet build` ist wichtig, weil das C#-Projekt die Native DLL neben das `.rhp` kopiert.

Prüfe danach:

```powershell
Test-Path src\RhinoAssemblyOutliner.native\x64\Release\RhinoAssemblyOutliner.Native.dll
Test-Path src\RhinoAssemblyOutliner\bin\Debug\net7.0-windows\RhinoAssemblyOutliner.rhp
Test-Path src\RhinoAssemblyOutliner\bin\Debug\net7.0-windows\RhinoAssemblyOutliner.Native.dll
```

Alle drei sollten `True` liefern. Wenn die Native DLL nicht neben dem `.rhp` liegt, stoppe hier: Sprint-4-Persistence kann in Rhino nicht laufen.

---

## 1. Plugin in Rhino 8 laden

1. Rhino 8 starten, neues leeres Dokument.
2. Befehl `_-PlugInManager` -> Install -> wähle:
   `src\RhinoAssemblyOutliner\bin\Debug\net7.0-windows\RhinoAssemblyOutliner.rhp`
3. Falls schon installiert: Plugin auf "Enabled" setzen oder Rhino neu starten.
4. In der Command History muss sinngemäß stehen:
   - `RhinoAssemblyOutliner plugin loaded.`
   - `AssemblyOutliner: Native module v5 loaded.`
5. Befehl `AssemblyOutliner` ausführen. Das Panel sollte dockbar erscheinen.

**Stopper:** Wenn die History `Native module not found` oder `Failed to load native module` meldet, erst den Build/Copy-Schritt reparieren. Ohne Native DLL ist der Roundtrip-Test nicht aussagekräftig.

---

## 2. Testblock mit drei Instanzen anlegen

Ein einfaches Test-Assembly mit mindestens drei Komponenten und drei Instanzen ist nötig.

1. Zeichne im aktiven Layer:
   - eine Box (`_Box`) als `Gehäuse`
   - einen Zylinder (`_Cylinder`) als `Welle`
   - eine Kugel (`_Sphere`) als `Lager`
2. Selektiere alle drei -> `_-Block` -> Name `Motor_v1`, Basispunkt = WCS-Origin, "Convert" wählen.
3. Befehl `_Insert` -> `Motor_v1` -> an drei unterschiedlichen Punkten platzieren. Du hast jetzt **3 Instanzen derselben Definition**.
4. Im Outliner-Panel `Refresh` klicken. Du solltest 3 `Motor_v1`-Knoten sehen, jeder mit 3 Children.

**Stopper:** Wenn der Tree leer bleibt oder Children fehlen, Screenshot und Rhino Command History sichern.

---

## 3. Sprint-3 Smoke: Per-Instance Visibility

### 3.1 Unterschiedliche Komponenten pro Instanz ausblenden

1. Instanz #1: `Lager`/Sphere ausblenden.
2. Instanz #2: `Gehäuse`/Box ausblenden.
3. Instanz #3: `Welle`/Cylinder ausblenden.

Erwartung:

- Im Viewport fehlt pro Instanz nur die jeweils ausgeblendete Komponente.
- Andere Instanzen derselben Source-Definition bleiben unverändert.
- Im Outliner ist pro Instanz genau das passende Child hidden.
- Die Elternknoten zeigen mixed state, solange nicht alle Children sichtbar sind.
- Im `_BlockManager` entstehen interne Variant-Definitionen mit Prefix `__aov_`.

**Stopper:** Wenn eine Komponente auf allen Instanzen verschwindet, wurde die Source-Definition statt einer Variant-Definition verändert.

### 3.2 Deduplizierung prüfen

1. Blende auf Instanz #1 wieder alles ein.
2. Blende auf Instanz #1 dieselbe Komponente aus wie auf Instanz #3, z.B. `Welle`.
3. Prüfe im `_BlockManager`, ob beide Instanzen denselben Hidden-State über dieselbe `__aov_...`-Definition teilen.

Erwartung: Gleicher State erzeugt keine doppelte Variant mit identischem Hash.

---

## 4. Undo / Redo

1. Mit drei unterschiedlichen Hidden-States starten.
2. 3x `Ctrl+Z` drücken.
3. 3x `Ctrl+Y` drücken.

Erwartung:

- Jeder Undo macht eine Visibility-Änderung rückgängig.
- Jeder Redo stellt genau eine Änderung wieder her.
- Rhino bleibt stabil, der Outliner lässt sich refreshen, und die Varianten zeigen weiter auf gültige Definitionen.

---

## 5. Sprint-4 Roundtrip: Save / Close / Reopen

Dieser Abschnitt ersetzt den alten Sprint-3-Failure-Test. Nach Sprint 4 muss Save/Load funktionieren.

### 5.1 Ausgangszustand setzen

Stelle vor dem Speichern einen eindeutig unterscheidbaren State her:

- Instanz #1: Sphere hidden
- Instanz #2: Box hidden
- Instanz #3: Cylinder hidden

Optional: Screenshot vom Viewport und Outliner machen.

### 5.2 Speichern und Rhino schließen

1. `_SaveAs` -> `sprint4-roundtrip.3dm`
2. Rhino komplett schließen, nicht nur das Dokument.

### 5.3 Neu öffnen und Restore prüfen

1. Rhino 8 neu starten.
2. `sprint4-roundtrip.3dm` öffnen.
3. Command History prüfen.

Erwartung:

- Die History meldet sinngemäß `AssemblyOutliner: Restored 3 persisted assembly instance(s).`
- Im Viewport fehlen dieselben drei Komponenten wie vor dem Save.
- Nach `AssemblyOutliner` und `Refresh` zeigt der Outliner dieselben Hidden-States:
  - Instanz #1: Sphere hidden
  - Instanz #2: Box hidden
  - Instanz #3: Cylinder hidden
- Weitere Toggles nach dem Reload funktionieren ohne Crash und ohne doppelte sinnlose Varianten.
- `_BlockManager` enthält die benötigten `__aov_...`-Definitionen und keine offensichtlichen Duplikate für identische States.

**Stopper:** Wenn der Viewport korrekt aussieht, der Outliner aber alle Children sichtbar zeigt, liest der TreeBuilder die persistierte `ON_AssemblyUserData` nicht. Das war der erwartete Pre-Sprint-4-Fehler und ist jetzt ein Regression-Bug.

---

## 6. Copy/Paste

### 6.1 Intra-Document Copy/Paste

1. In `sprint4-roundtrip.3dm` eine Instanz mit aktivem Hidden-State selektieren.
2. `Ctrl+C`, dann `Ctrl+V`, Kopie an anderer Stelle platzieren.
3. Outliner refreshen.

Erwartung:

- Die Kopie zeigt im Viewport denselben Hidden-State wie die kopierte Instanz.
- Der Outliner zeigt für die Kopie denselben Hidden-State.
- Ein anschließender Save -> Rhino schließen -> Reopen erhält auch den State der Kopie.

### 6.2 Cross-Document Copy/Paste

1. Instanz mit aktivem Hidden-State kopieren.
2. Neues Rhino-Dokument öffnen.
3. `Ctrl+V`, Kopie platzieren.
4. Outliner refreshen.
5. Speichern, Rhino schließen, neu öffnen.

Erwartung:

- Wenn Rhino die Source-Definition mit importiert oder eine gleichnamige Source-Definition im Ziel existiert, wird der State über Source-ID oder Source-Name wiederhergestellt.
- Wenn die Source-Definition nicht auflösbar ist, darf Rhino nicht crashen. Die Instanz darf als "frozen" Variant-Geometrie sichtbar bleiben; das Plugin darf die nicht auflösbare UserData entfernen und eine Warnung loggen.

---

## 7. Graceful Degradation ohne Plugin

Dieser Test validiert, dass `.3dm`-Dateien auch ohne Assembly-Outliner-Plugin öffnungsfähig bleiben.

1. `sprint4-roundtrip.3dm` mit gespeicherten Hidden-States schließen.
2. In Rhino `_-PlugInManager` öffnen und `RhinoAssemblyOutliner` deaktivieren, oder das `.rhp` temporär nicht laden.
3. Rhino neu starten und `sprint4-roundtrip.3dm` öffnen.

Erwartung ohne Plugin:

- Die Datei öffnet ohne Crash.
- Die sichtbare Geometrie bleibt im zuletzt gespeicherten Variant-Zustand erhalten.
- Es gibt keinen Outliner-Restore, weil das Plugin nicht geladen ist.
- Rhinos native UI kann interne `__aov_...`-Blockdefinitionen zeigen; das ist in diesem Zustand akzeptiert.

Optionaler Unknown-UserData-Save-Test:

1. Ohne geladenes Plugin `sprint4-roundtrip.3dm` als `sprint4-roundtrip-no-plugin-save.3dm` speichern.
2. Rhino schließen.
3. Plugin wieder aktivieren.
4. `sprint4-roundtrip-no-plugin-save.3dm` öffnen.

Erwartung: Rhino hat die unbekannte `ON_UserData` beim Save ohne Plugin erhalten, und der Restore funktioniert wieder. Falls das nicht zutrifft, ist das ein Graceful-Degradation-Bug, aber die Originaldatei bleibt durch den SaveAs-Schritt unangetastet.

Plugin wieder aktivieren:

1. Plugin wieder laden/aktivieren.
2. Wenn der optionale Save-Test übersprungen wurde: `sprint4-roundtrip.3dm` erneut öffnen. Wenn er ausgeführt wurde: `sprint4-roundtrip-no-plugin-save.3dm` geöffnet lassen oder erneut öffnen.
3. Outliner refreshen.

Erwartung:

- Persistierte `ON_UserData` wurde von Rhino erhalten.
- Plugin-Restore funktioniert wieder.
- Viewport und Outliner stimmen erneut überein.

---

## 8. Berichtsvorlage

```text
Sprint 3/4 Smoke + Roundtrip - Ergebnisse
=========================================

0. Managed Build:              [OK | FAIL: ...]
0. Native Release Build:       [OK | FAIL | SKIPPED: MSBuild/Rhino SDK fehlt]
0. Native DLL neben .rhp:      [OK | FAIL: ...]
1. Plugin Load + Native v5:    [OK | FAIL: ...]
2. 3 Instanzen angelegt:       [OK | FAIL: ...]
3.1 Per-Instance Hide:         [OK | FAIL: ...]
3.2 Variant Dedup:             [OK | FAIL: ...]
4. Undo/Redo:                  [OK | FAIL: ...]
5. Save/Close/Reopen Restore:  [OK | FAIL: ...]
5. Outliner State nach Reload: [OK | FAIL: ...]
6. Intra-doc Copy/Paste:       [OK | FAIL: ...]
6. Cross-doc Copy/Paste:       [OK | FAIL | SKIPPED: ...]
7. Ohne Plugin geöffnet:       [OK | FAIL | SKIPPED: ...]
7. Plugin wieder aktiviert:    [OK | FAIL | SKIPPED: ...]

Crashes:                       [keine | Schritt X mit Cmd History/Crash Dump]
Command-History Warnungen:     [...]
BlockManager Auffälligkeiten:  [...]
Sonstiges:                     [...]
```

---

## Bekannte Grenzen

- **Nested Blocks:** Dieser Smoke Test nutzt nur eine Block-Ebene. Definition Cloning für tiefer verschachtelte Visibility bleibt Sprint 8+.
- **Rhino Object Properties:** Rhinos natives Type-Feld kann interne Variant-Namen anzeigen. Der Outliner und das eigene DetailPanel sollen den Source-Namen verwenden.
- **Graceful degradation:** Ohne Plugin bleibt die Geometrie sichtbar wie gespeichert, aber sie ist nicht editierbar als Assembly-State. Das ist erwartetes Verhalten.
