using System.Globalization;
using FindLargeFiles.Core.Localization;

namespace FindLargeFiles.Core.Tests;

public sealed class LocalizationTests : IDisposable
{
    private readonly string _userFolder = Path.Combine(Path.GetTempPath(), "flf-loc-" + Guid.NewGuid().ToString("N"));

    public LocalizationTests() => Directory.CreateDirectory(_userFolder);

    public void Dispose()
    {
        try { Directory.Delete(_userFolder, true); } catch { /* best effort */ }
    }

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var language in Localizer.BuiltInLanguages) data.Add(language);
        return data;
    }

    [Fact]
    public void AllRequestedLanguagesShip() =>
        Assert.Equal(["de", "en", "es", "fr", "hi", "it", "pt"], Localizer.BuiltInLanguages);

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryLanguageHasExactlyTheEnglishKeys(string language)
    {
        var english = Localizer.Flatten(Localizer.ReadBuiltIn("en")!);
        var other = Localizer.Flatten(Localizer.ReadBuiltIn(language)!);

        Assert.Empty(english.Keys.Except(other.Keys));
        Assert.Empty(other.Keys.Except(english.Keys));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryTranslationKeepsThePlaceholders(string language)
    {
        var english = Localizer.Flatten(Localizer.ReadBuiltIn("en")!);
        var other = Localizer.Flatten(Localizer.ReadBuiltIn(language)!);

        var mismatches = english
            .Where(kv => !Localizer.Placeholders(kv.Value).SetEquals(Localizer.Placeholders(other[kv.Key])))
            .Select(kv => kv.Key)
            .ToList();
        Assert.Empty(mismatches);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void KeysFollowTheNamingConvention(string language)
    {
        // snake_case segments, dot-separated, at most three levels (see lokalise.com naming guide).
        foreach (var key in Localizer.Flatten(Localizer.ReadBuiltIn(language)!).Keys)
        {
            var parts = key.Split('.');
            Assert.InRange(parts.Length, 2, 3);
            Assert.All(parts, p => Assert.Matches("^[a-z][a-z0-9_]*$", p));
        }
    }

    [Fact]
    public void RegionalCultureUsesItsLanguage()
    {
        var loc = Localizer.Load(CultureInfo.GetCultureInfo("de-AT"));
        Assert.True(loc.IsTranslated);
        Assert.Equal("de", loc.Culture.Name);
        Assert.Equal("Abbrechen", loc.T("global.cancel"));
    }

    [Fact]
    public void MissingLanguageFallsBackToEnglish()
    {
        var loc = Localizer.Load(CultureInfo.GetCultureInfo("ja-JP"));
        Assert.False(loc.IsTranslated);
        Assert.Equal("en", loc.Culture.Name);
        Assert.Equal("Cancel", loc.T("global.cancel"));
    }

    [Fact]
    public void UserFileAddsLanguageAndMissingKeysFallBackToEnglish()
    {
        File.WriteAllText(Path.Combine(_userFolder, "ja.json"), """{ "global": { "cancel": "キャンセル" } }""");

        var loc = Localizer.Load(CultureInfo.GetCultureInfo("ja-JP"), [_userFolder]);

        Assert.True(loc.IsTranslated);
        Assert.Equal("キャンセル", loc.T("global.cancel"));
        Assert.Equal("Close", loc.T("global.close"));
        Assert.EndsWith("ja.json", loc.TranslationFile);
    }

    [Fact]
    public void UserFileOverridesBuiltInText()
    {
        File.WriteAllText(Path.Combine(_userFolder, "de.json"), """{ "global": { "cancel": "Abbruch" } }""");
        var loc = Localizer.Load(CultureInfo.GetCultureInfo("de-DE"), [_userFolder]);
        Assert.Equal("Abbruch", loc.T("global.cancel"));
        Assert.Equal("Schließen", loc.T("global.close"));
    }

    [Fact]
    public void PlaceholdersAndPluralsAreFilled()
    {
        var de = Localizer.Load(CultureInfo.GetCultureInfo("de"));
        Assert.Equal("Laufwerk C:", de.T("stats.drive_with_letter", ("letter", "C:")));
        Assert.Equal("1 weiteres Element", de.Plural("report.more_items", 1));
        Assert.EndsWith("weitere Elemente", de.Plural("report.more_items", 3));

        var fr = Localizer.Load(CultureInfo.GetCultureInfo("fr"));
        Assert.Equal("0 fichier", fr.Plural("global.file_count", 0));
    }

    [Fact]
    public void UnknownKeyReturnsTheKey() => Assert.Equal("does.not_exist", Localizer.English.T("does.not_exist"));
}
