namespace VIPCore;

public class Config
{
    public float Delay { get; init; } = 2;

    public Dictionary<string, VipGroup> Groups { get; init; } = new()
    {
        {
            "GROUP_NAME", new VipGroup
            {
                Values = new Dictionary<string, object>()
            }
        }
    };
}

public class VipGroup
{
    public Dictionary<string, object> Values { get; init; } = new();
}

public class CoreConfig
{
    public int TimeMode { get; init; } = 0;
    public int ServerId { get; init; } = 0;
    public bool UseCenterHtmlMenu { get; init; } = true;

    //public bool DisplayUnavailableOptions { get; init; }
    public bool ReOpenMenuAfterItemClick { get; init; } = false;
    public bool VipLogging { get; init; } = true;

    public VipDb Connection { get; init; } = new()
    {
        Host = "HOST",
        Database = "DATABASENAME",
        User = "USER",
        Password = "PASSWORD",
        Port = 3306
    };
}

public class VipDb
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public int Port { get; init; }
}