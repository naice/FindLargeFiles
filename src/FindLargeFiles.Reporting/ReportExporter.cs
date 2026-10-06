namespace FindLargeFiles.Reporting;

public static class ReportExporter
{
    /// <summary>Writes the report and returns every file created (CSV may write several).</summary>
    public static IReadOnlyList<string> Export(StorageReport report, ReportFormat format, string path)
    {
        switch (format)
        {
            case ReportFormat.Excel:
                ExcelReportWriter.Write(report, path);
                return [path];
            case ReportFormat.Pdf:
                PdfReportWriter.Write(report, path);
                return [path];
            case ReportFormat.Csv:
                return CsvReportWriter.Write(report, path);
            default:
                HtmlReportWriter.Write(report, path);
                return [path];
        }
    }
}
