using CounterStrikeSharp.API.Modules.Commands;

namespace ChatTranslatorHud.Utils;

public sealed class ConsoleSayMessageGate
{
    private const double DedupWindowSeconds = 0.1;
    private const int DedupCleanupThreshold = 64;
    private readonly Dictionary<string, DateTime> _recentMessages = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public ConsoleSayDecision ShouldHandle(
        bool translationEnabled,
        bool hasPlayer,
        CommandCallingContext callingContext,
        string message,
        DateTime now)
    {
        if (!translationEnabled ||
            hasPlayer ||
            callingContext != CommandCallingContext.Console ||
            string.IsNullOrWhiteSpace(message) ||
            message.StartsWith("[Translated]", StringComparison.OrdinalIgnoreCase))
        {
            return new ConsoleSayDecision(false, false);
        }

        lock (_sync)
        {
            if (_recentMessages.TryGetValue(message, out var lastTime) &&
                (now - lastTime).TotalSeconds < DedupWindowSeconds)
            {
                return new ConsoleSayDecision(true, true);
            }

            _recentMessages[message] = now;
            if (_recentMessages.Count > DedupCleanupThreshold)
            {
                var cutoff = now.AddSeconds(-10);
                foreach (var key in _recentMessages.Where(pair => pair.Value < cutoff).Select(pair => pair.Key).ToArray())
                    _recentMessages.Remove(key);
            }
        }

        return new ConsoleSayDecision(true, false);
    }

    public void Clear()
    {
        lock (_sync)
            _recentMessages.Clear();
    }
}

public readonly record struct ConsoleSayDecision(bool Handle, bool Duplicate);
