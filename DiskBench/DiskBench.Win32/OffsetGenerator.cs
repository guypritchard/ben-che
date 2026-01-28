using System.Runtime.CompilerServices;
using DiskBench.Core;

namespace DiskBench.Win32;

/// <summary>
/// Generates file offsets for sequential and random access patterns.
/// Offsets are precomputed to avoid RNG in the hot path.
/// </summary>
public sealed class OffsetGenerator
{
    private readonly long[] _offsets;
    private readonly int _count;
    private int _currentIndex;

    /// <summary>
    /// Gets the number of precomputed offsets.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Creates an offset generator.
    /// </summary>
    /// <param name="pattern">Access pattern.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="blockSize">Block size in bytes.</param>
    /// <param name="regionOffset">Region start offset.</param>
    /// <param name="regionLength">Region length (0 = entire file).</param>
    /// <param name="seed">Random seed.</param>
    /// <param name="precomputeCount">Number of offsets to precompute.</param>
    public OffsetGenerator(
        AccessPattern pattern,
        long fileSize,
        int blockSize,
        long regionOffset,
        long regionLength,
        int seed,
        int precomputeCount = 65536)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileSize);

        // Calculate effective region
        long effectiveLength = regionLength > 0 ? regionLength : (fileSize - regionOffset);
        long maxBlocks = effectiveLength / blockSize;

        if (maxBlocks <= 0)
        {
            throw new ArgumentException("Region too small for block size.");
        }

        _count = precomputeCount;
        _offsets = new long[_count];

        if (pattern == AccessPattern.Sequential)
        {
            // Sequential: cycle through file
            long currentOffset = regionOffset;
            for (int i = 0; i < _count; i++)
            {
                _offsets[i] = currentOffset;
                currentOffset += blockSize;
                if (currentOffset + blockSize > regionOffset + effectiveLength)
                {
                    currentOffset = regionOffset;
                }
            }
        }
        else
        {
            // Random: precompute random offsets (aligned)
            var random = new Random(seed);
            for (int i = 0; i < _count; i++)
            {
                long blockIndex = random.NextInt64(maxBlocks);
                _offsets[i] = regionOffset + (blockIndex * blockSize);
            }
        }
    }

    /// <summary>
    /// Gets the next offset (cycles through precomputed offsets).
    /// Zero-allocation hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetNextOffset()
    {
        int index = _currentIndex;
        _currentIndex = (index + 1) & (_count - 1); // Assumes count is power of 2
        return _offsets[index];
    }

    /// <summary>
    /// Gets offset at a specific index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetOffset(int index)
    {
        return _offsets[index & (_count - 1)];
    }

    /// <summary>
    /// Resets to the beginning.
    /// </summary>
    public void Reset()
    {
        _currentIndex = 0;
    }

    /// <summary>
    /// Validates that all offsets are properly aligned.
    /// </summary>
    public bool ValidateAlignment(int alignment)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_offsets[i] % alignment != 0)
            {
                return false;
            }
        }
        return true;
    }
}
