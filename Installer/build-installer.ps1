# Build-Installer.ps1
# Builds a self-contained WPF app, stages files, and creates an MSI using WiX v4.

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$stageDir = Join-Path $scriptDir "stage"
$outDir = Join-Path $scriptDir "out"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Host "WiX v4 CLI not found. Install with:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install --global wix" -ForegroundColor Yellow
    exit 1
}

function Ensure-WixExtension {
    param([string]$Id)
    $list = wix extension list 2>$null
    if ($list -notmatch [Regex]::Escape($Id)) {
        Write-Host "Adding WiX extension: $Id" -ForegroundColor Cyan
        wix extension add -g $Id
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Failed to add WiX extension: $Id" -ForegroundColor Red
            Write-Host "Try manually:" -ForegroundColor Yellow
            Write-Host "  wix extension add -g $Id" -ForegroundColor Yellow
            exit 1
        }
        $list = wix extension list 2>$null
        if ($list -notmatch [Regex]::Escape($Id)) {
            Write-Host "WiX extension still missing: $Id" -ForegroundColor Red
            Write-Host "Try manually:" -ForegroundColor Yellow
            Write-Host "  wix extension add -g $Id" -ForegroundColor Yellow
            exit 1
        }
    }
}

if (-not (Test-Path $stageDir)) {
    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
} else {
    Remove-Item -Recurse -Force -Path $stageDir
    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Build WPF app (self-contained single-file publish)
$wpfProj = Join-Path $repoRoot "DiskBench.Wpf\DiskBench.Wpf.csproj"
$wpfOut = Join-Path $stageDir "DiskBench.Wpf\bin\Release\net10.0-windows"
dotnet publish $wpfProj -c $Configuration -r win-x64 --self-contained true -o $wpfOut `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false

$wpfExe = Join-Path $wpfOut "DiskBench.exe"
if (-not (Test-Path $wpfExe)) {
    Write-Host "DiskBench.exe not found after publish: $wpfExe" -ForegroundColor Red
    exit 1
}

# Keep the app as a single EXE in the staged output.
$publishItems = Get-ChildItem -Force $wpfOut
$publishItems | Where-Object { $_.FullName -ne $wpfExe } | Remove-Item -Recurse -Force
$remaining = Get-ChildItem -Force $wpfOut
if ($remaining.Count -ne 1 -or $remaining[0].FullName -ne $wpfExe) {
    Write-Host "Publish output is not a single EXE in $wpfOut" -ForegroundColor Red
    $remaining | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Red }
    exit 1
}

# Build C++ shell extension
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    Write-Host "MSBuild not found at: $msbuild" -ForegroundColor Red
    exit 1
}
$cppProj = Join-Path $repoRoot "DiskBench.ShellExtension.Cpp\DiskBench.ShellExtension.Cpp.vcxproj"
$cppOutDir = Join-Path $stageDir "DiskBench.ShellExtension.Cpp\bin\Release"
$cppObjDir = Join-Path $stageDir "DiskBench.ShellExtension.Cpp\obj\Release"
New-Item -ItemType Directory -Force -Path $cppOutDir | Out-Null
New-Item -ItemType Directory -Force -Path $cppObjDir | Out-Null
& $msbuild $cppProj /p:Configuration=$Configuration /p:Platform=x64 /p:OutDir="$cppOutDir\\" /p:IntermediateOutputPath="$cppObjDir\\"
if ($LASTEXITCODE -ne 0) {
    Write-Host "C++ build failed." -ForegroundColor Red
    exit 1
}

# Copy sparse package files
Copy-Item -Recurse -Force (Join-Path $repoRoot "SparsePackage") (Join-Path $stageDir "SparsePackage")

# Copy app icon to stage root for ARP icon
Copy-Item -Force (Join-Path $repoRoot "DiskBench.Wpf\Assets\DiskBench.ico") (Join-Path $stageDir "DiskBench.ico")

# Ensure required extensions
Ensure-WixExtension "WixToolset.Util.wixext"
Ensure-WixExtension "WixToolset.UI.wixext"

# Build MSI
$msiPath = Join-Path $outDir "DiskBench.msi"
wix build -o $msiPath -d StageDir=$stageDir $scriptDir\Package.wxs `
    -ext WixToolset.Util.wixext `
    -ext WixToolset.UI.wixext
if ($LASTEXITCODE -ne 0) {
    Write-Host "WiX build failed." -ForegroundColor Red
    exit 1
}

Write-Host "MSI created at: $msiPath" -ForegroundColor Green
