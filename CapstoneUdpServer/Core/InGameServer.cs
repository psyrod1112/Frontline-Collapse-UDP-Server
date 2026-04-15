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
    private readonly ConcurrentDictionary<int, InGameData>  _inGameDataList;
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
        if (buffer[0] == '{')
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
        else
        {
            uint packetType = ProtobufSerializer.PeekType(buffer);
            switch (packetType)
            {
                case (uint)InGamePacketType.PlayerInput:
                    var packet = ProtobufSerializer.Deserialize<PlayerInputPacket>(buffer);
                    HandlePlayerInput(packet, clientEp);
                    break;
            }
        }
        
    }


    private void HandleUIUpdateRequest(UIPacket? deserialize)
    {
        Console.WriteLine("[InGameServer] UIUpdateRequest 수신 - 미구현");
    }
    
    private void HandlePlayerInput(PlayerInputPacket packet, IPEndPoint clientEp)
    {
        if(!_store.TryGetInGame(packet.PlayerId, out var PlayerData))
        {
            Console.WriteLine("[HandlePlayerInput] 플레이어 데이터가 없습니다!");
            return;
        }

        PlayerMoveConfirmPacket playerMoveConfirmPacket = new PlayerMoveConfirmPacket
        {
            Tick = packet.Tick,
            PlayerId = packet.PlayerId,
            PosX = packet.PosX,
            PosY = packet.PosY,
            PosZ = packet.PosZ,
            RotationY = packet.RotationY,
            AnimState = packet.AnimState,
        };
        SendProto((uint)InGamePacketType.MoveConfirm, playerMoveConfirmPacket, clientEp);

        RemotePlayerStatePacket remotePlayerStatePacket = new RemotePlayerStatePacket
        {
            Tick = packet.Tick,
            PlayerId = packet.PlayerId,
            PosX = packet.PosX,
            PosY = packet.PosY,
            PosZ = packet.PosZ,
            RotationY = packet.RotationY,
            AnimState = packet.AnimState,
        };

        foreach (var inGamePlayer in _store.InGamePlayers)
        {
            if (inGamePlayer.RelatedRoomId == PlayerData.FieldId && inGamePlayer.PlayerId != packet.PlayerId)
            {
                SendProto((uint)InGamePacketType.RemotePlayerState, remotePlayerStatePacket, (IPEndPoint)inGamePlayer.ClientEp);
            }
        }


    }

    private void SendProto<T>(uint packetType, T message, IPEndPoint ep)
    {
        byte[] buffer = ProtobufSerializer.Serialize(packetType, message);
        _socket.SendTo(buffer, ep);
    }
}
