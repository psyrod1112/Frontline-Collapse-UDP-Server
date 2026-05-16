using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CapstoneUdpServer.Data;
using CapstoneUdpServer.Network;

namespace CapstoneUdpServer.Core;

public class InGameServer
{
    private readonly Socket                                 _socket;
    private readonly ConcurrentDictionary<int, InGameData>  _inGameDataList;
    private readonly PlayerStore                            _store;
    private volatile bool                                   _running = true;

    private static readonly Random _rng                = new();
    private const int              MaxNpcsPerField     = 40;
    private const float            NpcMoveSpeed        = 2f;
    private const float            NpcSpawnInterval    = 5f;
    private const float            NpcBroadcastInterval = 0.2f;

    public InGameServer(
        Socket socket,
        ConcurrentDictionary<int, InGameData> inGameDataList,
        PlayerStore store)
    {
        _socket         = socket;
        _inGameDataList = inGameDataList;
        _store          = store;
    }

    public void Stop() => _running = false;

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
                case (uint)InGamePacketType.RespawnRequest:
                    var respawnPacket = ProtobufSerializer.Deserialize<RespawnRequestPacket>(buffer);
                    HandleRespawnRequest(respawnPacket, clientEp);
                    break;
                case (uint)InGamePacketType.ScoreBoardRequest:
                    var scoreBoardPacket = ProtobufSerializer.Deserialize<ScoreBoardRequestPacket>(buffer);
                    HandleScoreBoardRequest(scoreBoardPacket, clientEp);
                    break;
                case (uint)InGamePacketType.BuyRequest:
                    var buyRequestPacket = ProtobufSerializer.Deserialize<BuyRequestPacket>(buffer);
                    HandleBuyRequest(buyRequestPacket, clientEp);
                    break;
                case (uint)InGamePacketType.InventoryRequest:
                    var inventoryRequestPacket = ProtobufSerializer.Deserialize<InventoryRequestPacket>(buffer);
                    HandleInventoryRequest(inventoryRequestPacket, clientEp);
                    break;
                case (uint)InGamePacketType.InventoryShortcutRequest:
                    var shortcutRequestPacket = ProtobufSerializer.Deserialize<InventoryShortcutRequestPacket>(buffer);
                    HandleInventoryShortcutRequest(shortcutRequestPacket, clientEp);
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
            Shortcut1 = (int)unit.Shortcut1,
            Shortcut2 = (int)unit.Shortcut2,
            Shortcut3 = (int)unit.Shortcut3,
            Shortcut4 = (int)unit.Shortcut4,
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

        unit.Shortcut1 = (ItemName)packet.Slot1;
        unit.Shortcut2 = (ItemName)packet.Slot2;
        unit.Shortcut3 = (ItemName)packet.Slot3;
        unit.Shortcut4 = (ItemName)packet.Slot4;

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

        float currentHp = 0, maxHp = 0;
        int   finalDamage = 0;

        if (targetType == CapstoneUdpServer.Network.HitTargetType.Building)
        {
            if (!inGameData.TryGetBuilding(packet.TargetId, out var building, out var buildingOwner) || building == null) return;

            var buildingType = (BuildingType)building.PrefabIndex;
            int defense = CombatData.GetBuildingStat(buildingType).Defense;
            finalDamage        = Math.Max(1, rawDamage - defense);
            building.CurrentHp = Math.Max(0f, building.CurrentHp - finalDamage);
            currentHp = building.CurrentHp;
            maxHp     = building.MaxHp;

            if (currentHp <= 0)
            {
                buildingOwner?.Buildings.TryRemove(packet.TargetId, out _);
                inGameData.BuildingOwnerIndex.TryRemove(packet.TargetId, out _);

                byte[] destroyBuf = ProtobufSerializer.Serialize(
                    (uint)InGamePacketType.BuildingDestroy,
                    new BuildingDestroyPacket
                    {
                        BuildingId   = packet.TargetId,
                        BuildingType = building.PrefabIndex,
                        DestroyerId  = packet.AttackerId,
                    });
                inGameData.Broadcast(_socket, destroyBuf);

                if (inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var attacker))
                {
                    attacker.Gold += 300;
                    attacker.Exp  += 500;

                    SendProto((uint)InGamePacketType.RewardUpdate,
                        new RewardUpdatePacket
                        {
                            PlayerId   = attacker.PlayerId,
                            GoldAmount = 300,
                            TotalGold  = attacker.Gold,
                            ExpAmount   = 500,
                            TotalExp    = attacker.Exp,
                            Level       = attacker.Level,
                            RequiredExp = attacker.RequiredExp,
                        },
                        (IPEndPoint)attacker.IpEndPoint);
                }
            }
        }
        else if (targetType == CapstoneUdpServer.Network.HitTargetType.MovingUnit)
        {
            if (!inGameData.NpcMap.TryGetValue(packet.TargetId, out var npc) || !npc.IsAlive) return;

            finalDamage   = Math.Max(1, rawDamage);
            npc.CurrentHp = Math.Max(0f, npc.CurrentHp - finalDamage);
            currentHp     = npc.CurrentHp;
            maxHp         = npc.MaxHp;

            if (npc.CurrentHp <= 0f)
            {
                npc.IsAlive = false;
                inGameData.NpcMap.TryRemove(packet.TargetId, out _);

                byte[] deadBuf = ProtobufSerializer.Serialize(
                    (uint)InGamePacketType.DeathEvent,
                    new DeathEventPacket
                    {
                        TargetId   = packet.TargetId,
                        TargetType = (int)CapstoneUdpServer.Network.HitTargetType.MovingUnit,
                        KillerId   = packet.AttackerId,
                        GoldReward = 100,
                    });
                inGameData.Broadcast(_socket, deadBuf);

                if (inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var npcKiller))
                {
                    npcKiller.Gold    += 100;
                    npcKiller.Exp     += 100;
                    npcKiller.CSCount++;

                    SendProto((uint)InGamePacketType.RewardUpdate,
                        new RewardUpdatePacket
                        {
                            PlayerId   = npcKiller.PlayerId,
                            GoldAmount = 100,
                            TotalGold  = npcKiller.Gold,
                            ExpAmount   = 100,
                            TotalExp    = npcKiller.Exp,
                            CSCount     = npcKiller.CSCount,
                            Level       = npcKiller.Level,
                            RequiredExp = npcKiller.RequiredExp,
                        },
                        (IPEndPoint)npcKiller.IpEndPoint);
                }
            }
        }
        else if (targetType == CapstoneUdpServer.Network.HitTargetType.Player)
        {
            if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.TargetId, out var unit)) return;

            finalDamage    = Math.Max(1, rawDamage - 5);
            unit.CurrentHp = Math.Max(0f, unit.CurrentHp - finalDamage);
            currentHp      = unit.CurrentHp;
            maxHp          = unit.MaxHp;

            if (unit.IsDead)
            {
                unit.DeathCount++;

                byte[] deadBuf = ProtobufSerializer.Serialize(
                    (uint)InGamePacketType.DeathEvent,
                    new DeathEventPacket
                    {
                        TargetId   = packet.TargetId,
                        TargetType = (int)CapstoneUdpServer.Network.HitTargetType.Player,
                        KillerId   = packet.AttackerId,
                        GoldReward = 200,
                    });
                inGameData.Broadcast(_socket, deadBuf);

                SendProto((uint)InGamePacketType.DeathUpdate,
                    new DeathUpdatePacket
                    {
                        PlayerId   = unit.PlayerId,
                        DeathCount = unit.DeathCount,
                    },
                    (IPEndPoint)unit.IpEndPoint);

                if (inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var playerKiller))
                {
                    playerKiller.Gold      += 200;
                    playerKiller.Exp       += 200;
                    playerKiller.KillCount++;

                    SendProto((uint)InGamePacketType.RewardUpdate,
                        new RewardUpdatePacket
                        {
                            PlayerId   = playerKiller.PlayerId,
                            GoldAmount = 200,
                            TotalGold  = playerKiller.Gold,
                            ExpAmount   = 200,
                            TotalExp    = playerKiller.Exp,
                            KillCount   = playerKiller.KillCount,
                            Level       = playerKiller.Level,
                            RequiredExp = playerKiller.RequiredExp,
                        },
                        (IPEndPoint)playerKiller.IpEndPoint);
                }
            }
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

        // TODO: 사망 처리 — Player/NPC currentHp <= 0 시 DeathEventPacket 전체 브로드캐스트, RewardUpdatePacket 공격자에게 전송
    }

    private void HandleRespawnRequest(RespawnRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_store.TryGetInGame(packet.PlayerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        unit.Revive(unit.MaxHp);
        unit.LevelDown();

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.RespawnResponse,
            new RespawnResponsePacket
            {
                PlayerId = unit.PlayerId,
                FieldId = unit.FieldId,
                //TODO: 스폰 위치 지정하기
                MaxHp = unit.MaxHp,
                Level = unit.Level,
                ExpAmount = unit.Exp,
                RequiredExp = unit.RequiredExp
            });
        inGameData.Broadcast(_socket, buf);

        Console.WriteLine($"[Respawn] PlayerId={packet.PlayerId} 리스폰 브로드캐스트 완료 (HP={unit.MaxHp})");
    }

    private void HandleScoreBoardRequest(ScoreBoardRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        var sortedPlayers = inGameData.PlayerUnitDataMap.Values
            .OrderByDescending(p => p.TotalGold)
            .Select(p => p.PlayerId)
            .ToList();

        foreach (var player in inGameData.PlayerUnitDataMap.Values)
        {
            if (player == null) continue;
            int rank = sortedPlayers.IndexOf(player.PlayerId) + 1;
            SendProto((uint)InGamePacketType.ScoreBoardResponse, new ScoreBoardResponsePacket
            {
                PlayerId               = player.PlayerId,
                FieldId                = player.FieldId,
                PlayerName             = player.PlayerName ?? "",
                TotalGold              = player.TotalGold,
                Level                  = player.Level,
                MaxHp                  = player.MaxHp,
                CurrentHp              = player.CurrentHp,
                FinalBuildingMaxHp     = player.FinalBuildingMaxHp,
                FinalBuildingCurrentHp = player.FinalBuildingCurrentHp,
                KillCount              = player.KillCount,
                DeathCount             = player.DeathCount,
                CSCount                = player.CSCount,
                Rank                   = rank,
                isDead                 = player.IsDead,
                isMine                 = player.PlayerId == packet.PlayerId,
            }, clientEp);
        }
    }

    private void HandlePlayerInput(PlayerInputPacket packet, IPEndPoint clientEp)
    {
        if (!_store.TryGetInGame(packet.PlayerId, out var playerData))
        {
            Console.WriteLine("[HandlePlayerInput] 플레이어 데이터가 없습니다!");
            return;
        }
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;
        if (inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unitCheck) && unitCheck.IsDead) return;

        SendProto((uint)InGamePacketType.MoveConfirm, new PlayerMoveConfirmPacket
        {
            Tick      = packet.Tick,
            PlayerId  = packet.PlayerId,
            PosX      = packet.PosX,
            PosY      = packet.PosY,
            PosZ      = packet.PosZ,
            RotationY = packet.RotationY,
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
            WeaponIndex = packet.WeaponIndex,
            IsCrouching = packet.IsCrouching,
        });
        inGameData.Broadcast(_socket, remoteBuf, excludePlayerId: packet.PlayerId);

        // 서버 측 플레이어 위치 갱신 — NPC 추적 거리 계산에 사용
        if (inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit))
            unit.SetPosition(
                new System.Numerics.Vector3(packet.PosX, packet.PosY, packet.PosZ),
                new System.Numerics.Vector3(0, packet.RotationY, 0));
    }
    private ItemName itemName;
    
    private void HandleBuyRequest(BuyRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;
        string msg = "";

        if (unit.Gold < packet.ItemPrice)
        {
            msg = "골드가 부족합니다!";
        }
        else
        {
            ShopList shopItem = (ShopList)packet.ItemType;
            
            switch (shopItem)
            {
                case  ShopList.Grenade:
                    itemName = ItemName.NormalGrenade; break;
                case  ShopList.Tank:
                    itemName = ItemName.Tank; break;
                case  ShopList.Missile:
                    itemName = ItemName.NormalMissile; break;
                case ShopList.Artillery:
                    itemName = ItemName.Artillery; break;
            }
            
            unit.Gold -= packet.ItemPrice;
            unit.AddInventory(itemName, shopItem);
            switch (shopItem)
            {
                case ShopList.Grenade:
                    msg = "수류탄을 구매하였습니다."; break;
                case ShopList.Tank:
                    msg = "장갑차를 구매하였습니다."; break;
                case ShopList.Artillery:
                    msg = "박격포를 구매하였습니다."; break;
                case ShopList.Missile:
                    msg = "미사일을 구매하였습니다."; break;
            }
        }
        
        SendProto((uint)InGamePacketType.BuyResponse, new BuyResponsePacket
        {
            PlayerId = unit.PlayerId,
            FieldId =  unit.FieldId,
            Msg = msg,
            RemainGold = unit.Gold,
        }, clientEp);
        
    }
    
    private void HandleInventoryRequest(InventoryRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        foreach (var item in unit.Inventory.Values)
        {
            SendProto((uint)InGamePacketType.InventoryResponse, new InventoryResponsePacket
            {
                PlayerId = unit.PlayerId,
                FieldId = unit.FieldId,
                ItemId = item.ItemId,
                ItemType = (int)item.Type,
                Amount = item.Amount,
                
                Shortcut1 = (int)unit.Shortcut1,
                Shortcut2 = (int)unit.Shortcut2,
                Shortcut3 = (int)unit.Shortcut3,
                Shortcut4 = (int)unit.Shortcut4,
                
            }, clientEp);
            
        }

    }

    private void HandleInventoryShortcutRequest(InventoryShortcutRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        var itemName = (ItemName)packet.ItemName;
        switch (packet.SlotIndex)
        {
            case 1: unit.Shortcut1 = itemName; break;
            case 2: unit.Shortcut2 = itemName; break;
            case 3: unit.Shortcut3 = itemName; break;
            case 4: unit.Shortcut4 = itemName; break;
            default: return;
        }

        SendProto((uint)InGamePacketType.InventoryShortcutResponse, new InventoryShortcutResponsePacket
        {
            PlayerId           = unit.PlayerId,
            FieldId            = unit.FieldId,
            InventoryShortcut1 = (int)unit.Shortcut1,
            InventoryShortcut2 = (int)unit.Shortcut2,
            InventoryShortcut3 = (int)unit.Shortcut3,
            InventoryShortcut4 = (int)unit.Shortcut4,
        }, clientEp);
    }

    // ─────────────────────────────────────────────────────────────
    #region NPC

    private const float NpcChaseRange     = 15f;
    private const float NpcChaseExitRange = 20f;

    public void StartNpcLoop(int fieldId, InGameData inGameData)
    {
        Task.Run(async () =>
        {
            float spawnTimer     = NpcSpawnInterval; // 첫 틱에 즉시 스폰 시도
            float broadcastTimer = 0f;
            var   lastTime       = DateTime.UtcNow;

            while (_running)
            {
                await Task.Delay(100);

                var   now = DateTime.UtcNow;
                float dt  = (float)(now - lastTime).TotalSeconds;
                lastTime  = now;

                if (inGameData.PlayerUnitDataMap.IsEmpty) break;

                spawnTimer     += dt;
                broadcastTimer += dt;

                if (spawnTimer >= NpcSpawnInterval)
                {
                    spawnTimer = 0f;
                    TrySpawnNpc(fieldId, inGameData);
                }

                if (broadcastTimer >= NpcBroadcastInterval)
                {
                    broadcastTimer = 0f;
                    TickAndBroadcastNpcs(inGameData, NpcBroadcastInterval);
                }
            }
            Console.WriteLine($"[NPC] fieldId={fieldId} NPC 루프 종료");
        });
    }

    private void TrySpawnNpc(int fieldId, InGameData inGameData)
    {
        int alive = 0;
        foreach (var kv in inGameData.NpcMap)
            if (kv.Value.IsAlive) alive++;
        if (alive >= MaxNpcsPerField) return;

        int   npcId = inGameData.NextNpcId();
        // TODO: 스폰 위치 추후 변경 예정 (맵 스폰 포인트 지정 방식으로 교체)
        float angle  = (float)(_rng.NextDouble() * Math.PI * 2);
        float radius = 15f + (float)(_rng.NextDouble() * 30f);
        var   pos    = new System.Numerics.Vector3(
            400f + (float)Math.Sin(angle) * radius,
            2f,
            400f + (float)Math.Cos(angle) * radius);

        var npc = new NpcData { NpcId = npcId, NpcType = 0, Position = pos };
        npc.PickNewDirection();
        inGameData.NpcMap[npcId] = npc;

        var buf = ProtobufSerializer.Serialize((uint)InGamePacketType.SpawnNpc, new SpawnNpcPacket
        {
            NpcId   = npcId,
            PosX    = pos.X,
            PosY    = pos.Y,
            PosZ    = pos.Z,
            NpcType = 0,
            MaxHp   = npc.MaxHp,
        });
        inGameData.Broadcast(_socket, buf);
        Console.WriteLine($"[NPC] 스폰 NpcId={npcId}, fieldId={fieldId}, alive={alive + 1}");
    }

    private void TickAndBroadcastNpcs(InGameData inGameData, float dt)
    {
        uint tick = (uint)(Environment.TickCount64 / 100);

        foreach (var kv in inGameData.NpcMap)
        {
            var npc = kv.Value;
            if (!npc.IsAlive) continue;

            var prevPos = npc.Position;
            UpdateNpcChaseState(npc, inGameData);
            MoveNpc(npc, dt);

            // 위치 변화 없으면 패킷 전송 스킵
            if ((npc.Position - prevPos).LengthSquared() < 0.0001f) continue;

            var buf = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcState, new NpcStatePacket
            {
                Tick      = tick,
                NpcId     = npc.NpcId,
                PosX      = npc.Position.X,
                PosY      = npc.Position.Y,
                PosZ      = npc.Position.Z,
                RotY      = npc.RotY,
                VelX      = npc.MoveDir.X * NpcMoveSpeed,
                VelZ      = npc.MoveDir.Z * NpcMoveSpeed,
            });
            inGameData.Broadcast(_socket, buf);
        }
    }

    private void UpdateNpcChaseState(NpcData npc, InGameData inGameData)
    {
        if (npc.IsChasing)
        {
            bool targetGone = !inGameData.PlayerUnitDataMap.TryGetValue(npc.ChaseTargetId, out var target)
                              || DistanceSq(npc.Position, target.Position) > NpcChaseExitRange * NpcChaseExitRange;

            if (targetGone)
            {
                npc.IsChasing     = false;
                npc.ChaseTargetId = 0;
                npc.PickNewDirection();

                var evt = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcChaseEvent,
                    new NpcChaseEventPacket { NpcId = npc.NpcId, TargetPlayerId = 0, IsChasing = false });
                inGameData.Broadcast(_socket, evt);
            }
            else
            {
                var delta = target.Position - npc.Position;
                if (delta.LengthSquared() > 0.01f)
                {
                    var dir  = System.Numerics.Vector3.Normalize(delta);
                    npc.MoveDir = new System.Numerics.Vector3(dir.X, 0, dir.Z);
                    npc.RotY    = (float)(Math.Atan2(dir.X, dir.Z) * (180.0 / Math.PI));
                }
            }
        }
        else
        {
            int   nearestId     = 0;
            float nearestDistSq = NpcChaseRange * NpcChaseRange;

            foreach (var kv in inGameData.PlayerUnitDataMap)
            {
                float dSq = DistanceSq(npc.Position, kv.Value.Position);
                if (dSq < nearestDistSq)
                {
                    nearestDistSq = dSq;
                    nearestId     = kv.Key;
                }
            }

            if (nearestId != 0)
            {
                npc.IsChasing     = true;
                npc.ChaseTargetId = nearestId;

                var evt = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcChaseEvent,
                    new NpcChaseEventPacket { NpcId = npc.NpcId, TargetPlayerId = nearestId, IsChasing = true });
                inGameData.Broadcast(_socket, evt);
            }
        }
    }

    private void MoveNpc(NpcData npc, float dt)
    {
        const float bound = 80f;

        if (!npc.IsChasing)
        {
            npc.DirTimer -= dt;
            if (npc.DirTimer <= 0f)
            {
                npc.DirTimer = float.MaxValue;
                npc.MoveDir  = System.Numerics.Vector3.Zero;
                _ = Task.Run(async () => { await Task.Delay(3000); npc.PickNewDirection(); });
                return;
            }
        }

        var newPos = npc.Position + npc.MoveDir * NpcMoveSpeed * dt;
        if (Math.Abs(newPos.X) > bound || Math.Abs(newPos.Z) > bound)
        {
            npc.PickNewDirection();
            npc.Position = new System.Numerics.Vector3(
                Math.Clamp(newPos.X, -bound, bound), newPos.Y,
                Math.Clamp(newPos.Z, -bound, bound));
            return;
        }
        npc.Position = newPos;
    }

    private static float DistanceSq(System.Numerics.Vector3 a, System.Numerics.Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    #endregion

    private void SendProto<T>(uint packetType, T message, IPEndPoint ep)
    {
        byte[] buffer = ProtobufSerializer.Serialize(packetType, message);
        _socket.SendTo(buffer, ep);
    }
}
