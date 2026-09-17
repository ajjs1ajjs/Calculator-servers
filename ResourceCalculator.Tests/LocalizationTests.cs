using ResourceCalculator.Localization;

namespace ResourceCalculator.Tests;

public class LocalizationTests
{
    // Ключі uk/en мусять збігатися один-в-один: пропущений ключ в одному словнику
    // перетворює підпис у голий ключ ("col.metric") просто від перемикання мови,
    // і помітно це лише вручну на потрібній вкладці.
    [Fact]
    public void UkAndEnDictionaries_HaveIdenticalKeySets()
    {
        var loc = LocalizationService.Instance;

        loc.LoadLanguage("uk");
        var uk = loc.Keys.ToHashSet(StringComparer.Ordinal);
        loc.LoadLanguage("en");
        var en = loc.Keys.ToHashSet(StringComparer.Ordinal);
        loc.LoadLanguage("uk");

        Assert.NotEmpty(uk);
        var missingInEn = uk.Except(en).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var missingInUk = en.Except(uk).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Empty(missingInEn);
        Assert.Empty(missingInUk);
    }

    [Fact]
    public void NoKeyHasEmptyTranslation()
    {
        var loc = LocalizationService.Instance;
        foreach (var lang in new[] { "uk", "en" })
        {
            loc.LoadLanguage(lang);
            foreach (var key in loc.Keys)
                Assert.False(string.IsNullOrWhiteSpace(loc[key]), $"{lang}: порожній переклад для '{key}'");
        }
        loc.LoadLanguage("uk");
    }
}
