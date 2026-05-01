using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class InGameData
{
    public ConcurrentDictionary<int, PlayerUnitData>  PlayerUnitDataMap  { get; } = new();
    // buildingId → ownerId 빠른 조회용 인덱스
    public ConcurrentDictionary<int, int>             BuildingOwnerIndex { get; } = new();

    // 이 필드의 모든 플레이어에게 브로드캐스트 (excludePlayerId 제외)
    public void Broadcast(Socket socket, byte[] buf, int excludePlayerId = 0)
    {
        foreach (var unit in PlayerUnitDataMap.Values)
            if (unit.PlayerId != excludePlayerId)
                socket.SendTo(buf, (IPEndPoint)unit.IpEndPoint);
    }

    // buildingId로 건물 레코드 + 소유 플레이어 유닛 조회
    public bool TryGetBuilding(int buildingId, out InGameBuildingRecord? building, out PlayerUnitData? owner)
    {
        building = null;
        owner    = null;
        if (!BuildingOwnerIndex.TryGetValue(buildingId, out int ownerId)) return false;
        if (!PlayerUnitDataMap.TryGetValue(ownerId, out owner))           return false;
        return owner.Buildings.TryGetValue(buildingId, out building);
    }
}

public class InGameBuildingRecord
{
    public int        BuildingId        { get; set; }
    public int        OwnerId           { get; set; }
    public int        PrefabIndex       { get; set; }
    public float      MaxHp             { get; set; }
    public float      CurrentHp         { get; set; }
    public Vector3    Position          { get; set; }
    public bool       IsMissileLoaded   { get; set; }
    public int        LoadedMissileId   { get; set; }
    public WeaponType LoadedMissileType { get; set; }
}
