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
| **`css_vip_reload` or `!vip_reload`**    | Reloads the configuration | **@css/root** |
| **`css_vip_adduser <steamid or accountid> <vipgroup> <time or 0 permanently>`** | Adds a VIP player | **@css/root** |
| **`css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>`** | Updates the player's VIP | **@css/root** |
| **`css_vip_deleteuser <steamid or accountid>`** | Allows you to delete a player by SteamID identifier | **@css/root** |
| **`css_vip`** or **`!vip`** | Opens the VIP menu | 
| **`css_vip_help`** or **`!vip_help`** | Lists all VIP admin commands | **@css/root** |

## Configs
All configs are in `addons/counterstrikesharp/configs/plugins/VIPCore`. Module configs are in `VIPCore/Modules`.

### vip_core.json

| Option | Default | Description |
|---|---|---|
| `TimeMode` | `0` | Unit for VIP time in commands: `0` seconds, `1` minutes, `2` hours, `3` days |
| `ServerId` | `0` | Server ID, VIPs are stored per server |
| `UseCenterHtmlMenu` | `true` | `true` - menu in the center of the screen, `false` - in chat. |
| `ServerIP` | `0.0.0.0` | Server IP |
| `ServerPort` | `27015` | Server port |
| `ReOpenMenuAfterItemClick` | `false` | Reopen the VIP menu after selecting an item |
| `VipLogging` | `true` | Log VIPCore actions |
| `Connection` | | MySQL connection |

```json
{
  "TimeMode": 1,
  "ServerId": 0,
  "UseCenterHtmlMenu": true,
  "ServerIP": "0.0.0.0",
  "ServerPort": 27015,
  "ReOpenMenuAfterItemClick": false,
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

`Delay` - delay in seconds after spawn before VIP features are applied.

`Groups` - VIP groups. The key is the group name, `Values` are the features of the group. Each module describes its value in its own README.

```json
{
  "Delay": 2.0,
  "Groups": {
    "VIP": {
      "Values": {
        "Health": 120,
        "Money": "++1000",
        "Tag": { "ScoreboardTag": "VIP", "ChatTag": "[VIP]", "ColorTag": "lime" }
      }
    },
    "PREMIUM": {
      "Values": {
        "Health": 150,
        "Money": "++3000",
        "Tag": ["PREMIUM_TAGS"]
      }
    }
  }
}
```

## Example Module
```csharp
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using VipCoreApi;

namespace VIPMyModule;

public class VIPMyModule : BasePlugin
{
    public override string ModuleAuthor => "Author";
    public override string ModuleName => "[VIP] My Module";
    public override string ModuleVersion => "1.0.0";

    private MyPlugin _myPlugin;

    private IVipCoreApi? _api;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;

        _api.OnCoreReady += () =>
        {
            _myPlugin = new MyPlugin(this, _api);
            _api.RegisterFeature(_myPlugin);
        };
    }

    public override void Unload(bool hotReload)
    {
        _api?.UnRegisterFeature(_myPlugin);
    }
}

public class MyPlugin : VipFeatureBase
{
    public override string Feature => "MyFeature";
    private TestConfig _config;

    public MyPlugin(VIPMyModule vipMyModule, IVipCoreApi api) : base(api)
    {
        vipMyModule.AddCommand("css_viptestcommand", "", OnCmdVipCommand);
        _config = LoadConfig<TestConfig>("VIPMyModule");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (PlayerHasFeature(player))
        {
            PrintToChat(player, $"VIP player - {player.PlayerName} has spawned");
        }
    }

    public override void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        if (state == IVipCoreApi.FeatureState.Enabled)
        {
            PrintToChat(player, "Enabled");
        }
        else
        {
            PrintToChat(player, "Disabled");
        }
    }
    
    public void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (IsClientVip(player) && PlayerHasFeature(player))
        {
            PrintToChat(player, $"{player.PlayerName} is a VIP player");
            return;
        }

        PrintToChat(player, $"{player.PlayerName} not a VIP player");
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
