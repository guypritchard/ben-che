# Install-ExplorerCommand-Cpp.ps1
# Registers the DiskBench C++ ExplorerCommand shell extension for Windows 11
# Run as Administrator

#Requires -RunAsAdministrator

param(
    [switch]$Uninstall,
    [switch]$SkipBuild
)

$AppName = "DiskBench"
$CommandName = "Benchmark Drive Performance"
$ShellExtensionClsid = "{33560014-F9AA-43E9-83E3-3F58B9F03810}"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$ExePath = Join-Path $ScriptDir "DiskBench.Wpf\bin\Debug\net10.0-windows\DiskBench.exe"
$PublishedExePath = Join-Path $ScriptDir "publish\DiskBench.exe"
if (Test-Path $PublishedExePath) {
    $ExePath = $PublishedExePath
}

# C++ DLL path
$ShellExtensionDll = Join-Path $ScriptDir "DiskBench.ShellExtension.Cpp\bin\Debug\DiskBench.ShellExtension.Cpp.dll"

function Register-ShellExtension {
    Write-Host "Registering C++ shell extension..." -ForegroundColor Cyan
    
    # Step 1: Verify DLL exists
    if (-not (Test-Path $ShellExtensionDll)) {
        Write-Host "Error: Shell extension DLL not found at: $ShellExtensionDll" -ForegroundColor Red
        Write-Host "Please build the C++ project first in Visual Studio" -ForegroundColor Yellow
        exit 1
    }

    # Step 2: Register in Approved list (CRITICAL for Windows 11)
    Write-Host "Registering in Shell Extensions Approved list..." -ForegroundColor Cyan
    $approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
    try {
        if (-not (Test-Path $approvedKey)) {
            New-Item -Path $approvedKey -Force -ErrorAction Stop | Out-Null
            Write-Host "Created Approved registry key." -ForegroundColor DarkGray
        }
        Set-ItemProperty -Path $approvedKey -Name $ShellExtensionClsid -Value $AppName -ErrorAction Stop
        Write-Host "✓ Registered in Approved list: $ShellExtensionClsid" -ForegroundColor Green
    } catch {
        Write-Host "✗ Error registering in Approved list: $_" -ForegroundColor Red
        exit 1
    }

    # Step 3: Register shell verbs for legacy menu
    Write-Host "Registering shell verbs..." -ForegroundColor Cyan
    $shellRoots = @(
        @{ Path = "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName"; AppliesTo = $null },
        @{ Path = "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName"; AppliesTo = $null },
        @{ Path = "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName"; AppliesTo = $null },
        @{ Path = "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shell\$AppName"; AppliesTo = $null }
    )

    foreach ($shellRoot in $shellRoots) {
        $shellPath = $shellRoot.Path
        if (-not (Test-Path $shellPath)) {
            New-Item -Path $shellPath -Force | Out-Null
        }

        Set-ItemProperty -Path $shellPath -Name "MUIVerb" -Value $CommandName
        Set-ItemProperty -Path $shellPath -Name "Icon" -Value "`"$ExePath`",0"
        Set-ItemProperty -Path $shellPath -Name "Position" -Value "Top"
        Set-ItemProperty -Path $shellPath -Name "ExplorerCommandHandler" -Value $ShellExtensionClsid
        Set-ItemProperty -Path $shellPath -Name "CommandStateHandler" -Value $ShellExtensionClsid

        if ($shellRoot.AppliesTo) {
            Set-ItemProperty -Path $shellPath -Name "AppliesTo" -Value $shellRoot.AppliesTo
        } else {
            Remove-ItemProperty -Path $shellPath -Name "AppliesTo" -ErrorAction SilentlyContinue
        }
        Write-Host "✓ Registered: $($shellRoot.Path)" -ForegroundColor Green
    }

    # Step 4: Register for Windows 11 context menu handlers
    Write-Host "Registering context menu handlers..." -ForegroundColor Cyan
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

    # Step 5: Configure registry with exe path
    $configKey = "HKLM:\SOFTWARE\DiskBench\ShellExtension"
    if (-not (Test-Path $configKey)) {
        New-Item -Path $configKey -Force | Out-Null
    }

    Set-ItemProperty -Path $configKey -Name "ExePath" -Value $ExePath
    Write-Host "✓ Configured ExePath: $ExePath" -ForegroundColor Green

    # Step 6: Clear shell extension cache (important!)
    Write-Host "Clearing shell extension cache..." -ForegroundColor Cyan
    try {
        Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Cached" -Force -ErrorAction SilentlyContinue
        Write-Host "✓ Cache cleared" -ForegroundColor Green
    } catch {
        Write-Host "⚠ Could not clear cache (non-critical)" -ForegroundColor Yellow
    }

    Write-Host "Shell extension installed successfully!" -ForegroundColor Green
}

function Unregister-ShellExtension {
    Write-Host "Unregistering C++ shell extension..." -ForegroundColor Cyan
    
    function Remove-RegistryKey {
        param([string]$Path)
        if (Test-Path $Path) {
            Remove-Item -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "✓ Deleted: $Path" -ForegroundColor Green
        }
    }

    # Remove shell verbs
    $shellPaths = @(
        "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shell\$AppName"
    )
    foreach ($shellPath in $shellPaths) {
        Remove-RegistryKey $shellPath
    }

    # Remove context menu handlers
    $contextMenuPaths = @(
        "HKLM:\SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\$AppName",
        "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers\$AppName"
    )
    foreach ($contextPath in $contextMenuPaths) {
        Remove-RegistryKey $contextPath
    }

    # Remove from Approved list
    $approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
    if (Test-Path $approvedKey) {
        Remove-ItemProperty -Path $approvedKey -Name $ShellExtensionClsid -ErrorAction SilentlyContinue
        Write-Host "✓ Removed from Approved list" -ForegroundColor Green
    }

    # Remove config
    $configKey = "HKLM:\SOFTWARE\DiskBench\ShellExtension"
    if (Test-Path $configKey) {
        Remove-Item -Path $configKey -Recurse -Force
        Write-Host "✓ Removed configuration" -ForegroundColor Green
    }

    Write-Host "Shell extension removed successfully!" -ForegroundColor Green
}

function Restart-Explorer {
    Write-Host "Restarting Explorer..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process explorer
    Write-Host "✓ Explorer restarted." -ForegroundColor Green
}

function Stop-ExplorerIfRunning {
    if (Get-Process -Name explorer -ErrorAction SilentlyContinue) {
        Write-Host "Stopping Explorer to release locked files..." -ForegroundColor Cyan
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        return $true
    }
    return $false
}

# Main script
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "DiskBench C++ Shell Extension Installer" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$stoppedExplorer = Stop-ExplorerIfRunning

try {
    if ($Uninstall) {
        Unregister-ShellExtension
    } else {
        if (-not (Test-Path $ExePath)) {
            Write-Host "Error: DiskBench.exe not found at: $ExePath" -ForegroundColor Red
            exit 1
        }

        Register-ShellExtension
    }
} finally {
    if ($stoppedExplorer) {
        Start-Process explorer
        Write-Host "✓ Explorer restarted." -ForegroundColor Green
    }
}

if (-not $Uninstall -and -not $stoppedExplorer) {
    Write-Host ""
    $restart = Read-Host "Restart Explorer now to apply changes? (Y/N)"
    if ($restart -eq 'Y' -or $restart -eq 'y') {
        Restart-Explorer
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Right-click on a drive (C:, D:, etc.) in File Explorer" -ForegroundColor Cyan
Write-Host "2. Look for 'Benchmark Drive Performance' in the context menu" -ForegroundColor Cyan
Write-Host ""
