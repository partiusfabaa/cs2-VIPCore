using System.Text.Json;

namespace VIPCore.Configs;

public interface IConfig
{
    string Name { get; }
    void Load();
}

public class Config<T> : IConfig where T : new()
{
    private readonly string _path;

    public T Value { get; private set; } = default!;
    public string Name { get; }

    public Config(string path, string name)
    {
        Name = name;
        _path = path;
        Load();
    }

    public void Load()
    {
        if (File.Exists(_path))
        {
            Value = JsonSerializer.Deserialize<T>(File.ReadAllText(_path), ConfigSystem.ConfigJsonOptions) ??
                    throw new JsonException($"{Name}.json cannot be deserialized");
            return;
        }

        Value = new T();
        Save();
    }

    public void Save()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(Value, ConfigSystem.ConfigJsonOptions));
    }
}
