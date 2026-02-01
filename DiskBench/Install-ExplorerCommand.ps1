# Install-ExplorerCommand.ps1
# Registers the DiskBench C# ExplorerCommand shell extension for Windows 11
# Run as Administrator

#Requires -RunAsAdministrator

param(
    [switch]$Uninstall,
    [switch]$SkipBuild,
    [switch]$SkipRegAsm
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

$ShellExtensionDll = Join-Path $ScriptDir "DiskBench.ShellExtension\bin\Debug\net48\DiskBench.ShellExtension.dll"
$PublishedShellExtensionDll = Join-Path $ScriptDir "publish\DiskBench.ShellExtension.dll"
if (Test-Path $PublishedShellExtensionDll) {
    $ShellExtensionDll = $PublishedShellExtensionDll
}


function Get-RegAsmPath {
    $regAsm64 = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
    if (Test-Path $regAsm64) {
        return $regAsm64
    }

    $regAsm32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"
    if (Test-Path $regAsm32) {
        return $regAsm32
    }

    return $null
}

function Register-ShellExtension {
    $regAsm = Get-RegAsmPath
    if (-not $regAsm) {
        Write-Host "Error: RegAsm.exe not found. Install .NET Framework 4.x." -ForegroundColor Red
        exit 1
    }

    function Register-ComFallback {
        Write-Host "RegAsm failed. Falling back to manual COM registration..." -ForegroundColor Yellow

        try {
            $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($ShellExtensionDll)
        } catch {
            Write-Host "Error: Unable to read assembly name from $ShellExtensionDll" -ForegroundColor Red
            exit 1
        }

        $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$ShellExtensionClsid"
        $inprocKey = Join-Path $clsidKey "InprocServer32"
        if (-not (Test-Path $clsidKey)) {
            New-Item -Path $clsidKey -Force | Out-Null
        }
        if (-not (Test-Path $inprocKey)) {
            New-Item -Path $inprocKey -Force | Out-Null
        }

        $codeBase = ([System.Uri]::new($ShellExtensionDll)).AbsoluteUri
        Set-ItemProperty -Path $clsidKey -Name "(Default)" -Value "DiskBench.ShellExtension.ExplorerCommand"
        Set-ItemProperty -Path $inprocKey -Name "(Default)" -Value "mscoree.dll"
        Set-ItemProperty -Path $inprocKey -Name "ThreadingModel" -Value "Both"
        Set-ItemProperty -Path $inprocKey -Name "Class" -Value "DiskBench.ShellExtension.ExplorerCommand"
        Set-ItemProperty -Path $inprocKey -Name "Assembly" -Value $assemblyName.FullName
        Set-ItemProperty -Path $inprocKey -Name "RuntimeVersion" -Value "v4.0.30319"
        Set-ItemProperty -Path $inprocKey -Name "CodeBase" -Value $codeBase
    }

    Write-Host "Registering COM server with RegAsm..." -ForegroundColor Cyan
    $regAsmArgs = @("$ShellExtensionDll", "/codebase", "/nologo")
    $regAsmProcess = Start-Process -FilePath $regAsm -ArgumentList $regAsmArgs -PassThru -NoNewWindow
    $regAsmCompleted = $regAsmProcess | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
    if (-not $regAsmCompleted) {
        Write-Host "Warning: RegAsm timed out. Killing process." -ForegroundColor Yellow
        Stop-Process -Id $regAsmProcess.Id -Force -ErrorAction SilentlyContinue
    } elseif ($regAsmProcess.ExitCode -ne 0) {
        Write-Host "Warning: RegAsm failed with exit code $($regAsmProcess.ExitCode)." -ForegroundColor Yellow
    }

    $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$ShellExtensionClsid\InprocServer32"
    if (-not (Test-Path $clsidKey)) {
        Register-ComFallback
    }

    Write-Host "Registering Explorer command..." -ForegroundColor Cyan
    $shellRoots = @(
        @{ Path = "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName"; AppliesTo = $null },
        @{ Path = "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName"; AppliesTo = $null },
        @{ Path = "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName"; AppliesTo = $null },
        @{ Path = "HKLM:\SOFTWARE\Classes\*\shell\$AppName"; AppliesTo = $null },
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
    }

    $approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
    try {
        if (-not (Test-Path $approvedKey)) {
            New-Item -Path $approvedKey -Force -ErrorAction Stop | Out-Null
            Write-Host "Created Approved registry key." -ForegroundColor DarkGray
        }
        Set-ItemProperty -Path $approvedKey -Name $ShellExtensionClsid -Value $AppName -ErrorAction Stop
        Write-Host "Registered shell extension in Approved list: $ShellExtensionClsid" -ForegroundColor Green
    } catch {
        Write-Host "Error registering in Approved list: $_" -ForegroundColor Red
        exit 1
    }

    $configKey = "HKLM:\SOFTWARE\DiskBench\ShellExtension"
    if (-not (Test-Path $configKey)) {
        New-Item -Path $configKey -Force | Out-Null
    }

    Set-ItemProperty -Path $configKey -Name "ExePath" -Value $ExePath
    Set-ItemProperty -Path $configKey -Name "Diagnostics" -Value 1 -Type DWord

    # Windows 11 compatibility: Also register for new context menu handler paths
    Write-Host "Registering Windows 11 context menu handler paths..." -ForegroundColor Cyan
    
    $win11ContextPaths = @(
        "HKLM:\SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\$AppName",
        "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers\$AppName"
    )
    
    foreach ($contextPath in $win11ContextPaths) {
        if (-not (Test-Path $contextPath)) {
            New-Item -Path $contextPath -Force -ErrorAction SilentlyContinue | Out-Null
        }
        Set-ItemProperty -Path $contextPath -Name "(Default)" -Value $ShellExtensionClsid -ErrorAction SilentlyContinue
    }

    Write-Host "Shell extension installed." -ForegroundColor Green
}

function Unregister-ShellExtension {
    $regAsm = Get-RegAsmPath
    if (-not $regAsm) {
        Write-Host "Error: RegAsm.exe not found. Install .NET Framework 4.x." -ForegroundColor Red
        exit 1
    }

    Write-Host "Unregistering Explorer command..." -ForegroundColor Cyan
    function Remove-RegistryKey {
        param([string]$Path)

        $regPath = $Path.Replace("HKLM:\", "HKLM\")
        $args = @("delete", "`"$regPath`"", "/f")
        Write-Host "Deleting registry key: $regPath" -ForegroundColor DarkGray
        $process = Start-Process -FilePath "reg.exe" -ArgumentList $args -PassThru -NoNewWindow
        $completed = $process | Wait-Process -Timeout 3 -ErrorAction SilentlyContinue
        if (-not $completed) {
            Write-Host "Warning: reg.exe delete timed out for $regPath" -ForegroundColor Yellow
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        } elseif ($process.ExitCode -ne 0) {
            Write-Host "Warning: reg.exe delete exited $($process.ExitCode) for $regPath" -ForegroundColor Yellow
        }
    }

    function Remove-RegistryValue {
        param([string]$Path, [string]$Name)

        $regPath = $Path.Replace("HKLM:\", "HKLM\")
        $args = @("delete", "`"$regPath`"", "/v", "`"$Name`"", "/f")
        Write-Host "Deleting registry value: $regPath\\$Name" -ForegroundColor DarkGray
        $process = Start-Process -FilePath "reg.exe" -ArgumentList $args -PassThru -NoNewWindow
        $completed = $process | Wait-Process -Timeout 3 -ErrorAction SilentlyContinue
        if (-not $completed) {
            Write-Host "Warning: reg.exe delete value timed out for $regPath\$Name" -ForegroundColor Yellow
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        } elseif ($process.ExitCode -ne 0) {
            Write-Host "Warning: reg.exe delete value exited $($process.ExitCode) for $regPath\\$Name" -ForegroundColor Yellow
        }
    }

    $shellPaths = @(
        "HKLM:\SOFTWARE\Classes\Drive\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\Background\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\*\shell\$AppName",
        "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shell\$AppName"
    )
    foreach ($shellPath in $shellPaths) {
        Remove-RegistryKey $shellPath
    }

    # Also clean up Windows 11 context menu handler paths
    $win11ContextPaths = @(
        "HKLM:\SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\$AppName",
        "HKLM:\SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\$AppName",
        "HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers\$AppName"
    )
    foreach ($contextPath in $win11ContextPaths) {
        if (Test-Path $contextPath) {
            Remove-RegistryKey $contextPath
        }
    }

    $configKey = "HKLM:\SOFTWARE\DiskBench\ShellExtension"
    if (Test-Path $configKey) {
        Remove-Item -Path $configKey -Recurse -Force
    }

    $approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
    if (Test-Path $approvedKey) {
        Remove-RegistryValue -Path $approvedKey -Name $ShellExtensionClsid
    }

    if (-not $SkipRegAsm -and (Test-Path $ShellExtensionDll)) {
        Write-Host "Unregistering COM server..." -ForegroundColor Cyan
        $regAsmArgs = @("$ShellExtensionDll", "/u", "/nologo")
        $regAsmProcess = Start-Process -FilePath $regAsm -ArgumentList $regAsmArgs -PassThru -NoNewWindow
        $regAsmCompleted = $regAsmProcess | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
        if (-not $regAsmCompleted) {
            Write-Host "Warning: RegAsm unregister timed out. Killing process." -ForegroundColor Yellow
            Stop-Process -Id $regAsmProcess.Id -Force -ErrorAction SilentlyContinue
        } elseif ($regAsmProcess.ExitCode -ne 0) {
            Write-Host "Warning: RegAsm unregister failed with exit code $($regAsmProcess.ExitCode)." -ForegroundColor Yellow
        }
    } elseif ($SkipRegAsm) {
        Write-Host "Skipping RegAsm unregister (SkipRegAsm set)." -ForegroundColor Yellow
    }

    $clsidKey = "HKLM:\SOFTWARE\Classes\CLSID\$ShellExtensionClsid"
    if (Test-Path $clsidKey) {
        Remove-RegistryKey $clsidKey
    }

    Write-Host "Shell extension removed." -ForegroundColor Green
}

function Restart-Explorer {
    Write-Host "Restarting Explorer..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process explorer
    Write-Host "Explorer restarted." -ForegroundColor Green
}

function Stop-ExplorerIfRunning {
    if (Get-Process -Name explorer -ErrorAction SilentlyContinue) {
        Write-Host "Stopping Explorer to release locked DLLs..." -ForegroundColor Cyan
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        return $true
    }

    return $false
}

$stoppedExplorerForBuild = $false

if ($Uninstall) {
    $stoppedExplorerForBuild = Stop-ExplorerIfRunning
    try {
        Unregister-ShellExtension
    } finally {
        if ($stoppedExplorerForBuild) {
            Start-Process explorer
            Write-Host "Explorer restarted." -ForegroundColor Green
        }
    }
} else {
    $stoppedExplorerForBuild = Stop-ExplorerIfRunning
    try {
        if (-not $SkipBuild) {
            Write-Host "Building DiskBench shell extension and app..." -ForegroundColor Cyan
            dotnet build (Join-Path $ScriptDir "DiskBench.ShellExtension\\DiskBench.ShellExtension.csproj")
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Error: build failed for DiskBench.ShellExtension." -ForegroundColor Red
                exit 1
            }

            dotnet build (Join-Path $ScriptDir "DiskBench.Wpf\\DiskBench.Wpf.csproj")
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Error: build failed for DiskBench.Wpf." -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "Skipping build (SkipBuild set)." -ForegroundColor Yellow
        }

        if (-not (Test-Path $ExePath)) {
            Write-Host "Error: DiskBench.exe not found at: $ExePath" -ForegroundColor Red
            exit 1
        }

        if (-not (Test-Path $ShellExtensionDll)) {
            Write-Host "Error: Shell extension DLL not found at: $ShellExtensionDll" -ForegroundColor Red
            exit 1
        }

        Register-ShellExtension
    } finally {
        if ($stoppedExplorerForBuild) {
            Start-Process explorer
            Write-Host "Explorer restarted." -ForegroundColor Green
        }
    }
}

if (-not $Uninstall -and -not $stoppedExplorerForBuild) {
    $restart = Read-Host "Restart Explorer now to apply changes? (Y/N)"
    if ($restart -eq 'Y' -or $restart -eq 'y') {
        Restart-Explorer
    }
}
