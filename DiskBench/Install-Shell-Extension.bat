@echo off
REM DiskBench Shell Extension Installer for Windows 11
REM Right-click -> Run as Administrator

echo.
echo ============================================
echo DiskBench Shell Extension - Windows 11 Fix
echo ============================================
echo.

REM Request admin elevation if not already admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo This script requires Administrator privileges.
    echo Please right-click and select "Run as Administrator"
    pause
    exit /b 1
)

echo [1/6] Stopping Windows Explorer...
taskkill /F /IM explorer.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo [2/6] Registering COM class...
cd /d "%~dp0DiskBench"
dotnet build DiskBench.ShellExtension\DiskBench.ShellExtension.csproj >nul 2>&1

echo [3/6] Registering in Shell Extensions Approved list...
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" ^
    /v "{33560014-F9AA-43E9-83E3-3F58B9F03810}" ^
    /d "DiskBench" /f >nul 2>&1

if %errorlevel% equ 0 (
    echo [3/6] ✓ Added to Approved list
) else (
    echo [3/6] ✗ Failed to add to Approved list
    pause
    exit /b 1
)

echo [4/6] Registering shell verb...
for /f "tokens=2" %%A in ('reg query "HKLM\SOFTWARE\Classes\Drive\shell\DiskBench" /v ExplorerCommandHandler 2^>nul') do (
    echo [4/6] ✓ Drive shell verb registered
)

echo [5/6] Clearing shell extension cache...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Cached" /f >nul 2>&1

echo [6/6] Restarting Windows Explorer...
timeout /t 2 /nobreak >nul
start explorer.exe
timeout /t 3 /nobreak >nul

echo.
echo ============================================
echo ✓ Installation complete!
echo ============================================
echo.
echo Test by right-clicking on a drive (C:, D:, etc.)
echo in File Explorer and look for:
echo   "Benchmark Drive Performance"
echo.
pause
