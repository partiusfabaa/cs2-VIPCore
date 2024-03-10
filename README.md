# cs2-VIPCore

#### If you find an error or anything else. Please message me in discord: thesamefabius

<br>
<p align="center">
<a href="https://www.buymeacoffee.com/thesamefabius"><img src="https://img.buymeacoffee.com/button-api/?text=Support my work&emoji=🐱&slug=thesamefabius&button_colour=febee6&font_colour=000000&font_family=Inter&outline_colour=000000&coffee_colour=FFDD00" /></a>
</p>

## Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp), [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
3. Download [VIPCore](https://github.com/partiusfabaa/cs2-VIPCore/releases)
4. Unpack the archive and upload it to the game server **(example path: `addons/counterstrikesharp/plugins`)**

### Put the modules in this path `addons/counterstrikesharp/plugins`

## Commands 

| **Command**                             | **Description**                                               |
|-------------------------------------|-----------------------------------------------------------|
| **`css_vip_reload` or `!vip_reload`**    | Reloads the configuration **(`@css/root`)** |
| **`css_vip_adduser <steamid or accountid> <vipgroup> <time or 0 permanently>`** | Adds a VIP player **(for server console only)** |
| **`css_vip_updateuser <steamid or accountid> <group or -s> <time or -s>`** | Updates the player's VIP **(for server console only)** |
| **`css_vip_deleteuser <steamid or accountid>`** | Allows you to delete a player by SteamID identifier **(for server console only)** |
| **`css_vip`** or **`!vip`** | Opens the VIP menu |

## Configs
Located in the folder `addons/counterstrikesharp/configs/plugins/VIPCore`

### Core.json
```json
{
	"TimeMode": 0,		   // 0 - seconds | 1 - minutes | 2 - hours | 3 - days)
	"ServerId": 0,		   // SERVER ID
	"VipLogging": true,	   //Whether to log VIPCore | true - yes | false - no
	"UseCenterHtmlMenu": true, //If `true`, the menu will be in the center, if `false`, it will be in the chat. Note that if you have another plugin that uses `CenterHtml`, server crashes may occur
	"Connection": {
		"Host": 	"host",
		"Database": "database",
		"User": 	"user",
		"Password": "password"
	}
}
```
### vip.json
```json
{
	"Delay": 2.0
	"Groups": {
		"VIP1": {
			"Values": {
				"features": values
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
        _api.UnRegisterFeature(_myPlugin);
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
