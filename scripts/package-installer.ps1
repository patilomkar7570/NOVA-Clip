# Inno Setup compilation script for NOVA Clip
$ErrorActionPreference = "Stop"

$innoDir = "$env:LOCALAPPDATA\Programs\Inno Setup 6"
$isccPath = "$innoDir\ISCC.exe"

if (-not (Test-Path $isccPath)) {
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}

if (-not (Test-Path $isccPath)) {
    Write-Error "ISCC.exe not found. Please install Inno Setup 6 or ensure it is in PATH."
    exit 1
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NOVA Clip 1.0 - Inno Setup Compilation    " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path "dist" | Out-Null

Write-Host "Compiling installer with Inno Setup..." -ForegroundColor Yellow
& "$isccPath" "installer/NovaClip.iss"

Write-Host "Setup executable created at dist\NOVA-Clip-Setup.exe" -ForegroundColor Green
