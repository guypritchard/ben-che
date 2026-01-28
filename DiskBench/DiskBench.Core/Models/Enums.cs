namespace DiskBench.Core;

/// <summary>
/// Specifies the IO access pattern for a workload.
/// </summary>
public enum AccessPattern
{
    /// <summary>
    /// Sequential access pattern - IOs proceed linearly through the file.
    /// </summary>
    Sequential,

    /// <summary>
    /// Random access pattern - IOs are randomly distributed across the file.
    /// </summary>
    Random
}

/// <summary>
/// Specifies when to flush file buffers to disk.
/// </summary>
public enum FlushPolicy
{
    /// <summary>
    /// No explicit flushing - relies on OS/filesystem behavior.
    /// </summary>
    None,

    /// <summary>
    /// Flush once at the end of the trial.
    /// </summary>
    AtEnd,

    /// <summary>
    /// Flush at regular intervals during the trial (configured via FlushInterval).
    /// </summary>
    Interval,

    /// <summary>
    /// Flush after every IO operation. Warning: significantly impacts performance.
    /// </summary>
    EveryIO
}
