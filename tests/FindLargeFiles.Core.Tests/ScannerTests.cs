using FindLargeFiles.Core;

namespace FindLargeFiles.Core.Tests;

public sealed class ScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flf-tests-" + Guid.NewGuid().ToString("N"));

    public ScannerTests()
    {
        Directory.CreateDirectory(_root);
        Write("a.bin", 1000);
        Write("big/b.iso", 50_000);
        Write("big/c.txt", 10);
        Write("big/nested/d.txt", 300);
        Write("small/e.log", 20);
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private void Write(string relative, int bytes)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    [Fact]
    public async Task Scan_AggregatesSizesAndCounts()
    {
        var scanner = new DirectoryScanner();
        var result = await scanner.ScanAsync(_root);
        var root = result.Root;

        Assert.Equal(51_330, root.Size);
        Assert.Equal(5, root.FileCount);
        Assert.Equal(4, root.FolderCount);
        Assert.Equal(5, scanner.Progress.Files);
        Assert.Equal(51_330, scanner.Progress.Bytes);

        var big = root.Children[0];
        Assert.Equal("big", big.Name);
        Assert.Equal(50_310, big.Size);
        Assert.Equal(3, big.FileCount);
        Assert.Equal(1, big.FolderCount);
    }

    [Fact]
    public async Task Scan_SortsChildrenBySizeDescending()
    {
        var root = (await new DirectoryScanner().ScanAsync(_root)).Root;
        Assert.Equal(["big", "a.bin", "small", "empty"], root.Children.Select(c => c.Name));
    }

    [Fact]
    public async Task FullPath_RebuildsTheOriginalPath()
    {
        var root = (await new DirectoryScanner().ScanAsync(_root)).Root;
        var d = root.Children[0].Children.Single(c => c.Name == "nested").Children.Single();
        Assert.Equal(Path.Combine(_root, "big", "nested", "d.txt"), d.FullPath);
        Assert.True(File.Exists(d.FullPath));
    }

    [Fact]
    public async Task Remove_UpdatesAncestorTotals()
    {
        var root = (await new DirectoryScanner().ScanAsync(_root)).Root;
        var big = root.Children[0];
        var iso = big.Children.Single(c => c.Name == "b.iso");

        iso.Remove();

        Assert.Equal(310, big.Size);
        Assert.Equal(2, big.FileCount);
        Assert.Equal(1330, root.Size);
        Assert.Equal(4, root.FileCount);
        Assert.Equal("a.bin", root.Children[0].Name);
    }

    [Fact]
    public async Task Rescan_ReplacesContentsAndPropagatesDelta()
    {
        var scanner = new DirectoryScanner();
        var root = (await scanner.ScanAsync(_root)).Root;
        var small = root.Children.Single(c => c.Name == "small");

        Write("small/f.dat", 5000);
        await scanner.RescanAsync(small);

        Assert.Equal(5020, small.Size);
        Assert.Equal(56_330, root.Size);
        Assert.Equal(6, root.FileCount);
        Assert.All(small.Children, c => Assert.Same(small, c.Parent));
    }

    [Fact]
    public async Task Scan_CanBeCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DirectoryScanner().ScanAsync(_root, cts.Token));
    }

    [Fact]
    public async Task Analysis_ComputesExtensionsTopFilesAndBreakdown()
    {
        var root = (await new DirectoryScanner().ScanAsync(_root)).Root;

        var ext = Analysis.ByExtension(root);
        Assert.Equal(".iso", ext[0].Extension);
        Assert.Equal(2, ext.Single(e => e.Extension == ".txt").FileCount);

        var top = Analysis.LargestFiles(root, 2);
        Assert.Equal(["b.iso", "a.bin"], top.Select(f => f.Name));

        // Three non-empty children, two slots: one item plus "Others" grouping the remaining two.
        var slices = Analysis.Breakdown(root, 2);
        Assert.Equal(2, slices.Count);
        Assert.Equal("big", slices[0].Label);
        Assert.Equal(SliceKind.Other, slices[1].Kind);
        Assert.Equal(1020, slices[1].Size);
        Assert.Equal(3, Analysis.Breakdown(root, 3).Count(s => s.Kind == SliceKind.Item));

        var ages = Analysis.ByAge(root, DateTime.UtcNow);
        Assert.Equal(5, ages[0].FileCount);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(10L * 1024 * 1024 * 1024, "10.0 GB")]
    [InlineData(300L * 1024 * 1024, "300 MB")]
    public void ByteFormatter_UsesBinaryUnits(long bytes, string expected)
    {
        Assert.Equal(expected, ByteFormatter.Format(bytes, System.Globalization.CultureInfo.InvariantCulture));
    }
}
