using ClosedXML.Excel;
using FindLargeFiles.Core;
using FindLargeFiles.Reporting;

namespace FindLargeFiles.Core.Tests;

public sealed class ReportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flf-report-" + Guid.NewGuid().ToString("N"));
    private readonly string _out = Path.Combine(Path.GetTempPath(), "flf-out-" + Guid.NewGuid().ToString("N"));

    public ReportTests()
    {
        Directory.CreateDirectory(_out);
        Write("videos/a.mp4", 80_000);
        Write("videos/old/b.mp4", 15_000);
        Write("docs/c.pdf", 4_000);
        Write("docs/=formula.txt", 900);
        Write("tiny.txt", 10);
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _root, _out })
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
    }

    private void Write(string relative, int bytes)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    private async Task<StorageReport> BuildAsync(ReportOptions? options = null)
    {
        var root = (await new DirectoryScanner().ScanAsync(_root)).Root;
        return ReportBuilder.Build(root, options ?? new ReportOptions(), now: new DateTime(2026, 10, 6, 12, 0, 0));
    }

    [Fact]
    public async Task Builder_FlattensTreeAndFoldsSmallEntries()
    {
        var report = await BuildAsync(new ReportOptions { TreeDepth = 2, MinShare = 0.01 });

        Assert.Equal(99_910, report.TotalSize);
        Assert.Equal(5, report.FileCount);
        Assert.Equal(0, report.Tree[0].Depth);
        Assert.Equal("videos", report.Tree[1].Name);
        Assert.All(report.Tree, e => Assert.True(e.Depth <= 2));

        // tiny.txt (0.01 %) and =formula.txt (0.9 %) fall below 1 % and are folded.
        Assert.DoesNotContain(report.Tree, e => e.Name == "tiny.txt");
        Assert.Contains(report.Tree, e => e.IsSummary && e.Depth == 1 && e.Size == 10);
        Assert.Contains(report.Tree, e => e.IsSummary && e.Depth == 2 && e.Size == 900);

        Assert.Equal("a.mp4", report.TopFiles[0].Name);
        Assert.Equal(".mp4", report.Extensions[0].Extension);
        Assert.Equal(5, report.Ages.Sum(a => a.FileCount));
    }

    [Fact]
    public async Task Builder_SkipsDisabledSections()
    {
        var report = await BuildAsync(new ReportOptions { IncludeTree = false, IncludeTopFiles = false, IncludeExtensions = false, IncludeAge = false });
        Assert.Empty(report.Tree);
        Assert.Empty(report.TopFiles);
        Assert.Empty(report.Extensions);
        Assert.Empty(report.Ages);
        Assert.NotEmpty(report.Breakdown);
    }

    [Fact]
    public async Task Csv_WritesOneFilePerSectionAndNeutralizesFormulas()
    {
        var report = await BuildAsync(new ReportOptions { MinShare = 0 });
        var path = Path.Combine(_out, "report.csv");

        var files = ReportExporter.Export(report, ReportFormat.Csv, path);

        Assert.Equal(4, files.Count);
        Assert.All(files, f => Assert.True(File.Exists(f)));
        var lines = File.ReadAllLines(path);
        Assert.StartsWith("Path;Name;Type;Level;Size (bytes)", lines[0]);
        Assert.Contains(lines, l => l.Contains(";'=formula.txt;"));
        Assert.Contains(lines, l => l.Contains(";a.mp4;File;2;80000;"));
    }

    [Fact]
    public async Task Html_IsSelfContainedAndEncodesNames()
    {
        var report = await BuildAsync(new ReportOptions { MinShare = 0 });
        var html = HtmlReportWriter.Render(report);

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<svg", html);
        Assert.Contains("a.mp4", html);
        Assert.Contains("table class=\"tree\"", html);
        Assert.DoesNotContain("<script src", html);
        Assert.DoesNotContain("<link", html);
    }

    [Fact]
    public async Task Excel_ContainsSheetsWithByteValues()
    {
        var report = await BuildAsync(new ReportOptions { MinShare = 0 });
        var path = Path.Combine(_out, "report.xlsx");

        ReportExporter.Export(report, ReportFormat.Excel, path);

        using var workbook = new XLWorkbook(path);
        Assert.Equal(["Overview", "Folder structure", "Largest files", "File types", "File age"], workbook.Worksheets.Select(w => w.Name));
        var top = workbook.Worksheet("Largest files");
        Assert.Equal("a.mp4", top.Cell(2, 2).GetString());
        Assert.Equal(80_000, top.Cell(2, 3).GetDouble());
        var tree = workbook.Worksheet("Folder structure");
        Assert.True(tree.Row(3).OutlineLevel >= 1);
    }

    [Fact]
    public async Task Pdf_ProducesDocument()
    {
        var report = await BuildAsync();
        var bytes = PdfReportWriter.Render(report);
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task FileName_IsValidAndDated()
    {
        var report = await BuildAsync();
        var name = ReportStyle.FileName(report, ReportFormat.Excel);
        Assert.Equal("Storage report " + string.Join(' ', report.RootPath.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)) + " 2026-10-06.xlsx", name);
        Assert.True(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }
}
