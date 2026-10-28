using Dapper;
using Microsoft.Extensions.Logging;
using VIPCore.Configs;
using VIPCore.Player;

namespace VIPCore.Services;

public class DatabaseService(Plugin plugin, DatabaseProvider dbProvider, Config<VipConfig> coreConfig)
{
    private int ServerId => coreConfig.Value.ServerId;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public async Task CreateTablesAsync()
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS `vip_users` (
                    `account_id` BIGINT NOT NULL,
                    `name` VARCHAR(64) NOT NULL,
                    `lastvisit` BIGINT NOT NULL,
                    `sid` BIGINT NOT NULL,
                    `group` VARCHAR(64) NOT NULL,
                    `expires` BIGINT NOT NULL,
                    PRIMARY KEY (`account_id`, `sid`)
                );
                """);

            await connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS `vip_servers` (
                    `serverId` BIGINT NOT NULL,
                    `serverIp` VARCHAR(45) NOT NULL,
                    `port` INT NOT NULL,
                    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` TIMESTAMP,
                    PRIMARY KEY (`serverId`)
                );
                """);

            var config = coreConfig.Value;
            await connection.ExecuteAsync(
                """
                INSERT IGNORE INTO `vip_servers` (`serverId`, `serverIp`, `port`)
                VALUES (@ServerId, @ServerIp, @ServerPort);
                """,
                new { config.ServerId, config.ServerIp, config.ServerPort });
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Database initialization failed: {ex}", ex);
        }
    }

    public async Task<VipData?> GetUserAsync(int accountId)
    {
        await using var connection = await dbProvider.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<VipData>(
            """
            SELECT * FROM `vip_users`
            WHERE `account_id` = @AccountId AND `sid` = @ServerId AND (`expires` = 0 OR `expires` > @Now)
            """,
            new { AccountId = accountId, ServerId, Now });
    }

    public async Task UpsertUserAsync(VipData data)
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();
            await connection.ExecuteAsync(
                """
                INSERT INTO `vip_users` (`account_id`, `name`, `lastvisit`, `sid`, `group`, `expires`)
                VALUES (@AccountId, @Name, @LastVisit, @ServerId, @Group, @Expires)
                ON DUPLICATE KEY UPDATE
                    `name` = VALUES(`name`),
                    `lastvisit` = VALUES(`lastvisit`),
                    `group` = VALUES(`group`),
                    `expires` = VALUES(`expires`)
                """,
                data);

            plugin.LogInfo("Player '{name} [{accountId}]' has been successfully added", data.Name, data.AccountId);
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Failed to add user {accountId}: {ex}", data.AccountId, ex);
        }
    }

    public async Task UpdateUserAsync(int accountId, string? name, string? group, long? expires)
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();
            var updated = await connection.ExecuteAsync(
                """
                UPDATE `vip_users` SET
                    `name` = COALESCE(@Name, `name`),
                    `group` = COALESCE(@Group, `group`),
                    `expires` = COALESCE(@Expires, `expires`)
                WHERE `account_id` = @AccountId AND `sid` = @ServerId
                """,
                new { AccountId = accountId, ServerId, Name = name, Group = group, Expires = expires });

            if (updated == 0)
                plugin.LogWarning("User with account ID '{accountId}' does not exist", accountId);
            else
                plugin.LogInfo("Player [{accountId}] VIP information has been successfully updated", accountId);
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Failed to update user {accountId}: {ex}", accountId, ex);
        }
    }

    public async Task UpdateLastVisitAsync(int accountId, string name)
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();
            await connection.ExecuteAsync(
                """
                UPDATE `vip_users` SET `name` = @Name, `lastvisit` = @Now
                WHERE `account_id` = @AccountId AND `sid` = @ServerId
                """,
                new { AccountId = accountId, ServerId, Name = name, Now });
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Failed to update last visit of {accountId}: {ex}", accountId, ex);
        }
    }

    public async Task RemoveUserAsync(int accountId)
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();
            var removed = await connection.ExecuteAsync(
                "DELETE FROM `vip_users` WHERE `account_id` = @AccountId AND `sid` = @ServerId",
                new { AccountId = accountId, ServerId });

            if (removed != 0)
                plugin.LogInfo("Player [{accountId}] has been successfully removed", accountId);
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Failed to remove user {accountId}: {ex}", accountId, ex);
        }
    }

    public async Task RemoveExpiredUserAsync(int accountId)
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();
            await connection.ExecuteAsync(
                """
                DELETE FROM `vip_users`
                WHERE `account_id` = @AccountId AND `sid` = @ServerId AND `expires` > 0 AND `expires` < @Now
                """,
                new { AccountId = accountId, ServerId, Now });
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Failed to remove expired user {accountId}: {ex}", accountId, ex);
        }
    }

    public async Task PurgeExpiredUsersAsync()
    {
        try
        {
            await using var connection = await dbProvider.OpenConnectionAsync();
            var removed = await connection.ExecuteAsync(
                "DELETE FROM `vip_users` WHERE `sid` = @ServerId AND `expires` > 0 AND `expires` < @Now",
                new { ServerId, Now });

            if (removed != 0)
                plugin.LogInfo("{count} expired VIP users have been removed from the database", removed);
        }
        catch (Exception ex)
        {
            plugin.Logger.LogError("Expired users purge failed: {ex}", ex);
        }
    }
}
