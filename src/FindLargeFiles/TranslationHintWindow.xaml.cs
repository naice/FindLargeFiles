using System.Globalization;
using System.IO;
using System.Windows;
using FindLargeFiles.Core.Localization;
using FindLargeFiles.Localization;
using FindLargeFiles.Services;

namespace FindLargeFiles;

/// <summary>Explains, once, how to add a translation when the Windows language is not translated yet.</summary>
public partial class TranslationHintWindow : Window
{
    public TranslationHintWindow()
    {
        InitializeComponent();

        var requested = LanguageSetup.Requested;
        var neutral = CultureInfo.GetCultureInfo(requested.TwoLetterISOLanguageName);
        var language = neutral.NativeName == neutral.EnglishName ? neutral.EnglishName : $"{neutral.NativeName} ({neutral.EnglishName})";

        HeadingText.Text = Loc.T("translation_hint.heading", ("language", language));
        StepCreateText.Text = Loc.T("translation_hint.step_create", ("file", LanguageSetup.TranslationFileName));
        StepTranslateText.Text = Loc.T("translation_hint.step_translate", ("example", "{count}"));
        FolderText.Text = LanguageSetup.UserFolder;
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = LanguageSetup.CreateTranslationTemplate();
            CreatedText.Text = Loc.T("translation_hint.file_created", ("path", path));
            CreatedText.Visibility = Visibility.Visible;
            Shell.ShowInExplorer(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CreatedText.Text = ex.Message;
            CreatedText.Visibility = Visibility.Visible;
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LanguageSetup.UserFolder);
            Shell.Open(LanguageSetup.UserFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            CreatedText.Text = ex.Message;
            CreatedText.Visibility = Visibility.Visible;
        }
    }
}
