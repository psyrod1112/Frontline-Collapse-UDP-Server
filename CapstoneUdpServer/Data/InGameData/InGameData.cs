using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using CapstoneUdpServer.Network;

namespace CapstoneUdpServer.Data;

public class InGameData
{
    
    private Socket _socket;

    private readonly Stopwatch _stopwatch;
    
    public ConcurrentDictionary<int, PlayerUnitData>  PlayerUnitDataMap  { get; } 
    // buildingId → ownerId 빠른 조회용 인덱스
    public ConcurrentDictionary<int, BuildingData>    BuildingDataMap { get; } 
    public ConcurrentDictionary<int, NpcData>         NpcMap             { get; } 
    
    public float NpcSpawnInterval { get; set; }
    

    private readonly int _maxNpcAmount = 30;
    public int CurrentNpcAmount => NpcMap.Count;
    private bool _topBossOn;
    private bool _bottomBossOn;
    private int _npcSpawnPoint;
    private int _spawnPointAmount;
    private bool _occupationPhaseStarted;
    public bool OccupationPhaseStarted => _occupationPhaseStarted;

    private bool _onProgress; //인게임 구동중
    
    private int _npcIdCounter;
    public int NextNpcId() => Interlocked.Increment(ref _npcIdCounter);

    private int _buildingIdCounter;
    public int NextBuildingId() => Interlocked.Increment(ref _buildingIdCounter);

    public InGameData(Socket socket)
    {
        _topBossOn = false;
        _bottomBossOn = false;
        PlayerUnitDataMap = new();
        BuildingDataMap = new();
        NpcMap = new();
        _onProgress = true;
        _npcSpawnPoint = 0;
        _spawnPointAmount = 7;
        _ = StartSystemLoop();
        _socket = socket;

        _stopwatch = Stopwatch.StartNew();
        _occupationPhaseStarted = false;
    }
    
    public (int minutes, int seconds) GetElapsedTime() 
        => ((int)_stopwatch.Elapsed.TotalMinutes, _stopwatch.Elapsed.Seconds);
    

    // 이 필드의 모든 플레이어에게 브로드캐스트 (excludePlayerId 제외)
    public void Broadcast(Socket socket, byte[] buf, int excludePlayerId = 0)
    {
        foreach (var unit in PlayerUnitDataMap.Values)
            if (unit.PlayerId != excludePlayerId)
                socket.SendTo(buf, (IPEndPoint)unit.IpEndPoint);
    }
    
    public async Task StartSystemLoop()
    {
        var lastTime = DateTime.UtcNow;
        while (_onProgress)
        {
            
            //Npc 생성 루프
            if (PlayerUnitDataMap.IsEmpty) break;
            var now = DateTime.UtcNow;

            if ((float)(now - lastTime).TotalSeconds >= NpcSpawnInterval)
            {
                TrySpawnNpc();
                lastTime = DateTime.UtcNow;
            }
            
            //점령 로직 루프

            if (!_occupationPhaseStarted)
            {
                var (min, _) = GetElapsedTime();
                if (min >= 5)
                {
                    _occupationPhaseStarted = true;
                }
            }

            await Task.Delay(100);
        }
    }
    
    private void TrySpawnNpc()
    {
        if (CurrentNpcAmount >= _maxNpcAmount) return;
        
        int   npcId = NextNpcId() + 1000;
        UnitType npcType = UnitType.Npc;
        var npc = new NpcData(npcId, (int)npcType);
        NpcMap[npcId] = npc;

        var buf = ProtobufSerializer.Serialize((uint)InGamePacketType.SpawnNpc, new SpawnNpcPacket
        {
            NpcId   = npcId,
            SpawnPoint = _npcSpawnPoint % (_spawnPointAmount),
            NpcType = 0,
            MaxHp   = npc.MaxHp,
        });
        Broadcast(_socket, buf);
        _npcSpawnPoint++;
    }

}

