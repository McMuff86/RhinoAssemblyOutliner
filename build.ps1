# Build script for RhinoAssemblyOutliner
param(
    [switch]$Release,
    [switch]$Pack
)

$config = if ($Release) { "Release" } else { "Debug" }
$project = "src/RhinoAssemblyOutliner/RhinoAssemblyOutliner.csproj"

Write-Host "Building $config..." -ForegroundColor Cyan
dotnet build $project -c $config

if ($Pack) {
    Write-Host "Creating Yak package..." -ForegroundColor Cyan
    # Copy output to package directory
    $outDir = "dist"
    if (!(Test-Path $outDir)) { New-Item -ItemType Directory $outDir }
    Copy-Item "src/RhinoAssemblyOutliner/bin/$config/net7.0-windows/*.rhp" $outDir/
    Copy-Item "src/RhinoAssemblyOutliner/bin/$config/net7.0-windows/*.dll" $outDir/ -ErrorAction SilentlyContinue
    Copy-Item manifest.yml $outDir/
    Push-Location $outDir
    & yak build
    Pop-Location
    Write-Host "Package created in $outDir" -ForegroundColor Green
}
