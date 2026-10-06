using ClosedXML.Excel;
using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

/// <summary>
/// One worksheet per section. Sizes are stored as byte counts (sortable, summable) and displayed
/// with a unit-scaling number format; the folder structure uses Excel's row outline so it can be folded.
/// </summary>
public static class ExcelReportWriter
{
    // Bytes shown as KB / MB / GB depending on magnitude; the value stays the exact byte count.
    private const string SizeFormat = "[<1048576]0.0,\" KB\";[<1073741824]0.0,,\" MB\";0.00,,,\" GB\"";
    private const string PercentFormat = "0.0%";
    // Built-in date/time format 22 follows the regional settings of whoever opens the workbook.
    private const int DateTimeFormatId = 22;
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#21253A");
    private static readonly XLColor Data = XLColor.FromHtml(ReportStyle.DataLight);

    public static void Write(StorageReport report, string path)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = $"{report.Title} {report.RootPath}";
        workbook.Properties.Author = "FindLargeFiles";

        WriteSummary(workbook.Worksheets.Add(SheetName("report.sheet_overview")), report);
        if (report.Tree.Count > 0) WriteTree(workbook.Worksheets.Add(SheetName("report.sheet_structure")), report);
        if (report.TopFiles.Count > 0) WriteTopFiles(workbook.Worksheets.Add(SheetName("report.sheet_top_files")), report);
        if (report.Extensions.Count > 0) WriteExtensions(workbook.Worksheets.Add(SheetName("report.sheet_file_types")), report);
        if (report.Ages.Count > 0) WriteAges(workbook.Worksheets.Add(SheetName("report.sheet_file_age")), report);

        workbook.SaveAs(path);
    }

    private static void WriteSummary(IXLWorksheet ws, StorageReport report)
    {
        ws.Cell(1, 1).Value = report.Title;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(18);
        ws.Cell(2, 1).Value = report.RootPath;
        ws.Cell(2, 1).Style.Font.SetFontSize(12);

        var row = 4;
        void Meta(string label, XLCellValue value, string? format = null, int? formatId = null)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.SetFontColor(XLColor.FromHtml("#66655F"));
            var cell = ws.Cell(row, 2);
            cell.Value = value;
            cell.Style.Font.SetBold();
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            if (format != null) cell.Style.NumberFormat.Format = format;
            if (formatId != null) cell.Style.NumberFormat.NumberFormatId = formatId.Value;
            row++;
        }

        Meta(Loc.T("report.created_label"), report.CreatedAt, formatId: DateTimeFormatId);
        Meta(Loc.T("report.total_size"), report.TotalSize, SizeFormat);
        Meta(Loc.T("report.files"), report.FileCount, "#,##0");
        Meta(Loc.T("report.folders"), report.FolderCount, "#,##0");
        Meta(Loc.T("report.inaccessible_folders"), report.InaccessibleFolders, "#,##0");
        if (report.Drive is { } drive)
        {
            Meta(Loc.T("report.drive", ("name", drive.Name)), $"{drive.Label} ({drive.Format})");
            Meta(Loc.T("report.drive_capacity"), drive.TotalSize, SizeFormat);
            Meta(Loc.T("report.drive_free_label"), drive.FreeSpace, SizeFormat);
        }

        row += 1;
        ws.Cell(row, 1).Value = Loc.T("report.breakdown");
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13);
        row++;
        var start = row;
        ws.Cell(row, 1).Value = Loc.T("report.column_element");
        ws.Cell(row, 2).Value = Loc.T("report.column_size");
        ws.Cell(row, 3).Value = Loc.T("report.column_share");
        foreach (var slice in report.Breakdown)
        {
            row++;
            ws.Cell(row, 1).Value = slice.Label;
            ws.Cell(row, 2).Value = slice.Size;
            ws.Cell(row, 3).Value = slice.Share;
            if (slice.Kind != SliceKind.Item) ws.Range(row, 1, row, 3).Style.Font.SetItalic();
        }

        if (report.Breakdown.Count > 0)
        {
            var table = ws.Range(start, 1, row, 3).CreateTable("Breakdown");
            StyleTable(table);
            ws.Range(start + 1, 2, row, 2).Style.NumberFormat.Format = SizeFormat;
            ws.Range(start + 1, 3, row, 3).Style.NumberFormat.Format = PercentFormat;
            ws.Range(start + 1, 3, row, 3).AddConditionalFormat().DataBar(Data, showBarOnly: false).LowestValue().HighestValue();
        }

        ws.Column(1).Width = 42;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 18;
    }

    private static void WriteTree(IXLWorksheet ws, StorageReport report)
    {
        var headers = Columns("column_name", "column_type", "column_size", "column_share_of_folder", "column_share_of_total", "column_files", "column_folders", "column_modified", "column_path");
        Header(ws, headers);
        ws.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;

        var row = 1;
        foreach (var e in report.Tree)
        {
            row++;
            ws.Cell(row, 1).Value = e.Name;
            ws.Cell(row, 1).Style.Alignment.Indent = Math.Min(e.Depth * 2, 15);
            ws.Cell(row, 2).Value = e.Kind;
            ws.Cell(row, 3).Value = e.Size;
            ws.Cell(row, 4).Value = e.ShareOfParent;
            ws.Cell(row, 5).Value = e.ShareOfTotal;
            if (!e.IsSummary) ws.Cell(row, 6).Value = e.FileCount;
            if (!e.IsSummary && e.IsDirectory) ws.Cell(row, 7).Value = e.FolderCount;
            if (e.Modified is { } modified) ws.Cell(row, 8).Value = modified;
            ws.Cell(row, 9).Value = e.Path;

            if (e.IsDirectory) ws.Cell(row, 1).Style.Font.SetBold();
            if (e.IsSummary) ws.Range(row, 1, row, 9).Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#66655F"));
            if (e.Depth > 0) ws.Row(row).OutlineLevel = Math.Min(e.Depth, 7);
        }

        Finish(ws, row, headers.Length, "FolderStructure", sizeColumns: [3], percentColumns: [4, 5], countColumns: [6, 7], dateColumns: [8]);
        ws.Range(2, 4, row, 4).AddConditionalFormat().DataBar(Data, showBarOnly: false).LowestValue().HighestValue();
        ws.Column(1).Width = 50;
        ws.Column(9).Width = 70;
    }

    private static void WriteTopFiles(IXLWorksheet ws, StorageReport report)
    {
        var headers = Columns("column_rank", "column_file", "column_size", "column_modified", "column_folder", "column_path");
        Header(ws, headers);
        var row = 1;
        foreach (var f in report.TopFiles)
        {
            row++;
            ws.Cell(row, 1).Value = f.Rank;
            ws.Cell(row, 2).Value = f.Name;
            ws.Cell(row, 3).Value = f.Size;
            if (f.Modified is { } modified) ws.Cell(row, 4).Value = modified;
            ws.Cell(row, 5).Value = f.Folder;
            ws.Cell(row, 6).Value = f.Path;
        }

        Finish(ws, row, headers.Length, "LargestFiles", sizeColumns: [3], dateColumns: [4]);
        ws.Range(2, 3, row, 3).AddConditionalFormat().DataBar(Data, showBarOnly: false).LowestValue().HighestValue();
        ws.Column(2).Width = 40;
        ws.Column(5).Width = 60;
        ws.Column(6).Width = 80;
    }

    private static void WriteExtensions(IXLWorksheet ws, StorageReport report)
    {
        var headers = Columns("column_type", "column_files", "column_size", "column_share");
        Header(ws, headers);
        var row = 1;
        foreach (var e in report.Extensions)
        {
            row++;
            ws.Cell(row, 1).Value = e.Extension;
            ws.Cell(row, 2).Value = e.FileCount;
            ws.Cell(row, 3).Value = e.Size;
            ws.Cell(row, 4).Value = e.Share;
        }

        Finish(ws, row, headers.Length, "FileTypes", sizeColumns: [3], percentColumns: [4], countColumns: [2]);
        ws.Range(2, 4, row, 4).AddConditionalFormat().DataBar(Data, showBarOnly: false).LowestValue().HighestValue();
    }

    private static void WriteAges(IXLWorksheet ws, StorageReport report)
    {
        var headers = Columns("column_last_modified", "column_files", "column_size", "column_share");
        Header(ws, headers);
        var row = 1;
        foreach (var a in report.Ages)
        {
            row++;
            ws.Cell(row, 1).Value = a.Label;
            ws.Cell(row, 2).Value = a.FileCount;
            ws.Cell(row, 3).Value = a.Size;
            ws.Cell(row, 4).Value = a.Share;
        }

        Finish(ws, row, headers.Length, "FileAge", sizeColumns: [3], percentColumns: [4], countColumns: [2]);
        ws.Range(2, 4, row, 4).AddConditionalFormat().DataBar(Data, showBarOnly: false).LowestValue().HighestValue();
    }

    private static string[] Columns(params string[] keys) => keys.Select(k => Loc.T("report." + k)).ToArray();

    /// <summary>Excel sheet names: at most 31 characters and none of : \ / ? * [ ].</summary>
    private static string SheetName(string key)
    {
        var name = new string(Loc.T(key).Where(c => ":\\/?*[]".IndexOf(c) < 0).ToArray()).Trim();
        return name.Length > 31 ? name[..31] : name;
    }

    private static void Header(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
    }

    private static void Finish(IXLWorksheet ws, int lastRow, int columns, string tableName,
        int[]? sizeColumns = null, int[]? percentColumns = null, int[]? countColumns = null, int[]? dateColumns = null)
    {
        var table = ws.Range(1, 1, Math.Max(lastRow, 2), columns).CreateTable(tableName);
        StyleTable(table);
        if (lastRow >= 2)
        {
            foreach (var c in sizeColumns ?? []) ws.Range(2, c, lastRow, c).Style.NumberFormat.Format = SizeFormat;
            foreach (var c in percentColumns ?? []) ws.Range(2, c, lastRow, c).Style.NumberFormat.Format = PercentFormat;
            foreach (var c in countColumns ?? []) ws.Range(2, c, lastRow, c).Style.NumberFormat.Format = "#,##0";
            foreach (var c in dateColumns ?? []) ws.Range(2, c, lastRow, c).Style.NumberFormat.NumberFormatId = DateTimeFormatId;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns(1, columns).AdjustToContents(1, Math.Min(lastRow, 500), 8, 60);
    }

    private static void StyleTable(IXLTable table)
    {
        table.Theme = XLTableTheme.TableStyleLight1;
        table.ShowRowStripes = false;
        var header = table.HeadersRow().Style;
        header.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(HeaderFill);
    }
}
