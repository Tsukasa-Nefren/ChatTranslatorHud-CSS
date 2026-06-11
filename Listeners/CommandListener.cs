using ChatTranslatorHud.Services;
using ChatTranslatorHud.Utils;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;
using Microsoft.Extensions.Localization;

namespace ChatTranslatorHud.Listeners;

internal sealed class CommandListener(BasePlugin plugin, IStringLocalizer localizer, PlayerPreferenceService preferenceService)
{
    private const string KeyMenuTitle = "chattranslatorhud.menu.title";
    private const string KeyHudDisplay = "chattranslatorhud.menu.hud_display";
    private const string KeyOriginalMessage = "chattranslatorhud.menu.original_message";
    private const string KeyOn = "chattranslatorhud.common.on";
    private const string KeyOff = "chattranslatorhud.common.off";
    private const string KeyHudEnabled = "chattranslatorhud.chat.hud_enabled";
    private const string KeyHudDisabled = "chattranslatorhud.chat.hud_disabled";
    private const string KeyOriginalEnabled = "chattranslatorhud.chat.original_enabled";
    private const string KeyOriginalDisabled = "chattranslatorhud.chat.original_disabled";

    public void OnThudCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player is null || !player.TryGetAuthorizedSteamId64(out _))
        {
            commandInfo.ReplyToCommand("This command can only be used by an authorized player.");
            return;
        }

        CreateSettingsMenu(player).Display(player, 0);
    }

    private WasdMenu CreateSettingsMenu(CCSPlayerController player)
    {
        var menu = new WasdMenu(Localize(player, KeyMenuTitle), plugin);
        menu.AddItem(
            GetHudDisplayText(player),
            (selectedPlayer, option) =>
            {
                if (!selectedPlayer.TryGetAuthorizedSteamId64(out _))
                    return;

                var enabled = preferenceService.ToggleHud(selectedPlayer);
                option.Text = GetHudDisplayText(selectedPlayer);
                option.PostSelectAction = PostSelectAction.Reset;
                selectedPlayer.PrintToChat(enabled
                    ? $"{ChatColors.Green}[ChatTranslatorHud]{ChatColors.White} {Localize(selectedPlayer, KeyHudEnabled)}"
                    : $"{ChatColors.Red}[ChatTranslatorHud]{ChatColors.White} {Localize(selectedPlayer, KeyHudDisabled)}");
            });
        menu.AddItem(
            GetOriginalMessageText(player),
            (selectedPlayer, option) =>
            {
                if (!selectedPlayer.TryGetAuthorizedSteamId64(out _))
                    return;

                var enabled = preferenceService.ToggleOriginalMessage(selectedPlayer);
                option.Text = GetOriginalMessageText(selectedPlayer);
                option.PostSelectAction = PostSelectAction.Reset;
                selectedPlayer.PrintToChat(enabled
                    ? $"{ChatColors.Green}[ChatTranslatorHud]{ChatColors.White} {Localize(selectedPlayer, KeyOriginalEnabled)}"
                    : $"{ChatColors.Red}[ChatTranslatorHud]{ChatColors.White} {Localize(selectedPlayer, KeyOriginalDisabled)}");
            });
        return menu;
    }

    private string GetHudDisplayText(CCSPlayerController player)
    {
        return $"{Localize(player, KeyHudDisplay)}: {GetStatusText(player, preferenceService.IsHudEnabled(player))}";
    }

    private string GetOriginalMessageText(CCSPlayerController player)
    {
        return $"{Localize(player, KeyOriginalMessage)}: {GetStatusText(player, preferenceService.IsOriginalMessageEnabled(player))}";
    }

    private string GetStatusText(CCSPlayerController player, bool enabled)
    {
        return Localize(player, enabled ? KeyOn : KeyOff);
    }

    private string Localize(CCSPlayerController player, string key)
    {
        return localizer.ForPlayer(player, key);
    }
}
