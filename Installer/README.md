# DiskBench MSI Installer (WiX v4)

This builds a simple MSI using open-source WiX Toolset v4.

## Prereqs
- .NET SDK (for `dotnet publish`)
- WiX v4 CLI:
  - `dotnet tool install --global wix`

## Build
From `C:\Source\ben-che`:

```powershell
.\Installer\build-installer.ps1
```

The MSI will be written to:

```
.\Installer\out\DiskBench.msi
```

## What the MSI does
- Installs to `C:\Program Files\DiskBench`
- Includes the WPF app (self-contained single EXE), the C++ shell extension, and the sparse package
- Runs the sparse package registration script on install
- Runs sparse package unregister on uninstall

## Notes
- Sparse package registration requires **Developer Mode** enabled.
- The custom actions are set to **ignore** failures, so the MSI won't fail if registration is blocked.
