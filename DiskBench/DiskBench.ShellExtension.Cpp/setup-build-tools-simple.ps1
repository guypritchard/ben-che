#Requires -RunAsAdministrator

param()

Write-Host "DiskBench C++ Shell Extension - Build Tools Setup" -ForegroundColor Cyan
Write-Host ""

$BuildToolsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
$MSBuildExe = Join-Path $BuildToolsPath "MSBuild\Current\Bin\MSBuild.exe"

# Check if Build Tools already installed
if (Test-Path $MSBuildExe) {
    Write-Host "Build Tools already installed at:" -ForegroundColor Green
    Write-Host "  $BuildToolsPath" -ForegroundColor Green
} else {
    Write-Host "Build Tools not found. Downloading installer..." -ForegroundColor Yellow
    
    $TempDir = Join-Path $env:TEMP "vs_setup"
    $InstallerPath = Join-Path $TempDir "vs_buildtools.exe"
    
    if (-not (Test-Path $TempDir)) {
        New-Item -Path $TempDir -ItemType Directory -Force | Out-Null
    }
    
    if (-not (Test-Path $InstallerPath)) {
        Write-Host "Downloading Visual Studio Build Tools 2022 (~1.5 GB)..."
        Write-Host "This may take 5-15 minutes..."
        
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vs_buildtools.exe" -OutFile $InstallerPath
        } catch {
            Write-Host "Download failed: $_" -ForegroundColor Red
            exit 1
        }
    }
    
    Write-Host "Installing Build Tools..." -ForegroundColor Yellow
    Write-Host "This will take 10-30 minutes. Please wait..."
    
    $args = @(
        "--quiet",
        "--wait",
        "--norestart",
        "--add", "Microsoft.VisualStudio.Workload.VCTools",
        "--add", "Microsoft.VisualStudio.Component.Windows11SDK.22000",
        "--add", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64"
    )
    
    & $InstallerPath $args
    
    if ($LASTEXITCODE -eq 0 -or (Test-Path $MSBuildExe)) {
        Write-Host "Installation complete!" -ForegroundColor Green
    } else {
        Write-Host "Installation may have failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
}

# Verify headers
Write-Host ""
Write-Host "Checking Windows SDK headers..." -ForegroundColor Cyan

$SDKPath = "C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um"
$headers = @("windows.h", "shobjidl.h", "shlwapi.h", "strsafe.h", "objbase.h")

$allFound = $true
foreach ($header in $headers) {
    $path = Join-Path $SDKPath $header
    if (Test-Path $path) {
        Write-Host "  [OK] $header" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $header" -ForegroundColor Red
        $allFound = $false
    }
}

if (-not $allFound) {
    Write-Host ""
    Write-Host "Some headers are missing. Try running the installer again." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "All headers found!" -ForegroundColor Green

# Create build helper script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildBat = Join-Path $ScriptDir "build-cpp.bat"

$content = @"
@echo off
REM Build helper for DiskBench C++ Shell Extension

call "$($BuildToolsPath)\VC\Auxiliary\Build\vcvarsall.bat" x64

cd /d "$ScriptDir"

echo Building DiskBench.ShellExtension.Cpp...
"$MSBuildExe" DiskBench.ShellExtension.Cpp.vcxproj /p:Configuration=Release /p:Platform=x64

if %errorlevel% equ 0 (
    echo.
    echo Build succeeded!
    echo DLL: %cd%\bin\Release\DiskBench.ShellExtension.Cpp.dll
    echo.
) else (
    echo Build failed!
    exit /b 1
)
"@

$content | Out-File -FilePath $buildBat -Encoding ASCII -Force
Write-Host ""
Write-Host "Created: build-cpp.bat" -ForegroundColor Green

Write-Host ""
Write-Host "Setup complete!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next step: Run build-cpp.bat to compile the project"
Write-Host ""
