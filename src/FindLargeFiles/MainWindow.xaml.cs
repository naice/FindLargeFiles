using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FindLargeFiles.Localization;
using FindLargeFiles.Services;
using FindLargeFiles.ViewModels;

namespace FindLargeFiles;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        WindowPlacement.Attach(this, "Window");
        DataContext = _vm;
        _vm.RevealRequested += row => Dispatcher.BeginInvoke(() =>
        {
            TreeList.ScrollIntoView(row);
            if (TreeList.ItemContainerGenerator.ContainerFromItem(row) is ListViewItem item) item.Focus();
        });

        _vm.ShowExportDialog = options => new ExportWindow(options) { Owner = this }.ShowDialog() == true;
        Loaded += OnLoaded;
        TreeList.SizeChanged += (_, _) => FitColumn(TreeList, 0, 180);
        TopList.SizeChanged += (_, _) => FitColumn(TopList, 1, 120);
        StateChanged += (_, _) => UpdateMaximizedState();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.OwnerHandle = new WindowInteropHelper(this).Handle;
        UpdateSortIndicator();
        if (LanguageSetup.ShouldShowTranslationHint())
        {
            // Shown once per untranslated language; remembered in the registry.
            _ = Dispatcher.BeginInvoke(() =>
            {
                new TranslationHintWindow { Owner = this }.ShowDialog();
                LanguageSetup.MarkTranslationHintShown();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && Directory.Exists(args[1])) await _vm.ScanAsync(args[1]);
    }

    // ───────────── Window chrome ─────────────

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizedState()
    {
        // A maximized chrome-less window extends past the screen edges by the resize border.
        RootBorder.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    // ───────────── Tree list interaction ─────────────

    /// <summary>Lets one column take whatever width the other columns leave.</summary>
    private static void FitColumn(ListView list, int index, double minWidth)
    {
        if (list.View is not GridView view || view.Columns.Count <= index) return;
        var others = view.Columns.Where((_, i) => i != index).Sum(c => c.ActualWidth);
        view.Columns[index].Width = Math.Max(minWidth, list.ActualWidth - others - 24);
    }

    private void OnTreeHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Tag: string field }) return;
        _vm.SortCommand.Execute(field);
        UpdateSortIndicator();
    }

    /// <summary>Appends ↓/↑ to the header of the sorted column (the texts themselves come from the translations).</summary>
    private void UpdateSortIndicator()
    {
        if (TreeList.View is not GridView view) return;
        var arrow = _vm.Tree.SortDescending ? " ↓" : " ↑";
        GridViewColumnHeader? marked = null;
        foreach (var column in view.Columns)
        {
            if (column.Header is not GridViewColumnHeader header) continue;
            var text = ((string)header.Content).TrimEnd('↓', '↑', ' ');
            var isSorted = marked == null && (string)header.Tag == _vm.Tree.SortField.ToString();
            header.Content = isSorted ? text + arrow : text;
            if (isSorted) marked = header;
        }
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IsInside<System.Windows.Controls.Primitives.ToggleButton>(e.OriginalSource as DependencyObject)) return;
        if (TreeList.SelectedItem is not NodeRow row || !IsInside<ListViewItem>(e.OriginalSource as DependencyObject)) return;
        if (row.IsOverflow) _vm.NavigateCommand.Execute(row);
        else if (row.CanExpand) row.IsExpanded = !row.IsExpanded;
        else _vm.OpenItemCommand.Execute(row);
        e.Handled = true;
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (TreeList.SelectedItem is not NodeRow row) return;
        switch (e.Key)
        {
            case Key.Right when row.CanExpand && !row.IsExpanded:
                row.IsExpanded = true;
                break;
            case Key.Left when row.IsExpanded:
                row.IsExpanded = false;
                break;
            case Key.Left when row.ParentRow != null:
            case Key.Back:
                _vm.GoUpCommand.Execute(null);
                break;
            case Key.Enter when row.IsOverflow:
                _vm.NavigateCommand.Execute(row);
                break;
            case Key.Enter:
                _vm.OpenItemCommand.Execute(row);
                break;
            case Key.Delete:
                _vm.RecycleCommand.Execute(row);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnTopFileDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TopList.SelectedItem is FileRow file && IsInside<ListViewItem>(e.OriginalSource as DependencyObject))
            _vm.NavigateCommand.Execute(file);
    }

    private static bool IsInside<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var d = source; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is T) return true;
            if (d is System.Windows.Controls.Primitives.ScrollBar or ListView) return false;
        }

        return false;
    }
}
