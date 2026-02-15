# build-yak.ps1 — Build + Package for Rhino Yak
# Usage:
#   .\build-yak.ps1                          # Build Release + create .yak package
#   .\build-yak.ps1 -Debug                   # Build Debug + create .yak package
#   .\build-yak.ps1 -Push                    # Build + package + push to Yak server
#   .\build-yak.ps1 -Clean                   # Clean dist folder first
#   .\build-yak.ps1 -Version "1.2.0"         # Override version in manifest

param(
    [switch]$Debug,
    [switch]$Push,
    [switch]$Clean,
    [string]$Version
)

$ErrorActionPreference = "Stop"
$config = if ($Debug) { "Debug" } else { "Release" }
$project = "src\RhinoAssemblyOutliner\RhinoAssemblyOutliner.csproj"
$distDir = "dist"
$buildOutput = "src\RhinoAssemblyOutliner\bin\$config\net7.0-windows"
$nativeDll = "src\RhinoAssemblyOutliner.native\x64\$config\RhinoAssemblyOutliner.Native.dll"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  RhinoAssemblyOutliner — Yak Build" -ForegroundColor Cyan
Write-Host "  Config: $config" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- Verify yak is available early ---
$yakExe = Get-Command yak -ErrorAction SilentlyContinue
if (-not $yakExe) {
    $rhinoYak = "C:\Program Files\Rhino 8\System\Yak.exe"
    if (Test-Path $rhinoYak) {
        $yakExe = $rhinoYak
    } else {
        Write-Host "❌ yak not found! Install via: rhino -runscript '_PackageManager'" -ForegroundColor Red
        Write-Host "   Or ensure Rhino 8 is installed at the default location." -ForegroundColor Yellow
        exit 1
    }
}
Write-Host "Using yak: $yakExe" -ForegroundColor Gray

# --- Clean ---
if ($Clean -and (Test-Path $distDir)) {
    Write-Host "[1/5] Cleaning dist..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $distDir
}

# --- Build ---
Write-Host "[2/5] Building $config..." -ForegroundColor Yellow
dotnet build $project -c $config
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build succeeded" -ForegroundColor Green

# --- Package ---
Write-Host "[3/5] Assembling package contents..." -ForegroundColor Yellow

if (!(Test-Path $distDir)) { New-Item -ItemType Directory $distDir | Out-Null }

# Copy plugin files
Copy-Item "$buildOutput\RhinoAssemblyOutliner.rhp" $distDir\ -Force

# Copy dependency DLLs (exclude Rhino/Eto runtime — already in Rhino)
Get-ChildItem "$buildOutput\*.dll" | Where-Object {
    $_.Name -notmatch "^(RhinoCommon|Eto\.|Rhino\.)"
} | ForEach-Object {
    Copy-Item $_.FullName $distDir\ -Force
}

# Copy native DLL (C++)
if (Test-Path $nativeDll) {
    Copy-Item $nativeDll $distDir\ -Force
    Write-Host "  ✓ Native DLL included" -ForegroundColor Gray
} elseif (Test-Path "$buildOutput\RhinoAssemblyOutliner.Native.dll") {
    # Already copied by build-native.ps1
    Copy-Item "$buildOutput\RhinoAssemblyOutliner.Native.dll" $distDir\ -Force
    Write-Host "  ✓ Native DLL included (from build output)" -ForegroundColor Gray
} else {
    Write-Host "  ⚠ Native DLL not found — run build-native.ps1 first if needed" -ForegroundColor Yellow
}

# Copy manifest
Copy-Item manifest.yml $distDir\ -Force

# Override version if specified
if ($Version) {
    Write-Host "  ✓ Overriding version to $Version" -ForegroundColor Gray
    (Get-Content "$distDir\manifest.yml") -replace '^version:.*', "version: $Version" |
        Set-Content "$distDir\manifest.yml"
}

# Copy icon
$iconPath = "resources\plugin-icon.png"
if (Test-Path $iconPath) {
    Copy-Item $iconPath $distDir\ -Force
    Write-Host "  ✓ Icon included" -ForegroundColor Gray
} else {
    Write-Host "  ⚠ Icon not found at $iconPath" -ForegroundColor Yellow
}

# Copy README and CHANGELOG
foreach ($doc in @("README.md", "CHANGELOG.md")) {
    if (Test-Path $doc) {
        Copy-Item $doc $distDir\ -Force
        Write-Host "  ✓ $doc included" -ForegroundColor Gray
    }
}

# --- Yak Build ---
Write-Host "[4/5] Building .yak package..." -ForegroundColor Yellow
Push-Location $distDir
try {
    & $yakExe build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Yak build failed!" -ForegroundColor Red
        exit 1
    }

    $yakFile = Get-ChildItem "*.yak" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Host "✅ Package created: $($yakFile.Name) ($([math]::Round($yakFile.Length / 1KB, 1)) KB)" -ForegroundColor Green

    # --- Push ---
    if ($Push) {
        Write-Host "`n[5/5] Pushing to Yak server..." -ForegroundColor Yellow
        & $yakExe push $yakFile.Name
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Push failed! Make sure you're logged in: yak login" -ForegroundColor Red
            exit 1
        }
        Write-Host "✅ Published to Yak!" -ForegroundColor Green
    }
} finally {
    Pop-Location
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Done! Package in: .\$distDir\" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
