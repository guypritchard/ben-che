# DiskBench C++ Shell Extension - Build Instructions

## Prerequisites - What to Download

### 1. **Visual Studio 2022** (Free Community Edition)
- **Download**: https://visualstudio.microsoft.com/vs/
- **Installation**:
  - Run the installer
  - Select **"Desktop development with C++"** workload
  - Select **"Windows application development"** workload
  - Ensure "Windows 11 SDK (10.0.26xxx)" is included
  - Install (approx 5-10 GB)

### 2. **Windows SDK** (Usually included with VS)
- Should be automatically installed with Visual Studio
- If missing: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/

## Project Structure

```
DiskBench.ShellExtension.Cpp/
├── DiskBench.ShellExtension.Cpp.vcxproj  (Visual Studio project file)
├── stdafx.h                               (precompiled headers)
├── ExplorerCommand.h                      (main interface implementation)
├── ExplorerCommand.cpp                    (implementation)
├── dllmain.cpp                            (DLL entry point & class factory)
├── version.rc                             (version resource)
├── bin/                                   (output directory - will be created)
└── obj/                                   (intermediate files - will be created)
```

## Build Instructions

### Method 1: Using Visual Studio IDE (Recommended)

1. **Open Visual Studio 2022**
2. **File → Open → Project/Solution**
3. Navigate to: `c:\Source\ben-che\DiskBench.ShellExtension.Cpp\DiskBench.ShellExtension.Cpp.vcxproj`
4. **Build → Build Solution** (Ctrl+Shift+B)
5. Wait for the build to complete (should see "Build succeeded")
6. DLL will be at: `DiskBench.ShellExtension.Cpp\bin\Debug\DiskBench.ShellExtension.Cpp.dll`

### Method 2: Using Command Line (Advanced)

```batch
cd C:\Source\ben-che\DiskBench.ShellExtension.Cpp
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ^
    DiskBench.ShellExtension.Cpp.vcxproj ^
    /p:Configuration=Release ^
    /p:Platform=x64
```

## Installation

After building successfully, run as Administrator:

```powershell
# Run the install script
cd C:\Source\ben-che
.\Install-ExplorerCommand.ps1
```

Or manually:

```powershell
# 1. Stop Explorer
Stop-Process -Name explorer -Force

# 2. Register the COM DLL
$regAsm = "C:\Program Files (x86)\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
& $regAsm "C:\Source\ben-che\DiskBench.ShellExtension.Cpp\bin\Debug\DiskBench.ShellExtension.Cpp.dll" /codebase

# 3. Register in Shell Extensions Approved list
$approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
$clsid = "{33560014-F9AA-43E9-83E3-3F58B9F03810}"
Set-ItemProperty -Path $approvedKey -Name $clsid -Value "DiskBench" -Force

# 4. Clear shell extension cache
Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Cached" -Force -ErrorAction SilentlyContinue

# 5. Restart Explorer
Start-Process explorer
```

## Configuration

The extension reads the DiskBench.exe path from:
```
HKEY_LOCAL_MACHINE\SOFTWARE\DiskBench\ShellExtension
  ExePath = C:\Path\To\DiskBench.exe
```

Set this value before installation:
```powershell
$configKey = "HKLM:\SOFTWARE\DiskBench\ShellExtension"
if (-not (Test-Path $configKey)) {
    New-Item -Path $configKey -Force | Out-Null
}
Set-ItemProperty -Path $configKey -Name "ExePath" -Value "C:\Source\ben-che\DiskBench.Wpf\bin\Debug\net10.0-windows\DiskBench.exe"
```

## Testing

1. **Right-click on a drive** (C:, D:, etc.) in File Explorer
2. Look for **"Benchmark Drive Performance"** in the context menu
3. Should appear in both:
   - New Windows 11 context menu (top section)
   - Legacy context menu ("Show more options")

## Troubleshooting

### Shell extension doesn't appear
1. Verify it's in Approved list:
   ```powershell
   reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" /v "{33560014-F9AA-43E9-83E3-3F58B9F03810}"
   ```

2. Check the DLL file exists:
   ```powershell
   Test-Path "C:\Source\ben-che\DiskBench.ShellExtension.Cpp\bin\Debug\DiskBench.ShellExtension.Cpp.dll"
   ```

3. Verify ExePath registry value:
   ```powershell
   reg query "HKLM\SOFTWARE\DiskBench\ShellExtension" /v ExePath
   ```

4. Check system event log for COM errors

### Build fails
- Ensure Windows SDK is installed (Visual Studio Installer)
- Close Visual Studio and clean build:
  ```batch
  del /s /q obj bin
  ```
- Rebuild solution

### DLL won't register (RegAsm fails)
- You're using a native C++ DLL, not a .NET assembly, so RegAsm is **not needed**
- Just register the shell extension using the registry entries above

## Advantages of C++ over C#

✓ **Better Windows 11 compatibility** - Native implementation
✓ **No .NET runtime required** - Works with just Windows SDK
✓ **Faster loading** - Native binary
✓ **Direct COM implementation** - No CLR overhead
✓ **Better isolation** - No interaction with other .NET apps

## Key Files

| File | Purpose |
|------|---------|
| `stdafx.h` | Precompiled headers, includes Windows headers |
| `ExplorerCommand.h` | COM interface definition |
| `ExplorerCommand.cpp` | IExplorerCommand implementation |
| `dllmain.cpp` | DLL entry, class factory |
| `version.rc` | Version info resource |
| `.vcxproj` | Visual Studio project configuration |

## C++ Features Used

- **COM** - Component Object Model (Windows standard for shell extensions)
- **IUnknown** - Base interface for all COM objects
- **IExplorerCommand** - Shell extension interface
- **Registry** - For reading configuration
- **Process creation** - For launching DiskBench.exe
- **String handling** - StringCchCopy, StringCchPrintf (safe versions)

