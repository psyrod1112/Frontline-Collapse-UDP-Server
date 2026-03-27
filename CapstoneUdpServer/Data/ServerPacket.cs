
using System;

namespace CapstoneUdpServer.Data;

public enum PacketScene
{
    Lobby=0,
    InGame=1,
}
public enum PacketType
{
    //플레이어 관련
    Connection=0,
    Disconnection=1,
    Spawn=2,
    Despawn=3,
        
    //룸 관련
    CreateRequest=4,
    CreateRoom = 5,
    AddPlayerRequest=6,
    AddPlayerRoom=7,
    RemovePlayerRoom=8,
    DestroyRoom = 9,
    EnterRequest=10,
    EnterRoom=11,
    ExitRequest=12,
    ExitRoom=13,
    RoomUpdate=14,
        
    //게임 관련
    GameStart=15,
    GameOver=16,
    PlayerSpawn=17

}



public class BasePacket
{
    public PacketScene Scene {get; set;}
    public PacketType Type {get; set;}
}



public class PlayerPacket : BasePacket
{
    public int PlayerId {get; set;}
    public string? PlayerName  {get; set;}
    public int WinScore  {get; set;}
    public float WinRate   {get; set;}
    public PlayerRank PlayerRank  {get; set;}
    public int RelatedRoomId   {get; set;}

    public DateTime LastUpdateTime {get; set;}
}

public class RoomPacket : BasePacket
{

    public int OwnerId  {get; set;}
    public int RoomId   {get; set;}
    public string? RoomName   {get; set;}
    public int RoomPlayerLimit  {get; set;}
    public int CurrentRoomPlayers  {get; set;}

    public DateTime LastUpdateTime  {get; set;}
}






