using System.Text.Json;

namespace VIPCore;

public class Cfg
{
    private readonly VipCore _vipCore;

    public Cfg(VipCore vipCore)
    {
        _vipCore = vipCore;
    }

    public Config LoadConfig()
    {
        var configPath = Path.Combine(_vipCore.ModuleDirectory, "vip.json");

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            Groups = new Dictionary<string, VipGroup>()
            {
                {
                    "GROUP_NAME", new VipGroup
                    {
                        Values = new Dictionary<string, string>()
                    }
                }
            },
            Connection = new VipDb
            {
                Host = "HOST",
                Database = "DATABASENAME",
                User = "USER",
                Password = "PASSWORD"
            }
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("[VIPCore] The configuration was successfully saved to a file: " + configPath);
        Console.ResetColor();

        return config;
    }
}

public class Config
{
    public Dictionary<string, VipGroup> Groups { get; set; } = null!;
    public VipDb Connection { get; set; } = null!;
}

public class VipGroup
{
    public Dictionary<string, string> Values { get; set; } = null!;
}

public class VipDb
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
}