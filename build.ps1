# Bragi Mod — Build Script
# Run this from the repository root to compile and deploy to BepInEx

param(
    [string]$Configuration = "Debug",
    # r2modman Default profile — where BepInEx + Jotunn are actually installed
    [string]$ValheimDir = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\Default"
)

$ErrorActionPreference = "Stop"
$ProjectFile = "src\BragiPlugin\BragiPlugin.csproj"

Write-Host "🎵 Building Bragi ($Configuration)..." -ForegroundColor Cyan

# Locate dotnet
$dotnet = @(
    "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
    "C:\Program Files\dotnet\dotnet.exe",
    "dotnet"
) | Where-Object { 
    try { Test-Path $_ -ErrorAction SilentlyContinue } catch { $false }
} | Select-Object -First 1

if (-not $dotnet) { 
    Write-Error "dotnet SDK not found. Install from https://aka.ms/dotnet/download"
    exit 1
}

Write-Host "Using dotnet: $dotnet"

# Build
& $dotnet build $ProjectFile `
    -c $Configuration `
    -p:ValheimDir=$ValheimDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "✅ Build successful!" -ForegroundColor Green

# Check if deployed
$pluginDir = "$ValheimDir\BepInEx\plugins\Bragi"
if (Test-Path "$pluginDir\Bragi.dll") {
    Write-Host "🚀 Deployed to: $pluginDir" -ForegroundColor Green
} else {
    Write-Host "⚠  BepInEx not found — copy bin\Debug\net48\Bragi.dll manually to BepInEx\plugins\Bragi\" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📦 To package for Thunderstore/r2modman:"
Write-Host "   Run: .\build.ps1 -Configuration Release"
Write-Host "   Then zip: BepInEx\, manifest.json, README.md, icon.png"
