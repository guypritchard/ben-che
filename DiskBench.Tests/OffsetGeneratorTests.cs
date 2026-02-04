using DiskBench.Core;
using DiskBench.Win32;
using Xunit;

namespace DiskBench.Tests;

/// <summary>
/// Tests for the OffsetGenerator class.
/// </summary>
public class OffsetGeneratorTests
{
    [Fact]
    public void Sequential_GeneratesLinearOffsets()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Sequential,
            fileSize: 1024 * 1024,  // 1MB
            blockSize: 4096,        // 4KB
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 256);

        var offsets = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            offsets.Add(gen.GetNextOffset());
        }

        // Should be sequential
        Assert.Equal(0, offsets[0]);
        Assert.Equal(4096, offsets[1]);
        Assert.Equal(8192, offsets[2]);
        Assert.Equal(12288, offsets[3]);
    }

    [Fact]
    public void Sequential_WrapsAtEndOfFile()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Sequential,
            fileSize: 16384,  // 16KB
            blockSize: 4096,  // 4KB (4 blocks fit)
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 256);

        var offsets = new List<long>();
        for (int i = 0; i < 8; i++)
        {
            offsets.Add(gen.GetNextOffset());
        }

        // Should wrap after 4 blocks
        Assert.Equal(0, offsets[0]);
        Assert.Equal(4096, offsets[1]);
        Assert.Equal(8192, offsets[2]);
        Assert.Equal(12288, offsets[3]);
        Assert.Equal(0, offsets[4]);  // Wraps
        Assert.Equal(4096, offsets[5]);
    }

    [Fact]
    public void Random_GeneratesAlignedOffsets()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Random,
            fileSize: 1024 * 1024,  // 1MB
            blockSize: 4096,        // 4KB
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 1024);

        for (int i = 0; i < 100; i++)
        {
            var offset = gen.GetNextOffset();
            Assert.Equal(0, offset % 4096);  // Must be aligned
            Assert.True(offset >= 0);
            Assert.True(offset + 4096 <= 1024 * 1024);
        }
    }

    [Fact]
    public void Random_GeneratesVariedOffsets()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Random,
            fileSize: 1024 * 1024,  // 1MB
            blockSize: 4096,
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 1024);

        var uniqueOffsets = new HashSet<long>();
        for (int i = 0; i < 1000; i++)
        {
            uniqueOffsets.Add(gen.GetNextOffset());
        }

        // Should have significant variety (not just sequential)
        Assert.True(uniqueOffsets.Count > 100);
    }

    [Fact]
    public void Random_Deterministic_WithSameSeed()
    {
        var gen1 = new OffsetGenerator(
            AccessPattern.Random,
            fileSize: 1024 * 1024,
            blockSize: 4096,
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 256);

        var gen2 = new OffsetGenerator(
            AccessPattern.Random,
            fileSize: 1024 * 1024,
            blockSize: 4096,
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 256);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(gen1.GetNextOffset(), gen2.GetNextOffset());
        }
    }

    [Fact]
    public void Region_RespectsOffset()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Sequential,
            fileSize: 1024 * 1024,
            blockSize: 4096,
            regionOffset: 8192,  // Start at 8KB
            regionLength: 16384, // 16KB region
            seed: 42,
            precomputeCount: 256);

        var offset = gen.GetNextOffset();
        
        Assert.Equal(8192, offset);  // Should start at region offset
    }

    [Fact]
    public void Region_StaysWithinBounds()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Random,
            fileSize: 1024 * 1024,
            blockSize: 4096,
            regionOffset: 100 * 1024,   // Start at 100KB
            regionLength: 50 * 1024,    // 50KB region
            seed: 42,
            precomputeCount: 1024);

        for (int i = 0; i < 500; i++)
        {
            var offset = gen.GetNextOffset();
            Assert.True(offset >= 100 * 1024, $"Offset {offset} is below region start");
            Assert.True(offset + 4096 <= 150 * 1024, $"Offset {offset} exceeds region end");
        }
    }

    [Fact]
    public void ValidateAlignment_ReturnsTrue_WhenAligned()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Random,
            fileSize: 1024 * 1024,
            blockSize: 4096,
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 256);

        Assert.True(gen.ValidateAlignment(4096));
        Assert.True(gen.ValidateAlignment(512));
    }

    [Fact]
    public void Reset_RestartsFromBeginning()
    {
        var gen = new OffsetGenerator(
            AccessPattern.Sequential,
            fileSize: 1024 * 1024,
            blockSize: 4096,
            regionOffset: 0,
            regionLength: 0,
            seed: 42,
            precomputeCount: 256);

        var first = gen.GetNextOffset();
        gen.GetNextOffset();
        gen.GetNextOffset();
        gen.Reset();
        var afterReset = gen.GetNextOffset();

        Assert.Equal(first, afterReset);
    }
}
