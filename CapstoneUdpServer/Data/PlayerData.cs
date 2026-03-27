
using System.Net;

namespace CapstoneUdpServer.Data
{
    public enum PlayerRank
    {
        Sergeant = 0,
        Captain = 1,
        Colonel = 2,
        General = 3
    }


    public class PlayerData
    {
        #region 프로퍼티 변수
    
        private string? _playerName;
        private int _playerId;
        private int _winScore;
        private float _winRate;
        private PlayerRank _playerRank;
        private int _relatedRoomId;

        public bool IsGameReady = false;

        private EndPoint _clientEp;
        
        public string? PlayerName => _playerName;
        public int PlayerId => _playerId;
        public int WinScore => _winScore;
        public float WinRate => _winRate;
        public PlayerRank PlayerRank => _playerRank;
        public int RelatedRoomId => _relatedRoomId;
        public EndPoint ClientEp => _clientEp;

        #endregion
    
        #region 생성자

        public PlayerData(string? playerName, int playerId, int winScore, 
            float winRate, PlayerRank playerRank, IPEndPoint clientEp)
        {
            _playerName = playerName;
            _playerId = playerId;
            _winScore = winScore;
            _winRate = winRate;
            _playerRank = playerRank;
            
            _clientEp = clientEp;
        }

        #endregion

    
    
        #region 변수 변경 메서드

        public void PlayerWhereRoom(int roomId)
        {
            _relatedRoomId = roomId;
        }
    
    

        #endregion

    }
}
