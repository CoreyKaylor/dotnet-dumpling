using Spectre.Console;
using Dumpling.CLI.Shared.Constants;

namespace Dumpling.CLI.Shared.UI;

public static class StatusDisplay
{
    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[{UiConstants.ErrorColor}]Error:[/] {Markup.Escape(message)}");
    }
    
    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[{UiConstants.SuccessColor}]{Markup.Escape(message)}[/]");
    }
    
    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[{UiConstants.InfoColor}]{Markup.Escape(message)}[/]");
    }
    
    public static void ShowDim(string message)
    {
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(message)}[/]");
    }
    
    public static void ShowHeader(string title)
    {
        var rule = new Rule($"[yellow]{Markup.Escape(title)}[/]");
        rule.Style = UiConstants.HeaderStyle;
        AnsiConsole.Write(rule);
    }
    
    
    public static void ShowSection(string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}:[/]");
    }
    
    public static void Clear()
    {
        AnsiConsole.Clear();
    }
    
    public static void WriteLine()
    {
        AnsiConsole.WriteLine();
    }
    
    public static T ExecuteWithStatus<T>(string statusMessage, Func<T> action)
    {
        T result = default(T)!;
        
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start(statusMessage, ctx =>
            {
                result = action();
            });
            
        return result!;
    }
    
    public static void ExecuteWithStatus(string statusMessage, Action action)
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start(statusMessage, ctx =>
            {
                action();
            });
    }
}