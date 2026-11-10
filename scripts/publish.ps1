# Self-contained publish script for NOVA Clip
$ErrorActionPreference = "Stop"

$dotnetDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path "$dotnetDir\dotnet.exe") {
    $env:PATH = "$dotnetDir;$env:PATH"
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NOVA Clip 1.0 - Self-Contained Publish    " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$publishDir = "publish/win-x64"
$distDir = "dist"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Write-Host "Publishing self-contained win-x64 release..." -ForegroundColor Yellow
dotnet publish src/NOVA.Clip.App/NOVA.Clip.App.csproj -c Release -r win-x64 --self-contained -o $publishDir

Write-Host "Packaging portable zip distribution..." -ForegroundColor Yellow
$zipPath = "$distDir/NOVA-Clip-Portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$publishDir/*" -DestinationPath $zipPath -Force

Write-Host "Publish and portable packaging complete: $zipPath" -ForegroundColor Green
