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
set "BUILDTOOLS_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
set "MSBUILD_EXE=%BUILDTOOLS_PATH%\MSBuild\Current\Bin\MSBuild.exe"

if exist "%MSBUILD_EXE%" goto :FOUND_BUILD_TOOLS
goto :DOWNLOAD_TOOLS

:FOUND_BUILD_TOOLS
echo Build Tools already installed at: %BUILDTOOLS_PATH%
echo.
goto :CHECK_SDK

:DOWNLOAD_TOOLS

REM ============================================================================
REM Step 2: Download Visual Studio Build Tools
REM ============================================================================

echo [2/5] Downloading Visual Studio Build Tools 2022...
echo.

set "TEMP_DIR=%TEMP%\DiskBench_Setup"
set "BUILDTOOLS_EXE=%TEMP_DIR%\vs_buildtools.exe"

call "%~dp0download-build-tools.bat" "%TEMP_DIR%"

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

if not exist "!MSBUILD_EXE!" goto :MSBUILD_MISSING
echo Found MSBuild: !MSBUILD_EXE!
goto :CHECK_SDK_HEADERS

:MSBUILD_MISSING
echo ERROR: MSBuild not found at: !MSBUILD_EXE!
pause
exit /b 1

:CHECK_SDK_HEADERS

REM Check for Windows SDK headers
echo Checking for Windows SDK headers...

set "SDK_PATH=C:\Program Files (x86)\Windows Kits\10\Include"
if not exist "!SDK_PATH!" (
    echo WARNING: Windows SDK Include path not found
    echo Expected: !SDK_PATH!
    echo You may need to install Windows SDK manually
    goto :SDK_DONE
)

echo Found Windows SDK: !SDK_PATH!

REM Find the newest SDK version installed
set "SDK_VER="
for /f "delims=" %%D in ('dir /b /ad "!SDK_PATH!\10.0.*" 2^>nul ^| sort') do set "SDK_VER=%%D"

if "!SDK_VER!"=="" (
    echo WARNING: No Windows SDK versions found under !SDK_PATH!
    goto :SDK_DONE
)

echo Using SDK version: !SDK_VER!

REM Check for specific headers needed for the project
if exist "!SDK_PATH!\!SDK_VER!\um\windows.h" (
    echo OK: windows.h found
) else (
    echo WARN: windows.h not found
)

if exist "!SDK_PATH!\!SDK_VER!\um\shobjidl.h" (
    echo OK: shobjidl.h found
) else (
    echo WARN: shobjidl.h not found
)

if exist "!SDK_PATH!\!SDK_VER!\um\shlwapi.h" (
    echo OK: shlwapi.h found
) else (
    echo WARN: shlwapi.h not found
)

:SDK_DONE

echo.

REM ============================================================================
REM Step 5: Create environment batch file
REM ============================================================================

echo [5/5] Creating environment configuration...
echo.

set "ENV_FILE=%BUILDTOOLS_PATH%\VC\Auxiliary\Build\vcvarsall.bat"

if not exist "!ENV_FILE!" goto :ENV_MISSING
goto :ENV_OK

:ENV_MISSING
echo ERROR: vcvarsall.bat not found
echo Expected: !ENV_FILE!
pause
exit /b 1

:ENV_OK

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
    echo call "!ENV_FILE!" x64
    echo.
    echo REM Navigate to project
    echo cd /d "%~dp0DiskBench.ShellExtension.Cpp"
    echo.
    echo REM Build
    echo echo Building DiskBench.ShellExtension.Cpp...
    echo "!MSBUILD_EXE!" DiskBench.ShellExtension.Cpp.vcxproj /p:Configuration=Release /p:Platform=x64
    echo.
    echo if %%errorlevel%% equ 0 ^(
    echo     echo.
    echo     echo Build succeeded!
    echo     echo DLL location: %%cd%%\bin\Release\DiskBench.ShellExtension.Cpp.dll
    echo     echo.
    echo ^) else ^(
    echo     echo.
    echo     echo Build failed! Check the error messages above.
    echo     echo.
    echo     pause
    echo     exit /b 1
    echo ^)
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
echo   OK: Visual Studio Build Tools 2022
echo   OK: MSVC Compiler (C++)
echo   OK: Windows SDK (headers ^& libraries)
echo   OK: Build tools (MSBuild)
echo.
echo Required Headers:
echo   OK: windows.h
echo   OK: shobjidl.h  (IExplorerCommand interface)
echo   OK: shlwapi.h   (string functions)
echo   OK: strsafe.h   (safe string functions)
echo   OK: objbase.h   (COM interfaces)
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
