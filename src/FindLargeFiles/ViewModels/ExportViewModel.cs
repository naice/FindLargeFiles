using CommunityToolkit.Mvvm.ComponentModel;
using FindLargeFiles.Core;
using FindLargeFiles.Reporting;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.ViewModels;

/// <summary>Choices of the export dialog. Static so the last choice is remembered for the session.</summary>
public sealed partial class ExportViewModel : ObservableObject
{
    // Only the choices are remembered, never the nodes, so an old scan tree is not kept alive.
    private static (ReportFormat Format, ReportOptions Options, bool Open)? _last;

    private ExportViewModel(FsNode selection, FsNode root)
    {
        Selection = selection;
        Root = root;
    }

    public static ExportViewModel Create(FsNode selection, FsNode root)
    {
        var vm = new ExportViewModel(selection, root) { UseSelection = !ReferenceEquals(selection, root) };
        if (_last is { } last)
        {
            var o = last.Options;
            vm.Format = last.Format;
            vm.IncludeTree = o.IncludeTree;
            vm.IncludeFilesInTree = o.IncludeFilesInTree;
            vm.TreeDepth = o.TreeDepth;
            vm.IncludeTopFiles = o.IncludeTopFiles;
            vm.TopFileCount = o.TopFileCount;
            vm.IncludeExtensions = o.IncludeExtensions;
            vm.IncludeAge = o.IncludeAge;
            vm.OpenAfterExport = last.Open;
        }

        return vm;
    }

    public void Remember() => _last = (Format, ToOptions(), OpenAfterExport);

    public FsNode Selection { get; }
    public FsNode Root { get; }
    public FsNode Target => UseSelection ? Selection : Root;
    public bool HasSeparateSelection => !ReferenceEquals(Selection, Root);
    public string SelectionText => $"{Selection.FullPath}  ·  {ByteFormatter.Format(Selection.Size)}";
    public string RootText => $"{Root.FullPath}  ·  {ByteFormatter.Format(Root.Size)}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormatHint))]
    public partial ReportFormat Format { get; set; } = ReportFormat.Excel;

    [ObservableProperty] public partial bool UseSelection { get; set; }
    [ObservableProperty] public partial bool IncludeTree { get; set; } = true;
    [ObservableProperty] public partial bool IncludeFilesInTree { get; set; } = true;
    [ObservableProperty] public partial int TreeDepth { get; set; } = 3;
    [ObservableProperty] public partial bool IncludeTopFiles { get; set; } = true;
    [ObservableProperty] public partial int TopFileCount { get; set; } = 100;
    [ObservableProperty] public partial bool IncludeExtensions { get; set; } = true;
    [ObservableProperty] public partial bool IncludeAge { get; set; } = true;
    [ObservableProperty] public partial bool OpenAfterExport { get; set; } = true;

    public string FormatHint => Format switch
    {
        ReportFormat.Excel => Loc.T("export.hint_excel"),
        ReportFormat.Pdf => Loc.T("export.hint_pdf"),
        ReportFormat.Csv => Loc.T("export.hint_csv"),
        _ => Loc.T("export.hint_html"),
    };

    public ReportOptions ToOptions() => new()
    {
        IncludeTree = IncludeTree,
        IncludeFilesInTree = IncludeFilesInTree,
        TreeDepth = TreeDepth,
        IncludeTopFiles = IncludeTopFiles,
        TopFileCount = TopFileCount,
        IncludeExtensions = IncludeExtensions,
        IncludeAge = IncludeAge,
    };

    public static string Filter(ReportFormat format) => format switch
    {
        ReportFormat.Excel => Loc.T("export.filter_excel"),
        ReportFormat.Pdf => Loc.T("export.filter_pdf"),
        ReportFormat.Csv => Loc.T("export.filter_csv"),
        _ => Loc.T("export.filter_html"),
    };
}
