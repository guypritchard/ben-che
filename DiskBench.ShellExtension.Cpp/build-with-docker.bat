@echo off
REM Build DiskBench C++ Shell Extension using Docker
REM Uses pre-built Microsoft container with build tools already installed
REM No manual setup needed!

setlocal enabledelayedexpansion
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%"

echo.
echo ============================================
echo DiskBench C++ Shell Extension - Docker Build
echo ============================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Docker is not running!
    echo.
    echo Please start Docker Desktop and try again:
    echo   1. Open Docker Desktop application
    echo   2. Wait for "Docker is running" message
    echo   3. Run this script again
    popd
    pause
    exit /b 1
)

echo [1/3] Checking Docker image...
docker pull mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022
if %errorlevel% neq 0 (
    echo Error: Could not pull Docker image
    echo Make sure you have internet connection
    popd
    pause
    exit /b 1
)

echo.
echo [2/3] Building DLL...
docker run --rm ^
  -v "%cd%":C:\src ^
  -w C:\src ^
  mcr.microsoft.com/windows/servercore:ltsc2022-with-buildtools-2022 ^
  powershell -Command ^
    "Write-Host 'Building...' -ForegroundColor Cyan; " ^
    "'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' DiskBench.ShellExtension.Cpp.vcxproj /p:Configuration=Release /p:Platform=x64 /p:OutDir=bin\Release\; " ^
    "if ($LASTEXITCODE -eq 0) { Write-Host 'Build succeeded!' -ForegroundColor Green } else { exit 1 }"

if %errorlevel% neq 0 (
    echo.
    echo Error: Build failed!
    popd
    pause
    exit /b 1
)

echo.
echo [3/3] Verifying DLL...
if exist "bin\Release\DiskBench.ShellExtension.Cpp.dll" (
    echo.
    echo ============================================
    echo SUCCESS! DLL built successfully
    echo ============================================
    echo.
    echo Location: %cd%\bin\Release\DiskBench.ShellExtension.Cpp.dll
    echo.
    echo Next steps:
    echo   1. Run as Administrator: Install-ExplorerCommand-Cpp.ps1
    echo   2. Right-click a drive in File Explorer
    echo   3. Look for "Benchmark Drive Performance"
    echo.
) else (
    echo Error: DLL not found after build!
    echo Check the build output above for errors
)

popd
pause
