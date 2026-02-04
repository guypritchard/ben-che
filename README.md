# DiskBench

A high-performance Windows storage benchmarking library targeting .NET 10 with clean separation between core benchmarking logic and renderers.

## Features

- **Accurate Measurements**: Uses Win32 overlapped I/O with IO Completion Ports (IOCP) for true async I/O
- **Device-Focused Testing**: Supports `FILE_FLAG_NO_BUFFERING` and `FILE_FLAG_WRITE_THROUGH` for raw device performance
- **Comprehensive Metrics**: Throughput (MB/s, IOPS), latency percentiles (p50/p90/p95/p99/p99.9/max), per-second time series
- **Statistical Rigor**: Trial aggregation with mean/stddev and optional 95% bootstrap confidence intervals
- **Zero-Allocation Hot Path**: No GC pressure during measured window for accurate results
- **Extensible Architecture**: Clean separation between core logic, metrics, platform-specific I/O, and renderers

## Project Structure

```
.
|-- DiskBench.Core/          # Core models, interfaces, benchmark runner
|-- DiskBench.Metrics/       # Low-overhead histogram and time series
|-- DiskBench.Win32/         # Windows IOCP-based I/O engine
|-- DiskBench.Cli/           # Command-line interface
`-- DiskBench.Tests/         # Unit tests and fake engine
```

## Quick Start

```bash
# Build the solution
dotnet build DiskBench.slnx

# Run a quick benchmark
dotnet run --project DiskBench.Cli -- quick --file testfile.dat --size 1G

# Run with specific options
dotnet run --project DiskBench.Cli -- run --file testfile.dat --size 4G --trials 5 --duration 60

# Get disk information
dotnet run --project DiskBench.Cli -- info --path C:\
```

## CLI Commands

### `run` - Run a benchmark

```bash
diskbench run [options]

Options:
  -p, --plan <file>      JSON benchmark plan file
  -f, --file <path>      Target file path [default: diskbench_test.dat]
  -s, --size <size>      Test file size (e.g., 1G, 512M) [default: 1G]
  -t, --trials <n>       Number of trials per workload [default: 3]
  -d, --duration <sec>   Measured duration in seconds [default: 30]
  -w, --warmup <sec>     Warmup duration in seconds [default: 5]
  -o, --output <file>    Output JSON file for results
  --buffered             Use buffered I/O (not recommended)
```

### `quick` - Quick benchmark with common workloads

Runs:
- Sequential Read/Write 1MB QD1
- Random Read/Write 4KB QD1
- Random Read/Write 4KB QD32

### `profile` - Run a usage profile benchmark

Run workloads that simulate real-world usage patterns:

```bash
diskbench profile [options]

Options:
  -p, --profile <name>   Profile to run (see table below)
  -f, --file <path>      Target file path [default: diskbench_test.dat]
  -s, --size <size>      Override file size (default: profile-specific)
  -t, --trials <n>       Number of trials per workload [default: 3]
  -d, --duration <sec>   Base measured duration in seconds [default: 30]
  -o, --output <file>    Output JSON file for results
```

#### Available Profiles

| Profile | Description | Recommended Size |
|---------|-------------|------------------|
| `gaming` | Asset loading, texture streaming, saves | 8 GB |
| `streaming` | Video streaming at various bitrates | 4 GB |
| `compiling` | Source reads, object writes, linking | 2 GB |
| `browsing` | Browser cache, downloads, database ops | 1 GB |
| `database` | OLTP workloads: page I/O, log writes | 4 GB |
| `vm` | Virtual machine disk access patterns | 16 GB |
| `fileserver` | Mixed file sizes, concurrent access | 4 GB |
| `media` | Photo/video editing workflows | 8 GB |
| `os` | Operating system disk access | 2 GB |
| `backup` | Large sequential reads and writes | 8 GB |

Example:
```bash
# Test how drive performs for gaming workloads
diskbench profile -p gaming -f test.dat

# Test database workload with custom file size
diskbench profile -p database -f test.dat -s 8G -d 60
```

### `profiles` - List available profiles

Shows all available usage profiles with descriptions:

```bash
diskbench profiles
```

### `info` - Display disk information

Shows sector sizes, file system type, and capacity information.

## Understanding the Results

### Buffered vs Unbuffered I/O

| Flag | Behavior | Use Case |
|------|----------|----------|
| `NoBuffering = true` | Bypasses OS file cache | Measuring actual device performance |
| `NoBuffering = false` | Uses OS caching | Testing application-level I/O patterns |

**Important**: Unbuffered I/O requires:
- Buffer addresses aligned to sector size
- I/O sizes that are multiples of sector size
- File offsets aligned to sector size

DiskBench handles alignment automatically when using unbuffered mode.

### Write-Through vs Flush

| Setting | Behavior | Performance Impact |
|---------|----------|-------------------|
| `WriteThrough = true` | Forces each write to media | Slower, but data is durable |
| `WriteThrough = false` | Allows write caching | Faster, but data may be in cache |
| `FlushPolicy.AtEnd` | Single flush at trial end | Minimal impact |
| `FlushPolicy.EveryIO` | Flush after each I/O | Severe impact (not recommended) |

### Why Warmup Matters

SSDs and storage controllers use various caching strategies:

1. **SLC Cache**: Many SSDs use faster SLC cells as a write cache. When exhausted, write speeds drop significantly.
2. **Controller Caching**: Storage controllers cache recent data in RAM.
3. **Read-Ahead**: Sequential patterns trigger prefetching.

Warmup allows these caches to reach steady state before measurement begins.

### File Size Guidelines

| RAM | Recommended Test File |
|-----|----------------------|
| 8 GB | ≥ 16 GB |
| 16 GB | ≥ 32 GB |
| 32 GB | ≥ 64 GB |

Using a file larger than RAM ensures you're measuring device performance, not page cache performance.

### Queue Depth (QD) Effects

| QD | Latency | Throughput | Use Case |
|----|---------|------------|----------|
| 1 | Lowest | Lower | Single-threaded application |
| 4-8 | Medium | Higher | Multi-threaded application |
| 32+ | Highest | Maximum | Database, high-load server |

Higher queue depths allow the device to optimize I/O ordering but increase latency.

## Programmatic Usage

```csharp
using DiskBench.Core;
using DiskBench.Win32;

// Create the benchmark plan
var plan = new BenchmarkPlan
{
    Workloads =
    [
        new WorkloadSpec
        {
            Name = "Random Read 4K",
            FilePath = "testfile.dat",
            FileSize = 1L * 1024 * 1024 * 1024, // 1 GB
            BlockSize = 4096,
            Pattern = AccessPattern.Random,
            WritePercent = 0,
            QueueDepth = 32,
            NoBuffering = true
        }
    ],
    Trials = 3,
    WarmupDuration = TimeSpan.FromSeconds(5),
    MeasuredDuration = TimeSpan.FromSeconds(30),
    CollectTimeSeries = true,
    ComputeConfidenceIntervals = true
};

// Create engine and runner
await using var engine = new WindowsIoEngine();
var runner = new BenchmarkRunner(engine);

// Run benchmark
var result = await runner.RunAsync(plan);

// Access results
foreach (var workload in result.Workloads)
{
    Console.WriteLine($"{workload.Workload.Name}:");
    Console.WriteLine($"  Throughput: {workload.MeanBytesPerSecond / 1e6:F2} MB/s");
    Console.WriteLine($"  IOPS: {workload.MeanIops:F0}");
    Console.WriteLine($"  p99 Latency: {workload.MeanLatency.P99Us:F1} µs");
}
```

### Using Usage Profiles Programmatically

```csharp
using DiskBench.Core;
using DiskBench.Win32;

// Get a predefined usage profile
var profile = UsageProfiles.Get(UsageProfileType.Gaming);

// Create a benchmark plan from the profile
var plan = UsageProfiles.CreatePlan(
    profile,
    filePath: "testfile.dat",
    fileSize: 8L * 1024 * 1024 * 1024, // 8 GB (optional, uses profile default)
    trials: 3,
    measuredDuration: TimeSpan.FromSeconds(60));

// Run the benchmark
await using var engine = new WindowsIoEngine();
var runner = new BenchmarkRunner(engine);
var result = await runner.RunAsync(plan);

// Or generate individual workloads for custom plans
var workloads = UsageProfiles.GenerateWorkloads(
    profile,
    filePath: "testfile.dat");

// List all available profiles
foreach (var p in UsageProfiles.All)
{
    Console.WriteLine($"{p.Name}: {p.Description}");
    Console.WriteLine($"  Workloads: {p.Workloads.Count}");
    foreach (var w in p.Workloads)
    {
        Console.WriteLine($"    - {w.Name} ({w.Weight}%)");
    }
}
```

## JSON Output Format

```json
{
  "plan": {
    "name": "Quick Benchmark",
    "trials": 3,
    "warmupDuration": "00:00:05",
    "measuredDuration": "00:00:30"
  },
  "workloads": [
    {
      "workload": {
        "name": "Random Read 4K Q32",
        "blockSize": 4096,
        "pattern": "Random",
        "queueDepth": 32
      },
      "trials": [...],
      "meanBytesPerSecond": 524288000,
      "stdDevBytesPerSecond": 10485760,
      "meanIops": 128000,
      "meanLatency": {
        "p50Us": 45.2,
        "p90Us": 89.1,
        "p95Us": 112.3,
        "p99Us": 245.6,
        "p999Us": 512.8
      },
      "throughputCI": [510000000, 538000000]
    }
  ],
  "systemInfo": {
    "osVersion": "Microsoft Windows NT 10.0.22631.0",
    "logicalProcessors": 16,
    "totalMemoryBytes": 34359738368
  }
}
```

## Architecture

### Core Abstractions

- **`BenchmarkPlan`**: Defines workloads, trials, and options
- **`WorkloadSpec`**: Specifies a single benchmark configuration
- **`IBenchmarkEngine`**: Platform-specific I/O implementation
- **`IBenchmarkSink`**: Event receiver for progress and results

### Metrics Collection

The `LatencyHistogram` uses a log2 bucketing scheme for efficient percentile calculation without storing individual samples:

- First 64 buckets: linear (0-63 ticks, ~nanosecond resolution)
- Remaining buckets: log2 with 8 sub-buckets each
- Covers nanosecond to hour-long latencies

### Zero-Allocation Design

The hot path (during measured window) avoids allocations by:
- Pre-allocating all buffers and I/O slots
- Using value types and fixed-size arrays
- Pre-computing random offsets
- Using `Stopwatch.GetTimestamp()` instead of `DateTime`

## Testing

```bash
# Run all tests
dotnet test DiskBench.Tests

# Run specific test class
dotnet test DiskBench.Tests --filter "FullyQualifiedName~LatencyHistogramTests"
```

The `FakeBenchmarkEngine` allows testing the runner and metrics without actual disk I/O.

## Performance Considerations

### For Accurate Results

1. **Close other applications** that might cause I/O
2. **Disable antivirus** scanning on the test file
3. **Use unbuffered I/O** (`NoBuffering = true`)
4. **Test file > RAM** to avoid cache effects
5. **Run multiple trials** for statistical confidence
6. **Allow warmup** for cache stabilization

### Known Limitations

- Windows-only (IOCP-based I/O engine)
- File-based benchmarks only (no raw device access)
- Requires .NET 10 or later

## License

MIT License
