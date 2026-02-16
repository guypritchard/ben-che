using System.Runtime.InteropServices;
using System.Text;
using DiskBench.Core;
using CoreBusType = DiskBench.Core.StorageBusType;

namespace DiskBench.Win32;

/// <summary>
/// Queries disk sector size and alignment requirements.
/// </summary>
internal static class DiskInfo
{
    /// <summary>
    /// Gets the sector size information for a file path.
    /// </summary>
    /// <param name="filePath">Path to file or directory.</param>
    /// <returns>Tuple of (logical sector size, physical sector size).</returns>
    public static (int LogicalSectorSize, int PhysicalSectorSize) GetSectorSize(string filePath)
    {
        // Get the volume root path
        var volumePath = new char[260];
        if (!NativeMethods.GetVolumePathNameW(filePath, volumePath, (uint)volumePath.Length))
        {
            // Fall back to drive letter
            if (filePath.Length >= 2 && filePath[1] == ':')
            {
                volumePath[0] = filePath[0];
                volumePath[1] = ':';
                volumePath[2] = '\\';
                volumePath[3] = '\0';
            }
            else
            {
                return (512, 512); // Default fallback
            }
        }

        var rootPath = new string(volumePath).TrimEnd('\0');
        if (!rootPath.EndsWith('\\'))
        {
            rootPath += '\\';
        }

        // Try IOCTL_STORAGE_QUERY_PROPERTY first for more accurate info
        var (logical, physical) = TryGetAlignmentDescriptor(rootPath);
        if (logical > 0 && physical > 0)
        {
            return (logical, physical);
        }

        // Fall back to GetDiskFreeSpace
        if (NativeMethods.GetDiskFreeSpaceW(rootPath, out _, out uint bytesPerSector, out _, out _) != 0)
        {
            return ((int)bytesPerSector, (int)bytesPerSector);
        }

        return (512, 512); // Ultimate fallback
    }

    private static (int Logical, int Physical) TryGetAlignmentDescriptor(string volumePath)
    {
        // Open the volume for querying
        var volumeName = volumePath.TrimEnd('\\');
        if (!volumeName.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            volumeName = @"\\.\" + volumeName;
        }

        var handle = NativeMethods.CreateFileW(
            volumeName,
            0, // No access needed for IOCTL
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == NativeMethods.INVALID_HANDLE_VALUE)
        {
            return (0, 0);
        }

        try
        {
            var query = new StoragePropertyQuery
            {
                PropertyId = NativeMethods.StorageAccessAlignmentProperty,
                QueryType = NativeMethods.PropertyStandardQuery
            };

            var querySize = Marshal.SizeOf<StoragePropertyQuery>();
            var descriptorSize = Marshal.SizeOf<StorageAccessAlignmentDescriptor>();

            var queryPtr = Marshal.AllocHGlobal(querySize);
            var descriptorPtr = Marshal.AllocHGlobal(descriptorSize);

            try
            {
                Marshal.StructureToPtr(query, queryPtr, false);

                if (NativeMethods.DeviceIoControl(
                    handle,
                    NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                    queryPtr,
                    (uint)querySize,
                    descriptorPtr,
                    (uint)descriptorSize,
                    out _,
                    IntPtr.Zero))
                {
                    var descriptor = Marshal.PtrToStructure<StorageAccessAlignmentDescriptor>(descriptorPtr);
                    return ((int)descriptor.BytesPerLogicalSector, (int)descriptor.BytesPerPhysicalSector);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(queryPtr);
                Marshal.FreeHGlobal(descriptorPtr);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }

        return (0, 0);
    }

    /// <summary>
    /// Gets comprehensive details about all available drives.
    /// </summary>
    public static IReadOnlyList<DriveDetails> GetAllDriveDetails()
    {
        var drives = new List<DriveDetails>();

        foreach (var driveInfo in DriveInfo.GetDrives())
        {
            if (!driveInfo.IsReady)
                continue;

            try
            {
                var details = GetDriveDetails(driveInfo.Name);
                if (details != null)
                {
                    drives.Add(details);
                }
            }
            catch
            {
                // Skip drives we can't query
            }
        }

        return drives;
    }

    /// <summary>
    /// Gets detailed information about a specific drive.
    /// </summary>
    /// <param name="drivePath">Drive path (e.g., "C:\").</param>
    /// <returns>Drive details or null if unavailable.</returns>
    public static DriveDetails? GetDriveDetails(string drivePath)
    {
        try
        {
            var driveInfo = new DriveInfo(drivePath);
            if (!driveInfo.IsReady)
                return null;

            var (logicalSector, physicalSector) = GetSectorSize(drivePath);
            var deviceInfo = GetDeviceDescriptor(drivePath);

            return new DriveDetails
            {
                DriveLetter = driveInfo.Name.TrimEnd('\\'),
                VolumeLabel = string.IsNullOrEmpty(driveInfo.VolumeLabel) ? null : driveInfo.VolumeLabel,
                TotalSize = driveInfo.TotalSize,
                FreeSpace = driveInfo.AvailableFreeSpace,
                FileSystem = driveInfo.DriveFormat,
                BusType = deviceInfo.BusType,
                BusMajorVersion = deviceInfo.BusMajorVersion,
                BusMinorVersion = deviceInfo.BusMinorVersion,
                LogicalSectorSize = logicalSector,
                PhysicalSectorSize = physicalSector,
                VendorId = deviceInfo.VendorId,
                ProductId = deviceInfo.ProductId,
                SerialNumber = deviceInfo.SerialNumber,
                IsRemovable = deviceInfo.IsRemovable,
                SupportsCommandQueuing = deviceInfo.SupportsCommandQueuing
            };
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct DeviceInfo(
        CoreBusType BusType,
        string? VendorId,
        string? ProductId,
        string? SerialNumber,
        bool IsRemovable,
        bool SupportsCommandQueuing,
        int? BusMajorVersion,
        int? BusMinorVersion);

    private static DeviceInfo GetDeviceDescriptor(string drivePath)
    {
        var volumePath = drivePath.TrimEnd('\\');
        if (!volumePath.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            volumePath = @"\\.\" + volumePath;
        }

        var handle = NativeMethods.CreateFileW(
            volumePath,
            0, // No access needed for IOCTL
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == NativeMethods.INVALID_HANDLE_VALUE)
        {
            return new DeviceInfo(CoreBusType.Unknown, null, null, null, false, false, null, null);
        }

        try
        {
            var query = new StoragePropertyQuery
            {
                PropertyId = NativeMethods.StorageDeviceProperty,
                QueryType = NativeMethods.PropertyStandardQuery
            };

            var querySize = Marshal.SizeOf<StoragePropertyQuery>();
            // Allocate a larger buffer for the variable-length data
            const int bufferSize = 1024;

            var queryPtr = Marshal.AllocHGlobal(querySize);
            var descriptorPtr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                Marshal.StructureToPtr(query, queryPtr, false);

                if (NativeMethods.DeviceIoControl(
                    handle,
                    NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                    queryPtr,
                    (uint)querySize,
                    descriptorPtr,
                    bufferSize,
                    out _,
                    IntPtr.Zero))
                {
                    var descriptor = Marshal.PtrToStructure<StorageDeviceDescriptor>(descriptorPtr);

                    // Extract strings from the buffer
                    string? vendorId = ExtractString(descriptorPtr, descriptor.VendorIdOffset);
                    string? productId = ExtractString(descriptorPtr, descriptor.ProductIdOffset);
                    string? serialNumber = ExtractString(descriptorPtr, descriptor.SerialNumberOffset);

                    var (busMajor, busMinor) = TryGetAdapterDescriptor(handle);
                    return new DeviceInfo(
                        MapBusType(descriptor.BusType),
                        vendorId,
                        productId,
                        serialNumber,
                        descriptor.RemovableMedia,
                        descriptor.CommandQueueing,
                        busMajor,
                        busMinor);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(queryPtr);
                Marshal.FreeHGlobal(descriptorPtr);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }

        return new DeviceInfo(CoreBusType.Unknown, null, null, null, false, false, null, null);
    }

    private static (int? Major, int? Minor) TryGetAdapterDescriptor(IntPtr handle)
    {
        var query = new StoragePropertyQuery
        {
            PropertyId = NativeMethods.StorageAdapterProperty,
            QueryType = NativeMethods.PropertyStandardQuery
        };

        var querySize = Marshal.SizeOf<StoragePropertyQuery>();
        var descriptorSize = Marshal.SizeOf<StorageAdapterDescriptor>();

        var queryPtr = Marshal.AllocHGlobal(querySize);
        var descriptorPtr = Marshal.AllocHGlobal(descriptorSize);

        try
        {
            Marshal.StructureToPtr(query, queryPtr, false);

            if (NativeMethods.DeviceIoControl(
                handle,
                NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                queryPtr,
                (uint)querySize,
                descriptorPtr,
                (uint)descriptorSize,
                out _,
                IntPtr.Zero))
            {
                var descriptor = Marshal.PtrToStructure<StorageAdapterDescriptor>(descriptorPtr);
                var major = descriptor.BusMajorVersion > 0 ? descriptor.BusMajorVersion : (ushort)0;
                var minor = descriptor.BusMinorVersion > 0 ? descriptor.BusMinorVersion : (ushort)0;

                if (major == 0 && minor == 0)
                {
                    return (null, null);
                }

                return (major, minor);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(queryPtr);
            Marshal.FreeHGlobal(descriptorPtr);
        }

        return (null, null);
    }

    private static string? ExtractString(IntPtr buffer, uint offset)
    {
        if (offset == 0)
            return null;

        var str = Marshal.PtrToStringAnsi(buffer + (int)offset);
        return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
    }

    private static CoreBusType MapBusType(StorageBusType busType) => busType switch
    {
        StorageBusType.BusTypeUnknown => CoreBusType.Unknown,
        StorageBusType.BusTypeScsi => CoreBusType.Scsi,
        StorageBusType.BusTypeAtapi => CoreBusType.Atapi,
        StorageBusType.BusTypeAta => CoreBusType.Ata,
        StorageBusType.BusType1394 => CoreBusType.Ieee1394,
        StorageBusType.BusTypeSsa => CoreBusType.Ssa,
        StorageBusType.BusTypeFibre => CoreBusType.FibreChannel,
        StorageBusType.BusTypeUsb => CoreBusType.Usb,
        StorageBusType.BusTypeRAID => CoreBusType.Raid,
        StorageBusType.BusTypeiScsi => CoreBusType.iScsi,
        StorageBusType.BusTypeSas => CoreBusType.Sas,
        StorageBusType.BusTypeSata => CoreBusType.Sata,
        StorageBusType.BusTypeSd => CoreBusType.Sd,
        StorageBusType.BusTypeMmc => CoreBusType.Mmc,
        StorageBusType.BusTypeVirtual => CoreBusType.Virtual,
        StorageBusType.BusTypeFileBackedVirtual => CoreBusType.FileBackedVirtual,
        StorageBusType.BusTypeSpaces => CoreBusType.StorageSpaces,
        StorageBusType.BusTypeNvme => CoreBusType.NVMe,
        StorageBusType.BusTypeSCM => CoreBusType.Scm,
        StorageBusType.BusTypeUfs => CoreBusType.Ufs,
        _ => CoreBusType.Unknown
    };
}
