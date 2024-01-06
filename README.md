# cs2-VIPCore

#### If you find an error or anything else. Please message me in discord: thesamefabius

<br>
<p align="center">
<a href="https://www.buymeacoffee.com/thesamefabius"><img src="https://img.buymeacoffee.com/button-api/?text=Support my work&emoji=🐱&slug=thesamefabius&button_colour=febee6&font_colour=000000&font_family=Inter&outline_colour=000000&coffee_colour=FFDD00" /></a>
</p>

# Installation
1. Install [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp), [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master) and [CSSModularity](https://github.com/Muinez/CSSModularity)
3. Download [VIPCore](https://github.com/partiusfabaa/cs2-VIPCore/releases)
4. Unpack the archive and upload it to the game server (example path: addons/counterstrikesharp/plugins/ModularityPlugin/)

### Put the modules in this path
`addons/counterstrikesharp/plugins/ModularityPlugin/plugins`

# Commands 

| Command                             | Description                                               |
|-------------------------------------|-----------------------------------------------------------|
| `css_vip_reload` or `!vip_reload`    | Reloads the configuration. (`@css/root`) |
| `css_vip_adduser "steamid or accountid" "vipgroup" "time or 0 permanently"` | Adds a VIP player (for server console only) |
| `css_vip_updategroup "steamid or accountid" "vipgroup"` | Updates the player's VIP Group (for server console only) |
| `css_vip_updatetime "steamid or accountid" "time or 0 permanently"` | Updates the player's time (for server console only) |
| `css_vip_deleteuser "steamid or accountid"` | Allows you to delete a player by SteamID identifier (for server console only) |
| `css_vip` or `!vip` | Opens the VIP menu |

# Configs (addons/counterstrikesharp/configs/plugins/VIPCore/)

### Core.json
```json
{
  "TimeMode": 0,         // 0 - seconds | 1 - minutes | 2 - hours | 3 - days)
  "ServerId": 0,		 // SERVER ID
  "VipLogging": true,    //Whether to log VIPCore | true - yes | false - no
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
  "Groups": {
     "VIP1": {
        "Values": {
            "features": values
        }
     }
  }
}
```

# What you need to write a module
1. Add the dll from CSSModularity to your project (the dll can be found at this path: `addons/counterstrikesharp/plugins/ModularityPlugin/shared/Modularity/Modularity.dll`)
2. Add the dll from VipCoreApi to your project (the dll can be found at this path: `addons/counterstrikesharp/plugins/ModularityPlugin/shared/VipCoreApi/VipCoreApi.dll`)

# Example module

```csharp
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Modularity;
using VipCoreApi;

namespace VIPMyModule;

public class VIPMyModule : BasePlugin, IModulePlugin
{
    public override string ModuleAuthor => "Author";
    public override string ModuleName => "[VIP] My Module";
    public override string ModuleVersion => "1.0.0";

    private static readonly string Feature = "MyFeature";
    private IVipCoreApi _api = null!;
    private TestConfig _config = null!;

    public void LoadModule(IApiProvider provider)
    {
        _api = provider.Get<IVipCoreApi>();
        _api.RegisterFeature(Feature, selectItem: OnSelectItem);
        _api.OnPlayerSpawn += OnPlayerSpawn;
	_config = _api.LoadConfig<TestConfig>("VIPMyModule");
    }

    private void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_api.PlayerHasFeature(player, Feature))
        {
            _api.PrintToChat(player, $"VIP player - {player.PlayerName} has spawned");
        }
    }

    private void OnSelectItem(CCSPlayerController player, IVipCoreApi.FeatureState state)
    {
        if (state == IVipCoreApi.FeatureState.Enabled)
        {
            _api.PrintToChat(player, "Enabled");
        }
        else
        {
            _api.PrintToChat(player, "Disabled");
        }
    }
    
    [ConsoleCommand("css_viptestcommand")]
    public void OnCmdVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (_api.IsClientVip(player) && _api.PlayerHasFeature(player, Feature))
        {
            _api.PrintToChat(player, $"{player.PlayerName} is a VIP player");
            return;
        }
        
        _api.PrintToChat(player, $"{player.PlayerName} not a VIP player");
    }

    public override void Unload(bool hotReload)
    {
        _api.UnRegisterFeature(Feature);
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
