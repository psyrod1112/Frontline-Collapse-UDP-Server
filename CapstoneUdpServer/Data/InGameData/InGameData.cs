using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;

namespace CapstoneUdpServer.Data;

public class InGameData
{
    public ConcurrentDictionary<int, PlayerUnitData>  PlayerUnitDataMap  { get; } = new();
    // buildingId → ownerId 빠른 조회용 인덱스
    public ConcurrentDictionary<int, BuildingData>    BuildingDataMap { get; } = new();
    public ConcurrentDictionary<int, NpcData>         NpcMap             { get; } = new();

    private int _npcIdCounter;
    public int NextNpcId() => Interlocked.Increment(ref _npcIdCounter);

    private int _buildingIdCounter;
    public int NextBuildingId() => Interlocked.Increment(ref _buildingIdCounter);

    // 이 필드의 모든 플레이어에게 브로드캐스트 (excludePlayerId 제외)
    public void Broadcast(Socket socket, byte[] buf, int excludePlayerId = 0)
    {
        foreach (var unit in PlayerUnitDataMap.Values)
            if (unit.PlayerId != excludePlayerId)
                socket.SendTo(buf, (IPEndPoint)unit.IpEndPoint);
    }
    
}

