#Requires -RunAsAdministrator
# Quick installation script for DiskBench shell extension
# This script focuses only on the registry entries needed for Windows 11

$ShellExtensionClsid = "{33560014-F9AA-43E9-83E3-3F58B9F03810}"
$AppName = "DiskBench"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ExePath = Join-Path $ScriptDir "DiskBench.Wpf\bin\Debug\net10.0-windows\DiskBench.exe"

Write-Host "DiskBench Shell Extension - Quick Install" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Step 1: Stop Explorer
Write-Host "`nStopping Windows Explorer..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Step 2: Add to Approved list (CRITICAL for Windows 11)
Write-Host "`nRegistering in Shell Extensions Approved list..." -ForegroundColor Yellow
$approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
try {
    if (-not (Test-Path $approvedKey)) {
        New-Item -Path $approvedKey -Force | Out-Null
    }
    Set-ItemProperty -Path $approvedKey -Name $ShellExtensionClsid -Value $AppName -Force
    Write-Host "✓ Added to Approved list" -ForegroundColor Green
} catch {
    Write-Host "✗ Error adding to Approved list: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Register shell verbs for legacy menu
Write-Host "`nRegistering shell verbs (legacy context menu)..." -ForegroundColor Yellow
$shellRoots = @(
    "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName"
)

foreach ($shellPath in $shellRoots) {
    if (-not (Test-Path $shellPath)) {
        New-Item -Path $shellPath -Force | Out-Null
    }
    Set-ItemProperty -Path $shellPath -Name "MUIVerb" -Value "Benchmark Drive Performance"
    Set-ItemProperty -Path $shellPath -Name "Icon" -Value "`"$ExePath`",0"
    Set-ItemProperty -Path $shellPath -Name "Position" -Value "Top"
    Set-ItemProperty -Path $shellPath -Name "ExplorerCommandHandler" -Value $ShellExtensionClsid
    Set-ItemProperty -Path $shellPath -Name "CommandStateHandler" -Value $ShellExtensionClsid
    Write-Host "✓ Registered: $shellPath" -ForegroundColor Green
}

# Step 4: Register for new Windows 11 context menu (shellex)
Write-Host "`nRegistering context menu handlers (Windows 11 new menu)..." -ForegroundColor Yellow
$contextMenuPaths = @(
    "HKLM:\SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\$AppName",
    "HKLM:\SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\$AppName",
    "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers\$AppName"
)

foreach ($contextPath in $contextMenuPaths) {
    if (-not (Test-Path $contextPath)) {
        New-Item -Path $contextPath -Force | Out-Null
    }
    Set-ItemProperty -Path $contextPath -Name "(Default)" -Value $ShellExtensionClsid
    Write-Host "✓ Registered: $contextPath" -ForegroundColor Green
}

# Step 5: Clear the Shell Extension cache (important!)
Write-Host "`nClearing shell extension cache..." -ForegroundColor Yellow
$cachedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Cached"
if (Test-Path $cachedKey) {
    try {
        Remove-Item $cachedKey -Force -ErrorAction SilentlyContinue
        Write-Host "✓ Cache cleared" -ForegroundColor Green
    } catch {
        Write-Host "⚠ Could not clear cache (non-critical)" -ForegroundColor Yellow
    }
}

# Step 6: Restart Explorer
Write-Host "`nRestarting Windows Explorer..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
Start-Process explorer
Start-Sleep -Seconds 3

Write-Host "`n" -ForegroundColor Green
Write-Host "✓ Installation complete!" -ForegroundColor Green
Write-Host "`nNow test by:" -ForegroundColor Cyan
Write-Host "  1. Right-click on a Drive in File Explorer" -ForegroundColor Cyan
Write-Host "  2. Look for 'Benchmark Drive Performance' command" -ForegroundColor Cyan
Write-Host "  3. Check both the new context menu AND legacy menu" -ForegroundColor Cyan
