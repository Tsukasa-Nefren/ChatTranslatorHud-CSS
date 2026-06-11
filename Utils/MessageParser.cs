using System.Text.RegularExpressions;

namespace ChatTranslatorHud.Utils;

public enum MessageType
{
    Static,
    Countdown
}

public sealed class ParseResult
{
    public bool IsValid { get; init; }
    public MessageType Type { get; init; }
    public int Seconds { get; init; }
    public string Prefix { get; init; } = "";
    public string Suffix { get; init; } = "";
    public bool IsMmss { get; init; }
    public string Unit { get; init; } = "";
}

public static partial class MessageParser
{
    private const int MaxCountdownSeconds = 120;

    public static ParseResult TryParseMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsSystemMessage(message) || RatioPattern().IsMatch(message))
            return new ParseResult();

        if (TryParseMmss(message) is { } mmss)
            return mmss;

        if (TryParseUnit(message) is { } unit)
            return unit;

        if (TryParseSimpleNumber(message) is { } simple)
            return simple;

        if (TryParseBracketNumber(message) is { } bracket)
            return bracket;

        if (TryParseAnyNumber(message) is { } any)
            return any;

        return new ParseResult();
    }

    public static bool IsCountdownOnly(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsSystemMessage(message) || VersionPrefixPattern().IsMatch(message))
            return false;

        var letters = message.Count(char.IsLetter);
        var totalLength = message.Length;

        if (letters > 0 && (double)letters / totalLength > 0.3)
        {
            if (!CountdownUnitPattern().IsMatch(message))
                return false;

            if (DigitSequencePattern().Matches(message).Count >= 2)
                return false;
        }

        return TryParseMessage(message).IsValid;
    }

    private static ParseResult? TryParseMmss(string message)
    {
        var match = MmssPattern().Match(message);
        if (!match.Success)
            return null;

        var minutes = int.Parse(match.Groups[1].Value);
        var seconds = int.Parse(match.Groups[2].Value);
        var totalSeconds = minutes * 60 + seconds;
        if (!IsValidCountdownSeconds(totalSeconds))
            return null;

        return new ParseResult
        {
            IsValid = true,
            Type = MessageType.Countdown,
            Seconds = totalSeconds,
            Prefix = message[..match.Index],
            Suffix = message[(match.Index + match.Length)..],
            IsMmss = true
        };
    }

    private static ParseResult? TryParseUnit(string message)
    {
        var match = UnitPattern().Match(message);
        if (!match.Success)
            return null;

        var value = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value;
        var totalSeconds = IsMinuteUnit(unit) ? value * 60 : value;
        if (!IsValidCountdownSeconds(totalSeconds))
            return null;

        return new ParseResult
        {
            IsValid = true,
            Type = MessageType.Countdown,
            Seconds = totalSeconds,
            Prefix = message[..match.Index],
            Suffix = message[(match.Index + match.Length)..],
            Unit = unit
        };
    }

    private static ParseResult? TryParseSimpleNumber(string message)
    {
        var match = SimpleNumberPattern().Match(message);
        if (!match.Success)
            return null;

        var seconds = int.Parse(match.Groups[1].Value);
        if (!IsValidCountdownSeconds(seconds))
            return null;

        return new ParseResult
        {
            IsValid = true,
            Type = MessageType.Countdown,
            Seconds = seconds
        };
    }

    private static ParseResult? TryParseBracketNumber(string message)
    {
        var match = BracketNumberPattern().Match(message);
        if (!match.Success)
            return null;

        var seconds = int.Parse(match.Groups[1].Value);
        if (!IsValidCountdownSeconds(seconds))
            return null;

        var numberGroup = match.Groups[1];
        return new ParseResult
        {
            IsValid = true,
            Type = MessageType.Countdown,
            Seconds = seconds,
            Prefix = message[..numberGroup.Index],
            Suffix = message[(numberGroup.Index + numberGroup.Length)..]
        };
    }

    private static ParseResult? TryParseAnyNumber(string message)
    {
        var match = AnyNumberPattern().Match(message);
        if (!match.Success)
            return null;

        var seconds = int.Parse(match.Groups[1].Value);
        if (!IsValidCountdownSeconds(seconds))
            return null;

        return new ParseResult
        {
            IsValid = true,
            Type = MessageType.Countdown,
            Seconds = seconds,
            Prefix = message[..match.Index],
            Suffix = message[(match.Index + match.Length)..]
        };
    }

    private static bool IsValidCountdownSeconds(int seconds)
    {
        return seconds is > 0 and <= MaxCountdownSeconds;
    }

    private static bool IsMinuteUnit(string unit)
    {
        return unit.Equals("m", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("min", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("mins", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("minute", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("minutes", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("\uBD84", StringComparison.Ordinal);
    }

    private static bool IsSystemMessage(string message)
    {
        return StripperUpdateDotPattern().IsMatch(message)
            || StripperUpdateSlashPattern().IsMatch(message)
            || StripperUpdateIsoPattern().IsMatch(message)
            || StripperUpdatePrefixPattern().IsMatch(message)
            || StripperCs2Pattern().IsMatch(message)
            || ConsolePrefixPattern().IsMatch(message)
            || BracketStripperPattern().IsMatch(message);
    }

    [GeneratedRegex(@"\d+\s*[vVxX/]\s*\d+", RegexOptions.CultureInvariant)]
    private static partial Regex RatioPattern();

    [GeneratedRegex(@"(?<!\d)(\d{1,3}):([0-5]?\d)(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex MmssPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])(\d{1,4})\s*(s|sec|secs|second|seconds|m|min|mins|minute|minutes|\uCD08|\uBD84)(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnitPattern();

    [GeneratedRegex(@"^\s*(\d{1,4})\s*[.!?]?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SimpleNumberPattern();

    [GeneratedRegex(@"^[\s<>＜＞〈〉《》\[\]【】「」『』\(\)（）{}|:;""'`~*_.-]*(\d{1,4})[\s<>＜＞〈〉《》\[\]【】「」『』\(\)（）{}|:;""'`~*_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketNumberPattern();

    [GeneratedRegex(@"\b(\d{1,4})\b", RegexOptions.CultureInvariant)]
    private static partial Regex AnyNumberPattern();

    [GeneratedRegex(@"[vV](?:er)?\s*\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPrefixPattern();

    [GeneratedRegex(@"\d+\s*(s|sec|secs|second|seconds|m|min|mins|minute|minutes|\uCD08|\uBD84)(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CountdownUnitPattern();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitSequencePattern();

    [GeneratedRegex(@"^stripper\s+update\s+\d{2}\.\d{2}\.\d{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StripperUpdateDotPattern();

    [GeneratedRegex(@"^stripper\s+update\s+\d{2}/\d{2}/\d{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StripperUpdateSlashPattern();

    [GeneratedRegex(@"^stripper\s+update\s+\d{4}-\d{2}-\d{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StripperUpdateIsoPattern();

    [GeneratedRegex(@"^stripper\s+update", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StripperUpdatePrefixPattern();

    [GeneratedRegex(@"^strippercs2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StripperCs2Pattern();

    [GeneratedRegex(@"^CONSOLE:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConsolePrefixPattern();

    [GeneratedRegex(@"^\[.*\]\s*stripper", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketStripperPattern();
}
