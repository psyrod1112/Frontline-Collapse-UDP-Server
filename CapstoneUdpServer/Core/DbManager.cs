using CapstoneUdpServer.Data;
using Npgsql;
using StackExchange.Redis;

namespace CapstoneUdpServer.Core;

#region DB 받아올 타입 클래스

public class DB_players
{
    public int Player_id { get; set; }
    public string Player_name { get; set; }
    public int Win_score { get; set; }
    public float Win_rate { get; set; }
    public PlayerRank Player_rank { get; set; }
    public DateTimeOffset Created_at { get; set; }

    public DB_players(int player_id, string player_name, int win_score, float win_rate, PlayerRank player_rank,
        DateTimeOffset created_at)
    {
        Player_id = player_id;
        Player_name = player_name;
        Win_score = win_score;
        Win_rate = win_rate;
        Player_rank = player_rank;
        Created_at = created_at;
    }
}

public class DB_gamelogs
{
    public int Log_id { get; set; }
    public int Player_id { get; set; }
    public int Enemy_id { get; set; }
    public bool Game_result { get; set; }
    public DateTimeOffset Created_at { get; set; }

    public DB_gamelogs(int log_id, int player_id, int enemy_id, bool game_result, DateTimeOffset created_at)
    {
        Log_id = log_id;
        Player_id = player_id;
        Enemy_id = enemy_id;
        Game_result = game_result;
        Created_at = created_at;
    }
}

public class Redis_players
{
    public int Player_id { get; set; }
    public int Win_score { get; set; }
    public float Win_rate { get; set; }
    public PlayerRank Player_rank { get; set; }

    public Redis_players(int player_id, int win_score, float win_rate, PlayerRank player_rank)
    {
        Player_id = player_id;
        Win_score = win_score;
        Win_rate = win_rate;
        Player_rank = player_rank;
    }
}



#endregion


public class DbManager : IDisposable
{
    private NpgsqlDataSource? _dataSource;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;

    private string connectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=tkddbs321!";

    #region DB 연결 메서드

    // PostgreSQL 연결 풀 생성
    public async Task<bool> ConnectDBAsync()
    {
        try
        {
            _dataSource = NpgsqlDataSource.Create(connectionString);
            Console.WriteLine("[DbManager] ConnectDBAsync: DB 연결 완료");
            return true;
        }
        catch (NpgsqlException e)
        {
            Console.WriteLine($"[DbManager] ConnectDBAsync: DB연결중 오류가 발생하였습니다! {e.Message}");
            return false;
        }
    }

    // Redis 연결 및 IDatabase 인스턴스 초기화
    public async Task<bool> ConnectRedisAsync()
    {
        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            _db = _redis.GetDatabase();
            Console.WriteLine("[DbManager] ConnectRedisAsync: Redis 연결 완료");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DbManager] ConnectRedisAsync: Redis연결중 오류가 발생하였습니다! {e.Message}");
            return false;
        }
    }

    #endregion


    #region 메서드

    // Redis 캐시 조회 → 없으면 DB 조회 → 없으면 DB INSERT 후 캐싱
    // 항상 Redis_players 반환 (로그인/재접속 공통 진입점)
    public async Task<Redis_players?> SearchPlayerFromDB(string playerName)
    {
        try
        {
            if (await _db.KeyExistsAsync($"players:{playerName}"))
            {
                // 캐시 히트: Redis에서 바로 반환
                Console.WriteLine($"[DbManager] SearchPlayerFromDB: {playerName} Redis 캐시 히트");
                return await SearchPlayerFromRedis(playerName);
            }
            else
            {
                // 캐시 미스: DB 조회
                DB_players? playerDBData = await SelectPlayerFromDataSource(playerName);
                if (playerDBData == null)
                {
                    // DB에도 없으면 신규 플레이어 INSERT
                    await InsertPlayerFromDataSource(playerName);
                    playerDBData = await SelectPlayerFromDataSource(playerName);
                    Console.WriteLine($"[DbManager] SearchPlayerFromDB: {playerName} 신규 플레이어 생성 완료");
                }
                else
                {
                    Console.WriteLine($"[DbManager] SearchPlayerFromDB: {playerName} DB 조회 완료");
                }

                // DB 결과를 Redis에 캐싱
                await CachingPlayerToRedis(playerDBData);
                return await SearchPlayerFromRedis(playerName);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DbManager] SearchPlayerFromDB: 오류 발생 {e.Message}");
            return null;
        }
    }
    

    // DB players 테이블에서 player_name으로 단건 조회
    // 나중에 인룸 플레이어 정보 가져오거나, 플레이어 전적 패킷 받을 때 DB만 접근해야되서 빼놓음
    public async Task<DB_players?> SelectPlayerFromDataSource(string playerName)
    {
        try
        {
            await using var cmd = _dataSource?.CreateCommand(
                "SELECT player_id, player_name, win_score, win_rate, player_rank, created_at FROM players WHERE player_name = @name");
            cmd.Parameters.AddWithValue("name", playerName);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine($"[DbManager] SelectPlayerFromDataSource: {playerName} DB 조회 성공");
                return new DB_players(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), 
                    reader.GetFloat(3), (PlayerRank)reader.GetInt32(4), reader.GetFieldValue<DateTimeOffset>(5));
            }

            Console.WriteLine($"[DbManager] SelectPlayerFromDataSource: {playerName} DB에 존재하지 않음");
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DbManager] SelectPlayerFromDataSource: 오류 발생 {e.Message}");
            return null;
        }
    }

    // DB players 테이블에 신규 플레이어 INSERT, 생성된 player_id 반환
    private async Task InsertPlayerFromDataSource(string playerName)
    {
        try
        {
            await using var cmd = _dataSource?.CreateCommand(
                "INSERT INTO players (player_name) VALUES (@name) RETURNING player_id");
            cmd.Parameters.AddWithValue("name", playerName);

            int newPlayerId = (int)await cmd.ExecuteScalarAsync();
            Console.WriteLine($"[DbManager] InsertPlayerFromDataSource: {playerName} INSERT 완료 (player_id={newPlayerId})");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DbManager] InsertPlayerFromDataSource: 오류 발생 {e.Message}");
        }
    }

    public async Task<List<DB_gamelogs>> ShowGamelogsFromDataSource(int playerId)
    {
        var selectedLogList = new List<DB_gamelogs>();
        
        await using var cmd = _dataSource?.CreateCommand(
            "SELECT log_id, player_id, enemy_id, game_result, created_at FROM gamelogs WHERE player_id = @player_id");

        cmd.Parameters.AddWithValue("player_id", playerId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            DB_gamelogs? gamelog = new DB_gamelogs(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), 
                reader.GetBoolean(3), reader.GetFieldValue<DateTimeOffset>(4));
            
            selectedLogList.Add(gamelog);
        }
        
        return selectedLogList;
    }
    
    

    // Redis Hash에서 플레이어 데이터 조회 후 Redis_players로 변환
    public async Task<Redis_players?> SearchPlayerFromRedis(string playerName)
    {
        HashEntry[] entries = await _db.HashGetAllAsync($"player:{playerName}");

        // Dictionary로 변환해서 필드명으로 접근
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);

        int playerId    = (int)dict["player_id"];
        int winScore    = (int)dict["win_score"];
        float winRate   = float.Parse(dict["win_rate"]);
        PlayerRank rank = (PlayerRank)(int)dict["player_rank"];

        Console.WriteLine($"[DbManager] SearchPlayerFromRedis: player_id={playerId} 데이터 반환");
        return new Redis_players(playerId, winScore, winRate, rank);
    }
    

    // DB에서 가져온 players 데이터를 Redis Hash로 캐싱 (TTL 1시간)
    private async Task CachingPlayerToRedis(DB_players? playerDbData)
    {
        await _db?.HashSetAsync($"player:{playerDbData?.Player_name}", new HashEntry[]
            {
                new("player_id",   playerDbData.Player_id),
                new("win_score",   playerDbData.Win_score),
                new("win_rate",    playerDbData.Win_rate.ToString()),
                new("player_rank", (int)playerDbData.Player_rank)
            }
        );
        await _db?.KeyExpireAsync($"player:{playerDbData?.Player_name}", TimeSpan.FromHours(1));
        Console.WriteLine($"[DbManager] CachingPlayerToRedis: {playerDbData?.Player_name} Redis 캐싱 완료 (TTL 1시간)");
    }

    #endregion



    public void Dispose()
    {
        _dataSource?.Dispose();
        _redis?.Dispose();
        Console.WriteLine("[DbManager] Dispose: DB 및 Redis 연결 해제 완료");
    }
}
