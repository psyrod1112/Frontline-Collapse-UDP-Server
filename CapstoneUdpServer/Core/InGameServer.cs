using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CapstoneUdpServer.Data;
using CapstoneUdpServer.Network;

namespace CapstoneUdpServer.Core;

public class InGameServer
{
    private readonly Socket                                 _socket;
    private readonly ConcurrentDictionary<int, InGameData> _inGameDataList;
    private readonly PlayerStore                            _store;

    public InGameServer(
        Socket socket,
        ConcurrentDictionary<int, InGameData> inGameDataList,
        PlayerStore store)
    {
        _socket         = socket;
        _inGameDataList = inGameDataList;
        _store          = store;
    }

    // ─────────────────────────────────────────────────────────────
    #region 패킷 라우팅

    public void ProcessPacket(byte[] buffer, int bufferSize, IPEndPoint clientEp)
    {
        string      jsonData = Encoding.UTF8.GetString(buffer, 0, bufferSize);
        BasePacket? header   = JsonSerializer.Deserialize<BasePacket>(jsonData);

        Console.WriteLine($"[InGameServer] 수신: {jsonData}");

        switch (header?.Type2)
        {
            case InGamePacketType.SpawnPlayerUnit:
                // 클라이언트 Spawn 확인 시 처리
                break;

            // TODO: 이동, 공격, 피격 등 인게임 패킷 추가
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 송신 유틸

    private void Send<T>(T packet, IPEndPoint clientEp)
    {
        string json = JsonSerializer.Serialize(packet);
        byte[] buf  = Encoding.UTF8.GetBytes(json);
        _socket.SendTo(buf, clientEp);
        Console.WriteLine("[InGameServer] 전송: " + json);
    }

    /// <summary>같은 fieldId의 인게임 플레이어 전원에게 브로드캐스트</summary>
    private void BroadcastToField(byte[] buf, int fieldId)
    {
        if (!_inGameDataList.TryGetValue(fieldId, out var inGameData)) return;
        foreach (var unit in inGameData.PlayerUnitDataMap.Values)
            _socket.SendTo(buf, unit.IpEndPoint);
    }

    #endregion
}
