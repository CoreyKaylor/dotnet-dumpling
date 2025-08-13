using Spectre.Console;
using Dumpling.CLI.Shared.Constants;

namespace Dumpling.CLI.Shared.UI;

public static class TableFactory
{
    public static Table CreateStandardTable()
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.ShowRowSeparators = true;
        table.Expand = false;
        return table;
    }
    
    public static Table CreateTypeStatisticsTable()
    {
        var table = CreateStandardTable();
        table.AddColumn("Type");
        table.AddColumn(new TableColumn("Count").RightAligned());
        table.AddColumn(new TableColumn("Total Size").RightAligned());
        table.AddColumn(new TableColumn("Retained Size").RightAligned());
        table.AddColumn(new TableColumn("% of Heap").RightAligned());
        return table;
    }
    
    public static Table CreateComparisonTable()
    {
        var table = CreateStandardTable();
        table.AddColumn("Type");
        table.AddColumn(new TableColumn("Count Δ").RightAligned());
        table.AddColumn(new TableColumn("Size Δ").RightAligned());
        table.AddColumn(new TableColumn("Retained Δ").RightAligned());
        table.AddColumn(new TableColumn("Growth %").RightAligned());
        table.AddColumn(new TableColumn("Status").Centered());
        return table;
    }
    
    public static Table CreateInstanceTable()
    {
        var table = CreateStandardTable();
        table.AddColumn(new TableColumn("Instance").Width(UiConstants.ColumnWidths.Instance));
        table.AddColumn(new TableColumn("Address").RightAligned().Width(UiConstants.ColumnWidths.Address));
        table.AddColumn(new TableColumn("Size").RightAligned().Width(UiConstants.ColumnWidths.Size));
        table.AddColumn(new TableColumn("Retained Size").RightAligned().Width(UiConstants.ColumnWidths.RetainedSize));
        return table;
    }
    
    public static Table CreateCounterTable()
    {
        var table = CreateStandardTable();
        table.AddColumn("Counter");
        table.AddColumn(new TableColumn("Value").RightAligned());
        return table;
    }
    
    public static Table CreateFileSelectionTable()
    {
        var table = CreateStandardTable();
        table.AddColumn("#");
        table.AddColumn("File Name");
        table.AddColumn("Size");
        table.AddColumn("Modified");
        table.AddColumn("Age");
        return table;
    }
    
    public static Table CreateRetainerTable()
    {
        var table = CreateStandardTable();
        table.AddColumn("Retaining Type");
        table.AddColumn(new TableColumn("Unique Objects").RightAligned());
        table.AddColumn(new TableColumn("References").RightAligned());
        table.AddColumn(new TableColumn("Avg Refs").RightAligned());
        table.AddColumn(new TableColumn("Total Retained").RightAligned());
        return table;
    }
    
    public static Table CreateReferencePathTable()
    {
        var table = CreateStandardTable();
        table.AddColumn(new TableColumn("Reference Path").Width(60));
        table.AddColumn(new TableColumn("Count").RightAligned().Width(UiConstants.ColumnWidths.Count));
        table.AddColumn(new TableColumn("% of Sample").RightAligned().Width(UiConstants.ColumnWidths.Size));
        return table;
    }
}