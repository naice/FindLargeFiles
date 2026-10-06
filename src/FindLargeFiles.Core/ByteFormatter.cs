using System.Globalization;

namespace FindLargeFiles.Core;

public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>Formats a byte count with binary multiples, e.g. "12,4 GB".</summary>
    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024) return string.Create(culture, $"{bytes} B");

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value >= 100 ? "N0" : value >= 10 ? "N1" : "N2";
        return value.ToString(format, culture) + " " + Units[unit];
    }
}
