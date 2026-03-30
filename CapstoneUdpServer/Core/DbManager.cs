using Npgsql;
using StackExchange.Redis;

namespace CapstoneUdpServer.Core;

public class DbManager : IDisposable
{
    private NpgsqlDataSource? _dataSource;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;
    
    private string connectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=tkddbs321!";

    // PostgreSQL RDBMS 연결
    public async Task<bool> ConnectDBAsync()
    {
        try
        {
            _dataSource = NpgsqlDataSource.Create(connectionString);

            //await using var conn = await _dataSource.OpenConnectionAsync();

            return true;
        }
        catch (NpgsqlException e)
        {
            Console.WriteLine($"[DbManager] DB연결중 오류가 발생하였습니다! {e.Message}");
            return false;
        }
        
    }

    public async Task<bool> ConnectRedisAsync()
    {
        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            _db = _redis.GetDatabase();

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DbManager] Redis연결중 오류가 발생하였습니다! {e.Message}");
            return false;
        }
        
    }
    
    public void Dispose()
    {
        _dataSource?.Dispose();
        _redis?.Dispose();
    }

}