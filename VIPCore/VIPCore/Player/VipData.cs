using System.ComponentModel.DataAnnotations.Schema;

namespace VIPCore.Player;

public class VipData : IDapperObject
{
    [Column("account_id")] public int AccountId { get; set; }
    [Column("name")] public string Name { get; set; }
    [Column("lastvisit")] public long LastVisit { get; set; }
    [Column("sid")] public int ServerId { get; set; }
    [Column("group")] public string Group { get; set; }
    [Column("expires")] public long Expires { get; set; }
}