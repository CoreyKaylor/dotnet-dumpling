namespace Dumpling.CLI.Shared.Formatters;

public static class CountFormatter
{
    public static string FormatCountDelta(int count)
    {
        return count >= 0 ? $"+{count:N0}" : $"{count:N0}";
    }

    public static string FormatPercentage(double percent)
    {
        if (double.IsInfinity(percent) || double.IsNaN(percent))
            return "N/A";
        return percent >= 0 ? $"+{percent:F1}%" : $"{percent:F1}%";
    }
}