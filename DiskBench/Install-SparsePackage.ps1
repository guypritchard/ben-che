# Install-SparsePackage.ps1
# 
# NOTE: Windows 11 sparse package context menus do NOT support Drive context menus.
# The desktop5:ItemType only supports file extensions, Directory, and Directory\Background.
#
# For drive context menus, use Install-ContextMenu.ps1 instead, which uses the 
# classic registry approach that works for all Windows versions including Windows 11.
#
# This script is kept for reference but redirects to the working solution.

#Requires -RunAsAdministrator

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
Write-Host "║                           IMPORTANT NOTE                               ║" -ForegroundColor Yellow
Write-Host "╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Yellow
Write-Host ""
Write-Host "Windows 11 sparse packages do NOT support Drive context menus." -ForegroundColor Yellow
Write-Host ""
Write-Host "The desktop5:ItemType API only supports:" -ForegroundColor Cyan
Write-Host "  • File extensions (*.txt, *.jpg, etc.)" -ForegroundColor White
Write-Host "  • Directory" -ForegroundColor White  
Write-Host "  • Directory\Background" -ForegroundColor White
Write-Host ""
Write-Host "For drive right-click menus, use the classic registry approach instead." -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$classicScript = Join-Path $scriptDir "Install-ContextMenu.ps1"

if (Test-Path $classicScript) {
    Write-Host "Would you like to run Install-ContextMenu.ps1 instead? (Y/N)" -ForegroundColor Green
    $choice = Read-Host
    if ($choice -eq "Y" -or $choice -eq "y") {
        Write-Host ""
        & $classicScript
    }
} else {
    Write-Host "Run Install-ContextMenu.ps1 to add the drive context menu." -ForegroundColor Green
}

Write-Host ""
