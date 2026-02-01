# DiskBench Shell Extension - Windows 11 Issue Diagnosis

## Problem
The shell extension command "Benchmark Drive Performance" is **not appearing anywhere** on Windows 11 25H2, neither in the new context menu nor the legacy menu.

## Root Cause Identified
The shell extension is **NOT registered in the "Approved" list**, which is **mandatory for Windows 11**:
- Registry path: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved`
- Required value: `{33560014-F9AA-43E9-83E3-3F58B9F03810}` = `DiskBench`

When the install script was run previously, this critical step either:
1. Failed silently due to permission issues
2. Wasn't executed properly
3. The registry key didn't exist and creation failed

## What's Currently Registered ✓
```
HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{33560014-F9AA-43E9-83E3-3F58B9F03810}
    (Default)                = DiskBench.ShellExtension.ExplorerCommand
    InprocServer32          = mscoree.dll
    Assembly                = DiskBench.ShellExtension, Version=1.0.0.0
    RuntimeVersion          = v4.0.30319
    CodeBase                = file:///C:/Source/ben-che/DiskBench/DiskBench.ShellExtension/bin/Debug/net48/DiskBench.ShellExtension.DLL

HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Drive\shell\DiskBench
    MUIVerb                 = Benchmark Drive Performance
    ExplorerCommandHandler  = {33560014-F9AA-43E9-83E3-3F58B9F03810}
    CommandStateHandler     = {33560014-F9AA-43E9-83E3-3F58B9F03810}
```

## What's MISSING ✗
```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved
    {33560014-F9AA-43E9-83E3-3F58B9F03810} = DiskBench  [MISSING!]
```

## Solution
The shell extension MUST be added to the Approved list. This requires **Administrator** privileges.

### Quick Manual Fix (Requires Admin)
Run this in PowerShell as **Administrator**:
```powershell
$approvedKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
$clsid = "{33560014-F9AA-43E9-83E3-3F58B9F03810}"
Set-ItemProperty -Path $approvedKey -Name $clsid -Value "DiskBench" -Force
```

Then verify:
```powershell
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" /v $clsid
```

### After Registration
1. Stop Explorer: `Stop-Process -Name explorer -Force`
2. Clear cache: `Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Cached" -Force -ErrorAction SilentlyContinue`
3. Restart Explorer: `Start-Process explorer`
4. Test by right-clicking a drive

## Why This Matters
- **Approved List**: Windows security feature that validates shell extensions before loading them
- **Windows 11 25H2**: More strict about shell extension validation
- **Both Menus**: Without this, the extension won't load in either the new or legacy context menu

## Testing
After registration, check:
- [ ] Right-click on "C:" or "D:" drive in File Explorer
- [ ] Look in the new context menu (top section)
- [ ] Look in "Show more options" / Legacy menu
- [ ] Should see "Benchmark Drive Performance" with icon

## Additional Registry Entries Added
For Windows 11 full compatibility, also registered:
- `HKLM:\SOFTWARE\Classes\Drive\shellex\ContextMenuHandlers\DiskBench`
- `HKLM:\SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\DiskBench`
- `HKLM:\SOFTWARE\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers\DiskBench`

These entries point to the same CLSID and register the extension for the context menu handler system.
