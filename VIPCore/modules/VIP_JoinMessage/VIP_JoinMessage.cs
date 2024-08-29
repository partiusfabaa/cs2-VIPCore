using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using VipCoreApi;

namespace VIP_JoinMessage;

public class VIPJoinMessage : BasePlugin
{
    public override string ModuleAuthor => "panda";
    public override string ModuleName => "[VIP] Join Message";
    public override string ModuleVersion => "v1.0";
    private IVipCoreApi? _api;
    private JoinMessage? _join;
    private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");
    private IStringLocalizer<JoinMessage> _localizer = null!;
    public VIPJoinMessage(IStringLocalizer<JoinMessage> localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = PluginCapability.Get();
        if (_api == null) return;
        
        _join = new JoinMessage(this, _api, _localizer);
        _api.RegisterFeature(_join);
    }
    
    public override void Unload(bool hotReload)
    {
        if (_api != null && _join != null)
        {
            _api?.UnRegisterFeature(_join);
        }
    }
}
public class JoinMessage: VipFeatureBase
{
    public override string Feature => "JoinMessage";
    private IStringLocalizer<JoinMessage> _localizer;
    public JoinMessage(VIPJoinMessage joinMessage, IVipCoreApi api, IStringLocalizer<JoinMessage> localizer) : base(api)
    {
        _localizer = localizer;
        joinMessage.RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
    }
    private HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null) return HookResult.Continue;
        
        if (!PlayerHasFeature(player)) return HookResult.Continue;
        if (GetPlayerFeatureState(player) is IVipCoreApi.FeatureState.Disabled
            or IVipCoreApi.FeatureState.NoAccess) return HookResult.Continue;
        
        string message = GetRandomLocalizedMessage(player.PlayerName);
        string color_message = ReplaceColorPlaceholders(message);
        Server.PrintToChatAll(color_message);
        
        return HookResult.Continue;   
    }

    private string GetRandomLocalizedMessage(string playerName)
    {
        var messageKeys = _localizer.GetAllStrings()
                                    .Where(s => s.Name.StartsWith("message_"))
                                    .Select(s => s.Name)
                                    .ToList();
        
        if (messageKeys.Count == 0)
            return "{default}Welcome, {0}!";
        
        Random rand = new Random();
        int index = rand.Next(messageKeys.Count);
        string selectedMessage = _localizer[messageKeys[index]];
        
        return string.Format(selectedMessage, playerName);
    }

    public static readonly Dictionary<string, char> ColorMap = new Dictionary<string, char>
    {
        { "{default}", ChatColors.Default },
        { "{white}", ChatColors.White },
        { "{darkred}", ChatColors.DarkRed },
        { "{green}", ChatColors.Green },
        { "{lightyellow}", ChatColors.LightYellow },
        { "{lightblue}", ChatColors.LightBlue },
        { "{olive}", ChatColors.Olive },
        { "{lime}", ChatColors.Lime },
        { "{red}", ChatColors.Red },
        { "{lightpurple}", ChatColors.LightPurple },
        { "{purple}", ChatColors.Purple },
        { "{grey}", ChatColors.Grey },
        { "{yellow}", ChatColors.Yellow },
        { "{gold}", ChatColors.Gold },
        { "{silver}", ChatColors.Silver },
        { "{blue}", ChatColors.Blue },
        { "{darkblue}", ChatColors.DarkBlue },
        { "{bluegrey}", ChatColors.BlueGrey },
        { "{magenta}", ChatColors.Magenta },
        { "{lightred}", ChatColors.LightRed },
        { "{orange}", ChatColors.Orange }
    };

    public string ReplaceColorPlaceholders(string message)
    {
        if (!string.IsNullOrEmpty(message) && message[0] != ' ')
        {
            message = " " + message;
        }
        foreach (var colorPlaceholder in ColorMap)
        {
            message = message.Replace(colorPlaceholder.Key, colorPlaceholder.Value.ToString());
        }
        return message;
    }
}
