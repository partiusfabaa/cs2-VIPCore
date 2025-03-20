using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_Respawn;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Respawn";
    public override string ModuleVersion => "v2.0.0";

    private Respawn? _respawn;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _respawn = new Respawn(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _respawn?.Dispose();
    }
}

public class Respawn : VipFeature<int>
{
    private readonly int?[] _usedRespawns = new int?[65];

    public Respawn(Plugin plugin, IVipCoreApi api) : base("Respawn", api, FeatureType.Selectable)
    {
        plugin.RegisterListener<Listeners.OnClientConnected>(slot => _usedRespawns[slot + 1] = 0);
        plugin.RegisterListener<Listeners.OnClientDisconnectPost>(slot => _usedRespawns[slot + 1] = null);
        plugin.RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            for (var i = 0; i < _usedRespawns.Length; i++)
                _usedRespawns[i] = 0;

            return HookResult.Continue;
        });

        plugin.AddCommand("css_respawn", "", OnCmdVipCommand);
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        PlayerRespawn(player);
    }

    private void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;
        PlayerRespawn(player);
    }

    private void PlayerRespawn(CCSPlayerController player)
    {
        if (!IsPlayerValid(player)) return;

        if (GetValue(player) <= _usedRespawns[player.Index])
        {
            PrintToChat(player, GetTranslatedText("respawn.Limit"));
            return;
        }

        if (player.TeamNum is (int)CsTeam.None or (int)CsTeam.Spectator)
        {
            PrintToChat(player, GetTranslatedText("respawn.InTeam"));
            return;
        }

        if (player.PawnIsAlive)
        {
            PrintToChat(player, GetTranslatedText("respawn.IsAlive"));
            return;
        }

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        VirtualFunctions.CBasePlayerController_SetPawnFunc.Invoke(player, playerPawn, true, false);
        VirtualFunction.CreateVoid<CCSPlayerController>(player.Handle, GameData.GetOffset("CCSPlayerController_Respawn"))(player);
        _usedRespawns[player.Index]++;
        PrintToChat(player, GetTranslatedText("respawn.Success"));
    }
}