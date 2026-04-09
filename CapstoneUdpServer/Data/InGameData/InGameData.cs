using System.Collections.Concurrent;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class InGameData
{
    public ConcurrentDictionary<int, PlayerUnitData>    PlayerUnitDataMap { get; } = new();
    public ConcurrentDictionary<int, InGameBuildingRecord> BuildingMap    { get; } = new();
    public MovingUnitData? MovingUnitData;
}

/// <summary>씬에 배치된 건물 한 개의 인게임 상태</summary>
public class InGameBuildingRecord
{
    public int    BuildingId  { get; set; }
    public int    OwnerId     { get; set; }
    public int    PrefabIndex { get; set; }
    public float  MaxHp       { get; set; }
    public float  CurrentHp   { get; set; }
    public Vector3 Position   { get; set; }
}
