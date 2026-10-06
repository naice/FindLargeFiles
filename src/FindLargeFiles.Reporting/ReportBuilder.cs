using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Reporting;

public static class ReportBuilder
{
    public const int MaxSlices = 8;

    public static StorageReport Build(FsNode node, ReportOptions options, DriveSummary? drive = null,
        DateTime? now = null, CancellationToken ct = default)
    {
        if (!node.IsDirectory) throw new ArgumentException("Reports are created for folders.", nameof(node));
        var created = now ?? DateTime.Now;
        var path = node.FullPath;

        return new StorageReport
        {
            Title = Loc.T("report.title"),
            RootPath = path,
            CreatedAt = created,
            TotalSize = node.Size,
            FileCount = node.FileCount,
            FolderCount = node.FolderCount,
            InaccessibleFolders = CountInaccessible(node, ct),
            Drive = drive,
            Options = options,
            Breakdown = BuildBreakdown(node, drive, IsDriveRoot(path)),
            Tree = options.IncludeTree ? BuildTree(node, options, ct) : [],
            TopFiles = options.IncludeTopFiles
                ? Analysis.LargestFiles(node, options.TopFileCount, ct)
                    .Select((f, i) => new ReportFile(i + 1, f.Name, f.Parent?.FullPath ?? string.Empty, f.FullPath, f.Size, Modified(f)))
                    .ToList()
                : [],
            Extensions = options.IncludeExtensions ? Analysis.ByExtension(node, ct).Take(options.MaxExtensions).ToList() : [],
            Ages = options.IncludeAge ? Analysis.ByAge(node, created.ToUniversalTime(), ct) : [],
        };
    }

    public static bool IsDriveRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return root != null && string.Equals(root.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ReportSlice> BuildBreakdown(FsNode node, DriveSummary? drive, bool isDriveRoot)
    {
        var slices = Analysis.Breakdown(node, MaxSlices);
        var free = drive != null && isDriveRoot ? drive.FreeSpace : 0;
        var total = slices.Sum(s => s.Size) + free;
        var result = slices
            .Select(s => new ReportSlice(s.Label, s.Size, total == 0 ? 0 : (double)s.Size / total, s.Kind))
            .ToList();
        if (free > 0) result.Add(new ReportSlice(Loc.T("chart.free_space"), free, (double)free / total, SliceKind.FreeSpace));
        return result;
    }

    private static IReadOnlyList<ReportEntry> BuildTree(FsNode root, ReportOptions options, CancellationToken ct)
    {
        var entries = new List<ReportEntry>();
        var total = Math.Max(1, root.Size);
        var minSize = (long)(total * options.MinShare);

        void Visit(FsNode node, int depth)
        {
            ct.ThrowIfCancellationRequested();
            entries.Add(Entry(node, depth, total));
            if (!node.IsDirectory || depth >= options.TreeDepth) return;

            long hiddenSize = 0;
            var hiddenCount = 0;
            foreach (var child in node.Children)
            {
                if ((!child.IsDirectory && !options.IncludeFilesInTree) || child.Size < minSize || child.Size == 0)
                {
                    hiddenSize += child.Size;
                    hiddenCount++;
                    continue;
                }

                Visit(child, depth + 1);
            }

            if (hiddenCount > 0)
            {
                entries.Add(new ReportEntry(
                    Loc.Plural("report.more_items", hiddenCount), node.FullPath, depth + 1, false, hiddenSize,
                    node.Size == 0 ? 0 : (double)hiddenSize / node.Size, (double)hiddenSize / total,
                    0, 0, null, IsSummary: true));
            }
        }

        Visit(root, 0);
        return entries;
    }

    private static ReportEntry Entry(FsNode node, int depth, long total) => new(
        depth == 0 ? node.FullPath : node.Name,
        node.FullPath,
        depth,
        node.IsDirectory,
        node.Size,
        node.Parent is { Size: > 0 } parent ? (double)node.Size / parent.Size : 1,
        (double)node.Size / total,
        node.IsDirectory ? node.FileCount : 1,
        node.IsDirectory ? node.FolderCount : 0,
        Modified(node),
        IsInaccessible: node.IsInaccessible);

    private static DateTime? Modified(FsNode node) => node.LastWriteUtcTicks == 0 ? null : node.LastWriteTime;

    private static long CountInaccessible(FsNode root, CancellationToken ct)
    {
        long count = 0;
        var stack = new Stack<FsNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            if (dir.IsInaccessible) count++;
            foreach (var child in dir.Children)
                if (child.IsDirectory) stack.Push(child);
        }

        return count;
    }
}
