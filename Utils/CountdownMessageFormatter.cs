namespace ChatTranslatorHud.Utils;

public static class CountdownMessageFormatter
{
    public static bool ShouldTranslate(ParseResult parseResult)
    {
        return parseResult.IsValid &&
            (ContainsLetter(parseResult.Prefix) ||
                ContainsLetter(parseResult.Suffix) ||
                ContainsLetter(parseResult.Unit));
    }

    public static string FormatForConsoleChat(ParseResult parseResult, string originalMessage)
    {
        if (!parseResult.IsValid || ShouldTranslate(parseResult))
            return originalMessage;

        return FormatCountdownText(
            parseResult.Prefix,
            parseResult.Seconds,
            parseResult.Suffix,
            parseResult.IsMmss,
            parseResult.Unit);
    }

    public static string FormatForStaticHud(ParseResult parseResult, string displayText)
    {
        if (!parseResult.IsValid || ShouldTranslate(parseResult))
            return displayText;

        return FormatCountdownText(
            parseResult.Prefix,
            parseResult.Seconds,
            parseResult.Suffix,
            parseResult.IsMmss,
            parseResult.Unit);
    }

    public static string FormatCountdownText(string prefix, int seconds, string suffix, bool isMmss, string unit)
    {
        var middle = FormatMiddle(seconds, isMmss, unit);
        return FormatWithDecoration(prefix, middle, suffix);
    }

    public static string FormatWithDecoration(string prefix, string middle, string suffix)
    {
        var hasVerticalBars = ContainsVerticalBarDecoration(prefix) || ContainsVerticalBarDecoration(suffix);
        var text = $"{RemoveVerticalBarDecoration(prefix)}{middle}{RemoveVerticalBarDecoration(suffix)}";
        return hasVerticalBars ? text.Trim() : text;
    }

    private static string FormatMiddle(int seconds, bool isMmss, string unit)
    {
        if (isMmss)
        {
            var minutes = seconds / 60;
            var remainingSeconds = seconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        return string.IsNullOrEmpty(unit) ? $"{seconds}" : $"{seconds} {unit}";
    }

    private static bool ContainsLetter(string value)
    {
        return value.Any(char.IsLetter);
    }

    private static bool ContainsVerticalBarDecoration(string value)
    {
        return value.Any(IsVerticalBarDecoration);
    }

    private static string RemoveVerticalBarDecoration(string value)
    {
        return new string(value.Where(character => !IsVerticalBarDecoration(character)).ToArray());
    }

    private static bool IsVerticalBarDecoration(char character)
    {
        return character is '|' or '\u2502' or '\u2503' or '\uFF5C';
    }
}
