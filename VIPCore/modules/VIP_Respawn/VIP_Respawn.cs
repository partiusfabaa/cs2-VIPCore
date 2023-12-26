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

namespace VIP_Respawn;

public class VipRespawn : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "[VIP] Respawn";
    public override string ModuleVersion => "1.0.1";

    private MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;

    private static readonly string Feature = "Respawn";
    private IVipCoreApi _api = null!;
    private readonly int?[] _usedRespawns = new int?[65];

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientConnected>(slot => _usedRespawns[slot + 1] = 0);
        RegisterListener<Listeners.OnClientDisconnectPost>(slot => _usedRespawns[slot + 1] = null);
        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            for (var i = 0; i < _usedRespawns.Length; i ++)
                _usedRespawns[i] = 0;

            return HookResult.Continue;
        });
        CBasePlayerController_SetPawnFunc =
            new MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool>(GetSignature());
    }

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, IVipCoreApi.FeatureType.Selectable, OnSelectItem);
    }

    private void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        Respawn(player);
    }

    [ConsoleCommand("css_respawn")]
    public void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;
        Respawn(player);
    }

    private void Respawn(CCSPlayerController player)
    {
        if (!_api.IsClientVip(player)) return;
        if (!_api.PlayerHasFeature(player, Feature)) return;

        if (_api.GetFeatureValue<int>(player, Feature) <= _usedRespawns[player.Index])
        {
            _api.PrintToChat(player, _api.GetTranslatedText("respawn.Limit"));
            return;
        }

        if (player.TeamNum is (int)CsTeam.None or (int)CsTeam.Spectator)
        {
            _api.PrintToChat(player, _api.GetTranslatedText("respawn.InCommand"));
            return;
        }

        if (player.PawnIsAlive)
        {
            _api.PrintToChat(player, _api.GetTranslatedText("respawn.IsAlive"));
            return;
        }

        var playerPawn = player.PlayerPawn.Value;

        if (playerPawn == null) return;

        CBasePlayerController_SetPawnFunc.Invoke(player, playerPawn, true, false);
        VirtualFunction.CreateVoid<CCSPlayerController>(player.Handle,
            GameData.GetOffset("CCSPlayerController_Respawn"))(player);
        _usedRespawns[player.Index] ++;
        _api.PrintToChat(player, _api.GetTranslatedText("respawn.Success"));
    }

    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
    }
}