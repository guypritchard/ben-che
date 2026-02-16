using System;
using System.Runtime.InteropServices;

namespace DiskBench.ShellExtension;

public static class ComGuids
{
    public const string ExplorerCommand = "33560014-f9aa-43e9-83e3-3f58b9f03810";
}

public static class HResults
{
    public const int S_OK = 0x00000000;
    public const int S_FALSE = 0x00000001;
    public const int E_NOTIMPL = unchecked((int)0x80004001);
    public const int E_FAIL = unchecked((int)0x80004005);
}

[Flags]
public enum EXPCMDFLAGS : uint
{
    ECF_DEFAULT = 0x00000000,
    ECF_HASSUBCOMMANDS = 0x00000001,
    ECF_HASSPLITBUTTON = 0x00000002,
    ECF_HIDELABEL = 0x00000004,
    ECF_ISSEPARATOR = 0x00000008,
    ECF_HASLUASHIELD = 0x00000010
}

[Flags]
public enum EXPCMDSTATE : uint
{
    ECS_ENABLED = 0x00000000,
    ECS_DISABLED = 0x00000001,
    ECS_HIDDEN = 0x00000002,
    ECS_CHECKED = 0x00000004,
    ECS_DEFAULT = 0x00000008
}

public enum SIGDN : uint
{
    FILESYSPATH = 0x80058000
}

public enum SIATTRIBFLAGS
{
    SIATTRIBFLAGS_AND = 0x1,
    SIATTRIBFLAGS_OR = 0x2,
    SIATTRIBFLAGS_APPCOMPAT = 0x3
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
}

[ComVisible(true)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c3233f05")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName);

    [PreserveSig]
    int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon);

    [PreserveSig]
    int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfotip);

    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    [PreserveSig]
    int GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState);

    [PreserveSig]
    int Invoke(IShellItemArray? psiItemArray, System.Runtime.InteropServices.ComTypes.IBindCtx? pbc);

    [PreserveSig]
    int GetFlags(out EXPCMDFLAGS pFlags);

    [PreserveSig]
    int EnumSubCommands(out IEnumExplorerCommand? ppEnum);
}

[ComImport]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumExplorerCommand
{
    [PreserveSig]
    int Next(uint celt, out IExplorerCommand? pUICommand, out uint pceltFetched);

    [PreserveSig]
    int Skip(uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out IEnumExplorerCommand? ppenum);
}

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetParent(out IShellItem? ppsi);

    [PreserveSig]
    int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);

    [PreserveSig]
    int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetPropertyDescriptionList(ref PROPERTYKEY keyType, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetAttributes(SIATTRIBFLAGS dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int GetCount(out uint pdwNumItems);

    [PreserveSig]
    int GetItemAt(uint dwIndex, out IShellItem? ppsi);

    [PreserveSig]
    int EnumItems(out IEnumShellItems? ppenumShellItems);
}

[ComImport]
[Guid("70629033-e363-4a28-a567-0db78006e6d7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumShellItems
{
    [PreserveSig]
    int Next(uint celt, out IShellItem? rgelt, out uint pceltFetched);

    [PreserveSig]
    int Skip(uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out IEnumShellItems? ppenum);
}
