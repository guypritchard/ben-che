using Xunit;
using DiskBench.Core;

namespace DiskBench.Tests;

/// <summary>
/// Tests for usage profile functionality.
/// </summary>
public sealed class UsageProfileTests
{
    [Fact]
    public void All_ReturnsAllProfiles()
    {
        var profiles = UsageProfiles.All;

        Assert.NotEmpty(profiles);
        Assert.Equal(10, profiles.Count); // 10 predefined profiles
    }

    [Theory]
    [InlineData(UsageProfileType.Gaming)]
    [InlineData(UsageProfileType.VideoStreaming)]
    [InlineData(UsageProfileType.Compiling)]
    [InlineData(UsageProfileType.WebBrowsing)]
    [InlineData(UsageProfileType.Database)]
    [InlineData(UsageProfileType.VirtualMachine)]
    [InlineData(UsageProfileType.FileServer)]
    [InlineData(UsageProfileType.MediaEditing)]
    [InlineData(UsageProfileType.OperatingSystem)]
    [InlineData(UsageProfileType.Backup)]
    public void Get_ReturnsCorrectProfile(UsageProfileType type)
    {
        var profile = UsageProfiles.Get(type);

        Assert.Equal(type, profile.Type);
        Assert.NotNull(profile.Name);
        Assert.NotNull(profile.Description);
        Assert.NotEmpty(profile.Workloads);
    }

    [Fact]
    public void GamingProfile_HasExpectedWorkloads()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Gaming);

        Assert.Equal("Gaming", profile.Name);
        Assert.Equal(4, profile.Workloads.Count);
        Assert.Contains(profile.Workloads, w => w.Name == "Initial Load");
        Assert.Contains(profile.Workloads, w => w.Name == "Texture Streaming");
        Assert.Contains(profile.Workloads, w => w.Name == "Audio Streaming");
        Assert.Contains(profile.Workloads, w => w.Name == "Save/Checkpoint");
    }

    [Fact]
    public void GenerateWorkloads_CreatesWorkloadSpecs()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Gaming);
        var filePath = "test.dat";

        var workloads = UsageProfiles.GenerateWorkloads(profile, filePath);

        Assert.Equal(profile.Workloads.Count, workloads.Count);
        Assert.All(workloads, w => Assert.Equal(filePath, w.FilePath));
        Assert.All(workloads, w => Assert.Equal(profile.RecommendedFileSize, w.FileSize));
    }

    [Fact]
    public void GenerateWorkloads_RespectsFileSizeOverride()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Gaming);
        var filePath = "test.dat";
        var customSize = 1024L * 1024 * 1024 * 2; // 2 GB

        var workloads = UsageProfiles.GenerateWorkloads(profile, filePath, customSize);

        Assert.All(workloads, w => Assert.Equal(customSize, w.FileSize));
    }

    [Fact]
    public void GenerateWorkloads_PreservesWorkloadSettings()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Database);
        var filePath = "test.dat";

        var workloads = UsageProfiles.GenerateWorkloads(profile, filePath);

        var pageReads = workloads.First(w => w.Name == "Page Reads");
        Assert.Equal(8 * 1024, pageReads.BlockSize);
        Assert.Equal(AccessPattern.Random, pageReads.Pattern);
        Assert.Equal(0, pageReads.WritePercent);
        Assert.True(pageReads.NoBuffering);
    }

    [Fact]
    public void CreatePlan_GeneratesValidPlan()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Compiling);
        var filePath = "test.dat";

        var plan = UsageProfiles.CreatePlan(profile, filePath, trials: 2);

        Assert.NotNull(plan.Name);
        Assert.Contains("Compilation", plan.Name);
        Assert.Equal(profile.Workloads.Count, plan.Workloads.Count);
        Assert.Equal(2, plan.Trials);
    }

    [Fact]
    public void CreatePlan_RespectsCustomDuration()
    {
        var profile = UsageProfiles.Get(UsageProfileType.WebBrowsing);
        var filePath = "test.dat";
        var duration = TimeSpan.FromSeconds(60);

        var plan = UsageProfiles.CreatePlan(
            profile,
            filePath,
            measuredDuration: duration);

        Assert.Equal(duration, plan.MeasuredDuration);
    }

    [Fact]
    public void CreatePlan_WorkloadNamesArePrefixed()
    {
        var profile = UsageProfiles.Get(UsageProfileType.VideoStreaming);
        var filePath = "test.dat";

        var plan = UsageProfiles.CreatePlan(profile, filePath);

        Assert.All(plan.Workloads, w =>
            Assert.StartsWith(profile.Name + ":", w.Name));
    }

    [Theory]
    [InlineData(UsageProfileType.Gaming)]
    [InlineData(UsageProfileType.Database)]
    [InlineData(UsageProfileType.VirtualMachine)]
    public void Profile_WorkloadWeightsSumToReasonableTotal(UsageProfileType type)
    {
        var profile = UsageProfiles.Get(type);

        var totalWeight = profile.Workloads.Sum(w => w.Weight);

        // Weights should sum to 100 for percentage-based profiles
        Assert.Equal(100, totalWeight);
    }

    [Fact]
    public void Profile_RecommendedFileSizesAreReasonable()
    {
        foreach (var profile in UsageProfiles.All)
        {
            // All profiles should have at least 1 GB recommended
            Assert.True(profile.RecommendedFileSize >= 1L * 1024 * 1024 * 1024,
                $"Profile {profile.Name} has too small recommended file size");

            // And not more than 16 GB
            Assert.True(profile.RecommendedFileSize <= 16L * 1024 * 1024 * 1024,
                $"Profile {profile.Name} has unexpectedly large recommended file size");
        }
    }

    [Fact]
    public void Profile_WorkloadsHaveValidBlockSizes()
    {
        foreach (var profile in UsageProfiles.All)
        {
            foreach (var workload in profile.Workloads)
            {
                // Block sizes should be power of 2 and at least 4KB
                Assert.True(workload.BlockSize >= 4096,
                    $"Profile {profile.Name}, workload {workload.Name} has too small block size");
                Assert.True(IsPowerOfTwo(workload.BlockSize),
                    $"Profile {profile.Name}, workload {workload.Name} block size is not power of 2");
            }
        }
    }

    [Fact]
    public void Profile_WorkloadsHaveValidQueueDepths()
    {
        foreach (var profile in UsageProfiles.All)
        {
            foreach (var workload in profile.Workloads)
            {
                Assert.True(workload.QueueDepth >= 1,
                    $"Profile {profile.Name}, workload {workload.Name} has invalid queue depth");
            }
        }
    }

    [Fact]
    public void DatabaseProfile_HasDurableLogWrites()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Database);

        var logWrites = profile.Workloads.First(w => w.Name == "Log Writes");

        Assert.True(logWrites.WriteThrough);
        Assert.True(logWrites.NoBuffering);
        Assert.Equal(100, logWrites.WritePercent);
    }

    [Fact]
    public void GamingProfile_HasDurableSaves()
    {
        var profile = UsageProfiles.Get(UsageProfileType.Gaming);

        var saves = profile.Workloads.First(w => w.Name == "Save/Checkpoint");

        Assert.True(saves.WriteThrough);
        Assert.Equal(100, saves.WritePercent);
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}
