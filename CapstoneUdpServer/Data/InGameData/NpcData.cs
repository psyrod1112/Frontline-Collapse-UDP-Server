using System;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class NpcData
{
    private static readonly Random Rng = new();

    public int     NpcId          { get; set; }
    public int     NpcType        { get; set; }
    public float   MaxHp          { get; set; } 
    public float   CurrentHp      { get; set; }
    public bool    IsAlive        { get; set; } 
    public Vector3 Position       { get; set; }
    public float   RotY           { get; set; }
    public Vector3 MoveDir        { get; set; }
    public int  LastCornerIdx { get; set; }
    public bool Fighting      { get; set; }
    public int  SpawnPoint    { get; set; }

    public NpcData(int npcId, int npcType,float maxHp = 100f)
    {
        NpcId     = npcId;
        NpcType   = npcType;
        MaxHp     = maxHp;
        CurrentHp = maxHp;
        IsAlive   = true;
        LastCornerIdx = 0;
    }
    
}
