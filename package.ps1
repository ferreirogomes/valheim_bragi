# Bragi Mod - Thunderstore / r2modman Packaging Script
# Run this from the repository root: .\package.ps1

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectFile = "src\BragiPlugin\BragiPlugin.csproj"

Write-Host "Packaging Bragi for Thunderstore / r2modman..." -ForegroundColor Cyan

# 1. Read manifest info
if (-not (Test-Path "manifest.json")) {
    Write-Error "manifest.json not found in repository root."
    exit 1
}

$manifest = Get-Content "manifest.json" | ConvertFrom-Json
$pkgName    = $manifest.name
$pkgVersion = $manifest.version_number
Write-Host "Package: $pkgName v$pkgVersion" -ForegroundColor Yellow

# 2. Build project
Write-Host "Building project ($Configuration)..."
dotnet build $ProjectFile -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit $LASTEXITCODE
}

# 3. Verify icon dimensions (Thunderstore requires exact 256x256 PNG)
if (Test-Path "icon.png") {
    Add-Type -AssemblyName System.Drawing
    $fullIconPath = (Resolve-Path "icon.png").Path
    $img = [System.Drawing.Image]::FromFile($fullIconPath)
    if ($img.Width -ne 256 -or $img.Height -ne 256) {
        Write-Host "Resizing icon.png to 256x256 (required by Thunderstore)..." -ForegroundColor Yellow
        $resized = New-Object System.Drawing.Bitmap 256, 256
        $g = [System.Drawing.Graphics]::FromImage($resized)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($img, 0, 0, 256, 256)
        $img.Dispose()
        $resized.Save($fullIconPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose()
        $resized.Dispose()
    } else {
        $img.Dispose()
    }
} else {
    Write-Error "icon.png not found! Thunderstore requires a 256x256 icon.png."
    exit 1
}

# 4. Prepare staging directory
$distDir = "dist"
$stagingDir = "$distDir\staging"
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory -Path "$stagingDir\plugins\Bragi" -Force | Out-Null

# Copy Thunderstore required root files
Copy-Item "manifest.json" "$stagingDir\"
Copy-Item "README.md"     "$stagingDir\"
Copy-Item "icon.png"      "$stagingDir\"

# Copy DLL
$dllPath = "src\BragiPlugin\bin\$Configuration\net48\Bragi.dll"
if (-not (Test-Path $dllPath)) {
    Write-Error "DLL not found at: $dllPath"
    exit 1
}
Copy-Item $dllPath "$stagingDir\plugins\Bragi\"

# Copy resources & songs if present
if (Test-Path "resources") {
    Copy-Item "resources" "$stagingDir\plugins\Bragi\" -Recurse -Force
}
if (Test-Path "songs") {
    New-Item -ItemType Directory -Path "$stagingDir\config\Bragi\songs" -Force | Out-Null
    Copy-Item "songs\*" "$stagingDir\config\Bragi\songs\" -Recurse -Force
}

# 5. Create zip file
$zipFile = "$distDir\$pkgName-$pkgVersion.zip"
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }

Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipFile -Force
Remove-Item $stagingDir -Recurse -Force

$roundedSize = [math]::Round(((Get-Item $zipFile).Length / 1KB), 1)
Write-Host "Thunderstore package created:" -ForegroundColor Green
Write-Host "   $zipFile ($roundedSize KB)" -ForegroundColor Cyan
Write-Host ""
Write-Host "To publish to r2modman:" -ForegroundColor Yellow
Write-Host "   1. Log in to https://valheim.thunderstore.io/"
Write-Host "   2. Click Upload (https://valheim.thunderstore.io/package/create/)"
Write-Host "   3. Drag and drop $zipFile"
Write-Host "   4. Select categories (e.g. Items, Quality of Life)"
Write-Host "   5. Click Submit"
