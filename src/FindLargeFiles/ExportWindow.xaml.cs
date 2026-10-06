using System.Windows;
using FindLargeFiles.ViewModels;

namespace FindLargeFiles;

public partial class ExportWindow : Window
{
    public ExportWindow(ExportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnExport(object sender, RoutedEventArgs e) => DialogResult = true;
}
