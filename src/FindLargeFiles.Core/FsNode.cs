using System.Diagnostics;
using System.Text;

namespace FindLargeFiles.Core;

/// <summary>
/// A file or directory in a scanned tree. Kept deliberately lean because a full
/// drive scan produces millions of instances.
/// </summary>
[DebuggerDisplay("{Name} ({Size})")]
public sealed class FsNode
{
    private static readonly List<FsNode> NoChildren = [];

    internal List<FsNode>? ChildList;

    public FsNode(string name, FsNode? parent, bool isDirectory, long size = 0, long lastWriteUtcTicks = 0)
    {
        Name = name;
        Parent = parent;
        IsDirectory = isDirectory;
        Size = size;
        LastWriteUtcTicks = lastWriteUtcTicks;
    }

    public string Name { get; }
    public FsNode? Parent { get; internal set; }
    public bool IsDirectory { get; }

    /// <summary>Logical size in bytes; for directories the sum of all descendants.</summary>
    public long Size { get; internal set; }

    /// <summary>Number of files below this directory (1 for a file).</summary>
    public long FileCount { get; internal set; }

    /// <summary>Number of directories below this directory (not counting itself).</summary>
    public long FolderCount { get; internal set; }

    public long LastWriteUtcTicks { get; internal set; }

    /// <summary>The directory could not be read (access denied, I/O error).</summary>
    public bool IsInaccessible { get; internal set; }

    /// <summary>The directory is a junction or symbolic link and was not followed.</summary>
    public bool IsReparsePoint { get; internal set; }

    public DateTime LastWriteTime => LastWriteUtcTicks == 0
        ? DateTime.MinValue
        : new DateTime(LastWriteUtcTicks, DateTimeKind.Utc).ToLocalTime();

    /// <summary>Children sorted by size, largest first (after the scan has completed).</summary>
    public IReadOnlyList<FsNode> Children => ChildList ?? NoChildren;

    public bool HasChildren => ChildList is { Count: > 0 };

    public string Extension
    {
        get
        {
            if (IsDirectory) return string.Empty;
            var dot = Name.LastIndexOf('.');
            return dot <= 0 || dot == Name.Length - 1 ? string.Empty : Name[dot..].ToLowerInvariant();
        }
    }

    public int Depth
    {
        get
        {
            var depth = 0;
            for (var p = Parent; p != null; p = p.Parent) depth++;
            return depth;
        }
    }

    public string FullPath
    {
        get
        {
            if (Parent == null) return Name;
            var parts = new Stack<string>();
            FsNode? n = this;
            while (n!.Parent != null)
            {
                parts.Push(n.Name);
                n = n.Parent;
            }

            var sb = new StringBuilder(n.Name);
            foreach (var part in parts)
            {
                if (sb.Length > 0 && sb[^1] != Path.DirectorySeparatorChar) sb.Append(Path.DirectorySeparatorChar);
                sb.Append(part);
            }

            return sb.ToString();
        }
    }

    public IEnumerable<FsNode> Ancestors()
    {
        for (var p = Parent; p != null; p = p.Parent) yield return p;
    }

    public bool IsDescendantOf(FsNode node) => Ancestors().Contains(node);

    /// <summary>Enumerates all files below this node (or the node itself if it is a file).</summary>
    public IEnumerable<FsNode> EnumerateFiles()
    {
        if (!IsDirectory)
        {
            yield return this;
            yield break;
        }

        var stack = new Stack<FsNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var child in dir.Children)
            {
                if (child.IsDirectory) stack.Push(child);
                else yield return child;
            }
        }
    }

    internal static readonly Comparison<FsNode> BySizeDescending = (a, b) =>
    {
        var c = b.Size.CompareTo(a.Size);
        return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    };

    internal void AddChild(FsNode child) => (ChildList ??= []).Add(child);

    /// <summary>Takes over the children and totals of a freshly scanned copy of this directory.</summary>
    internal void ReplaceContents(FsNode fresh)
    {
        var sizeDelta = fresh.Size - Size;
        var filesDelta = fresh.FileCount - FileCount;
        var foldersDelta = fresh.FolderCount - FolderCount;

        ChildList = fresh.ChildList;
        if (ChildList != null)
            foreach (var child in ChildList) child.Parent = this;
        Size = fresh.Size;
        FileCount = fresh.FileCount;
        FolderCount = fresh.FolderCount;
        IsInaccessible = fresh.IsInaccessible;
        LastWriteUtcTicks = fresh.LastWriteUtcTicks;

        foreach (var ancestor in Ancestors())
        {
            ancestor.Size += sizeDelta;
            ancestor.FileCount += filesDelta;
            ancestor.FolderCount += foldersDelta;
        }

        for (FsNode? n = this; n?.Parent != null; n = n.Parent)
            n.Parent.ChildList?.Sort(BySizeDescending);
    }

    /// <summary>Detaches this node from its parent and removes its totals from every ancestor.</summary>
    public void Remove()
    {
        if (Parent == null) return;
        var files = IsDirectory ? FileCount : 1;
        var folders = IsDirectory ? FolderCount + 1 : 0;
        Parent.ChildList?.Remove(this);
        foreach (var ancestor in Ancestors())
        {
            ancestor.Size -= Size;
            ancestor.FileCount -= files;
            ancestor.FolderCount -= folders;
        }

        for (var n = Parent; n.Parent != null; n = n.Parent)
            n.Parent.ChildList?.Sort(BySizeDescending);

        Parent = null;
    }
}
