using System.Text.RegularExpressions;
using FindLargeFiles.Core;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

/// <summary>A4 report: summary tiles, donut chart with legend, then one table per section.</summary>
public static class PdfReportWriter
{
    private const string Text = "#1B1B19";
    private const string Muted = "#66655F";
    private const string Border = "#E3E2DC";
    private const string Track = "#ECEBE6";

    static PdfReportWriter()
    {
        // FindLargeFiles is developed by an individual (PolyForm Noncommercial); the QuestPDF Community license covers that.
        // If commercial licenses of FindLargeFiles ever bring in more than USD 1M a year, QuestPDF needs a paid license.
        QuestPDF.Settings.License = LicenseType.Community;
        // The app only runs on Windows, where Segoe UI is always installed; Lato ships with QuestPDF as fallback.
        QuestPDF.Settings.UseSystemFonts = true;
    }

    public static void Write(StorageReport report, string path) => Create(report).GeneratePdf(path);

    public static byte[] Render(StorageReport report) => Create(report).GeneratePdf();

    private static Document Create(StorageReport report) => Document.Create(doc =>
    {
        doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontFamily("Segoe UI", "Nirmala UI", "Lato").FontSize(8.5f).FontColor(Text));

            page.Header().ShowOnce().PaddingBottom(14).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(report.Title.ToUpperInvariant()).FontSize(8).SemiBold().FontColor(Muted).LetterSpacing(0.08f);
                    col.Item().Text(report.RootPath).FontSize(17).SemiBold();
                    col.Item().Text(Loc.T("report.created", ("date", ReportStyle.LongDate(report.CreatedAt)))).FontColor(Muted);
                });
            });

            page.Content().Column(col =>
            {
                col.Spacing(16);
                col.Item().Element(c => Tiles(c, report));
                col.Item().Element(c => Breakdown(c, report));
                if (report.Tree.Count > 0) col.Item().Element(c => Tree(c, report));
                if (report.TopFiles.Count > 0) col.Item().Element(c => TopFiles(c, report));
                if (report.Extensions.Count > 0) col.Item().Element(c => Extensions(c, report));
                if (report.Ages.Count > 0) col.Item().Element(c => Ages(c, report));
            });

            page.Footer().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(Loc.T("report.footer_note")).FontSize(7).FontColor(Muted);
                row.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                    // "Page {page} of {total}" – split at the placeholders so every language keeps its word order.
                    foreach (var part in Regex.Split(Loc.T("report.page_number"), @"(\{page\}|\{total\})"))
                    {
                        if (part == "{page}") t.CurrentPageNumber();
                        else if (part == "{total}") t.TotalPages();
                        else if (part.Length > 0) t.Span(part);
                    }
                });
            });
        });
    });

    private static void Tiles(IContainer container, StorageReport report)
    {
        container.Row(row =>
        {
            row.Spacing(8);
            Tile(row, Loc.T("report.total_size"), ReportStyle.Bytes(report.TotalSize));
            Tile(row, Loc.T("report.files"), ReportStyle.Number(report.FileCount));
            Tile(row, Loc.T("report.folders"), ReportStyle.Number(report.FolderCount));
            if (report.Drive is { } d)
                Tile(row, Loc.T("report.drive", ("name", d.Name)), Loc.T("report.drive_free", ("free", ReportStyle.Bytes(d.FreeSpace))),
                    Loc.T("report.drive_of_total", ("total", ReportStyle.Bytes(d.TotalSize))));
            if (report.InaccessibleFolders > 0)
                Tile(row, Loc.T("report.inaccessible"), ReportStyle.Number(report.InaccessibleFolders), Loc.T("report.inaccessible_detail"));
        });
    }

    private static void Tile(RowDescriptor row, string label, string value, string? detail = null)
    {
        row.RelativeItem().Border(0.75f).BorderColor(Border).CornerRadius(6).Padding(9).Column(col =>
        {
            col.Item().Text(label).FontSize(7.5f).FontColor(Muted);
            col.Item().Text(value).FontSize(13).SemiBold();
            if (detail != null) col.Item().Text(detail).FontSize(7.5f).FontColor(Muted);
        });
    }

    private static void Breakdown(IContainer container, StorageReport report)
    {
        var slices = report.Breakdown;
        container.Column(col =>
        {
            col.Item().Element(Heading).Text(Loc.T("report.breakdown"));
            if (slices.Count == 0)
            {
                col.Item().Text(Loc.T("report.no_data")).FontColor(Muted);
                return;
            }

            string Color(int i) => ReportStyle.SliceColor(slices, i, ReportStyle.LightSeries, ReportStyle.OtherLight, ReportStyle.FreeLight);
            var svg = ReportStyle.DonutSvg(slices, Color, "#FFFFFF", Loc.T("chart.center_used"),
                ReportStyle.Bytes(ReportStyle.UsedInBreakdown(report)), Text, Muted, 220);

            col.Item().Row(row =>
            {
                row.ConstantItem(150).Height(150).Svg(svg);
                row.ConstantItem(20);
                row.RelativeItem().AlignMiddle().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(14);
                        c.RelativeColumn();
                        c.ConstantColumn(64);
                        c.ConstantColumn(48);
                    });
                    for (var i = 0; i < slices.Count; i++)
                    {
                        var slice = slices[i];
                        table.Cell().Element(Cell).AlignMiddle().Width(8).Height(8).Background(Color(i))
                            .Border(slice.Kind == SliceKind.FreeSpace ? 0.75f : 0).BorderColor(Muted);
                        table.Cell().Element(Cell).Text(slice.Label);
                        table.Cell().Element(Cell).AlignRight().Text(ReportStyle.Bytes(slice.Size)).SemiBold();
                        table.Cell().Element(Cell).AlignRight().Text(ReportStyle.Percent(slice.Share)).FontColor(Muted);
                    }
                });
            });
        });
    }

    private static void Tree(IContainer container, StorageReport report)
    {
        var o = report.Options;
        container.Column(col =>
        {
            col.Item().Element(Heading).Text(Loc.T("report.structure"));
            col.Item().PaddingBottom(6).Text(Loc.T("report.structure_note", ("depth", o.TreeDepth), ("share", ReportStyle.Percent(o.MinShare))))
                .FontColor(Muted);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.ConstantColumn(58);
                    c.ConstantColumn(92);
                    c.ConstantColumn(52);
                    c.ConstantColumn(44);
                    c.ConstantColumn(66);
                });
                table.Header(h =>
                {
                    HeaderCell(h, Loc.T("report.column_name"));
                    HeaderCell(h, Loc.T("report.column_size"), right: true);
                    HeaderCell(h, Loc.T("report.column_share_of_folder"));
                    HeaderCell(h, Loc.T("report.column_files"), right: true);
                    HeaderCell(h, Loc.T("report.column_folders"), right: true);
                    HeaderCell(h, Loc.T("report.column_modified"));
                });

                foreach (var e in report.Tree)
                {
                    table.Cell().Element(Cell).PaddingLeft(Math.Min(e.Depth, 8) * 10).Text(t =>
                    {
                        t.ClampLines(1);
                        var span = t.Span(e.Name);
                        if (e.IsSummary) span.Italic().FontColor(Muted);
                        else if (e.IsDirectory) span.SemiBold();
                        if (e.IsInaccessible) t.Span("  " + Loc.T("report.no_access_badge")).FontSize(7).FontColor("#B25E00");
                    });
                    table.Cell().Element(Cell).AlignRight().Text(ReportStyle.Bytes(e.Size)).SemiBold();
                    table.Cell().Element(Cell).Element(c => ShareBar(c, e.ShareOfParent));
                    table.Cell().Element(Cell).AlignRight().Text(e.IsSummary ? "" : ReportStyle.Number(e.FileCount)).FontColor(Muted);
                    table.Cell().Element(Cell).AlignRight().Text(e.IsSummary || !e.IsDirectory ? "" : ReportStyle.Number(e.FolderCount)).FontColor(Muted);
                    table.Cell().Element(Cell).Text(ShortDate(e.Modified)).FontColor(Muted);
                }
            });
        });
    }

    private static void TopFiles(IContainer container, StorageReport report)
    {
        container.Column(col =>
        {
            col.Item().Element(Heading).Text(Loc.T("report.top_files", ("count", report.TopFiles.Count)));
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(24);
                    c.RelativeColumn(5);
                    c.ConstantColumn(58);
                    c.ConstantColumn(66);
                });
                table.Header(h =>
                {
                    HeaderCell(h, "#", right: true);
                    HeaderCell(h, Loc.T("report.column_file"));
                    HeaderCell(h, Loc.T("report.column_size"), right: true);
                    HeaderCell(h, Loc.T("report.column_modified"));
                });
                foreach (var f in report.TopFiles)
                {
                    table.Cell().Element(Cell).AlignRight().Text(f.Rank.ToString(ReportStyle.Culture)).FontColor(Muted);
                    table.Cell().Element(Cell).Column(c =>
                    {
                        c.Item().Text(f.Name);
                        c.Item().Text(f.Folder).FontSize(7).FontColor(Muted).ClampLines(1);
                    });
                    table.Cell().Element(Cell).AlignRight().Text(ReportStyle.Bytes(f.Size)).SemiBold();
                    table.Cell().Element(Cell).Text(ShortDate(f.Modified)).FontColor(Muted);
                }
            });
        });
    }

    private static void Extensions(IContainer container, StorageReport report)
    {
        container.Column(col =>
        {
            col.Item().Element(Heading).Text(Loc.T("report.file_types"));
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.ConstantColumn(64);
                    c.ConstantColumn(64);
                    c.RelativeColumn(3);
                });
                table.Header(h =>
                {
                    HeaderCell(h, Loc.T("report.column_type"));
                    HeaderCell(h, Loc.T("report.column_files"), right: true);
                    HeaderCell(h, Loc.T("report.column_size"), right: true);
                    HeaderCell(h, Loc.T("report.column_share"));
                });
                foreach (var e in report.Extensions)
                {
                    table.Cell().Element(Cell).Text(e.Extension).FontFamily("Consolas", "Nirmala UI", "Lato");
                    table.Cell().Element(Cell).AlignRight().Text(ReportStyle.Number(e.FileCount)).FontColor(Muted);
                    table.Cell().Element(Cell).AlignRight().Text(ReportStyle.Bytes(e.Size)).SemiBold();
                    table.Cell().Element(Cell).PaddingLeft(10).Element(c => ShareBar(c, e.Share));
                }
            });
        });
    }

    private static void Ages(IContainer container, StorageReport report)
    {
        var max = Math.Max(1, report.Ages.Max(a => a.Size));
        container.ShowEntire().Column(col =>
        {
            col.Item().Element(Heading).Text(Loc.T("report.file_age"));
            col.Item().PaddingBottom(6).Text(Loc.T("report.file_age_note"))
                .FontColor(Muted);
            foreach (var a in report.Ages)
            {
                col.Item().PaddingVertical(3).Row(row =>
                {
                    row.ConstantItem(70).AlignMiddle().Text(a.Label);
                    row.RelativeItem().AlignMiddle().Height(10).Row(bar =>
                    {
                        var fraction = (float)a.Size / max;
                        if (fraction > 0) bar.RelativeItem(Math.Max(fraction, 0.004f)).Background(ReportStyle.DataLight);
                        if (fraction < 1) bar.RelativeItem(1 - fraction);
                    });
                    row.ConstantItem(150).AlignMiddle().AlignRight().Text(t =>
                    {
                        t.Span(ReportStyle.Bytes(a.Size)).SemiBold();
                        t.Span($"  {ReportStyle.Percent(a.Share)}  ·  {Loc.Plural("global.file_count", a.FileCount)}").FontColor(Muted);
                    });
                });
            }
        });
    }

    private static void ShareBar(IContainer container, double share)
    {
        var fraction = (float)Math.Clamp(share, 0, 1);
        container.Row(row =>
        {
            row.RelativeItem().AlignMiddle().Height(5).Background(Track).Row(bar =>
            {
                if (fraction > 0) bar.RelativeItem(Math.Max(fraction, 0.01f)).Background(ReportStyle.DataLight);
                if (fraction < 1) bar.RelativeItem(1 - fraction);
            });
            row.ConstantItem(36).AlignMiddle().AlignRight().Text(ReportStyle.Percent(share)).FontSize(7.5f).FontColor(Muted);
        });
    }

    private static string ShortDate(DateTime? value) => value?.ToString("d", ReportStyle.Culture) ?? string.Empty;

    private static IContainer Heading(IContainer c) => c.PaddingBottom(6).DefaultTextStyle(t => t.FontSize(12).SemiBold());

    private static IContainer Cell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Border).PaddingVertical(3).PaddingHorizontal(3);

    private static void HeaderCell(TableCellDescriptor header, string text, bool right = false)
    {
        var cell = header.Cell().BorderBottom(1).BorderColor(Muted).PaddingVertical(4).PaddingHorizontal(3);
        if (right) cell = cell.AlignRight();
        cell.Text(text).FontSize(7.5f).SemiBold().FontColor(Muted);
    }
}
