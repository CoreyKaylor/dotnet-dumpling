using Spectre.Console;

namespace Dumpling.CLI.Shared.Constants;

public static class UiConstants
{
    public const int DefaultTopTypes = 20;
    public const int DefaultMaxInstances = 3;
    
    public static readonly Style HeaderStyle = Style.Parse("blue");
    public static readonly Color ErrorColor = Color.Red;
    public static readonly Color SuccessColor = Color.Green;
    public static readonly Color InfoColor = Color.Yellow;
    
    public static class ColumnWidths
    {
        public const int Count = 10;
        public const int Size = 12;
        public const int RetainedSize = 14;
        public const int Address = 15;
        public const int Instance = 10;
    }
    
    public static class Messages
    {
        public const string PressAnyKey = "Press any key to continue...";
        public const string NavigationHelp = "Keys: [yellow]↑↓[/] Navigate | [yellow]Enter[/] Select | [yellow]1-9[/] Sort Column | [yellow]Ctrl+F[/] Filter | [yellow]Ctrl+C[/] Clear Filter | [yellow]Esc/Q[/] Back";
    }
}