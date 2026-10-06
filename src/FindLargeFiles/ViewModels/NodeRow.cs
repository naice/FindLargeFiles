using CommunityToolkit.Mvvm.ComponentModel;
using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.ViewModels;

/// <summary>One visible line of the flattened tree list.</summary>
public sealed class NodeRow : ObservableObject
{
    private readonly TreeListModel _owner;
    private bool _isExpanded;

    internal NodeRow(TreeListModel owner, FsNode node, NodeRow? parentRow, int level, bool isExpanded)
    {
        _owner = owner;
        Node = node;
        ParentRow = parentRow;
        Level = level;
        _isExpanded = isExpanded && CanExpand;
    }

    private NodeRow(TreeListModel owner, NodeRow parentRow, int hiddenCount, long hiddenSize)
    {
        _owner = owner;
        Node = parentRow.Node;
        ParentRow = parentRow;
        Level = parentRow.Level + 1;
        IsOverflow = true;
        HiddenCount = hiddenCount;
        HiddenSize = hiddenSize;
    }

    internal static NodeRow Overflow(TreeListModel owner, NodeRow parentRow, int hiddenCount, long hiddenSize) =>
        new(owner, parentRow, hiddenCount, hiddenSize);

    public FsNode Node { get; }
    public NodeRow? ParentRow { get; }
    public int Level { get; }

    /// <summary>Placeholder row standing for children that are not listed yet.</summary>
    public bool IsOverflow { get; }
    public int HiddenCount { get; }
    public long HiddenSize { get; }

    public bool IsRoot => Node.Parent == null && !IsOverflow;
    public bool IsDirectory => Node.IsDirectory && !IsOverflow;
    public bool CanExpand => IsDirectory && Node.HasChildren;
    public double Indent => Level * 18;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!CanExpand || _isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            _owner.OnExpandedChanged(this);
        }
    }

    public string Name => IsOverflow ? Loc.T("tree.show_more", ("count", HiddenCount.ToString("N0"))) : Node.Name;
    public long Size => IsOverflow ? HiddenSize : Node.Size;
    public string SizeText => ByteFormatter.Format(Size);

    /// <summary>Share of the parent directory, 0..1.</summary>
    public double Share
    {
        get
        {
            var parent = IsOverflow ? Node : Node.Parent;
            if (parent == null) return 1;
            return parent.Size <= 0 ? 0 : Math.Clamp((double)Size / parent.Size, 0, 1);
        }
    }

    public string ShareText => Share.ToString("P1");

    public string FilesText => IsDirectory ? Node.FileCount.ToString("N0") : string.Empty;
    public string FoldersText => IsDirectory ? Node.FolderCount.ToString("N0") : string.Empty;

    public string ModifiedText => IsOverflow || Node.LastWriteUtcTicks == 0
        ? string.Empty
        : Node.LastWriteTime.ToString("g");

    public RowIcon Icon => IsOverflow ? RowIcon.More
        : IsRoot ? RowIcon.Drive
        : Node.IsInaccessible ? RowIcon.Locked
        : Node.IsReparsePoint ? RowIcon.Link
        : Node.IsDirectory ? RowIcon.Folder
        : RowIcon.File;

    public string? Warning => IsOverflow ? null
        : Node.IsInaccessible ? Loc.T("tree.warning_inaccessible")
        : Node.IsReparsePoint ? Loc.T("tree.warning_reparse_point")
        : null;

    /// <summary>Changes the flag without triggering an expand/collapse of rows.</summary>
    internal void SetExpandedSilently(bool value)
    {
        if (_isExpanded == value) return;
        _isExpanded = value;
        OnPropertyChanged(nameof(IsExpanded));
    }
}

public enum RowIcon { Drive, Folder, File, Locked, Link, More }
