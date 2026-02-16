namespace DiskBench.Core;

/// <summary>
/// Represents the bus/interface type used to connect a storage device.
/// </summary>
public enum StorageBusType
{
    /// <summary>Unknown or unrecognized bus type.</summary>
    Unknown = 0,
    /// <summary>SCSI bus.</summary>
    Scsi = 1,
    /// <summary>ATAPI (IDE CD/DVD).</summary>
    Atapi = 2,
    /// <summary>ATA/IDE bus.</summary>
    Ata = 3,
    /// <summary>IEEE 1394 (FireWire).</summary>
    Ieee1394 = 4,
    /// <summary>SSA bus.</summary>
    Ssa = 5,
    /// <summary>Fibre Channel.</summary>
    FibreChannel = 6,
    /// <summary>USB bus.</summary>
    Usb = 7,
    /// <summary>RAID controller.</summary>
    Raid = 8,
    /// <summary>iSCSI network storage.</summary>
    iScsi = 9,
    /// <summary>Serial Attached SCSI.</summary>
    Sas = 10,
    /// <summary>Serial ATA.</summary>
    Sata = 11,
    /// <summary>SD card.</summary>
    Sd = 12,
    /// <summary>MMC card.</summary>
    Mmc = 13,
    /// <summary>Virtual storage.</summary>
    Virtual = 14,
    /// <summary>File-backed virtual storage.</summary>
    FileBackedVirtual = 15,
    /// <summary>Storage Spaces.</summary>
    StorageSpaces = 16,
    /// <summary>NVMe (PCIe SSD).</summary>
    NVMe = 17,
    /// <summary>Storage Class Memory (Intel Optane).</summary>
    Scm = 18,
    /// <summary>Universal Flash Storage.</summary>
    Ufs = 19
}

/// <summary>
/// Detailed information about a storage drive.
/// </summary>
public sealed class DriveDetails
{
    /// <summary>
    /// Drive letter (e.g., "C:").
    /// </summary>
    public required string DriveLetter { get; init; }

    /// <summary>
    /// Volume label if available.
    /// </summary>
    public string? VolumeLabel { get; init; }

    /// <summary>
    /// Total capacity in bytes.
    /// </summary>
    public long TotalSize { get; init; }

    /// <summary>
    /// Available free space in bytes.
    /// </summary>
    public long FreeSpace { get; init; }

    /// <summary>
    /// File system type (NTFS, FAT32, etc.).
    /// </summary>
    public string? FileSystem { get; init; }

    /// <summary>
    /// The bus type used to connect this drive.
    /// </summary>
    public StorageBusType BusType { get; init; }

    /// <summary>
    /// Major version of the bus/protocol if reported by the adapter.
    /// </summary>
    public int? BusMajorVersion { get; init; }

    /// <summary>
    /// Minor version of the bus/protocol if reported by the adapter.
    /// </summary>
    public int? BusMinorVersion { get; init; }

    /// <summary>
    /// Logical sector size in bytes.
    /// </summary>
    public int LogicalSectorSize { get; init; }

    /// <summary>
    /// Physical sector size in bytes.
    /// </summary>
    public int PhysicalSectorSize { get; init; }

    /// <summary>
    /// Device vendor/manufacturer name if available.
    /// </summary>
    public string? VendorId { get; init; }

    /// <summary>
    /// Device product/model name if available.
    /// </summary>
    public string? ProductId { get; init; }

    /// <summary>
    /// Device serial number if available.
    /// </summary>
    public string? SerialNumber { get; init; }

    /// <summary>
    /// Whether the device is removable media.
    /// </summary>
    public bool IsRemovable { get; init; }

    /// <summary>
    /// Whether the device supports command queuing (NCQ/TCQ).
    /// </summary>
    public bool SupportsCommandQueuing { get; init; }

    /// <summary>
    /// Gets a friendly description of the bus type.
    /// </summary>
    public string BusTypeDescription => BusType switch
    {
        StorageBusType.Unknown => "Unknown",
        StorageBusType.Scsi => "SCSI",
        StorageBusType.Atapi => "ATAPI",
        StorageBusType.Ata => "ATA/IDE",
        StorageBusType.Ieee1394 => "FireWire",
        StorageBusType.Ssa => "SSA",
        StorageBusType.FibreChannel => "Fibre Channel",
        StorageBusType.Usb => "USB",
        StorageBusType.Raid => "RAID",
        StorageBusType.iScsi => "iSCSI",
        StorageBusType.Sas => "SAS",
        StorageBusType.Sata => "SATA",
        StorageBusType.Sd => "SD Card",
        StorageBusType.Mmc => "MMC",
        StorageBusType.Virtual => "Virtual",
        StorageBusType.FileBackedVirtual => "Virtual (File-backed)",
        StorageBusType.StorageSpaces => "Storage Spaces",
        StorageBusType.NVMe => "NVMe (PCIe)",
        StorageBusType.Scm => "Storage Class Memory",
        StorageBusType.Ufs => "UFS",
        _ => $"Unknown ({(int)BusType})"
    };

    /// <summary>
    /// Gets a short icon/emoji representation of the bus type.
    /// </summary>
    public string BusTypeIcon => BusType switch
    {
        StorageBusType.NVMe => "⚡", // Fast - PCIe
        StorageBusType.Sata => "💽", // Disk (minidisc - renders reliably)
        StorageBusType.Usb => "🔌",
        StorageBusType.Sd or StorageBusType.Mmc => "💳",
        StorageBusType.Virtual or StorageBusType.FileBackedVirtual => "☁️",
        StorageBusType.iScsi or StorageBusType.FibreChannel => "🌐",
        StorageBusType.Raid => "📦",
        _ => "💿"
    };

    /// <summary>
    /// Gets a performance tier estimate based on bus type.
    /// </summary>
    public string PerformanceTier => BusType switch
    {
        StorageBusType.NVMe => "Ultra",
        StorageBusType.Scm => "Ultra",
        StorageBusType.Sata => "High",
        StorageBusType.Sas => "High",
        StorageBusType.Raid => "High",
        StorageBusType.Usb => "Medium",
        StorageBusType.Sd or StorageBusType.Mmc or StorageBusType.Ufs => "Medium",
        StorageBusType.iScsi => "Variable",
        StorageBusType.FibreChannel => "High",
        StorageBusType.Virtual or StorageBusType.FileBackedVirtual => "Variable",
        _ => "Unknown"
    };

    /// <summary>
    /// Formats the drive details as a summary string.
    /// </summary>
    public override string ToString()
    {
        var product = !string.IsNullOrEmpty(ProductId) ? ProductId : "Unknown Device";
        var size = FormatBytes(TotalSize);
        return $"{DriveLetter} {product} ({size}, {BusTypeDescription})";
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:0.#} {suffixes[i]}";
    }
}
