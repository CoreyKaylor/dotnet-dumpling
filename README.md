# 🥟 Dumpling

A delightful CLI tool for analyzing .NET memory dumps.

## Overview

Dumpling is a cross-platform .NET global tool that helps developers analyze heap dumps (`.gcdump` files) from .NET applications. It provides both quick command-line analysis and an interactive mode for exploring memory usage patterns, finding memory leaks, and understanding object retention.

## Features

- **Type Analysis**: Group objects by type with counts, sizes, and retained memory
- **Retained Size Calculations**: Understand true memory impact using dominator tree analysis
- **Instance Inspection**: View sample instances with addresses and individual retained sizes
- **Retainer Analysis**: Trace reference paths to understand why objects stay in memory
- **Multiple Output Formats**: Table (default), JSON, or CSV for easy integration
- **Interactive Mode**: Explore heap data with a rich terminal UI
- **Heap Comparison**: Compare multiple heap dumps to identify memory growth and changes over time
- **Reference Paths**: Find GC roots keeping objects alive
- **Fast & Efficient**: Optimized algorithms for analyzing large heap dumps

## Installation

```bash
# Not yet available - coming soon!
dotnet tool install -g dotnet-dumpling
```

## Usage

### Basic Analysis

Analyze a heap dump and display the top 20 types by retained size:

```bash
dumpling analyze heap.gcdump
```

Show more or fewer types:

```bash
dumpling analyze heap.gcdump --top-types 50
```

### Output Formats

Export results as JSON for further processing:

```bash
dumpling analyze heap.gcdump --format json > analysis.json
```

Export as CSV for Excel analysis:

```bash
dumpling analyze heap.gcdump --format csv > analysis.csv
```

### Interactive Mode

Launch the interactive terminal UI to explore the heap:

```bash
dumpling analyze heap.gcdump --interactive
```

In interactive mode, you can:
- Navigate through types and instances
- Search and filter objects
- Drill down into retainers
- Export selected data

### Heap Comparison

Compare multiple heap dumps to identify memory growth and changes:

```bash
dumpling compare before.gcdump after.gcdump
```

Show only types with significant growth:

```bash
dumpling compare before.gcdump after.gcdump --threshold 0.05  # 5% minimum change
```

Interactive comparison exploration:

```bash
dumpling compare before.gcdump after.gcdump --interactive
```

### Commands

#### `analyze`
Main analysis command for heap dumps.

Options:
- `--top-types, -t <number>`: Number of top types to display (default: 20)
- `--format, -f <format>`: Output format: Table, Json, or Csv (default: Table)
- `--interactive, -i`: Launch interactive mode
- `--show-instances, -si`: Show sample instances for each type
- `--show-retainers, -sr`: Show retainer paths for instances (implies --show-instances)
- `--max-instances, -mi <number>`: Maximum instances to show per type (default: 3)

#### `compare`
Compare multiple heap dump files to identify changes and memory growth.

Options:
- `--interactive, -i`: Launch interactive mode for exploring comparison results
- `--select-files, -sf`: Launch interactive file selection when multiple files are found
- `--format, -f <format>`: Output format: Table, Json, or Csv (default: Table)
- `--top-types, -t <number>`: Number of top changed types to display (default: 20)
- `--threshold, -th <threshold>`: Minimum change percentage to display (default: 0.01 = 1%)
- `--show-all, -a`: Show all types including unchanged ones
- `--sort-by, -s <field>`: Sort by CountDelta, GrowthPercent, RetainedSizeDelta, or TotalSizeDelta

## Creating Heap Dumps

### From a Running .NET Application

Use `dotnet-gcdump` tool to capture heap dumps:

```bash
# Install dotnet-gcdump
dotnet tool install -g dotnet-gcdump

# List running .NET processes
dotnet-gcdump ps

# Create a heap dump
dotnet-gcdump collect -p <process-id>
```

### From Visual Studio

1. Debug → Windows → Diagnostic Tools
2. Take Snapshot
3. Export as .gcdump file

### From PerfView

1. Collect → Collect → Heap Snapshot
2. Save as .gcdump file

## Understanding the Output

### Type Statistics Table

```
┌─────────────────────────┬────────┬──────────────┬────────────────┬──────────┐
│ Type                    │  Count │  Total Size  │ Retained Size  │ % of Heap│
├─────────────────────────┼────────┼──────────────┼────────────────┼──────────┤
│ System.String           │ 10,234 │   2.45 MB    │    15.67 MB    │  23.45%  │
│ System.Byte[]           │  1,523 │   5.12 MB    │    12.34 MB    │  18.47%  │
│ MyApp.CustomerData      │    856 │   1.23 MB    │     8.91 MB    │  13.34%  │
└─────────────────────────┴────────┴──────────────┴────────────────┴──────────┘
```

- **Type**: The .NET type name
- **Count**: Number of instances
- **Total Size**: Direct memory used by all instances
- **Retained Size**: Total memory that would be freed if all instances were collected
- **% of Heap**: Percentage of total retained heap size

### Key Concepts

**Retained Size**: The amount of memory that would be freed if an object and all objects it exclusively references were garbage collected. This is often more important than the object's own size for understanding memory impact.

**Dominator Tree**: Used to calculate retained sizes. An object X dominates object Y if every path from the root to Y goes through X.

## Examples

### Finding Memory Leaks

Look for types with unexpectedly high retained sizes:

```bash
dumpling analyze app.gcdump --top-types 50
```

Types with high retained size relative to their expected usage often indicate memory leaks.

### Understanding Object Retention

See why objects are staying in memory:

```bash
# Show instances and their retainer paths
dumpling analyze app.gcdump --show-retainers

# Get detailed JSON output for analysis
dumpling analyze app.gcdump --show-retainers --format json > retention.json
```

### Analyzing Production Dumps

For large production dumps, export to JSON for detailed analysis:

```bash
dumpling analyze prod.gcdump --format json | jq '.Types[] | select(.RetainedSize > 10000000)'
```

### Comparing Snapshots

Compare multiple heap dumps to track memory changes over time:

```bash
# Basic comparison showing memory growth
dumpling compare before.gcdump after.gcdump

# Focus on significant changes only
dumpling compare dump1.gcdump dump2.gcdump dump3.gcdump --threshold 0.1

# Export comparison results for analysis
dumpling compare before.gcdump after.gcdump --format json > comparison.json

# Interactive exploration of changes
dumpling compare before.gcdump after.gcdump --interactive
```

### Investigating Specific Types

Focus on problematic types with instance details:

```bash
# Show 5 instances of each top type
dumpling analyze app.gcdump --top-types 10 --show-instances --max-instances 5
```

## Requirements

- .NET 8.0 or later
- Windows, macOS, or Linux

## License

Apache License 2.0 - see LICENSE file for details.

## Acknowledgments

Dumpling's heap analysis algorithms are inspired by the excellent work in:
- [PerfView](https://github.com/microsoft/perfview)
- [dotnet-heapview](https://github.com/1hub/dotnet-heapview)
- Microsoft.Diagnostics.Tracing.TraceEvent

## Why "Dumpling"?

Because we're analyzing *dump* files, and dumplings are delightful! 🥟 