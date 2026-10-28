using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using VipCoreApi.Enums;
using VipCoreApi.Args;

namespace VIP_Respawn;

public class VipRespawn : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Respawn";
    public override string ModuleVersion => "v2.0.0";

    private Respawn? _respawn;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _respawn = new Respawn(this, _api);
        };
    }
    
    public override void Unload(bool hotReload)
    {
        _respawn?.Dispose();
    }
}

public class Respawn : VipFeature<int>
{
    private readonly VipRespawn _vipRespawn;
    private readonly Dictionary<ulong, int> _usedRespawns = new();

    public Respawn(VipRespawn vipRespawn, IVipCoreApi api) : base("Respawn", api, FeatureType.Selectable)
    {
        _vipRespawn = vipRespawn;

        vipRespawn.RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            _usedRespawns.Clear();

            return HookResult.Continue;
        });

        vipRespawn.AddCommand("css_respawn", "", OnCmdVipCommand);
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
        if (!IsPlayerVip(player)) return;
        if (!PlayerHasFeature(player)) return;
        
        if (GetValue(player) <= _usedRespawns.GetValueOrDefault(player.SteamID))
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
        _usedRespawns[player.SteamID] = _usedRespawns.GetValueOrDefault(player.SteamID) + 1;
        PrintToChat(player, GetTranslatedText("respawn.Success"));
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        var limit = GetValue(args.Controller);
        var left = Math.Max(limit - _usedRespawns.GetValueOrDefault(args.Controller.SteamID), 0);

        args.Display = $"[{left}/{limit}]";
    }
}