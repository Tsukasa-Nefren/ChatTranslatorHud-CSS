using ChatTranslatorHud.Utils;

namespace ChatTranslatorHud.Services;

public sealed record CountdownTextTemplate(string Prefix, string Suffix);

public sealed class ActiveHudMessage
{
    private ActiveHudMessage()
    {
    }

    public MessageType Type { get; private init; }
    public DateTimeOffset ExpiryTime { get; private init; }
    public string StaticText { get; private init; } = "";
    public string OriginalText { get; private init; } = "";
    public string Prefix { get; private init; } = "";
    public string Suffix { get; private init; } = "";
    public bool IsMmss { get; private init; }
    public string Unit { get; private init; } = "";
    public int DisplayThresholdSeconds { get; private init; }
    public IReadOnlyDictionary<string, CountdownTextTemplate> PerLanguageTemplates { get; private init; } =
        new Dictionary<string, CountdownTextTemplate>();

    public static ActiveHudMessage Static(string text, DateTimeOffset expiryTime, string? originalText = null)
    {
        return new ActiveHudMessage
        {
            Type = MessageType.Static,
            ExpiryTime = expiryTime,
            StaticText = text,
            OriginalText = originalText ?? text
        };
    }

    public static ActiveHudMessage Countdown(
        string prefix,
        int seconds,
        string suffix,
        bool isMmss,
        string unit,
        DateTimeOffset createdAt,
        int displayThresholdSeconds,
        string? originalText = null,
        IReadOnlyDictionary<string, CountdownTextTemplate>? perLanguageTemplates = null)
    {
        return new ActiveHudMessage
        {
            Type = MessageType.Countdown,
            ExpiryTime = createdAt.AddSeconds(seconds),
            Prefix = prefix,
            Suffix = suffix,
            IsMmss = isMmss,
            Unit = unit,
            OriginalText = originalText ?? "",
            DisplayThresholdSeconds = displayThresholdSeconds,
            PerLanguageTemplates = perLanguageTemplates ?? new Dictionary<string, CountdownTextTemplate>()
        };
    }

    public bool ShouldDisplay(DateTimeOffset now)
    {
        if (Type == MessageType.Static)
            return now < ExpiryTime;

        var seconds = GetRemainingSeconds(now);
        return seconds > 0 && seconds <= DisplayThresholdSeconds;
    }

    public string GetCurrentText(DateTimeOffset now, string? language = null)
    {
        if (Type == MessageType.Static)
            return StaticText;

        var seconds = GetRemainingSeconds(now);
        if (language is not null &&
            PerLanguageTemplates.TryGetValue(language.ToUpperInvariant(), out var languageTemplate))
        {
            return $"{languageTemplate.Prefix}{seconds}{languageTemplate.Suffix}";
        }

        if (IsMmss)
        {
            var minutes = seconds / 60;
            var remainingSeconds = seconds % 60;
            return $"{Prefix}{minutes:00}:{remainingSeconds:00}{Suffix}";
        }

        var middle = string.IsNullOrEmpty(Unit) ? $"{seconds}" : $"{seconds} {Unit}";
        return $"{Prefix}{middle}{Suffix}";
    }

    private int GetRemainingSeconds(DateTimeOffset now)
    {
        var seconds = (int)Math.Ceiling((ExpiryTime - now).TotalSeconds);
        return Math.Max(0, seconds);
    }
}
