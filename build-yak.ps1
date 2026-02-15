# build-yak.ps1 — Build + Package for Rhino Yak
# Usage:
#   .\build-yak.ps1              # Build Release + create .yak package
#   .\build-yak.ps1 -Debug       # Build Debug + create .yak package  
#   .\build-yak.ps1 -Push        # Build + package + push to Yak server
#   .\build-yak.ps1 -Clean       # Clean dist folder first

param(
    [switch]$Debug,
    [switch]$Push,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$config = if ($Debug) { "Debug" } else { "Release" }
$project = "src\RhinoAssemblyOutliner\RhinoAssemblyOutliner.csproj"
$distDir = "dist"
$buildOutput = "src\RhinoAssemblyOutliner\bin\$config\net7.0-windows"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  RhinoAssemblyOutliner — Yak Build" -ForegroundColor Cyan
Write-Host "  Config: $config" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- Clean ---
if ($Clean -and (Test-Path $distDir)) {
    Write-Host "[1/4] Cleaning dist..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $distDir
}

# --- Build ---
Write-Host "[2/4] Building $config..." -ForegroundColor Yellow
dotnet build $project -c $config
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build succeeded" -ForegroundColor Green

# --- Package ---
Write-Host "[3/4] Creating Yak package..." -ForegroundColor Yellow

if (!(Test-Path $distDir)) { New-Item -ItemType Directory $distDir | Out-Null }

# Copy plugin files
Copy-Item "$buildOutput\RhinoAssemblyOutliner.rhp" $distDir\ -Force
# Copy dependency DLLs (exclude Rhino/Eto runtime — already in Rhino)
Get-ChildItem "$buildOutput\*.dll" | Where-Object {
    $_.Name -notmatch "^(RhinoCommon|Eto\.|Rhino\.)"
} | ForEach-Object {
    Copy-Item $_.FullName $distDir\ -Force
}

# Copy manifest
Copy-Item manifest.yml $distDir\ -Force

# Copy icon if exists
if (Test-Path "icon.png") {
    Copy-Item "icon.png" $distDir\ -Force
}

# Run yak build
Push-Location $distDir
try {
    $yakExe = Get-Command yak -ErrorAction SilentlyContinue
    if (-not $yakExe) {
        # Try Rhino's bundled yak
        $rhinoYak = "C:\Program Files\Rhino 8\System\Yak.exe"
        if (Test-Path $rhinoYak) {
            $yakExe = $rhinoYak
        } else {
            Write-Host "❌ yak not found! Install via: rhino -runscript '_PackageManager'" -ForegroundColor Red
            exit 1
        }
    }
    
    & $yakExe build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Yak build failed!" -ForegroundColor Red
        exit 1
    }
    
    $yakFile = Get-ChildItem "*.yak" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Host "✅ Package created: $($yakFile.Name) ($([math]::Round($yakFile.Length / 1KB, 1)) KB)" -ForegroundColor Green
    
    # --- Push ---
    if ($Push) {
        Write-Host "`n[4/4] Pushing to Yak server..." -ForegroundColor Yellow
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
