using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core.Translations;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;

namespace VIPCore.Services;

public class DatabaseService : IFeature
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Plugin _plugin;
    private readonly DatabaseProvider _dbProvider;
    private readonly int _serverId;
    private PlayersManager _playersManager;

    public DatabaseService(
        IServiceProvider serviceProvider,
        Plugin plugin,
        DatabaseProvider dbProvider,
        Config<VipConfig> config)
    {
        _serviceProvider = serviceProvider;
        _plugin = plugin;
        _dbProvider = dbProvider;
        _serverId = config.Value.ServerId;
    }

    public async Task CreateTableAsync()
    {
        _playersManager = _serviceProvider.GetRequiredService<PlayersManager>();
        try
        {
            using var connection = await _dbProvider.OpenConnectionAsync();

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS `vip_users` (
                    `account_id` BIGINT NOT NULL,
                    `name` VARCHAR(64) NOT NULL,
                    `last_visit` BIGINT NOT NULL,
                    `sid` BIGINT NOT NULL,
                    `group` VARCHAR(64) NOT NULL,
                    `expires` BIGINT NOT NULL,
                    PRIMARY KEY (`account_id`, `sid`)
                );
                """);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("Database initialization failed, {ex}", ex);
        }
    }

    public async Task<VipData?> GetUserAsync(int accountId)
    {
        try
        {
            using var connection = await _dbProvider.OpenConnectionAsync();
            return await connection.QuerySingleOrDefaultAsync<VipData>(
                "SELECT * FROM vip_users WHERE account_id = @AccountId AND sid = @ServerId",
                new { AccountId = accountId, ServerId = _serverId });
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("Failed to fetch user: {ex}", ex);
            return null;
        }
    }

    public async Task AddUserAsync(VipData data)
    {
        try
        {
            using var connection = await _dbProvider.OpenConnectionAsync();

            if (await GetUserAsync(data.AccountId) != null)
            {
                _plugin.Logger.LogWarning($"User {data.Name} already exists");
                return;
            }

            await connection.ExecuteAsync(
                """
                INSERT INTO vip_users 
                (account_id, name, lastvisit, sid, `group`, expires)
                VALUES (@AccountId, @Name, @LastVisit, @ServerId, @Group, @Expires)
                """,
                new
                {
                    data.AccountId,
                    data.Name,
                    data.LastVisit,
                    ServerId = _serverId,
                    data.Group,
                    data.Expires
                });

            _plugin.Logger.LogInformation($"Added: {data.Name} [{data.AccountId}]");
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("User addition failed {ex}", ex);
        }
    }

    public async Task UpdateUserAsync(VipData data)
    {
        try
        {
            using var connection = await _dbProvider.OpenConnectionAsync();

            if (await GetUserAsync(data.AccountId) == null)
            {
                _plugin.Logger.LogWarning("User not found");
                return;
            }

            await connection.ExecuteAsync(
                """
                UPDATE vip_users SET
                    name = @Name,
                    lastvisit = @LastVisit,
                    `group` = @Group,
                    expires = @Expires
                WHERE account_id = @AccountId AND sid = @ServerId
                """,
                new
                {
                    data.AccountId,
                    data.Name,
                    data.LastVisit,
                    data.Group,
                    data.Expires,
                    ServerId = _serverId
                });

            _plugin.Logger.LogInformation($"Updated: {data.Name} [{data.AccountId}]");
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("User update failed {ex}", ex);
        }
    }

    public async Task UpdateVipAsync(int accountId, string? name = null, string? group = null, int duration = -1)
    {
        try
        {
            var user = await GetUserAsync(accountId);
            if (user == null) return;

            var modified = false;

            if (!string.IsNullOrEmpty(name) && user.Name != name)
            {
                user.Name = name;
                modified = true;
            }

            if (!string.IsNullOrEmpty(group) && user.Group != group)
            {
                user.Group = group;
                modified = true;
            }

            if (duration >= 0)
            {
                user.Expires = duration == 0
                    ? 0
                    : duration; //_plugin.CalculateEndTimeInSeconds(duration);
                modified = true;
            }

            if (modified) await UpdateUserAsync(user);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("VIP update failed {ex}", ex);
        }
    }

    public async Task RemoveUserAsync(int accountId)
    {
        try
        {
            using var connection = await _dbProvider.OpenConnectionAsync();

            await connection.ExecuteAsync(
                "DELETE FROM vip_users WHERE account_id = @AccountId AND sid = @ServerId",
                new { AccountId = accountId, ServerId = _serverId });

            _plugin.Logger.LogInformation($"Removed user: {accountId}");
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("User removal failed", ex);
        }
    }

    public async Task PurgeExpiredUsersAsync()
    {
        try
        {
            using var connection = await _dbProvider.OpenConnectionAsync();

            var expiredUsers = await connection.QueryAsync<VipData>(
                """
                SELECT * FROM vip_users 
                WHERE expires < @CurrentTime 
                    AND expires > 0 
                    AND sid = @ServerId
                """,
                new
                {
                    CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ServerId = _serverId
                });

            foreach (var user in expiredUsers)
            {
                await RemoveUserAsync(user.AccountId);

                await Server.NextFrameAsync(() =>
                {
                    var player = Utilities.GetPlayerFromSteamId((ulong)user.AccountId);
                    if (player == null) return;

                    _playersManager.PrintToChat(player, _plugin.Localizer.ForPlayer(player, "vip.Expired", user.Group));
                });
            }
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError("Expired users purge failed {ex}", ex);
        }
    }
}