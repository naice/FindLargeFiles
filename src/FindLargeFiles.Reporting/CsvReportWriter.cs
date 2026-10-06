using System.Globalization;
using System.Text;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

/// <summary>
/// Writes the folder structure to the chosen file and every further section to its own file next to it
/// ("… - Largest files.csv"), because CSV holds exactly one table. Semicolon-separated UTF-8 with BOM,
/// which Excel opens directly in German locales.
/// </summary>
public static class CsvReportWriter
{
    private const char Separator = ';';

    public static IReadOnlyList<string> Write(StorageReport report, string path)
    {
        var written = new List<string>();
        var dir = Path.GetDirectoryName(path) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(path);
        string Sibling(string sheetKey) => Path.Combine(dir, $"{stem} - {Loc.T(sheetKey)}.csv");
        string[] Columns(params string[] keys) => keys.Select(k => Loc.T("report." + k)).ToArray();

        var tree = report.Tree.Count > 0 ? report.Tree : [new ReportEntry(report.RootPath, report.RootPath, 0, true,
            report.TotalSize, 1, 1, report.FileCount, report.FolderCount, null)];
        WriteTable(path, written,
            Columns("column_path", "column_name", "column_type", "column_level", "column_size_bytes", "column_size",
                "column_share_of_folder_percent", "column_share_of_total_percent", "column_files", "column_folders", "column_modified"),
            tree.Select(e => new object?[]
            {
                e.Path, e.Name, e.Kind, e.Depth, e.Size, ReportStyle.Bytes(e.Size), e.ShareOfParent, e.ShareOfTotal,
                e.IsSummary ? null : e.FileCount, e.IsSummary || !e.IsDirectory ? null : e.FolderCount, e.Modified,
            }));

        if (report.TopFiles.Count > 0)
        {
            WriteTable(Sibling("report.sheet_top_files"), written,
                Columns("column_rank", "column_name", "column_size_bytes", "column_size", "column_modified", "column_folder", "column_path"),
                report.TopFiles.Select(f => new object?[] { f.Rank, f.Name, f.Size, ReportStyle.Bytes(f.Size), f.Modified, f.Folder, f.Path }));
        }

        if (report.Extensions.Count > 0)
        {
            WriteTable(Sibling("report.sheet_file_types"), written,
                Columns("column_type", "column_files", "column_size_bytes", "column_size", "column_share_percent"),
                report.Extensions.Select(e => new object?[] { e.Extension, e.FileCount, e.Size, ReportStyle.Bytes(e.Size), e.Share }));
        }

        if (report.Ages.Count > 0)
        {
            WriteTable(Sibling("report.sheet_file_age"), written,
                Columns("column_period", "column_files", "column_size_bytes", "column_size", "column_share_percent"),
                report.Ages.Select(a => new object?[] { a.Label, a.FileCount, a.Size, ReportStyle.Bytes(a.Size), a.Share }));
        }

        return written;
    }

    private static void WriteTable(string path, List<string> written, string[] header, IEnumerable<object?[]> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine(string.Join(Separator, header.Select(Escape)));
        foreach (var row in rows) writer.WriteLine(string.Join(Separator, row.Select(Format)));
        written.Add(path);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        double share => Escape(Math.Round(share * 100, 2).ToString(CultureInfo.CurrentCulture)),
        DateTime date => date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => Escape(value.ToString() ?? string.Empty),
    };

    internal static string Escape(string value)
    {
        // Leading =, +, -, @ would be evaluated as formulas by spreadsheet apps.
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([Separator, '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
