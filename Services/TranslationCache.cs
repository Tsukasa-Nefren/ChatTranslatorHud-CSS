using System.Text.Json;

namespace ChatTranslatorHud.Services;

public sealed class TranslationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _cacheDirectory;
    private readonly bool _enabled;
    private readonly object _sync = new();
    private readonly object _saveScheduleSync = new();
    private readonly SemaphoreSlim _saveWriter = new(1, 1);
    private volatile bool _dirty;
    private long _lastSaveScheduledAtTicks;
    private Dictionary<string, Dictionary<string, string>> _memoryCache = [];
    private string _currentMapName = "";

    public string CurrentMapName
    {
        get
        {
            lock (_sync)
                return _currentMapName;
        }
    }

    public TranslationCache(string cacheDirectory, bool enabled)
    {
        _cacheDirectory = cacheDirectory;
        _enabled = enabled;
        if (_enabled)
        {
            Directory.CreateDirectory(_cacheDirectory);
            CleanupStaleTempFiles();
        }
    }

    public void SetCurrentMap(string mapName)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(mapName))
            return;

        lock (_sync)
        {
            if (string.Equals(_currentMapName, mapName, StringComparison.Ordinal))
                return;

            FlushCore();
            _memoryCache = [];
            _currentMapName = mapName;
            LoadCore(mapName);
        }
    }

    public bool TryGetTranslation(string originalText, string targetLanguage, out string? translatedText)
    {
        translatedText = null;
        if (!_enabled || string.IsNullOrWhiteSpace(originalText) || string.IsNullOrWhiteSpace(targetLanguage))
            return false;

        lock (_sync)
        {
            return _memoryCache.TryGetValue(originalText, out var languageCache)
                && languageCache.TryGetValue(NormalizeLanguage(targetLanguage), out translatedText);
        }
    }

    public void SaveTranslation(string originalText, string targetLanguage, string translatedText, string? expectedMapName = null)
    {
        if (!_enabled ||
            string.IsNullOrWhiteSpace(_currentMapName) ||
            string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(targetLanguage) ||
            string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        var saved = false;
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(expectedMapName) &&
                !string.Equals(_currentMapName, expectedMapName, StringComparison.Ordinal))
            {
                return;
            }

            if (!_memoryCache.TryGetValue(originalText, out var languageCache))
            {
                languageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _memoryCache[originalText] = languageCache;
            }

            languageCache[NormalizeLanguage(targetLanguage)] = translatedText;
            saved = true;
        }

        if (saved)
            ScheduleSave();
    }

    public void Flush()
    {
        if (!_enabled)
            return;

        lock (_sync)
        {
            FlushCore();
            _dirty = false;
        }
    }

    private void LoadCore(string mapName)
    {
        var cacheFile = GetCacheFilePath(mapName);
        if (!File.Exists(cacheFile))
            return;

        try
        {
            var json = File.ReadAllText(cacheFile);
            try
            {
                _memoryCache = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, JsonOptions) ?? [];
                return;
            }
            catch (JsonException)
            {
            }

            var oldCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (oldCache is null)
                return;

            _memoryCache = oldCache
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToDictionary(
                    x => x.Key,
                    x => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["EN"] = x.Value },
                    StringComparer.Ordinal);
            FlushCore();
        }
        catch
        {
            _memoryCache = [];
        }
    }

    private void FlushCore()
    {
        if (string.IsNullOrWhiteSpace(_currentMapName))
            return;

        var cache = BuildCacheForSave();
        if (cache.Count == 0)
            return;

        Directory.CreateDirectory(_cacheDirectory);
        var json = JsonSerializer.Serialize(cache, JsonOptions);
        var cacheFile = GetCacheFilePath(_currentMapName);
        var tempFile = cacheFile + ".tmp";
        File.WriteAllText(tempFile, json);
        File.Move(tempFile, cacheFile, overwrite: true);
    }

    private void ScheduleSave()
    {
        if (!_enabled)
            return;

        _dirty = true;

        var nowTicks = DateTimeOffset.UtcNow.Ticks;
        lock (_saveScheduleSync)
        {
            if (nowTicks - _lastSaveScheduledAtTicks < TimeSpan.TicksPerSecond)
                return;

            _lastSaveScheduledAtTicks = nowTicks;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await WriteIfDirtyAsync();
        });
    }

    private async Task WriteIfDirtyAsync()
    {
        if (!_dirty || !await _saveWriter.WaitAsync(0))
            return;

        try
        {
            string mapName;
            Dictionary<string, Dictionary<string, string>> cache;
            lock (_sync)
            {
                if (!_dirty || string.IsNullOrWhiteSpace(_currentMapName))
                    return;

                _dirty = false;
                mapName = _currentMapName;
                cache = BuildCacheForSave();
            }

            if (cache.Count == 0)
                return;

            Directory.CreateDirectory(_cacheDirectory);
            var json = JsonSerializer.Serialize(cache, JsonOptions);
            var cacheFile = GetCacheFilePath(mapName);
            var tempFile = cacheFile + ".tmp";
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, cacheFile, overwrite: true);
        }
        catch
        {
            _dirty = true;
        }
        finally
        {
            _saveWriter.Release();
        }
    }

    private Dictionary<string, Dictionary<string, string>> BuildCacheForSave()
    {
        var cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (originalText, languageCache) in _memoryCache)
        {
            var validTranslations = languageCache
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToDictionary(x => NormalizeLanguage(x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);

            if (validTranslations.Count > 0)
                cache[originalText] = validTranslations;
        }

        return cache;
    }

    private string GetCacheFilePath(string mapName)
    {
        var safeMapName = string.Join("_", mapName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(_cacheDirectory, $"{safeMapName}.json");
    }

    private static string NormalizeLanguage(string language)
    {
        return language.ToUpperInvariant();
    }

    private void CleanupStaleTempFiles()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDirectory, "*.tmp"))
                File.Delete(file);
        }
        catch
        {
        }
    }
}
