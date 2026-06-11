using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ChatTranslatorHud;
using Microsoft.Extensions.Logging;

namespace ChatTranslatorHud.Services;

public sealed partial class TranslationService(HttpClient httpClient, ChatTranslatorConfig config, ILogger logger)
{
    private const string DateContext = "Dates in this text use yyyy-mm-dd ISO 8601 format.";
    private const int MaxRetries = 3;
    private const int FailureThresholdForOpen = 5;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromMinutes(1);
    private long _circuitOpenUntilTicks;
    private int _consecutiveFailures;

    public async Task<string?> TranslateAsync(
        string text,
        string targetLanguage,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(targetLanguage) ||
            string.IsNullOrWhiteSpace(config.DeepLApiKey))
        {
            return null;
        }

        targetLanguage = targetLanguage.ToUpperInvariant();

        if (Interlocked.Read(ref _circuitOpenUntilTicks) > DateTimeOffset.UtcNow.Ticks)
            return null;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var textToSend = config.UseDateContext ? NormalizeDateText(text) : text;
                var requestBody = new Dictionary<string, object>
                {
                    ["text"] = new[] { textToSend },
                    ["target_lang"] = targetLanguage
                };

                var requestContext = context;
                if (config.UseDateContext && textToSend != text && string.IsNullOrWhiteSpace(requestContext))
                    requestContext = DateContext;

                if (!string.IsNullOrWhiteSpace(requestContext))
                    requestBody["context"] = requestContext;

                using var request = new HttpRequestMessage(HttpMethod.Post, config.DeepLApiUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", config.DeepLApiKey);

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    if ((status == 429 || status == 503) && attempt < MaxRetries - 1)
                    {
                        await Task.Delay(500 * (int)Math.Pow(2, attempt), cancellationToken);
                        continue;
                    }

                    logger.LogWarning("DeepL API request failed: {StatusCode}", response.StatusCode);
                    RecordFailure();
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<DeepLResponse>(responseContent);
                var translation = result?.Translations?.FirstOrDefault();
                Interlocked.Exchange(ref _consecutiveFailures, 0);

                if (translation?.Text is null)
                    return null;

                if (string.Equals(translation.DetectedSourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
                    return null;

                return string.IsNullOrWhiteSpace(translation.Text) ? null : translation.Text;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < MaxRetries - 1)
            {
                logger.LogWarning(ex, "DeepL request timed out, retrying");
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error occurred during translation: {Text}", text);
                RecordFailure();
                return null;
            }
        }

        RecordFailure();
        return null;
    }

    private void RecordFailure()
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= FailureThresholdForOpen)
        {
            Interlocked.Exchange(ref _circuitOpenUntilTicks, DateTimeOffset.UtcNow.Add(CircuitOpenDuration).Ticks);
            logger.LogWarning("DeepL circuit opened after {Failures} consecutive failures", failures);
        }
    }

    private static string NormalizeDateText(string text)
    {
        return YyMmDdDatePattern().Replace(text, match =>
            $"20{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}");
    }

    private sealed class DeepLResponse
    {
        [JsonPropertyName("translations")]
        public DeepLTranslation[]? Translations { get; set; }
    }

    private sealed class DeepLTranslation
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("detected_source_language")]
        public string? DetectedSourceLanguage { get; set; }
    }

    [GeneratedRegex(@"(?<![\d.])(\d{2})\.(\d{2})\.(\d{2})(?![\d.])", RegexOptions.CultureInvariant)]
    private static partial Regex YyMmDdDatePattern();
}
