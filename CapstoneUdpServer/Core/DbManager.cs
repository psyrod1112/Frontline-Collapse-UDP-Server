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
    public DateTime Created_at { get; set; }

    // win_rate, player_rank는 DB에 저장하지 않고 win_counts/lose_counts/win_score로 계산
    public DB_players(int player_id, string player_name, int win_score, int win_counts, int lose_counts,
        DateTime created_at)
    {
        Player_id = player_id;
        Player_name = player_name;
        Win_score = win_score;
        int total = win_counts + lose_counts;
        Win_rate = total > 0 ? (float)100.0 * win_counts / total : 0f;
        Player_rank = DB_PlayerGameoverInfo.ComputeRank(win_score);
        Created_at = created_at;
    }
}

public class DB_PlayerGameoverInfo
{
    public int Win_score { get; set; }
    public int Win_counts { get; set; }
    public int Lose_counts { get; set; }

    public DB_PlayerGameoverInfo(int win_score, int win_counts, int lose_counts)
    {
        Win_score = win_score;
        Win_counts = win_counts;
        Lose_counts = lose_counts;
    }

    public float UpdateInfo(int update_win_score, bool isWin)
    {
        Win_score += update_win_score;
        if (Win_score < 0) Win_score = 0;
        
        if (isWin) Win_counts += 1;
        else Lose_counts += 1;

        int total = Win_counts + Lose_counts;
        return total > 0 ? (float)100.0 * Win_counts / total : 0f;
    }

    public static PlayerRank ComputeRank(int winScore)
    {
        if (winScore >= 2400) return PlayerRank.General;
        if (winScore >= 1000) return PlayerRank.Colonel;
        if (winScore >= 500)  return PlayerRank.Captain;
        if (winScore >= 0)    return PlayerRank.Sergeant;
        return PlayerRank.None;
    }

    public PlayerRank UpdateRank() => ComputeRank(Win_score);
}

public class DB_playersNgamelogsJoined
{
    public string Player_Name { get; set; }
    public PlayerRank Player_Rank { get; set; }
    public string Enemy_Name { get; set; }
    public PlayerRank Enemy_rank { get; set; }
    
    public bool Game_result { get; set; }
    public DateTime Created_at { get; set; }

    public DB_playersNgamelogsJoined(string playerName, int playerRank, string enemyName, int enemyRank,
        bool gameResult, DateTime created_at)
    {
        Player_Name = playerName;
        Player_Rank = (PlayerRank)playerRank;
        Enemy_Name = enemyName;
        Enemy_rank = (PlayerRank)enemyRank;
        Game_result = gameResult;
        Created_at = created_at;
    }
    
    
}

public class Redis_playerRankInfo
{
    public int Player_id { get; set; }
    public int Win_score { get; set; }
    public float Win_rate { get; set; }
    public PlayerRank Player_rank { get; set; }

    public Redis_playerRankInfo(int player_id, int win_score, float win_rate, PlayerRank player_rank)
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
        "Host=127.0.0.1;Port=5432;Database=frontline_collapse;Username=postgres;Password=tkddbs321!";

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
    public async Task<Redis_playerRankInfo?> SearchPlayerFromDB(string playerName)
    {
        try
        {
            if (await _db.KeyExistsAsync($"player:{playerName}"))
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
                "SELECT player_id, player_name, win_score, win_counts, lose_counts, created_at FROM players WHERE player_name = @name");
            cmd.Parameters.AddWithValue("name", playerName);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine($"[DbManager] SelectPlayerFromDataSource: {playerName} DB 조회 성공");
                return new DB_players(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2),
                    reader.GetInt32(3), reader.GetInt32(4), reader.GetDateTime(5));
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

    public async Task<DB_PlayerGameoverInfo?> SelectGameOverInfoFromDataSource(int playerId)
    {
        try
        {
            await using var cmd = _dataSource.CreateCommand(
                "SELECT win_score, win_counts, lose_counts FROM players WHERE player_id = @playerId");
            cmd.Parameters.AddWithValue("playerId", playerId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine("[DbManager] SelectGameOverInfoFromDataSource 유저 랭크 정보 불러옴!");
                return new DB_PlayerGameoverInfo(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            }
            Console.WriteLine("[DbManager] SelectGameOverInfoFromDataSource : 오류발생! ");
            return null;
            
        }
        catch (Exception e)
        {
            Console.WriteLine("[DbManager] SelectGameOverInfoFromDataSource : 오류발생! " + e.Message);
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

    public async Task<List<DB_playersNgamelogsJoined>> ShowGamelogsFromDataSource(string playerName)
    {
        var selectedLogList = new List<DB_playersNgamelogsJoined>();
        
        await using var cmd = _dataSource?.CreateCommand(
            "SELECT player_name, player_rank, enemy_name, enemy_rank, game_result, played_at " +
            "FROM gamelogs WHERE player_name = @player_name ORDER BY played_at DESC LIMIT 10");

        cmd.Parameters.AddWithValue("player_name", playerName);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            DB_playersNgamelogsJoined? gamelog = new DB_playersNgamelogsJoined(reader.GetString(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetBoolean(4), reader.GetDateTime(5));
            
            selectedLogList.Add(gamelog);
        }
        Console.WriteLine($"[DbManager] ShowGamelogsFromDataSource: playerName={playerName} 조회 완료 ({selectedLogList.Count}건)");
        return selectedLogList;
    }

    public async Task UpdatePlayerAndInsertGamelog(int playerId, DB_PlayerGameoverInfo info,
        string playerName, PlayerRank playerRank, string enemyName, PlayerRank enemyRank, bool gameResult)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            // players 업데이트
            await using var updateCmd = connection.CreateCommand();
            updateCmd.Transaction = transaction;
            updateCmd.CommandText =
                "UPDATE players SET win_score = @win_score, win_counts = @win_counts, lose_counts = @lose_counts WHERE player_id = @player_id";
            updateCmd.Parameters.AddWithValue("win_score", info.Win_score);
            updateCmd.Parameters.AddWithValue("win_counts", info.Win_counts);
            updateCmd.Parameters.AddWithValue("lose_counts", info.Lose_counts);
            updateCmd.Parameters.AddWithValue("player_id", playerId);
            await updateCmd.ExecuteNonQueryAsync();

            // gamelogs INSERT
            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText =
                "INSERT INTO gamelogs (player_name, player_rank, enemy_name, enemy_rank, game_result) " +
                "VALUES (@player_name, @player_rank, @enemy_name, @enemy_rank, @game_result)";
            insertCmd.Parameters.AddWithValue("player_name", playerName);
            insertCmd.Parameters.AddWithValue("player_rank", (int)playerRank);
            insertCmd.Parameters.AddWithValue("enemy_name", enemyName);
            insertCmd.Parameters.AddWithValue("enemy_rank", (int)enemyRank);
            insertCmd.Parameters.AddWithValue("game_result", gameResult);
            await insertCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            Console.WriteLine($"[DbManager] UpdatePlayerAndInsertGamelog: {playerName} vs {enemyName} 트랜잭션 커밋 완료");
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            Console.WriteLine("[DbManager] UpdatePlayerAndInsertGamelog: 트랜잭션 롤백 " + e.Message);
        }
    }

    // Redis Hash에서 플레이어 데이터 조회 후 Redis_players로 변환
    public async Task<Redis_playerRankInfo?> SearchPlayerFromRedis(string playerName)
    {
        HashEntry[] entries = await _db.HashGetAllAsync($"player:{playerName}");

        // Dictionary로 변환해서 필드명으로 접근
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);

        int playerId    = (int)dict["player_id"];
        int winScore    = (int)dict["win_score"];
        float winRate   = float.Parse(dict["win_rate"]);
        PlayerRank rank = (PlayerRank)(int)dict["player_rank"];

        Console.WriteLine($"[DbManager] SearchPlayerFromRedis: player_id={playerId} 데이터 반환");
        return new Redis_playerRankInfo(playerId, winScore, winRate, rank);
    }

    public async Task DeletePlayerCacheFromRedis(string? playerName)
    {
        await _db.KeyDeleteAsync($"player:{playerName}");
        await _db.KeyDeleteAsync($"session:{playerName}");
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


    /// <summary>
    /// 게임 오버 시 플레이어 한 명의 DB/Redis 처리를 한 번에 수행.
    /// 1) SelectGameOverInfo → 2) ComputeRank → 3) UpdateInfo(±20) →
    /// 4) UpdatePlayerAndInsertGamelog → 5) DeletePlayerCacheFromRedis
    /// </summary>
    public async Task ProcessGameOverAsync(
        int playerId, string playerName,
        string enemyName, PlayerRank enemyRank,
        bool isWinner)
    {
        DB_PlayerGameoverInfo? info = await SelectGameOverInfoFromDataSource(playerId);
        if (info == null)
        {
            Console.WriteLine($"[DbManager] ProcessGameOverAsync: {playerName} DB 조회 실패");
            return;
        }

        PlayerRank myRank = DB_PlayerGameoverInfo.ComputeRank(info.Win_score);
        info.UpdateInfo(isWinner ? 20 : -20, isWinner);

        await UpdatePlayerAndInsertGamelog(
            playerId, info,
            playerName, myRank,
            enemyName, enemyRank,
            isWinner);

        await DeletePlayerCacheFromRedis(playerName);
        Console.WriteLine($"[DbManager] ProcessGameOverAsync: {playerName} 처리 완료 (isWinner={isWinner})");
    }

    public void Dispose()
    {
        _dataSource?.Dispose();
        _redis?.Dispose();
        Console.WriteLine("[DbManager] Dispose: DB 및 Redis 연결 해제 완료");
    }
}
