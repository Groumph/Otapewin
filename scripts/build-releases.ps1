#!/usr/bin/env pwsh
# Script to build release binaries locally for testing
# This mimics what the GitHub Actions release workflow does

param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0-local",
    
    [Parameter(Mandatory=$false)]
    [string[]]$Platforms = @("win-x64", "linux-x64", "osx-arm64"),
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Otapewin Local Release Builder" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$ProjectPath = "src/Otapewin/Otapewin.csproj"
$OutputBase = "publish"

# Clean if requested
if ($Clean -and (Test-Path $OutputBase)) {
    Write-Host "🧹 Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputBase -Recurse -Force
}

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputBase | Out-Null

Write-Host "📦 Building version: $Version" -ForegroundColor Green
Write-Host "📋 Platforms: $($Platforms -join ', ')" -ForegroundColor Green
Write-Host ""

$Success = @()
$Failed = @()

foreach ($Platform in $Platforms) {
    Write-Host "⚙️  Building for $Platform..." -ForegroundColor Cyan
    
    $OutputPath = Join-Path $OutputBase $Platform
    
    try {
        # Build
        dotnet publish $ProjectPath `
            --configuration Release `
            --runtime $Platform `
            --self-contained true `
            --output $OutputPath `
            /p:PublishSingleFile=true `
            /p:PublishTrimmed=true `
            /p:EnableCompressionInSingleFile=true `
            /p:DebugType=none `
            /p:DebugSymbols=false `
            /p:Version=$Version `
            --verbosity minimal
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Build successful" -ForegroundColor Green
            
            # Create archive
            $ArchiveName = if ($Platform -like "win-*") {
                "otapewin-$Platform.zip"
            } else {
                "otapewin-$Platform.tar.gz"
            }
            
            $ArchivePath = Join-Path $OutputBase $ArchiveName
            
            if ($Platform -like "win-*") {
                # Windows - create ZIP
                Compress-Archive -Path "$OutputPath\*" -DestinationPath $ArchivePath -Force
                Write-Host "   📦 Created: $ArchiveName" -ForegroundColor Green
            } else {
                # Unix - create tar.gz
                $CurrentDir = Get-Location
                Set-Location $OutputPath
                tar -czf (Join-Path $CurrentDir $ArchivePath) *
                Set-Location $CurrentDir
                Write-Host "   📦 Created: $ArchiveName" -ForegroundColor Green
            }
            
            # Get file size
            $Size = (Get-Item $ArchivePath).Length / 1MB
            Write-Host "   📊 Size: $([math]::Round($Size, 2)) MB" -ForegroundColor Gray
            
            $Success += $Platform
        } else {
            Write-Host "   ❌ Build failed" -ForegroundColor Red
            $Failed += $Platform
        }
    }
    catch {
        Write-Host "   ❌ Error: $_" -ForegroundColor Red
        $Failed += $Platform
    }
    
    Write-Host ""
}

# Summary
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "📊 Build Summary" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

if ($Success.Count -gt 0) {
    Write-Host "✅ Successful: $($Success.Count)" -ForegroundColor Green
    $Success | ForEach-Object { Write-Host "   - $_" -ForegroundColor Green }
}

if ($Failed.Count -gt 0) {
    Write-Host "❌ Failed: $($Failed.Count)" -ForegroundColor Red
    $Failed | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
}

Write-Host ""
Write-Host "📁 Output directory: $OutputBase" -ForegroundColor Cyan
Write-Host ""

if ($Failed.Count -eq 0) {
    Write-Host "🎉 All builds completed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  Some builds failed. Check the output above for details." -ForegroundColor Yellow
    exit 1
}
