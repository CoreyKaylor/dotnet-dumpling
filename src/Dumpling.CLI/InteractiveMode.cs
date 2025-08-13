using Dumpling.Core;
using Spectre.Console;
using Dumpling.CLI.Shared.Formatters;
using Dumpling.CLI.Shared.UI;

namespace Dumpling.CLI;

public class InteractiveMode
{
    private readonly HeapSnapshot snapshot;
    private readonly HeapAnalyzer analyzer;
    private readonly List<TypeStatistics> allTypes;
    private List<TypeStatistics> filteredTypes;
    private string searchTerm = string.Empty;

    public InteractiveMode(HeapSnapshot snapshot)
    {
        this.snapshot = snapshot;
        this.analyzer = new HeapAnalyzer(snapshot);
        this.allTypes = analyzer.GetTypeStatistics();
        this.filteredTypes = allTypes;
    }

    public void Run()
    {
        AnsiConsole.Clear();
        
        while (true)
        {
            var choice = ShowMainMenu();
            
            switch (choice)
            {
                case "Browse Types":
                    BrowseTypes();
                    break;
                case "Search Types":
                    SearchTypes();
                    break;
                case "View Statistics":
                    ViewStatistics();
                    break;
                case "Export Data":
                    ExportData();
                    break;
                case "Exit":
                    return;
            }
        }
    }

    private string ShowMainMenu()
    {
        AnsiConsole.Clear();
        
        // Display header
        StatusDisplay.ShowHeader("🥟 Dumpling Interactive Mode");
        
        // Display heap summary
        var heapStats = analyzer.GetHeapStatistics();
        var panel = new Panel(
            new Markup($"Total Objects: [green]{heapStats.TotalObjects:N0}[/]\n" +
                      $"Total Size: [green]{ByteFormatter.FormatBytes(heapStats.TotalSize)}[/]\n" +
                      $"Total Retained Size: [green]{ByteFormatter.FormatBytes(heapStats.TotalRetainedSize)}[/]")
        )
        {
            Header = new PanelHeader("Heap Summary"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        
        // Show menu
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]What would you like to do?[/]")
                .PageSize(10)
                .AddChoices(new[] {
                    "Browse Types",
                    "Search Types",
                    "View Statistics",
                    "Export Data",
                    "Exit"
                }));
        
        return choice;
    }

    private void BrowseTypes()
    {
        string currentFilter = string.Empty;
        
        while (true)
        {
            AnsiConsole.Clear();
            
            StatusDisplay.ShowHeader("Browse Types");
            
            // Show current filter if active
            if (!string.IsNullOrEmpty(currentFilter))
            {
                AnsiConsole.MarkupLine($"[dim]Active filter: {currentFilter}[/]");
            }
            AnsiConsole.MarkupLine($"[dim]Showing {Math.Min(20, filteredTypes.Count)} of {filteredTypes.Count} types (Total: {allTypes.Count})[/]\n");
            
            // Create a table of types
            var table = TableFactory.CreateTypeStatisticsTable();

            // Add top 20 types
            foreach (var type in filteredTypes.Take(20))
            {
                var percentage = (double)type.RetainedSize / analyzer.GetHeapStatistics().TotalRetainedSize * 100;
                var displayName = analyzer.TypeNameFormatter?.FormatTypeName(type.TypeName) ?? type.TypeName;
                table.AddRow(
                    Markup.Escape(displayName.Length > 50 ? displayName.Substring(0, 47) + "..." : displayName),
                    type.Count.ToString("N0"),
                    ByteFormatter.FormatBytes(type.TotalSize),
                    ByteFormatter.FormatBytes(type.RetainedSize),
                    $"{percentage:F2}%"
                );
            }
            
            AnsiConsole.Write(table);
            
            // Show options
            AnsiConsole.WriteLine();
            var menuChoices = new List<string>
            {
                "View Instances & Retainers",
                "Sort by Retained Size",
                "Sort by Count", 
                "Sort by Total Size",
                "Filter by Size (min)",
                "Filter by Count (min)",
                "Filter by Name",
                filteredTypes.Count < allTypes.Count ? "Clear Filters" : string.Empty,
                "Back to Main Menu"
            }.ToArray();
            
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Options:[/]")
                    .AddChoices(menuChoices));
            
            switch (choice)
            {
                case "View Instances & Retainers":
                    ViewTypeDetails();
                    break;
                case "Sort by Retained Size":
                    filteredTypes = filteredTypes.OrderByDescending(t => t.RetainedSize).ToList();
                    break;
                case "Sort by Count":
                    filteredTypes = filteredTypes.OrderByDescending(t => t.Count).ToList();
                    break;
                case "Sort by Total Size":
                    filteredTypes = filteredTypes.OrderByDescending(t => t.TotalSize).ToList();
                    break;
                case "Filter by Size (min)":
                    ApplyMinSizeFilter(ref currentFilter);
                    break;
                case "Filter by Count (min)":
                    ApplyMinCountFilter(ref currentFilter);
                    break;
                case "Filter by Name":
                    ApplyNameFilter(ref currentFilter);
                    break;
                case "Clear Filters":
                    filteredTypes = allTypes;
                    currentFilter = string.Empty;
                    break;
                case "Back to Main Menu":
                    return;
            }
        }
    }

    private void SearchTypes()
    {
        AnsiConsole.Clear();
        
        StatusDisplay.ShowHeader("Search Types");
        
        searchTerm = InputHelper.GetSearchTerm("Enter search term (type name contains):");
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filteredTypes = allTypes
                .Where(t => t.TypeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.RetainedSize)
                .ToList();
            
            AnsiConsole.MarkupLine($"\n[green]Found {filteredTypes.Count} types matching '{searchTerm}'[/]");
            
            if (filteredTypes.Any())
            {
                // Show results
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.ShowRowSeparators = true;
                table.Expand = false;
                table.AddColumn("Type");
                table.AddColumn(new TableColumn("Count").RightAligned());
                table.AddColumn(new TableColumn("Retained Size").RightAligned());

                foreach (var type in filteredTypes.Take(10))
                {
                    table.AddRow(
                        Markup.Escape(type.TypeName),
                        type.Count.ToString("N0"),
                        ByteFormatter.FormatBytes(type.RetainedSize)
                    );
                }
                
                AnsiConsole.Write(table);
            }
        }
        else
        {
            filteredTypes = allTypes;
            AnsiConsole.MarkupLine("[yellow]Search cleared - showing all types[/]");
        }
        
        InputHelper.WaitForKeyPress();
    }

    private void ApplyMinSizeFilter(ref string currentFilter)
    {
        // Position prompt at bottom of screen to keep table visible
        StatusDisplay.WriteLine();
        var minSizeKb = InputHelper.GetMinSizeKB();
        
        var minSize = (ulong)(minSizeKb * 1024);
        filteredTypes = allTypes.Where(t => t.RetainedSize >= minSize).ToList();
        currentFilter = $"Retained size >= {ByteFormatter.FormatBytes(minSize)}";
        
        // Re-sort by retained size when filtering by size
        filteredTypes = filteredTypes.OrderByDescending(t => t.RetainedSize).ToList();
    }
    
    private void ApplyMinCountFilter(ref string currentFilter)
    {
        StatusDisplay.WriteLine();
        var minCount = InputHelper.GetMinCount();
        
        filteredTypes = allTypes.Where(t => t.Count >= minCount).ToList();
        currentFilter = $"Count >= {minCount:N0}";
        
        // Re-sort by count when filtering by count
        filteredTypes = filteredTypes.OrderByDescending(t => t.Count).ToList();
    }
    
    private void ApplyNameFilter(ref string currentFilter)
    {
        StatusDisplay.WriteLine();
        var pattern = InputHelper.GetTypeNameFilter();
        
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            filteredTypes = allTypes
                .Where(t => t.TypeName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.RetainedSize)
                .ToList();
            currentFilter = $"Name contains '{pattern}'";
        }
        else
        {
            filteredTypes = allTypes;
            currentFilter = string.Empty;
        }
    }

    private void ViewStatistics()
    {
        AnsiConsole.Clear();
        
        StatusDisplay.ShowHeader("Heap Statistics");
        
        var stats = analyzer.GetHeapStatistics();
        
        // Overall statistics
        var statsTable = new Table();
        statsTable.Border(TableBorder.Rounded);
        statsTable.ShowRowSeparators = true;
        statsTable.Expand = false;
        statsTable.AddColumn("Metric");
        statsTable.AddColumn(new TableColumn("Value").RightAligned());
        
        statsTable.AddRow("Total Objects", stats.TotalObjects.ToString("N0"));
        statsTable.AddRow("Total Size", ByteFormatter.FormatBytes(stats.TotalSize));
        statsTable.AddRow("Total Retained Size", ByteFormatter.FormatBytes(stats.TotalRetainedSize));
        statsTable.AddRow("Unique Types", allTypes.Count.ToString("N0"));
        
        AnsiConsole.Write(statsTable);
        
        // Top consumers
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Top 5 Types by Retained Size:[/]");
        
        var topTable = new Table();
        topTable.Border(TableBorder.Rounded);
        topTable.ShowRowSeparators = true;
        topTable.Expand = false;
        topTable.AddColumn("Type");
        topTable.AddColumn(new TableColumn("Retained Size").RightAligned());
        topTable.AddColumn(new TableColumn("% of Heap").RightAligned());
        
        foreach (var type in allTypes.Take(5))
        {
            var percentage = (double)type.RetainedSize / stats.TotalRetainedSize * 100;
            topTable.AddRow(
                Markup.Escape(type.TypeName.Length > 50 ? type.TypeName.Substring(0, 47) + "..." : type.TypeName),
                ByteFormatter.FormatBytes(type.RetainedSize),
                $"{percentage:F2}%"
            );
        }
        
        AnsiConsole.Write(topTable);
        
        // Show counters if available
        if (stats.Counters != null && stats.Counters.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Performance Counters:[/]");
            
            var counterTable = new Table();
            counterTable.Border(TableBorder.Rounded);
            counterTable.ShowRowSeparators = true;
            counterTable.Expand = false;
            counterTable.AddColumn("Counter");
            counterTable.AddColumn(new TableColumn("Value").RightAligned());
            
            foreach (var counter in stats.Counters.OrderBy(c => c.Key))
            {
                counterTable.AddRow(counter.Key, counter.Value.ToString("N2"));
            }
            
            AnsiConsole.Write(counterTable);
        }
        
        InputHelper.WaitForKeyPress();
    }

    private void ExportData()
    {
        AnsiConsole.Clear();
        
        var exportChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Export format:[/]")
                .AddChoices(new[] {
                    "CSV - Current filtered types",
                    "JSON - Current filtered types",
                    "CSV - All types",
                    "JSON - All types",
                    "Cancel"
                }));
        
        if (exportChoice == "Cancel")
            return;
        
        var filename = AnsiConsole.Ask<string>("Enter filename (without extension):");
        
        try
        {
            var typesToExport = exportChoice.Contains("All types") ? allTypes : filteredTypes;
            
            if (exportChoice.StartsWith("CSV"))
            {
                var csvPath = $"{filename}.csv";
                using var writer = new StreamWriter(csvPath);
                writer.WriteLine("TypeName,Count,TotalSize,RetainedSize");
                foreach (var type in typesToExport)
                {
                    writer.WriteLine($"\"{type.TypeName}\",{type.Count},{type.TotalSize},{type.RetainedSize}");
                }
                AnsiConsole.MarkupLine($"[green]Exported {typesToExport.Count} types to {csvPath}[/]");
            }
            else
            {
                var jsonPath = $"{filename}.json";
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ExportDate = DateTime.Now,
                    TotalTypes = typesToExport.Count,
                    Types = typesToExport.Select(t => new
                    {
                        t.TypeName,
                        t.Count,
                        t.TotalSize,
                        t.RetainedSize
                    })
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                File.WriteAllText(jsonPath, json);
                AnsiConsole.MarkupLine($"[green]Exported {typesToExport.Count} types to {jsonPath}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Export failed: {ex.Message}[/]");
        }
        
        InputHelper.WaitForKeyPress();
    }

    
    private string FormatPathForTable(string path)
    {
        // Split path by arrow and format each segment
        var segments = path.Split(" ← ");
        if (segments.Length <= 1)
            return Markup.Escape(path);
        
        var formatted = new System.Text.StringBuilder();
        for (int i = 0; i < segments.Length; i++)
        {
            if (i == 0)
            {
                // First segment without indentation
                formatted.Append(Markup.Escape(segments[i]));
            }
            else
            {
                // Add line break and indentation for subsequent segments
                formatted.Append("\n  ← ");
                formatted.Append(Markup.Escape(segments[i]));
            }
        }
        
        return formatted.ToString();
    }

    private void ViewTypeDetails()
    {
        var typesToShow = filteredTypes.Take(20).ToList();
        if (!typesToShow.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No types to analyze[/]");
            AnsiConsole.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
            return;
        }

        int selectedIndex = 0;
        
        while (true)
        {
            AnsiConsole.Clear();
            
            var rule = new Rule("[yellow]Select Type to Analyze[/]");
            rule.Style = Style.Parse("blue");
            AnsiConsole.Write(rule);
            
            // Create a table with selection indicator
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.ShowRowSeparators = true;
            table.Expand = false;
            table.AddColumn(new TableColumn("").Width(2));  // Selection indicator column
            table.AddColumn(new TableColumn("Type").Width(45));
            table.AddColumn(new TableColumn("Count").RightAligned().Width(10));
            table.AddColumn(new TableColumn("Total Size").RightAligned().Width(12));
            table.AddColumn(new TableColumn("Retained Size").RightAligned().Width(14));
            table.AddColumn(new TableColumn("% of Heap").RightAligned().Width(10));
            
            var heapStats = analyzer.GetHeapStatistics();
            
            for (int i = 0; i < typesToShow.Count; i++)
            {
                var type = typesToShow[i];
                var displayName = analyzer.TypeNameFormatter?.FormatTypeName(type.TypeName) ?? type.TypeName;
                var percentage = (double)type.RetainedSize / heapStats.TotalRetainedSize * 100;
                
                // Add selection indicator
                var selector = i == selectedIndex ? "[yellow]→[/]" : " ";
                
                // Highlight selected row
                if (i == selectedIndex)
                {
                    table.AddRow(
                        selector,
                        $"[yellow]{Markup.Escape(displayName.Length > 50 ? displayName.Substring(0, 47) + "..." : displayName)}[/]",
                        $"[yellow]{type.Count:N0}[/]",
                        $"[yellow]{ByteFormatter.FormatBytes(type.TotalSize)}[/]",
                        $"[yellow]{ByteFormatter.FormatBytes(type.RetainedSize)}[/]",
                        $"[yellow]{percentage:F2}%[/]"
                    );
                }
                else
                {
                    table.AddRow(
                        selector,
                        Markup.Escape(displayName.Length > 50 ? displayName.Substring(0, 47) + "..." : displayName),
                        type.Count.ToString("N0"),
                        ByteFormatter.FormatBytes(type.TotalSize),
                        ByteFormatter.FormatBytes(type.RetainedSize),
                        $"{percentage:F2}%"
                    );
                }
            }
            
            AnsiConsole.Write(table);
            
            // Show navigation instructions
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use ↑/↓ to navigate, Enter to select, Esc to cancel[/]");
            
            // Handle keyboard input
            var key = Console.ReadKey(true);
            
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (selectedIndex > 0)
                        selectedIndex--;
                    break;
                    
                case ConsoleKey.DownArrow:
                    if (selectedIndex < typesToShow.Count - 1)
                        selectedIndex++;
                    break;
                    
                case ConsoleKey.Enter:
                    var selectedType = typesToShow[selectedIndex];
                    ViewTypeRetainerAnalysis(selectedType);
                    return;
                    
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void ViewTypeRetainerAnalysis(TypeStatistics typeStats)
    {
        while (true)
        {
            AnsiConsole.Clear();
            
            var rule = new Rule($"[yellow]Analyzing {Markup.Escape(typeStats.TypeName)}[/]");
            rule.Style = Style.Parse("blue");
            AnsiConsole.Write(rule);
            
            // Display summary
            var panel = new Panel(
                new Markup($"Total Instances: [green]{typeStats.Count:N0}[/]\n" +
                          $"Total Size: [green]{ByteFormatter.FormatBytes(typeStats.TotalSize)}[/]\n" +
                          $"Total Retained Size: [green]{ByteFormatter.FormatBytes(typeStats.RetainedSize)}[/]\n" +
                          $"Avg Size per Instance: [green]{ByteFormatter.FormatBytes(typeStats.TotalSize / (ulong)Math.Max(1, typeStats.Count))}[/]")
            )
            {
                Header = new PanelHeader("Type Summary"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(panel);
            
            AnsiConsole.WriteLine();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]What would you like to analyze?[/]")
                    .AddChoices(new[] {
                        "View Retaining Types (what's holding these objects)",
                        "View Sample Instances",
                        "View Reference Paths (sample)",
                        "Back"
                    }));
            
            switch (choice)
            {
                case "View Retaining Types (what's holding these objects)":
                    ViewAggregatedRetainers(typeStats);
                    break;
                case "View Sample Instances":
                    ViewInstanceDetails(typeStats);
                    break;
                case "View Reference Paths (sample)":
                    ViewSampleReferencePaths(typeStats);
                    break;
                case "Back":
                    return;
            }
        }
    }

    private void ViewAggregatedRetainers(TypeStatistics typeStats)
    {
        AnsiConsole.Clear();
        
        var displayName = analyzer.TypeNameFormatter?.FormatTypeName(typeStats.TypeName) ?? typeStats.TypeName;
        var rule = new Rule($"[yellow]Types Retaining {Markup.Escape(displayName)}[/]");
        rule.Style = Style.Parse("blue");
        AnsiConsole.Write(rule);
        
        Dictionary<string, (int count, HashSet<Graphs.NodeIndex> uniqueRetainers, ulong totalRetainedSize)>? retainerStats = null;
        
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Analyzing retainers...", ctx =>
            {
                // Analyze all instances to find what types are retaining them
                retainerStats = new Dictionary<string, (int count, HashSet<Graphs.NodeIndex> uniqueRetainers, ulong totalRetainedSize)>();
                var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
                var typeStorage = snapshot.MemoryGraph.AllocTypeNodeStorage();
                
                // Sample a reasonable number of instances for analysis
                var instancesToAnalyze = typeStats.Instances.Take(Math.Min(100, typeStats.Instances.Count)).ToList();
                ctx.Status($"Analyzing {instancesToAnalyze.Count} sample instances...");
                
                foreach (var nodeIndex in instancesToAnalyze)
                {
                    // Get immediate retainers for this instance
                    var refNode = snapshot.RefGraph.GetNode(nodeIndex);
                    
                    for (var retainerIndex = refNode.GetFirstChildIndex();
                         retainerIndex != Graphs.NodeIndex.Invalid;
                         retainerIndex = refNode.GetNextChildIndex())
                    {
                        if (retainerIndex != nodeIndex) // Skip self-references
                        {
                            var retainerNode = snapshot.MemoryGraph.GetNode(retainerIndex, nodeStorage);
                            var retainerType = snapshot.MemoryGraph.GetType(retainerNode.TypeIndex, typeStorage);
                            var retainerTypeName = retainerType.Name;
                            var retainedSize = snapshot.GetRetainedSize(retainerIndex);
                            
                            if (!retainerStats.ContainsKey(retainerTypeName))
                            {
                                retainerStats[retainerTypeName] = (0, new HashSet<Graphs.NodeIndex>(), 0);
                            }
                            
                            var current = retainerStats[retainerTypeName];
                            current.uniqueRetainers.Add(retainerIndex);
                            retainerStats[retainerTypeName] = (
                                current.count + 1,
                                current.uniqueRetainers,
                                current.totalRetainedSize + retainedSize
                            );
                        }
                    }
                }
            });
        
        if (retainerStats != null && retainerStats.Any())
        {
            AnsiConsole.MarkupLine($"\n[dim]Analyzed {typeStats.Instances.Take(100).Count():N0} sample instances of {typeStats.Count:N0} total[/]\n");
            
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.ShowRowSeparators = true;
            table.Expand = false;
            table.AddColumn("Retaining Type");
            table.AddColumn(new TableColumn("Unique Objects").RightAligned());
            table.AddColumn(new TableColumn("References").RightAligned());
            table.AddColumn(new TableColumn("Avg Refs").RightAligned());
            table.AddColumn(new TableColumn("Total Retained").RightAligned());
            
            var sortedRetainers = retainerStats
                .OrderByDescending(r => r.Value.count)
                .Take(20).ToList();
            
            foreach (var retainer in sortedRetainers)
            {
                var avgRefs = (double)retainer.Value.count / Math.Max(1, retainer.Value.uniqueRetainers.Count);
                var retainerDisplayName = analyzer.TypeNameFormatter?.FormatTypeName(retainer.Key) ?? retainer.Key;
                
                table.AddRow(
                    Markup.Escape(retainerDisplayName.Length > 50 ? retainerDisplayName.Substring(0, 47) + "..." : retainerDisplayName),
                    retainer.Value.uniqueRetainers.Count.ToString("N0"),
                    retainer.Value.count.ToString("N0"),
                    avgRefs.ToString("F1"),
                    ByteFormatter.FormatBytes(retainer.Value.totalRetainedSize / (ulong)Math.Max(1, retainer.Value.uniqueRetainers.Count))
                );
            }
            
            AnsiConsole.Write(table);
            
            // Show insights
            AnsiConsole.WriteLine();
            var topRetainer = sortedRetainers.First();
            var topRetainerName = analyzer.TypeNameFormatter?.FormatTypeName(topRetainer.Key) ?? topRetainer.Key;
            AnsiConsole.MarkupLine($"[dim]💡 Most references from: {Markup.Escape(topRetainerName)}[/]");
            AnsiConsole.MarkupLine($"[dim]   {topRetainer.Value.count:N0} references from {topRetainer.Value.uniqueRetainers.Count:N0} unique objects[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No retainers found (these instances may be directly rooted)[/]");
        }
        
        InputHelper.WaitForKeyPress();
    }

    private void ViewInstanceDetails(TypeStatistics typeStats)
    {
        AnsiConsole.Clear();
        
        var displayName = analyzer.TypeNameFormatter?.FormatTypeName(typeStats.TypeName) ?? typeStats.TypeName;
        var rule = new Rule($"[yellow]Sample Instances of {Markup.Escape(displayName)}[/]");
        rule.Style = Style.Parse("blue");
        AnsiConsole.Write(rule);
        
        var sampleInstances = typeStats.Instances.Take(10).ToList();
        
        if (sampleInstances.Any())
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.ShowRowSeparators = true;
            table.Expand = false;
            table.AddColumn(new TableColumn("Instance").Width(10));
            table.AddColumn(new TableColumn("Address").RightAligned().Width(15));
            table.AddColumn(new TableColumn("Size").RightAligned().Width(12));
            table.AddColumn(new TableColumn("Retained Size").RightAligned().Width(14));
            
            var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
            
            foreach (var nodeIndex in sampleInstances)
            {
                var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
                var address = snapshot.MemoryGraph.GetAddress(nodeIndex);
                var retainedSize = snapshot.GetRetainedSize(nodeIndex);
                
                table.AddRow(
                    $"#{sampleInstances.IndexOf(nodeIndex) + 1}",
                    $"0x{address:X}",
                    ByteFormatter.FormatBytes((ulong)node.Size),
                    ByteFormatter.FormatBytes(retainedSize)
                );
            }
            
            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No instances available for this type.[/]");
        }
        
        InputHelper.WaitForKeyPress();
    }

    private void ViewSampleReferencePaths(TypeStatistics typeStats)
    {
        AnsiConsole.Clear();
        
        var displayName = analyzer.TypeNameFormatter?.FormatTypeName(typeStats.TypeName) ?? typeStats.TypeName;
        var rule = new Rule($"[yellow]Reference Paths for {Markup.Escape(displayName)}[/]");
        rule.Style = Style.Parse("blue");
        AnsiConsole.Write(rule);
        
        // Sample more instances to get better path statistics
        var sampleInstances = typeStats.Instances.Take(Math.Min(50, typeStats.Instances.Count)).ToList();
        
        if (sampleInstances.Any())
        {
            var pathCounts = new Dictionary<string, int>();
            
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start($"Analyzing reference paths for {sampleInstances.Count} instances...", _ =>
                {
                    foreach (var nodeIndex in sampleInstances)
                    {
                        var paths = analyzer.FindReferencePaths(nodeIndex, maxPaths: 1);
                        foreach (var path in paths)
                        {
                            // Paths are already formatted by FindReferencePaths if formatter is available
                            pathCounts.TryAdd(path, 0);
                            pathCounts[path]++;
                        }
                    }
                });
            
            AnsiConsole.MarkupLine($"\n[dim]Analyzed {sampleInstances.Count} sample instances[/]\n");
            
            if (pathCounts.Any())
            {
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.ShowRowSeparators = true;  // Add borders between rows
                table.Expand = false;
                
                // Configure columns with explicit widths for better wrapping
                table.AddColumn(new TableColumn("Reference Path").Width(60));
                table.AddColumn(new TableColumn("Count").RightAligned().Width(10));
                table.AddColumn(new TableColumn("% of Sample").RightAligned().Width(12));
                
                var sortedPaths = pathCounts
                    .OrderByDescending(p => p.Value)
                    .Take(15).ToList();
                
                foreach (var pathEntry in sortedPaths)
                {
                    var percentage = (double)pathEntry.Value / sampleInstances.Count * 100;
                    
                    // Format the path with proper line breaks for wrapping
                    var formattedPath = FormatPathForTable(pathEntry.Key);
                    
                    table.AddRow(
                        new Markup(formattedPath),
                        new Text(pathEntry.Value.ToString("N0")),
                        new Text($"{percentage:F1}%")
                    );
                }
                
                AnsiConsole.Write(table);
                
                // Show insights
                AnsiConsole.WriteLine();
                var topPath = sortedPaths.First();
                AnsiConsole.MarkupLine($"[dim]💡 Most common path ({topPath.Value}/{sampleInstances.Count} instances):[/]");
                AnsiConsole.MarkupLine($"[dim]   {Markup.Escape(topPath.Key)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No reference paths found (instances may be directly rooted)[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No instances available for this type.[/]");
        }
        
        InputHelper.WaitForKeyPress();
    }
}