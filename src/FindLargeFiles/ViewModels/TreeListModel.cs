using FindLargeFiles.Core;

namespace FindLargeFiles.ViewModels;

public enum SortField { Name, Size, Files, Modified }

/// <summary>
/// Presents a <see cref="FsNode"/> tree as a flat, virtualizable row list. Expanding a row inserts
/// its children directly below it; very large folders are paged.
/// </summary>
public sealed class TreeListModel
{
    public const int PageSize = 500;

    // Inserting or removing more rows than this is done with a single Reset notification.
    private const int IncrementalLimit = 64;

    private readonly HashSet<FsNode> _expanded = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FsNode, int> _pages = new(ReferenceEqualityComparer.Instance);
    private FsNode? _root;

    public BulkObservableCollection<NodeRow> Rows { get; } = [];

    public SortField SortField { get; private set; } = SortField.Size;
    public bool SortDescending { get; private set; } = true;

    public void SetRoot(FsNode? root)
    {
        _root = root;
        _expanded.Clear();
        _pages.Clear();
        if (root != null) _expanded.Add(root);
        Rebuild();
    }

    /// <summary>Recreates every row, keeping which folders are expanded.</summary>
    public void Rebuild()
    {
        var rows = new List<NodeRow>();
        if (_root != null) AppendRow(rows, _root, null, 0);
        Rows.ReplaceAll(rows);
    }

    public void Sort(SortField field)
    {
        if (SortField == field) SortDescending = !SortDescending;
        else
        {
            SortField = field;
            SortDescending = field != SortField.Name;
        }

        Rebuild();
    }

    public NodeRow? FindRow(FsNode node) => Rows.FirstOrDefault(r => !r.IsOverflow && ReferenceEquals(r.Node, node));

    /// <summary>Expands every ancestor of <paramref name="node"/> (paging further if needed) and returns its row.</summary>
    public NodeRow? Reveal(FsNode node)
    {
        if (_root == null || (!ReferenceEquals(node, _root) && !node.IsDescendantOf(_root))) return null;

        var changed = false;
        foreach (var ancestor in node.Ancestors())
        {
            changed |= _expanded.Add(ancestor);
            var index = IndexIn(ancestor, node);
            if (index >= PageCount(ancestor))
            {
                _pages[ancestor] = (index / PageSize + 1) * PageSize;
                changed = true;
            }
        }

        if (changed) Rebuild();
        return FindRow(node);
    }

    public void ShowMore(NodeRow overflow)
    {
        if (!overflow.IsOverflow) return;
        _pages[overflow.Node] = PageCount(overflow.Node) + PageSize;
        var index = Rows.IndexOf(overflow);
        var rows = new List<NodeRow>();
        var children = Sorted(overflow.Node);
        var start = PageCount(overflow.Node) - PageSize;
        AppendChildren(rows, overflow.ParentRow!, children, start);
        Replace(index, 1, rows);
    }

    internal void OnExpandedChanged(NodeRow row)
    {
        var index = Rows.IndexOf(row);
        if (row.IsExpanded)
        {
            _expanded.Add(row.Node);
            if (index < 0) return;
            var rows = new List<NodeRow>();
            AppendChildren(rows, row, Sorted(row.Node), 0);
            Replace(index + 1, 0, rows);
        }
        else
        {
            _expanded.Remove(row.Node);
            if (index < 0) return;
            var end = index + 1;
            while (end < Rows.Count && Rows[end].Level > row.Level) end++;
            Replace(index + 1, end - index - 1, []);
        }
    }

    private void Replace(int index, int removeCount, List<NodeRow> insert)
    {
        if (removeCount + insert.Count <= IncrementalLimit)
        {
            for (var i = 0; i < removeCount; i++) Rows.RemoveAt(index);
            for (var i = 0; i < insert.Count; i++) Rows.Insert(index + i, insert[i]);
            return;
        }

        var all = new List<NodeRow>(Rows.Count - removeCount + insert.Count);
        all.AddRange(Rows.Take(index));
        all.AddRange(insert);
        all.AddRange(Rows.Skip(index + removeCount));
        Rows.ReplaceAll(all);
    }

    private void AppendRow(List<NodeRow> rows, FsNode node, NodeRow? parentRow, int level)
    {
        var row = new NodeRow(this, node, parentRow, level, _expanded.Contains(node));
        rows.Add(row);
        if (row.IsExpanded) AppendChildren(rows, row, Sorted(node), 0);
    }

    private void AppendChildren(List<NodeRow> rows, NodeRow parentRow, IReadOnlyList<FsNode> children, int start)
    {
        var end = Math.Min(children.Count, PageCount(parentRow.Node));
        for (var i = start; i < end; i++) AppendRow(rows, children[i], parentRow, parentRow.Level + 1);

        if (children.Count > end)
        {
            long hidden = 0;
            for (var i = end; i < children.Count; i++) hidden += children[i].Size;
            rows.Add(NodeRow.Overflow(this, parentRow, children.Count - end, hidden));
        }
    }

    private int PageCount(FsNode node) => _pages.TryGetValue(node, out var count) ? count : PageSize;

    private int IndexIn(FsNode parent, FsNode descendant)
    {
        var child = descendant;
        while (!ReferenceEquals(child.Parent, parent)) child = child.Parent!;
        var children = Sorted(parent);
        for (var i = 0; i < children.Count; i++)
            if (ReferenceEquals(children[i], child)) return i;
        return 0;
    }

    private IReadOnlyList<FsNode> Sorted(FsNode node)
    {
        var children = node.Children;
        if (SortField == SortField.Size && SortDescending) return children;

        IEnumerable<FsNode> ordered = SortField switch
        {
            SortField.Name => children.OrderBy(c => !c.IsDirectory).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase),
            SortField.Files => children.OrderBy(c => c.IsDirectory ? c.FileCount : 0),
            SortField.Modified => children.OrderBy(c => c.LastWriteUtcTicks),
            _ => children.OrderBy(c => c.Size),
        };

        var list = ordered.ToList();
        if (SortDescending)
        {
            list.Reverse();
            // Keep folders before files when sorting by name, in either direction.
            if (SortField == SortField.Name) list = list.OrderBy(c => !c.IsDirectory).ToList();
        }

        return list;
    }
}
