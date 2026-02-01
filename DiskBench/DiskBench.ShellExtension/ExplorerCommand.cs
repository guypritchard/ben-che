using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace DiskBench.ShellExtension;

[ComVisible(true)]
[Guid(ComGuids.ExplorerCommand)]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IExplorerCommand))]
public sealed class ExplorerCommand : IExplorerCommand
{
    private const string CommandTitle = "Benchmark Drive Performance";
    private const string CommandTooltip = "Run DiskBench on this drive";

    public ExplorerCommand()
    {
        ShellLogger.Log("ExplorerCommand constructed");
    }

    public int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName)
    {
        ShellLogger.Log("GetTitle");
        ppszName = Marshal.StringToCoTaskMemUni(CommandTitle);
        return HResults.S_OK;
    }

    public int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon)
    {
        var iconPath = ShellLogger.ReadExePath();
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            var iconValue = string.Format(CultureInfo.InvariantCulture, "{0},0", iconPath);
            ShellLogger.Log($"GetIcon: {iconValue}");
            ppszIcon = Marshal.StringToCoTaskMemUni(iconValue);
            return HResults.S_OK;
        }

        ShellLogger.Log("GetIcon: no ExePath configured");
        ppszIcon = IntPtr.Zero;
        return HResults.S_FALSE;
    }

    public int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfotip)
    {
        ShellLogger.Log("GetToolTip");
        ppszInfotip = Marshal.StringToCoTaskMemUni(CommandTooltip);
        return HResults.S_OK;
    }

    public int GetCanonicalName(out Guid pguidCommandName)
    {
        ShellLogger.Log("GetCanonicalName");
        pguidCommandName = new Guid(ComGuids.ExplorerCommand);
        return HResults.S_OK;
    }

    public int GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        var isDrive = TryGetSelectedDrive(psiItemArray, out var drivePath);
        pCmdState = isDrive ? EXPCMDSTATE.ECS_ENABLED : EXPCMDSTATE.ECS_HIDDEN;
        ShellLogger.Log($"GetState: isDrive={isDrive} drivePath={drivePath ?? "<none>"}");
        return HResults.S_OK;
    }

    public int Invoke(IShellItemArray? psiItemArray, System.Runtime.InteropServices.ComTypes.IBindCtx? pbc)
    {
        ShellLogger.Log("Invoke");

        if (!TryGetSelectedDrive(psiItemArray, out var drivePath) || string.IsNullOrWhiteSpace(drivePath))
        {
            ShellLogger.Log("Invoke: no drive selected");
            return HResults.S_FALSE;
        }

        var exePath = ShellLogger.ReadExePath();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ShellLogger.Log($"Invoke: ExePath missing or not found: {exePath ?? "<null>"}");
            return HResults.E_FAIL;
        }

        var args = string.Format(CultureInfo.InvariantCulture, "--quick \"{0}\"", drivePath);
        ShellLogger.Log($"Invoke: launching '{exePath}' {args}");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return HResults.S_OK;
        }
        catch (Exception ex)
        {
            ShellLogger.Log($"Invoke: failed to start process: {ex}");
            return HResults.E_FAIL;
        }
    }

    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        ShellLogger.Log("GetFlags");
        pFlags = EXPCMDFLAGS.ECF_DEFAULT;
        return HResults.S_OK;
    }

    public int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        ShellLogger.Log("EnumSubCommands");
        ppEnum = null;
        return HResults.E_NOTIMPL;
    }

    private static bool TryGetSelectedDrive(IShellItemArray? psiItemArray, out string? drivePath)
    {
        ShellLogger.Log("TryGetSelectedDrive: start");
        drivePath = null;
        if (psiItemArray == null)
        {
            ShellLogger.Log("TryGetSelectedDrive: null item array");
            return false;
        }

        var countResult = psiItemArray.GetCount(out var count);
        if (countResult != HResults.S_OK || count != 1)
        {
            ShellLogger.Log($"TryGetSelectedDrive: countResult=0x{countResult:X8} count={count}");
            return false;
        }

        var itemResult = psiItemArray.GetItemAt(0, out var item);
        if (itemResult != HResults.S_OK || item == null)
        {
            ShellLogger.Log($"TryGetSelectedDrive: GetItemAt failed result=0x{itemResult:X8}");
            return false;
        }

        var displayNameResult = item.GetDisplayName(SIGDN.FILESYSPATH, out var pszName);
        if (displayNameResult != HResults.S_OK || pszName == IntPtr.Zero)
        {
            ShellLogger.Log($"TryGetSelectedDrive: GetDisplayName failed result=0x{displayNameResult:X8}");
            return false;
        }

        string? path;
        try
        {
            path = Marshal.PtrToStringUni(pszName);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pszName);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            ShellLogger.Log("TryGetSelectedDrive: empty path");
            return false;
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            ShellLogger.Log($"TryGetSelectedDrive: no root for path '{path}'");
            return false;
        }

        var normalizedPath = path.TrimEnd('\\');
        var normalizedRoot = root.TrimEnd('\\');
        if (!string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            ShellLogger.Log($"TryGetSelectedDrive: path not root path='{normalizedPath}' root='{normalizedRoot}'");
            return false;
        }

        if (normalizedRoot.Length < 2 || !char.IsLetter(normalizedRoot[0]) || normalizedRoot[1] != ':')
        {
            ShellLogger.Log($"TryGetSelectedDrive: root not drive '{normalizedRoot}'");
            return false;
        }

        var finalRoot = normalizedRoot + "\\";

        try
        {
            var driveInfo = new DriveInfo(finalRoot);
            if (!driveInfo.IsReady || driveInfo.DriveType != DriveType.Fixed)
            {
                ShellLogger.Log($"TryGetSelectedDrive: not fixed or not ready '{finalRoot}' type={driveInfo.DriveType}");
                return false;
            }
        }
        catch
        {
            ShellLogger.Log($"TryGetSelectedDrive: DriveInfo failed '{finalRoot}'");
            return false;
        }

        drivePath = finalRoot;
        ShellLogger.Log($"TryGetSelectedDrive: success '{drivePath}'");
        return true;
    }
}
