using System.Data;
using CounterStrikeSharp.API.Core;
using MySqlConnector;
using VIPCore.Configs;

namespace VIPCore;

public class DatabaseProvider : IFeature
{
    public string ConnectionString { get; }

    public DatabaseProvider(Config<VipConfig> coreConfig)
    {
        ConnectionString = BuildConnectionString(coreConfig.Value.Connection);
    }

    private string BuildConnectionString(VipConnection data)
    {
        return new MySqlConnectionStringBuilder
        {
            Server = data.Host,
            Database = data.Database,
            UserID = data.User,
            Password = data.Password,
            Port = data.Port,
            Pooling = true,
            MinimumPoolSize = 0,
            MaximumPoolSize = 640,
            ConnectionIdleTimeout = 30
        }.ToString();
    }

    public async Task<IDbConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}