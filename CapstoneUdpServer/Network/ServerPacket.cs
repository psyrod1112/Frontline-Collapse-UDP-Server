using System;
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
    
    //UI 업데이트
    UIUpdateRequest,
    UIUpdateResponse,
    
    //플레이어 입력
    PlayerInput,
    MoveConfirm,
    RemotePlayerState,
    
    NpcState,
    NpcChaseEvent,

    SpawnNpc,

    // 이벤트 (클라 → 서버 → 브로드캐스트)
    FireEvent,
    MeleeEvent,
    ReloadEvent,
    WeaponChange,
    BuildingPlace,
    BuildingDestroy,
    DamageEvent,
    DeathEvent,
    DeathUpdate,
    RewardUpdate,
    MissileExplode,
    HotkeySlotSave,
    MissileLoadRequest,
    MissileLoadResponse,
    MissileLaunch,
    MissileHitRequest,
    DamageResult,
    RespawnRequest,
    RespawnResponse,
    
    ScoreBoardRequest,
    ScoreBoardResponse,
    
    BuyRequest,
    BuyResponse,
    InventoryRequest,
    InventoryResponse,

    InventoryShortcutRequest,
    InventoryShortcutResponse,
    
    ShortcutSwitchRequest,
    ShortcutSwitchResponse,
    BuildingBtnRequest,
    BuildingBtnResponse,
    AnimTrigger,
    HitRequest,
    DeathRequest,
}

#region 로비 패킷

public class BasePacket
{
    public LobbyPacketType  Type           { get; set; }
    public InGamePacketType Type2          { get; set; }
    public string?          LastUpdateTime { get; set; }
}

public class ErrorPacket : BasePacket
{
    public string ErrorMessage { get; set; }
    
}

public class LobbyPacket : BasePacket
{
    public int        PlayerId      { get; set; }
}

public class PlayerPacket : LobbyPacket
{
    public string?    PlayerName    { get; set; }
    public int        WinScore      { get; set; }
    public float      WinRate       { get; set; }
    public PlayerRank PlayerRank    { get; set; }
    public int        RelatedRoomId { get; set; }
}

public class RoomPacket : LobbyPacket
{
    public int     RoomId             { get; set; }
    public string? RoomName           { get; set; }
    public int     RoomPlayerLimit    { get; set; }
    public int     CurrentRoomPlayers { get; set; }
}

public class GamelogPacket : LobbyPacket
{
    public int            LogId        { get; set; }
    public string?        MyName       { get; set; }
    public PlayerRank     MyRank       { get; set; }
    public int            EnemyId      { get; set; }
    public string?        EnemyName    { get; set; }
    public PlayerRank     EnemyRank    { get; set; }
    public bool           GameResult   { get; set; }
    public DateTimeOffset GameOverTime { get; set; }
}

#endregion

