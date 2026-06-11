using System.Collections.Concurrent;
using ChatTranslatorHud.Utils;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Cs2MenuManager = CS2MenuManager.API.Class.MenuManager;

namespace ChatTranslatorHud.Services;

public sealed class HudDisplayService(
    PlayerPreferenceService preferenceService,
    PlayerTranslationService languageService,
    TranslationCache translationCache,
    int staticDurationSeconds,
    int countdownDisplayThresholdSeconds)
{
    private readonly ConcurrentQueue<ActiveHudMessage> _staticMessages = new();
    private readonly object _sync = new();
    private ActiveHudMessage? _currentCountdown;

    public void AddStaticMessage(string text, string originalText, DateTimeOffset now)
    {
        _staticMessages.Enqueue(ActiveHudMessage.Static(text, now.AddSeconds(staticDurationSeconds), originalText));
    }

    public void AddCountdown(
        ParseResult parseResult,
        string originalText,
        DateTimeOffset now,
        IReadOnlyDictionary<string, CountdownTextTemplate>? perLanguageTemplates)
    {
        lock (_sync)
        {
            _currentCountdown = ActiveHudMessage.Countdown(
                parseResult.Prefix,
                parseResult.Seconds,
                parseResult.Suffix,
                parseResult.IsMmss,
                parseResult.Unit,
                now,
                countdownDisplayThresholdSeconds,
                originalText,
                perLanguageTemplates);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            while (_staticMessages.TryDequeue(out _))
            {
            }

            _currentCountdown = null;
        }
    }

    public void Update(DateTimeOffset now)
    {
        var displayableMessages = GetDisplayableMessages(now);
        if (displayableMessages.Count == 0)
            return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.TryGetAuthorizedSteamId64(out var steamId64) || !preferenceService.IsHudEnabled(player))
                continue;

            if (Cs2MenuManager.GetActiveMenu(player) is not null)
                continue;

            var language = languageService.GetLanguage(steamId64);
            var lines = new List<string>(displayableMessages.Count);
            foreach (var message in displayableMessages)
            {
                var text = GetTextForPlayer(message, now, language);
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text);
            }

            if (lines.Count > 0)
                SendCenterTextMessage(player, string.Join("\n", lines));
        }
    }

    private static void SendCenterTextMessage(CCSPlayerController player, string message)
    {
        player.PrintToCenterAlert(message);
    }

    private List<ActiveHudMessage> GetDisplayableMessages(DateTimeOffset now)
    {
        var validMessages = new List<ActiveHudMessage>();
        var displayableMessages = new List<ActiveHudMessage>();

        lock (_sync)
        {
            while (_staticMessages.TryDequeue(out var message))
            {
                if (message.ShouldDisplay(now))
                {
                    validMessages.Add(message);
                    displayableMessages.Add(message);
                }
            }

            foreach (var message in validMessages)
                _staticMessages.Enqueue(message);

            if (_currentCountdown is not null && _currentCountdown.ShouldDisplay(now))
            {
                displayableMessages.Add(_currentCountdown);
            }
            else if (_currentCountdown is not null && now >= _currentCountdown.ExpiryTime)
            {
                _currentCountdown = null;
            }
        }

        return displayableMessages;
    }

    private string GetTextForPlayer(ActiveHudMessage message, DateTimeOffset now, string language)
    {
        if (message.Type == MessageType.Static &&
            !string.IsNullOrWhiteSpace(message.OriginalText) &&
            translationCache.TryGetTranslation(message.OriginalText, language, out var translatedText) &&
            !string.IsNullOrWhiteSpace(translatedText))
        {
            return translatedText;
        }

        return message.GetCurrentText(now, language);
    }
}
