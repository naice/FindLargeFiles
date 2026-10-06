using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Enumeration;

namespace FindLargeFiles.Core;

/// <summary>Live counters of a running scan. All members are safe to read from any thread.</summary>
public sealed class ScanProgress
{
    private long _files;
    private long _folders;
    private long _bytes;
    private long _errors;
    private volatile string _currentPath = string.Empty;

    public long Files => Interlocked.Read(ref _files);
    public long Folders => Interlocked.Read(ref _folders);
    public long Bytes => Interlocked.Read(ref _bytes);
    public long Errors => Interlocked.Read(ref _errors);
    public string CurrentPath => _currentPath;

    internal void AddFile(long length)
    {
        Interlocked.Increment(ref _files);
        Interlocked.Add(ref _bytes, length);
    }

    internal void AddFolder() => Interlocked.Increment(ref _folders);
    internal void AddError() => Interlocked.Increment(ref _errors);
    internal void SetCurrent(string path) => _currentPath = path;
}

public sealed record ScanResult(FsNode Root, TimeSpan Duration, long Errors);

/// <summary>
/// Scans a directory tree with a pool of workers sharing one work queue, so that
/// unbalanced trees (one huge folder next to many small ones) still use every core.
/// </summary>
public sealed class DirectoryScanner
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = 0,
    };

    private readonly int _workerCount;

    public DirectoryScanner(int? workerCount = null)
    {
        _workerCount = Math.Max(1, workerCount ?? Math.Min(Environment.ProcessorCount * 2, 32));
    }

    public ScanProgress Progress { get; private set; } = new();

    public async Task<ScanResult> ScanAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);

        var root = new FsNode(fullPath, null, true, lastWriteUtcTicks: SafeLastWrite(fullPath));
        var progress = Progress = new ScanProgress();
        var stopwatch = Stopwatch.StartNew();

        await Task.Run(() => Crawl(root, fullPath, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        Aggregate(root);
        return new ScanResult(root, stopwatch.Elapsed, progress.Errors);
    }

    /// <summary>Scans <paramref name="node"/> again and replaces its contents in place.</summary>
    public async Task<ScanResult> RescanAsync(FsNode node, CancellationToken cancellationToken = default)
    {
        if (!node.IsDirectory) throw new ArgumentException("Only directories can be rescanned.", nameof(node));
        var result = await ScanAsync(node.FullPath, cancellationToken).ConfigureAwait(false);
        node.ReplaceContents(result.Root);
        return result with { Root = node };
    }

    private void Crawl(FsNode root, string rootPath, ScanProgress progress, CancellationToken ct)
    {
        using var queue = new BlockingCollection<(FsNode Node, string Path)>(new ConcurrentStack<(FsNode, string)>());
        var pending = 1;
        queue.Add((root, rootPath), ct);

        void Worker()
        {
            try
            {
                foreach (var (dir, dirPath) in queue.GetConsumingEnumerable(ct))
                {
                    try
                    {
                        foreach (var sub in ScanDirectory(dir, dirPath, progress))
                        {
                            Interlocked.Increment(ref pending);
                            queue.Add(sub, ct);
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref pending) == 0) queue.CompleteAdding();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is reported by the caller.
            }
        }

        var workers = new Thread[_workerCount];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new Thread(Worker) { IsBackground = true, Name = $"Scanner {i}" };
            workers[i].Start();
        }

        foreach (var worker in workers) worker.Join();
    }

    private static List<(FsNode, string)> ScanDirectory(FsNode dir, string dirPath, ScanProgress progress)
    {
        progress.SetCurrent(dirPath);
        var subDirectories = new List<(FsNode, string)>();
        try
        {
            var entries = new FileSystemEnumerable<Entry>(dirPath, static (ref FileSystemEntry e) => new Entry(
                e.FileName.ToString(),
                e.IsDirectory,
                (e.Attributes & FileAttributes.ReparsePoint) != 0,
                e.IsDirectory ? 0 : e.Length,
                e.LastWriteTimeUtc.UtcTicks), Options);

            foreach (var entry in entries)
            {
                if (entry.IsDirectory)
                {
                    var child = new FsNode(entry.Name, dir, true, lastWriteUtcTicks: entry.LastWrite);
                    dir.AddChild(child);
                    progress.AddFolder();
                    if (entry.IsReparsePoint) child.IsReparsePoint = true;
                    else subDirectories.Add((child, Path.Join(dirPath, entry.Name)));
                }
                else
                {
                    dir.AddChild(new FsNode(entry.Name, dir, false, entry.Length, entry.LastWrite));
                    progress.AddFile(entry.Length);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            dir.IsInaccessible = true;
            progress.AddError();
        }

        return subDirectories;
    }

    /// <summary>Computes directory totals bottom-up and sorts every child list by size.</summary>
    internal static void Aggregate(FsNode root)
    {
        var directories = new List<FsNode>();
        var stack = new Stack<FsNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            directories.Add(dir);
            foreach (var child in dir.Children)
                if (child.IsDirectory) stack.Push(child);
        }

        for (var i = directories.Count - 1; i >= 0; i--)
        {
            var dir = directories[i];
            long size = 0, files = 0, folders = 0;
            foreach (var child in dir.Children)
            {
                size += child.Size;
                if (child.IsDirectory)
                {
                    files += child.FileCount;
                    folders += child.FolderCount + 1;
                }
                else
                {
                    child.FileCount = 1;
                    files++;
                }
            }

            dir.Size = size;
            dir.FileCount = files;
            dir.FolderCount = folders;
            dir.ChildList?.Sort(FsNode.BySizeDescending);
            dir.ChildList?.TrimExcess();
        }
    }

    private static long SafeLastWrite(string path)
    {
        try { return Directory.GetLastWriteTimeUtc(path).Ticks; }
        catch { return 0; }
    }

    private readonly record struct Entry(string Name, bool IsDirectory, bool IsReparsePoint, long Length, long LastWrite);
}
