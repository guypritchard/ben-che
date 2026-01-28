# Install-ContextMenu.ps1
# Registers DiskBench in the Windows 11 modern context menu for drives
# Run as Administrator

#Requires -RunAsAdministrator

param(
    [switch]$Uninstall
)

$AppName = "DiskBench"
$AppDescription = "Benchmark Drive Performance"

# Get the path to the executable
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ExePath = Join-Path $ScriptDir "DiskBench.Wpf\bin\Debug\net10.0-windows\DiskBench.exe"

# Check if published version exists
$PublishedPath = Join-Path $ScriptDir "publish\DiskBench.exe"
if (Test-Path $PublishedPath) {
    $ExePath = $PublishedPath
}

if (-not (Test-Path $ExePath) -and -not $Uninstall) {
    Write-Host "Error: DiskBench.exe not found at: $ExePath" -ForegroundColor Red
    Write-Host "Please build the project first with: dotnet build DiskBench.Wpf" -ForegroundColor Yellow
    exit 1
}

# Unique identifiers for the app
$AppId = "DiskBench.QuickBenchmark"
$PackageFamilyName = "DiskBench_benchmark"

# Registry paths
$DriveShellKey = "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName"
$DriveShellExKey = "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName\command"

function Install-ContextMenu {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║              DiskBench Context Menu Installer                 ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Installing context menu for drives..." -ForegroundColor Cyan
    
    # Create the shell key
    if (-not (Test-Path $DriveShellKey)) {
        New-Item -Path $DriveShellKey -Force | Out-Null
    }
    
    # Set display properties
    Set-ItemProperty -Path $DriveShellKey -Name "(Default)" -Value "⚡ $AppDescription"
    Set-ItemProperty -Path $DriveShellKey -Name "Icon" -Value "`"$ExePath`",0"
    
    # Extended attribute removed - this makes it appear in the top-level Windows 11 context menu
    # when the Position is set
    Set-ItemProperty -Path $DriveShellKey -Name "Position" -Value "Top"
    
    # Create command key
    if (-not (Test-Path $DriveShellExKey)) {
        New-Item -Path $DriveShellExKey -Force | Out-Null
    }
    
    # Set the command - %V is the selected path
    $Command = "`"$ExePath`" --quick `"%V`""
    Set-ItemProperty -Path $DriveShellExKey -Name "(Default)" -Value $Command
    
    Write-Host "✓ Drive context menu installed!" -ForegroundColor Green
    
    # Also register for directory/folder right-click
    $DirShellKey = "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName"
    $DirShellExKey = "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName\command"
    
    if (-not (Test-Path $DirShellKey)) {
        New-Item -Path $DirShellKey -Force | Out-Null
    }
    Set-ItemProperty -Path $DirShellKey -Name "(Default)" -Value "⚡ Benchmark This Drive"
    Set-ItemProperty -Path $DirShellKey -Name "Icon" -Value "`"$ExePath`",0"
    Set-ItemProperty -Path $DirShellKey -Name "Position" -Value "Top"
    # Only show on root directories (drives)
    Set-ItemProperty -Path $DirShellKey -Name "AppliesTo" -Value "System.ItemPathDisplay:~<`"?:\\`""
    
    if (-not (Test-Path $DirShellExKey)) {
        New-Item -Path $DirShellExKey -Force | Out-Null
    }
    Set-ItemProperty -Path $DirShellExKey -Name "(Default)" -Value $Command
    
    Write-Host "✓ Directory context menu installed!" -ForegroundColor Green
    
    # Register for directory background (right-click in empty space)
    $DirBgShellKey = "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName"
    $DirBgShellExKey = "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName\command"
    
    if (-not (Test-Path $DirBgShellKey)) {
        New-Item -Path $DirBgShellKey -Force | Out-Null
    }
    Set-ItemProperty -Path $DirBgShellKey -Name "(Default)" -Value "⚡ Benchmark This Drive"
    Set-ItemProperty -Path $DirBgShellKey -Name "Icon" -Value "`"$ExePath`",0"
    Set-ItemProperty -Path $DirBgShellKey -Name "Position" -Value "Top"
    # Only show on root directories (drives)
    Set-ItemProperty -Path $DirBgShellKey -Name "AppliesTo" -Value "System.ItemPathDisplay:~<`"?:\\`""
    
    if (-not (Test-Path $DirBgShellExKey)) {
        New-Item -Path $DirBgShellExKey -Force | Out-Null
    }
    Set-ItemProperty -Path $DirBgShellExKey -Name "(Default)" -Value $Command
    
    Write-Host "✓ Background context menu installed!" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "Installation complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  • Right-click on any drive in File Explorer"
    Write-Host "  • Select '⚡ Benchmark Drive Performance'"
    Write-Host ""
    Write-Host "Note: On Windows 11, this appears in 'Show more options' menu." -ForegroundColor Gray
    Write-Host "      For top-level menu, see Install-SparsePackage.ps1" -ForegroundColor Gray
}

function Uninstall-ContextMenu {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║              DiskBench Context Menu Uninstaller               ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Removing context menu entries..." -ForegroundColor Cyan
    
    $keysToRemove = @(
        "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName"
    )
    
    foreach ($key in $keysToRemove) {
        if (Test-Path $key) {
            Remove-Item -Path $key -Recurse -Force
            Write-Host "  Removed: $key" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "✓ Context menu removed successfully!" -ForegroundColor Green
}

function Restart-Explorer {
    Write-Host ""
    Write-Host "Restarting Explorer..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process explorer
    Write-Host "✓ Explorer restarted" -ForegroundColor Green
}

# Main
if ($Uninstall) {
    Uninstall-ContextMenu
} else {
    Install-ContextMenu
}

Write-Host ""
$restart = Read-Host "Restart Explorer now to apply changes? (Y/N)"
if ($restart -eq 'Y' -or $restart -eq 'y') {
    Restart-Explorer
}
Write-Host ""
