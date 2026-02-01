@echo off
REM Build DiskBench Shell Extension using Docker
REM Requires Docker Desktop for Windows

echo Building DiskBench C++ Shell Extension in Docker...
echo.

REM Check if Docker is installed
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Docker is not installed or not in PATH
    echo Download Docker Desktop from: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

REM Build the Docker image
echo Step 1: Building Docker image...
docker build -t diskbench-cpp-builder .
if %errorlevel% neq 0 (
    echo Error: Docker build failed
    pause
    exit /b 1
)

REM Run the container and extract the DLL
echo Step 2: Building the extension...
docker run --rm -v %cd%\output:/output diskbench-cpp-builder
if %errorlevel% neq 0 (
    echo Error: Build failed
    pause
    exit /b 1
)

echo.
echo Build complete!
echo DLL is at: %cd%\output\DiskBench.ShellExtension.Cpp.dll
pause
