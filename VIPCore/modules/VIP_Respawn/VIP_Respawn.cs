using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Respawn;

public class VipRespawn : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Respawn";
    public override string ModuleVersion => "1.0.2";

    private Respawn _respawn;
    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _respawn = new Respawn(this, _api);
            _api.RegisterFeature(_respawn, FeatureType.Selectable);
        };
    }
    
    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_respawn);
    }
}

public class Respawn : VipFeatureBase
{
    public override string Feature => "Respawn";

    private readonly VipRespawn _vipRespawn;
    private readonly int?[] _usedRespawns = new int?[65];

    public Respawn(VipRespawn vipRespawn, IVipCoreApi api) : base(api)
    {
        _vipRespawn = vipRespawn;
        
        vipRespawn.RegisterListener<Listeners.OnClientConnected>(slot => _usedRespawns[slot + 1] = 0);
        vipRespawn. RegisterListener<Listeners.OnClientDisconnectPost>(slot => _usedRespawns[slot + 1] = null);
        vipRespawn.RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            for (var i = 0; i < _usedRespawns.Length; i ++)
                _usedRespawns[i] = 0;

            return HookResult.Continue;
        });

        vipRespawn.AddCommand("css_respawn", "", OnCmdVipCommand);
    }

    public override void OnSelectItem(CCSPlayerController player, FeatureState state)
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
        if (!IsClientVip(player)) return;
        if (!PlayerHasFeature(player)) return;
        
        if (GetFeatureValue<int>(player) <= _usedRespawns[player.Index])
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
        _usedRespawns[player.Index] ++;
        PrintToChat(player, GetTranslatedText("respawn.Success"));
    }
}