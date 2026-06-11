using System.Text.RegularExpressions;

namespace ChatTranslatorHud.Services;

public sealed partial class RoundMessageContext(int maxMessages = 30, int contextMessageCount = 10)
{
    private readonly List<string> _messages = [];
    private readonly object _sync = new();

    public void Push(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        lock (_sync)
        {
            _messages.Add(message);
            if (_messages.Count > maxMessages)
                _messages.RemoveAt(0);
        }
    }

    public string? GetContext()
    {
        lock (_sync)
        {
            var messages = _messages
                .Where(message => !DateLikePattern().IsMatch(message))
                .TakeLast(contextMessageCount)
                .ToArray();

            return messages.Length == 0 ? null : string.Join("\n", messages);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _messages.Clear();
        }
    }

    [GeneratedRegex(@"(?<![\d.])\d{2}\.\d{2}\.\d{2}(?![\d.])", RegexOptions.CultureInvariant)]
    private static partial Regex DateLikePattern();
}
