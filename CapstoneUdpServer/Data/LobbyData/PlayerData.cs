using System.Net;

namespace CapstoneUdpServer.Data;

public enum PlayerRank
{
    Sergeant = 0,
    Captain = 1,
    Colonel = 2,
    General = 3
}

public class PlayerData
{
    public string? PlayerName { get; private set; }
    public int PlayerId { get; private set; }
    public int RelatedRoomId { get; private set; }
    public EndPoint ClientEp { get; private set; }
    public bool IsGameReady;

    public PlayerData(string? playerName, int playerId, IPEndPoint clientEp)
    {
        PlayerName = playerName;
        PlayerId = playerId;
        ClientEp = clientEp;
    }

    public void PlayerWhereRoom(int roomId)
    {
        RelatedRoomId = roomId;
    }
}
