using System.Net;

namespace CapstoneUdpServer.Data;

public enum PlayerRank
{
    Sergeant,
    Captain,
    Colonel,
    General,
    None
}

public class PlayerData
{
    public string? PlayerName  { get; private set; }
    public int     PlayerId    { get; private set; }
    public PlayerRank PlayerRank { get; private set; }
    public int     RelatedRoomId { get; private set; }
    public int      FieldId   { get; private set; }
    public EndPoint ClientEp  { get; private set; }
    public bool    IsGameReady;

    public PlayerData(string? playerName, int playerId, PlayerRank playerRank, IPEndPoint clientEp)
    {
        PlayerName = playerName;
        PlayerId   = playerId;
        PlayerRank = playerRank;
        ClientEp   = clientEp;
    }

    public void PlayerWhereRoom(int roomId) => RelatedRoomId = roomId;
    
    public void SetFieldId(int relatedRoomId)
    {
        FieldId = relatedRoomId;
    }
}
