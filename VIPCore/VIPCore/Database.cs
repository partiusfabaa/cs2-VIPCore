using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using MySqlConnector;

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
            
            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid",
                new { AccId = accountId, sid = _vipCore.CoreConfig.ServerId });

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

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = user.account_id,
                    sid = _vipCore.CoreConfig.ServerId
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

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = user.account_id,
                    sid = _vipCore.CoreConfig.ServerId
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

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid", new
                {
                    AccId = accountId,
                    sid = _vipCore.CoreConfig.ServerId
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
                existingUser.expires = DateTime.UtcNow.AddSeconds(time).GetUnixEpoch();

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
            
            await connection.ExecuteAsync(@"
            DELETE FROM vip_users
        WHERE account_id = @AccId AND sid = @sid;", new { AccId = accId, sid = _vipCore.CoreConfig.ServerId });

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
            
            var user = await connection.QueryAsync<User?>(
                "SELECT * FROM `vip_users` WHERE `account_id` = @AccId AND sid = @sid AND (expires > @CurrTime OR expires = 0)",
                new { AccId = accId, sid = _vipCore.CoreConfig.ServerId, CurrTime = DateTime.UtcNow.GetUnixEpoch() }
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

            var expiredUsers = await connection.QueryAsync<User>(
                "SELECT * FROM vip_users WHERE account_id = @AccId AND sid = @sid AND expires < @CurrentTime AND expires > 0",
                new
                {
                    AccId = steamId.AccountId,
                    sid = _vipCore.CoreConfig.ServerId,
                    CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

            foreach (var user in expiredUsers)
            {
                await connection.ExecuteAsync("DELETE FROM vip_users WHERE account_id = @AccId AND sid = @sid",
                    new
                    {
                        AccId = user.account_id,
                        user.sid
                    });

                Server.NextFrame(() =>
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
}