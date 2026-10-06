using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FindLargeFiles.Core.Localization;

/// <summary>
/// Translations from nested JSON files named after the locale (<c>en.json</c>, <c>de.json</c>, <c>pt-BR.json</c> …).
/// Built-in files are embedded; files in user folders override or add languages. Keys are flattened
/// with dots (<c>sidebar.scan_folder</c>); a key missing in the chosen language falls back to English.
/// </summary>
public sealed partial class Localizer
{
    public const string FallbackLanguage = "en";
    private const string ResourcePrefix = "FindLargeFiles.Core.Localization.";

    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, string> _fallback;

    private Localizer(CultureInfo culture, Dictionary<string, string> strings, Dictionary<string, string> fallback, bool isTranslated, string? translationFile)
    {
        Culture = culture;
        _strings = strings;
        _fallback = fallback;
        IsTranslated = isTranslated;
        TranslationFile = translationFile;
    }

    /// <summary>The language whose texts are shown (English when the requested one is missing).</summary>
    public CultureInfo Culture { get; }

    /// <summary>False when no translation exists for the requested culture and English is used instead.</summary>
    public bool IsTranslated { get; }

    /// <summary>The most specific file that provided the texts, if any came from a user folder.</summary>
    public string? TranslationFile { get; }

    public IReadOnlyDictionary<string, string> Strings => _strings;

    /// <summary>Languages that ship with the application.</summary>
    public static IReadOnlyList<string> BuiltInLanguages { get; } = typeof(Localizer).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
        .Select(n => n[ResourcePrefix.Length..^".json".Length])
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>English, built in. Used until <see cref="Load"/> is called and by tests.</summary>
    public static Localizer English { get; } = Load(CultureInfo.GetCultureInfo(FallbackLanguage));

    /// <summary>
    /// Loads the best match for <paramref name="requested"/>: e.g. for de-AT the files de-AT.json and de.json
    /// are merged (specific wins), user folders win over built-in files, and English fills every gap.
    /// </summary>
    public static Localizer Load(CultureInfo requested, IEnumerable<string>? userFolders = null)
    {
        var folders = userFolders?.Where(Directory.Exists).ToList() ?? [];
        var fallback = Flatten(ReadBuiltIn(FallbackLanguage) ?? "{}");

        // Most general first so that more specific cultures override it: de, then de-AT.
        var chain = new List<CultureInfo>();
        for (var c = requested; !Equals(c, CultureInfo.InvariantCulture); c = c.Parent) chain.Insert(0, c);

        var strings = new Dictionary<string, string>(fallback, StringComparer.Ordinal);
        var found = false;
        string? userFile = null;
        CultureInfo? used = null;
        foreach (var culture in chain)
        {
            var sources = new List<string>();
            if (ReadBuiltIn(culture.Name) is { } builtIn) sources.Add(builtIn);
            foreach (var folder in folders)
            {
                var file = Path.Combine(folder, culture.Name + ".json");
                if (!File.Exists(file)) continue;
                sources.Add(File.ReadAllText(file));
                userFile = file;
            }

            foreach (var json in sources)
            {
                foreach (var (key, value) in Flatten(json)) strings[key] = value;
                found = true;
                used = culture;
            }
        }

        var isEnglish = chain.Count > 0 && chain[0].Name == FallbackLanguage;
        return new Localizer(used ?? CultureInfo.GetCultureInfo(FallbackLanguage), strings, fallback, found || isEnglish, userFile);
    }

    /// <summary>The text for <paramref name="key"/>; English when missing, the key itself when unknown.</summary>
    public string this[string key] => T(key);

    public string T(string key) =>
        _strings.TryGetValue(key, out var value) ? value
        : _fallback.TryGetValue(key, out var english) ? english
        : key;

    /// <summary>Text with named placeholders, e.g. <c>T("scan.status_done", ("duration", "1,5 s"))</c>.</summary>
    public string T(string key, params (string Name, object? Value)[] args) => Fill(T(key), args);

    /// <summary>
    /// Picks <c>key_one</c> or <c>key_other</c> by count and fills <c>{count}</c> (formatted with the
    /// current culture) plus any further placeholders.
    /// </summary>
    public string Plural(string key, long count, params (string Name, object? Value)[] args)
    {
        var form = IsSingular(count) ? "_one" : "_other";
        return Fill(T(key + form), [("count", count.ToString("N0", CultureInfo.CurrentCulture)), .. args]);
    }

    private bool IsSingular(long count) => Culture.TwoLetterISOLanguageName switch
    {
        // In French and Hindi, zero also takes the singular form.
        "fr" or "hi" => count is 0 or 1,
        _ => count == 1,
    };

    private static string Fill(string template, (string Name, object? Value)[] args)
    {
        foreach (var (name, value) in args)
            template = template.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.CurrentCulture), StringComparison.Ordinal);
        return template;
    }

    /// <summary>The placeholders used in a text, e.g. <c>{count}</c> → <c>count</c>.</summary>
    public static IReadOnlySet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    public static string? ReadBuiltIn(string language)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"{ResourcePrefix}{language}.json");
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Turns nested JSON objects into dotted keys.</summary>
    public static Dictionary<string, string> Flatten(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        void Walk(JsonElement element, string prefix)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
                if (property.Value.ValueKind == JsonValueKind.Object) Walk(property.Value, key);
                else if (property.Value.ValueKind == JsonValueKind.String) result[key] = property.Value.GetString()!;
            }
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object) Walk(doc.RootElement, string.Empty);
        return result;
    }

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex PlaceholderPattern();
}

/// <summary>The active translation for the whole process. English until the app calls <see cref="Use"/>.</summary>
public static class Loc
{
    public static Localizer Current { get; private set; } = Localizer.English;

    public static void Use(Localizer localizer) => Current = localizer;

    public static string T(string key) => Current.T(key);
    public static string T(string key, params (string Name, object? Value)[] args) => Current.T(key, args);
    public static string Plural(string key, long count, params (string Name, object? Value)[] args) => Current.Plural(key, count, args);
}
