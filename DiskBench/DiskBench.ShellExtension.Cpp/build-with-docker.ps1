#!/usr/bin/env powershell
# Build DiskBench C++ Shell Extension using Docker
# Uses pre-built Microsoft container with build tools already installed

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "DiskBench C++ Shell Extension - Docker Build" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
try {
    docker info | Out-Null -ErrorAction Stop
} catch {
    Write-Host "Error: Docker is not running!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please start Docker Desktop:" -ForegroundColor Yellow
    Write-Host "  1. Open Docker Desktop application" -ForegroundColor Yellow
    Write-Host "  2. Wait for 'Docker is running' message" -ForegroundColor Yellow
    Write-Host "  3. Run this script again" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "[1/3] Pulling Docker image..." -ForegroundColor Cyan
docker pull mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Could not pull Docker image" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[2/3] Building DLL in container..." -ForegroundColor Cyan
docker run --rm `
  -v "${scriptDir}:C:\src" `
  -w C:\src\DiskBench.ShellExtension.Cpp `
  mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022 `
  powershell -Command @"
Write-Host 'Building DiskBench.ShellExtension.Cpp.dll...' -ForegroundColor Cyan
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' `
  DiskBench.ShellExtension.Cpp.vcxproj `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:OutDir=bin\Release\
  
if (`$LASTEXITCODE -eq 0) {
  Write-Host 'Build succeeded!' -ForegroundColor Green
} else {
  exit 1
}
"@

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/3] Verifying DLL..." -ForegroundColor Cyan
$dllPath = Join-Path $scriptDir "bin\Release\DiskBench.ShellExtension.Cpp.dll"
if (Test-Path $dllPath) {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "SUCCESS! DLL built successfully" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Location: $dllPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Run as Administrator: Install-ExplorerCommand-Cpp.ps1" -ForegroundColor Cyan
    Write-Host "  2. Right-click a drive in File Explorer" -ForegroundColor Cyan
    Write-Host "  3. Look for 'Benchmark Drive Performance'" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host "Error: DLL not found at $dllPath" -ForegroundColor Red
    Write-Host "Check the build output above for errors" -ForegroundColor Red
    exit 1
}
