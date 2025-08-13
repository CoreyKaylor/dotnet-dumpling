using System.CommandLine;
using System.CommandLine.Invocation;
using Dumpling.Core;
using Graphs;
using Spectre.Console;
using Spectre.Console.Rendering;
using Dumpling.CLI.Shared.Constants;
using Dumpling.CLI.Shared.Formatters;
using Dumpling.CLI.Shared.UI;

namespace Dumpling.CLI.Commands;

public class CompareCommand : Command
{
    public CompareCommand() : base("compare", "Compare multiple heap dump files to identify changes and memory growth")
    {
        var filesArgument = new Argument<string[]>(
            "files",
            "Paths to .gcdump files to compare (2 or more), or a directory containing dump files"
        )
        {
            Arity = ArgumentArity.OneOrMore
        };

        var interactiveOption = new Option<bool>(
            new[] { "--interactive", "-i" },
            getDefaultValue: () => false,
            description: "Launch interactive mode for exploring comparison results"
        );
        
        var selectFilesOption = new Option<bool>(
            new[] { "--select-files", "-sf" },
            getDefaultValue: () => false,
            description: "Launch interactive file selection when multiple files are found"
        );

        var formatOption = new Option<OutputFormat>(
            new[] { "--format", "-f" },
            getDefaultValue: () => OutputFormat.Table,
            description: "Output format"
        );

        var topTypesOption = new Option<int>(
            new[] { "--top-types", "-t" },
            getDefaultValue: () => UiConstants.DefaultTopTypes,
            description: "Number of top changed types to display"
        );

        var thresholdOption = new Option<double>(
            new[] { "--threshold", "-th" },
            getDefaultValue: () => 0.01,
            description: "Minimum change percentage to display (0.01 = 1%)"
        );

        var showAllOption = new Option<bool>(
            new[] { "--show-all", "-a" },
            getDefaultValue: () => false,
            description: "Show all types including unchanged ones"
        );

        var sortByOption = new Option<CompareSortBy>(
            new[] { "--sort-by", "-s" },
            getDefaultValue: () => CompareSortBy.RetainedSizeDelta,
            description: "Sort comparison results by"
        );

        AddArgument(filesArgument);
        AddOption(interactiveOption);
        AddOption(selectFilesOption);
        AddOption(formatOption);
        AddOption(topTypesOption);
        AddOption(thresholdOption);
        AddOption(showAllOption);
        AddOption(sortByOption);

        this.SetHandler(
            ExecuteAsync,
            filesArgument,
            interactiveOption,
            selectFilesOption,
            formatOption,
            topTypesOption,
            thresholdOption,
            showAllOption,
            sortByOption
        );
    }

    private async Task ExecuteAsync(
        string[] files,
        bool interactive,
        bool selectFiles,
        OutputFormat format,
        int topTypes,
        double threshold,
        bool showAll,
        CompareSortBy sortBy)
    {
        await Task.Run(() =>
        {
            var dumpFiles = ResolveFiles(files);
            
            if (dumpFiles.Count < 2)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] At least 2 dump files are required for comparison.");
                return;
            }

            if (selectFiles || (dumpFiles.Count > 2 && AnsiConsole.Profile.Capabilities.Interactive))
            {
                dumpFiles = RunInteractiveFileSelection(dumpFiles);
                if (dumpFiles.Count < 2)
                {
                    AnsiConsole.MarkupLine("[yellow]Comparison cancelled.[/]");
                    return;
                }
            }
            else if (dumpFiles.Count > 2 && !AnsiConsole.Profile.Capabilities.Interactive)
            {
                // In non-interactive mode with multiple files, just use all of them
                AnsiConsole.MarkupLine($"[yellow]Comparing {dumpFiles.Count} dump files (interactive selection not available in non-interactive terminal)[/]");
            }

            RunComparison(dumpFiles, format, topTypes, threshold, showAll, sortBy, interactive);
        });
    }

    private List<FileInfo> ResolveFiles(string[] paths)
    {
        var files = new List<FileInfo>();
        
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                // If it's a directory, get all .gcdump files
                var dir = new DirectoryInfo(path);
                files.AddRange(dir.GetFiles("*.gcdump", SearchOption.AllDirectories));
            }
            else if (File.Exists(path))
            {
                files.Add(new FileInfo(path));
            }
            else
            {
                // Try as a glob pattern
                var directory = Path.GetDirectoryName(path) ?? ".";
                var pattern = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(pattern) && Directory.Exists(directory))
                {
                    var dir = new DirectoryInfo(directory);
                    files.AddRange(dir.GetFiles(pattern));
                }
            }
        }

        // Sort by modification time (oldest first) for chronological analysis
        return files.OrderBy(f => f.LastWriteTime).ToList();
    }

    private List<FileInfo> RunInteractiveFileSelection(List<FileInfo> availableFiles)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold yellow]Select dump files to compare[/]");
        AnsiConsole.WriteLine();

        // Display available files with metadata
        var table = TableFactory.CreateFileSelectionTable();

        var now = DateTime.Now;
        for (int i = 0; i < availableFiles.Count; i++)
        {
            var file = availableFiles[i];
            var age = now - file.LastWriteTime;
            var ageStr = age.TotalDays > 1 ? $"{(int)age.TotalDays} days ago" : 
                         age.TotalHours > 1 ? $"{(int)age.TotalHours} hours ago" : 
                         $"{(int)age.TotalMinutes} minutes ago";
            
            table.AddRow(
                (i + 1).ToString(),
                Markup.Escape(file.Name),
                ByteFormatter.FormatBytes((ulong)file.Length),
                file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ageStr
            );
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Multi-selection prompt
        var choices = availableFiles.Select((f, i) => $"{i + 1}. {f.Name}").ToArray();
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select [green]2 or more files[/] to compare:")
                .Required()
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more files)[/]")
                .InstructionsText(
                    "[grey](Press [blue]<space>[/] to toggle a file, " +
                    "[green]<enter>[/] to accept)[/]")
                .AddChoices(choices));

        var selectedFiles = new List<FileInfo>();
        foreach (var choice in selected)
        {
            var index = int.Parse(choice.Split('.')[0]) - 1;
            selectedFiles.Add(availableFiles[index]);
        }

        return selectedFiles.OrderBy(f => f.LastWriteTime).ToList();
    }

    private void RunComparison(
        List<FileInfo> files, 
        OutputFormat format, 
        int topTypes,
        double threshold,
        bool showAll,
        CompareSortBy sortBy,
        bool launchInteractive = false)
    {
        if (format == OutputFormat.Table)
        {
            List<HeapSnapshot>? snapshots = null;
            ComparisonResult? comparisonResult = null;
            List<ComparisonResult>? trendResults = null;
            Exception? loadError = null;
            
            // Load and analyze within Status context
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start($"Comparing {files.Count} dump files...", ctx =>
                {
                    try
                    {
                        ctx.Status("Loading heap dumps...");
                        snapshots = LoadSnapshots(files);
                        
                        ctx.Status("Analyzing differences...");
                        var comparer = new HeapComparer();
                        
                        if (files.Count == 2)
                        {
                            comparisonResult = comparer.Compare(snapshots[0], snapshots[1]);
                        }
                        else
                        {
                            trendResults = comparer.CompareMultiple(snapshots);
                        }
                    }
                    catch (Exception ex)
                    {
                        loadError = ex;
                    }
                });
            
            // Handle any errors
            if (loadError != null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(loadError.Message)}");
                return;
            }
            
            // Now handle display and interactive mode outside of Status context
            if (files.Count == 2 && comparisonResult != null && snapshots != null)
            {
                // If launching directly into interactive mode, skip the table
                if (launchInteractive && AnsiConsole.Profile.Capabilities.Interactive)
                {
                    var interactiveMode = new ComparisonInteractiveMode(
                        snapshots[0], 
                        snapshots[1], 
                        files[0], 
                        files[1]);
                    interactiveMode.Run();
                }
                else
                {
                    DisplayComparisonTable(comparisonResult, files[0], files[1], topTypes, threshold, showAll, sortBy);
                    
                    // Ask if user wants to enter interactive mode
                    AnsiConsole.WriteLine();
                    if (AnsiConsole.Profile.Capabilities.Interactive)
                    {
                        var enterInteractive = AnsiConsole.Confirm("Would you like to explore the comparison interactively?");
                        if (enterInteractive)
                        {
                            var interactiveMode = new ComparisonInteractiveMode(
                                snapshots[0], 
                                snapshots[1], 
                                files[0], 
                                files[1]);
                            interactiveMode.Run();
                        }
                    }
                }
            }
            else if (trendResults != null)
            {
                DisplayTrendAnalysis(trendResults, files, topTypes, threshold, sortBy);
            }
        }
        else
        {
            try
            {
                var snapshots = LoadSnapshots(files);
                var comparer = new HeapComparer();
                
                if (files.Count == 2)
                {
                    var result = comparer.Compare(snapshots[0], snapshots[1]);
                    DisplayComparisonOutput(result, files[0], files[1], format, topTypes, threshold, showAll, sortBy);
                }
                else
                {
                    var results = comparer.CompareMultiple(snapshots);
                    DisplayTrendOutput(results, files, format, topTypes, threshold, sortBy);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }

    private List<HeapSnapshot> LoadSnapshots(List<FileInfo> files)
    {
        var snapshots = new List<HeapSnapshot>();
        foreach (var file in files)
        {
            var heapDump = new GCHeapDump(file.FullName);
            snapshots.Add(new HeapSnapshot(heapDump));
        }
        return snapshots;
    }

    private void DisplayComparisonTable(
        ComparisonResult result,
        FileInfo file1,
        FileInfo file2,
        int topTypes,
        double threshold,
        bool showAll,
        CompareSortBy sortBy)
    {
        // Display summary
        var panel = new Panel(
            new Markup($"[bold yellow]Heap Comparison Results[/]\n" +
                      $"Baseline: [cyan]{Markup.Escape(file1.Name)}[/] ({file1.LastWriteTime:yyyy-MM-dd HH:mm})\n" +
                      $"Current: [cyan]{Markup.Escape(file2.Name)}[/] ({file2.LastWriteTime:yyyy-MM-dd HH:mm})\n" +
                      $"Time Diff: [green]{(file2.LastWriteTime - file1.LastWriteTime).TotalHours:F1} hours[/]\n" +
                      $"\n" +
                      $"Total Objects: [green]{result.BaselineStats.TotalObjects:N0}[/] → [green]{result.CurrentStats.TotalObjects:N0}[/] " +
                      $"([{(result.ObjectCountDelta >= 0 ? "red" : "green")}]{result.ObjectCountDelta:+#,0;-#,0;0}[/])\n" +
                      $"Total Size: [green]{ByteFormatter.FormatBytes(result.BaselineStats.TotalSize)}[/] → [green]{ByteFormatter.FormatBytes(result.CurrentStats.TotalSize)}[/] " +
                      $"([{(result.TotalSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(result.TotalSizeDelta)}[/])\n" +
                      $"Retained Size: [green]{ByteFormatter.FormatBytes(result.BaselineStats.TotalRetainedSize)}[/] → [green]{ByteFormatter.FormatBytes(result.CurrentStats.TotalRetainedSize)}[/] " +
                      $"([{(result.RetainedSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(result.RetainedSizeDelta)}[/])")
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        AnsiConsole.Write(panel);

        // Display type changes
        StatusDisplay.WriteLine();
        StatusDisplay.ShowSection("Type Changes");
        
        var table = TableFactory.CreateComparisonTable();

        var deltas = GetSortedDeltas(result, sortBy, showAll, threshold).Take(topTypes);

        foreach (var delta in deltas)
        {
            var growthPercent = delta.BaselineRetainedSize > 0 
                ? (double)delta.RetainedSizeDelta / delta.BaselineRetainedSize * 100 
                : delta.RetainedSizeDelta > 0 ? 100.0 : 0.0;

            var status = delta.Status == TypeChangeStatus.Added ? "[green]NEW[/]" :
                        delta.Status == TypeChangeStatus.Removed ? "[red]REMOVED[/]" :
                        delta.CountDelta > 0 ? "[yellow]↑[/]" :
                        delta.CountDelta < 0 ? "[cyan]↓[/]" : "[dim]-[/]";

            var displayName = delta.TypeName.Length > 50 ? delta.TypeName.Substring(0, 47) + "..." : delta.TypeName;

            table.AddRow(
                Markup.Escape(displayName),
                CountFormatter.FormatCountDelta(delta.CountDelta),
                ByteFormatter.FormatBytesDelta(delta.TotalSizeDelta),
                ByteFormatter.FormatBytesDelta(delta.RetainedSizeDelta),
                CountFormatter.FormatPercentage(growthPercent),
                status
            );
        }

        AnsiConsole.Write(table);

        // Show summary statistics
        if (result.NewTypes.Any() || result.RemovedTypes.Any())
        {
            AnsiConsole.WriteLine();
            if (result.NewTypes.Any())
            {
                AnsiConsole.MarkupLine($"[green]New types: {result.NewTypes.Count}[/]");
            }
            if (result.RemovedTypes.Any())
            {
                AnsiConsole.MarkupLine($"[red]Removed types: {result.RemovedTypes.Count}[/]");
            }
        }
    }

    private void DisplayTrendAnalysis(
        List<ComparisonResult> results,
        List<FileInfo> files,
        int topTypes,
        double threshold,
        CompareSortBy sortBy)
    {
        // Display timeline header
        var panel = new Panel(
            new Markup($"[bold yellow]Heap Trend Analysis[/]\n" +
                      $"Files analyzed: [cyan]{files.Count}[/]\n" +
                      $"Time span: [green]{(files.Last().LastWriteTime - files.First().LastWriteTime).TotalHours:F1} hours[/]")
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        AnsiConsole.Write(panel);

        // Create timeline chart
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Memory Growth Timeline:[/]");
        
        var chart = new BarChart()
            .Width(60)
            .Label("[green bold]Retained Size (MB)[/]");
            
        for (int i = 0; i < files.Count; i++)
        {
            var snapshot = i == 0 ? results[0].BaselineStats : results[i - 1].CurrentStats;
            var sizeMB = snapshot.TotalRetainedSize / (1024.0 * 1024.0);
            chart.AddItem(files[i].Name.Replace(".gcdump", ""), sizeMB, Color.Green);
        }
        
        AnsiConsole.Write(chart);

        // Identify consistent growth patterns
        var growthPatterns = IdentifyGrowthPatterns(results);
        
        if (growthPatterns.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold red]Potential Memory Leaks (Consistent Growth):[/]");
            
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.ShowRowSeparators = true;
            table.Expand = false;
            table.AddColumn("Type");
            table.AddColumn(new TableColumn("Total Growth").RightAligned());
            table.AddColumn(new TableColumn("Avg Growth/Step").RightAligned());
            table.AddColumn(new TableColumn("Growth Rate").RightAligned());

            foreach (var pattern in growthPatterns.Take(topTypes))
            {
                var displayName = pattern.TypeName.Length > 50 ? 
                    pattern.TypeName.Substring(0, 47) + "..." : pattern.TypeName;
                    
                table.AddRow(
                    Markup.Escape(displayName),
                    ByteFormatter.FormatBytesDelta(pattern.TotalGrowth),
                    ByteFormatter.FormatBytes(pattern.AverageGrowthPerStep),
                    $"{pattern.GrowthRate:F1}% per step"
                );
            }
            
            AnsiConsole.Write(table);
        }
    }

    private void DisplayComparisonOutput(
        ComparisonResult result,
        FileInfo file1,
        FileInfo file2,
        OutputFormat format,
        int topTypes,
        double threshold,
        bool showAll,
        CompareSortBy sortBy)
    {
        var deltas = GetSortedDeltas(result, sortBy, showAll, threshold).Take(topTypes).ToList();

        if (format == OutputFormat.Json)
        {
            var output = new
            {
                Comparison = new
                {
                    Baseline = new
                    {
                        File = file1.Name,
                        Timestamp = file1.LastWriteTime,
                        TotalObjects = result.BaselineStats.TotalObjects,
                        TotalSize = result.BaselineStats.TotalSize,
                        RetainedSize = result.BaselineStats.TotalRetainedSize
                    },
                    Current = new
                    {
                        File = file2.Name,
                        Timestamp = file2.LastWriteTime,
                        TotalObjects = result.CurrentStats.TotalObjects,
                        TotalSize = result.CurrentStats.TotalSize,
                        RetainedSize = result.CurrentStats.TotalRetainedSize
                    },
                    Deltas = new
                    {
                        ObjectCount = result.ObjectCountDelta,
                        TotalSize = result.TotalSizeDelta,
                        RetainedSize = result.RetainedSizeDelta
                    }
                },
                TypeChanges = deltas.Select(d => new
                {
                    TypeName = d.TypeName,
                    Status = d.Status.ToString(),
                    CountDelta = d.CountDelta,
                    TotalSizeDelta = d.TotalSizeDelta,
                    RetainedSizeDelta = d.RetainedSizeDelta,
                    GrowthPercent = d.BaselineRetainedSize > 0 
                        ? (double)d.RetainedSizeDelta / d.BaselineRetainedSize * 100 
                        : d.RetainedSizeDelta > 0 ? 100.0 : 0.0
                }),
                NewTypes = result.NewTypes,
                RemovedTypes = result.RemovedTypes
            };

            var json = System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            Console.WriteLine(json);
        }
        else if (format == OutputFormat.Csv)
        {
            Console.WriteLine("TypeName,Status,CountDelta,TotalSizeDelta,RetainedSizeDelta,GrowthPercent");
            foreach (var delta in deltas)
            {
                var growthPercent = delta.BaselineRetainedSize > 0 
                    ? (double)delta.RetainedSizeDelta / delta.BaselineRetainedSize * 100 
                    : delta.RetainedSizeDelta > 0 ? 100.0 : 0.0;
                    
                Console.WriteLine($"\"{delta.TypeName}\",{delta.Status},{delta.CountDelta}," +
                    $"{delta.TotalSizeDelta},{delta.RetainedSizeDelta},{growthPercent:F2}");
            }
        }
    }

    private void DisplayTrendOutput(
        List<ComparisonResult> results,
        List<FileInfo> files,
        OutputFormat format,
        int topTypes,
        double threshold,
        CompareSortBy sortBy)
    {
        var growthPatterns = IdentifyGrowthPatterns(results).Take(topTypes).ToList();

        if (format == OutputFormat.Json)
        {
            var snapshots = new List<object>();
            snapshots.Add(new
            {
                File = files[0].Name,
                Timestamp = files[0].LastWriteTime,
                TotalObjects = results[0].BaselineStats.TotalObjects,
                TotalSize = results[0].BaselineStats.TotalSize,
                RetainedSize = results[0].BaselineStats.TotalRetainedSize
            });

            for (int i = 0; i < results.Count; i++)
            {
                snapshots.Add(new
                {
                    File = files[i + 1].Name,
                    Timestamp = files[i + 1].LastWriteTime,
                    TotalObjects = results[i].CurrentStats.TotalObjects,
                    TotalSize = results[i].CurrentStats.TotalSize,
                    RetainedSize = results[i].CurrentStats.TotalRetainedSize
                });
            }

            var output = new
            {
                TrendAnalysis = new
                {
                    FileCount = files.Count,
                    TimeSpanHours = (files.Last().LastWriteTime - files.First().LastWriteTime).TotalHours,
                    Snapshots = snapshots,
                    GrowthPatterns = growthPatterns.Select(p => new
                    {
                        TypeName = p.TypeName,
                        TotalGrowth = p.TotalGrowth,
                        AverageGrowthPerStep = p.AverageGrowthPerStep,
                        GrowthRate = p.GrowthRate
                    })
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            Console.WriteLine(json);
        }
        else if (format == OutputFormat.Csv)
        {
            Console.WriteLine("TypeName,TotalGrowth,AverageGrowthPerStep,GrowthRatePercent");
            foreach (var pattern in growthPatterns)
            {
                Console.WriteLine($"\"{pattern.TypeName}\",{pattern.TotalGrowth}," +
                    $"{pattern.AverageGrowthPerStep},{pattern.GrowthRate:F2}");
            }
        }
    }

    private IEnumerable<TypeDelta> GetSortedDeltas(
        ComparisonResult result, 
        CompareSortBy sortBy,
        bool showAll,
        double threshold)
    {
        var deltas = result.TypeDeltas.AsEnumerable();

        // Filter by threshold unless showing all
        if (!showAll)
        {
            deltas = deltas.Where(d => 
                Math.Abs((double)d.RetainedSizeDelta / (d.BaselineRetainedSize > 0 ? d.BaselineRetainedSize : 1)) >= threshold ||
                d.Status == TypeChangeStatus.Added ||
                d.Status == TypeChangeStatus.Removed);
        }

        // Sort by specified criteria
        return sortBy switch
        {
            CompareSortBy.RetainedSizeDelta => deltas.OrderByDescending(d => Math.Abs(d.RetainedSizeDelta)),
            CompareSortBy.CountDelta => deltas.OrderByDescending(d => Math.Abs(d.CountDelta)),
            CompareSortBy.TotalSizeDelta => deltas.OrderByDescending(d => Math.Abs(d.TotalSizeDelta)),
            CompareSortBy.GrowthPercent => deltas.OrderByDescending(d => 
                d.BaselineRetainedSize > 0 ? Math.Abs((double)d.RetainedSizeDelta / d.BaselineRetainedSize) : 0),
            _ => deltas
        };
    }

    private List<GrowthPattern> IdentifyGrowthPatterns(List<ComparisonResult> results)
    {
        var patterns = new Dictionary<string, GrowthPattern>();

        foreach (var result in results)
        {
            foreach (var delta in result.TypeDeltas.Where(d => d.RetainedSizeDelta > 0))
            {
                if (!patterns.TryGetValue(delta.TypeName, out var pattern))
                {
                    pattern = new GrowthPattern { TypeName = delta.TypeName };
                    patterns[delta.TypeName] = pattern;
                }

                pattern.TotalGrowth += delta.RetainedSizeDelta;
                pattern.GrowthSteps++;
            }
        }

        // Calculate averages and rates
        foreach (var pattern in patterns.Values)
        {
            pattern.AverageGrowthPerStep = (ulong)(pattern.TotalGrowth / pattern.GrowthSteps);
            pattern.GrowthRate = (double)pattern.TotalGrowth / pattern.GrowthSteps / pattern.TotalGrowth * 100;
        }

        // Return patterns that show consistent growth (appeared in most comparisons)
        return patterns.Values
            .Where(p => p.GrowthSteps >= results.Count * 0.6) // Present in at least 60% of comparisons
            .OrderByDescending(p => p.TotalGrowth)
            .ToList();
    }

    private class GrowthPattern
    {
        public string TypeName { get; set; } = string.Empty;
        public long TotalGrowth { get; set; }
        public int GrowthSteps { get; set; }
        public ulong AverageGrowthPerStep { get; set; }
        public double GrowthRate { get; set; }
    }

}

public enum CompareSortBy
{
    RetainedSizeDelta,
    CountDelta,
    TotalSizeDelta,
    GrowthPercent
}