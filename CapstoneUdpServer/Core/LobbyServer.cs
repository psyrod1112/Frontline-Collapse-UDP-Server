using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using CapstoneUdpServer.Data;
using CapstoneUdpServer.Network;
using static CapstoneUdpServer.Network.JsonOpts;

namespace CapstoneUdpServer.Core;

public class LobbyServer : IDisposable
{
    private readonly Socket                                  _socket;
    private readonly PlayerStore                             _store;
    private readonly ConcurrentDictionary<int, InGameData>  _inGameDataList;
    private readonly ConcurrentDictionary<int, RoomData>    _roomLists = new();
    private readonly DbManager                              _dbManager;

    private int _nextRoomId;
    private int _currentPlayerCounts;

    public LobbyServer(
        Socket socket,
        PlayerStore store,
        ConcurrentDictionary<int, InGameData> inGameDataList,
        DbManager dbManager)
    {
        _socket         = socket;
        _store          = store;
        _inGameDataList = inGameDataList;
        _dbManager      = dbManager;
    }

    // ─────────────────────────────────────────────────────────────
    #region 패킷 라우팅

    public void ProcessPacket(byte[] buffer, int bufferSize, IPEndPoint clientEp)
    {
        string      jsonData = Encoding.UTF8.GetString(buffer, 0, bufferSize);
        BasePacket? header   = JsonSerializer.Deserialize<BasePacket>(jsonData);

        Console.WriteLine($"[LobbyServer] 수신: {jsonData}");

        switch (header?.Type)
        {
            case LobbyPacketType.Connection:
                _ = HandleConnection(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.Disconnection:
                HandleDisconnection(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.CreateRequest:
                HandleCreateRequest(JsonSerializer.Deserialize<RoomPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.AddPlayerRequest:
                HandleAddPlayerRequest(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.EnterRequest:
                HandleEnterRequest(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.ExitRequest:
                HandleExitRequest(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.ShowGamelogsRequest:
                HandleShowGamelogsRequest(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.GameStart:
                HandleGameStart(JsonSerializer.Deserialize<RoomPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.GameReady:
                HandleGameReady(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            case LobbyPacketType.GameOver:
                _ = HandleGameOver(JsonSerializer.Deserialize<GamelogPacket>(jsonData), clientEp);
                break;
            //TODO: 게임오버는 서버 내부 탐지로 전환 예정 → InGameServer.HandleServerGameOver()
            case LobbyPacketType.Heartbeat:
                HandleHeartbeat(JsonSerializer.Deserialize<PlayerPacket>(jsonData), clientEp);
                break;
            default:
                Console.WriteLine("[LobbyServer] 알 수 없는 패킷 타입");
                break;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 핸들러

    private async Task HandleConnection(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (_currentPlayerCounts > 100)        { Console.WriteLine("[LobbyServer] 서버 인원 초과"); return; }
        if (packet == null)                    { Console.WriteLine("[LobbyServer] HandleConnection: 패킷 null"); return; }

        Redis_playerRankInfo? data = await _dbManager.SearchPlayerFromDB(packet.PlayerName);
        if (data == null) return;

        // 로비 pool에 추가 (연결은 이미 수립된 상태)
        _store.AddToLobby(data.Player_id, new PlayerData(packet.PlayerName, data.Player_id, clientEp));

        Send(new PlayerPacket
        {
            Type = LobbyPacketType.Spawn, PlayerId = data.Player_id,
            PlayerName = packet.PlayerName, WinScore = data.Win_score,
            WinRate = data.Win_rate, PlayerRank = data.Player_rank,
            LastUpdateTime = DateTime.UtcNow.ToString("o")
        }, clientEp);

        SendRoomList(packet, clientEp);
        Interlocked.Increment(ref _currentPlayerCounts);
        Console.WriteLine($"[LobbyServer] {packet.PlayerName} 로비 pool 입장");
    }

    private void HandleDisconnection(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;

        if (_roomLists.TryGetValue(packet.RelatedRoomId, out var roomData))
            HandleExitRequest(packet, clientEp);

        // 어느 pool에 있든 제거
        if (_store.Remove(packet.PlayerId))
            Console.WriteLine($"[LobbyServer] {packet.PlayerName} pool에서 제거");

        Send(new PlayerPacket { Type = LobbyPacketType.Despawn, LastUpdateTime = DateTime.UtcNow.ToString("o") }, clientEp);
    }

    private void HandleCreateRequest(RoomPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null || _roomLists.Count > 20) return;
        if (!_store.TryGetLobby(packet.OwnerId, out var playerData)) return;

        int roomId = Interlocked.Increment(ref _nextRoomId);
        var newRoom = new RoomData(packet.OwnerId, roomId, packet.RoomName,
                                   packet.RoomPlayerLimit, packet.CurrentRoomPlayers);
        playerData.PlayerWhereRoom(roomId);
        newRoom.AddPlayer(playerData);
        _roomLists[roomId] = newRoom;

        BroadcastCreateRoomPacket(new RoomPacket
        {
            Type = LobbyPacketType.CreateRoom, OwnerId = newRoom.OwnerId, RoomId = newRoom.RoomId,
            RoomName = newRoom.RoomName, RoomPlayerLimit = newRoom.RoomPlayerLimit,
            CurrentRoomPlayers = newRoom.CurrentRoomPlayers, LastUpdateTime = DateTime.UtcNow.ToString("o")
        });

        Send(new PlayerPacket
        {
            Type = LobbyPacketType.EnterRoom,
            PlayerId = playerData.PlayerId, RelatedRoomId = playerData.RelatedRoomId,
            LastUpdateTime = DateTime.UtcNow.ToString("o")
        }, clientEp);
    }

    private void HandleEnterRequest(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_store.TryGetLobby(packet.PlayerId, out var playerData))
        {
            Console.WriteLine("[LobbyServer] HandleEnterRequest: 로비에 플레이어 없음");
            return;
        }
        if (!_roomLists.TryGetValue(packet.RelatedRoomId, out var roomData))
        {
            Console.WriteLine("[LobbyServer] HandleEnterRequest: 룸 없음");
            return;
        }
        if (roomData.CurrentRoomPlayers >= roomData.RoomPlayerLimit)
        {
            Console.WriteLine("[LobbyServer] HandleEnterRequest: 룸 인원 초과");
            return;
        }

        playerData.PlayerWhereRoom(roomData.RoomId);
        roomData.AddPlayer(playerData);

        Send(new PlayerPacket
        {
            Type = LobbyPacketType.EnterRoom,
            PlayerId = playerData.PlayerId, RelatedRoomId = playerData.RelatedRoomId,
            LastUpdateTime = DateTime.UtcNow.ToString("o")
        }, clientEp);
        BroadcastUpdateRoomPacket(packet.RelatedRoomId);
    }

    private async void HandleAddPlayerRequest(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_store.TryGetLobby(packet.PlayerId, out var playerData)) return;

        Redis_playerRankInfo? info = await _dbManager.SearchPlayerFromRedis(playerData.PlayerName);
        if (info == null) return;

        BroadcastAddPlayer(new PlayerPacket
        {
            Type = LobbyPacketType.AddPlayerRoom,
            PlayerId = playerData.PlayerId, PlayerName = playerData.PlayerName,
            WinScore = info.Win_score, WinRate = info.Win_rate, PlayerRank = info.Player_rank,
            RelatedRoomId = playerData.RelatedRoomId, LastUpdateTime = DateTime.UtcNow.ToString("o")
        });
        SendAddPlayerPacket(playerData, clientEp);
    }

    private void HandleExitRequest(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_roomLists.TryGetValue(packet.RelatedRoomId, out var roomData)) return;
        if (!roomData.InRoomPlayers.TryGetValue(packet.PlayerId, out var playerData)) return;

        if (roomData.OwnerId == playerData.PlayerId)
        {
            BroadcastDestroyRoom(new RoomPacket
            {
                Type = LobbyPacketType.DestroyRoom,
                RoomId = roomData.RoomId, LastUpdateTime = DateTime.UtcNow.ToString("o")
            }, roomData, false);
        }
        else
        {
            playerData.PlayerWhereRoom(0);
            Send(new PlayerPacket { Type = LobbyPacketType.ExitRoom, LastUpdateTime = DateTime.UtcNow.ToString("o") }, clientEp);
            BroadcastRemovePlayerRoomPacket(packet, clientEp);
            roomData.RemovePlayer(playerData.PlayerId);
            BroadcastUpdateRoomPacket(roomData.RoomId);
        }
    }

    private async void HandleShowGamelogsRequest(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_store.TryGetLobby(packet.PlayerId, out var playerData)) return;

        try
        {
            DB_players? info = await _dbManager.SelectPlayerFromDataSource(playerData.PlayerName);
            Send(new PlayerPacket
            {
                Type = LobbyPacketType.ShowPlayerInfo,
                PlayerId = info.Player_id, 
                PlayerName = playerData.PlayerName,
                WinScore = info.Win_score, 
                WinRate = info.Win_rate, 
                PlayerRank = info.Player_rank,
                RelatedRoomId = playerData.RelatedRoomId, 
                LastUpdateTime = DateTime.UtcNow.ToString("o")
            }, clientEp);

            var logs = await _dbManager.ShowGamelogsFromDataSource(playerData.PlayerName);
            foreach (var log in logs)
            {
                Send(new GamelogPacket
                {
                    Type = LobbyPacketType.ShowGamelogsResponse,
                    MyName = log.Player_Name, 
                    MyRank = log.Player_Rank,
                    EnemyName = log.Enemy_Name, 
                    EnemyRank = log.Enemy_rank,
                    GameResult = log.Game_result, 
                    GameOverTime = log.Created_at,
                    LastUpdateTime = DateTime.UtcNow.ToString("o")
                }, clientEp);
            }
        }
        catch (Exception e) { Console.WriteLine("[LobbyServer] HandleShowGamelogsRequest 오류: " + e.Message); }
    }

    private void HandleGameStart(RoomPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_store.TryGetLobby(packet.OwnerId, out var playerData)) return;
        if (!_roomLists.TryGetValue(packet.RoomId, out var roomData)) return;
        if (roomData.OwnerId != playerData.PlayerId) return;
        if (roomData.CurrentRoomPlayers != roomData.RoomPlayerLimit) return;

        SendGameStartPacket(new RoomPacket
        {
            Type = LobbyPacketType.GameStart, 
            RoomId = roomData.RoomId,
            LastUpdateTime = DateTime.UtcNow.ToString("o")
        }, roomData);
    }

    private void HandleGameReady(PlayerPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_store.TryGetLobby(packet.PlayerId, out var playerData)) return;
        if (!_roomLists.TryGetValue(packet.RelatedRoomId, out var roomData)) return;

        playerData.IsGameReady = true;
        foreach (var p in roomData.InRoomPlayers.Values)
            if (!p.IsGameReady) return;
        

        // 막타 치는 놈이 앞으로 아래 코드들 실행.
        // 로비 pool → 인게임 pool 이동 (연결 유지, 데이터만 이동)
        foreach (var p in roomData.InRoomPlayers.Values)
        {
            _store.MoveToInGame(p.PlayerId);
            Console.WriteLine($"[LobbyServer] {p.PlayerName} 로비 → 인게임 pool 이동");
        }

        var newInGameData = new InGameData();
        int startPosX = -10;
        foreach (var p in roomData.InRoomPlayers.Values)
        {
            var unit = new PlayerUnitData(p, roomData.RoomId);
            newInGameData.PlayerUnitDataMap[p.PlayerId] = unit;
            BroadcastSpawnPlayerUnit(new PlayerUnitPacket
            {
                Type2 = InGamePacketType.SpawnPlayerUnit,
                PlayerId = unit.PlayerId, 
                FieldId = unit.FieldId,
                CurrentHp = unit.CurrentHp, 
                Position = unit.Position + new Vector3(startPosX, 5, 0),
                Rotation = unit.Rotation, 
                PrefabIndex = unit.WeaponPrefabIndex,
                LastUpdateTime = DateTime.UtcNow.ToString("o")
            }, roomData);
            startPosX += 10;
        }
        _inGameDataList[roomData.RoomId] = newInGameData;

        BroadcastDestroyRoom(new RoomPacket
        {
            Type = LobbyPacketType.DestroyRoom, RoomId = roomData.RoomId,
            LastUpdateTime = DateTime.UtcNow.ToString("o")
        }, roomData, true);
    }

    /// <summary>
    /// 클라이언트 GameOver 패킷 수신 시 처리 (테스트용).
    /// 패킷 발신자(MyId)와 상대방 모두 DB 처리 → pool 복귀 → 로비 재연결.
    /// </summary>
    private async Task HandleGameOver(GamelogPacket? packet, IPEndPoint clientEp)
    {
        if (packet == null) return;
        if (!_store.TryGetInGame(packet.MyId, out var myData))
        {
            Console.WriteLine("[LobbyServer] HandleGameOver: 인게임 pool에 플레이어 없음");
            return;
        }
        if (!_inGameDataList.TryGetValue(myData.RelatedRoomId, out var inGameData)) return;

        var players = inGameData.PlayerUnitDataMap.Values.ToList();
        if (players.Count < 2)
        {
            Console.WriteLine("[LobbyServer] HandleGameOver: 플레이어 수 부족");
            return;
        }

        // 게임오버 전 랭크 미리 조회 (gamelog 상대 랭크 기록용)
        var rankCache = new Dictionary<int, PlayerRank>();
        foreach (var p in players)
        {
            Redis_playerRankInfo? info = await _dbManager.SearchPlayerFromRedis(p.PlayerName);
            rankCache[p.PlayerId] = info != null
                ? DB_PlayerGameoverInfo.ComputeRank(info.Win_score)
                : PlayerRank.None;
        }

        // 양쪽 모두 DB 처리 → pool 복귀 → 로비 재연결
        foreach (var p in players)
        {
            // 패킷 발신자의 GameResult 기준으로 승패 결정
            bool isWinner  = p.PlayerId == packet.MyId ? packet.GameResult : !packet.GameResult;
            var  enemy     = players.First(x => x.PlayerId != p.PlayerId);
            PlayerRank enemyRank = rankCache.GetValueOrDefault(enemy.PlayerId, PlayerRank.None);

            try
            {
                await _dbManager.ProcessGameOverAsync(
                    p.PlayerId, p.PlayerName,
                    enemy.PlayerName, enemyRank,
                    isWinner);

                _store.MoveToLobby(p.PlayerId);
                Console.WriteLine($"[LobbyServer] {p.PlayerName} 인게임 → 로비 pool 복귀");

                Interlocked.Decrement(ref _currentPlayerCounts);
                await HandleConnection(
                    new PlayerPacket { PlayerName = p.PlayerName },
                    (IPEndPoint)p.IpEndPoint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[LobbyServer] HandleGameOver {p.PlayerName} 처리 오류: {e.Message}");
            }
        }

        _inGameDataList.TryRemove(myData.RelatedRoomId, out _);
        Console.WriteLine($"[LobbyServer] fieldId={myData.RelatedRoomId} 인게임 데이터 제거 완료");
    }

    /*
    // TODO: 서버 내부 탐지 전환 시 아래 메서드 사용
    // InGameServer에서 HP 0 탐지 후 호출: await _lobbyServer.HandleServerGameOver(fieldId, winnerPlayerId)
    public async Task HandleServerGameOver(int fieldId, int winnerPlayerId)
    {
        if (!_inGameDataList.TryGetValue(fieldId, out var inGameData)) return;
        var players = inGameData.PlayerUnitDataMap.Values.ToList();
        if (players.Count < 2) return;

        var rankCache = new Dictionary<int, PlayerRank>();
        foreach (var p in players)
        {
            Redis_playerRankInfo? info = await _dbManager.SearchPlayerFromRedis(p.PlayerName);
            rankCache[p.PlayerId] = info != null
                ? DB_PlayerGameoverInfo.ComputeRank(info.Win_score)
                : PlayerRank.None;
        }
        foreach (var p in players)
        {
            bool isWinner = p.PlayerId == winnerPlayerId;
            var  enemy    = players.First(x => x.PlayerId != p.PlayerId);
            PlayerRank enemyRank = rankCache.GetValueOrDefault(enemy.PlayerId, PlayerRank.None);
            await _dbManager.ProcessGameOverAsync(p.PlayerId, p.PlayerName, enemy.PlayerName, enemyRank, isWinner);
            _store.MoveToLobby(p.PlayerId);
            Interlocked.Decrement(ref _currentPlayerCounts);
            await HandleConnection(new PlayerPacket { PlayerName = p.PlayerName }, (IPEndPoint)p.IpEndPoint);
        }
        _inGameDataList.TryRemove(fieldId, out _);
    }
    */

    private void HandleHeartbeat(PlayerPacket? packet, IPEndPoint clientEp)
    {
        // TODO: 하트비트 처리
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 송신

    private void Send<T>(T packet, IPEndPoint clientEp)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);
        _socket.SendTo(buf, clientEp);
        Console.WriteLine("[LobbyServer] 전송: " + json);
    }

    private void SendRoomList(PlayerPacket packet, IPEndPoint clientEp)
    {
        int count = 0;
        foreach (var room in _roomLists.Values)
        {
            Send(new RoomPacket
            {
                Type = LobbyPacketType.CreateRoom, OwnerId = room.OwnerId, RoomId = room.RoomId,
                RoomName = room.RoomName ?? $"GameRoom{room.RoomId}",
                RoomPlayerLimit = room.RoomPlayerLimit, CurrentRoomPlayers = room.CurrentRoomPlayers,
                LastUpdateTime = DateTime.UtcNow.ToString("o")
            }, clientEp);
            count++;
        }
        Console.WriteLine($"[LobbyServer] {count}개 룸 정보를 {packet.PlayerName}에게 전송");
    }

    private async void SendAddPlayerPacket(PlayerData me, IPEndPoint clientEp)
    {
        if (!_roomLists.TryGetValue(me.RelatedRoomId, out var roomData)) return;
        foreach (var p in roomData.InRoomPlayers.Values)
        {
            Redis_playerRankInfo info = await _dbManager.SearchPlayerFromRedis(p.PlayerName);
            Send(new PlayerPacket
            {
                Type = LobbyPacketType.AddPlayerRoom,
                PlayerId = p.PlayerId, 
                PlayerName = p.PlayerName,
                WinScore = info.Win_score, 
                WinRate = info.Win_rate, PlayerRank = info.Player_rank,
                RelatedRoomId = me.RelatedRoomId, 
                LastUpdateTime = DateTime.UtcNow.ToString("o")
            }, clientEp);
        }
    }

    private void SendGameStartPacket(RoomPacket packet, RoomData roomData)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);
        foreach (var p in roomData.InRoomPlayers.Values)
            _socket.SendTo(buf, p.ClientEp);
        Console.WriteLine($"[LobbyServer] {roomData.RoomId}번 룸에 GameStart 전송");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 브로드캐스트

    private void BroadcastCreateRoomPacket(RoomPacket packet)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);
        // LobbyPlayers만 순회 → 인게임 중인 플레이어 자동 제외
        foreach (var p in _store.LobbyPlayers)
            _socket.SendTo(buf, p.ClientEp);
    }

    private void BroadcastAddPlayer(PlayerPacket packet)
    {
        if (!_roomLists.TryGetValue(packet.RelatedRoomId, out var roomData)) return;
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);
        foreach (var p in roomData.InRoomPlayers.Values)
        {
            if (p.PlayerId != packet.PlayerId)
                _socket.SendTo(buf, p.ClientEp);
        }
    }

    private void BroadcastUpdateRoomPacket(int roomId)
    {
        if (!_roomLists.TryGetValue(roomId, out var roomData)) return;
        string json = JsonSerializer.Serialize(new RoomPacket
        {
            Type = LobbyPacketType.RoomUpdate, RoomId = roomId,
            RoomName = roomData.RoomName, RoomPlayerLimit = roomData.RoomPlayerLimit,
            CurrentRoomPlayers = roomData.CurrentRoomPlayers, LastUpdateTime = DateTime.UtcNow.ToString("o")
        });
        byte[] buf = Encoding.UTF8.GetBytes(json);
        // LobbyPlayers만 순회
        foreach (var p in _store.LobbyPlayers)
            _socket.SendTo(buf, p.ClientEp);
    }

    private void BroadcastRemovePlayerRoomPacket(PlayerPacket packet, IPEndPoint excludeEp)
    {
        if (!_roomLists.TryGetValue(packet.RelatedRoomId, out var roomData)) return;
        if (!roomData.InRoomPlayers.TryGetValue(packet.PlayerId, out var playerData)) return;

        string json = JsonSerializer.Serialize(new PlayerPacket
        {
            Type = LobbyPacketType.RemovePlayerRoom,
            PlayerId = playerData.PlayerId, LastUpdateTime = DateTime.UtcNow.ToString("o")
        });
        byte[] buf = Encoding.UTF8.GetBytes(json);
        foreach (var p in roomData.InRoomPlayers.Values)
        {
            if (!p.ClientEp.Equals(excludeEp))
                _socket.SendTo(buf, p.ClientEp);
        }
    }

    private void BroadcastDestroyRoom(RoomPacket packet, RoomData roomData, bool isGameStart)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);

        foreach (var p in roomData.InRoomPlayers.Values)
            p.PlayerWhereRoom(0);

        if (isGameStart)
        {
            // 게임 시작: 로비 플레이어에게만 전송 (인게임 pool은 이미 이동 완료)
            foreach (var p in _store.LobbyPlayers)
                _socket.SendTo(buf, p.ClientEp);
        }
        else
        {
            // 방 해산: 모든 로비 플레이어에게 전송
            foreach (var p in _store.LobbyPlayers)
                _socket.SendTo(buf, p.ClientEp);
        }

        if (_roomLists.TryRemove(roomData.RoomId, out _))
            Console.WriteLine($"[LobbyServer] {roomData.RoomName} 방 제거");
    }

    private void BroadcastSpawnPlayerUnit(PlayerUnitPacket packet, RoomData roomData)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);
        foreach (var p in roomData.InRoomPlayers.Values)
            _socket.SendTo(buf, p.ClientEp);
    }

    #endregion

    public void Dispose()
    {
        _dbManager?.Dispose();
        _roomLists.Clear();
    }
}
