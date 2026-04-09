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
        BasePacket? header   = JsonSerializer.Deserialize<BasePacket>(jsonData, JsonOpts.Default);

        Console.WriteLine($"[InGameServer] 수신: {header?.Type2} from {clientEp}");

        switch (header?.Type2)
        {
            case InGamePacketType.SpawnPlayerUnit:
                break;

            case InGamePacketType.MovePlayer:
                HandleMovePlayer(buffer, bufferSize,
                    JsonSerializer.Deserialize<MovePacket>(jsonData, JsonOpts.Default));
                break;

            case InGamePacketType.BulletFire:
                HandleBulletFire(buffer, bufferSize,
                    JsonSerializer.Deserialize<BulletFirePacket>(jsonData, JsonOpts.Default));
                break;

            case InGamePacketType.BulletHit:
                HandleBulletHit(
                    JsonSerializer.Deserialize<BulletHitPacket>(jsonData, JsonOpts.Default));
                break;

            case InGamePacketType.BuildingCreate:
                HandleBuildingCreate(buffer, bufferSize,
                    JsonSerializer.Deserialize<BuildingCreatePacket>(jsonData, JsonOpts.Default));
                break;

            case InGamePacketType.BuildingDestroy:
                HandleBuildingDestroy(buffer, bufferSize,
                    JsonSerializer.Deserialize<BuildingDestroyPacket>(jsonData, JsonOpts.Default));
                break;

            case InGamePacketType.MissileFire:
                HandleMissileFire(buffer, bufferSize,
                    JsonSerializer.Deserialize<MissileFirePacket>(jsonData, JsonOpts.Default));
                break;

            case InGamePacketType.MissileExplosion:
                HandleMissileExplosion(
                    JsonSerializer.Deserialize<MissileExplosionPacket>(jsonData, JsonOpts.Default));
                break;

            default:
                Console.WriteLine($"[InGameServer] 알 수 없는 패킷 타입: {header?.Type2}");
                break;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 핸들러

    /// <summary>이동 패킷: 위치 업데이트 후 같은 필드 전체에 relay</summary>
    private void HandleMovePlayer(byte[] rawBuf, int rawLen, MovePacket? packet)
    {
        if (packet == null) return;
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        // 서버 위치 업데이트
        if (inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit))
        {
            unit.Position = packet.Position;
            unit.Rotation = packet.Rotation;
        }

        BroadcastToField(rawBuf, rawLen, packet.FieldId, excludePlayerId: packet.PlayerId);
    }

    /// <summary>총구 이펙트: 보낸 사람 제외 relay</summary>
    private void HandleBulletFire(byte[] rawBuf, int rawLen, BulletFirePacket? packet)
    {
        if (packet == null) return;
        BroadcastToField(rawBuf, rawLen, packet.FieldId, excludePlayerId: packet.PlayerId);
    }

    /// <summary>피격: 데미지 적용 → 결과 브로드캐스트</summary>
    private void HandleBulletHit(BulletHitPacket? packet)
    {
        if (packet == null) return;
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        float remainHp = packet.CurrentHp; // 기본값: 패킷에 담긴 현재 HP 사용

        switch (packet.HitTargetType)
        {
            case HitTargetType.Player:
            case HitTargetType.MovingUnit:
                if (inGameData.PlayerUnitDataMap.TryGetValue(packet.TargetId, out var target))
                {
                    target.CurrentHp -= packet.Damage;
                    remainHp          = target.CurrentHp;
                    Console.WriteLine($"[InGameServer] Player {packet.TargetId} HP: {remainHp}");
                }
                break;

            case HitTargetType.Building:
                if (inGameData.BuildingMap.TryGetValue(packet.TargetId, out var building))
                {
                    building.CurrentHp -= packet.Damage;
                    remainHp            = building.CurrentHp;
                    Console.WriteLine($"[InGameServer] Building {packet.TargetId} HP: {remainHp}");
                }
                break;
        }

        // 서버 확정 HP를 담아 브로드캐스트
        packet.CurrentHp = remainHp;
        Send(packet, packet.FieldId);
    }

    /// <summary>건물 생성: 서버에 기록 후 relay</summary>
    private void HandleBuildingCreate(byte[] rawBuf, int rawLen, BuildingCreatePacket? packet)
    {
        if (packet == null) return;
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        inGameData.BuildingMap[packet.BuildingId] = new InGameBuildingRecord
        {
            BuildingId  = packet.BuildingId,
            OwnerId     = packet.PlayerId,
            PrefabIndex = packet.PrefabIndex,
            MaxHp       = packet.MaxHp,
            CurrentHp   = packet.MaxHp,
            Position    = packet.Position,
        };

        Console.WriteLine($"[InGameServer] Building {packet.BuildingId} 등록 (fieldId:{packet.FieldId})");
        BroadcastToField(rawBuf, rawLen, packet.FieldId, excludePlayerId: packet.PlayerId);
    }

    /// <summary>건물 파괴: 서버에서 제거 후 relay</summary>
    private void HandleBuildingDestroy(byte[] rawBuf, int rawLen, BuildingDestroyPacket? packet)
    {
        if (packet == null) return;
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        if (inGameData.BuildingMap.TryRemove(packet.BuildingId, out _))
            Console.WriteLine($"[InGameServer] Building {packet.BuildingId} 제거 (fieldId:{packet.FieldId})");

        BroadcastToField(rawBuf, rawLen, packet.FieldId, excludePlayerId: packet.PlayerId);
    }

    /// <summary>미사일 발사: 보낸 사람 제외 relay</summary>
    private void HandleMissileFire(byte[] rawBuf, int rawLen, MissileFirePacket? packet)
    {
        if (packet == null) return;
        BroadcastToField(rawBuf, rawLen, packet.FieldId, excludePlayerId: packet.PlayerId);
    }

    /// <summary>미사일 폭발: HitList 순회하며 데미지 적용 → 결과 브로드캐스트</summary>
    private void HandleMissileExplosion(MissileExplosionPacket? packet)
    {
        if (packet == null) return;
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        foreach (var hit in packet.HitList)
        {
            switch (hit.TargetType)
            {
                case HitTargetType.Player:
                case HitTargetType.MovingUnit:
                    if (inGameData.PlayerUnitDataMap.TryGetValue(hit.TargetId, out var p))
                        p.CurrentHp -= hit.Damage;
                    break;
                case HitTargetType.Building:
                    if (inGameData.BuildingMap.TryGetValue(hit.TargetId, out var b))
                        b.CurrentHp -= hit.Damage;
                    break;
            }
        }

        Console.WriteLine($"[InGameServer] MissileExplosion 처리 hitCount:{packet.HitList.Count}");
        Send(packet, packet.FieldId);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region 송신 유틸

    /// <summary>직렬화 후 필드 전체 브로드캐스트</summary>
    private void Send<T>(T packet, int fieldId) where T : InGamePacket
    {
        byte[] buf = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, JsonOpts.Default));
        BroadcastRawToField(buf, fieldId, excludePlayerId: -1);
    }

    /// <summary>수신 raw 버퍼를 보낸 사람 제외하고 브로드캐스트 (재직렬화 없음)</summary>
    private void BroadcastToField(byte[] buf, int len, int fieldId, int excludePlayerId)
    {
        byte[] sendBuf = new byte[len];
        Array.Copy(buf, sendBuf, len);
        BroadcastRawToField(sendBuf, fieldId, excludePlayerId);
    }

    private void BroadcastRawToField(byte[] buf, int fieldId, int excludePlayerId)
    {
        if (!_inGameDataList.TryGetValue(fieldId, out var inGameData)) return;
        foreach (var unit in inGameData.PlayerUnitDataMap.Values)
        {
            if (unit.PlayerId == excludePlayerId) continue;
            _socket.SendTo(buf, unit.IpEndPoint);
        }
    }

    #endregion
}
