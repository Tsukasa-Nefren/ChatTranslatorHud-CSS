# Changelog

## [0.1.0-beta]

### Changed

- Ported the plugin from ModSharp to CSSSharp.
- Replaced ModSharp MenuManager and ClientPreferences with CS2MenuManager WASD menu support and PlayerSettingsCS2-backed preferences.
- Replaced ModSharp HUD printing with direct CSSSharp `TextMsg` user messages to preserve the same center HUD destination.
- Ported ChatTranslatorHud menu localization to CSSSharp `lang/*.json` files.
- Updated project target to .NET 10 and `CounterStrikeSharp.API` 1.0.369.
- Refreshes connected player language mappings during CSSSharp hot reload.

### Added

- `css_thud` command for the per-player CS2MenuManager WASD settings menu.
- Unit tests for message parsing, HUD countdown formatting, config defaults, player language mapping, player preferences, translation cache, and DeepL response handling.

### Removed

- Removed ModSharp, Ptr.Shared, MenuManager, LocalizerManager, and ClientPreferences dependencies.
