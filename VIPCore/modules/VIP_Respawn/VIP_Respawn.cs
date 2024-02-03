using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using Modularity;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Respawn;

public class VipRespawn : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Respawn";
    public override string ModuleVersion => "1.0.1";

    public MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerControllerSetPawnFunc =
        new(GetSignature());

    private Respawn _respawn;
    private IVipCoreApi _api = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _respawn = new Respawn(this, _api);
        _api.RegisterFeature(_respawn, FeatureType.Selectable, _respawn.OnSelectItem);
    }

    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(_respawn);
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

    public void OnSelectItem(CCSPlayerController player, FeatureState state)
    {
        PlayerRespawn(player);
    }

    public void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
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

        _vipRespawn.CBasePlayerControllerSetPawnFunc.Invoke(player, playerPawn, true, false);
        VirtualFunction.CreateVoid<CCSPlayerController>(player.Handle,
            GameData.GetOffset("CCSPlayerController_Respawn"))(player);
        _usedRespawns[player.Index] ++;
        PrintToChat(player, GetTranslatedText("respawn.Success"));
    }
}