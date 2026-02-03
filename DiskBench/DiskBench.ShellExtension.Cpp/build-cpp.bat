@echo off
REM Build Helper Script - Auto-generated
REM This script sets up the environment and builds the C++ project

setlocal enabledelayedexpansion

REM Set up environment
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsall.bat" x64

REM Navigate to project
cd /d "C:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp\DiskBench.ShellExtension.Cpp"

REM Build
echo Building DiskBench.ShellExtension.Cpp...
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" DiskBench.ShellExtension.Cpp.vcxproj /p:Configuration=Release /p:Platform=x64

if %errorlevel% equ 0 (
    echo.
    echo Build succeeded
    echo DLL location: %cd%\bin\Release\DiskBench.ShellExtension.Cpp.dll
    echo.
) else (
    echo.
    echo Build failed Check the error messages above.
    echo.
    pause
    exit /b 1
)
