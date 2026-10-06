using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FindLargeFiles.Core;
using FindLargeFiles.Reporting;
using FindLargeFiles.Services;
using Microsoft.Win32;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.ViewModels;

public enum ChartMode { Pie, Donut, Bar, Treemap }

public enum DetailTab { Chart, Extensions, TopFiles, Age }

public sealed partial class MainViewModel : ObservableObject
{
    private readonly DirectoryScanner _scanner = new();
    private readonly DispatcherTimer _progressTimer;
    private readonly Stopwatch _scanClock = new();
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _detailsCts;
    private int _driveLoadVersion;
    private readonly Dictionary<string, Task<DriveItem?>> _driveQueries = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel()
    {
        Stats = [_sizeStat, _filesStat, _foldersStat, _shareStat, _modifiedStat, _driveStat];
        _progressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) => UpdateProgress(), Dispatcher.CurrentDispatcher);
        _progressTimer.Stop();
        RefreshDrives();
    }

    /// <summary>Raised when a row should be selected and scrolled into view.</summary>
    public event Action<NodeRow>? RevealRequested;

    /// <summary>Shows the export options; set by the view. Returns false when the user cancels.</summary>
    public Func<ExportViewModel, bool>? ShowExportDialog { get; set; }

    /// <summary>Window handle used as owner for shell dialogs.</summary>
    public IntPtr OwnerHandle { get; set; }

    public bool IsElevated { get; } = Shell.IsElevated;

    public TreeListModel Tree { get; } = new();
    public ObservableCollection<DriveItem> Drives { get; } = [];
    public ObservableCollection<Crumb> Breadcrumbs { get; } = [];
    private readonly Stat _sizeStat = new(Loc.T("stats.size"), 88);
    private readonly Stat _filesStat = new(Loc.T("stats.files"), 96);
    private readonly Stat _foldersStat = new(Loc.T("stats.folders"), 84);
    private readonly Stat _shareStat = new(Loc.T("stats.share_of_scan"), 96);
    private readonly Stat _modifiedStat = new(Loc.T("stats.modified"), 136);
    private readonly Stat _driveStat = new(Loc.T("stats.drive"), double.NaN); // last slot takes the remaining width

    /// <summary>Always the same slots in the same order; inapplicable values show a dash.</summary>
    public IReadOnlyList<Stat> Stats { get; }

    [ObservableProperty] public partial FsNode? Root { get; private set; }
    [ObservableProperty] public partial NodeRow? SelectedRow { get; set; }
    [ObservableProperty] public partial FsNode? ChartNode { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<ChartItem> ChartItems { get; private set; } = [];
    [ObservableProperty] public partial ChartMode ChartMode { get; set; } = ChartMode.Donut;
    [ObservableProperty] public partial bool IncludeFreeSpace { get; set; } = true;
    [ObservableProperty] public partial bool CanShowFreeSpace { get; private set; }
    [ObservableProperty] public partial int SelectedTabIndex { get; set; }

    public DetailTab SelectedTab => (DetailTab)SelectedTabIndex;
    [ObservableProperty] public partial IReadOnlyList<ExtensionRow> Extensions { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<FileRow> TopFiles { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<AgeRow> AgeRows { get; private set; } = [];
    [ObservableProperty] public partial bool IsComputingDetails { get; private set; }

    [ObservableProperty] public partial bool IsScanning { get; private set; }
    [ObservableProperty] public partial bool IsExporting { get; private set; }
    [ObservableProperty] public partial bool IsLoadingDrives { get; private set; }
    [ObservableProperty] public partial string ScanPath { get; private set; } = string.Empty;
    [ObservableProperty] public partial string ProgressFiles { get; private set; } = "0";
    [ObservableProperty] public partial string ProgressFolders { get; private set; } = "0";
    [ObservableProperty] public partial string ProgressSize { get; private set; } = "0 B";
    [ObservableProperty] public partial string ProgressCurrent { get; private set; } = string.Empty;
    [ObservableProperty] public partial string ProgressElapsed { get; private set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; private set; } = Loc.T("scan.status_ready");
    [ObservableProperty] public partial string? ErrorText { get; private set; }

    public bool HasResult => Root != null;
    public bool ShowEmptyState => Root == null && !IsScanning;
    public bool ShowLegend => ChartMode != ChartMode.Bar && ChartItems.Count > 0;
    public string ChartTitle => ChartNode?.Name ?? string.Empty;
    public string ChartSubtitle => ChartNode == null ? string.Empty
        : Loc.T("chart.subtitle", ("size", ByteFormatter.Format(ChartNode.Size)),
            ("files", Loc.Plural("global.file_count", ChartNode.FileCount)),
            ("folders", Loc.Plural("global.folder_count", ChartNode.FolderCount)));

    public string VersionText { get; } = Loc.T("app.version",
        ("version", typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"));

    partial void OnRootChanged(FsNode? value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

    partial void OnSelectedRowChanged(NodeRow? value)
    {
        if (value?.IsOverflow == true) return;
        var node = value?.Node;
        UpdateBreadcrumbs(node);
        UpdateStats(node);
        var chartNode = node == null ? null : node.IsDirectory ? node : node.Parent;
        if (!ReferenceEquals(chartNode, ChartNode)) ChartNode = chartNode;
    }

    partial void OnChartNodeChanged(FsNode? value)
    {
        OnPropertyChanged(nameof(ChartTitle));
        OnPropertyChanged(nameof(ChartSubtitle));
        RefreshChart();
        RefreshDetails();
    }

    partial void OnChartModeChanged(ChartMode value) => OnPropertyChanged(nameof(ShowLegend));
    partial void OnIncludeFreeSpaceChanged(bool value) => RefreshChart();
    partial void OnSelectedTabIndexChanged(int value) => RefreshDetails();
    partial void OnChartItemsChanged(IReadOnlyList<ChartItem> value) => OnPropertyChanged(nameof(ShowLegend));

    // ───────────── Scanning ─────────────

    [RelayCommand]
    private async Task OpenFolder()
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("scan.folder_dialog_title") };
        if (Root != null) dialog.InitialDirectory = Root.FullPath;
        if (dialog.ShowDialog() == true) await ScanAsync(dialog.FolderName);
    }

    [RelayCommand]
    private Task ScanDrive(DriveItem? drive) => drive == null ? Task.CompletedTask : ScanAsync(drive.RootPath);

    /// <summary>
    /// Reloads the drive list without blocking the command: each drive is queried on its own thread,
    /// because a sleeping disk or an unreachable network drive can take many seconds to answer.
    /// </summary>
    [RelayCommand]
    private void RefreshDrives() => _ = LoadDrivesAsync();

    private async Task LoadDrivesAsync()
    {
        var version = ++_driveLoadVersion;
        var loads = DriveItem.Load().Select(d => QueryDrive(d.RootPath, d.Load)).ToList();
        Drives.Clear();
        IsLoadingDrives = true;
        try
        {
            while (loads.Count > 0)
            {
                var done = await Task.WhenAny(loads);
                loads.Remove(done);
                if (version != _driveLoadVersion) return; // a newer refresh took over
                if (await done is not { } drive) continue;

                var index = 0;
                while (index < Drives.Count && string.CompareOrdinal(Drives[index].RootPath, drive.RootPath) < 0) index++;
                Drives.Insert(index, drive);
                if (SelectedRow is { IsOverflow: false } row && DriveOf(row.Node) == drive)
                {
                    UpdateStats(row.Node);
                    RefreshChart();
                }
            }
        }
        finally
        {
            if (version == _driveLoadVersion) IsLoadingDrives = false;
        }
    }

    /// <summary>Reuses a still running query for the same drive instead of piling up blocked threads.</summary>
    private Task<DriveItem?> QueryDrive(string rootPath, Func<DriveItem?> load)
    {
        if (_driveQueries.TryGetValue(rootPath, out var running) && !running.IsCompleted) return running;
        return _driveQueries[rootPath] = Task.Run(load);
    }

    [RelayCommand]
    private void CancelScan() => _scanCts?.Cancel();

    public async Task ScanAsync(string path)
    {
        if (IsScanning) return;
        if (!Directory.Exists(path))
        {
            ErrorText = Loc.T("scan.folder_not_found", ("path", path));
            return;
        }

        ErrorText = null;
        using var cts = _scanCts = new CancellationTokenSource();
        BeginScan(path);
        try
        {
            var result = await _scanner.ScanAsync(path, cts.Token);
            Root = result.Root;
            Tree.SetRoot(result.Root);
            SelectedRow = Tree.Rows.FirstOrDefault();
            StatusText = result.Errors > 0
                ? Loc.T("scan.status_done_with_errors", ("duration", FormatDuration(result.Duration)), ("count", result.Errors.ToString("N0")))
                : Loc.T("scan.status_done", ("duration", FormatDuration(result.Duration)));
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc.T("scan.status_cancelled");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorText = ex.Message;
            StatusText = Loc.T("scan.status_failed");
        }
        finally
        {
            EndScan();
        }
    }

    [RelayCommand]
    private async Task Rescan(object? parameter)
    {
        var node = NodeOf(parameter) ?? Root;
        if (node == null || IsScanning) return;
        if (!node.IsDirectory) node = node.Parent!;
        if (node.Parent == null)
        {
            await ScanAsync(node.FullPath);
            return;
        }

        if (!Directory.Exists(node.FullPath))
        {
            RemoveNode(node);
            return;
        }

        using var cts = _scanCts = new CancellationTokenSource();
        BeginScan(node.FullPath);
        try
        {
            var result = await _scanner.RescanAsync(node, cts.Token);
            RebuildKeepingSelection(node);
            StatusText = Loc.T("scan.status_refreshed", ("name", node.Name), ("duration", FormatDuration(result.Duration)));
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc.T("scan.status_refresh_cancelled");
        }
        finally
        {
            EndScan();
        }
    }

    private void BeginScan(string path)
    {
        ScanPath = path;
        IsScanning = true;
        StatusText = Loc.T("scan.status_scanning", ("path", path));
        _scanClock.Restart();
        UpdateProgress();
        _progressTimer.Start();
    }

    private void EndScan()
    {
        _progressTimer.Stop();
        _scanClock.Stop();
        _scanCts = null;
        IsScanning = false;
        RefreshDrives();
    }

    private void UpdateProgress()
    {
        var p = _scanner.Progress;
        ProgressFiles = p.Files.ToString("N0");
        ProgressFolders = p.Folders.ToString("N0");
        ProgressSize = ByteFormatter.Format(p.Bytes);
        ProgressCurrent = p.CurrentPath;
        ProgressElapsed = FormatDuration(_scanClock.Elapsed);
    }

    private static string FormatDuration(TimeSpan t) =>
        t.TotalSeconds < 60
            ? Loc.T("global.duration_seconds", ("seconds", t.TotalSeconds.ToString("N1")))
            : Loc.T("global.duration_minutes", ("minutes", (int)t.TotalMinutes), ("seconds", t.Seconds));

    // ───────────── Navigation ─────────────

    [RelayCommand]
    private void Navigate(object? parameter)
    {
        if (parameter is NodeRow { IsOverflow: true } overflow)
        {
            Tree.ShowMore(overflow);
            return;
        }

        var node = NodeOf(parameter);
        if (node == null) return;
        var row = Tree.Reveal(node);
        if (row == null) return;
        SelectedRow = row;
        RevealRequested?.Invoke(row);
    }

    [RelayCommand]
    private void GoUp()
    {
        var parent = SelectedRow?.Node.Parent;
        if (parent != null) Navigate(parent);
    }

    [RelayCommand]
    private void Sort(string field)
    {
        if (!Enum.TryParse<SortField>(field, out var sortField)) return;
        var node = SelectedRow?.Node;
        Tree.Sort(sortField);
        if (node != null) Navigate(node);
    }

    [RelayCommand]
    private async Task ScopeTo(object? parameter)
    {
        var node = NodeOf(parameter);
        if (node == null) return;
        await ScanAsync((node.IsDirectory ? node : node.Parent!).FullPath);
    }

    // ───────────── Item actions ─────────────

    [RelayCommand]
    private void OpenItem(object? parameter) => TryShell(parameter, Shell.Open);

    [RelayCommand]
    private void ShowInExplorer(object? parameter) => TryShell(parameter, Shell.ShowInExplorer);

    [RelayCommand]
    private void ShowProperties(object? parameter) => TryShell(parameter, Shell.ShowProperties);

    [RelayCommand]
    private void CopyPath(object? parameter)
    {
        var node = NodeOf(parameter);
        if (node == null) return;
        Clipboard.SetText(node.FullPath);
        StatusText = Loc.T("actions.path_copied", ("path", node.FullPath));
    }

    [RelayCommand]
    private void Recycle(object? parameter)
    {
        var node = NodeOf(parameter);
        if (node?.Parent == null || IsScanning) return;
        var path = node.FullPath;
        if (Shell.MoveToRecycleBin(path, OwnerHandle))
        {
            var size = node.Size;
            RemoveNode(node);
            StatusText = Loc.T("actions.moved_to_recycle_bin", ("name", node.Name), ("size", ByteFormatter.Format(size)));
            RefreshDrives();
        }
    }

    [RelayCommand]
    private async Task ExportReport(object? parameter)
    {
        if (Root == null || IsScanning || IsExporting || ShowExportDialog == null) return;
        var node = NodeOf(parameter) ?? Root;
        if (!node.IsDirectory) node = node.Parent ?? Root;

        var options = ExportViewModel.Create(node, Root);
        if (!ShowExportDialog(options)) return;
        options.Remember();

        var target = options.Target;
        var format = options.Format;
        var reportOptions = options.ToOptions();
        var drive = DriveOf(target) is { } d ? new DriveSummary(d.Letter, d.Label, d.Format, d.TotalSize, d.FreeSpace) : null;

        var dialog = new SaveFileDialog
        {
            Title = Loc.T("export.save_dialog_title"),
            Filter = ExportViewModel.Filter(format),
            FileName = ReportStyle.FileName(target.FullPath, DateTime.Now, format),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.FileName;

        IsExporting = true;
        StatusText = Loc.T("export.status_creating");
        try
        {
            var files = await Task.Run(() =>
            {
                var report = ReportBuilder.Build(target, reportOptions, drive);
                return ReportExporter.Export(report, format, path);
            });

            StatusText = files.Count == 1
                ? Loc.T("export.status_saved", ("path", files[0]))
                : Loc.T("export.status_saved_many", ("count", files.Count), ("folder", Path.GetDirectoryName(path)));
            if (options.OpenAfterExport) Shell.Open(files[0]);
        }
        catch (Exception ex)
        {
            ErrorText = Loc.T("export.error_failed", ("message", ex.Message));
            StatusText = Loc.T("export.status_failed");
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private void RestartAsAdmin()
    {
        if (Shell.RestartElevated(Root?.FullPath)) Application.Current.Shutdown();
    }

    [RelayCommand]
    private void DismissError() => ErrorText = null;

    private void TryShell(object? parameter, Action<string> action)
    {
        var node = NodeOf(parameter);
        if (node == null) return;
        try
        {
            action(node.FullPath);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    private void RemoveNode(FsNode node)
    {
        var parent = node.Parent;
        node.Remove();
        RebuildKeepingSelection(parent);
    }

    private void RebuildKeepingSelection(FsNode? fallback)
    {
        var selected = SelectedRow?.Node;
        Tree.Rebuild();
        var target = selected != null && (selected.Parent != null || ReferenceEquals(selected, Root)) && Tree.FindRow(selected) != null
            ? selected
            : fallback;
        SelectedRow = null;
        if (target != null) Navigate(target);
        RefreshChart();
        RefreshDetails();
    }

    private FsNode? NodeOf(object? parameter) => parameter switch
    {
        NodeRow { IsOverflow: false } row => row.Node,
        FsNode node => node,
        ChartItem item => item.Node,
        FileRow file => file.Node,
        Crumb crumb => crumb.Node,
        null => SelectedRow is { IsOverflow: false } selected ? selected.Node : null,
        _ => null,
    };

    // ───────────── Derived views ─────────────

    private void UpdateBreadcrumbs(FsNode? node)
    {
        Breadcrumbs.Clear();
        if (node == null) return;
        var chain = node.Ancestors().Reverse().Append(node).ToList();
        for (var i = 0; i < chain.Count; i++)
            Breadcrumbs.Add(new Crumb(chain[i].Name.TrimEnd('\\'), chain[i], i == chain.Count - 1));
    }

    private void UpdateStats(FsNode? node)
    {
        var none = Stat.Empty;
        _sizeStat.Value = node == null ? none : ByteFormatter.Format(node.Size);
        _filesStat.Value = node is { IsDirectory: true } ? node.FileCount.ToString("N0") : none;
        _foldersStat.Value = node is { IsDirectory: true } ? node.FolderCount.ToString("N0") : none;
        _shareStat.Value = node == null || Root == null ? none
            : (Root.Size == 0 ? 0 : (double)node.Size / Root.Size).ToString("P1");
        _modifiedStat.Value = node is { LastWriteUtcTicks: not 0 } ? node.LastWriteTime.ToString("g") : none;

        var drive = node == null ? null : DriveOf(node);
        _driveStat.Label = drive == null ? Loc.T("stats.drive") : Loc.T("stats.drive_with_letter", ("letter", drive.Letter));
        _driveStat.Value = drive?.SpaceText ?? none;
    }

    private DriveItem? DriveOf(FsNode node)
    {
        var root = Path.GetPathRoot(node.Ancestors().LastOrDefault()?.Name ?? node.Name);
        return Drives.FirstOrDefault(d => string.Equals(d.RootPath, root, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshChart()
    {
        var node = ChartNode;
        if (node == null)
        {
            ChartItems = [];
            CanShowFreeSpace = false;
            return;
        }

        var slices = Analysis.Breakdown(node, ChartPalette.MaxSeries);
        var isDriveRoot = node.Parent == null && Path.GetPathRoot(node.Name) is { } r
                          && string.Equals(r.TrimEnd('\\'), node.Name.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        var drive = isDriveRoot ? DriveOf(node) : null;
        CanShowFreeSpace = drive != null;

        var total = slices.Sum(s => s.Size);
        long free = 0;
        if (drive != null && IncludeFreeSpace)
        {
            free = drive.FreeSpace;
            total += free;
        }

        var items = new List<ChartItem>(slices.Count + 1);
        var slot = 0;
        foreach (var slice in slices)
        {
            var brush = slice.Kind == SliceKind.Other ? ChartPalette.Other : ChartPalette.Series[slot++ % ChartPalette.MaxSeries];
            items.Add(new ChartItem(slice.Label, slice.Size, total == 0 ? 0 : (double)slice.Size / total, brush, slice.Node, slice.Kind));
        }

        if (free > 0)
            items.Add(new ChartItem(Loc.T("chart.free_space"), free, (double)free / total, ChartPalette.FreeSpace, null, SliceKind.FreeSpace));

        ChartItems = items;
    }

    private async void RefreshDetails()
    {
        _detailsCts?.Cancel();
        var node = ChartNode;
        var tab = SelectedTab;
        if (node == null || tab == DetailTab.Chart)
        {
            if (node == null)
            {
                Extensions = [];
                TopFiles = [];
                AgeRows = [];
            }

            return;
        }

        var cts = _detailsCts = new CancellationTokenSource();
        IsComputingDetails = true;
        try
        {
            switch (tab)
            {
                case DetailTab.Extensions:
                    var ext = await Task.Run(() => Analysis.ByExtension(node, cts.Token), cts.Token);
                    Extensions = ext.Take(500).Select(e => new ExtensionRow(e.Extension, e.FileCount, e.Size, e.Share)).ToList();
                    break;
                case DetailTab.TopFiles:
                    var top = await Task.Run(() => Analysis.LargestFiles(node, 100, cts.Token), cts.Token);
                    TopFiles = top.Select((f, i) => new FileRow(f, i + 1)).ToList();
                    break;
                case DetailTab.Age:
                    var ages = await Task.Run(() => Analysis.ByAge(node, DateTime.UtcNow, cts.Token), cts.Token);
                    var max = Math.Max(1, ages.Max(a => a.Size));
                    AgeRows = ages.Select(a => new AgeRow(a.Label, a.FileCount, a.Size, a.Share, (double)a.Size / max)).ToList();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer request superseded this one.
        }
        finally
        {
            if (ReferenceEquals(_detailsCts, cts)) IsComputingDetails = false;
        }
    }
}
