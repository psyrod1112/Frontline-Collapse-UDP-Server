
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapstoneUdpServer.Data;
using CapstoneUdpServer.NetworkStream;

namespace CapstoneUdpServer.Network;

public class UdpServer:IDisposable
{
    private Socket _socket;
    private ServerConfig _config;
    private bool _isRunning;
    private Thread[] _workerThreads;
    private const int WORKER_THREAD_COUNT = 10;
    
    private JobQueue _jobQueue = new JobQueue();
    private ConcurrentDictionary<int, PlayerData> _players;
    private ConcurrentDictionary<int, RoomData> _roomLists;
    
    private int _nextPlayerId;
    private int _nextRoomId;
    private bool _disposed;
    public bool IsRunning => _isRunning;

    #region 생성자

    public UdpServer(ServerConfig config)
    {
        _config = config;
        _isRunning = false;
        _players = new ConcurrentDictionary<int, PlayerData>();
        _roomLists = new ConcurrentDictionary<int, RoomData>();
        
        _nextPlayerId = 0;
        _nextRoomId = 0;
        _disposed = false;
    }

    #endregion


    #region 시작 메서드

    public void Initialize()
    {
        //소켓 초기화
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //소켓 옵션 설정
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        //소켓 바인딩
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(_config.ServerIp), _config.Port);
        _socket.Bind(serverEp);
        
        Console.WriteLine("[서버] Initialize: 소켓 초기 설정 완료");

    }

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            Console.WriteLine("[서버] StartAsync: 이미 연결되어 있습니다...");
            return;
        }
        
        _isRunning = true;

        StartThreadWork();

        await Task.Run(ReceiveLoopAsync);
    }

    #endregion
    
    

    #region 스레드 메서드

    private void StartThreadWork()
    {
        _workerThreads = new Thread[WORKER_THREAD_COUNT];
        for (int i = 0; i < WORKER_THREAD_COUNT; i++)
        {
            int threadId = i;
            _workerThreads[i] = new Thread(() => ThreadJobProcessLoop(threadId));
            _workerThreads[i].IsBackground = true;
            _workerThreads[i].Start();
            
        }
        Console.WriteLine($"[서버] StartThreadWork: 워크 스레드 {WORKER_THREAD_COUNT}개가 실행 중입니다.");
    }

    private void ThreadJobProcessLoop(int threadId)
    {
        int processThreadCount = 0;

        while (_isRunning)
        {
            _jobQueue.Dequeue().Execute();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[서버] {threadId}번 스레드 패킷 처리 (총 처리량 : {++processThreadCount})");
            Console.ResetColor();
        }
        
    }

    #endregion
    
    
    private void ReceiveLoopAsync()
    {
        
        //버퍼 빌려오기
        byte[] buffer = ArrayPool<byte>.Shared.Rent(_config.BufferSize);
        EndPoint clientEp = new IPEndPoint(IPAddress.Any, _config.Port);
        //버퍼 받아오기
        try
        {
            while (_isRunning)
            {
                int receivedLength = _socket.ReceiveFrom(buffer, ref clientEp);
                if (receivedLength > 0)
                {
                    byte[] packetBuffer = new byte[receivedLength];
                    Array.Copy(buffer, packetBuffer, receivedLength);
                    IJob job = new PacketJob(this, packetBuffer, receivedLength, (IPEndPoint)clientEp);
                    _jobQueue.Enqueue(job);
                    Console.WriteLine($"[서버] ReceiveLoopAsync: 받은 패킷 Enqueue함");
                }
            }
            
        }
        catch (SocketException se)
        {
            Console.WriteLine("[서버] ReceiveLoopAsync: 소켓 오류 발생 " + se.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine("[서버] ReceiveLoopAsync: 수신 오류 발생 " + e.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Dispose();
        }
        
        Console.WriteLine("[서버] ReceiveLoopAsync: 수신 루프 종료");
    }

    public void ProcessPacket(byte[] buffer, int bufferSize, IPEndPoint clientEp)
    {
        
        //BasePacket 헤더 이용
        string jsonData = Encoding.UTF8.GetString(buffer, 0, bufferSize);
        BasePacket? header = JsonSerializer.Deserialize<BasePacket>(jsonData);
        
        Console.WriteLine(jsonData);
        
        //예외상황?

        switch (header?.Type)
        {
            case PacketType.Connection:
                PlayerPacket? connectionPacket = JsonSerializer.Deserialize<PlayerPacket>(jsonData);
                HandleConnection(connectionPacket, clientEp);
                break;
            case PacketType.Disconnection:
                PlayerPacket? disconnectionPacket = JsonSerializer.Deserialize<PlayerPacket>(jsonData);
                HandleDisconnection(disconnectionPacket, clientEp);
                break;
            case PacketType.CreateRequest:
                RoomPacket? roomPacket = JsonSerializer.Deserialize<RoomPacket>(jsonData);
                HandleCreateRequest(roomPacket, clientEp);
                break;
            case PacketType.AddPlayerRequest:
                PlayerPacket? addPlayerPacket = JsonSerializer.Deserialize<PlayerPacket>(jsonData);
                HandleAddPlayerRequest(addPlayerPacket, clientEp);
                break;
            case PacketType.EnterRequest:
                PlayerPacket? enterRequestPacket = JsonSerializer.Deserialize<PlayerPacket>(jsonData);
                HandleEnterRequest(enterRequestPacket, clientEp);
                break;
            case PacketType.ExitRequest:
                PlayerPacket? exitRequestPacket = JsonSerializer.Deserialize<PlayerPacket>(jsonData);
                HandleExitRequest(exitRequestPacket, clientEp);
                break;
            case PacketType.GameStart:
                RoomPacket? gameStartPacket = JsonSerializer.Deserialize<RoomPacket>(jsonData);
                HandleGameStart(gameStartPacket, clientEp);
                break;
            case PacketType.GameOver:
                //HandleGameOverPacket(lobbyPacket, clientEp);
                break;
        }
        
    }
    
    #region 핸들러 메소드

    private void HandleConnection(PlayerPacket? packet, IPEndPoint clientEp)
    {
        //예외상황 처리
        if (!_isRunning)
        {
            Console.WriteLine("[서버] HandleConnectionPacket : 연결되어 있지 않음");
            return;
        }

        if (_players.Count > _config.MaxPlayerCounts)
        {
            Console.WriteLine("[서버] HandleConnectionPacket : 서버 인원 초과!");
            return;
        }

        if (packet == null)
        {
            Console.WriteLine("[서버]  HandleConnectionPacket : Packet is null!");
            return;
        }

        int playerId = Interlocked.Increment(ref _nextPlayerId);
        foreach(var playerData in _players.Values)
        {
            if (playerData.PlayerName.Equals(packet.PlayerName))
            {
                Console.WriteLine("[서버]  HandleConnectionPacket: 이미 한번 접속했던 유저! " + playerData.PlayerName);
                Interlocked.Decrement(ref _nextPlayerId);
            
                SendPlayerSpawn(playerData, clientEp);
        
                SendRoomList(packet, clientEp);
            
                return;
            }
        }
        
        //새 유저 만들기
        PlayerData newPlayerData = new PlayerData(packet.PlayerName, playerId,0 ,0f, PlayerRank.Sergeant, clientEp);
        _players[playerId] = newPlayerData;
        Console.WriteLine($"[서버] HandleConnectionPacket {packet.PlayerName} 플레이어 접속 완료!");

        Console.WriteLine($"packet.PlayerName : {packet.PlayerName}");
        Console.WriteLine($"packetData : {newPlayerData}");
        SendPlayerSpawn(newPlayerData, clientEp);
        
        SendRoomList(packet, clientEp);
        //TryGetValue로 닉네임으로 이미 있는 정보인지 확인
        //새로운 정보면 ConcurrentDict에 추가
        
        //나에게 다른 룸 정보(서버에 있는 리스트) 패킷(RoomUpdate) 전송
        
    }
    
    private void HandleDisconnection(PlayerPacket? disconnectionPacket, IPEndPoint clientEp)
    {
        if (disconnectionPacket == null)
        {
            Console.WriteLine("[서버] HandleDisconnection 패킷이 없습니다!");
            return;
        }
        
        //룸 리스트에 포함되어있는지 확인 -> 지우기 -> HandleExitRequest 실행하기
        if (_roomLists.TryGetValue(disconnectionPacket.RelatedRoomId, out var roomData))
        {
            Console.WriteLine($"[서버] {roomData.RoomId}번 방에서 {disconnectionPacket.PlayerName}를 삭제하였습니다.");
            HandleExitRequest(disconnectionPacket, clientEp);
        }
        
        //플레이어 리스트에 포함되어있는지 확인 -> 지우기 
        if (_players.TryRemove(disconnectionPacket.PlayerId, out var playerData))
        {
            Console.WriteLine($"[서버] {playerData.PlayerName}을 _players에서 삭제하였습니다.");
            
        }
        
        //despawn패킷 보내기 -> 버튼들 다 삭제할 것임. 그리고 TCP, UDP Dispose할 것임.

        SendDespawnPacket(clientEp);
    }


    private void HandleCreateRequest(RoomPacket? roomPacket, IPEndPoint clientEp)
    {
        //예외상황 처리
        if (_roomLists.Count > 20)
        {
            Console.WriteLine("[서버] HandleCreateRequestPacket : 방의 갯수가 20개를 초과합니다!");
            return;
        }
        
        if (!_isRunning)
        {
            Console.WriteLine("[서버] HandleConnectionPacket : 연결되어 있지 않음");
            return;
        }
        
        if (roomPacket == null)
        {
            Console.WriteLine("[서버]  HandleConnectionPacket : Packet is null!");
            return;
        }
        
        
        if (_players.TryGetValue(roomPacket.OwnerId, out PlayerData? playerData))
        {
            //룸 데이터 리스트에 저장
            int roomId = Interlocked.Increment(ref _nextRoomId);

            RoomData newRoomData = new RoomData(roomPacket.OwnerId, roomId,
                roomPacket.RoomName, roomPacket.RoomPlayerLimit, roomPacket.CurrentRoomPlayers);
            
            playerData.PlayerWhereRoom(roomId);
            newRoomData.AddPlayer(playerData);
            _roomLists[roomId] = newRoomData;
            
            // 룸 생성 패킷 브로드캐스팅
            BroadcastCreateRoomPacket(newRoomData);
        
            SendEnterRoomPacket(playerData, clientEp);
        }
    }
    
    private void HandleEnterRequest(PlayerPacket? enterRequestPacket, IPEndPoint clientEp)
    {
        if (enterRequestPacket == null) return;

        if (!_players.TryGetValue(enterRequestPacket.PlayerId, out PlayerData? playerData))
        {
            Console.WriteLine("[서버] HandleEnterRequest: 플레이어를 찾을 수 없음");
            return;
        }

        if (!_roomLists.TryGetValue(enterRequestPacket.RelatedRoomId, out RoomData? roomData))
        {
            Console.WriteLine("[서버] HandleEnterRequest: 룸을 찾을 수 없음");
            return;
        }

        if (roomData.CurrentRoomPlayers >= roomData.RoomPlayerLimit)
        {
            Console.WriteLine("[서버] HandleEnterRequest 룸 인원 초과!");
            return;
        }

        playerData.PlayerWhereRoom(roomData.RoomId);
        roomData.AddPlayer(playerData);

        SendEnterRoomPacket(playerData, clientEp);
        BroadcastUpdateRoomPacket(enterRequestPacket.RelatedRoomId);
    }

    private void HandleAddPlayerRequest(PlayerPacket? addPlayerPacket, IPEndPoint clientEp)
    {
        if (addPlayerPacket == null) return;

        if (!_players.TryGetValue(addPlayerPacket.PlayerId, out PlayerData? playerData))
        {
            Console.WriteLine("[서버] HandleAddPlayerRequest: 플레이어를 찾을 수 없음");
            return;
        }

        // 나를 제외한 방 안의 사람들에게 내 버튼을 추가하는 패킷 보내기
        BroadcastAddPlayer(playerData);
        // 나에게 방 안 전체 플레이어 목록 전송
        SendAddPlayerPacket(playerData, clientEp);
    }

    private void HandleExitRequest(PlayerPacket? exitRequestPacket, IPEndPoint clientEp)
    {
        if (exitRequestPacket == null)
        {
            Console.WriteLine("[서버] HandleExitRequest 패킷이 없습니다!");
            return;
        }
        
        // _roomLists에서 패킷의 RelatedRoomId로 룸이 존재하는지 확인
        if (!_roomLists.TryGetValue(exitRequestPacket.RelatedRoomId, out RoomData? roomData))
        {
            Console.WriteLine("[서버] HandleExitRequest 룸이 존재하지 않습니다");
            return;
        }
        // 룸의 InRoomPlayers에서 패킷의 PlayerId로 해당 플레이어가 존재하는지 확인
        if (!roomData.InRoomPlayers.TryGetValue(exitRequestPacket.PlayerId, out PlayerData? playerData))
        {
            Console.WriteLine("[서버] HandleExitRequest 룸 안에 플레이어가 존재하지 않습니다");
            return;
        }
        
        // roomData.OwnerId == 패킷 PlayerId 이면 (방장이 나간 경우)
        //       → 방 안 모든 플레이어에게 DestroyRoom 패킷 브로드캐스팅
        //       → _roomLists에서 해당 룸 제거 후 종료
        if (roomData.OwnerId == playerData.PlayerId)
        {
            BroadcastDestroyRoom(roomData, false);
        }
        else
        {
            _players[playerData.PlayerId].PlayerWhereRoom(0);
            
            //일반 플레이어가 나간 경우
            //       → 나(clientEp)에게 ExitRoom 패킷 전송
            SendExitRoomPacket(clientEp);
            
            //       → 방 안의 나머지 플레이어들에게 RemovePlayerRoom 패킷 브로드캐스팅
            BroadcastRemovePlayerRoomPacket(exitRequestPacket, clientEp);
            
            //       → roomData.RemovePlayer(playerId)
            roomData.RemovePlayer(playerData.PlayerId);

            //       → 모든 클라이언트에게 RoomUpdate 패킷 브로드캐스팅
            BroadcastUpdateRoomPacket(roomData.RoomId);

        }
        
    }

    private void HandleGameStart(RoomPacket? gameStartPacket, IPEndPoint clientEp)
    {
        if (gameStartPacket == null)
        {
            Console.WriteLine($"[서버] HandleGameStart 패킷이 없습니다...");
            return;
        }

        if (!_players.TryGetValue(gameStartPacket.OwnerId, out var playerData))
        {
            Console.WriteLine($"[서버] HandleGameStart 플레이어가 없습니다...");
            return;
        }

        if (!_roomLists.TryGetValue(gameStartPacket.RoomId, out var roomData))
        {
            Console.WriteLine($"[서버] HandleGameStart 해당하는 룸이 없습니다...");
            return;
        }

        if (roomData.OwnerId != playerData.PlayerId)
        {
            Console.WriteLine($"[서버] HandleGameStart {playerData.PlayerName}님은 {roomData.RoomId}번 방의 방장이 아닙니다.");
            return;
        }

        if (roomData.CurrentRoomPlayers != roomData.RoomPlayerLimit)
        {
            Console.WriteLine($"[서버] HandleGameStart 인원 수가 맞지 않습니다.");
            return;
        }

        SendGameStartPacket(roomData);
        
        BroadcastDestroyRoom(roomData, true);
    }
    
    #endregion

    #region 패킷 송신 메서드
    
    private void SendPlayerSpawn(PlayerData playerData, IPEndPoint clientEp)
    {
        PlayerPacket playerPacket = new PlayerPacket
        {
            Type = PacketType.Spawn,
            PlayerId = playerData.PlayerId,
            PlayerName = playerData.PlayerName,
            WinScore =  playerData.WinScore,
            WinRate = playerData.WinRate,
            PlayerRank = playerData.PlayerRank,
            
            LastUpdateTime = DateTime.UtcNow
        };
        
        string json = JsonSerializer.Serialize(playerPacket);
        // Console.WriteLine(json);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
            
        _socket.SendTo(buffer, clientEp);
        
        Console.WriteLine("[패킷 전송 완료]" + json );
    }
    
    
    private void SendDespawnPacket(IPEndPoint clientEp)
    {
        PlayerPacket despawnPacket = new PlayerPacket
        {
            Type = PacketType.Despawn,
            LastUpdateTime = DateTime.UtcNow,
        };
        string json = JsonSerializer.Serialize(despawnPacket);
       // Console.WriteLine(json);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        _socket.SendTo(buffer, clientEp);
        
        Console.WriteLine("[패킷 전송 완료]" + json );
    }
    
    private void SendRoomList(PlayerPacket packet, IPEndPoint clientEp)
    {
        int sentCount = 0;
        foreach (RoomData roomData in _roomLists.Values)
        {
            RoomPacket roomPacket = new RoomPacket
            {
                Type = PacketType.CreateRoom,
                OwnerId = roomData.OwnerId,
                RoomId = roomData.RoomId,
                RoomName = roomData.RoomName ?? $"GameRoom{roomData.RoomId}",
                RoomPlayerLimit = roomData.RoomPlayerLimit,
                CurrentRoomPlayers = roomData.CurrentRoomPlayers,
                LastUpdateTime = DateTime.UtcNow

            };
            
            string json = JsonSerializer.Serialize(roomPacket);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            _socket?.SendTo(buffer, clientEp);
            
            sentCount++;

        }
        Console.WriteLine($"[룸 데이터] {sentCount}개의 룸 데이터를 {packet.PlayerName}님에게 전달했습니다.");
        
    }
    
    private void SendEnterRoomPacket(PlayerData newPlayerData, IPEndPoint clientEp)
    {
        PlayerPacket enterRoomPacket = new PlayerPacket
        {
            Type = PacketType.EnterRoom,
            PlayerId =  newPlayerData.PlayerId,
            RelatedRoomId = newPlayerData.RelatedRoomId,
            LastUpdateTime = DateTime.UtcNow

        };
        string json = JsonSerializer.Serialize(enterRoomPacket);
        // Console.WriteLine(json);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        _socket.SendTo(buffer, clientEp);
            
        Console.WriteLine("[패킷 전송 완료]" + json );
        
    }

    private void SendAddPlayerPacket(PlayerData newPlayerData, IPEndPoint clientEp)
    {
        if (!_roomLists.TryGetValue(newPlayerData.RelatedRoomId, out RoomData? roomData))
        {
            Console.WriteLine($"[서버] SendAddPlayerPacketAsync: RelatedRoomId={newPlayerData.RelatedRoomId} 룸을 찾을 수 없음");
            return;
        }

        // 나를 포함한 방 안의 모든 사람들의 정보를 나에게 패킷 보내기(AddPlayer)
        foreach (var inRoomPlayer in roomData.InRoomPlayers.Values)
        {
            PlayerPacket addPlayerPacket = new PlayerPacket
            {
                Type = PacketType.AddPlayerRoom,
                PlayerId = inRoomPlayer.PlayerId,
                PlayerName = inRoomPlayer.PlayerName,
                WinScore =  inRoomPlayer.WinScore,
                WinRate = inRoomPlayer.WinRate,
                PlayerRank = inRoomPlayer.PlayerRank,

                RelatedRoomId = newPlayerData.RelatedRoomId,
                LastUpdateTime = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(addPlayerPacket);
            Console.WriteLine($"[서버] SendAddPlayerPacketAsync: AddPlayerRoom 패킷 전송 → {inRoomPlayer.PlayerName} 정보를 {newPlayerData.PlayerName}에게");
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            _socket?.SendTo(buffer, clientEp);
        }
    }
    
    private void SendExitRoomPacket(IPEndPoint clientEp)
    {
        PlayerPacket exitRoomPacket = new PlayerPacket
        {
            Type = PacketType.ExitRoom,
            LastUpdateTime = DateTime.UtcNow
        };
        string json = JsonSerializer.Serialize(exitRoomPacket);
        // Console.WriteLine(json);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        _socket?.SendTo(buffer, clientEp);
        
        Console.WriteLine("[서버] SendExitRoomPacket 패킷전달" + json);
    }
    
    private void SendGameStartPacket(RoomData roomData)
    {
        RoomPacket gameStartResponsePacket = new RoomPacket
        {
            Type = PacketType.GameStart,
            RoomId = roomData.RoomId,
            LastUpdateTime = DateTime.UtcNow
        };
        
        
        string json = JsonSerializer.Serialize(gameStartResponsePacket);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        int sentCount = 0;
        foreach (var inRoomPlayerData in roomData.InRoomPlayers.Values)
        {
            _socket.SendTo(buffer, inRoomPlayerData.ClientEp);
            sentCount++;
        }
        Console.WriteLine($"[서버] {roomData.RoomId}번 룸의 플레이어 {sentCount}명에게 GameStart 패킷을 보냈습니다." + json);

    }

    #endregion

    #region 브로드캐스팅
    
    private void BroadcastCreateRoomPacket(RoomData newRoomData)
    {
        RoomPacket broadcastPacket = new RoomPacket
        {
            Type = PacketType.CreateRoom,
            OwnerId = newRoomData.OwnerId,
            RoomId = newRoomData.RoomId,
            RoomName = newRoomData.RoomName,
            RoomPlayerLimit = newRoomData.RoomPlayerLimit,
            CurrentRoomPlayers = newRoomData.CurrentRoomPlayers,
            LastUpdateTime = DateTime.UtcNow
        };
        
        string json = JsonSerializer.Serialize(broadcastPacket);
        // Console.WriteLine(json);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        int sentCount = 0;
        foreach (PlayerData playerData in _players.Values)
        {
            _socket.SendTo(buffer, playerData.ClientEp);
            sentCount++;
        }
        
        Console.WriteLine($"[서버] BroadcastCreateRoomPacketAsync: {sentCount}명의 " +
                          $"플레이어에게 새로운 룸{newRoomData.RoomId} 정보 브로드캐스트 완료" + json);
    }
    
    private void BroadcastAddPlayer(PlayerData newPlayerData)
    {
        if (!_roomLists.TryGetValue(newPlayerData.RelatedRoomId, out RoomData? roomData)) return;

        // 패킷은 한 번만 직렬화
        PlayerPacket addPlayerPacket = new PlayerPacket
        {
            Type = PacketType.AddPlayerRoom,
            PlayerId = newPlayerData.PlayerId,
            PlayerName = newPlayerData.PlayerName,
            WinScore = newPlayerData.WinScore,
            WinRate = newPlayerData.WinRate,
            PlayerRank = newPlayerData.PlayerRank,
            RelatedRoomId = newPlayerData.RelatedRoomId,
            LastUpdateTime = DateTime.UtcNow
        };
        int sentCount = 0;
        string json = JsonSerializer.Serialize(addPlayerPacket);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        foreach (var inRoomPlayer in roomData.InRoomPlayers.Values)
        {
            if (inRoomPlayer.PlayerId != newPlayerData.PlayerId)
            {
                sentCount++;
                _socket?.SendTo(buffer, inRoomPlayer.ClientEp);
            }
        }
        Console.WriteLine($"[서버] BroadcastAddPlayer: {sentCount}명의 " +
                          $"플레이어에게 새로운 플레이어{newPlayerData.PlayerName} 정보 브로드캐스트 완료" + json);
    }

    private void BroadcastUpdateRoomPacket(int roomId)
    {
        if (_roomLists.TryGetValue(roomId, out RoomData? roomData))
        {
            int sentCount = 0;
            RoomPacket roomUpdatePacket = new RoomPacket
            {
                Type = PacketType.RoomUpdate,
                RoomId = roomId,
                RoomName = roomData.RoomName,
                RoomPlayerLimit = roomData.RoomPlayerLimit,
                CurrentRoomPlayers = roomData.CurrentRoomPlayers,
                LastUpdateTime = DateTime.UtcNow
            };
            
            string json = JsonSerializer.Serialize(roomUpdatePacket);
            Console.WriteLine(json);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            foreach (var player in _players.Values)
            {
                sentCount++;
                _socket.SendTo(buffer, player.ClientEp);
            }
            Console.WriteLine($"[서버] BroadcastUpdateRoomPacket: 룸 업데이트 정보 브로드캐스트 완료" + json);
        }
    }
    
    private void BroadcastRemovePlayerRoomPacket(PlayerPacket exitRequestPacket, IPEndPoint clientEp)
    {
        int sentCount = 0;

        if (!_roomLists.TryGetValue(exitRequestPacket.RelatedRoomId, out RoomData? roomData))
        {
            Console.WriteLine("[서버] BroadcastRemovePlayerRoomPacket: 룸을 가져올 수 없습니다...");
            return;
        }

        if (!roomData.InRoomPlayers.TryGetValue(exitRequestPacket.PlayerId, out PlayerData? playerData))
        {
            Console.WriteLine("[서버] BroadcastRemovePlayerRoomPacket: 해당하는 플레이어를 찾을 수 없습니다...");
            return;
        }

        PlayerPacket removePlayerPacket = new PlayerPacket
        {
            Type = PacketType.RemovePlayerRoom,
            PlayerId = playerData.PlayerId,
            LastUpdateTime = DateTime.UtcNow
        };
        string json =  JsonSerializer.Serialize(removePlayerPacket);
        Console.WriteLine(json);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        
        foreach (var inRoomPlayer in roomData.InRoomPlayers.Values)
        {
            if (!inRoomPlayer.ClientEp.Equals(clientEp))
            {
                _socket?.SendTo(buffer, inRoomPlayer.ClientEp);
                sentCount++;
            }
        }
        Console.WriteLine($"[서버] BroadcastRemovePlayerRoomPacket " +
                          $": {sentCount}명의 플레이어에게 {playerData.PlayerName} RemovePlayer 패킷을 날렸습니다." + json);
    }
    
    private void BroadcastDestroyRoom(RoomData roomData, bool isGameStart)
    {
        int sentCount = 0;
        RoomPacket destroyRoomPacket = new RoomPacket
        {
            Type = PacketType.DestroyRoom,
            RoomId = roomData.RoomId,
            LastUpdateTime = DateTime.UtcNow
        };
        string json = JsonSerializer.Serialize(destroyRoomPacket);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        foreach (var inRoomPlayer in roomData.InRoomPlayers.Values)
        {
            inRoomPlayer.PlayerWhereRoom(0);
        }
            
        foreach (var player in _players.Values)
        {
            if (isGameStart)
            {
                if (player.RelatedRoomId != roomData.RoomId)
                {
                    _socket.SendTo(buffer, player.ClientEp);
                    sentCount++;
                }
            }
            else
            {
                _socket.SendTo(buffer, player.ClientEp);
                sentCount++;
            }
            
        }
        Console.WriteLine($"[서버] {roomData.RoomName} 방의 {sentCount}명의 " +
                          $"인원에게 DestroyRoom 패킷을 보냈습니다.");
        if (_roomLists.TryRemove(roomData.RoomId, out _))
        {
            Console.WriteLine($"[서버] {roomData.RoomName} 방을 삭제하였습니다.");
        }
    }
    
    #endregion
    
    

    public void Dispose()
    {
        if (_disposed) return;
        _isRunning = false;
        _socket.Dispose();
        _players.Clear();
        _roomLists.Clear();
        
        _nextPlayerId = 0;
        _nextRoomId = 0;
        
        _disposed = true;
        
    }
}