using System.CommandLine;
using Dumpling.Core;
using Graphs;
using Spectre.Console;
using Dumpling.CLI.Shared.Constants;
using Dumpling.CLI.Shared.Formatters;
using Dumpling.CLI.Shared.UI;

namespace Dumpling.CLI.Commands;

public class AnalyzeCommand : Command
{
    public AnalyzeCommand() : base("analyze", "Analyze a heap dump file")
    {
        var fileArgument = new Argument<FileInfo>(
            "file",
            "Path to the .gcdump file to analyze"
        ).ExistingOnly();

        var topTypesOption = new Option<int>(
            new[] { "--top-types", "-t" },
            getDefaultValue: () => UiConstants.DefaultTopTypes,
            description: "Number of top types to display"
        );

        var interactiveOption = new Option<bool>(
            new[] { "--interactive", "-i" },
            getDefaultValue: () => false,
            description: "Launch interactive mode"
        );

        var formatOption = new Option<OutputFormat>(
            new[] { "--format", "-f" },
            getDefaultValue: () => OutputFormat.Table,
            description: "Output format"
        );

        var showInstancesOption = new Option<bool>(
            new[] { "--show-instances", "-si" },
            getDefaultValue: () => false,
            description: "Show sample instances for each type"
        );

        var showRetainersOption = new Option<bool>(
            new[] { "--show-retainers", "-sr" },
            getDefaultValue: () => false,
            description: "Show retainer paths for top instances (implies --show-instances)"
        );

        var maxInstancesOption = new Option<int>(
            new[] { "--max-instances", "-mi" },
            getDefaultValue: () => UiConstants.DefaultMaxInstances,
            description: "Maximum instances to show per type when --show-instances is used"
        );

        AddArgument(fileArgument);
        AddOption(topTypesOption);
        AddOption(interactiveOption);
        AddOption(formatOption);
        AddOption(showInstancesOption);
        AddOption(showRetainersOption);
        AddOption(maxInstancesOption);

        this.SetHandler(
            ExecuteAsync,
            fileArgument,
            topTypesOption,
            interactiveOption,
            formatOption,
            showInstancesOption,
            showRetainersOption,
            maxInstancesOption
        );
    }

    private async Task ExecuteAsync(
        FileInfo file,
        int topTypes,
        bool interactive,
        OutputFormat format,
        bool showInstances,
        bool showRetainers,
        int maxInstances)
    {
        await Task.Run(() =>
        {
            if (interactive)
            {
                RunInteractiveMode(file);
            }
            else
            {
                // If showing retainers, we need to show instances too
                if (showRetainers)
                    showInstances = true;
                    
                RunAnalysis(file, topTypes, format, showInstances, showRetainers, maxInstances);
            }
        });
    }

    private void RunAnalysis(FileInfo file, int topTypes, OutputFormat format, bool showInstances, bool showRetainers, int maxInstances)
    {
        if (format == OutputFormat.Table)
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start($"Loading [yellow]{file.Name}[/]...", ctx =>
                {
                    try
                    {
                        // Load the heap dump
                        ctx.Status($"Loading heap dump from [yellow]{file.Name}[/]...");
                        var heapDump = new GCHeapDump(file.FullName);
                        var snapshot = new HeapSnapshot(heapDump);
                    
                        ctx.Status("Analyzing heap...");
                        var analyzer = new HeapAnalyzer(snapshot);
                        
                        // Get statistics
                        var heapStats = analyzer.GetHeapStatistics();
                        var typeStats = analyzer.GetTypeStatistics(topTypes);

                        DisplayTableOutput(heapStats, typeStats, file, snapshot, analyzer, showInstances, showRetainers, maxInstances);
                    }
                    catch (Exception ex)
                    {
                        StatusDisplay.ShowError(ex.Message);
                        if (ex.Message.Contains("Not a understood file format"))
                        {
                            StatusDisplay.ShowInfo("Please provide a valid .gcdump file.");
                        }
                    }
                });
        }
        else
        {
            try
            {
                // For non-table formats, don't show status spinner
                var heapDump = new GCHeapDump(file.FullName);
                var snapshot = new HeapSnapshot(heapDump);
                var analyzer = new HeapAnalyzer(snapshot);
                
                // Get statistics
                var heapStats = analyzer.GetHeapStatistics();
                var typeStats = analyzer.GetTypeStatistics(topTypes);

                // Display results based on format
                switch (format)
                {
                    case OutputFormat.Json:
                        DisplayJsonOutput(heapStats, typeStats, snapshot, analyzer, showInstances, showRetainers, maxInstances);
                        break;
                    case OutputFormat.Csv:
                        DisplayCsvOutput(typeStats, snapshot, showInstances, maxInstances);
                        break;
                }
            }
            catch (Exception ex)
            {
                // For non-table formats, output error to stderr
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.Message.Contains("Not a understood file format"))
                {
                    Console.Error.WriteLine("Please provide a valid .gcdump file.");
                }
                Environment.Exit(1);
            }
        }
    }

    private void DisplayTableOutput(HeapStatistics heapStats, List<TypeStatistics> typeStats, FileInfo file, 
        HeapSnapshot? snapshot = null, HeapAnalyzer? analyzer = null, 
        bool showInstances = false, bool showRetainers = false, int maxInstances = 3)
    {
        // Display header
        var panel = PanelFactory.CreateHeapAnalysisPanel(file.Name, heapStats.TotalObjects, heapStats.TotalSize, heapStats.TotalRetainedSize);
        AnsiConsole.Write(panel);

        // Display counters if available
        if (heapStats.Counters != null && heapStats.Counters.Count > 0)
        {
            StatusDisplay.WriteLine();
            StatusDisplay.ShowSection("Performance Counters");
            var counterTable = TableFactory.CreateCounterTable();

            foreach (var counter in heapStats.Counters.OrderBy(c => c.Key))
            {
                counterTable.AddRow(counter.Key, counter.Value.ToString("N2"));
            }
            AnsiConsole.Write(counterTable);
        }

        // Display type statistics
        StatusDisplay.WriteLine();
        StatusDisplay.ShowSection("Top Types by Retained Size");
        
        var table = TableFactory.CreateTypeStatisticsTable();

        foreach (var type in typeStats)
        {
            var percentage = (double)type.RetainedSize / heapStats.TotalRetainedSize * 100;
            var displayName = analyzer?.TypeNameFormatter?.FormatTypeName(type.TypeName) ?? type.TypeName;
            table.AddRow(
                Markup.Escape(displayName.Length > 60 ? displayName.Substring(0, 57) + "..." : displayName),
                type.Count.ToString("N0"),
                ByteFormatter.FormatBytes(type.TotalSize),
                ByteFormatter.FormatBytes(type.RetainedSize),
                $"{percentage:F2}%"
            );
        }

        AnsiConsole.Write(table);

        // Show instances and retainers if requested
        if (showInstances && snapshot != null && analyzer != null)
        {
            StatusDisplay.WriteLine();
            StatusDisplay.ShowSection("Sample Instances");
            
            foreach (var type in typeStats.Take(Math.Min(3, typeStats.Count)))
            {
                AnsiConsole.WriteLine();
                var displayName = analyzer?.TypeNameFormatter?.FormatTypeName(type.TypeName) ?? type.TypeName;
                AnsiConsole.MarkupLine($"[yellow]Type: {Markup.Escape(displayName)}[/]");
                
                var sampleInstances = type.Instances.Take(maxInstances).ToList();
                if (sampleInstances.Any())
                {
                    var instanceTable = TableFactory.CreateInstanceTable();
                    
                    var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
                    
                    foreach (var nodeIndex in sampleInstances)
                    {
                        var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
                        var address = snapshot.MemoryGraph.GetAddress(nodeIndex);
                        var retainedSize = snapshot.GetRetainedSize(nodeIndex);
                        
                        instanceTable.AddRow(
                            $"#{sampleInstances.IndexOf(nodeIndex) + 1}",
                            $"0x{address:X}",
                            ByteFormatter.FormatBytes((ulong)node.Size),
                            ByteFormatter.FormatBytes(retainedSize)
                        );
                        
                        if (showRetainers)
                        {
                            // Show reference paths for the first instance
                            if (sampleInstances.IndexOf(nodeIndex) == 0)
                            {
                                if (analyzer != null)
                                {
                                    var paths = analyzer.FindReferencePaths(nodeIndex, maxPaths: 2);
                                    if (paths.Any())
                                    {
                                        AnsiConsole.MarkupLine($"  [dim]Reference paths:[/]");
                                        foreach (var path in paths)
                                        {
                                            // Split path and show with indentation
                                            var segments = path.Split(" ← ");
                                            for (int i = 0; i < segments.Length; i++)
                                            {
                                                if (i == 0)
                                                    AnsiConsole.MarkupLine($"    [dim cyan]{Markup.Escape(segments[i])}[/]");
                                                else
                                                    AnsiConsole.MarkupLine($"      [dim cyan]← {Markup.Escape(segments[i])}[/]");
                                            }
                                            if (paths.IndexOf(path) < paths.Count - 1)
                                                AnsiConsole.WriteLine();  // Blank line between paths
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    AnsiConsole.Write(instanceTable);
                }
            }
        }
    }

    private void DisplayJsonOutput(HeapStatistics heapStats, List<TypeStatistics> typeStats,
        HeapSnapshot? snapshot = null, HeapAnalyzer? analyzer = null,
        bool showInstances = false, bool showRetainers = false, int maxInstances = 3)
    {
        object result;
        
        if (showInstances && snapshot != null && analyzer != null)
        {
            var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
            
            result = new
            {
                Summary = new
                {
                    heapStats.TotalObjects,
                    heapStats.TotalSize,
                    heapStats.TotalRetainedSize,
                    heapStats.Counters
                },
                Types = typeStats.Select(t => new
                {
                    t.TypeName,
                    t.Count,
                    t.TotalSize,
                    t.RetainedSize,
                    Instances = showInstances ? t.Instances.Take(maxInstances).Select(nodeIndex =>
                    {
                        var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
                        var address = snapshot.MemoryGraph.GetAddress(nodeIndex);
                        var retainedSize = snapshot.GetRetainedSize(nodeIndex);
                        
                        return new
                        {
                            Address = $"0x{address:X}",
                            Size = (ulong)node.Size,
                            RetainedSize = retainedSize,
                            ReferencePaths = showRetainers ? analyzer.FindReferencePaths(nodeIndex, maxPaths: 2) : null
                        };
                    }).ToList() : null
                })
            };
        }
        else
        {
            result = new
            {
                Summary = new
                {
                    heapStats.TotalObjects,
                    heapStats.TotalSize,
                    heapStats.TotalRetainedSize,
                    heapStats.Counters
                },
                Types = typeStats.Select(t => new
                {
                    t.TypeName,
                    t.Count,
                    t.TotalSize,
                    t.RetainedSize
                })
            };
        }

        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        Console.WriteLine(json);
    }

    private void DisplayCsvOutput(List<TypeStatistics> typeStats, HeapSnapshot? snapshot = null, 
        bool showInstances = false, int maxInstances = 3)
    {
        if (showInstances && snapshot != null)
        {
            Console.WriteLine("TypeName,Count,TotalSize,RetainedSize,InstanceAddress,InstanceSize,InstanceRetainedSize");
            var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
            
            foreach (var type in typeStats)
            {
                var sampleInstances = type.Instances.Take(maxInstances).ToList();
                if (sampleInstances.Any())
                {
                    foreach (var nodeIndex in sampleInstances)
                    {
                        var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
                        var address = snapshot.MemoryGraph.GetAddress(nodeIndex);
                        var retainedSize = snapshot.GetRetainedSize(nodeIndex);
                        
                        Console.WriteLine($"\"{type.TypeName}\",{type.Count},{type.TotalSize},{type.RetainedSize}," +
                            $"0x{address:X},{(ulong)node.Size},{retainedSize}");
                    }
                }
                else
                {
                    Console.WriteLine($"\"{type.TypeName}\",{type.Count},{type.TotalSize},{type.RetainedSize},,,");
                }
            }
        }
        else
        {
            Console.WriteLine("TypeName,Count,TotalSize,RetainedSize");
            foreach (var type in typeStats)
            {
                Console.WriteLine($"\"{type.TypeName}\",{type.Count},{type.TotalSize},{type.RetainedSize}");
            }
        }
    }

    private void RunInteractiveMode(FileInfo file)
    {
        try
        {
            HeapSnapshot? snapshot = null;
            
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start($"Loading [yellow]{file.Name}[/]...", ctx =>
                {
                    ctx.Status($"Loading heap dump from [yellow]{file.Name}[/]...");
                    var heapDump = new GCHeapDump(file.FullName);
                    snapshot = new HeapSnapshot(heapDump);
                    
                    ctx.Status("Initializing interactive mode...");
                });
            
            // Run interactive mode after the status spinner completes
            if (snapshot != null)
            {
                var interactive = new InteractiveMode(snapshot);
                interactive.Run();
            }
        }
        catch (Exception ex)
        {
            StatusDisplay.ShowError(ex.Message);
            if (ex.Message.Contains("Not a understood file format"))
            {
                StatusDisplay.ShowInfo("Please provide a valid .gcdump file.");
            }
        }
    }

}

public enum OutputFormat
{
    Table,
    Json,
    Csv
}