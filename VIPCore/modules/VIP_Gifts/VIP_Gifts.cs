using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Utils;

namespace VIP_Gift;

public class VIP_GiftConfig : BasePluginConfig
{
    [JsonPropertyName("MinimumPlayersForGifts")]
    public int MinimumPlayersForGifts { get; set; } = 15;

    [JsonPropertyName("MaxGiftedPlayers")]
    public int MaxGiftedPlayers { get; set; } = 2;

    [JsonPropertyName("Gifts")]
    public List<GiftItem> Gifts { get; set; } = new()
    {
        new GiftItem { Name = "VIP 1h", Type = "command", Value = "css_vip_adduser {STEAMID} VIP 3600", Chance = 50 },
        new GiftItem { Name = "VIP 2h", Type = "command", Value = "css_vip_adduser {STEAMID} VIP 7200", Chance = 30 },
        new GiftItem { Name = "VIP 3h", Type = "command", Value = "css_vip_adduser {STEAMID} VIP 10800", Chance = 20 }
    };
}

public class GiftItem
{
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("Value")] public string Value { get; set; } = string.Empty;
    [JsonPropertyName("Chance")] public int Chance { get; set; } = 0;
}

[MinimumApiVersion(130)]
public class VipGiftPlugin : BasePlugin, IPluginConfig<VIP_GiftConfig>
{
    public override string ModuleName => "VIP Gifts";
    public override string ModuleAuthor => "GSM-RO";
    public override string ModuleVersion => "1.0.0";

    public VIP_GiftConfig Config { get; set; } = new();
    private readonly Random _random = new();
    private bool _giftGiven = false;
    public void OnConfigParsed(VIP_GiftConfig config)
    {
        Config = config;
    }
   

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundAnnounceLastRoundHalf>(OnLastRoundAnnounce);
        RegisterListener<Listeners.OnMapStart>(_ => _giftGiven = false);
    }

    private HookResult OnLastRoundAnnounce(EventRoundAnnounceLastRoundHalf evt, GameEventInfo info)
    {
        
        if (_giftGiven)
            return HookResult.Continue;

            
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
        if (players.Count < Config.MinimumPlayersForGifts)
        {
            Logger.LogInformation($"[VipGiftPlugin] Not enough players online ({players.Count}/{Config.MinimumPlayersForGifts}). No gifts given.");
            return HookResult.Continue;
        }

        _giftGiven = true;

        int giftsToGive = Math.Min(Config.MaxGiftedPlayers, players.Count);
        if (giftsToGive <= 0)
        {
            Logger.LogInformation("[VipGiftPlugin] No gifts to give, MaxGiftedPlayers is set to 0.");
            return HookResult.Continue;
        }
        var selected = players.OrderBy(_ => _random.Next()).Take(giftsToGive);

        foreach (var player in selected)
        {
            var gift = GetRandomGift();
            if (gift != null)
            {
                GiveGiftToPlayer(player, gift);
                player.PrintToCenter($"You received a gift: {gift.Name}!");
                player.PrintToChat($"{ChatColors.Green}[Store]{ChatColors.Default} ******************************");
                player.PrintToChat($"{ChatColors.Green}[Gifts]{ChatColors.Green} You received: {ChatColors.Red}{gift.Name}{ChatColors.Green}!");
                player.PrintToChat($"{ChatColors.Green}[Store]{ChatColors.Default} ******************************");
                player.ExecuteClientCommand("play sounds/ui/item_drop1_common.vsnd_c");
                Logger.LogInformation($"[Gifts] {player.PlayerName} ({player.SteamID}) received: {gift.Name} [{gift.Type}:{gift.Value}]");
            }
        }

        return HookResult.Continue;
    }

    private GiftItem? GetRandomGift()
    {
        int totalChance = Config.Gifts.Sum(g => g.Chance);
        if (totalChance <= 0) return null;

        int roll = _random.Next(1, totalChance + 1);
        int accumulated = 0;

        foreach (var gift in Config.Gifts)
        {
            accumulated += gift.Chance;
            if (roll <= accumulated)
                return gift;
        }

        return null;
    }

    private void GiveGiftToPlayer(CCSPlayerController player, GiftItem gift)
    {
        if (!player.IsValid) return;

        if (gift.Type.Equals("command", StringComparison.OrdinalIgnoreCase))
        {
            string command = gift.Value
                .Replace("{STEAMID}", player.SteamID.ToString())
                .Replace("{PLAYERNAME}", player.PlayerName ?? "Player");

            Server.ExecuteCommand(command);
        }
    }
}
