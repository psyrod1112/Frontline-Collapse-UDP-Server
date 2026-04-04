using System.Numerics;

namespace CapstoneUdpServer.Data;

public enum PacketScene
{
    Lobby = 0,
    InGame = 1,
}

public enum LobbyPacketType
{
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
    //인게임 관련
    SpawnPlayerUnit,
    GameOver,
}

#region 로비 패킷

public class BasePacket
{
    public PacketScene Scene { get; set; }
    public LobbyPacketType Type { get; set; }
    public InGamePacketType Type2 { get; set; }
    public string LastUpdateTime { get; set; }
}

public class PlayerPacket : BasePacket
{
    public int PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public int WinScore { get; set; }
    public float WinRate { get; set; }
    public PlayerRank PlayerRank { get; set; }
    
    public int RelatedRoomId { get; set; }
}

public class RoomPacket : BasePacket
{
    public int OwnerId { get; set; }
    public int RoomId { get; set; }
    public string? RoomName { get; set; }
    public int RoomPlayerLimit { get; set; }
    public int CurrentRoomPlayers { get; set; }
}

public class GamelogPacket : BasePacket
{
    public int LogId { get; set; }

    public int MyId { get; set; }
    public string MyName { get; set; }
    public PlayerRank MyRank { get; set; }

    public int EnemyId { get; set; }
    public string EnemyName { get; set; }
    public PlayerRank EnemyRank { get; set; }

    public bool GameResult { get; set; }
    public DateTimeOffset GameOverTime { get; set; }
}

#endregion

#region 인게임 패킷

public class InGamePacket : BasePacket
{
    public int FieldId { get; set; }
    public int PlayerId { get; set; } //Lobby 의 PlayerId
    public float CurrentHp { get; set; }
    public float DamageAmount { get; set; }
    public float DamagedAmount { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public int PrefabIndex { get; set; }
}

public class PlayerUnitPacket : InGamePacket
{
    public int WeaponIndex { get; set; }
    public int BuildingIndex { get; set; }
}

public class NpcUnitPacket : InGamePacket
{
    public int NpcUnitId { get; set; }
    public int DropGolds { get; set; }
    public int DropItemIndex { get; set; }
}

public class BuildingUnitPacket : InGamePacket
{
    public int BuildingUnitId { get; set; }
    public int MissileIndex { get; set; }
}

#endregion
