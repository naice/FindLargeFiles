using System.Windows;
using System.Windows.Threading;
using FindLargeFiles.Core.Localization;
using FindLargeFiles.Localization;

namespace FindLargeFiles;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        // Must run before the main window's XAML is loaded, which reads the translations.
        LanguageSetup.Apply();
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, Loc.T("app.unexpected_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
