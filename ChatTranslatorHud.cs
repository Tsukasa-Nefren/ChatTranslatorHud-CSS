using System.Reflection;
using ChatTranslatorHud.Listeners;
using ChatTranslatorHud.Services;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using PlayerSettings;
using CssListeners = CounterStrikeSharp.API.Core.Listeners;
using CsTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace ChatTranslatorHud;

[MinimumApiVersion(80)]
public sealed class ChatTranslatorHud : BasePlugin, IPluginConfig<ChatTranslatorConfig>
{
    private const string FallbackTargetLanguage = "EN";
    private const bool DefaultHudEnabled = true;
    private const bool DefaultOriginalMessageEnabled = true;
    private const int StaticHudDurationSeconds = 3;
    private const int CountdownDisplayThresholdSeconds = 5;
    private static readonly PluginCapability<ISettingsApi?> SettingsCapability = new("settings:nfcore");

    private HttpClient? _httpClient;
    private TranslationService? _translationService;
    private TranslationCache? _translationCache;
    private ClientConVarQueryService? _clientConVarQueryService;
    private PlayerTranslationService? _playerTranslationService;
    private PlayerPreferenceService? _playerPreferenceService;
    private HudDisplayService? _hudDisplayService;
    private RoundMessageContext? _roundMessageContext;
    private GameEventListener? _gameEventListener;
    private ClientLanguageListener? _clientLanguageListener;
    private CommandListener? _commandListener;
    private CsTimer? _hudTimer;

    public override string ModuleName => "Chat Translator HUD";
    public override string ModuleVersion => typeof(ChatTranslatorHud).Assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
    public override string ModuleAuthor => "Tsukasa";
    public override string ModuleDescription => "Translates console chat messages and displays HUD countdowns.";

    public ChatTranslatorConfig Config { get; set; } = new();

    public void OnConfigParsed(ChatTranslatorConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        InitializeServices();

        var gameEvents = _gameEventListener!;
        var clientLanguages = _clientLanguageListener!;
        var commands = _commandListener!;

        RegisterListener<CssListeners.OnMapStart>(mapName =>
        {
            gameEvents.OnMapStart(mapName);
            _hudTimer?.Kill();
            _hudTimer = AddTimer(0.1f, () => _hudDisplayService?.Update(DateTimeOffset.UtcNow), TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        });
        RegisterListener<CssListeners.OnMapEnd>(gameEvents.OnMapEnd);
        RegisterListener<CssListeners.OnClientAuthorized>(clientLanguages.OnClientAuthorized);
        RegisterListener<CssListeners.OnClientPutInServer>(clientLanguages.OnClientPutInServer);
        RegisterListener<CssListeners.OnClientDisconnect>(clientLanguages.OnClientDisconnect);
        RegisterEventHandler<EventRoundStart>(gameEvents.OnRoundStart);
        AddCommandListener("say", gameEvents.OnSayCommand, HookMode.Pre);
        AddCommand("css_thud", "Toggle ChatTranslatorHud HUD", commands.OnThudCommand);

        if (!string.IsNullOrWhiteSpace(Server.MapName))
            _translationCache?.SetCurrentMap(Server.MapName);

        if (hotReload)
            clientLanguages.RefreshConnectedPlayers();
    }

    public override void Unload(bool hotReload)
    {
        if (_gameEventListener is not null)
        {
            DeregisterEventHandler<EventRoundStart>(_gameEventListener.OnRoundStart);
            RemoveCommandListener("say", _gameEventListener.OnSayCommand, HookMode.Pre);
        }

        if (_commandListener is not null)
            RemoveCommand("css_thud", _commandListener.OnThudCommand);

        _gameEventListener?.Clear();
        _clientLanguageListener?.Clear();
        DisposeServices();
    }

    private void InitializeServices()
    {
        ClientConVarResponseReader.ResetForReload(ModuleDirectory);

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _translationService = new TranslationService(_httpClient, Config, Logger);
        _translationCache = new TranslationCache(Path.Combine(ModuleDirectory, "translation_cache"), Config.CacheTranslations);
        _clientConVarQueryService = new ClientConVarQueryService(this, Logger, Config.UseNativeClientConVarHook);
        _roundMessageContext = new RoundMessageContext();
        _playerTranslationService = new PlayerTranslationService(FallbackTargetLanguage);
        _playerPreferenceService = new PlayerPreferenceService(
            TryGetSettingsApi,
            DefaultHudEnabled,
            DefaultOriginalMessageEnabled);
        _hudDisplayService = new HudDisplayService(
            _playerPreferenceService,
            _playerTranslationService,
            _translationCache,
            StaticHudDurationSeconds,
            CountdownDisplayThresholdSeconds);
        _gameEventListener = new GameEventListener(
            Config,
            _translationService,
            _translationCache,
            _playerTranslationService,
            _playerPreferenceService,
            _hudDisplayService,
            _roundMessageContext,
            Logger);
        _clientLanguageListener = new ClientLanguageListener(this, _clientConVarQueryService, _playerTranslationService, _playerPreferenceService, Logger);
        _commandListener = new CommandListener(this, Localizer, _playerPreferenceService);
        _hudTimer = AddTimer(0.1f, () => _hudDisplayService?.Update(DateTimeOffset.UtcNow), TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    private static ISettingsApi? TryGetSettingsApi()
    {
        try
        {
            return SettingsCapability.Get();
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void DisposeServices()
    {
        _hudTimer?.Kill();
        _hudTimer = null;
        _hudDisplayService?.Clear();
        _clientConVarQueryService?.Clear();
        ClientConVarResponseReader.DisposeBridge();
        _translationCache?.Flush();
        _httpClient?.Dispose();
        _httpClient = null;
        _translationService = null;
        _translationCache = null;
        _roundMessageContext = null;
        _playerTranslationService = null;
        _clientConVarQueryService = null;
        _playerPreferenceService = null;
        _hudDisplayService = null;
        _gameEventListener = null;
        _clientLanguageListener = null;
        _commandListener = null;
    }
}
