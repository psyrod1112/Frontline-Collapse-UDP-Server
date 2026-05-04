using System;
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
                case (uint)InGamePacketType.FireEvent:
                    var firePacket = ProtobufSerializer.Deserialize<FireEventPacket>(buffer);
                    HandleFireEvent(firePacket);
                    break;
                case (uint)InGamePacketType.WeaponChange:
                    var wcPacket = ProtobufSerializer.Deserialize<WeaponChangePacket>(buffer);
                    HandleWeaponChange(wcPacket);
                    break;
                case (uint)InGamePacketType.BuildingPlace:
                    var buildingPlacePacket = ProtobufSerializer.Deserialize<BuildingPlacePacket>(buffer);
                    HandleBuildingPlace(buildingPlacePacket);
                    break;
                case (uint)InGamePacketType.BuildingDestroy:
                    var buildingDestroyPacket = ProtobufSerializer.Deserialize<BuildingDestroyPacket>(buffer);
                    HandleBuildingDestroy(buildingDestroyPacket);
                    break;
                case (uint)InGamePacketType.HotkeySlotSave:
                    var hsPacket = ProtobufSerializer.Deserialize<HotkeySavePacket>(buffer);
                    HandleHotkeySlotSave(hsPacket);
                    break;
                case (uint)InGamePacketType.MissileLoadRequest:
                    var mlPacket = ProtobufSerializer.Deserialize<MissileLoadRequestPacket>(buffer);
                    HandleMissileLoadRequest(mlPacket, clientEp);
                    break;
                case (uint)InGamePacketType.MissileLaunch:
                    var launchPacket = ProtobufSerializer.Deserialize<MissileLaunchPacket>(buffer);
                    HandleMissileLaunch(launchPacket);
                    break;
                case (uint)InGamePacketType.MissileHitRequest:
                    var hitPacket = ProtobufSerializer.Deserialize<MissileHitRequestPacket>(buffer);
                    HandleMissileHitRequest(hitPacket);
                    break;
                case (uint)InGamePacketType.DamageEvent:
                    var damageEventPacket = ProtobufSerializer.Deserialize<DamageEventPacket>(buffer);
                    HandleDamageEvent(damageEventPacket);
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
    
    private void HandleBuildingPlace(BuildingPlacePacket packet)
    {
        if (!_store.TryGetInGame(packet.OwnerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.OwnerId, out var unit)) return;

        var buildingType = (BuildingType)packet.BuildingType;
        var stat         = CombatData.GetBuildingStat(buildingType);
        var record       = new InGameBuildingRecord
        {
            BuildingId = packet.BuildingId,
            OwnerId    = packet.OwnerId,
            PrefabIndex = packet.BuildingType,
            MaxHp      = stat.MaxHp,
            CurrentHp  = stat.MaxHp,
            Position   = new System.Numerics.Vector3(packet.PosX, packet.PosY, packet.PosZ),
        };
        unit.Buildings[packet.BuildingId]              = record;
        inGameData.BuildingOwnerIndex[packet.BuildingId] = packet.OwnerId;

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.BuildingPlace, packet);
        inGameData.Broadcast(_socket, buf, excludePlayerId: packet.OwnerId);
    }

    private void HandleBuildingDestroy(BuildingDestroyPacket packet)
    {
        if (!_store.TryGetInGame(packet.DestroyerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        if (inGameData.TryGetBuilding(packet.BuildingId, out _, out var owner))
        {
            owner?.Buildings.TryRemove(packet.BuildingId, out _);
            inGameData.BuildingOwnerIndex.TryRemove(packet.BuildingId, out _);
        }

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.BuildingDestroy, packet);
        inGameData.Broadcast(_socket, buf, excludePlayerId: packet.DestroyerId);
    }

    private void HandleMissileLoadRequest(MissileLoadRequestPacket packet, IPEndPoint clientEp)
    {
        bool success = false;
        int remaining = 0;

        if (_store.TryGetInGame(packet.PlayerId, out var playerData)
            && _inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)
            && inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)
            && inGameData.TryGetBuilding(packet.BuildingId, out var building, out _)
            && building != null
            && building.OwnerId == packet.PlayerId
            && building.PrefabIndex == (int)BuildingType.Artillery)
        {
            var missileType = (WeaponType)packet.MissileType;
            if (packet.IsLoaded)
            {
                success = TryConsumeMissile(unit, missileType, out remaining);
                if (success)
                {
                    building.IsMissileLoaded = true;
                    building.LoadedMissileId = packet.MissileId;
                    building.LoadedMissileType = missileType;
                }
            }
            else
            {
                success = building.IsMissileLoaded && building.LoadedMissileId == packet.MissileId;
                if (success)
                {
                    ReturnMissile(unit, building.LoadedMissileType, out remaining);
                    building.IsMissileLoaded = false;
                    building.LoadedMissileId = 0;
                    building.LoadedMissileType = WeaponType.None;
                }
            }
        }

        SendProto((uint)InGamePacketType.MissileLoadResponse, new MissileLoadResponsePacket
        {
            PlayerId = packet.PlayerId,
            BuildingId = packet.BuildingId,
            MissileId = packet.MissileId,
            MissileType = packet.MissileType,
            IsLoaded = success && packet.IsLoaded,
            Success = success,
            RemainingMissileCount = remaining,
        }, clientEp);
    }

    private void HandleMissileLaunch(MissileLaunchPacket packet)
    {
        if (!_store.TryGetInGame(packet.OwnerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        if (inGameData.TryGetBuilding(packet.BuildingId, out var building, out _) && building != null)
        {
            building.IsMissileLoaded = false;
            building.LoadedMissileId = 0;
            building.LoadedMissileType = WeaponType.None;
        }

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.MissileLaunch, packet);
        inGameData.Broadcast(_socket, buf, excludePlayerId: packet.OwnerId);
    }

    private void HandleMissileHitRequest(MissileHitRequestPacket packet)
    {
        if (!_store.TryGetInGame(packet.OwnerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        var missileType = (WeaponType)packet.MissileType;

        ApplyMissileDamage(inGameData, packet.OwnerId, packet.DirectTargetId, packet.DirectTargetType, missileType);
        foreach (var target in packet.SplashTargets)
            ApplyMissileDamage(inGameData, packet.OwnerId, target.TargetId, target.TargetType, missileType);
    }

    private void ApplyMissileDamage(
        InGameData inGameData,
        int attackerId,
        int targetId,
        int targetTypeValue,
        WeaponType weaponType)
    {
        if (targetId == 0) return;

        var targetType = (CapstoneUdpServer.Network.HitTargetType)targetTypeValue;
        int rawDamage = CombatData.GetWeaponDamage(weaponType);
        float currentHp;
        float maxHp;
        int finalDamage;

        if (targetType == CapstoneUdpServer.Network.HitTargetType.Building)
        {
            if (!inGameData.TryGetBuilding(targetId, out var building, out _) || building == null) return;

            var buildingType = (BuildingType)building.PrefabIndex;
            int defense = CombatData.GetBuildingStat(buildingType).Defense;
            finalDamage = Math.Max(1, rawDamage - defense);
            building.CurrentHp = Math.Max(0f, building.CurrentHp - finalDamage);
            currentHp = building.CurrentHp;
            maxHp = building.MaxHp;
        }
        else
        {
            if (!inGameData.PlayerUnitDataMap.TryGetValue(targetId, out var unit)) return;

            int defense = targetType == CapstoneUdpServer.Network.HitTargetType.Player ? 5 : 10;
            finalDamage = Math.Max(1, rawDamage - defense);
            unit.CurrentHp = Math.Max(0f, unit.CurrentHp - finalDamage);
            currentHp = unit.CurrentHp;
            maxHp = unit.MaxHp;
        }

        BroadcastDamageResult(inGameData, new DamageResultPacket
        {
            AttackerId = attackerId,
            TargetId = targetId,
            TargetType = targetTypeValue,
            Damage = finalDamage,
            CurrentHp = currentHp,
            MaxHp = maxHp,
        });
    }

    private bool TryConsumeMissile(PlayerUnitData unit, WeaponType missileType, out int remaining)
    {
        remaining = 0;
        switch (missileType)
        {
            case WeaponType.MissileGuided when unit.GuidedMissileCount > 0:
                unit.GuidedMissileCount--;
                remaining = unit.GuidedMissileCount;
                return true;
            case WeaponType.MissileNuke when unit.NukeMissileCount > 0:
                unit.NukeMissileCount--;
                remaining = unit.NukeMissileCount;
                return true;
            default:
                return false;
        }
    }

    private void ReturnMissile(PlayerUnitData unit, WeaponType missileType, out int remaining)
    {
        switch (missileType)
        {
            case WeaponType.MissileGuided:
                unit.GuidedMissileCount++;
                remaining = unit.GuidedMissileCount;
                break;
            case WeaponType.MissileNuke:
                unit.NukeMissileCount++;
                remaining = unit.NukeMissileCount;
                break;
            default:
                remaining = 0;
                break;
        }
    }

    private void BroadcastDamageResult(InGameData inGameData, DamageResultPacket packet)
    {
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.DamageResult, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleWeaponChange(WeaponChangePacket packet)
    {
        if (!_store.TryGetInGame(packet.PlayerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.WeaponChange, packet);
        inGameData.Broadcast(_socket, buf, excludePlayerId: packet.PlayerId);
    }

    private void HandleHotkeySlotSave(HotkeySavePacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        unit.WeaponPrefabIndex_1 = (WeaponType)packet.Slot1;
        unit.WeaponPrefabIndex_2 = (WeaponType)packet.Slot2;
        unit.WeaponPrefabIndex_3 = (WeaponType)packet.Slot3;
        unit.WeaponPrefabIndex_4 = (WeaponType)packet.Slot4;

        Console.WriteLine($"[InGameServer] 핫키 저장: PlayerId={packet.PlayerId}, Slots={packet.Slot1}/{packet.Slot2}/{packet.Slot3}/{packet.Slot4}");
    }

    private void HandleFireEvent(FireEventPacket packet)
    {
        if (!_store.TryGetInGame(packet.PlayerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        // 발사자 포함 전원에게 브로드캐스트 — 발사자 본인도 애니메이션/사운드 처리
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.FireEvent, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleDamageEvent(DamageEventPacket packet)
    {
        if (!_store.TryGetInGame(packet.AttackerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        var weaponType = (WeaponType)packet.WeaponType;
        var targetType = (CapstoneUdpServer.Network.HitTargetType)packet.TargetType;
        int rawDamage  = CombatData.GetWeaponDamage(weaponType);

        float currentHp, maxHp;
        int   finalDamage;

        if (targetType == CapstoneUdpServer.Network.HitTargetType.Building)
        {
            if (!inGameData.TryGetBuilding(packet.TargetId, out var building, out _) || building == null) return;

            var buildingType = (BuildingType)building.PrefabIndex;
            int defense = CombatData.GetBuildingStat(buildingType).Defense;
            finalDamage     = Math.Max(1, rawDamage - defense);
            building.CurrentHp = Math.Max(0f, building.CurrentHp - finalDamage);
            currentHp = building.CurrentHp;
            maxHp     = building.MaxHp;
        }
        else
        {
            if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.TargetId, out var unit)) return;

            int defense = targetType == CapstoneUdpServer.Network.HitTargetType.Player ? 5 : 10;
            finalDamage  = Math.Max(1, rawDamage - defense);
            unit.CurrentHp = Math.Max(0f, unit.CurrentHp - finalDamage);
            currentHp = unit.CurrentHp;
            maxHp     = unit.MaxHp;
        }

        BroadcastDamageResult(inGameData, new DamageResultPacket
        {
            AttackerId = packet.AttackerId,
            TargetId   = packet.TargetId,
            TargetType = packet.TargetType,
            Damage     = finalDamage,
            CurrentHp  = currentHp,
            MaxHp      = maxHp,
            HitPosX    = packet.HitPosX,
            HitPosY    = packet.HitPosY,
            HitPosZ    = packet.HitPosZ,
            HitNormalX = packet.HitNormalX,
            HitNormalY = packet.HitNormalY,
            HitNormalZ = packet.HitNormalZ,
            // TODO: IsDead — currentHp <= 0 시 true 설정 및 DeathEventPacket 전송
        });

        // TODO: 사망 처리 — currentHp <= 0 시 DeathEventPacket 전체 브로드캐스트, GoldUpdatePacket 공격자에게 전송
    }

    private void HandlePlayerInput(PlayerInputPacket packet, IPEndPoint clientEp)
    {
        if (!_store.TryGetInGame(packet.PlayerId, out var playerData))
        {
            Console.WriteLine("[HandlePlayerInput] 플레이어 데이터가 없습니다!");
            return;
        }
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        SendProto((uint)InGamePacketType.MoveConfirm, new PlayerMoveConfirmPacket
        {
            Tick      = packet.Tick,
            PlayerId  = packet.PlayerId,
            PosX      = packet.PosX,
            PosY      = packet.PosY,
            PosZ      = packet.PosZ,
            RotationY = packet.RotationY,
            AnimState = packet.AnimState,
        }, clientEp);

        byte[] remoteBuf = ProtobufSerializer.Serialize((uint)InGamePacketType.RemotePlayerState, new RemotePlayerStatePacket
        {
            Tick        = packet.Tick,
            PlayerId    = packet.PlayerId,
            PosX        = packet.PosX,
            PosY        = packet.PosY,
            PosZ        = packet.PosZ,
            RotationY   = packet.RotationY,
            CameraPitch = packet.CameraPitch,
            AnimState   = packet.AnimState,
            WeaponIndex = packet.WeaponIndex,
            IsCrouching = packet.IsCrouching,
        });
        inGameData.Broadcast(_socket, remoteBuf, excludePlayerId: packet.PlayerId);
    }

    private void SendProto<T>(uint packetType, T message, IPEndPoint ep)
    {
        byte[] buffer = ProtobufSerializer.Serialize(packetType, message);
        _socket.SendTo(buffer, ep);
    }
}
