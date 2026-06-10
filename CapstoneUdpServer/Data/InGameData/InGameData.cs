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
    

    private static readonly Random _rng = new();
    private readonly int _maxNpcAmount = 30;
    public int CurrentNpcAmount => NpcMap.Count;
    private bool _topBossOn;
    private bool _bottomBossOn;
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
        _spawnPointAmount = 5;
        _socket = socket;
        _stopwatch = Stopwatch.StartNew();
        _occupationPhaseStarted = false;
    }

    public void StartGame(float npcSpawnInterval)
    {
        NpcSpawnInterval = npcSpawnInterval;
        _ = StartSystemLoop();
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
                var (min, sec) = GetElapsedTime();
                if (min >= 5)
                {
                    _occupationPhaseStarted = true;
                    Console.WriteLine($"[Boss] 5분 경과({min}m{sec}s) → 보스 스폰 트리거");
                    SpawnBossMonsters();
                }
            }
            await Task.Delay(100);
        }
    }
    
    private void SpawnBossMonsters()
    {
        if (!_topBossOn)    SpawnBoss(0);
        if (!_bottomBossOn) SpawnBoss(1);
    }

    private void SpawnBoss(int spawnPoint)
    {
        if (spawnPoint == 0) _topBossOn    = true;
        else                 _bottomBossOn = true;

        int npcId        = NextNpcId() + 2000;
        var boss         = new NpcData(npcId, 1, 500f);
        boss.SpawnPoint  = spawnPoint;
        NpcMap[npcId]    = boss;

        var buf = ProtobufSerializer.Serialize((uint)InGamePacketType.SpawnNpc, new SpawnNpcPacket
        {
            NpcId      = npcId,
            SpawnPoint = spawnPoint,
            NpcType    = 1,
            MaxHp      = boss.MaxHp,
        });
        Broadcast(_socket, buf);
        Console.WriteLine($"[Boss] 보스 스폰: npcId={npcId} spawnPoint={spawnPoint}");
    }

    public void OnBossDied(int spawnPoint)
    {
        if (spawnPoint == 0) _topBossOn    = false;
        else                 _bottomBossOn = false;
    }

    private void TrySpawnNpc()
    {
        if (CurrentNpcAmount >= _maxNpcAmount)
        {
            Console.WriteLine($"[NPC] 스폰 스킵 — 현재 NPC 수={CurrentNpcAmount}/{_maxNpcAmount}");
            return;
        }

        int npcId      = NextNpcId() + 1000;
        int spawnPoint = _rng.Next(2, _spawnPointAmount); // 2~9 랜덤
        var npc        = new NpcData(npcId, (int)UnitType.Npc);
        NpcMap[npcId]  = npc;

        var buf = ProtobufSerializer.Serialize((uint)InGamePacketType.SpawnNpc, new SpawnNpcPacket
        {
            NpcId      = npcId,
            SpawnPoint = spawnPoint,
            NpcType    = 0,
            MaxHp      = npc.MaxHp,
        });
        Broadcast(_socket, buf);
        Console.WriteLine($"[NPC] 스폰: npcId={npcId} spawnPoint={spawnPoint} 현재총={CurrentNpcAmount}");
    }

}

