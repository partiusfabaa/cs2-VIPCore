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
    private bool _isLoaded;
    private event Action<T>? OnLoadedReal;

    public event Action<T>? OnLoaded
    {
        add
        {
            if (_isLoaded)
                value?.Invoke(Value);

            OnLoadedReal += value;
        }
        remove => OnLoadedReal -= value;
    }

    public T Value { get; set; }
    public string Name { get; }

    public Config(string path, string name)
    {
        Name = name;
        _path = path;
        Load();
    }

    public void Load()
    {
        T obj;
        if (File.Exists(_path))
        {
            obj = JsonSerializer.Deserialize<T>(File.ReadAllText(_path), ConfigSystem.ConfigJsonOptions)!;
        }
        else
        {
            obj = new T();
            File.WriteAllText(_path, JsonSerializer.Serialize(obj, ConfigSystem.ConfigJsonOptions));
        }

        Value = obj;
        _isLoaded = true;
        OnLoadedReal?.Invoke(obj);
    }

    public void Save()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(Value, ConfigSystem.ConfigJsonOptions));
    }
}