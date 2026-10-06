using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using FindLargeFiles.Core.Localization;
using Microsoft.Win32;

namespace FindLargeFiles.Localization;

/// <summary>
/// Chooses the UI language at startup: the Windows display language (or a <c>Language</c> override in
/// HKCU\Software\FindLargeFiles\Localization), falling back to English when no translation exists.
/// Translation files placed in <see cref="UserFolder"/> or next to the executable are picked up too.
/// </summary>
public static class LanguageSetup
{
    private const string RegistryPath = @"Software\FindLargeFiles\Localization";
    private const string OverrideValue = "Language";
    private const string HintShownValue = "TranslationHintShownFor";

    /// <summary>Writable folder for user translations: %LOCALAPPDATA%\FindLargeFiles\Localization.</summary>
    public static string UserFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FindLargeFiles", "Localization");

    /// <summary>The language the user asked for (system or override), even if it is not translated.</summary>
    public static CultureInfo Requested { get; private set; } = CultureInfo.CurrentUICulture;

    public static void Apply()
    {
        Requested = ReadOverride() ?? CultureInfo.CurrentUICulture;
        var folders = new List<string> { UserFolder };
        if (Path.GetDirectoryName(Environment.ProcessPath) is { } exeDir) folders.Add(Path.Combine(exeDir, "Localization"));

        Localizer localizer;
        try
        {
            localizer = Localizer.Load(Requested, folders);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // A broken user file must not keep the app from starting.
            localizer = Localizer.Load(Requested);
        }

        Loc.Use(localizer);
        CultureInfo.DefaultThreadCurrentUICulture = localizer.Culture;
        Thread.CurrentThread.CurrentUICulture = localizer.Culture;

        // Numbers and dates keep following the Windows regional format.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
    }

    /// <summary>True once per missing language: English is shown and the user has not been told yet.</summary>
    public static bool ShouldShowTranslationHint()
    {
        if (Loc.Current.IsTranslated) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return !string.Equals(key?.GetValue(HintShownValue) as string, Requested.Name, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void MarkTranslationHintShown()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue(HintShownValue, Requested.Name, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            // Then the hint simply appears again next time.
        }
    }

    /// <summary>File name a new translation for the requested language should get, e.g. <c>ja.json</c>.</summary>
    public static string TranslationFileName => Requested.TwoLetterISOLanguageName + ".json";

    /// <summary>Writes the English texts as a starting point for a translation and returns its path.</summary>
    public static string CreateTranslationTemplate()
    {
        Directory.CreateDirectory(UserFolder);
        var path = Path.Combine(UserFolder, TranslationFileName);
        if (!File.Exists(path)) File.WriteAllText(path, Localizer.ReadBuiltIn(Localizer.FallbackLanguage));
        return path;
    }

    private static CultureInfo? ReadOverride()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(OverrideValue) is string { Length: > 0 } name ? CultureInfo.GetCultureInfo(name) : null;
        }
        catch (Exception ex) when (ex is CultureNotFoundException or System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
