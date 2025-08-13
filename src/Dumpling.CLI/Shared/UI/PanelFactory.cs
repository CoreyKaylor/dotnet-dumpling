using Spectre.Console;
using Dumpling.CLI.Shared.Constants;
using Dumpling.CLI.Shared.Formatters;

namespace Dumpling.CLI.Shared.UI;

public static class PanelFactory
{
    public static Panel CreateHeaderPanel(string title, string content)
    {
        return new Panel(new Markup(content))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            BorderStyle = UiConstants.HeaderStyle
        };
    }
    
    public static Panel CreateSummaryPanel(string content)
    {
        return new Panel(new Markup(content))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = UiConstants.HeaderStyle
        };
    }
    
    public static Panel CreateHeapAnalysisPanel(string fileName, int totalObjects, ulong totalSize, ulong totalRetainedSize)
    {
        var content = $"[bold yellow]Heap Analysis Results[/]\n" +
                     $"File: [cyan]{Markup.Escape(fileName)}[/]\n" +
                     $"Total Objects: [green]{totalObjects:N0}[/]\n" +
                     $"Total Size: [green]{ByteFormatter.FormatBytes(totalSize)}[/]\n" +
                     $"Total Retained Size: [green]{ByteFormatter.FormatBytes(totalRetainedSize)}[/]";
        
        return CreateSummaryPanel(content);
    }
    
    public static Panel CreateComparisonPanel(string baselineFile, string currentFile, DateTime baselineTime, DateTime currentTime, long objectCountDelta, long totalSizeDelta, long retainedSizeDelta)
    {
        var content = $"[bold yellow]Heap Comparison Results[/]\n" +
                     $"Baseline: [cyan]{Markup.Escape(baselineFile)}[/] ({baselineTime:yyyy-MM-dd HH:mm})\n" +
                     $"Current: [cyan]{Markup.Escape(currentFile)}[/] ({currentTime:yyyy-MM-dd HH:mm})\n" +
                     $"Time Diff: [green]{(currentTime - baselineTime).TotalHours:F1} hours[/]\n" +
                     $"\n" +
                     $"Objects Δ: [{(objectCountDelta >= 0 ? "red" : "green")}]{objectCountDelta:+#,0;-#,0;0}[/]\n" +
                     $"Size Δ: [{(totalSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(totalSizeDelta)}[/]\n" +
                     $"Retained Δ: [{(retainedSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(retainedSizeDelta)}[/]";
        
        return CreateSummaryPanel(content);
    }
    
    public static Panel CreateTypeAnalysisPanel(string typeName, string status, int baselineCount, int currentCount, int countDelta, ulong baselineTotalSize, ulong currentTotalSize, long totalSizeDelta, ulong baselineRetainedSize, ulong currentRetainedSize, long retainedSizeDelta)
    {
        var content = $"[bold yellow]Type Analysis: {Markup.Escape(typeName)}[/]\n" +
                     $"Status: {status}\n" +
                     $"Count: {baselineCount:N0} → {currentCount:N0} ([{(countDelta >= 0 ? "red" : "green")}]{countDelta:+#,0;-#,0;0}[/])\n" +
                     $"Size: {ByteFormatter.FormatBytes(baselineTotalSize)} → {ByteFormatter.FormatBytes(currentTotalSize)} ([{(totalSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(totalSizeDelta)}[/])\n" +
                     $"Retained: {ByteFormatter.FormatBytes(baselineRetainedSize)} → {ByteFormatter.FormatBytes(currentRetainedSize)} ([{(retainedSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(retainedSizeDelta)}[/])";
        
        return CreateSummaryPanel(content);
    }
    
    public static Panel CreateTypeSummaryPanel(string typeName, int count, ulong totalSize, ulong retainedSize, ulong avgSizePerInstance)
    {
        var content = $"Total Instances: [green]{count:N0}[/]\n" +
                     $"Total Size: [green]{ByteFormatter.FormatBytes(totalSize)}[/]\n" +
                     $"Total Retained Size: [green]{ByteFormatter.FormatBytes(retainedSize)}[/]\n" +
                     $"Avg Size per Instance: [green]{ByteFormatter.FormatBytes(avgSizePerInstance)}[/]";
        
        return CreateHeaderPanel("Type Summary", content);
    }
    
    public static Panel CreateComparisonHeaderPanel(string baselineFile, string currentFile, long retainedSizeDelta)
    {
        var content = $"[bold yellow]Heap Comparison - Interactive Mode[/]\n" +
                     $"Baseline: [cyan]{Markup.Escape(baselineFile)}[/]\n" +
                     $"Current: [cyan]{Markup.Escape(currentFile)}[/]\n" +
                     $"Total Delta: [{(retainedSizeDelta >= 0 ? "red" : "green")}]{ByteFormatter.FormatBytesDelta(retainedSizeDelta)}[/]";
        
        return new Panel(new Markup(content))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = UiConstants.HeaderStyle
        };
    }
}