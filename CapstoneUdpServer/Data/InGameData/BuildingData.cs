using System.Numerics;

namespace CapstoneUdpServer.Data;

public enum BuildingState
{
    None,
    Normal,
    Burning1,
    Burning2,
    Destroyed
}

public class BuildingData
{
    public int BuildingId { get; set; }
    public int OwnerId { get; set; }
    public ItemName BuildingType { get; set; }
    
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    
    public float MaxHp { get; set; }
    public float CurrentHp { get; set; }
    
    public BuildingState State { get; set; }

}