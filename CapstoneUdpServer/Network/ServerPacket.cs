using System.Numerics;

namespace CapstoneUdpServer.Data;

public enum LobbyPacketType
{
    None,
    //플레이어 관련
    Connection,
    Disconnection,
    Spawn,
    Despawn,

    //룸 관련
    CreateRequest,
    CreateRoom,
    AddPlayerRequest,
    AddPlayerRoom,
    RemovePlayerRoom,
    DestroyRoom,
    EnterRequest,
    EnterRoom,
    ExitRequest,
    ExitRoom,
    RoomUpdate,

    //전적 관련
    ShowGamelogsRequest,
    ShowPlayerInfo,
    ShowGamelogsResponse,

    //게임 관련
    GameStart,
    GameReady,
    GameOver,

    //하트비트
    Heartbeat
}

public enum InGamePacketType
{
    None,
    // 스폰
    SpawnPlayerUnit,
    GameOver,

    // 이동
    MovePlayer,

    // 총알
    BulletFire,
    BulletHit,

    // 건물
    BuildingCreate,
    BuildingDestroy,

    // 미사일
    MissileFire,
    MissileExplosion,
}

public enum HitTargetType  { Player, MovingUnit, Building, Environment }
public enum PlayerAnimState { Idle, Walk, Run, Jump, Crouch, Attack, Die }

#region 로비 패킷

public class BasePacket
{
    public LobbyPacketType  Type           { get; set; }
    public InGamePacketType Type2          { get; set; }
    public string?          LastUpdateTime { get; set; }
}

public class PlayerPacket : BasePacket
{
    public int        PlayerId      { get; set; }
    public string?    PlayerName    { get; set; }
    public int        WinScore      { get; set; }
    public float      WinRate       { get; set; }
    public PlayerRank PlayerRank    { get; set; }
    public int        RelatedRoomId { get; set; }
}

public class RoomPacket : BasePacket
{
    public int     OwnerId            { get; set; }
    public int     RoomId             { get; set; }
    public string? RoomName           { get; set; }
    public int     RoomPlayerLimit    { get; set; }
    public int     CurrentRoomPlayers { get; set; }
}

public class GamelogPacket : BasePacket
{
    public int            LogId        { get; set; }
    public int            MyId         { get; set; }
    public string?        MyName       { get; set; }
    public PlayerRank     MyRank       { get; set; }
    public int            EnemyId      { get; set; }
    public string?        EnemyName    { get; set; }
    public PlayerRank     EnemyRank    { get; set; }
    public bool           GameResult   { get; set; }
    public DateTimeOffset GameOverTime { get; set; }
}

#endregion

#region 인게임 패킷 (기반)

public class InGamePacket : BasePacket
{
    public int     FieldId       { get; set; }
    public int     PlayerId      { get; set; }
    public float   CurrentHp     { get; set; }
    public float   DamageAmount  { get; set; }
    public float   DamagedAmount { get; set; }
    public Vector3 Position      { get; set; }
    public Vector3 Rotation      { get; set; }
    public int     PrefabIndex   { get; set; }
}

public class PlayerUnitPacket : InGamePacket
{
    public Vector3         Velocity      { get; set; }
    public PlayerAnimState AnimState     { get; set; }
    public int             WeaponIndex   { get; set; }
    public int             BuildingIndex { get; set; }
}

public class NpcUnitPacket : InGamePacket
{
    public int NpcUnitId      { get; set; }
    public int DropGolds      { get; set; }
    public int DropItemIndex  { get; set; }
}

public class BuildingUnitPacket : InGamePacket
{
    public int BuildingUnitId { get; set; }
    public int MissileIndex   { get; set; }
}

#endregion

#region 인게임 패킷 (동작)

/// <summary>Position/Rotation = 현재 위치/방향, Velocity = 이동속도벡터</summary>
public class MovePacket : InGamePacket
{
    public Vector3         Velocity  { get; set; }
    public PlayerAnimState AnimState { get; set; }
}

/// <summary>총구 이펙트 브로드캐스트용. Position = 총구 위치, Rotation = 발사 방향</summary>
public class BulletFirePacket : InGamePacket { }

/// <summary>Raycast 피격 결과. 서버가 데미지 계산 후 브로드캐스트</summary>
public class BulletHitPacket : InGamePacket
{
    public HitTargetType HitTargetType { get; set; }
    public int           TargetId      { get; set; }
    public Vector3       HitPoint      { get; set; }
    public float         Damage        { get; set; }
}

/// <summary>Position = 배치 좌표, PrefabIndex = 건물 종류, Rotation = 방향</summary>
public class BuildingCreatePacket : InGamePacket
{
    public int   BuildingId { get; set; }
    public float MaxHp      { get; set; }
}

public class BuildingDestroyPacket : InGamePacket
{
    public int BuildingId { get; set; }
}

/// <summary>Position = 발사 위치, Rotation = 발사 방향, PrefabIndex = 미사일 종류</summary>
public class MissileFirePacket : InGamePacket
{
    public int     MissileId { get; set; }
    public Vector3 Force     { get; set; }
}

/// <summary>폭발 범위 내 다중 피격을 하나의 패킷으로 전송</summary>
public class MissileExplosionPacket : InGamePacket
{
    public int           MissileId { get; set; }
    public List<HitInfo> HitList   { get; set; } = new();
}

public class HitInfo
{
    public HitTargetType TargetType { get; set; }
    public int           TargetId   { get; set; }
    public float         Damage     { get; set; }
}

#endregion
