using Spectre.Console;
using Dumpling.CLI.Shared.Constants;

namespace Dumpling.CLI.Shared.UI;

public static class InputHelper
{
    public static void WaitForKeyPress(string message = UiConstants.Messages.PressAnyKey)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]{message}[/]");
        Console.ReadKey(true);
    }
    
    public static string GetSearchTerm(string prompt = "Enter search term:")
    {
        return AnsiConsole.Ask<string>(prompt);
    }
    
    public static string GetFilename(string prompt = "Enter filename (without extension):")
    {
        return AnsiConsole.Ask<string>(prompt);
    }
    
    public static double GetMinSizeKB(double defaultValue = 100.0)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<double>("[yellow]Minimum retained size in KB:[/]")
                .DefaultValue(defaultValue)
                .ValidationErrorMessage("[red]Please enter a valid number[/]"));
    }
    
    public static int GetMinCount(int defaultValue = 100)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<int>("[yellow]Minimum instance count:[/]")
                .DefaultValue(defaultValue)
                .ValidationErrorMessage("[red]Please enter a valid number[/]"));
    }
    
    public static string GetTypeNameFilter()
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Type name contains:[/]")
                .AllowEmpty());
    }
    
    public static bool ConfirmAction(string message)
    {
        return AnsiConsole.Confirm(message);
    }
    
    public static void ShowNavigationHelp()
    {
        AnsiConsole.MarkupLine($"[dim]{UiConstants.Messages.NavigationHelp}[/]");
    }
}