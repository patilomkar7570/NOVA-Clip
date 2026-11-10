# Build and test script for NOVA Clip
$ErrorActionPreference = "Stop"

$dotnetDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path "$dotnetDir\dotnet.exe") {
    $env:PATH = "$dotnetDir;$env:PATH"
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NOVA Clip 1.0 - Solution Build & Test     " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore NOVA.Clip.sln

Write-Host "Building solution in Release mode..." -ForegroundColor Yellow
dotnet build NOVA.Clip.sln -c Release --no-restore

Write-Host "Executing automated tests..." -ForegroundColor Yellow
dotnet test NOVA.Clip.sln -c Release --no-build --verbosity normal

Write-Host "Build & Test completed successfully!" -ForegroundColor Green
