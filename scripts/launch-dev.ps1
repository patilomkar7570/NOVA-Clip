# Launch development instance of NOVA Clip
$ErrorActionPreference = "Stop"

$dotnetDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path "$dotnetDir\dotnet.exe") {
    $env:PATH = "$dotnetDir;$env:PATH"
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NOVA Clip 1.0 - Launching Development App " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

dotnet run --project src/NOVA.Clip.App/NOVA.Clip.App.csproj
