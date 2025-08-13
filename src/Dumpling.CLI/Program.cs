using System.CommandLine;
using Dumpling.CLI.Commands;
using Spectre.Console;

namespace Dumpling.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Display banner
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            DisplayBanner();
        }

        var rootCommand = new RootCommand("🥟 Dumpling - A delightful CLI tool for analyzing .NET memory dumps");
        
        // Add commands
        rootCommand.AddCommand(new AnalyzeCommand());
        rootCommand.AddCommand(new CompareCommand());

        return await rootCommand.InvokeAsync(args);
    }

    private static void DisplayBanner()
    {
        var banner = new FigletText("Dumpling")
            .LeftJustified()
            .Color(Color.Yellow);
        
        AnsiConsole.Write(banner);
        AnsiConsole.MarkupLine("[dim]A delightful CLI tool for analyzing .NET memory dumps[/]");
        AnsiConsole.WriteLine();
    }
}