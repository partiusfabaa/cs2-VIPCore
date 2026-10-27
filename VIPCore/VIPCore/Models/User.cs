using VipCoreApi;

namespace VIPCore.Models;

public class User
{
    public int account_id { get; set; }
    public required string name { get; set; }
    public int lastvisit { get; set; }
    public int sid { get; set; }
    public required string group { get; set; }
    public int expires { get; set; }
    public Dictionary<string, IVipCoreApi.FeatureState> FeatureState { get; set; } = new();
}