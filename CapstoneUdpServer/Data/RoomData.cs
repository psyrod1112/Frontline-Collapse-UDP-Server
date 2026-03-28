using System;
using System.Collections.Concurrent;

namespace CapstoneUdpServer.Data;

public class RoomData
{
    public int OwnerId { get; private set; }
    public int RoomId { get; private set; }
    public string? RoomName { get; private set; }
    public int RoomPlayerLimit { get; private set; }
    public int CurrentRoomPlayers { get; private set; }

    public ConcurrentDictionary<int, PlayerData> InRoomPlayers { get; } = new();

    public RoomData(int ownerId, int roomId, string roomName, int roomPlayerLimit, int currentRoomPlayers)
    {
        OwnerId = ownerId;
        RoomId = roomId;
        RoomName = roomName;
        RoomPlayerLimit = roomPlayerLimit;
        CurrentRoomPlayers = currentRoomPlayers;
    }

    public void AddPlayer(PlayerData playerData)
    {
        if (InRoomPlayers.TryAdd(playerData.PlayerId, playerData))
        {
            CurrentRoomPlayers++;
            Console.WriteLine($"[서버] AddPlayer : {playerData.PlayerId}님이 {RoomId}번 룸에 입장하였습니다.");
        }
    }

    public PlayerData? RemovePlayer(int playerId)
    {
        if (InRoomPlayers.TryRemove(playerId, out PlayerData playerData))
        {
            CurrentRoomPlayers--;
            Console.WriteLine($"[서버] RemovePlayer : {playerData.PlayerId}님이 {RoomId}번 룸에서 퇴장하였습니다.");
            return playerData;
        }

        Console.WriteLine($"[서버] RemovePlayer : 플레이어가 없습니다.");
        return null;
    }
}
