using System.Runtime.InteropServices;

namespace DiskBench.Win32;

/// <summary>
/// Win32 P/Invoke declarations for file I/O operations.
/// </summary>
internal static partial class NativeMethods
{
    // File access flags
    internal const uint GENERIC_READ = 0x80000000;
    internal const uint GENERIC_WRITE = 0x40000000;

    // Share mode
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint FILE_SHARE_DELETE = 0x00000004;

    // Creation disposition
    internal const uint CREATE_NEW = 1;
    internal const uint CREATE_ALWAYS = 2;
    internal const uint OPEN_EXISTING = 3;
    internal const uint OPEN_ALWAYS = 4;

    // File flags
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    internal const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    internal const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
    internal const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;
    internal const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
    internal const uint FILE_FLAG_DELETE_ON_CLOSE = 0x04000000;

    // Error codes
    internal const int ERROR_IO_PENDING = 997;
    internal const int ERROR_HANDLE_EOF = 38;
    internal const int ERROR_OPERATION_ABORTED = 995;
    internal const int ERROR_SUCCESS = 0;

    // IOCTL codes
    internal const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
    internal const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    // Invalid handle value
    internal static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    // Storage property query constants
    internal const int PropertyStandardQuery = 0;
    internal const int StorageAccessAlignmentProperty = 6;
    internal const int StorageDeviceProperty = 0;
    internal const int StorageAdapterProperty = 1;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadFile(
        IntPtr hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        ref NativeOverlapped lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WriteFile(
        IntPtr hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        ref NativeOverlapped lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr CreateIoCompletionPort(
        IntPtr fileHandle,
        IntPtr existingCompletionPort,
        nuint completionKey,
        uint numberOfConcurrentThreads);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetQueuedCompletionStatus(
        IntPtr completionPort,
        out uint lpNumberOfBytesTransferred,
        out nuint lpCompletionKey,
        out IntPtr lpOverlapped,
        uint dwMilliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetQueuedCompletionStatusEx(
        IntPtr completionPort,
        [Out] OverlappedEntry[] lpCompletionPortEntries,
        uint ulCount,
        out uint ulNumEntriesRemoved,
        uint dwMilliseconds,
        [MarshalAs(UnmanagedType.Bool)] bool fAlertable);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostQueuedCompletionStatus(
        IntPtr completionPort,
        uint dwNumberOfBytesTransferred,
        nuint dwCompletionKey,
        IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetFilePointerEx(
        IntPtr hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetEndOfFile(IntPtr hFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FlushFileBuffers(IntPtr hFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetFileValidData(IntPtr hFile, long ValidDataLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint GetLastError();

    [LibraryImport("kernel32.dll")]
    internal static partial nuint SetThreadAffinityMask(IntPtr hThread, nuint dwThreadAffinityMask);

    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetCurrentThread();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetThreadPriority(IntPtr hThread, int nPriority);

    internal const int THREAD_PRIORITY_HIGHEST = 2;
    internal const int THREAD_PRIORITY_TIME_CRITICAL = 15;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint GetDiskFreeSpaceW(
        string lpRootPathName,
        out uint lpSectorsPerCluster,
        out uint lpBytesPerSector,
        out uint lpNumberOfFreeClusters,
        out uint lpTotalNumberOfClusters);

    // Volume information
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetVolumePathNameW(
        string lpszFileName,
        [Out] char[] lpszVolumePathName,
        uint cchBufferLength);
}

/// <summary>
/// OVERLAPPED_ENTRY structure for GetQueuedCompletionStatusEx.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OverlappedEntry
{
    public nuint CompletionKey;
    public IntPtr Overlapped;
    public nuint Internal;
    public uint NumberOfBytesTransferred;
}

/// <summary>
/// DISK_GEOMETRY_EX structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiskGeometryEx
{
    public long Cylinders;
    public int MediaType;
    public uint TracksPerCylinder;
    public uint SectorsPerTrack;
    public uint BytesPerSector;
    public long DiskSize;
    public byte Data; // Variable length data follows
}

/// <summary>
/// STORAGE_PROPERTY_QUERY structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StoragePropertyQuery
{
    public int PropertyId;
    public int QueryType;
    public byte AdditionalParameters;
}

/// <summary>
/// STORAGE_ACCESS_ALIGNMENT_DESCRIPTOR structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StorageAccessAlignmentDescriptor
{
    public uint Version;
    public uint Size;
    public uint BytesPerCacheLine;
    public uint BytesOffsetForCacheAlignment;
    public uint BytesPerLogicalSector;
    public uint BytesPerPhysicalSector;
    public uint BytesOffsetForSectorAlignment;
}

/// <summary>
/// STORAGE_DEVICE_DESCRIPTOR structure (partial - we only need the header and bus type).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StorageDeviceDescriptor
{
    public uint Version;
    public uint Size;
    public byte DeviceType;
    public byte DeviceTypeModifier;
    [MarshalAs(UnmanagedType.U1)]
    public bool RemovableMedia;
    [MarshalAs(UnmanagedType.U1)]
    public bool CommandQueueing;
    public uint VendorIdOffset;
    public uint ProductIdOffset;
    public uint ProductRevisionOffset;
    public uint SerialNumberOffset;
    public StorageBusType BusType;
    public uint RawPropertiesLength;
    // Raw property data follows
}

/// <summary>
/// STORAGE_ADAPTER_DESCRIPTOR structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StorageAdapterDescriptor
{
    public uint Version;
    public uint Size;
    public uint MaximumTransferLength;
    public uint MaximumPhysicalPages;
    public uint AlignmentMask;
    [MarshalAs(UnmanagedType.U1)]
    public bool AdapterUsesPio;
    [MarshalAs(UnmanagedType.U1)]
    public bool AdapterScansDown;
    [MarshalAs(UnmanagedType.U1)]
    public bool CommandQueueing;
    [MarshalAs(UnmanagedType.U1)]
    public bool AcceleratedTransfer;
    public StorageBusType BusType;
    public ushort BusMajorVersion;
    public ushort BusMinorVersion;
    public byte SrbType;
    public byte AddressType;
}

/// <summary>
/// Storage bus types from Windows SDK.
/// </summary>
internal enum StorageBusType
{
    BusTypeUnknown = 0x00,
    BusTypeScsi = 0x01,
    BusTypeAtapi = 0x02,
    BusTypeAta = 0x03,
    BusType1394 = 0x04,
    BusTypeSsa = 0x05,
    BusTypeFibre = 0x06,
    BusTypeUsb = 0x07,
    BusTypeRAID = 0x08,
    BusTypeiScsi = 0x09,
    BusTypeSas = 0x0A,
    BusTypeSata = 0x0B,
    BusTypeSd = 0x0C,
    BusTypeMmc = 0x0D,
    BusTypeVirtual = 0x0E,
    BusTypeFileBackedVirtual = 0x0F,
    BusTypeSpaces = 0x10,
    BusTypeNvme = 0x11,
    BusTypeSCM = 0x12,
    BusTypeUfs = 0x13,
    BusTypeMax = 0x14,
    BusTypeMaxReserved = 0x7F
}
