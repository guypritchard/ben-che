@echo off
REM ============================================================================
REM Download Visual Studio Build Tools bootstrapper.
REM Optional: pass a layout dir as the 2nd arg to pre-download packages.
REM Outputs the full path to vs_buildtools.exe as the last line on success.
REM ============================================================================

setlocal

set "TEMP_DIR=%~1"
if "%TEMP_DIR%"=="" set "TEMP_DIR=%TEMP%\DiskBench_Setup"

set "LAYOUT_DIR=%~2"

if not exist "%TEMP_DIR%" mkdir "%TEMP_DIR%"

set "BUILDTOOLS_EXE=%TEMP_DIR%\vs_buildtools.exe"

if exist "%BUILDTOOLS_EXE%" (
    echo Installer already cached at: %BUILDTOOLS_EXE% 1>&2
    goto :LAYOUT
)

echo Downloading Visual Studio Build Tools... 1>&2
echo URL: https://aka.ms/vs/17/release/vs_buildtools.exe 1>&2

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ProgressPreference='SilentlyContinue';" ^
    "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12;" ^
    "Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vs_buildtools.exe' -OutFile '%BUILDTOOLS_EXE%'"

if not exist "%BUILDTOOLS_EXE%" (
    echo ERROR: Failed to download Visual Studio Build Tools 1>&2
    exit /b 1
)

:LAYOUT

if not "%LAYOUT_DIR%"=="" (
    if not exist "%LAYOUT_DIR%" mkdir "%LAYOUT_DIR%"

    echo Creating offline layout in: %LAYOUT_DIR% 1>&2
    "%BUILDTOOLS_EXE%" --layout "%LAYOUT_DIR%" --lang en-US ^
        --add Microsoft.VisualStudio.Workload.VCTools ^
        --add Microsoft.VisualStudio.Component.Windows10SDK.19041 ^
        --add Microsoft.VisualStudio.Component.Windows11SDK.22000 ^
        --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ^
        --add Microsoft.VisualStudio.Component.VC.CMake.Project ^
        --includeRecommended --includeOptional

    if %errorlevel% neq 0 (
        echo ERROR: Offline layout creation failed 1>&2
        exit /b 1
    )
)

echo %BUILDTOOLS_EXE%
exit /b 0
