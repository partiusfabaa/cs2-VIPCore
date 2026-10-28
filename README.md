![GitHub all releases](https://img.shields.io/github/downloads/partiusfabaa/cs2-VIPCore/total?style=social)

# cs2-VIPCore

#### If you find an error or anything else. Please message me in discord: thesamefabius

## Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp), [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [VIPCore](https://github.com/partiusfabaa/cs2-VIPCore/releases)
4. Unpack the archive and upload it to the game server **(example path: `addons/counterstrikesharp/plugins`)**

### Put the modules in this path `addons/counterstrikesharp/plugins`

## Commands 

| **Command**                             | **Description**                                               | **Flag** |
|-------------------------------------|-----------------------------------------------------------|----------------|
| **`css_vip`** or **`!vip`** | Opens the VIP menu | |
| **`css_vip_admin`** or **`!vip_admin`** | Opens the VIP admin menu: give or remove VIP, reload configs and players | `AdminMenuPermission` |
| **`css_vip_adduser <steamid or accountid> <vipgroup> <time or 0 permanently>`** | Adds a VIP player | **@css/root** |
| **`css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>`** | Updates the player's VIP | **@css/root** |
| **`css_vip_deleteuser <steamid or accountid>`** | Removes the player's VIP | **@css/root** |
| **`css_reload_vip_player <steamid>`** | Reloads the player's VIP from the database | **@css/root** |
| **`css_vip_reload`** or **`!vip_reload`** | Reloads the configuration | **@css/root** |
| **`css_vip_help`** or **`!vip_help`** | Lists all VIP admin commands | **@css/root** |

## Configs
All configs are in `addons/counterstrikesharp/configs/plugins/VIPCore`. Module configs are in `VIPCore/Modules`.

Configs from VIPCore 1.x are converted on the first start: groups are taken out of `Groups`/`Values`, `Delay` and `UseCenterHtmlMenu` move to `vip_core.json`. The old files are kept as `vip.json.old` and `vip_core.json.old`.

### vip_core.json

| Option | Default | Description |
|---|---|---|
| `Delay` | `2` | Delay in seconds after spawn before VIP features are applied |
| `TimeMode` | `0` | Unit for VIP time in commands: `0` seconds, `1` minutes, `2` hours, `3` days |
| `ServerId` | `0` | Server ID, VIPs are stored per server |
| `ServerIP` | `0.0.0.0` | Server IP |
| `ServerPort` | `27015` | Server port |
| `MenuType` | `center` | `center` - menu in the center of the screen, `chat` - in chat, `console` - in console |
| `ReOpenMenuAfterItemClick` | `false` | Reopen the VIP menu after selecting an item |
| `AdminMenuPermission` | `["@css/root"]` | Permissions needed for `css_vip_admin` |
| `VipLogging` | `true` | Log VIPCore actions |
| `Connection` | | MySQL connection |

```json
{
  "Delay": 2,
  "TimeMode": 1,
  "ServerId": 0,
  "ServerIP": "0.0.0.0",
  "ServerPort": 27015,
  "MenuType": "center",
  "ReOpenMenuAfterItemClick": false,
  "AdminMenuPermission": ["@css/root"],
  "VipLogging": true,
  "Connection": {
    "Host": "localhost",
    "Database": "database",
    "User": "user",
    "Password": "password",
    "Port": 3306
  }
}
```

### vip.json

VIP groups. The key is the group name, the value is the features of the group. Each module describes its value in its own README.

`$inherit` - takes all features of another group, the group's own features override them.

```json
{
  "VIP": {
    "Health": 120,
    "Money": "++1000",
    "Tag": { "ScoreboardTag": "VIP", "ChatTag": "[VIP]", "ColorTag": "lime" }
  },
  "PREMIUM": {
    "$inherit": "VIP",
    "Health": 150,
    "Money": "++3000",
    "Tag": ["PREMIUM_TAGS"]
  }
}
```

## Example Module
```csharp
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using VipCoreApi;
using VipCoreApi.Args;
using VipCoreApi.Enums;

namespace VIPMyModule;

public class VIPMyModule : BasePlugin
{
    public override string ModuleAuthor => "Author";
    public override string ModuleName => "[VIP] My Module";
    public override string ModuleVersion => "1.0.0";

    private MyFeature? _myFeature;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        var api = IVipCoreApi.Capability.Get();
        if (api == null) return;

        _myFeature = new MyFeature(this, api);
    }

    public override void Unload(bool hotReload)
    {
        _myFeature?.Dispose();
    }
}

public class MyFeature : VipFeature<int>
{
    private readonly TestConfig _config;

    public MyFeature(VIPMyModule plugin, IVipCoreApi api) : base("MyFeature", api)
    {
        plugin.AddCommand("css_viptestcommand", "", OnCmdVipCommand);
        _config = LoadConfig<TestConfig>("VIPMyModule");
    }

    public override void OnPlayerSpawn(CCSPlayerController player, bool vip)
    {
        if (!vip || !IsPlayerValid(player)) return;

        PrintToChat(player, $"VIP player - {player.PlayerName} has spawned, value: {GetValue(player)}");
    }

    public override void OnSelectItem(CCSPlayerController player, VipFeature feature)
    {
        PrintToChat(player, GetPlayerFeatureState(player) == FeatureState.Enabled ? "Enabled" : "Disabled");
    }

    public override void OnFeatureDisplay(FeatureDisplayArgs args)
    {
        if (args.State == FeatureState.Enabled)
            args.Display = $"[{GetValue(args.Controller)}]";
    }

    public override void OnPlayerRemoved(CCSPlayerController player, string group)
    {
        PrintToChat(player, $"You lost the {group} group");
    }

    public void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        PrintToChat(player, IsPlayerVip(player) && PlayerHasFeature(player)
            ? $"{player.PlayerName} is a VIP player"
            : $"{player.PlayerName} not a VIP player");
    }
}

public class TestConfig
{
    public int Test1 { get; set; } = 50;
    public bool Test2 { get; set; } = true;
    public string Test3 { get; set; } = "TEST";
    public float Test4 { get; set; } = 30.0f;
}
```
## License

This project is licensed under the MIT License. See the LICENSE file for details.
