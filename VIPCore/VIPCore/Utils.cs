using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;

namespace VIPCore;

public static class Utils
{
    public static bool IsValidEntity(CEntityInstance ent)
    {
        return ent.IsValid;
    }

    public static int GetAccountIdFromCommand(string steamId, out CCSPlayerController? player)
    {
        player = null;

        if (steamId.Contains("STEAM_1"))
        {
            steamId = ReplaceFirstCharacter(steamId);
        }

        if (steamId.Contains("STEAM_") || steamId.Contains("765611"))
        {
            player = GetPlayerFromSteamId(steamId);

            if (steamId.StartsWith("765611"))
            {
                var accId = new SteamID(ulong.Parse(steamId)).AccountId;

                if (player == null) return accId;
                var authorizedSteamId = player.AuthorizedSteamID;

                return authorizedSteamId == null ? accId : authorizedSteamId.AccountId;
            }
            else
            {
                var accId = new SteamID(steamId).AccountId;
                if (player == null) return accId;

                var authorizedSteamId = player.AuthorizedSteamID;
                return authorizedSteamId == null ? accId : authorizedSteamId.AccountId;
            }
        }

        return int.Parse(steamId);
    }

    public static CCSPlayerController? GetPlayerFromSteamId(string steamId)
    {
        return Utilities.GetPlayers().Find(u =>
            u.AuthorizedSteamID != null &&
            (u.AuthorizedSteamID.SteamId2.ToString().Equals(steamId) ||
            u.AuthorizedSteamID.SteamId64.ToString().Equals(steamId) ||
            u.AuthorizedSteamID.AccountId.ToString().Equals(steamId)));
    }

    public static string ReplaceFirstCharacter(string input)
    {
        if (input.Length <= 0) return input;

        var charArray = input.ToCharArray();
        charArray[6] = '0';

        return new string(charArray);
    }
}