using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Core;

public sealed record ExtensionStat(string Extension, long FileCount, long Size, double Share);

public sealed record AgeBucket(string Label, long FileCount, long Size, double Share);

/// <summary>One segment of a size chart. <see cref="Node"/> is null for aggregated or synthetic segments.</summary>
public sealed record SizeSlice(string Label, long Size, FsNode? Node, SliceKind Kind = SliceKind.Item);

public enum SliceKind { Item, Other, FreeSpace }

public static class Analysis
{
    /// <summary>
    /// At most <paramref name="maxSlices"/> items (one per color slot). When there are more children, the
    /// last slot becomes "Others", which therefore always groups at least two entries.
    /// </summary>
    public static IReadOnlyList<SizeSlice> Breakdown(FsNode node, int maxSlices)
    {
        var children = node.Children.Where(c => c.Size > 0).ToList();
        var shown = children.Count <= maxSlices ? children.Count : Math.Max(0, maxSlices - 1);
        var slices = children.Take(shown).Select(c => new SizeSlice(c.Name, c.Size, c)).ToList();
        if (children.Count > shown)
        {
            var rest = children.Skip(shown).ToList();
            slices.Add(new SizeSlice(Loc.T("chart.others", ("count", rest.Count.ToString("N0"))), rest.Sum(c => c.Size), null, SliceKind.Other));
        }

        return slices;
    }

    public static IReadOnlyList<ExtensionStat> ByExtension(FsNode node, CancellationToken ct = default)
    {
        var map = new Dictionary<string, (long Count, long Size)>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        var i = 0;
        foreach (var file in node.EnumerateFiles())
        {
            if ((++i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            var ext = file.Extension;
            map.TryGetValue(ext, out var s);
            map[ext] = (s.Count + 1, s.Size + file.Size);
            total += file.Size;
        }

        return map
            .Select(kv => new ExtensionStat(
                kv.Key.Length == 0 ? Loc.T("file_types.no_extension") : kv.Key,
                kv.Value.Count,
                kv.Value.Size,
                total == 0 ? 0 : (double)kv.Value.Size / total))
            .OrderByDescending(e => e.Size)
            .ToList();
    }

    public static IReadOnlyList<FsNode> LargestFiles(FsNode node, int count, CancellationToken ct = default)
    {
        var heap = new PriorityQueue<FsNode, long>(count + 1);
        var i = 0;
        foreach (var file in node.EnumerateFiles())
        {
            if ((++i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            if (heap.Count < count) heap.Enqueue(file, file.Size);
            else if (file.Size > heap.Peek().Size) heap.EnqueueDequeue(file, file.Size);
        }

        var result = new List<FsNode>(heap.Count);
        while (heap.Count > 0) result.Add(heap.Dequeue());
        result.Reverse();
        return result;
    }

    // Labels are translation keys, resolved when the buckets are built.
    private static readonly (string Key, Func<DateTime, DateTime> Limit)[] AgeLimits =
    [
        ("file_age.under_1_month", now => now.AddMonths(-1)),
        ("file_age.months_1_to_3", now => now.AddMonths(-3)),
        ("file_age.months_3_to_6", now => now.AddMonths(-6)),
        ("file_age.months_6_to_12", now => now.AddYears(-1)),
        ("file_age.years_1_to_2", now => now.AddYears(-2)),
        ("file_age.years_2_to_5", now => now.AddYears(-5)),
        ("file_age.over_5_years", _ => DateTime.MinValue),
    ];

    public static IReadOnlyList<AgeBucket> ByAge(FsNode node, DateTime nowUtc, CancellationToken ct = default)
    {
        var limits = AgeLimits.Select(a => a.Limit(nowUtc).Ticks).ToArray();
        var counts = new long[limits.Length];
        var sizes = new long[limits.Length];
        long total = 0;
        var i = 0;
        foreach (var file in node.EnumerateFiles())
        {
            if ((++i & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            var bucket = 0;
            while (bucket < limits.Length - 1 && file.LastWriteUtcTicks < limits[bucket]) bucket++;
            counts[bucket]++;
            sizes[bucket] += file.Size;
            total += file.Size;
        }

        return AgeLimits
            .Select((a, b) => new AgeBucket(Loc.T(a.Key), counts[b], sizes[b], total == 0 ? 0 : (double)sizes[b] / total))
            .ToList();
    }
}
