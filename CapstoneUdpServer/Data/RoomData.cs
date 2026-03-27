using System;
using System.Collections.Concurrent;

namespace CapstoneUdpServer.Data;

public class RoomData
{
    #region 프로퍼티/변수 

    private int _ownerId;
    private int _roomId;
    private string _roomName;
    private int _roomPlayerLimit;
    private int _currentRoomPlayers;
    
    
    public int OwnerId => _ownerId;
    public int RoomId => _roomId;
    public string? RoomName => _roomName;
    public int RoomPlayerLimit => _roomPlayerLimit;
    public int CurrentRoomPlayers => _currentRoomPlayers;
    
    public ConcurrentDictionary<int, PlayerData> InRoomPlayers = new ConcurrentDictionary<int, PlayerData>();


    #endregion

    #region 생성자

    public RoomData(int ownerId, int roomId, string roomName, int roomPlayerLimit, int currentRoomPlayers)
    {
        _ownerId = ownerId;
        _roomId = roomId;
        _roomName = roomName;
        _roomPlayerLimit = roomPlayerLimit;
        _currentRoomPlayers = currentRoomPlayers;
    }

    #endregion
    
    
    public void AddPlayer(PlayerData playerData)
    {
        if (InRoomPlayers.TryAdd(playerData.PlayerId, playerData))
        {
            _currentRoomPlayers++;
            Console.WriteLine($"[서버] AddPlayer : {playerData.PlayerId}님이 {RoomId}번 룸에 입장하였습니다.");
        }
    }

    public PlayerData? RemovePlayer(int playerId)
    {
        if (InRoomPlayers.TryRemove(playerId, out PlayerData playerData))
        {
            _currentRoomPlayers--;
            Console.WriteLine($"[서버] RemovePlayer : {playerData.PlayerId}님이 {RoomId}번 룸에서 퇴장하였습니다.");
            return playerData;
        }
        
        Console.WriteLine($"[서버] RemovePlayer : 플레이어가 없습니다.");
        return null;
    }
    
}