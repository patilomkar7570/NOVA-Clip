# Inno Setup compilation script for NOVA Clip
$ErrorActionPreference = "Stop"

$isccCmd = Get-Command ISCC -ErrorAction SilentlyContinue
if ($isccCmd) {
    $isccPath = $isccCmd.Source
} elseif (Test-Path "C:\Program Files\Inno Setup 7\ISCC.exe") {
    $isccPath = "C:\Program Files\Inno Setup 7\ISCC.exe"
} elseif (Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") {
    $isccPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
} elseif (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") {
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
} else {
    Write-Error "ISCC.exe not found. Please install Inno Setup or ensure it is in PATH."
    exit 1
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NOVA Clip 1.0 - Inno Setup Compilation    " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path "dist" | Out-Null

Write-Host "Compiling installer with Inno Setup..." -ForegroundColor Yellow
& "$isccPath" "installer/NovaClip.iss"

Write-Host "Setup executable created at dist\NOVA-Clip-Setup.exe" -ForegroundColor Green
