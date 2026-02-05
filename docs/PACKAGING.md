# Packaging & Distribution

## Rhino Package Manager (Yak)

Das Plugin wird über den **Yak Package Manager** verteilt, der in Rhino 8 integriert ist.

### Voraussetzungen

- Rhino 8 installiert (enthält Yak CLI)
- Rhino Account (für das Pushen)

### Yak CLI Pfade

```bash
# Windows
"C:\Program Files\Rhino 8\System\Yak.exe"

# Mac
"/Applications/Rhino 8.app/Contents/Resources/bin/yak"
```

---

## Build & Package Workflow

### 1. Plugin bauen

```bash
# Im Projekt-Root
dotnet build -c Release
```

Output: `src/RhinoAssemblyOutliner/bin/Release/net7.0/RhinoAssemblyOutliner.rhp`

### 2. Dist-Ordner vorbereiten

```bash
mkdir -p dist
cp src/RhinoAssemblyOutliner/bin/Release/net7.0/RhinoAssemblyOutliner.rhp dist/
cp src/RhinoAssemblyOutliner/bin/Release/net7.0/RhinoAssemblyOutliner.dll dist/
cp manifest.yml dist/
cp icon.png dist/  # 256x256 PNG
cp LICENSE dist/
cp README.md dist/
```

### 3. Package erstellen

```powershell
# Windows
cd dist
& "C:\Program Files\Rhino 8\System\Yak.exe" build

# Oder für spezifische Plattform
& "C:\Program Files\Rhino 8\System\Yak.exe" build --platform win
```

Output: `assemblyoutliner-0.1.0-rh8_0-win.yak`

### 4. Test-Server (optional)

Vor dem Release auf dem Testserver testen (wird täglich geleert):

```powershell
& "C:\Program Files\Rhino 8\System\Yak.exe" push --source https://test.yak.rhino3d.com assemblyoutliner-0.1.0-rh8_0-win.yak

# Suchen
& "C:\Program Files\Rhino 8\System\Yak.exe" search --source https://test.yak.rhino3d.com --all assemblyoutliner
```

### 5. Login (einmalig)

```powershell
& "C:\Program Files\Rhino 8\System\Yak.exe" login
```

Öffnet Browser für Rhino Account OAuth.

### 6. Auf Production pushen

```powershell
& "C:\Program Files\Rhino 8\System\Yak.exe" push assemblyoutliner-0.1.0-rh8_0-win.yak
```

### 7. Verifizieren

```powershell
& "C:\Program Files\Rhino 8\System\Yak.exe" search --all assemblyoutliner
```

In Rhino: `PackageManager` Command → Suche nach "assemblyoutliner"

---

## manifest.yml Referenz

```yaml
---
name: assemblyoutliner          # Kleinbuchstaben, keine Leerzeichen
version: 0.1.0                   # SemVer
authors:
- Adrian Muff
description: >                   # Mehrzeilig mit >
  Beschreibung des Plugins...
url: https://github.com/...      # Homepage/Repo
icon: icon.png                   # 256x256 PNG
keywords:
- keyword1
- keyword2
```

---

## Versionierung

- **SemVer** verwenden: `MAJOR.MINOR.PATCH`
- Einmal gepusht = unveränderlich
- Neue Version = neuer Push

### Pre-Release Versionen

```yaml
version: 0.2.0-beta.1
```

---

## Distribution Tags

Der Dateiname enthält einen Distribution Tag: `assemblyoutliner-0.1.0-rh8_0-win.yak`

- `rh8_0` - Rhino 8 (automatisch aus RhinoCommon-Referenz)
- `win` / `mac` / `any` - Plattform

Für Cross-Platform: Zwei Packages bauen und beide pushen.

---

## Troubleshooting

### Package existiert bereits
→ Version erhöhen, altes Package kann nicht überschrieben werden

### Nicht Owner
→ Nur der Ersteller kann neue Versionen pushen. Weitere Owner mit `yak owner add` hinzufügen.

### Manifest ungültig
→ YAML Syntax mit [yamllint.com](http://www.yamllint.com) prüfen

### Package zurückziehen
```powershell
& "C:\Program Files\Rhino 8\System\Yak.exe" yank assemblyoutliner 0.1.0
```

---

## Checkliste vor Release

- [ ] Version in `manifest.yml` erhöht
- [ ] Version in `.csproj` erhöht
- [ ] CHANGELOG aktualisiert
- [ ] README aktuell
- [ ] Build ohne Errors
- [ ] Lokal getestet
- [ ] Auf Test-Server getestet
- [ ] Icon vorhanden (256x256 PNG)
