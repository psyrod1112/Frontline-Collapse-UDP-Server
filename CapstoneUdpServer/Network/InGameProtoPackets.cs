using CapstoneUdpServer.Data;
using ProtoBuf;

namespace CapstoneUdpServer.Network
{
    public enum PlayerAnimState { Idle, Walk, Run, Jump, Fire, Dead }
    public enum HitTargetType { Player, MovingUnit, Building, Environment }
    
    [ProtoContract]
    public class PlayerInputPacket
    {
        [ProtoMember(1)] public uint Tick;
        [ProtoMember(2)] public int PlayerId;
        
        [ProtoMember(3)] public float MoveX;
        [ProtoMember(4)] public float MoveZ;
        [ProtoMember(5)] public float RotationY;
        [ProtoMember(6)] public float CameraPitch;
        
        [ProtoMember(7)] public bool IsJumping;
        [ProtoMember(8)] public bool IsRunning;
        [ProtoMember(9)] public bool IsCrouching;
        
        [ProtoMember(10)] public PlayerAnimState AnimState;
        [ProtoMember(11)] public WeaponType WeaponIndex;
        [ProtoMember(12)] public float DeltaTime;

        [ProtoMember(13)] public float PosX;
        [ProtoMember(14)] public float PosY;
        [ProtoMember(15)] public float PosZ;
    }

    [ProtoContract]
    public class PlayerMoveConfirmPacket // 서버가 나에게만 보정(Reconciliation)용으로 보내는 패킷
    {
        [ProtoMember(1)] public uint Tick;
        [ProtoMember(2)] public int PlayerId;
        [ProtoMember(3)] public float PosX;
        [ProtoMember(4)] public float PosY;
        [ProtoMember(5)] public float PosZ;
        [ProtoMember(6)] public float VelX;
        [ProtoMember(7)] public float VelY;
        [ProtoMember(8)] public float VelZ;
        [ProtoMember(9)] public float RotationY;
        [ProtoMember(10)] public PlayerAnimState AnimState;
    }

    [ProtoContract]
    public class RemotePlayerStatePacket // 나를 제외한 다른 사람들에게 내 위치를 보여주는 패킷
    {
        [ProtoMember(1)] public uint Tick;
        [ProtoMember(2)] public int PlayerId;
        [ProtoMember(3)] public float PosX;
        [ProtoMember(4)] public float PosY;
        [ProtoMember(5)] public float PosZ;
        [ProtoMember(6)] public float VelX;
        [ProtoMember(7)] public float VelY;
        [ProtoMember(8)] public float VelZ;
        [ProtoMember(9)] public float RotationY;
        [ProtoMember(10)] public PlayerAnimState AnimState;
        [ProtoMember(11)] public WeaponType WeaponIndex;
        
    }

    [ProtoContract]
    public class FireEventPacket
    {
        [ProtoMember(1)] public uint Tick;
        [ProtoMember(2)] public int PlayerId;
        [ProtoMember(3)] public float OriginX;
        [ProtoMember(4)] public float OriginY;
        [ProtoMember(5)] public float OriginZ; // 총구 위치
        [ProtoMember(6)] public float DirX;
        [ProtoMember(7)] public float DirY;
        [ProtoMember(8)] public float DirZ; // 발사 방향
        [ProtoMember(9)] public WeaponType WeaponIndex;
        [ProtoMember(10)] public int HitTargetId;
        [ProtoMember(11)] public HitTargetType HitTargetType;
    }

    [ProtoContract]
    public class NpcStatePacket
    {
        [ProtoMember(1)] public uint Tick;
        [ProtoMember(2)] public int NpcId;
        [ProtoMember(3)] public float PosX;
        [ProtoMember(4)] public float PosY;
        [ProtoMember(5)] public float PosZ;
        [ProtoMember(6)] public float RotY;
        [ProtoMember(7)] public float VelX;
        [ProtoMember(8)] public float VelZ;
        [ProtoMember(9)] public float CurrentHp;
        [ProtoMember(10)] public PlayerAnimState AnimState;
    }

    [ProtoContract]
    public class MissileStatePacket
    {
        [ProtoMember(1)] public uint Tick;
        [ProtoMember(2)] public int MissileId;
        [ProtoMember(3)] public float PosX;
        [ProtoMember(4)] public float PosY;
        [ProtoMember(5)] public float PosZ;
        [ProtoMember(6)] public float VelX;
        [ProtoMember(7)] public float VelY;
        [ProtoMember(8)] public float VelZ;
        [ProtoMember(9)] public bool Exploded;
    }

    [ProtoContract]
    public class SpawnNpcRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int SpawnPositionIndex;
    }

    [ProtoContract]
    public class SpawnNpcPacket
    {
        [ProtoMember(1)] public int NpcId;
        [ProtoMember(2)] public float PosX;
        [ProtoMember(3)] public float PosY;
        [ProtoMember(4)] public float PosZ;
        [ProtoMember(5)] public int NpcType;
    }

    [ProtoContract]
    public class SpawnMissileRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public float OriginX;
        [ProtoMember(3)] public float OriginY;
        [ProtoMember(4)] public float OriginZ;
        [ProtoMember(5)] public float DirX;
        [ProtoMember(6)] public float DirY;
        [ProtoMember(7)] public float DirZ;
        [ProtoMember(8)] public int WeaponIndex;
    }

    [ProtoContract]
    public class SpawnMissilePacket
    {
        [ProtoMember(1)] public int MissileId;
        [ProtoMember(2)] public int OwnerPlayerId;
        [ProtoMember(3)] public float OriginX;
        [ProtoMember(4)] public float OriginY;
        [ProtoMember(5)] public float OriginZ;
        [ProtoMember(6)] public float DirX;
        [ProtoMember(7)] public float DirY;
        [ProtoMember(8)] public float DirZ;
        [ProtoMember(9)] public int WeaponIndex;
    }
}