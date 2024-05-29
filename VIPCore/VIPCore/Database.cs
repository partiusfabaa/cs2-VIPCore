using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using MySqlConnector;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VIPCore;

public class Database
{
    private readonly VipCore _vipCore;
    private readonly string _dbConnectionString;
    
    public Database(VipCore vipCore, string connection)
    {
        _vipCore = vipCore;
        _dbConnectionString = connection;
    }
    
    public async Task CreateTable()
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            await dbConnection.OpenAsync();

            var createVipUsersTable = @"
            CREATE TABLE IF NOT EXISTS `vip_users` (
                `account_id` BIGINT NOT NULL,
                `name` VARCHAR(64) NOT NULL,
                `lastvisit` BIGINT NOT NULL,
                `sid` BIGINT NOT NULL,
                `group` VARCHAR(64) NOT NULL,
                `expires` BIGINT NOT NULL,
            PRIMARY KEY (`account_id`, `sid`));";


            await dbConnection.ExecuteAsync(createVipUsersTable);

            var createVipServersTable = @"
             CREATE TABLE IF NOT EXISTS `vip_servers` (
                 `serverId` BIGINT NOT NULL,
                 `serverIp` VARCHAR(45) NOT NULL,
                 `port` INT NOT NULL,
                 `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                 `updated_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
             PRIMARY KEY (`serverId`));";

            await dbConnection.ExecuteAsync(createVipServersTable);

            // Check if the ServerIP and ServerPort already exist
            var checkVipServerQuery = @"
                SELECT COUNT(*) 
                FROM `vip_servers` 
                WHERE `serverIp` = @ServerIP AND `port` = @ServerPort;";

            var serverExists = await dbConnection.ExecuteScalarAsync<int>(checkVipServerQuery, new
            {
                ServerIP = _vipCore.CoreConfig.ServerIP,
                ServerPort = _vipCore.CoreConfig.ServerPort
            });

            if (serverExists == 0)
            {
                // Insert ServerIP and ServerPort from config into vip_servers table
                var insertVipServerQuery = @"
                INSERT INTO `vip_servers` (`serverId`, `serverIp`, `port`) 
                VALUES (@ServerId, @ServerIP, @ServerPort);";

                await dbConnection.ExecuteAsync(insertVipServerQuery, new
                {
                    ServerId = _vipCore.CoreConfig.ServerId,
                    ServerIP = _vipCore.CoreConfig.ServerIP,
                    ServerPort = _vipCore.CoreConfig.ServerPort,
                });
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<User?> GetExistingUserFromDb(int accountId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid",
                new { AccId = accountId, sid = serverId });

            if (existingUser != null) return existingUser;

            _vipCore.PrintLogError("User not found");
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public async Task AddUserToDb(User user)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = user.account_id,
                    sid = serverId
                });

            if (existingUser != null)
            {
                _vipCore.PrintLogWarning("User already exists");
                return;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO vip_users (account_id, name, lastvisit, sid, `group`, expires)
                VALUES (@account_id, @name, @lastvisit, @sid, @group, @expires);", user);

            _vipCore.PrintLogInfo("Player '{name} [{accId}]' has been successfully added", user.name, user.account_id);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task UpdateUserInDb(User user)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);
            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = user.account_id,
                    sid = serverId
                });

            if (existingUser == null)
            {
                _vipCore.PrintLogWarning("User does not exist");
                return;
            }

            await connection.ExecuteAsync(@"
            UPDATE 
                vip_users
            SET 
                name = @name,
                lastvisit = @lastvisit,
                `group` = @group,
                expires = @expires
            WHERE account_id = @account_id AND sid = @sid;", user);

            _vipCore.PrintLogInfo("Player '{name} [{accId}]' has been successfully updated", user.name, user.account_id);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task UpdateUserVip(int accountId, string name = "", string group = "", int time = -1)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);
            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = accountId,
                    sid = serverId
                });

            if (existingUser == null)
            {
                _vipCore.PrintLogWarning($"User with account ID '{accountId}' does not exist");
                return;
            }

            if (!string.IsNullOrEmpty(name))
                existingUser.name = name;

            if (!string.IsNullOrEmpty(group))
                existingUser.group = group;
            
            if (time > -1)
                existingUser.expires = time == 0 ? 0 : _vipCore.CalculateEndTimeInSeconds(time);
         
            await connection.ExecuteAsync(@"
            UPDATE 
                vip_users
            SET 
                name = @name,
                `group` = @group,
                expires = @expires
            WHERE account_id = @account_id AND sid = @sid;", existingUser);

            _vipCore.PrintLogInfo($"Player '{existingUser.name} [{accountId}]' VIP information has been successfully updated");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task RemoveUserFromDb(int accId)
    {
        var existingUser = await GetExistingUserFromDb(accId);

        if (existingUser == null)
            return;

        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);
            await connection.ExecuteAsync(@"
            DELETE FROM vip_users
        WHERE account_id = @AccId AND sid = @sid;", new { AccId = accId, sid = serverId });

            _vipCore.PrintLogInfo("Player {name}[{accId}] has been successfully removed", existingUser.name, accId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<List<User?>?> GetUserFromDb(int accId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);
            var user = await connection.QueryAsync<User?>(
                "SELECT * FROM `vip_users` WHERE `account_id` = @AccId AND sid = @sid AND (expires > @CurrTime OR expires = 0)",
                new { AccId = accId, sid = serverId, CurrTime = DateTime.UtcNow.GetUnixEpoch() }
            );

            return user.ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    public async Task RemoveExpiredUsers(CCSPlayerController player, SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var serverId = await GetServerId(connection);

            var expiredUsers = await connection.QueryAsync<User>(
                "SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid AND expires < @CurrentTime AND expires > 0",
                new
                {
                    AccId = steamId.AccountId,
                    sid = serverId,
                    CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            Console.WriteLine($"Removing expired VIPS, Current time:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            foreach (var user in expiredUsers)
            {
                await connection.ExecuteAsync("DELETE FROM vip_users WHERE account_id = @AccId AND sid = @sid",
                    new
                    {
                        AccId = user.account_id,
                        user.sid
                    });

                await Server.NextFrameAsync(() =>
                {
                    var authSteamId = player.AuthorizedSteamID;
                    if (authSteamId != null && authSteamId.AccountId == user.account_id)
                        _vipCore.PrintToChat(player, _vipCore.Localizer["vip.Expired", user.group]);

                    _vipCore.VipApi.OnPlayerRemoved(player, user.group);
                });

                _vipCore.PrintLogInfo("User '{name} [{accId}]' has been removed due to expired VIP status.", user.name,
                    user.account_id);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    private async Task<long> GetServerId(MySqlConnection connection)
    {
        var query = @"
                SELECT `serverId`
                FROM `vip_servers`
                WHERE `serverIp` = @ServerIP AND `port` = @ServerPort;";

        return await connection.ExecuteScalarAsync<long>(query, new { ServerIP = _vipCore.CoreConfig.ServerIP, ServerPort = _vipCore.CoreConfig.ServerPort });
    }
}