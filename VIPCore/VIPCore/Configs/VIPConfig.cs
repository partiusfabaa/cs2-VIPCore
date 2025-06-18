
namespace VIPCore.Configs;

public class VipConfig
{
    public int Delay { get; init; } = 2;
    public int TimeMode { get; init; } = 0;
    public int ServerId { get; init; } = 0;
    public string MenuType { get; init; } = "screen";
    public bool ReOpenMenuAfterItemClick { get; init; } = false;
    public string[] AdminMenuPermission { get; set; } = ["@css/root"];
    public bool VipLogging { get; init; } = true;

    public VipConnection Connection { get; init; } = new()
    {
        Host = "HOST",
        Database = "DATABASENAME",
        User = "USER",
        Password = "PASSWORD",
        Port = 3306
    };

}

public class VipConnection
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public uint Port { get; init; }
}