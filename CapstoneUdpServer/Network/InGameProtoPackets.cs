using CapstoneUdpServer.Data;
using System.Collections.Generic;
using ProtoBuf;

namespace CapstoneUdpServer.Network
{
    public enum UnitType { Player, MovingUnit, Building, Environment, Missile }
    public enum AnimTriggerType { Jump = 0, ThrowGrenade = 1, Hit = 2, Shot = 3 }

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

        [ProtoMember(11)] public ItemName WeaponIndex;
        [ProtoMember(12)] public float DeltaTime;

        [ProtoMember(13)] public float PosX;
        [ProtoMember(14)] public float PosY;
        [ProtoMember(15)] public float PosZ;
        [ProtoMember(16)] public bool IsZooming;
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
        [ProtoMember(11)] public ItemName WeaponIndex;
        [ProtoMember(12)] public float CameraPitch;
        [ProtoMember(13)] public bool IsCrouching;
        [ProtoMember(14)] public float MoveX;
        [ProtoMember(15)] public float MoveZ;
        [ProtoMember(16)] public bool IsRunning;
        [ProtoMember(17)] public bool IsZooming;
    }

    [ProtoContract]
    public class AnimTriggerPacket
    {
        [ProtoMember(1)] public int PlayerId; // 발신자 PlayerId (서버 라우팅용)
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int Trigger;  // AnimTriggerType
        [ProtoMember(4)] public int TargetId; // Player → PlayerId / NPC → NpcId
    }

    [ProtoContract]
    public class FireEventPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public float OriginX;
        [ProtoMember(4)] public float OriginY;
        [ProtoMember(5)] public float OriginZ; // 총구 위치
        [ProtoMember(6)] public float DirX;
        [ProtoMember(7)] public float DirY;
        [ProtoMember(8)] public float DirZ; // 발사 방향
        [ProtoMember(9)] public int ItemName;
    }

    [ProtoContract]
    public class NpcStatePacket
    {
        [ProtoMember(1)]  public uint            Tick;
        [ProtoMember(2)]  public int             NpcId;
        [ProtoMember(3)]  public float           PosX;
        [ProtoMember(4)]  public float           PosY;
        [ProtoMember(5)]  public float           PosZ;
        [ProtoMember(6)]  public float           RotY;
        [ProtoMember(7)]  public float           VelX;
        [ProtoMember(8)]  public float           VelZ;
        [ProtoMember(9)]  public float           CurrentHp;
    }

    [ProtoContract]
    public class NpcChaseEventPacket
    {
        [ProtoMember(1)] public int  NpcId;
        [ProtoMember(2)] public int  TargetPlayerId; // 0이면 추적 중단
        [ProtoMember(3)] public bool IsChasing;
    }

    [ProtoContract]
    public class SpawnNpcPacket
    {
        [ProtoMember(1)] public int   NpcId;
        [ProtoMember(2)] public float PosX;
        [ProtoMember(3)] public float PosY;
        [ProtoMember(4)] public float PosZ;
        [ProtoMember(5)] public int   NpcType;
        [ProtoMember(6)] public float MaxHp;
    }

    [ProtoContract]
    public class SpawnPlayerUnitPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public float PosX;
        [ProtoMember(4)] public float PosY;
        [ProtoMember(5)] public float PosZ;
        [ProtoMember(6)] public float RotX;
        [ProtoMember(7)] public float RotY;
        [ProtoMember(8)] public float RotZ;
        [ProtoMember(9)] public float CurrentHp;
        [ProtoMember(10)] public float MaxHp;
        [ProtoMember(11)] public int CurrentGrippingItem;
        [ProtoMember(12)] public int HotkeyIndex;
    }

    [ProtoContract]
    public class WeaponChangePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int SlotIndex;
        [ProtoMember(4)] public int ItemName;
    }

    [ProtoContract]
    public class HotkeySavePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int Slot1;
        [ProtoMember(4)] public int Slot2;
        [ProtoMember(5)] public int Slot3;
        [ProtoMember(6)] public int Slot4;
    }

    [ProtoContract]
    public class UIUpdateRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
    }

    [ProtoContract]
    public class BuildingPlacePacket
    {
        [ProtoMember(1)] public int   BuildingId;
        [ProtoMember(2)] public int   OwnerId;
        [ProtoMember(3)] public int   BuildingType;
        [ProtoMember(4)] public float PosX;
        [ProtoMember(5)] public float PosY;
        [ProtoMember(6)] public float PosZ;
        [ProtoMember(7)] public float RotY;
    }

    [ProtoContract]
    public class BuildingDestroyPacket
    {
        [ProtoMember(1)] public int BuildingId;
        [ProtoMember(2)] public int BuildingType;
        [ProtoMember(3)] public int DestroyerId;
    }

    [ProtoContract]
    public class MissileLoadRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int BuildingId;
        [ProtoMember(4)] public int MissileType;
    }

    [ProtoContract]
    public class MissileLoadResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int BuildingId;
        [ProtoMember(3)] public int MissileType;
        [ProtoMember(4)] public int LoadedNormalMissileAmount;
        [ProtoMember(5)] public int LoadedNuclearMissileAmount;
    }

    [ProtoContract]
    public class MissileLaunchPacket
    {
        [ProtoMember(1)] public int MissileId;
        [ProtoMember(2)] public int MissileType;
        [ProtoMember(3)] public int OwnerId;
        [ProtoMember(4)] public int BuildingId;
        [ProtoMember(5)] public float OriginX;
        [ProtoMember(6)] public float OriginY;
        [ProtoMember(7)] public float OriginZ;
        [ProtoMember(8)] public float VelocityX;
        [ProtoMember(9)] public float VelocityY;
        [ProtoMember(10)] public float VelocityZ;
        [ProtoMember(11)] public float TargetX;
        [ProtoMember(12)] public float TargetY;
        [ProtoMember(13)] public float TargetZ;
    }

    [ProtoContract]
    public class MissileHitTargetPacket
    {
        [ProtoMember(1)] public int TargetId;
        [ProtoMember(2)] public int TargetType;
    }

    [ProtoContract]
    public class MissileHitRequestPacket
    {
        [ProtoMember(1)] public int MissileId;
        [ProtoMember(2)] public int MissileType;
        [ProtoMember(3)] public int OwnerId;
        [ProtoMember(4)] public int DirectTargetId;
        [ProtoMember(5)] public int DirectTargetType;
        [ProtoMember(6)] public List<MissileHitTargetPacket> SplashTargets = new();
    }

    [ProtoContract]
    public class DamageEventPacket
    {
        [ProtoMember(1)] public int   TargetId;
        [ProtoMember(2)] public int   TargetType;
        [ProtoMember(3)] public int   AttackerId;
        [ProtoMember(4)] public int   Damage;
        [ProtoMember(5)] public float CurrentHp;
        [ProtoMember(6)] public float MaxHp;
        [ProtoMember(7)] public float HitPosX;
        [ProtoMember(8)] public float HitPosY;
        [ProtoMember(9)] public float HitPosZ;
        [ProtoMember(10)] public float HitNormalX;
        [ProtoMember(11)] public float HitNormalY;
        [ProtoMember(12)] public float HitNormalZ;
        [ProtoMember(13)] public int  WeaponType;
    }

    [ProtoContract]
    public class DamageResultPacket
    {
        [ProtoMember(1)] public int   AttackerId;
        [ProtoMember(2)] public int   TargetId;
        [ProtoMember(3)] public int   TargetType;
        [ProtoMember(4)] public int   Damage;
        [ProtoMember(5)] public float CurrentHp;
        [ProtoMember(6)] public float MaxHp;
        [ProtoMember(7)] public float HitPosX;
        [ProtoMember(8)] public float HitPosY;
        [ProtoMember(9)] public float HitPosZ;
        [ProtoMember(10)] public float HitNormalX;
        [ProtoMember(11)] public float HitNormalY;
        [ProtoMember(12)] public float HitNormalZ;
        [ProtoMember(13)] public bool IsDead;
    }

    [ProtoContract]
    public class DeathEventPacket
    {
        [ProtoMember(1)] public int TargetId;
        [ProtoMember(2)] public int TargetType;
        [ProtoMember(3)] public int KillerId;
        [ProtoMember(4)] public int GoldReward;
    }

    [ProtoContract]
    public class DeathUpdatePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int DeathCount;
    }

    [ProtoContract]
    public class RespawnRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
    }

    [ProtoContract]
    public class RespawnResponsePacket
    {
        [ProtoMember(1)] public int   PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public float SpawnPosX;
        [ProtoMember(4)] public float SpawnPosY;
        [ProtoMember(5)] public float SpawnPosZ;
        [ProtoMember(6)] public float SpawnRotY;
        [ProtoMember(7)] public float MaxHp;
        [ProtoMember(8)] public int Level;
        [ProtoMember(9)] public float ExpAmount;
        [ProtoMember(10)] public float RequiredExp;
    }

    [ProtoContract]
    public class RewardUpdatePacket
    {
        [ProtoMember(1)] public int   PlayerId;
        [ProtoMember(2)] public int   GoldAmount;
        [ProtoMember(3)] public int   TotalGold;
        [ProtoMember(4)] public float ExpAmount;
        [ProtoMember(5)] public float TotalExp;
        [ProtoMember(6)] public int   KillCount;
        [ProtoMember(7)] public int   CSCount;
        [ProtoMember(8)] public int   Level;
        [ProtoMember(9)] public float RequiredExp;
    }

    [ProtoContract]
    public class UIUpdateResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public string PlayerName;
        [ProtoMember(4)] public int PlayerRank;
        [ProtoMember(5)] public int Gold;
        [ProtoMember(6)] public int Level;
        [ProtoMember(7)] public float CurrentHp;
        [ProtoMember(8)] public float MaxHp;
        [ProtoMember(9)] public float Exp;
        [ProtoMember(10)] public float RequiredExp;
        [ProtoMember(11)] public int Shortcut1;
        [ProtoMember(12)] public int Shortcut2;
        [ProtoMember(13)] public int Shortcut3;
        [ProtoMember(14)] public int Shortcut4;
        [ProtoMember(15)] public int KillCount;
        [ProtoMember(16)] public int DeathCount;
        [ProtoMember(17)] public int CSCount;
    }

    [ProtoContract]
    public class ScoreBoardRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
    }

    [ProtoContract]
    public class ScoreBoardResponsePacket
    {
        [ProtoMember(1)]  public int    PlayerId;
        [ProtoMember(2)]  public int    FieldId;
        [ProtoMember(3)]  public string PlayerName;
        [ProtoMember(4)]  public int    TotalGold;
        [ProtoMember(5)]  public int    Level;
        [ProtoMember(6)]  public float  MaxHp;
        [ProtoMember(7)]  public float  CurrentHp;
        [ProtoMember(8)]  public float  FinalBuildingMaxHp;
        [ProtoMember(9)]  public float  FinalBuildingCurrentHp;
        [ProtoMember(10)] public int    KillCount;
        [ProtoMember(11)] public int    DeathCount;
        [ProtoMember(12)] public int    CSCount;
        [ProtoMember(13)] public int    Rank;
        [ProtoMember(14)] public bool   isDead;
        [ProtoMember(15)] public bool   isMine;
    }

    [ProtoContract]
    public class BuyRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public uint ItemType;
        [ProtoMember(4)] public int ItemPrice;
    }
    
    [ProtoContract]
    public class BuyResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public string Msg;
        [ProtoMember(4)] public int RemainGold;
    }
    
    [ProtoContract]
    public class InventoryRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
    }

    [ProtoContract]
    public class InventoryResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int ItemType;
        [ProtoMember(4)] public int ItemId;
        [ProtoMember(5)] public int Amount;

        [ProtoMember(6)] public int Shortcut1;
        [ProtoMember(7)] public int Shortcut2;
        [ProtoMember(8)] public int Shortcut3;
        [ProtoMember(9)] public int Shortcut4;

    }

    [ProtoContract]
    public class InventoryShortcutRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int SlotIndex;
        [ProtoMember(4)] public int ItemName;
    }

    [ProtoContract]
    public class InventoryShortcutResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int InventoryShortcut1;
        [ProtoMember(4)] public int InventoryShortcut2;
        [ProtoMember(5)] public int InventoryShortcut3;
        [ProtoMember(6)] public int InventoryShortcut4;
        [ProtoMember(7)] public int CurrentGrippingItemId;
    }

    [ProtoContract]
    public class ShortcutSwitchRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int SlotIndex;
    }

    [ProtoContract]
    public class ShortcutSwitchResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int SlotIndex;
        [ProtoMember(4)] public int CurrentGrippingItemId;
        [ProtoMember(5)] public int CurrentGrippingItemType;
    }
    
    [ProtoContract]
    public class BuildingBtnRequestpacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
    }

    [ProtoContract]
    public class BuildingBtnResponsePacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int ItemName;
        [ProtoMember(4)] public int Amount;
    }

    // ── 인터셉트 미사일 타겟팅 ───────────────────────────────────────────

    [ProtoContract]
    public class InterceptTargetRequestPacket
    {
        [ProtoMember(1)] public int OwnerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int TargetNetworkId;
        [ProtoMember(4)] public int TargetType;
        [ProtoMember(5)] public int BuildingId;
    }

    [ProtoContract]
    public class InterceptTargetEventPacket
    {
        [ProtoMember(1)] public int BuildingId;
        [ProtoMember(2)] public int TargetNetworkId;
        [ProtoMember(3)] public int TargetType;
    }

    [ProtoContract]
    public class InterceptRotationPacket
    {
        [ProtoMember(1)] public int   OwnerId;
        [ProtoMember(2)] public int   FieldId;
        [ProtoMember(3)] public int   BuildingId;
        [ProtoMember(4)] public float AimRotY;
    }

    /// <summary>IsMine Controller가 조준 딜레이 후 전체 브로드캐스트 → 모든 클라 Launch</summary>
    [ProtoContract]
    public class InterceptFirePacket
    {
        [ProtoMember(1)] public int OwnerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int BuildingId;
        [ProtoMember(4)] public int TargetNetworkId;
        [ProtoMember(5)] public int TargetType;
        [ProtoMember(6)] public int LaunchSpotIdx;
        [ProtoMember(7)] public int MissileType;
    }

    // ── 새 피격 / 사망 시스템 ────────────────────────────────────────────

    [ProtoContract]
    public class HitRequestPacket
    {
        [ProtoMember(1)]  public int   AttackerId;
        [ProtoMember(2)]  public int   FieldId;
        [ProtoMember(3)]  public int   WeaponItemName; // ItemName enum 값
        [ProtoMember(4)]  public int   TargetId;
        [ProtoMember(5)]  public int   TargetType;     // HitTargetType
        [ProtoMember(6)]  public float HitPosX;
        [ProtoMember(7)]  public float HitPosY;
        [ProtoMember(8)]  public float HitPosZ;
        [ProtoMember(9)]  public float HitNormalX;
        [ProtoMember(10)] public float HitNormalY;
        [ProtoMember(11)] public float HitNormalZ;
    }

    [ProtoContract]
    public class DeathRequestPacket
    {
        [ProtoMember(1)] public int TargetId;
        [ProtoMember(2)] public int TargetType;  // HitTargetType
        [ProtoMember(3)] public int AttackerId;
        [ProtoMember(4)] public int FieldId;
    }

    [ProtoContract]
    public class ExceptionPacket
    {
        [ProtoMember(1)] public string ErrorMessage;
    }


    [ProtoContract]
    public class ArtilleryInfoRequestPacket
    {
        [ProtoMember(1)] public int PlayerId;
        [ProtoMember(2)] public int FieldId;
        [ProtoMember(3)] public int BuildingId;
    }

    [ProtoContract]
    public class ArtilleryInfoResponsePacket
    {
        [ProtoMember(1)] public int BuildingId;
        [ProtoMember(2)] public int NormalMissileCount;
        [ProtoMember(3)] public int NuclearMissileCount;
    }
}
