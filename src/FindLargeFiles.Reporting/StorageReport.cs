using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

public enum ReportFormat { Excel, Pdf, Csv, Html }

public sealed record ReportOptions
{
    /// <summary>How many folder levels below the report root the structure section lists.</summary>
    public int TreeDepth { get; init; } = 3;

    /// <summary>Also list files (not only folders) in the structure section.</summary>
    public bool IncludeFilesInTree { get; init; } = true;

    /// <summary>Entries smaller than this share of the report total are folded into one summary line per folder.</summary>
    public double MinShare { get; init; } = 0.001;

    public bool IncludeTree { get; init; } = true;
    public bool IncludeTopFiles { get; init; } = true;
    public bool IncludeExtensions { get; init; } = true;
    public bool IncludeAge { get; init; } = true;
    public int TopFileCount { get; init; } = 100;
    public int MaxExtensions { get; init; } = 50;
}

public sealed record DriveSummary(string Name, string Label, string Format, long TotalSize, long FreeSpace)
{
    public long UsedSpace => TotalSize - FreeSpace;
}

/// <summary>One line of the folder structure section, in display (pre-)order.</summary>
public sealed record ReportEntry(
    string Name,
    string Path,
    int Depth,
    bool IsDirectory,
    long Size,
    double ShareOfParent,
    double ShareOfTotal,
    long FileCount,
    long FolderCount,
    DateTime? Modified,
    bool IsSummary = false,
    bool IsInaccessible = false)
{
    public string Kind => Loc.T(IsSummary ? "report.kind_more" : IsDirectory ? "report.kind_folder" : "report.kind_file");
}

public sealed record ReportFile(int Rank, string Name, string Folder, string Path, long Size, DateTime? Modified);

public sealed record ReportSlice(string Label, long Size, double Share, SliceKind Kind);

/// <summary>Everything a report writer needs, detached from the live scan tree.</summary>
public sealed record StorageReport
{
    public required string Title { get; init; }
    public required string RootPath { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required long TotalSize { get; init; }
    public required long FileCount { get; init; }
    public required long FolderCount { get; init; }
    public required long InaccessibleFolders { get; init; }
    public DriveSummary? Drive { get; init; }
    public required ReportOptions Options { get; init; }
    public required IReadOnlyList<ReportSlice> Breakdown { get; init; }
    public IReadOnlyList<ReportEntry> Tree { get; init; } = [];
    public IReadOnlyList<ReportFile> TopFiles { get; init; } = [];
    public IReadOnlyList<ExtensionStat> Extensions { get; init; } = [];
    public IReadOnlyList<AgeBucket> Ages { get; init; } = [];
}
