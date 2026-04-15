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
                default:
                    Console.WriteLine($"[InGameServer] 알 수 없는 JSON 패킷 타입: {header?.Type2}");
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
                case (uint)InGamePacketType.UIUpdateRequest:
                    var uiReqPacket = ProtobufSerializer.Deserialize<UIUpdateRequestPacket>(buffer);
                    HandleUIUpdateRequest(uiReqPacket, clientEp);
                    break;
            }
        }
        
    }


    private void HandleUIUpdateRequest(UIUpdateRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        SendProto((uint)InGamePacketType.UIUpdateResponse, new UIUpdateResponsePacket
        {
            PlayerId            = unit.PlayerId,
            FieldId             = unit.FieldId,
            PlayerName          = unit.PlayerName ?? "",
            PlayerRank          = (int)unit.PlayerRank,
            Gold                = unit.Gold,
            Level               = unit.Level,
            CurrentHp           = unit.CurrentHp,
            MaxHp               = unit.MaxHp,
            Exp                 = unit.Exp,
            RequiredExp         = unit.RequiredExp,
            WeaponPrefabIndex_1 = (int)unit.WeaponPrefabIndex_1,
            WeaponPrefabIndex_2 = (int)unit.WeaponPrefabIndex_2,
            WeaponPrefabIndex_3 = (int)unit.WeaponPrefabIndex_3,
            WeaponPrefabIndex_4 = (int)unit.WeaponPrefabIndex_4,
            KillCount           = unit.KillCount,
            DeathCount          = unit.DeathCount,
            CSCount             = unit.CSCount,
        }, clientEp);
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
            if (inGamePlayer.FieldId == PlayerData.FieldId && inGamePlayer.PlayerId != packet.PlayerId)
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
