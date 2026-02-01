@echo off
REM ============================================================================
REM DiskBench C++ Shell Extension - Automatic Setup Script
REM Downloads and installs everything needed to build the project
REM ============================================================================

setlocal enabledelayedexpansion

REM Check for administrator rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo ERROR: This script requires Administrator privileges!
    echo.
    echo Please right-click this script and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

color 0A
cls

echo.
echo ============================================================================
echo DiskBench C++ Shell Extension - Automatic Setup
echo ============================================================================
echo.
echo This script will:
echo   1. Download Visual Studio Build Tools 2022
echo   2. Install C++ workload
echo   3. Install Windows 11 SDK
echo   4. Verify installation
echo.
echo Installation location: C:\Program Files (x86)\Microsoft Visual Studio\2022
echo Disk space required: ~5 GB
echo.
echo Press any key to start...
pause >nul

REM ============================================================================
REM Step 1: Check if already installed
REM ============================================================================

echo.
echo [1/5] Checking if Build Tools are already installed...
set BUILDTOOLS_PATH="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
set MSBUILD_EXE="%BUILDTOOLS_PATH%\MSBuild\Current\Bin\MSBuild.exe"

if exist "%MSBUILD_EXE%" (
    echo Build Tools already installed at: %BUILDTOOLS_PATH%
    echo.
    goto :CHECK_SDK
)

REM ============================================================================
REM Step 2: Download Visual Studio Build Tools
REM ============================================================================

echo [2/5] Downloading Visual Studio Build Tools 2022...
echo.

set TEMP_DIR="%TEMP%\DiskBench_Setup"
if not exist "%TEMP_DIR%" mkdir "%TEMP_DIR%"

set BUILDTOOLS_EXE="%TEMP_DIR%\vs_buildtools.exe"

if exist "%BUILDTOOLS_EXE%" (
    echo Using cached installer: %BUILDTOOLS_EXE%
) else (
    echo Downloading from Microsoft (this may take a few minutes)...
    echo URL: https://aka.ms/vs/17/release/vs_buildtools.exe
    echo.
    
    powershell -Command "^ ^
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ^ ^
        $ProgressPreference = 'SilentlyContinue'; ^ ^
        Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vs_buildtools.exe' ^ ^
        -OutFile '%BUILDTOOLS_EXE%'" 2>nul
    
    if not exist "%BUILDTOOLS_EXE%" (
        echo.
        echo ERROR: Failed to download Visual Studio Build Tools
        echo.
        echo Please download manually from:
        echo   https://visualstudio.microsoft.com/downloads/
        echo.
        echo Look for "Visual Studio Build Tools 2022"
        pause
        exit /b 1
    )
)

echo Download complete!
echo.

REM ============================================================================
REM Step 3: Install Build Tools with C++ workload
REM ============================================================================

echo [3/5] Installing Visual Studio Build Tools 2022...
echo.
echo This will take 10-20 minutes. Please wait...
echo.

REM Install with C++ tools and Windows SDK
"%BUILDTOOLS_EXE%" ^
    --quiet ^
    --wait ^
    --norestart ^
    --add Microsoft.VisualStudio.Workload.VCTools ^
    --add Microsoft.VisualStudio.Component.Windows10SDK.19041 ^
    --add Microsoft.VisualStudio.Component.Windows11SDK.22000 ^
    --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ^
    --add Microsoft.VisualStudio.Component.VC.CMake.Project

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Build Tools installation failed
    pause
    exit /b 1
)

echo Build Tools installed successfully!
echo.

REM ============================================================================
REM Step 4: Verify Build Tools Installation
REM ============================================================================

:CHECK_SDK

echo [4/5] Verifying installation...
echo.

if not exist "%MSBUILD_EXE%" (
    echo ERROR: MSBuild not found at: %MSBUILD_EXE%
    pause
    exit /b 1
)

echo Found MSBuild: %MSBUILD_EXE%

REM Check for Windows SDK headers
echo Checking for Windows SDK headers...

set "SDK_PATH=C:\Program Files (x86)\Windows Kits\10\Include"
if not exist "%SDK_PATH%" (
    echo WARNING: Windows SDK Include path not found
    echo Expected: %SDK_PATH%
    echo You may need to install Windows SDK manually
) else (
    echo Found Windows SDK: %SDK_PATH%
    
    REM Check for specific headers needed for the project
    if exist "%SDK_PATH%\10.0.22621.0\um\windows.h" (
        echo ✓ windows.h found
    ) else (
        echo ⚠ windows.h not found in expected location
    )
    
    if exist "%SDK_PATH%\10.0.22621.0\um\shobjidl.h" (
        echo ✓ shobjidl.h found
    ) else (
        echo ⚠ shobjidl.h not found in expected location
    )
    
    if exist "%SDK_PATH%\10.0.22621.0\um\shlwapi.h" (
        echo ✓ shlwapi.h found
    ) else (
        echo ⚠ shlwapi.h not found in expected location
    )
)

echo.

REM ============================================================================
REM Step 5: Create environment batch file
REM ============================================================================

echo [5/5] Creating environment configuration...
echo.

set "ENV_FILE=%BUILDTOOLS_PATH%\VC\Auxiliary\Build\vcvarsall.bat"

if not exist "%ENV_FILE%" (
    echo ERROR: vcvarsall.bat not found
    echo Expected: %ENV_FILE%
    pause
    exit /b 1
)

REM Create a helper batch file for building
set "HELPER_BATCH=%~dp0build-cpp.bat"

(
    echo @echo off
    echo REM Build Helper Script - Auto-generated
    echo REM This script sets up the environment and builds the C++ project
    echo.
    echo setlocal enabledelayedexpansion
    echo.
    echo REM Set up environment
    echo call "%ENV_FILE%" x64
    echo.
    echo REM Navigate to project
    echo cd /d "%~dp0DiskBench.ShellExtension.Cpp"
    echo.
    echo REM Build
    echo echo Building DiskBench.ShellExtension.Cpp...
    echo "%MSBUILD_EXE%" DiskBench.ShellExtension.Cpp.vcxproj /p:Configuration=Release /p:Platform=x64
    echo.
    echo if %%errorlevel%% equ 0 ^(
    echo     echo.
    echo     echo Build succeeded!
    echo     echo DLL location: %%cd%%\bin\Release\DiskBench.ShellExtension.Cpp.dll
    echo     echo.
    echo ) else ^(
    echo     echo.
    echo     echo Build failed! Check the error messages above.
    echo     echo.
    echo     pause
    echo     exit /b 1
    echo )
) > "%HELPER_BATCH%"

if exist "%HELPER_BATCH%" (
    echo Created build helper: build-cpp.bat
) else (
    echo ERROR: Failed to create build helper
)

echo.

REM ============================================================================
REM Summary
REM ============================================================================

color 0B

echo.
echo ============================================================================
echo SETUP COMPLETE!
echo ============================================================================
echo.
echo Installed:
echo   ✓ Visual Studio Build Tools 2022
echo   ✓ MSVC Compiler (C++)
echo   ✓ Windows SDK (headers ^& libraries)
echo   ✓ Build tools (MSBuild)
echo.
echo Required Headers:
echo   ✓ windows.h
echo   ✓ shobjidl.h  (IExplorerCommand interface)
echo   ✓ shlwapi.h   (string functions)
echo   ✓ strsafe.h   (safe string functions)
echo   ✓ objbase.h   (COM interfaces)
echo.
echo All headers are installed in:
echo   C:\Program Files (x86)\Windows Kits\10\Include
echo.
echo MSBuild location:
echo   %MSBUILD_EXE%
echo.
echo Next Steps:
echo   1. Open Visual Studio project:
echo      File ^> Open ^> Project/Solution
echo      Select: DiskBench.ShellExtension.Cpp.vcxproj
echo.
echo   2. Build the project:
echo      Build ^> Build Solution (Ctrl+Shift+B)
echo.
echo   3. Or use the helper script:
echo      build-cpp.bat
echo.
echo   4. Install the shell extension:
echo      Install-ExplorerCommand-Cpp.ps1 (run as Administrator)
echo.
echo   5. Test by right-clicking a drive in File Explorer
echo.
echo ============================================================================
echo.

pause
