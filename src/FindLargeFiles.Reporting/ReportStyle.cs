using System.Globalization;
using System.Net;
using System.Text;
using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

/// <summary>Colors and formatting shared by all report writers.</summary>
public static class ReportStyle
{
    /// <summary>Categorical slots for light surfaces (same hues as the app's dark palette).</summary>
    public static readonly string[] LightSeries =
        ["#2A78D6", "#EB6834", "#1BAF7A", "#EDA100", "#E87BA4", "#008300", "#4A3AA7", "#E34948"];

    public static readonly string[] DarkSeries =
        ["#3987E5", "#D95926", "#199E70", "#C98500", "#D55181", "#008300", "#9085E9", "#E66767"];

    public const string OtherLight = "#9A9890";
    public const string FreeLight = "#E4E2DC";
    public const string DataLight = "#2A78D6";

    public static CultureInfo Culture => CultureInfo.CurrentCulture;

    public static string Bytes(long bytes) => ByteFormatter.Format(bytes, Culture);
    public static string Percent(double share) => share.ToString("P1", Culture);
    public static string Number(long value) => value.ToString("N0", Culture);
    public static string Date(DateTime? value) => value?.ToString("g", Culture) ?? string.Empty;

    /// <summary>Spelled-out date for sentences: month and weekday names in the UI language, not the regional format's.</summary>
    public static string LongDate(DateTime value) => value.ToString("f", Loc.Current.Culture);

    /// <summary>Color slot per slice: items take series slots in order, "other" and free space are neutral.</summary>
    public static string SliceColor(IReadOnlyList<ReportSlice> slices, int index, string[] series, string other, string free)
    {
        var slice = slices[index];
        if (slice.Kind == SliceKind.Other) return other;
        if (slice.Kind == SliceKind.FreeSpace) return free;
        var slot = slices.Take(index).Count(s => s.Kind == SliceKind.Item);
        return series[slot % series.Length];
    }

    public static string FileName(StorageReport report, ReportFormat format) =>
        FileName(report.RootPath, report.CreatedAt, format);

    public static string FileName(string rootPath, DateTime created, ReportFormat format)
    {
        // "D:\Source\Projekte" -> "D Source Projekte"
        var parts = rootPath.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = string.Join(' ', parts);
        var ext = format switch
        {
            ReportFormat.Excel => ".xlsx",
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Csv => ".csv",
            _ => ".html",
        };
        return Loc.T("report.file_name", ("name", name), ("date", created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))) + ext;
    }

    /// <summary>
    /// Donut chart as standalone SVG. <paramref name="fill"/> returns the fill for slice i
    /// (a color or a CSS expression such as <c>var(--s1)</c>).
    /// </summary>
    public static string DonutSvg(IReadOnlyList<ReportSlice> slices, Func<int, string> fill, string gap,
        string centerLabel, string centerValue, string textColor, string mutedColor, double size = 220, string? extraDefs = null)
    {
        var ic = CultureInfo.InvariantCulture;
        var c = size / 2;
        var r = size / 2 - 4;
        var inner = r * 0.62;
        var sb = new StringBuilder();
        sb.Append(ic, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(Loc.T("report.breakdown_aria"))}\">");
        if (extraDefs != null) sb.Append("<defs>").Append(extraDefs).Append("</defs>");

        var start = -90.0;
        for (var i = 0; i < slices.Count; i++)
        {
            var sweep = slices[i].Share * 360;
            if (sweep <= 0) continue;
            var title = WebUtility.HtmlEncode($"{slices[i].Label}: {Bytes(slices[i].Size)} ({Percent(slices[i].Share)})");
            if (sweep >= 359.99)
            {
                sb.Append(ic, $"<circle cx=\"{c}\" cy=\"{c}\" r=\"{(r + inner) / 2:0.##}\" fill=\"none\" stroke=\"{fill(i)}\" stroke-width=\"{r - inner:0.##}\"><title>{title}</title></circle>");
            }
            else
            {
                var end = start + sweep;
                var large = sweep > 180 ? 1 : 0;
                var (x1, y1) = At(c, r, start);
                var (x2, y2) = At(c, r, end);
                var (x3, y3) = At(c, inner, end);
                var (x4, y4) = At(c, inner, start);
                sb.Append(ic, $"<path d=\"M{x1:0.##},{y1:0.##} A{r:0.##},{r:0.##} 0 {large} 1 {x2:0.##},{y2:0.##} L{x3:0.##},{y3:0.##} A{inner:0.##},{inner:0.##} 0 {large} 0 {x4:0.##},{y4:0.##} Z\" fill=\"{fill(i)}\" stroke=\"{gap}\" stroke-width=\"2\"><title>{title}</title></path>");
            }

            start += sweep;
        }

        sb.Append(ic, $"<text x=\"{c}\" y=\"{c - 6:0.##}\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"11\" fill=\"{mutedColor}\">{WebUtility.HtmlEncode(centerLabel)}</text>");
        sb.Append(ic, $"<text x=\"{c}\" y=\"{c + 16:0.##}\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"20\" font-weight=\"600\" fill=\"{textColor}\">{WebUtility.HtmlEncode(centerValue)}</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static (double X, double Y) At(double c, double radius, double deg)
    {
        var rad = deg * Math.PI / 180;
        return (c + Math.Cos(rad) * radius, c + Math.Sin(rad) * radius);
    }

    public static long UsedInBreakdown(StorageReport report) =>
        report.Breakdown.Where(s => s.Kind != SliceKind.FreeSpace).Sum(s => s.Size);
}
