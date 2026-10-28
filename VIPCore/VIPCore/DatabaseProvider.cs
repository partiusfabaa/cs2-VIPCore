using System.Data.Common;
using MySqlConnector;
using VIPCore.Configs;

namespace VIPCore;

public class DatabaseProvider(Config<VipConfig> coreConfig)
{
    public string ConnectionString { get; } = BuildConnectionString(coreConfig.Value.Connection);

    private static string BuildConnectionString(VipConnection data)
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
        }.ConnectionString;
    }

    public async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
