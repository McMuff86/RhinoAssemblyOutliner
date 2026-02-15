# build-native.ps1 — Build C++ native DLL with VS Build Tools
param(
    [switch]$Debug,
    [switch]$Clean
)

$config = if ($Debug) { "Debug" } else { "Release" }
$project = "src\RhinoAssemblyOutliner.native\RhinoAssemblyOutliner.native.vcxproj"

# Find MSBuild
$msbuildPaths = @(
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
)

$msbuild = $msbuildPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $msbuild) {
    Write-Host "❌ MSBuild not found! Install Visual Studio Build Tools 2022." -ForegroundColor Red
    Write-Host "   https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022" -ForegroundColor Yellow
    exit 1
}

Write-Host "Using: $msbuild" -ForegroundColor Gray

$target = if ($Clean) { "/t:Clean;Build" } else { "/t:Build" }

Write-Host "`nBuilding C++ native ($config | x64)..." -ForegroundColor Cyan
& $msbuild $project /p:Configuration=$config /p:Platform=x64 $target /v:minimal

if ($LASTEXITCODE -eq 0) {
    $dllPath = "src\RhinoAssemblyOutliner.native\x64\$config\RhinoAssemblyOutliner.Native.dll"
    if (Test-Path $dllPath) {
        $size = [math]::Round((Get-Item $dllPath).Length / 1KB, 1)
        Write-Host "✅ Built: $dllPath ($size KB)" -ForegroundColor Green
        
        # Auto-copy next to .rhp if it exists
        $rhpDir = "src\RhinoAssemblyOutliner\bin\$config\net7.0-windows"
        if (Test-Path $rhpDir) {
            Copy-Item $dllPath $rhpDir\ -Force
            Write-Host "📋 Copied to $rhpDir" -ForegroundColor Green
        }
    }
} else {
    Write-Host "❌ Build failed!" -ForegroundColor Red
}
