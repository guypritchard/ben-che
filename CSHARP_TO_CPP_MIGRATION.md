# DiskBench C++ Shell Extension - Quick Start Guide

## What You Need to Download

### Visual Studio 2022 Community (FREE)
1. Go to: https://visualstudio.microsoft.com/vs/
2. Click "Download Visual Studio 2022 Community"
3. Run the installer
4. When asked what to install, select:
   - ✓ Desktop development with C++
   - ✓ Windows application development
5. Click Install (5-10 GB)
6. Wait for completion

That's it! You get everything you need (Windows SDK, C++ tools, etc.)

## Files Created

Your project is now at: `c:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp\`

**Key files:**
- `DiskBench.ShellExtension.Cpp.vcxproj` - Visual Studio project file
- `ExplorerCommand.h` - Interface definition
- `ExplorerCommand.cpp` - Main implementation
- `dllmain.cpp` - DLL entry point
- `version.rc` - Version info
- `README_BUILD.md` - Detailed build instructions

## Step-by-Step Build

1. **Open Visual Studio 2022**

2. **File → Open → Project/Solution**

3. Navigate to and select:
   ```
   c:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp\DiskBench.ShellExtension.Cpp.vcxproj
   ```

4. **Build → Build Solution** (or press Ctrl+Shift+B)

5. Wait for "Build succeeded" message

6. Your DLL is now at:
   ```
   c:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp\bin\Debug\DiskBench.ShellExtension.Cpp.dll
   ```

## Step-by-Step Installation

1. **Open PowerShell as Administrator**
   - Right-click PowerShell → "Run as Administrator"

2. **Navigate to the DiskBench folder:**
   ```powershell
   cd C:\Source\ben-che\DiskBench
   ```

3. **Run the installation script:**
   ```powershell
   .\Install-ExplorerCommand-Cpp.ps1
   ```

4. **Wait for completion** - you should see green ✓ checkmarks

5. **Test it:**
   - Open File Explorer
   - Right-click on a drive (C:, D:, etc.)
   - Look for "Benchmark Drive Performance" in the context menu

## What's Different from C#?

| Feature | C# | C++ |
|---------|----|----|
| Compile to | .NET bytecode | Native binary |
| Runtime needed | .NET Framework 4.8 | Windows native only |
| Performance | Good | Excellent |
| Windows 11 support | Good | Better |
| File size | ~50 KB | ~200-300 KB |
| Complexity | Simple | More complex |

## C++ Shell Extension Structure

```
┌─────────────────────────────────────┐
│     Windows 11 File Explorer        │
└─────────────────┬───────────────────┘
                  │ (right-click)
┌─────────────────▼───────────────────┐
│  Shell Extension Manager (Windows)  │
└─────────────────┬───────────────────┘
                  │ (loads COM object)
┌─────────────────▼───────────────────┐
│    DiskBench.ShellExtension.Cpp.dll │
│  - IExplorerCommand implementation  │
│  - COM Class Factory                │
└─────────────────┬───────────────────┘
                  │ (when invoked)
┌─────────────────▼───────────────────┐
│   DiskBench.exe --quick "C:"        │
│   (launches disk benchmark)         │
└─────────────────────────────────────┘
```

## How It Works

1. **User right-clicks a drive** in File Explorer
2. **Windows loads the DLL** (DiskBench.ShellExtension.Cpp.dll)
3. **Calls IExplorerCommand::GetState()** - checks if it's a drive
4. **Calls IExplorerCommand::GetTitle()** - returns "Benchmark Drive Performance"
5. **Calls IExplorerCommand::GetIcon()** - returns the icon path
6. **Displays in context menu** ✓
7. **User clicks the command**
8. **Calls IExplorerCommand::Invoke()** - launches DiskBench.exe with the drive letter

## Troubleshooting

### Build fails in Visual Studio
- **Solution**: Go to Tools → Options → Projects and Solutions → VC++ Project Settings
  - Set "Prefer using MSBuild 17.0 toolset" if available
  - Ensure Windows SDK is installed (look in Visual Studio Installer)

### "DLL not found" when running install script
- **Solution**: Check that build was successful and DLL exists at:
  ```powershell
  Test-Path "C:\Source\ben-che\DiskBench\DiskBench.ShellExtension.Cpp\bin\Debug\DiskBench.ShellExtension.Cpp.dll"
  ```

### Context menu still doesn't show
- **Solution**: Run in admin PowerShell:
  ```powershell
  .\Install-ExplorerCommand-Cpp.ps1 -Uninstall
  # Wait 10 seconds
  .\Install-ExplorerCommand-Cpp.ps1
  ```

### "Access Denied" when running install script
- **Solution**: Right-click PowerShell icon → "Run as Administrator"

## What Gets Created

After build and install:

**Registry entries (automatically created):**
```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved
    {33560014-F9AA-43E9-83E3-3F58B9F03810} = DiskBench

HKLM\SOFTWARE\Classes\Drive\shell\DiskBench
    ExplorerCommandHandler = {33560014-F9AA-43E9-83E3-3F58B9F03810}
    ...

HKLM\SOFTWARE\DiskBench\ShellExtension
    ExePath = C:\Source\ben-che\DiskBench\DiskBench.Wpf\bin\Debug\net10.0-windows\DiskBench.exe
```

## Questions?

See `README_BUILD.md` in the same folder for detailed technical information.

## Next Steps

1. ✓ Download Visual Studio 2022 Community Edition
2. ✓ Open the .vcxproj file in Visual Studio
3. ✓ Build the solution
4. ✓ Run the install script as Administrator
5. ✓ Test by right-clicking a drive

Let me know when you get to the build step!
