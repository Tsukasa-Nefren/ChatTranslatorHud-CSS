using System.Globalization;
using System.Collections.Concurrent;

namespace ChatTranslatorHud.Services;

public sealed class PlayerTranslationService(string defaultTargetLanguage)
{
    private readonly ConcurrentDictionary<ulong, string> _playerLanguages = new();
    private readonly string _defaultTargetLanguage = NormalizeLanguage(defaultTargetLanguage);

    private static readonly Dictionary<string, LanguageMapping> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["english"] = new("EN", "en"),
        ["en"] = new("EN", "en"),
        ["en-US"] = new("EN", "en"),
        ["en-GB"] = new("EN", "en-GB"),
        ["korean"] = new("KO", "ko-KR"),
        ["koreana"] = new("KO", "ko-KR"),
        ["ko"] = new("KO", "ko-KR"),
        ["ko-KR"] = new("KO", "ko-KR"),
        ["schinese"] = new("ZH", "zh-CN"),
        ["zh"] = new("ZH", "zh-CN"),
        ["zh-CN"] = new("ZH", "zh-CN"),
        ["tchinese"] = new("ZH", "zh-TW"),
        ["zh-TW"] = new("ZH", "zh-TW"),
        ["japanese"] = new("JA", "ja-JP"),
        ["ja"] = new("JA", "ja-JP"),
        ["ja-JP"] = new("JA", "ja-JP"),
        ["russian"] = new("RU", "ru-RU"),
        ["ru"] = new("RU", "ru-RU"),
        ["ru-RU"] = new("RU", "ru-RU"),
        ["german"] = new("DE", "de-DE"),
        ["de"] = new("DE", "de-DE"),
        ["de-DE"] = new("DE", "de-DE"),
        ["french"] = new("FR", "fr-FR"),
        ["fr"] = new("FR", "fr-FR"),
        ["fr-FR"] = new("FR", "fr-FR"),
        ["spanish"] = new("ES", "es-ES"),
        ["es"] = new("ES", "es-ES"),
        ["es-ES"] = new("ES", "es-ES"),
        ["latam"] = new("ES", "es-419"),
        ["es-419"] = new("ES", "es-419"),
        ["portuguese"] = new("PT-PT", "pt-PT"),
        ["pt"] = new("PT-PT", "pt-PT"),
        ["pt-PT"] = new("PT-PT", "pt-PT"),
        ["brazilian"] = new("PT-BR", "pt-BR"),
        ["pt-BR"] = new("PT-BR", "pt-BR"),
        ["italian"] = new("IT", "it-IT"),
        ["it"] = new("IT", "it-IT"),
        ["it-IT"] = new("IT", "it-IT"),
        ["polish"] = new("PL", "pl-PL"),
        ["pl"] = new("PL", "pl-PL"),
        ["pl-PL"] = new("PL", "pl-PL"),
        ["turkish"] = new("TR", "tr-TR"),
        ["tr"] = new("TR", "tr-TR"),
        ["tr-TR"] = new("TR", "tr-TR"),
        ["dutch"] = new("NL", "nl-NL"),
        ["nl"] = new("NL", "nl-NL"),
        ["nl-NL"] = new("NL", "nl-NL"),
        ["danish"] = new("DA", "da-DK"),
        ["da"] = new("DA", "da-DK"),
        ["da-DK"] = new("DA", "da-DK"),
        ["finnish"] = new("FI", "fi-FI"),
        ["fi"] = new("FI", "fi-FI"),
        ["fi-FI"] = new("FI", "fi-FI"),
        ["norwegian"] = new("NB", "nb-NO"),
        ["nb"] = new("NB", "nb-NO"),
        ["nb-NO"] = new("NB", "nb-NO"),
        ["swedish"] = new("SV", "sv-SE"),
        ["sv"] = new("SV", "sv-SE"),
        ["sv-SE"] = new("SV", "sv-SE"),
        ["czech"] = new("CS", "cs-CZ"),
        ["cs"] = new("CS", "cs-CZ"),
        ["cs-CZ"] = new("CS", "cs-CZ"),
        ["hungarian"] = new("HU", "hu-HU"),
        ["hu"] = new("HU", "hu-HU"),
        ["hu-HU"] = new("HU", "hu-HU"),
        ["romanian"] = new("RO", "ro-RO"),
        ["ro"] = new("RO", "ro-RO"),
        ["ro-RO"] = new("RO", "ro-RO"),
        ["bulgarian"] = new("BG", "bg-BG"),
        ["bg"] = new("BG", "bg-BG"),
        ["bg-BG"] = new("BG", "bg-BG"),
        ["greek"] = new("EL", "el-GR"),
        ["el"] = new("EL", "el-GR"),
        ["el-GR"] = new("EL", "el-GR"),
        ["ukrainian"] = new("UK", "uk-UA"),
        ["uk"] = new("UK", "uk-UA"),
        ["uk-UA"] = new("UK", "uk-UA"),
        ["indonesian"] = new("ID", "id-ID"),
        ["id"] = new("ID", "id-ID"),
        ["id-ID"] = new("ID", "id-ID"),
        ["thai"] = new("TH", "th-TH"),
        ["th"] = new("TH", "th-TH"),
        ["th-TH"] = new("TH", "th-TH"),
        ["vietnamese"] = new("VI", "vi-VN"),
        ["vi"] = new("VI", "vi-VN"),
        ["vi-VN"] = new("VI", "vi-VN")
    };

    public void SetLanguage(ulong steamId64, string steamLanguage)
    {
        if (LanguageMap.TryGetValue(steamLanguage, out var mapping))
        {
            _playerLanguages[steamId64] = mapping.DeepLTargetLanguage;
            return;
        }

        _playerLanguages.TryRemove(steamId64, out _);
    }

    public bool TryGetCultureInfo(string steamLanguage, out CultureInfo cultureInfo)
    {
        if (LanguageMap.TryGetValue(steamLanguage, out var mapping))
        {
            cultureInfo = CultureInfo.GetCultureInfo(mapping.CultureName);
            return true;
        }

        cultureInfo = CultureInfo.InvariantCulture;
        return false;
    }

    public string GetLanguage(ulong steamId64)
    {
        return _playerLanguages.GetValueOrDefault(steamId64, _defaultTargetLanguage);
    }

    public void Remove(ulong steamId64)
    {
        _playerLanguages.TryRemove(steamId64, out _);
    }

    private static string NormalizeLanguage(string language)
    {
        return string.IsNullOrWhiteSpace(language) ? "EN" : language.ToUpperInvariant();
    }

    private sealed record LanguageMapping(string DeepLTargetLanguage, string CultureName);
}
