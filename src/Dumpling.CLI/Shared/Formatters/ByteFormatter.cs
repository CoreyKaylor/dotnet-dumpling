namespace Dumpling.CLI.Shared.Formatters;

public static class ByteFormatter
{
    public static string FormatBytes(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F2} {sizes[order]}";
    }

    public static string FormatBytesDelta(long bytes)
    {
        var absBytes = Math.Abs(bytes);
        var formatted = FormatBytes((ulong)absBytes);
        return bytes >= 0 ? $"+{formatted}" : $"-{formatted}";
    }
}