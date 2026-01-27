# Disk Benchmark Tool

A high-performance disk benchmarking library and test harness for .NET 10, designed to measure sequential read/write performance with various block sizes.

## Features

- **Sequential Read/Write Tests**: Measure throughput for sequential I/O operations
- **Multiple Block Sizes**: Test with small (4 KB), medium (64 KB), and large (1 MB) blocks
- **Cross-Platform**: Works on Windows, Linux, and macOS
- **Network Drive Support**: Benchmark local and remote network shares
- **Multiple Output Formats**: Text, JSON, and CSV reports
- **Progress Reporting**: Real-time progress events during benchmarking

## Projects

| Project | Description |
|---------|-------------|
| `DiskBenchmark.Core` | Core library with benchmark engine and models |
| `DiskBenchmark.Tests` | xUnit test suite |
| `DiskBenchmark.Harness` | Interactive console test harness |

## Quick Start

### Using the Test Harness

```powershell
# Interactive mode - select a drive
dotnet run --project DiskBenchmark.Harness

# Benchmark a specific path
dotnet run --project DiskBenchmark.Harness -- C:\

# Quick benchmark (64 MB, 1 iteration)
dotnet run --project DiskBenchmark.Harness -- D:\ --quick

# Custom configuration
dotnet run --project DiskBenchmark.Harness -- E:\ --size=512 --iterations=5 --no-small

# Auto-save results to desktop
dotnet run --project DiskBenchmark.Harness -- D:\ --save

# Save results to specific location
dotnet run --project DiskBenchmark.Harness -- D:\ --output=C:\Results\benchmark
```

### Command Line Options

| Option | Description |
|--------|-------------|
| `--size=<MB>` | Test file size in megabytes (default: 256) |
| `--iterations=<N>` | Number of iterations to average (default: 3) |
| `--quick` | Quick mode: 64 MB, 1 iteration |
| `--no-small` | Skip small block (4 KB) tests |
| `--no-medium` | Skip medium block (64 KB) tests |
| `--no-large` | Skip large block (1 MB) tests |
| `--save` | Save results to desktop (JSON, CSV, TXT) |
| `--no-save` | Skip save prompt, don't save results |
| `--output=<path>` | Save results to specified path (implies --save) |

### Using the Library

```csharp
using DiskBenchmark.Core;
using DiskBenchmark.Core.Models;

var engine = new DiskBenchmarkEngine();

// Subscribe to progress updates
engine.ProgressChanged += (sender, e) => 
    Console.WriteLine($"[{e.PercentComplete}%] {e.OperationDescription}");

// Configure benchmark
var options = new BenchmarkOptions
{
    TargetPath = @"D:\",
    TestFileSizeBytes = 256L * 1024 * 1024, // 256 MB
    Iterations = 3,
    RunSmallBlocks = true,
    RunMediumBlocks = true,
    RunLargeBlocks = true
};

// Run benchmark
var report = await engine.RunBenchmarkAsync(options);

// Display results
Console.WriteLine(BenchmarkReportFormatter.FormatAsText(report));
```

## Sample Output

```
╔══════════════════════════════════════════════════════════════════╗
║                    DISK BENCHMARK REPORT                         ║
╠══════════════════════════════════════════════════════════════════╣
║ Target:      D:\                                                 ║
║ Volume:      Data                                                ║
║ Format:      NTFS                                                ║
║ Total Size:  500.00 GB                                           ║
║ Free Space:  250.00 GB                                           ║
║ Drive Type:  Local                                               ║
╠══════════════════════════════════════════════════════════════════╣
║ Started:     2026-01-26 10:30:00                                 ║
║ Duration:    45.3 seconds                                        ║
╠══════════════════════════════════════════════════════════════════╣
║                          RESULTS                                 ║
╠══════════════════════════════════════════════════════════════════╣
║ Operation          │ Block Size │ Throughput │  IOPS   │ Latency ║
╟────────────────────┼────────────┼────────────┼─────────┼─────────╢
║ Sequential Write   │       4 KB │   120.5 MB/s │   30720 │   33 μs ║
║ Sequential Read    │       4 KB │   180.2 MB/s │   46131 │   22 μs ║
║ Sequential Write   │      64 KB │   450.0 MB/s │    7200 │  139 μs ║
║ Sequential Read    │      64 KB │   520.3 MB/s │    8325 │  120 μs ║
║ Sequential Write   │       1 MB │   550.0 MB/s │     550 │ 1818 μs ║
║ Sequential Read    │       1 MB │   600.5 MB/s │     601 │ 1665 μs ║
╚══════════════════════════════════════════════════════════════════╝
```

## Future Roadmap

- [ ] Random I/O tests
- [ ] Windows Explorer context menu integration
- [ ] GUI application
- [ ] Historical results comparison
- [ ] S.M.A.R.T. data integration

## Building

```powershell
# Build all projects
dotnet build

# Run tests
dotnet test

# Run the harness
dotnet run --project DiskBenchmark.Harness
```

## Requirements

- .NET 10.0 SDK or later
- Windows 10/11 (for network share and P/Invoke features)

## License

MIT License
