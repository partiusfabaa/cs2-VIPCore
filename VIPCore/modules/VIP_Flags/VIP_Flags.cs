using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace VIP_Flags;

public class VipFlags : BasePlugin
{
	public override string ModuleAuthor => "thesamefabius";
	public override string ModuleName => "[VIP] Flags";
	public override string ModuleVersion => "v1.0.0";

	private IVipCoreApi? _api;
	private Flags _flags;

	private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

	public override void OnAllPluginsLoaded(bool hotReload)
	{
		_api = PluginCapability.Get();

		if (_api == null) return;

		_flags = new Flags(this, _api);
		_api.RegisterFeature(_flags, FeatureType.Hide);
	}

	public override void Unload(bool hotReload)
	{
		_api?.UnRegisterFeature(_flags);
	}
}

public class Flags : VipFeatureBase
{
	private readonly VipFlags _vipFlags;
	public override string Feature => "flags";
	private readonly Dictionary<ulong, List<string>> _flags = new();
	private bool[] _clientDisconnected = new bool[70];

	public Flags(VipFlags vipFlags, IVipCoreApi api) : base(api)
	{
		_vipFlags = vipFlags;
		vipFlags.RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
		{
			var player = @event.Userid;

			if (player is null || !player.IsValid || player.IsBot || _clientDisconnected[player.Slot]) return HookResult.Continue;

			if (PlayerHasFeature(player))
			{
				_clientDisconnected[player.Slot] = true;
				RemovePlayerPermissions(player);
			}

			return HookResult.Continue;
		});
	}

	public override void OnPlayerLoaded(CCSPlayerController player, string group)
	{
		_vipFlags.AddTimer(1.5f, () =>
		{
			if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected) return;

			if (!IsClientVip(player) || !PlayerHasFeature(player))
				return;

			if (!_flags.ContainsKey(player.SteamID))
				_flags.Add(player.SteamID, []);

			var flagsOrGroups = GetFeatureValue<List<string>>(player);
			if (flagsOrGroups == null || flagsOrGroups.Count == 0) return;

			var steamId = new SteamID(player.SteamID);
			foreach (var flagOrGroup in flagsOrGroups)
			{
				if (flagOrGroup.StartsWith('@'))
					AdminManager.AddPlayerPermissions(steamId, flagOrGroup);
				else
					AdminManager.AddPlayerToGroup(steamId, flagOrGroup);

				_flags[player.SteamID].Add(flagOrGroup);
			}

			_clientDisconnected[player.Slot] = false;
		});
	}

	/*
	public override void OnPlayerRemoved(CCSPlayerController player, string group)
	{
		RemovePlayerPermissions(player);
	}
	*/

	private void RemovePlayerPermissions(CCSPlayerController player)
	{
		if (!_flags.TryGetValue(player.SteamID, out var value)) return;

		var playerFlagsOrGroups = new List<string>(value);

		var steamId = new SteamID(player.SteamID);
		foreach (var flagOrGroups in playerFlagsOrGroups)
		{
			if (flagOrGroups.StartsWith('@'))
				AdminManager.RemovePlayerPermissions(steamId, flagOrGroups);
			else
				AdminManager.RemovePlayerFromGroup(steamId, groups: flagOrGroups);
			value.Remove(flagOrGroups);
		}
	}
}

