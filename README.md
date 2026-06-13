# ChatTranslatorHud

CounterStrikeSharp plugin for Counter-Strike 2 servers. It intercepts server console `say` messages, translates them with DeepL, prints per-player chat translations, and displays static messages or countdowns in center HUD.

> **Note**: This plugin integrates and replaces the legacy [translate-css](https://github.com/Tsukasa-Nefren/translate-css) and [consolechatmanager](https://github.com/Tsukasa-Nefren/consolechatmanager) plugins.

## Features

- DeepL translation for server console messages
- Per-player target language from Steam `cl_language`
- Per-map, per-language translation cache
- HUD countdown detection for common ZE-style console messages
- CS2MenuManager WASD menu for HUD and original-message visibility
- Localized settings menu without chat-number selection
- CSSSharp-native config and lifecycle

## Requirements

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [CS2MenuManager](https://github.com/schwarper/CS2MenuManager)
- [PlayerSettingsCS2 (PlayerSettingsApi)](https://github.com/NickFox007/PlayerSettingsCS2)
- .NET 10 runtime bundled with current CounterStrikeSharp builds
- DeepL API key

## Installation

1. Download the latest release archive.
2. Extract it into the CS2 server root.
3. Start the server once to generate the CSSSharp config.
4. Edit the generated config and set `DeepLApiKey`.

```text
addons/
  counterstrikesharp/
    plugins/
      ChatTranslatorHud/
        ChatTranslatorHud.dll
        ChatTranslatorHud.deps.json
        ChatTranslatorHud.Native.dll
        ChatTranslatorHud.Native.so
        lang/
```

## Configuration

CSSSharp generates the config under:

```text
addons/counterstrikesharp/configs/plugins/ChatTranslatorHud/ChatTranslatorHud.json
```

```json
{
  "ConfigVersion": 1,
  "DeepLApiKey": "YOUR_API_KEY_HERE",
  "EnableTranslation": true,
  "CacheTranslations": true,
  "DeepLApiUrl": "https://api-free.deepl.com/v2/translate",
  "UseRoundContext": true,
  "UseDateContext": true,
  "UseNativeClientConVarHook": true
}
```

## Commands

| Command | Description |
|---|---|
| `css_thud` | Open the ChatTranslatorHud CS2MenuManager WASD settings menu |

## Notes

- This is the CounterStrikeSharp port of the previous ModSharp ChatTranslatorHud.
- `UseNativeClientConVarHook` enables the native vtable hook used to receive `cl_language` query responses. Set it to `false` to fall back to CSSSharp's UserMessage hook path.
- Player preferences are stored through PlayerSettingsApi.
- Translation cache files are stored in `translation_cache` inside the plugin directory.

## License

MIT
