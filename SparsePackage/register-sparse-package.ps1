# Register-Sparse-Package.ps1
# Registers/unregisters the DiskBench sparse package for Windows 11 context menu.

param(
    [switch]$Uninstall
)

$packageName = "DiskBench.Sparse"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Join-Path $scriptDir "AppxManifest.xml"
$externalLocation = Resolve-Path (Join-Path $scriptDir "..")
$exePath = Join-Path $externalLocation.Path "DiskBench.Wpf"
$exePath = Join-Path $exePath "bin"
$exePath = Join-Path $exePath "Release"
$exePath = Join-Path $exePath "net10.0-windows"
$exePath = Join-Path $exePath "DiskBench.exe"
$dllPath = Join-Path $externalLocation.Path "DiskBench.ShellExtension.Cpp"
$dllPath = Join-Path $dllPath "bin"
$dllPath = Join-Path $dllPath "Release"
$dllPath = Join-Path $dllPath "DiskBench.ShellExtension.Cpp.dll"

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
    $configKey = "HKCU:\SOFTWARE\DiskBench\ShellExtension"
    if (Test-Path $configKey) {
        Remove-Item -Path $configKey -Recurse -Force
        Write-Host "Removed HKCU config: $configKey" -ForegroundColor Green
    }
    $configKey = "HKLM:\SOFTWARE\DiskBench\ShellExtension"
    if (Test-Path $configKey) {
        Remove-Item -Path $configKey -Recurse -Force
        Write-Host "Removed HKLM config: $configKey" -ForegroundColor Green
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

$existing = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing existing package: $($existing.PackageFullName)" -ForegroundColor Yellow
    Remove-AppxPackage -Package $existing.PackageFullName
}

Add-AppxPackage -Register $manifest -ExternalLocation $externalLocation.Path -ForceApplicationShutdown

function Set-ExePath {
    param(
        [string]$RootPath,
        [string]$TargetPath
    )
    if (-not (Test-Path $RootPath)) {
        New-Item -Path $RootPath -Force | Out-Null
    }
    Set-ItemProperty -Path $RootPath -Name "ExePath" -Value $TargetPath
    Write-Host "Configured $RootPath ExePath: $TargetPath" -ForegroundColor Green
}

Set-ExePath -RootPath "HKCU:\SOFTWARE\DiskBench\ShellExtension" -TargetPath $exePath
try {
    Set-ExePath -RootPath "HKLM:\SOFTWARE\DiskBench\ShellExtension" -TargetPath $exePath
} catch {
    Write-Host "Warning: Failed to set HKLM ExePath (insufficient rights?)" -ForegroundColor Yellow
}

Write-Host "Done." -ForegroundColor Green
