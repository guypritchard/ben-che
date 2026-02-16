#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Automatic setup script for DiskBench C++ Shell Extension development
    
.DESCRIPTION
    Downloads and installs:
    - Visual Studio Build Tools 2022
    - C++ workload
    - Windows SDK (includes all required headers)
    
.EXAMPLE
    .\setup-build-tools.ps1
#>

$ErrorActionPreference = "Stop"

# Colors
function Write-Header {
    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host $args[0] -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    Write-Host "[*]" -ForegroundColor Green -NoNewline
    Write-Host " $($args[0])" -ForegroundColor White
}

function Write-Success {
    Write-Host "[✓]" -ForegroundColor Green -NoNewline
    Write-Host " $($args[0])" -ForegroundColor Green
}

function Write-Error-Custom {
    Write-Host "[✗]" -ForegroundColor Red -NoNewline
    Write-Host " $($args[0])" -ForegroundColor Red
}

function Write-Info {
    Write-Host "    $($args[0])" -ForegroundColor Gray
}

# Verify admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Error-Custom "This script requires Administrator privileges!"
    Write-Host ""
    Write-Host "Please right-click PowerShell and select 'Run as Administrator', then try again."
    Write-Host ""
    exit 1
}

Write-Header "DiskBench C++ Shell Extension - Automatic Setup"

Write-Host "This script will:"
Write-Host "  1. Download Visual Studio Build Tools 2022"
Write-Host "  2. Install C++ workload and Windows SDK"
Write-Host "  3. Verify all required headers are present"
Write-Host "  4. Create helper scripts for building"
Write-Host ""
Write-Host "Installation location: C:\Program Files (x86)\Microsoft Visual Studio\2022"
Write-Host "Disk space required: ~5 GB"
Write-Host "Time required: ~15-30 minutes"
Write-Host ""

$continue = Read-Host "Continue? (Y/N)"
if ($continue -ne "Y" -and $continue -ne "y") {
    Write-Host "Setup cancelled."
    exit 0
}

# ============================================================================
# Configuration
# ============================================================================

$BuildToolsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
$MSBuildExe = Join-Path $BuildToolsPath "MSBuild\Current\Bin\MSBuild.exe"
$BuildToolsInstaller = "https://aka.ms/vs/17/release/vs_buildtools.exe"
$TempDir = Join-Path $env:TEMP "DiskBench_Setup"
$InstallerPath = Join-Path $TempDir "vs_buildtools.exe"

# Headers we need
$RequiredHeaders = @(
    "windows.h",
    "shobjidl.h",
    "shlwapi.h",
    "strsafe.h",
    "objbase.h"
)

# ============================================================================
# Step 1: Check if already installed
# ============================================================================

Write-Step "Checking if Build Tools are already installed..."

if (Test-Path $MSBuildExe) {
    Write-Success "Build Tools already installed"
    Write-Info "Location: $BuildToolsPath"
    $SkipInstall = $true
} else {
    $SkipInstall = $false
}

# ============================================================================
# Step 2: Download installer if needed
# ============================================================================

if (-not $SkipInstall) {
    Write-Step "Downloading Visual Studio Build Tools 2022..."
    
    # Create temp directory
    if (-not (Test-Path $TempDir)) {
        New-Item -Path $TempDir -ItemType Directory -Force | Out-Null
    }
    
    # Check if already cached
    if (Test-Path $InstallerPath) {
        Write-Info "Using cached installer at: $InstallerPath"
    } else {
        Write-Info "Downloading from Microsoft (this may take several minutes)..."
        
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest -Uri $BuildToolsInstaller -OutFile $InstallerPath -ErrorAction Stop
            Write-Success "Downloaded: $InstallerPath"
        } catch {
            Write-Error-Custom "Failed to download Build Tools"
            Write-Host ""
            Write-Host "Please download manually from:"
            Write-Host "  https://visualstudio.microsoft.com/downloads/"
            Write-Host ""
            Write-Host "Look for: Visual Studio Build Tools 2022"
            Write-Host ""
            exit 1
        }
    }
}

# ============================================================================
# Step 3: Install Build Tools
# ============================================================================

if (-not $SkipInstall) {
    Write-Step "Installing Visual Studio Build Tools 2022..."
    Write-Info "This will take 10-30 minutes. Please wait..."
    Write-Host ""
    
    try {
        & $InstallerPath `
            --quiet `
            --wait `
            --norestart `
            --add Microsoft.VisualStudio.Workload.VCTools `
            --add Microsoft.VisualStudio.Component.Windows10SDK.19041 `
            --add Microsoft.VisualStudio.Component.Windows11SDK.22000 `
            --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
            --add Microsoft.VisualStudio.Component.VC.CMake.Project `
            2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build Tools installed successfully"
        } else {
            Write-Host "Installation exit code: $LASTEXITCODE"
            if (Test-Path $MSBuildExe) {
                Write-Info "MSBuild is available, continuing..."
            } else {
                Write-Error-Custom "Installation may have failed"
                exit 1
            }
        }
    } catch {
        Write-Error-Custom "Error during installation: $_"
        exit 1
    }
    
    Write-Host ""
    Start-Sleep -Seconds 2
}

# ============================================================================
# Step 4: Verify installation
# ============================================================================

Write-Step "Verifying installation..."

if (-not (Test-Path $MSBuildExe)) {
    Write-Error-Custom "MSBuild not found at: $MSBuildExe"
    exit 1
}

Write-Success "MSBuild found: $MSBuildExe"

# Check Windows SDK
$SDKPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um",
    "C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\um"
)

$SDKFound = $false
$ActualSDKPath = $null

foreach ($path in $SDKPaths) {
    if (Test-Path $path) {
        $SDKFound = $true
        $ActualSDKPath = $path
        break
    }
}

if ($SDKFound) {
    Write-Success "Windows SDK found: $ActualSDKPath"
} else {
    Write-Error-Custom "Windows SDK not found"
    Write-Info "Expected in: C:\Program Files (x86)\Windows Kits\10\Include"
    Write-Host ""
    exit 1
}

# ============================================================================
# Step 5: Check required headers
# ============================================================================

Write-Step "Checking required headers..."

$MissingHeaders = @()

foreach ($header in $RequiredHeaders) {
    $headerPath = Join-Path $ActualSDKPath $header
    
    if (Test-Path $headerPath) {
        Write-Success "$header found"
    } else {
        Write-Error-Custom "$header not found"
        $MissingHeaders += $header
    }
}

if ($MissingHeaders.Count -gt 0) {
    Write-Host ""
    Write-Error-Custom "Some headers are missing!"
    Write-Info "Try reinstalling Windows SDK component"
    exit 1
}

Write-Host ""
Write-Success "All required headers are present"

# ============================================================================
# Step 6: Create helper scripts
# ============================================================================

Write-Step "Creating helper scripts..."

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Create build script
$BuildScript = Join-Path $ScriptDir "build-cpp.bat"

$buildBatContent = @"
@echo off
REM Build helper script - Auto-generated by setup-build-tools.ps1
REM Builds DiskBench C++ Shell Extension

setlocal enabledelayedexpansion

REM Set up Visual Studio environment
call "$($BuildToolsPath)\VC\Auxiliary\Build\vcvarsall.bat" x64

REM Navigate to project
cd /d "$ScriptDir"

REM Show configuration
echo.
echo Configuration:
echo   MSBuild: $MSBuildExe
echo   Platform: x64
echo   Configuration: Release
echo   Project: DiskBench.ShellExtension.Cpp.vcxproj
echo.

REM Build
echo Building DiskBench.ShellExtension.Cpp...
"$MSBuildExe" DiskBench.ShellExtension.Cpp.vcxproj /p:Configuration=Release /p:Platform=x64

if %errorlevel% equ 0 (
    echo.
    echo ============================================
    echo Build succeeded!
    echo ============================================
    echo.
    echo DLL created at:
    echo   %cd%\bin\Release\DiskBench.ShellExtension.Cpp.dll
    echo.
    echo Next step:
    echo   Run Install-ExplorerCommand-Cpp.ps1
    echo.
) else (
    echo.
    echo Build failed! Check errors above.
    echo.
    pause
    exit /b 1
)
"@

$buildBatContent | Out-File -FilePath $BuildScript -Encoding ASCII -Force
Write-Success "Created: build-cpp.bat"

# Create Visual Studio shortcut helper
$VSScript = Join-Path $ScriptDir "open-in-visual-studio.bat"

$vsScriptContent = @"
@echo off
REM Open DiskBench.ShellExtension.Cpp project in Visual Studio

set "PROJ_FILE=$ScriptDir\DiskBench.ShellExtension.Cpp.vcxproj"

if not exist "!PROJ_FILE!" (
    echo Error: Project file not found
    echo Expected: !PROJ_FILE!
    pause
    exit /b 1
)

REM Find Visual Studio
set "DEVENV=$BuildToolsPath\Common7\IDE\devenv.exe"

if not exist "!DEVENV!" (
    echo Error: Visual Studio not found
    echo Expected: !DEVENV!
    pause
    exit /b 1
)

echo Opening project in Visual Studio...
start "" "!DEVENV!" "!PROJ_FILE!"
"@

$vsScriptContent | Out-File -FilePath $VSScript -Encoding ASCII -Force
Write-Success "Created: open-in-visual-studio.bat"

# ============================================================================
# Summary
# ============================================================================

Write-Header "SETUP COMPLETE!"

Write-Host "Installed Components:"
Write-Success "Visual Studio Build Tools 2022"
Write-Success "MSVC Compiler (C++)"
Write-Success "Windows SDK with headers"
Write-Success "MSBuild and build tools"

Write-Host ""
Write-Host "Required Headers (All Present):"
foreach ($header in $RequiredHeaders) {
    Write-Success "$header"
}

Write-Host ""
Write-Host "Build Paths:"
Write-Info "Build Tools: $BuildToolsPath"
Write-Info "MSBuild: $MSBuildExe"
Write-Info "Windows SDK: $ActualSDKPath"

Write-Host ""
Write-Host "Helper Scripts Created:"
Write-Info "build-cpp.bat - Compile the project"
Write-Info "open-in-visual-studio.bat - Open in Visual Studio IDE"

Write-Host ""
Write-Host "Next Steps:"
Write-Host ""
Write-Host "Option 1: Use Visual Studio IDE (Easiest)"
Write-Host "  1. Run: open-in-visual-studio.bat"
Write-Host "  2. Build → Build Solution (Ctrl+Shift+B)"
Write-Host ""
Write-Host "Option 2: Use build script (Command line)"
Write-Host "  1. Run: build-cpp.bat"
Write-Host "  2. Wait for build to complete"
Write-Host ""
Write-Host "Option 3: Use Docker (Alternative)"
Write-Host "  1. Run: build-with-docker.bat"
Write-Host "  2. Requires Docker Desktop"
Write-Host ""
Write-Host "After building:"
Write-Host "  1. Run as Administrator: Install-ExplorerCommand-Cpp.ps1"
Write-Host "  2. Test: Right-click a drive in File Explorer"
Write-Host "  3. Look for: 'Benchmark Drive Performance'"
Write-Host ""

Write-Host "Documentation:"
Write-Host "  • INDEX.txt - Project overview"
Write-Host "  • README_BUILD.md - Detailed build guide"
Write-Host "  • BUILD_OPTIONS.txt - Compare build methods"
Write-Host ""

Write-Header "Ready to build!"

Read-Host "Press Enter to close this window"
