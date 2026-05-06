using System;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class NpcData
{
    private static readonly Random Rng = new();

    public int     NpcId          { get; set; }
    public int     NpcType        { get; set; }
    public float   MaxHp          { get; set; } = 100f;
    public float   CurrentHp      { get; set; } = 100f;
    public bool    IsAlive        { get; set; } = true;
    public Vector3 Position       { get; set; }
    public float   RotY           { get; set; }
    public Vector3 MoveDir        { get; set; }
    public float   DirTimer       { get; set; }
    public bool    IsChasing      { get; set; }
    public int     ChaseTargetId  { get; set; } // 추적 중인 PlayerId (0이면 wander)

    public void PickNewDirection()
    {
        float angle = (float)(Rng.NextDouble() * Math.PI * 2);
        MoveDir  = new Vector3((float)Math.Sin(angle), 0, (float)Math.Cos(angle));
        RotY     = angle * (180f / (float)Math.PI);
        DirTimer = 3f + (float)(Rng.NextDouble() * 4f);
    }
}
