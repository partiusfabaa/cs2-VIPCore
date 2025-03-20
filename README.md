![GitHub all releases](https://img.shields.io/github/downloads/partiusfabaa/cs2-VIPCore/total?style=social&label=Downloads)
![GitHub stars](https://img.shields.io/github/stars/partiusfabaa/cs2-VIPCore?style=social)
![NuGet Version](https://img.shields.io/nuget/vpre/VipCoreApi)

<div align="center">
  <strong>VIPCore</strong>
</div>

#

> [!NOTE]
> If you find an error or anything else. Please message me in discord: [thesamefabius](https://discord.com/users/658204951595712519)

## Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp), [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [VIPCore](https://github.com/partiusfabaa/cs2-VIPCore/releases)
4. Unpack the archive and upload it to the game server

### Put the modules in this path `addons/counterstrikesharp/plugins`

## Commands 

| **Command** | **Description** | **FLAG** |
|-|-|-|
| **`css_vip`** or **`!vip`** | Opens the VIP menu | **-**  |
| **`css_vip_reload` or `!vip_reload`**    | Reloads the configuration | **@css/root**  |
| **`css_vip_adduser <steamid or accountid> <vipgroup> <time or 0 permanently>`** | Adds a VIP player | **@css/root**  |
| **`css_vip_deleteuser <steamid or accountid>`** | Allows you to delete a player by SteamID identifier | **@css/root**  |
| **`css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>`** | Updates the player's VIP | **@css/root**  |

## Configs
Located in the folder `addons/counterstrikesharp/configs/plugins/VIPCore`

### vip_core.json
```json
{
  "TimeMode": 0,		   // 0 - seconds | 1 - minutes | 2 - hours | 3 - days)
  "ServerId": 0,		   // SERVER ID
  "MenuType": "html",
  "ReOpenMenuAfterItemClick": true,//Whether to reopen the menu after selecting an item | true - yes | false - no
  "VipLogging": true,	   	   //Whether to log VIPCore | true - yes | false - no
  "Connection": {
	"Host": 	"host",
	"Database": "database",
	"User": 	"user",
	"Password": "password",
	"Port": 3306
  }
}
```
### vip.json
```json
{
    "VIP1": {
        "Health": 110,
        "SmokeColor": [255, 0, 0]
    }
    "VIP2": {
        "$inherit": "VIP1",
        "SmokeColor": [0, 255, 255]
    }
}
```

## Example Module
```csharp
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using VipCoreApi;
using VipCoreApi.Enums;

namespace VIP_ExampleModule;

public class Plugin : BasePlugin
{
    public override string ModuleAuthor => "Author";
    public override string ModuleName => "[VIP] Example Module";
    public override string ModuleVersion => "1.0.0";

    private MyPlugin? _myPlugin;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _myPlugin = new MyPlugin(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _myPlugin?.Dispose();
    }
}

public class MyPlugin : VipFeature<int>
{
    private TestConfig _config;

    public MyPlugin(Plugin plugin, IVipCoreApi api) : base("MyFeature", api)
    {
        plugin.AddCommand("css_viptestcommand", "", OnCmdVipCommand);
        _config = LoadConfig<TestConfig>("vip_examplemodule");
    }

    private void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (IsPlayerValid(player))
        {
            PrintToChat(player, $"{player.PlayerName} is a VIP player");
            return;
        }

        PrintToChat(player, $"{player.PlayerName} not a VIP player");
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !IsPlayerValid(player) || !_config.Enabled) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn is null)
            return;

        var health = GetValue(player) * _config.Multiplier;

        playerPawn.Health = health;
        playerPawn.MaxHealth = health;

        Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        if (feature.State == FeatureState.Enabled)
        {
            PrintToChat(player, "Enabled");
        }
        else
        {
            PrintToChat(player, "Disabled");
        }
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
        {
            args.Display = $"[+{GetValue(args.Controller)}]";
        }
    }
}

public class TestConfig
{
    public int Multiplier { get; set; } = 2;
    public bool Enabled { get; set; } = true;
}
```
