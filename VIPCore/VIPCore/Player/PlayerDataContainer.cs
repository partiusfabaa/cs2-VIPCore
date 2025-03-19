using CounterStrikeSharp.API.Core;

namespace VIPCore.Player;

public class PlayerDataContainer<T> where T : IDisposable
{
    public readonly T?[] Players = new T?[66];
    private readonly Func<int, T> _create;

    public PlayerDataContainer(BasePlugin plugin, Func<int, T> create)
    {
        _create = create;
        plugin.RegisterListener<Listeners.OnClientConnected>(OnConnected);
        plugin.RegisterListener<Listeners.OnClientDisconnect>(OnDisconnect);
    }

    private void OnConnected(int slot)
    {
        Players[slot] = _create(slot);
    }

    private void OnDisconnect(int slot)
    {
        var player = Players[slot];
        if (player is null) return;
        
        player.Dispose();
        Players[slot] = default;
    }

    public T? this[int i] => Players[i];
    public T? this[CCSPlayerController controller] => Players[controller.Slot];
}