# Register-Sparse-Package.ps1
# Registers/unregisters the DiskBench sparse package for Windows 11 context menu.

param(
    [switch]$Uninstall
)

$packageName = "DiskBench.Sparse"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Join-Path $scriptDir "AppxManifest.xml"
$externalLocation = Resolve-Path (Join-Path $scriptDir "..")
$exePath = Join-Path $externalLocation "DiskBench.Wpf\\bin\\Release\\net10.0-windows\\DiskBench.exe"
$dllPath = Join-Path $externalLocation "DiskBench.ShellExtension.Cpp\\bin\\Release\\DiskBench.ShellExtension.Cpp.dll"

if (-not (Test-Path $manifest)) {
    Write-Host "Manifest not found: $manifest" -ForegroundColor Red
    exit 1
}

if ($Uninstall) {
    $pkg = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
    if ($pkg) {
        Remove-AppxPackage -Package $pkg.PackageFullName
        Write-Host "Removed package: $($pkg.PackageFullName)" -ForegroundColor Green
    } else {
        Write-Host "Package not found: $packageName" -ForegroundColor Yellow
    }
    exit 0
}

if (-not (Test-Path $exePath)) {
    Write-Host "DiskBench.exe not found at: $exePath" -ForegroundColor Red
    Write-Host "Build the WPF app in Release before registering." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $dllPath)) {
    Write-Host "Shell extension DLL not found at: $dllPath" -ForegroundColor Red
    Write-Host "Build the C++ shell extension in Release before registering." -ForegroundColor Yellow
    exit 1
}

Write-Host "Registering sparse package..." -ForegroundColor Cyan
Write-Host "Manifest: $manifest"
Write-Host "External location: $externalLocation"

Add-AppxPackage -Register $manifest -ExternalLocation $externalLocation.Path -ForceApplicationShutdown

$configKey = "HKCU:\SOFTWARE\DiskBench\ShellExtension"
if (-not (Test-Path $configKey)) {
    New-Item -Path $configKey -Force | Out-Null
}
Set-ItemProperty -Path $configKey -Name "ExePath" -Value $exePath
Write-Host "Configured HKCU ExePath: $exePath" -ForegroundColor Green

Write-Host "Done." -ForegroundColor Green
