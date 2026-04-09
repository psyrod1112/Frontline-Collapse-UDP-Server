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

    public void ProcessPacket(byte[] buffer, int bufferSize, IPEndPoint clientEp)
    {
        string      jsonData = Encoding.UTF8.GetString(buffer, 0, bufferSize);
        BasePacket? header   = JsonSerializer.Deserialize<BasePacket>(jsonData, JsonOpts.Default);

        Console.WriteLine($"[InGameServer] 수신: {header?.Type2} from {clientEp}");

        switch (header?.Type2)
        {
            case InGamePacketType.UIUpdateRequest:
                HandleUIUpdateRequest(JsonSerializer.Deserialize<UIPacket>(jsonData, JsonOpts.Default));
                break;

            default:
                Console.WriteLine($"[InGameServer] 알 수 없는 패킷 타입: {header?.Type2}");
                break;
        }
    }

    private void HandleUIUpdateRequest(UIPacket? deserialize)
    {
        throw new NotImplementedException();
    }
}
