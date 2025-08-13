using Dumpling.Core;
using Dumpling.CLI.Commands;
using Spectre.Console;
using Dumpling.CLI.Shared.Formatters;
using Dumpling.CLI.Shared.UI;

namespace Dumpling.CLI;

public class ComparisonInteractiveMode
{
    private readonly HeapSnapshot baselineSnapshot;
    private readonly HeapSnapshot currentSnapshot;
    private readonly ComparisonResult comparisonResult;
    private readonly HeapComparer comparer;
    private readonly FileInfo baselineFile;
    private readonly FileInfo currentFile;
    private readonly List<TypeDelta> allDeltas;
    private List<TypeDelta> filteredDeltas;
    private string searchTerm = string.Empty;
    private CompareSortBy currentSort = CompareSortBy.RetainedSizeDelta;
    private bool showUnchanged;

    public ComparisonInteractiveMode(
        HeapSnapshot baselineSnapshot, 
        HeapSnapshot currentSnapshot,
        FileInfo baselineFile,
        FileInfo currentFile)
    {
        this.baselineSnapshot = baselineSnapshot;
        this.currentSnapshot = currentSnapshot;
        this.baselineFile = baselineFile;
        this.currentFile = currentFile;
        this.comparer = new HeapComparer();
        this.comparisonResult = comparer.Compare(baselineSnapshot, currentSnapshot);
        this.allDeltas = comparisonResult.TypeDeltas;
        this.filteredDeltas = FilterDeltas();
    }

    public void Run()
    {
        AnsiConsole.Clear();
        
        while (true)
        {
            var choice = ShowMainMenu();
            
            switch (choice)
            {
                case "Browse Type Changes":
                    BrowseTypeChanges();
                    break;
                case "View Growth Patterns":
                    ViewGrowthPatterns();
                    break;
                case "Search Types":
                    SearchTypes();
                    break;
                case "Compare Specific Type":
                    CompareSpecificType();
                    break;
                case "View Summary":
                    ViewSummary();
                    break;
                case "Change Sort Order":
                    ChangeSortOrder();
                    break;
                case "Toggle Unchanged Types":
                    ToggleUnchangedTypes();
                    break;
                case "Export Comparison":
                    ExportComparison();
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
        var panel = PanelFactory.CreateComparisonHeaderPanel(baselineFile.Name, currentFile.Name, comparisonResult.RetainedSizeDelta);
        AnsiConsole.Write(panel);
        
        // Display current settings
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Current Sort: {currentSort} | Show Unchanged: {showUnchanged} | Filter: {(string.IsNullOrEmpty(searchTerm) ? "None" : searchTerm)}[/]");
        AnsiConsole.WriteLine();
        
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to do?[/]")
                .PageSize(10)
                .AddChoices(new[]
                {
                    "Browse Type Changes",
                    "View Growth Patterns",
                    "Search Types",
                    "Compare Specific Type",
                    "View Summary",
                    "Change Sort Order",
                    "Toggle Unchanged Types",
                    "Export Comparison",
                    "Exit"
                }));
    }

    private void BrowseTypeChanges()
    {
        while (true)
        {
            var types = GetSortedDeltas()
                .Where(d => showUnchanged || d.Status != TypeChangeStatus.Unchanged)
                .ToList();
            
            if (!types.Any())
            {
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[yellow]No types to display with current filters.[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                Console.ReadKey(true);
                return;
            }
            
            var table = new InteractiveTable<TypeDelta>("Type Changes - Comparison Results", types)
                .AddColumn("Type", d => Markup.Escape(d.TypeName.Length > 50 ? d.TypeName.Substring(0, 47) + "..." : d.TypeName))
                .AddColumn("Status", d => GetStatusText(d.Status), "Status", Justify.Center)
                .AddColumn("Count Δ", d => CountFormatter.FormatCountDelta(d.CountDelta), "CountDelta", Justify.Right)
                .AddColumn("Size Δ", d => ByteFormatter.FormatBytesDelta(d.TotalSizeDelta), "TotalSizeDelta", Justify.Right)
                .AddColumn("Retained Δ", d => ByteFormatter.FormatBytesDelta(d.RetainedSizeDelta), "RetainedSizeDelta", Justify.Right)
                .AddColumn("Growth %", d => CountFormatter.FormatPercentage(d.BaselineRetainedSize > 0 
                    ? (double)d.RetainedSizeDelta / d.BaselineRetainedSize * 100 
                    : d.RetainedSizeDelta > 0 ? 100.0 : 0.0), "GrowthPercent", Justify.Right);
            
            var selected = table.Show();
            
            if (selected == null)
                break;
                
            ViewTypeDetails(selected);
        }
    }
    
    private string GetStatusText(TypeChangeStatus status)
    {
        return status switch
        {
            TypeChangeStatus.Added => "NEW",
            TypeChangeStatus.Removed => "REMOVED",
            TypeChangeStatus.Changed => "CHANGED",
            TypeChangeStatus.Unchanged => "UNCHANGED",
            _ => "-"
        };
    }

    private void ViewTypeDetails(TypeDelta delta)
    {
        AnsiConsole.Clear();
        
        // Display type header
        var panel = new Panel(
            new Markup($"[bold yellow]Type Analysis: {Markup.Escape(delta.TypeName)}[/]\n" +
                      $"Status: {GetStatusMarkup(delta.Status)}\n" +
                      $"Count: {delta.BaselineCount:N0} → {delta.CurrentCount:N0} ([{(delta.CountDelta >= 0 ? "red" : "green")}]{delta.CountDelta:+#,0;-#,0;0}[/])\n" +
                      $"Size: {ByteFormatter.FormatBytes(delta.BaselineTotalSize)} → {ByteFormatter.FormatBytes(delta.CurrentTotalSize)} ([{(delta.TotalSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(delta.TotalSizeDelta)}[/])\n" +
                      $"Retained: {ByteFormatter.FormatBytes(delta.BaselineRetainedSize)} → {ByteFormatter.FormatBytes(delta.CurrentRetainedSize)} ([{(delta.RetainedSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(delta.RetainedSizeDelta)}[/])")
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        AnsiConsole.Write(panel);
        
        var choices = new List<string>
        {
            "View Instance Comparison",
            "Show Reference Paths (Baseline)",
            "Show Reference Paths (Current)",
            "Compare Growth Pattern",
            "← Back to Type List"
        };
        
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to analyze?[/]")
                .AddChoices(choices));
        
        switch (choice)
        {
            case "View Instance Comparison":
                ViewInstanceComparison(delta);
                break;
            case "Show Reference Paths (Baseline)":
                ShowReferencePaths(delta, true);
                break;
            case "Show Reference Paths (Current)":
                ShowReferencePaths(delta, false);
                break;
            case "Compare Growth Pattern":
                CompareGrowthPattern(delta);
                break;
        }
    }

    private void ViewInstanceComparison(TypeDelta delta)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold yellow]Instance Comparison: {Markup.Escape(delta.TypeName)}[/]");
        AnsiConsole.WriteLine();
        
        var result = comparer.CompareInstances(baselineSnapshot, currentSnapshot, delta.TypeName);
        
        // Display baseline instances
        if (result.BaselineInstances.Any())
        {
            AnsiConsole.MarkupLine("[bold]Baseline Instances:[/]");
            var baselineTable = new Table();
            baselineTable.Border(TableBorder.Rounded);
            baselineTable.AddColumn("#");
            baselineTable.AddColumn("Address");
            baselineTable.AddColumn("Size");
            baselineTable.AddColumn("Retained");
            
            int index = 1;
            foreach (var instance in result.BaselineInstances.Take(5))
            {
                baselineTable.AddRow(
                    index.ToString(),
                    $"0x{instance.Address:X}",
                    ByteFormatter.FormatBytes(instance.Size),
                    ByteFormatter.FormatBytes(instance.RetainedSize)
                );
                index++;
            }
            AnsiConsole.Write(baselineTable);
        }
        
        AnsiConsole.WriteLine();
        
        // Display current instances
        if (result.CurrentInstances.Any())
        {
            AnsiConsole.MarkupLine("[bold]Current Instances:[/]");
            var currentTable = new Table();
            currentTable.Border(TableBorder.Rounded);
            currentTable.AddColumn("#");
            currentTable.AddColumn("Address");
            currentTable.AddColumn("Size");
            currentTable.AddColumn("Retained");
            
            int index = 1;
            foreach (var instance in result.CurrentInstances.Take(5))
            {
                currentTable.AddRow(
                    index.ToString(),
                    $"0x{instance.Address:X}",
                    ByteFormatter.FormatBytes(instance.Size),
                    ByteFormatter.FormatBytes(instance.RetainedSize)
                );
                index++;
            }
            AnsiConsole.Write(currentTable);
        }
        
        AnsiConsole.WriteLine();
        InputHelper.WaitForKeyPress();
    }

    private void ShowReferencePaths(TypeDelta delta, bool baseline)
    {
        AnsiConsole.Clear();
        var snapshot = baseline ? baselineSnapshot : currentSnapshot;
        var analyzer = new HeapAnalyzer(snapshot);
        var types = analyzer.GetTypeStatistics();
        var type = types.FirstOrDefault(t => t.TypeName == delta.TypeName);
        
        if (type == null || !type.Instances.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]No instances found in {(baseline ? "baseline" : "current")} snapshot.[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
            return;
        }
        
        AnsiConsole.MarkupLine($"[bold yellow]Reference Paths - {(baseline ? "Baseline" : "Current")}: {Markup.Escape(delta.TypeName)}[/]");
        AnsiConsole.WriteLine();
        
        // Show paths for first few instances
        var instancesToShow = Math.Min(3, type.Instances.Count);
        for (int i = 0; i < instancesToShow; i++)
        {
            var nodeIndex = type.Instances[i];
            var paths = analyzer.FindReferencePaths(nodeIndex, maxPaths: 2);
            
            AnsiConsole.MarkupLine($"[bold]Instance #{i + 1}:[/]");
            foreach (var path in paths)
            {
                var segments = path.Split(" ← ");
                foreach (var segment in segments)
                {
                    AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(segment)}[/]");
                }
                AnsiConsole.WriteLine();
            }
        }
        
        InputHelper.WaitForKeyPress();
    }

    private void CompareGrowthPattern(TypeDelta delta)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold yellow]Growth Pattern: {Markup.Escape(delta.TypeName)}[/]");
        AnsiConsole.WriteLine();
        
        var growthPercent = delta.BaselineRetainedSize > 0 
            ? (double)delta.RetainedSizeDelta / delta.BaselineRetainedSize * 100 
            : delta.RetainedSizeDelta > 0 ? 100.0 : 0.0;
        
        // Create a simple bar chart
        var chart = new BarChart()
            .Width(60)
            .Label("[green bold]Memory Usage[/]");
        
        chart.AddItem("Baseline", delta.BaselineRetainedSize / 1024.0, Color.Blue);
        chart.AddItem("Current", delta.CurrentRetainedSize / 1024.0, Color.Green);
        
        AnsiConsole.Write(chart);
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Growth Rate: [{(growthPercent >= 0 ? "red" : "green")}]{growthPercent:F2}%[/]");
        AnsiConsole.WriteLine();
        
        // Provide analysis
        if (delta.CountDelta > 0 && delta.RetainedSizeDelta > 0)
        {
            AnsiConsole.MarkupLine("[red]⚠ Potential memory leak detected:[/]");
            AnsiConsole.MarkupLine($"  • Instance count increased by {delta.CountDelta:N0}");
            AnsiConsole.MarkupLine($"  • Retained memory increased by {ByteFormatter.FormatBytes((ulong)Math.Abs(delta.RetainedSizeDelta))}");
        }
        else if (delta.CountDelta < 0)
        {
            AnsiConsole.MarkupLine("[green]✓ Memory usage decreased:[/]");
            AnsiConsole.MarkupLine($"  • Instance count reduced by {Math.Abs(delta.CountDelta):N0}");
            AnsiConsole.MarkupLine($"  • Retained memory reduced by {ByteFormatter.FormatBytes((ulong)Math.Abs(delta.RetainedSizeDelta))}");
        }
        
        AnsiConsole.WriteLine();
        InputHelper.WaitForKeyPress();
    }

    private void ViewGrowthPatterns()
    {
        var growingTypes = filteredDeltas
            .Where(d => d.RetainedSizeDelta > 0)
            .OrderByDescending(d => d.RetainedSizeDelta)
            .ToList();
        
        if (!growingTypes.Any())
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold yellow]Growth Patterns[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]No types show memory growth.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
            return;
        }
        
        var table = new InteractiveTable<TypeDelta>("Growth Patterns - Types with Memory Growth", growingTypes)
            .AddColumn("Type", d => Markup.Escape(d.TypeName.Length > 50 ? d.TypeName.Substring(0, 47) + "..." : d.TypeName))
            .AddColumn("Growth", d => ByteFormatter.FormatBytesDelta(d.RetainedSizeDelta), "RetainedSizeDelta", Justify.Right)
            .AddColumn("Count Δ", d => $"+{d.CountDelta:N0}", "CountDelta", Justify.Right)
            .AddColumn("Growth %", d => $"+{(d.BaselineRetainedSize > 0 
                ? (double)d.RetainedSizeDelta / d.BaselineRetainedSize * 100 
                : 100.0):F1}%", "GrowthPercent", Justify.Right)
            .AddColumn("Base Size", d => ByteFormatter.FormatBytes(d.BaselineRetainedSize), "BaselineRetainedSize", Justify.Right)
            .AddColumn("Current Size", d => ByteFormatter.FormatBytes(d.CurrentRetainedSize), "CurrentRetainedSize", Justify.Right);
        
        var selected = table.Show();
        
        if (selected != null)
        {
            ViewTypeDetails(selected);
        }
    }

    private void SearchTypes()
    {
        AnsiConsole.Clear();
        var search = AnsiConsole.Ask<string>("Enter search term:");
        searchTerm = search;
        filteredDeltas = FilterDeltas();
        
        AnsiConsole.MarkupLine($"[green]Filter applied. Found {filteredDeltas.Count} matching types.[/]");
        InputHelper.WaitForKeyPress();
    }

    private void CompareSpecificType()
    {
        AnsiConsole.Clear();
        var typeName = AnsiConsole.Ask<string>("Enter type name to compare:");
        
        var delta = allDeltas.FirstOrDefault(d => d.TypeName.Contains(typeName, StringComparison.OrdinalIgnoreCase));
        
        if (delta == null)
        {
            AnsiConsole.MarkupLine($"[red]Type '{typeName}' not found.[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
            return;
        }
        
        ViewTypeDetails(delta);
    }

    private void ViewSummary()
    {
        AnsiConsole.Clear();
        
        var panel = new Panel(
            new Markup($"[bold yellow]Comparison Summary[/]\n\n" +
                      $"[bold]Files:[/]\n" +
                      $"  Baseline: {Markup.Escape(baselineFile.Name)} ({baselineFile.LastWriteTime:yyyy-MM-dd HH:mm})\n" +
                      $"  Current: {Markup.Escape(currentFile.Name)} ({currentFile.LastWriteTime:yyyy-MM-dd HH:mm})\n" +
                      $"  Time Difference: {(currentFile.LastWriteTime - baselineFile.LastWriteTime).TotalHours:F1} hours\n\n" +
                      $"[bold]Overall Changes:[/]\n" +
                      $"  Objects: {comparisonResult.BaselineStats.TotalObjects:N0} → {comparisonResult.CurrentStats.TotalObjects:N0} ({comparisonResult.ObjectCountDelta:+#,0;-#,0;0})\n" +
                      $"  Total Size: {ByteFormatter.FormatBytes(comparisonResult.BaselineStats.TotalSize)} → {ByteFormatter.FormatBytes(comparisonResult.CurrentStats.TotalSize)} ({ByteFormatter.FormatBytesDelta(comparisonResult.TotalSizeDelta)})\n" +
                      $"  Retained Size: {ByteFormatter.FormatBytes(comparisonResult.BaselineStats.TotalRetainedSize)} → {ByteFormatter.FormatBytes(comparisonResult.CurrentStats.TotalRetainedSize)} ({ByteFormatter.FormatBytesDelta(comparisonResult.RetainedSizeDelta)})\n\n" +
                      $"[bold]Type Changes:[/]\n" +
                      $"  New Types: {comparisonResult.NewTypes.Count}\n" +
                      $"  Removed Types: {comparisonResult.RemovedTypes.Count}\n" +
                      $"  Changed Types: {comparisonResult.TypeDeltas.Count(d => d.Status == TypeChangeStatus.Changed)}\n" +
                      $"  Unchanged Types: {comparisonResult.TypeDeltas.Count(d => d.Status == TypeChangeStatus.Unchanged)}")
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        InputHelper.WaitForKeyPress();
    }

    private void ChangeSortOrder()
    {
        AnsiConsole.Clear();
        
        var sortOptions = Enum.GetValues<CompareSortBy>()
            .Select(s => s.ToString())
            .ToArray();
        
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select sort order:[/]")
                .AddChoices(sortOptions));
        
        currentSort = Enum.Parse<CompareSortBy>(selected);
        filteredDeltas = FilterDeltas();
        
        AnsiConsole.MarkupLine($"[green]Sort order changed to {currentSort}.[/]");
        InputHelper.WaitForKeyPress();
    }

    private void ToggleUnchangedTypes()
    {
        showUnchanged = !showUnchanged;
        filteredDeltas = FilterDeltas();
        
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[green]Show unchanged types: {showUnchanged}[/]");
        InputHelper.WaitForKeyPress();
    }

    private void ExportComparison()
    {
        AnsiConsole.Clear();
        
        var format = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select export format:[/]")
                .AddChoices("JSON", "CSV", "Cancel"));
        
        if (format == "Cancel")
            return;
        
        var filename = AnsiConsole.Ask<string>("Enter filename (without extension):");
        
        try
        {
            if (format == "JSON")
            {
                ExportToJson($"{filename}.json");
            }
            else if (format == "CSV")
            {
                ExportToCsv($"{filename}.csv");
            }
            
            AnsiConsole.MarkupLine($"[green]Successfully exported to {filename}.{format.ToLower()}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Export failed: {Markup.Escape(ex.Message)}[/]");
        }
        
        InputHelper.WaitForKeyPress();
    }

    private void ExportToJson(string filename)
    {
        var output = new
        {
            Comparison = new
            {
                BaselineFile = baselineFile.Name,
                CurrentFile = currentFile.Name,
                comparisonResult.BaselineStats,
                comparisonResult.CurrentStats,
                Deltas = new
                {
                    ObjectCount = comparisonResult.ObjectCountDelta,
                    TotalSize = comparisonResult.TotalSizeDelta,
                    RetainedSize = comparisonResult.RetainedSizeDelta
                }
            },
            TypeChanges = filteredDeltas.Select(d => new
            {
                d.TypeName,
                Status = d.Status.ToString(),
                d.CountDelta,
                d.TotalSizeDelta,
                d.RetainedSizeDelta,
                GrowthPercent = d.BaselineRetainedSize > 0 
                    ? (double)d.RetainedSizeDelta / d.BaselineRetainedSize * 100 
                    : d.RetainedSizeDelta > 0 ? 100.0 : 0.0
            })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        File.WriteAllText(filename, json);
    }

    private void ExportToCsv(string filename)
    {
        using var writer = new StreamWriter(filename);
        writer.WriteLine("TypeName,Status,BaselineCount,CurrentCount,CountDelta,BaselineSize,CurrentSize,SizeDelta,BaselineRetained,CurrentRetained,RetainedDelta,GrowthPercent");
        
        foreach (var delta in filteredDeltas)
        {
            var growthPercent = delta.BaselineRetainedSize > 0 
                ? (double)delta.RetainedSizeDelta / delta.BaselineRetainedSize * 100 
                : delta.RetainedSizeDelta > 0 ? 100.0 : 0.0;
                
            writer.WriteLine($"\"{delta.TypeName}\",{delta.Status},{delta.BaselineCount},{delta.CurrentCount},{delta.CountDelta}," +
                $"{delta.BaselineTotalSize},{delta.CurrentTotalSize},{delta.TotalSizeDelta}," +
                $"{delta.BaselineRetainedSize},{delta.CurrentRetainedSize},{delta.RetainedSizeDelta},{growthPercent:F2}");
        }
    }


    private List<TypeDelta> FilterDeltas()
    {
        var deltas = allDeltas.AsEnumerable();
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            deltas = deltas.Where(d => d.TypeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }
        
        return deltas.ToList();
    }

    private IEnumerable<TypeDelta> GetSortedDeltas()
    {
        var deltas = filteredDeltas.AsEnumerable();
        
        return currentSort switch
        {
            CompareSortBy.RetainedSizeDelta => deltas.OrderByDescending(d => Math.Abs(d.RetainedSizeDelta)),
            CompareSortBy.CountDelta => deltas.OrderByDescending(d => Math.Abs(d.CountDelta)),
            CompareSortBy.TotalSizeDelta => deltas.OrderByDescending(d => Math.Abs(d.TotalSizeDelta)),
            CompareSortBy.GrowthPercent => deltas.OrderByDescending(d => 
                d.BaselineRetainedSize > 0 ? Math.Abs((double)d.RetainedSizeDelta / d.BaselineRetainedSize) : 0),
            _ => deltas
        };
    }

    private string GetStatusMarkup(TypeChangeStatus status)
    {
        return status switch
        {
            TypeChangeStatus.Added => "[green]NEW[/]",
            TypeChangeStatus.Removed => "[red]REMOVED[/]",
            TypeChangeStatus.Changed => "[yellow]CHANGED[/]",
            TypeChangeStatus.Unchanged => "[dim]UNCHANGED[/]",
            _ => "[dim]-[/]"
        };
    }

}