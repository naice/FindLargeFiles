using System.IO;
using System.Windows.Media;
using FindLargeFiles.Core;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.ViewModels;

public sealed class DriveItem
{
    public DriveItem(DriveInfo drive)
    {
        RootPath = drive.RootDirectory.FullName;
        Letter = drive.Name.TrimEnd('\\');
        Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? DefaultLabel(drive.DriveType) : drive.VolumeLabel;
        Kind = drive.DriveType;
        TotalSize = drive.TotalSize;
        FreeSpace = drive.AvailableFreeSpace;
        Format = drive.DriveFormat;
    }

    public string RootPath { get; }
    public string Letter { get; }
    public string Label { get; }
    public DriveType Kind { get; }
    public string Format { get; }
    public long TotalSize { get; }
    public long FreeSpace { get; }
    public long UsedSpace => TotalSize - FreeSpace;
    public double UsedShare => TotalSize <= 0 ? 0 : (double)UsedSpace / TotalSize;
    public string UsedShareText => UsedShare.ToString("P0");
    public string SpaceText => Loc.T("drives.space_free", ("free", ByteFormatter.Format(FreeSpace)), ("total", ByteFormatter.Format(TotalSize)));
    public string ToolTip => Loc.T("drives.tooltip", ("letter", Letter), ("label", Label), ("format", Format),
        ("used", ByteFormatter.Format(UsedSpace)), ("free", ByteFormatter.Format(FreeSpace)), ("total", ByteFormatter.Format(TotalSize)));

    /// <summary>normal / warning (≥ 80 %) / critical (≥ 92 %) – drives the usage bar color.</summary>
    public string Level => UsedShare >= 0.92 ? "critical" : UsedShare >= 0.80 ? "warning" : "normal";

    /// <summary>Returns one loader per drive; each may block while the disk spins up or the network times out.</summary>
    public static IEnumerable<(string RootPath, Func<DriveItem?> Load)> Load()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            yield return (drive.RootDirectory.FullName, () =>
            {
                try
                {
                    return drive.IsReady ? new DriveItem(drive) : null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return null;
                }
            });
        }
    }

    private static string DefaultLabel(DriveType type) => type switch
    {
        DriveType.Fixed => Loc.T("drives.type_fixed"),
        DriveType.Removable => Loc.T("drives.type_removable"),
        DriveType.Network => Loc.T("drives.type_network"),
        DriveType.CDRom => Loc.T("drives.type_optical"),
        DriveType.Ram => Loc.T("drives.type_ram"),
        _ => Loc.T("drives.type_unknown"),
    };
}

/// <summary>A chart segment with its resolved color, shared by the chart and its legend.</summary>
public sealed record ChartItem(string Label, long Size, double Share, Brush Brush, FsNode? Node, SliceKind Kind)
{
    public string SizeText => ByteFormatter.Format(Size);
    public string ShareText => Share.ToString("P1");
    public bool CanNavigate => Node != null;
}

public sealed record ExtensionRow(string Extension, long FileCount, long Size, double Share)
{
    public string SizeText => ByteFormatter.Format(Size);
    public string ShareText => Share.ToString("P1");
    public string CountText => FileCount.ToString("N0");
}

public sealed record AgeRow(string Label, long FileCount, long Size, double Share, double Relative)
{
    public string SizeText => ByteFormatter.Format(Size);
    public string ShareText => Share.ToString("P1");
    public string CountText => Loc.Plural("global.file_count", FileCount);
}

public sealed record FileRow(FsNode Node, int Rank)
{
    public string Name => Node.Name;
    public string Folder => Node.Parent?.FullPath ?? string.Empty;
    public string SizeText => ByteFormatter.Format(Node.Size);
    public string ModifiedText => Node.LastWriteUtcTicks == 0 ? string.Empty : Node.LastWriteTime.ToString("g");
}

public sealed record Crumb(string Label, FsNode Node, bool IsLast);

/// <summary>One fixed slot of the stats strip; created once so the strip does not re-layout on selection changes.</summary>
public sealed partial class Stat(string label, double width) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public static string Empty => Loc.T("global.not_available");

    /// <summary>Content width of the slot in DIPs; NaN lets the slot fill the remaining space.</summary>
    public double Width { get; } = width;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] public partial string Label { get; set; } = label;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] public partial string Value { get; set; } = Empty;
}
