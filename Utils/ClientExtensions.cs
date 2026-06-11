using CounterStrikeSharp.API.Core;

namespace ChatTranslatorHud.Utils;

public static class PlayerExtensions
{
    public static bool IsRealPlayer(this CCSPlayerController? player)
    {
        return player is { IsValid: true, IsBot: false, IsHLTV: false };
    }

    public static bool TryGetAuthorizedSteamId64(this CCSPlayerController? player, out ulong steamId64)
    {
        steamId64 = 0;
        if (!player.IsRealPlayer())
            return false;

        var steamId = player!.AuthorizedSteamID;
        if (steamId is null || !steamId.IsValid())
            return false;

        steamId64 = steamId.SteamId64;
        return true;
    }
}
