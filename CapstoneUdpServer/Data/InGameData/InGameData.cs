using System.Collections.Concurrent;

namespace CapstoneUdpServer.Data;

public class InGameData
{
    public ConcurrentDictionary<int, PlayerUnitData> PlayerUnitDataMap { get; } = new();
    public BuildingData BuildingData;
    public NpcUnitData NpcUnitData;
}