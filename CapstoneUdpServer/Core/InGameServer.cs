
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
    private volatile bool                                   _running = true;

    private static readonly Random _rng                = new();
    private const float            NpcSpawnInterval    = 10f;
    private int                    _missileIdCounter   = 20000;

    

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
                // ── 플레이어 이동/상태 ───────────────────────────────────────────────
                case (uint)InGamePacketType.PlayerInput:
                    var packet = ProtobufSerializer.Deserialize<PlayerInputPacket>(buffer);
                    HandlePlayerInput(packet, clientEp);
                    break;
                case (uint)InGamePacketType.UIUpdateRequest:
                    var uiReqPacket = ProtobufSerializer.Deserialize<UIUpdateRequestPacket>(buffer);
                    HandleUIUpdateRequest(uiReqPacket, clientEp);
                    break;

                // ── 전투 (발사/피격/사망) ────────────────────────────────────────────
                case (uint)InGamePacketType.FireEvent:
                    var firePacket = ProtobufSerializer.Deserialize<FireEventPacket>(buffer);
                    HandleFireEvent(firePacket);
                    break;
                case (uint)InGamePacketType.AnimTrigger:
                    var animTriggerPacket = ProtobufSerializer.Deserialize<AnimTriggerPacket>(buffer);
                    HandleAnimTrigger(animTriggerPacket);
                    break;
                case (uint)InGamePacketType.HitRequest:
                    var hitRequestPacket = ProtobufSerializer.Deserialize<HitRequestPacket>(buffer);
                    HandleHitRequest(hitRequestPacket);
                    break;
                case (uint)InGamePacketType.MissileHitRequest:
                    var hitPacket = ProtobufSerializer.Deserialize<MissileHitRequestPacket>(buffer);
                    HandleMissileHitRequest(hitPacket);
                    break;
                case (uint)InGamePacketType.DamageEvent:
                    var damageEventPacket = ProtobufSerializer.Deserialize<DamageEventPacket>(buffer);
                    HandleDamageEvent(damageEventPacket);
                    break;
                case (uint)InGamePacketType.DeathRequest:
                    var deathRequestPacket = ProtobufSerializer.Deserialize<DeathRequestPacket>(buffer);
                    HandleDeathRequest(deathRequestPacket);
                    break;

                // ── 리스폰/스코어 ─────────────────────────────────────────────────
                case (uint)InGamePacketType.RespawnRequest:
                    var respawnPacket = ProtobufSerializer.Deserialize<RespawnRequestPacket>(buffer);
                    HandleRespawnRequest(respawnPacket, clientEp);
                    break;
                case (uint)InGamePacketType.ScoreBoardRequest:
                    var scoreBoardPacket = ProtobufSerializer.Deserialize<ScoreBoardRequestPacket>(buffer);
                    HandleScoreBoardRequest(scoreBoardPacket, clientEp);
                    break;

                // ── 인벤토리/상점 ─────────────────────────────────────────────────
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
                case (uint)InGamePacketType.ShortcutSwitchRequest:
                    var shortcutSwitchRequestPacket = ProtobufSerializer.Deserialize<ShortcutSwitchRequestPacket>(buffer);
                    HandleShortcutSwitchRequest(shortcutSwitchRequestPacket, clientEp);
                    break;
                case (uint)InGamePacketType.HotkeySlotSave:
                    var hsPacket = ProtobufSerializer.Deserialize<HotkeySavePacket>(buffer);
                    HandleHotkeySlotSave(hsPacket);
                    break;
                case (uint)InGamePacketType.WeaponChange:
                    var wcPacket = ProtobufSerializer.Deserialize<WeaponChangePacket>(buffer);
                    HandleWeaponChange(wcPacket);
                    break;
                case (uint)InGamePacketType.TimerRequest:
                    var timerRequestPacket = ProtobufSerializer.Deserialize<TimerRequestPacket>(buffer);
                    HandleTimerRequest(timerRequestPacket, clientEp);
                    break;

                // ── 건물 ──────────────────────────────────────────────────────────
                case (uint)InGamePacketType.BuildingPlace:
                    var buildingPlacePacket = ProtobufSerializer.Deserialize<BuildingPlacePacket>(buffer);
                    HandleBuildingPlace(buildingPlacePacket, clientEp);
                    break;
                case (uint)InGamePacketType.BuildingBtnRequest:
                    var buildingBtnRequestPacket = ProtobufSerializer.Deserialize<BuildingBtnRequestpacket>(buffer);
                    HandleBuildingBtnRequest(buildingBtnRequestPacket, clientEp);
                    break;
                case (uint)InGamePacketType.ArtilleryInfoRequest:
                    var artilleryInfoPacket = ProtobufSerializer.Deserialize<ArtilleryInfoRequestPacket>(buffer);
                    HandleArtilleryInfoRequest(artilleryInfoPacket, clientEp);
                    break;
                // ── 미사일포 (Intercept System) ────────────────────────────────────
                case (uint)InGamePacketType.MissileLoadRequest:
                    var mlPacket = ProtobufSerializer.Deserialize<MissileLoadRequestPacket>(buffer);
                    HandleMissileLoadRequest(mlPacket, clientEp);
                    break;
                case (uint)InGamePacketType.MissileLaunch:
                    var launchPacket = ProtobufSerializer.Deserialize<MissileLaunchPacket>(buffer);
                    HandleMissileLaunch(launchPacket);
                    break;
                case (uint)InGamePacketType.InterceptTargetRequest:
                    var interceptTargetPacket = ProtobufSerializer.Deserialize<InterceptTargetRequestPacket>(buffer);
                    HandleInterceptTargetRequest(interceptTargetPacket);
                    break;
                case (uint)InGamePacketType.InterceptRotation:
                    var interceptRotationPacket = ProtobufSerializer.Deserialize<InterceptRotationPacket>(buffer);
                    HandleInterceptRotation(interceptRotationPacket);
                    break;
                case (uint)InGamePacketType.InterceptFire:
                    var interceptFirePacket = ProtobufSerializer.Deserialize<InterceptFirePacket>(buffer);
                    HandleInterceptFire(interceptFirePacket);
                    break;
                case (uint)InGamePacketType.AttackBuildingRotate:
                    var attackRotatePacket = ProtobufSerializer.Deserialize<AttackBuildingRotatePacket>(buffer);
                    HandleAttackBuildingRotate(attackRotatePacket);
                    break;
                case (uint)InGamePacketType.AttackBuildingFire:
                    var attackFirePacket = ProtobufSerializer.Deserialize<AttackBuildingFirePacket>(buffer);
                    HandleAttackBuildingFire(attackFirePacket);
                    break;
                case (uint)InGamePacketType.NpcInterpolate:
                    var npcInterpolatePacket = ProtobufSerializer.Deserialize<NpcInterpolatePacket>(buffer);
                    HandleNpcInterpolate(npcInterpolatePacket);
                    break;
                case (uint)InGamePacketType.NpcFightEnter:
                    var npcFightEnterPacket = ProtobufSerializer.Deserialize<NpcFightEnterPacket>(buffer);
                    HandleNpcFightEnter(npcFightEnterPacket);
                    break;
                case (uint)InGamePacketType.NpcFightExit:
                    var npcFightExitPacket = ProtobufSerializer.Deserialize<NpcFightExitPacket>(buffer);
                    HandleNpcFightExit(npcFightExitPacket);
                    break;
                case (uint)InGamePacketType.NpcFireEvent:
                    var npcFireEventPacket = ProtobufSerializer.Deserialize<NpcFireEventPacket>(buffer);
                    HandleNpcFireEvent(npcFireEventPacket);
                    break;
                case (uint)InGamePacketType.HealRequest:
                    var healRequestPacket = ProtobufSerializer.Deserialize<HealRequestPacket>(buffer);
                    HandleHealRequest(healRequestPacket, clientEp);
                    break;
                case (uint)InGamePacketType.CaptureRequest:
                    var captureRequestPacket = ProtobufSerializer.Deserialize<CaptureRequestPacket>(buffer);
                    HandleCaptureRequest(captureRequestPacket);
                    break;
                case (uint)InGamePacketType.AreaAttackEffect:
                    var areaEffectPacket = ProtobufSerializer.Deserialize<AreaAttackEffectPacket>(buffer);
                    HandleAreaAttackEffect(areaEffectPacket);
                    break;
                case (uint)InGamePacketType.CoreInitRequest:
                    var coreInitPacket = ProtobufSerializer.Deserialize<CoreInitRequestPacket>(buffer);
                    HandleCoreInitRequest(coreInitPacket);
                    break;
            }
        }

    }

    private void HandleAreaAttackEffect(AreaAttackEffectPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        inGameData.Broadcast(_socket,
            ProtobufSerializer.Serialize((uint)InGamePacketType.AreaAttackEffect, packet));
    }

    private void HandleCoreInitRequest(CoreInitRequestPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        int coreId = packet.SpawnIndex == 0 ? 90010001 : 90010002;
        if (inGameData.BuildingDataMap.ContainsKey(coreId)) return; // 중복 방지

        var coreData = new BuildingData
        {
            BuildingId   = coreId,
            OwnerId      = 0,
            BuildingType = ItemName.Core,
            PosX = packet.PosX, PosY = packet.PosY, PosZ = packet.PosZ,
            MaxHp     = 400f,
            CurrentHp = 400f,
            IsCore    = true,
        };
        inGameData.BuildingDataMap[coreId] = coreData;

        inGameData.Broadcast(_socket, ProtobufSerializer.Serialize(
            (uint)InGamePacketType.BuildingPlace,
            new BuildingPlacePacket
            {
                BuildingId   = coreId,
                OwnerId      = 0,
                BuildingType = (int)ItemName.Core,
                PosX = packet.PosX, PosY = packet.PosY, PosZ = packet.PosZ,
                IsCore = true,
            }));

        Console.WriteLine($"[Core] CoreId={coreId} SpawnIndex={packet.SpawnIndex} pos=({packet.PosX},{packet.PosY},{packet.PosZ}) 생성 완료");
    }

    private void HandleTimerRequest(TimerRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        var (min, sec) = inGameData.GetElapsedTime();
        
        SendProto((uint)InGamePacketType.TimerResponse, new TimerResponsePacket
        {
            Min = min,
            Sec = sec
        }, clientEp);
    }

    private void HandleAttackBuildingRotate(AttackBuildingRotatePacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.AttackBuildingRotate, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleAttackBuildingFire(AttackBuildingFirePacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.AttackBuildingFire, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleCaptureRequest(CaptureRequestPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;
        if (inGameData.OccupationPhaseStarted == false) return;
        double roll   = _rng.NextDouble();
        ItemName reward = roll < 0.80 ? ItemName.NormalMissile : ItemName.NuclearMissile;

        unit.AddInventory(reward);
        Console.WriteLine();

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.CaptureResponse,
            new CaptureResponsePacket
            {
                PlayerId    = packet.PlayerId,
                BuildingId  = packet.BuildingId,
                CooldownSec = 180,
                Msg = $"PlayerId={unit.PlayerName}의 점령 보상={reward.ToString()}"
            });
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleHealRequest(HealRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;
        if (unit.IsDead) return;

        unit.CurrentHp = Math.Min(unit.CurrentHp + packet.HealAmount, unit.MaxHp);

        if (unit.RemoveInventory((ItemName)packet.ItemName))
        {
            if (unit.CurrentGrippingItem != (ItemName)packet.ItemName)
                return;
            switch (unit.CurrentSlotIndex)
            {
                case 0:
                    unit.Shortcut1 = ItemName.None; break;
                case 1:
                    unit.Shortcut2 = ItemName.None; break;
                case 2:
                    unit.Shortcut3 = ItemName.None; break;
                case 3:
                    unit.Shortcut4 = ItemName.None; break;
            }
            
            unit.CurrentGrippingItem = ItemName.None;

            SendProto((uint)InGamePacketType.UIUpdateResponse,
                new UIUpdateResponsePacket
                {
                    PlayerId = unit.PlayerId,
                    Shortcut1 = (int)unit.Shortcut1,
                    Shortcut2 = (int)unit.Shortcut2,
                    Shortcut3 = (int)unit.Shortcut3,
                    Shortcut4 = (int)unit.Shortcut4,
                }, clientEp); //인게임 메인 UI의 미사일 -> 주먹(default)으로 바꿈
            
            byte[] buf1 =  ProtobufSerializer.Serialize((uint)InGamePacketType.WeaponChange,
                new WeaponChangePacket
                {
                    PlayerId = packet.PlayerId,
                    ItemName = (int)unit.CurrentGrippingItem,
                    SlotIndex = unit.CurrentSlotIndex,
                });
                
            inGameData.Broadcast(_socket, buf1);
        }

        byte[] buf2 = ProtobufSerializer.Serialize((uint)InGamePacketType.HealResponse, new HealResponsePacket
        {
            PlayerId  = packet.PlayerId,
            CurrentHp = unit.CurrentHp,
            MaxHp     = unit.MaxHp,
        });
        inGameData.Broadcast(_socket, buf2);
    }

    private void HandleNpcInterpolate(NpcInterpolatePacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.NpcMap.TryGetValue(packet.NpcId, out var npcUnit)) return;

        if (npcUnit.LastCornerIdx == packet.ArriveCornerIdx) return; // 중복 무시
        npcUnit.LastCornerIdx = packet.ArriveCornerIdx;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcInterpolate,
            packet);
        inGameData.Broadcast(_socket, buf); // 최초 1회만 브로드캐스트
    }

    private void HandleNpcFightEnter(NpcFightEnterPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.NpcMap.TryGetValue(packet.NpcId, out var npcUnit)) return;
        npcUnit.Fighting = true;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcFightEnter, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleNpcFightExit(NpcFightExitPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.NpcMap.TryGetValue(packet.NpcId, out var npcUnit)) return;
        npcUnit.Fighting = false;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcFightExit, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleNpcFireEvent(NpcFireEventPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.NpcFireEvent, packet);
        inGameData.Broadcast(_socket, buf);
    }

    #region 플레이어 이동/상태

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
    
    #endregion

    #region 건물

    private void HandleBuildingPlace(BuildingPlacePacket packet,IPEndPoint clientEp)
    {
        if (!_store.TryGetInGame(packet.OwnerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.OwnerId, out var unit)) return;
        // 서버에서 고유 BuildingId 발급
        packet.BuildingId = inGameData.NextBuildingId() + 10000;

        var itemName = (ItemName)packet.BuildingType;
        var stat     = CombatData.GetBuildingStat(itemName);

        int amount = 0;
        if(!unit.RemoveInventory(itemName)) amount = unit.Inventory[itemName].Amount;
        
        SendProto((uint)InGamePacketType.BuildingBtnUpdate, new BuildingBtnUpdatePacket
        {
            PlayerId = packet.OwnerId,
            ItemName = (int)itemName,
            Amount = amount,
            
        }, clientEp);
        
        inGameData.BuildingDataMap[packet.BuildingId] = new BuildingData
        {
            BuildingId   = packet.BuildingId,
            OwnerId      = packet.OwnerId,
            BuildingType = itemName,
            MaxHp        = stat.MaxHp,
            CurrentHp    = stat.MaxHp,
            PosX         = packet.PosX, //TODO: 위치 정보는 필요 없을 수도 있다...
            PosY         = packet.PosY,
            PosZ         = packet.PosZ,
            RotY         = packet.RotY,
        };

        // BuildingId 채워진 패킷으로 전체 브로드캐스트 (배치자 포함)
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.BuildingPlace, packet);
        inGameData.Broadcast(_socket, buf);
    }


    #endregion

    #region 미사일포 (Intercept System)

    private void HandleMissileLoadRequest(MissileLoadRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if(!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;
        if (!inGameData.BuildingDataMap.TryGetValue(packet.BuildingId, out var artillery)) return;
        if ((ItemName)packet.MissileType != ItemName.NormalMissile &&
              (ItemName)packet.MissileType != ItemName.NuclearMissile)
        {
            SendProto((uint)InGamePacketType.ExceptionResponse,
                new ExceptionPacket
                {
                    ErrorMessage = "미사일을 들고 장전하시오"
                }, clientEp);
            return;
        }

        var missileType = (ItemName)packet.MissileType;

        if (!unit.HasItemInventory(missileType))
        {
            SendProto((uint)InGamePacketType.ExceptionResponse,
                new ExceptionPacket
                {
                    ErrorMessage = "미사일이 부족합니다."
                }, clientEp);
            return;
        }

        if (unit.RemoveInventory(missileType))
        {
            switch (unit.CurrentSlotIndex)
            {
                case 0:
                    unit.Shortcut1 = ItemName.None; break;
                case 1:
                    unit.Shortcut2 = ItemName.None; break;
                case 2:
                    unit.Shortcut3 = ItemName.None; break;
                case 3:
                    unit.Shortcut4 = ItemName.None; break;
            }

            unit.CurrentGrippingItem = ItemName.None;
            
            SendProto((uint)InGamePacketType.UIUpdateResponse, 
                new UIUpdateResponsePacket
            {
                PlayerId = unit.PlayerId,
                Shortcut1 = (int)unit.Shortcut1,
                Shortcut2 = (int)unit.Shortcut2,
                Shortcut3 = (int)unit.Shortcut3,
                Shortcut4 = (int)unit.Shortcut4,
            }, clientEp); //인게임 메인 UI의 미사일 -> 주먹(default)으로 바꿈
            
            byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.WeaponChange,
                new WeaponChangePacket()
                {
                    PlayerId = unit.PlayerId,
                    ItemName = (int)unit.CurrentGrippingItem,
                    SlotIndex = unit.CurrentSlotIndex,
                }); //미사일 소진해서 아무것도 안들고있게끔 브로드캐스팅
            
            inGameData.Broadcast(_socket, buf);
        }
        
        artillery.LoadMissile(missileType);

        SendProto((uint)InGamePacketType.MissileLoadResponse, new MissileLoadResponsePacket
        {
            PlayerId                    = packet.PlayerId,
            BuildingId                  = packet.BuildingId,
            MissileType                 = packet.MissileType,
            LoadedNormalMissileAmount   = artillery.NormalMissileCount,
            LoadedNuclearMissileAmount  = artillery.NuclearMissileCount,
        }, clientEp);
    }

    private void HandleMissileLaunch(MissileLaunchPacket packet)
    {
        if (!_store.TryGetInGame(packet.OwnerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;
        if (!inGameData.BuildingDataMap.TryGetValue(packet.BuildingId, out var building)) return;

        var missileType = (ItemName)packet.MissileType;
        switch (missileType)
        {
            case ItemName.NormalMissile:
                if (building.NormalMissileCount <= 0) return;
                building.NormalMissileCount--;
                break;
            case ItemName.NuclearMissile:
                if (building.NuclearMissileCount <= 0) return;
                building.NuclearMissileCount--;
                break;
            default:
                return;
        }

        packet.MissileId = _missileIdCounter++;
        Console.WriteLine($"[MissileLaunch] id={packet.MissileId} type={missileType} owner={packet.OwnerId} building={packet.BuildingId}");

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.MissileLaunch, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleMissileHitRequest(MissileHitRequestPacket packet)
    {
        if (!_store.TryGetInGame(packet.OwnerId, out var playerData)) return;
        if (!_inGameDataList.TryGetValue(playerData.FieldId, out var inGameData)) return;

        var missileType = (ItemName)packet.MissileType;

        ApplyMissileDamage(inGameData, packet.OwnerId, packet.DirectTargetId, packet.DirectTargetType, missileType);
        foreach (var target in packet.SplashTargets)
            ApplyMissileDamage(inGameData, packet.OwnerId, target.TargetId, target.TargetType, missileType);
    }

    private void ApplyMissileDamage(
        InGameData inGameData,
        int attackerId,
        int targetId,
        int targetTypeValue,
        ItemName weaponType)
    {
        if (targetId == 0) return;

        var targetType = (UnitType)targetTypeValue;
        int rawDamage = CombatData.GetDamageByItemName(weaponType);
        float currentHp;
        float maxHp;
        int finalDamage;

        if (targetType == UnitType.Building)
        {
            if (!inGameData.BuildingDataMap.TryGetValue(targetId, out var building)) return;
            var stat           = CombatData.GetBuildingStat(building.BuildingType);
            finalDamage        = Math.Max(1, rawDamage - stat.Defense);
            building.CurrentHp = Math.Max(0f, building.CurrentHp - finalDamage);
            currentHp          = building.CurrentHp;
            maxHp              = building.MaxHp;
        }
        else
        {
            if (!inGameData.PlayerUnitDataMap.TryGetValue(targetId, out var unit)) return;
            int defense    = targetType == UnitType.Player ? 5 : 10;
            finalDamage    = Math.Max(1, rawDamage - defense);
            unit.CurrentHp = Math.Max(0f, unit.CurrentHp - finalDamage);
            currentHp      = unit.CurrentHp;
            maxHp          = unit.MaxHp;
        }

        BroadcastDamageResult(inGameData, new DamageResultPacket
        {
            AttackerId = attackerId,
            TargetId   = targetId,
            TargetType = targetTypeValue,
            Damage     = finalDamage,
            CurrentHp  = currentHp,
            MaxHp      = maxHp,
        });
    }


    private void ReturnMissile(PlayerUnitData unit, ItemName missileType, out int remaining)
    {
        switch (missileType)
        {
            case ItemName.NormalMissile:
                unit.GuidedMissileCount++;
                remaining = unit.GuidedMissileCount;
                break;
            case ItemName.NuclearMissile:
                unit.NukeMissileCount++;
                remaining = unit.NukeMissileCount;
                break;
            default:
                remaining = 0;
                break;
        }
    }

    #endregion

    #region 전투 (피격/사망)

    /// <summary>
    /// BulletScript(IsMine) → HitRequestPacket → 데미지 계산 → DamageResultPacket 브로드캐스트
    /// 사망 처리는 하지 않음 — 클라이언트 Health 가 DeathRequestPacket 을 별도 전송
    /// </summary>
    private void HandleHitRequest(HitRequestPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        if (packet.AttackerType == (int)UnitType.Player)
        {
            if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var attackerData)) return;
            if (attackerData.CurrentGrippingItem != (ItemName)packet.WeaponItemName)
            {
                Console.WriteLine($"[Hit] 무기 불일치 — 서버:{attackerData.CurrentGrippingItem} 클라:{(ItemName)packet.WeaponItemName}");
                return;
            }
        }
        
        var itemName   = (ItemName)packet.WeaponItemName;
        var targetType = (UnitType)packet.TargetType;
        int rawDamage  = CombatData.GetDamageByItemName(itemName);

        float currentHp, maxHp;
        int   finalDamage;

        switch (targetType)
        {
            case UnitType.Npc:
            {
                if (!inGameData.NpcMap.TryGetValue(packet.TargetId, out var npc) || !npc.IsAlive) return;
                finalDamage   = Math.Max(1, rawDamage);
                npc.CurrentHp = Math.Max(0f, npc.CurrentHp - finalDamage);
                currentHp     = npc.CurrentHp;
                maxHp         = npc.MaxHp;
                break;
            }
            case UnitType.Player:
            {
                if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.TargetId, out var unit)) return;
                if (unit.IsDead) return;
                finalDamage    = Math.Max(1, rawDamage - unit.Defense); // 플레이어 기본 방어력 5
                unit.CurrentHp = Math.Max(0f, unit.CurrentHp - finalDamage);
                currentHp      = unit.CurrentHp;
                maxHp          = unit.MaxHp;
                break;
            }
            case UnitType.Building:
            {
                if (!inGameData.BuildingDataMap.TryGetValue(packet.TargetId, out var building)) return;
                var stat           = CombatData.GetBuildingStat(building.BuildingType);
                finalDamage        = Math.Max(1, rawDamage - stat.Defense);
                building.CurrentHp = Math.Max(0f, building.CurrentHp - finalDamage);
                currentHp          = building.CurrentHp;
                maxHp              = building.MaxHp;
                break;
            }
            default: return;
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
        });

        Console.WriteLine($"[Hit] attacker={packet.AttackerId} target={packet.TargetId}({targetType}) dmg={finalDamage} hp={currentHp}/{maxHp}");
    }

    /// <summary>
    /// 클라이언트 Health.SetHealth 가 사망 감지 → DeathRequestPacket → 사망 처리 + 리워드
    /// </summary>
    private void HandleDeathRequest(DeathRequestPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;

        if(!inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var attackerUnit)) return;
        var targetType = (UnitType)packet.TargetType;

        switch (targetType)
        {
            case UnitType.Npc:
            {
                if (!inGameData.NpcMap.TryGetValue(packet.TargetId, out var npc)) return;
                if (!npc.IsAlive) return;

                npc.IsAlive = false;
                inGameData.NpcMap.TryRemove(packet.TargetId, out _);

                bool isBoss    = npc.NpcType == 1;
                int goldReward = isBoss ? 500 : RewardData.NpcKillReward.Gold;
                int expReward  = isBoss ? 300 : RewardData.NpcKillReward.Exp;

                if (isBoss) inGameData.OnBossDied(npc.SpawnPoint);

                inGameData.Broadcast(_socket, ProtobufSerializer.Serialize(
                    (uint)InGamePacketType.DeathEvent,
                    new DeathEventPacket
                    {
                        TargetId   = packet.TargetId,
                        TargetType = (int)UnitType.Npc,
                        KillerId   = packet.AttackerId,
                        GoldReward = goldReward,
                    }));

                if (packet.AttackerId != 0
                    && inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var npcKiller))
                {
                    npcKiller.Gold += goldReward;
                    npcKiller.Exp  += expReward;
                    npcKiller.CSCount++;

                    if (isBoss) npcKiller.AddInventory(ItemName.Bazuka);

                    SendProto((uint)InGamePacketType.RewardUpdate, new RewardUpdatePacket
                    {
                        PlayerId    = npcKiller.PlayerId,
                        GoldAmount  = goldReward, TotalGold   = npcKiller.Gold,
                        ExpAmount   = expReward,  TotalExp    = npcKiller.Exp,
                        CSCount     = npcKiller.CSCount,
                        Level       = npcKiller.Level,
                        RequiredExp = npcKiller.RequiredExp,
                        MaxHp       = npcKiller.MaxHp,
                        CurrentHp   = npcKiller.CurrentHp,
                    }, (IPEndPoint)npcKiller.IpEndPoint);
                }

                Console.WriteLine($"[Death] NPC npcId={packet.TargetId} isBoss={isBoss} killed by={packet.AttackerId}");
                break;
            }
            case UnitType.Player:
            {
                if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.TargetId, out var unit)) return;
                if (!unit.IsDead)       return; // HP 가 0 이 아님
                if (unit.DeathProcessed) return; // 중복 전송 방지

                unit.DeathProcessed = true;
                unit.DeathCount++;

                inGameData.Broadcast(_socket, ProtobufSerializer.Serialize(
                    (uint)InGamePacketType.DeathEvent,
                    new DeathEventPacket
                    {
                        TargetId   = packet.TargetId,
                        TargetType = (int)UnitType.Player,
                        KillerId   = packet.AttackerId,
                        GoldReward = RewardData.PlayerKillReward.Gold,
                    }));

                SendProto((uint)InGamePacketType.DeathUpdate,
                    new DeathUpdatePacket { PlayerId = unit.PlayerId, DeathCount = unit.DeathCount },
                    (IPEndPoint)unit.IpEndPoint);

                if (packet.AttackerId != 0
                    && inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var killer))
                {
                    var playerReward = RewardData.PlayerKillReward;
                    killer.Gold += playerReward.Gold;
                    killer.Exp  += playerReward.Exp;
                    killer.KillCount++;
                    SendProto((uint)InGamePacketType.RewardUpdate, new RewardUpdatePacket
                    {
                        PlayerId    = killer.PlayerId,
                        GoldAmount  = playerReward.Gold,  TotalGold   = killer.Gold,
                        ExpAmount   = playerReward.Exp,   TotalExp    = killer.Exp,
                        KillCount   = killer.KillCount,
                        Level       = killer.Level,
                        RequiredExp = killer.RequiredExp,
                    }, (IPEndPoint)killer.IpEndPoint);
                }

                Console.WriteLine($"[Death] Player targetId={packet.TargetId} killed by player={packet.AttackerId}");
                break;
            }
            case UnitType.Building:
            {
                if (!inGameData.BuildingDataMap.TryGetValue(packet.TargetId, out var building)) return;
                if (building.CurrentHp > 0f) return; // 아직 살아있음

                if (building.IsCore)
                {
                    inGameData.BuildingDataMap.TryRemove(packet.TargetId, out _);
                    inGameData.Broadcast(_socket, ProtobufSerializer.Serialize(
                        (uint)InGamePacketType.GameOverEvent,
                        new GameOverEventPacket { WinnerPlayerId = packet.AttackerId }));
                    Console.WriteLine($"[GameOver] CoreId={packet.TargetId} 파괴. Winner={packet.AttackerId}");
                    return;
                }

                inGameData.BuildingDataMap.TryRemove(packet.TargetId, out _);

                var buildingReward = RewardData.GetBuildingReward(building.BuildingType);

                inGameData.Broadcast(_socket, ProtobufSerializer.Serialize(
                    (uint)InGamePacketType.DeathEvent,
                    new DeathEventPacket
                    {
                        TargetId   = packet.TargetId,
                        TargetType = (int)UnitType.Building,
                        KillerId   = packet.AttackerId,
                        GoldReward = buildingReward.Gold,
                    }));

                if (packet.AttackerId != 0
                    && inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var buildingKiller))
                {
                    buildingKiller.Gold += buildingReward.Gold;
                    buildingKiller.Exp  += buildingReward.Exp;
                    SendProto((uint)InGamePacketType.RewardUpdate, new RewardUpdatePacket
                    {
                        PlayerId    = buildingKiller.PlayerId,
                        GoldAmount  = buildingReward.Gold,  TotalGold   = buildingKiller.Gold,
                        ExpAmount   = buildingReward.Exp,   TotalExp    = buildingKiller.Exp,
                        Level       = buildingKiller.Level,
                        RequiredExp = buildingKiller.RequiredExp,
                    }, (IPEndPoint)buildingKiller.IpEndPoint);
                }

                Console.WriteLine($"[Death] Building buildingId={packet.TargetId} destroyed by player={packet.AttackerId}");
                break;
            }
        }
    }

    private void BroadcastDamageResult(InGameData inGameData, DamageResultPacket packet)
    {
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.DamageResult, packet);
        inGameData.Broadcast(_socket, buf);
    }

    #endregion

    #region 인벤토리/상점/무기

    private void HandleWeaponChange(WeaponChangePacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        // 서버 저장값에서 직접 조회 (클라 값 무시) — SlotIndex 0-based
        ItemName newItem = packet.SlotIndex switch
        {
            0 => unit.Shortcut1,
            1 => unit.Shortcut2,
            2 => unit.Shortcut3,
            3 => unit.Shortcut4,
            _ => ItemName.None
        };

        if (newItem == ItemName.None) return;

        unit.CurrentGrippingItem = newItem;
        unit.CurrentSlotIndex = packet.SlotIndex;

        // 브로드캐스트할 땐 서버 검증값으로 덮어쓰기
        packet.ItemName = (int)newItem;
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.WeaponChange, packet);
        inGameData.Broadcast(_socket, buf);
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

        var weaponType = (ItemName)packet.WeaponType;
        var targetType = (UnitType)packet.TargetType;
        int rawDamage  = CombatData.GetDamageByItemName(weaponType);

        float currentHp = 0, maxHp = 0;
        int   finalDamage = 0;

        if (targetType == UnitType.Building)
        {
            
        }
        else if (targetType == UnitType.Npc)
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
                        TargetType = (int)UnitType.Npc,
                        KillerId   = packet.AttackerId,
                        GoldReward = RewardData.NpcKillReward.Gold,
                    });
                inGameData.Broadcast(_socket, deadBuf);

                if (inGameData.PlayerUnitDataMap.TryGetValue(packet.AttackerId, out var npcKiller))
                {
                    var npcReward2 = RewardData.NpcKillReward;
                    npcKiller.Gold    += npcReward2.Gold;
                    npcKiller.Exp     += npcReward2.Exp;
                    npcKiller.CSCount++;

                    SendProto((uint)InGamePacketType.RewardUpdate,
                        new RewardUpdatePacket
                        {
                            PlayerId   = npcKiller.PlayerId,
                            GoldAmount = npcReward2.Gold,
                            TotalGold  = npcKiller.Gold,
                            ExpAmount   = npcReward2.Exp,
                            TotalExp    = npcKiller.Exp,
                            CSCount     = npcKiller.CSCount,
                            Level       = npcKiller.Level,
                            RequiredExp = npcKiller.RequiredExp,
                        },
                        (IPEndPoint)npcKiller.IpEndPoint);
                }
            }
        }
        else if (targetType == UnitType.Player)
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
                        TargetType = (int)UnitType.Player,
                        KillerId   = packet.AttackerId,
                        GoldReward = RewardData.PlayerKillReward.Gold,
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
                    var playerReward2 = RewardData.PlayerKillReward;
                    playerKiller.Gold      += playerReward2.Gold;
                    playerKiller.Exp       += playerReward2.Exp;
                    playerKiller.KillCount++;

                    SendProto((uint)InGamePacketType.RewardUpdate,
                        new RewardUpdatePacket
                        {
                            PlayerId   = playerKiller.PlayerId,
                            GoldAmount = playerReward2.Gold,
                            TotalGold  = playerKiller.Gold,
                            ExpAmount   = playerReward2.Exp,
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

    #endregion

    #region 리스폰/스코어

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
            MoveX       = packet.MoveX,
            MoveZ       = packet.MoveZ,
            IsRunning   = packet.IsRunning,
            IsZooming   = packet.IsZooming,
        });
        inGameData.Broadcast(_socket, remoteBuf, excludePlayerId: packet.PlayerId);

        // 서버 측 플레이어 위치 갱신 — NPC 추적 거리 계산에 사용
        if (inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit))
            unit.SetPosition(
                new System.Numerics.Vector3(packet.PosX, packet.PosY, packet.PosZ),
                new System.Numerics.Vector3(0, packet.RotationY, 0));
    }
    #endregion

    #region 인벤토리/상점/무기

    private void HandleBuyRequest(BuyRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        string   msg      = "";
        ItemName itemName = (ItemName)packet.ItemType;

        if (itemName == ItemName.None)
        {
            msg = "알 수 없는 아이템입니다.";
        }
        else if (unit.Gold < packet.ItemPrice)
        {
            msg = "골드가 부족합니다!";
        }
        else
        {
            unit.Gold -= packet.ItemPrice;
            unit.AddInventory(itemName);
            msg = $"{itemName} 을(를) 구매하였습니다.";
            Console.WriteLine($"[ShopServer] PlayerId={packet.PlayerId} 구매: {itemName} (가격: {packet.ItemPrice})");
        }

        SendProto((uint)InGamePacketType.BuyResponse, new BuyResponsePacket
        {
            PlayerId   = unit.PlayerId,
            FieldId    = unit.FieldId,
            Msg        = msg,
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
            CurrentGrippingItemId = (int)unit.CurrentGrippingItem,
        }, clientEp);
    }
    
    private void HandleShortcutSwitchRequest(ShortcutSwitchRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        ItemType grippingItemType = ItemType.None;
        ItemName grippingItemId = unit.SetAndReturnGrippingItem(packet.SlotIndex);

        if (grippingItemId == ItemName.Rifle_Normal  || grippingItemId == ItemName.Rifle_HK416  ||
            grippingItemId == ItemName.Sniper_Normal || grippingItemId == ItemName.Sniper_Barret ||
            grippingItemId == ItemName.NormalGrenade || grippingItemId == ItemName.Bazuka)
            grippingItemType = ItemType.Weapon;
        else if (grippingItemId == ItemName.Tank)
            grippingItemType = ItemType.Vehicle;
        else if (grippingItemId == ItemName.Turret    || grippingItemId == ItemName.CheckPoint  ||
                 grippingItemId == ItemName.Artillery || grippingItemId == ItemName.GyunInPo)
            grippingItemType = ItemType.Building;
        else if (grippingItemId == ItemName.NormalMissile || grippingItemId == ItemName.NuclearMissile)
            grippingItemType = ItemType.Missile;
        
        byte[] buffer = ProtobufSerializer.Serialize((uint)InGamePacketType.ShortcutSwitchResponse,
            new ShortcutSwitchResponsePacket
            {
                PlayerId = unit.PlayerId,
                FieldId = unit.FieldId,
                CurrentGrippingItemId = (int)grippingItemId,
                CurrentGrippingItemType = (int)grippingItemType,
                SlotIndex = packet.SlotIndex,
            });
        
        inGameData.Broadcast(_socket, buffer);
    }
    
    private void HandleBuildingBtnRequest(BuildingBtnRequestpacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        foreach (var item in unit.Inventory.Values)
        {
            if (item.Type != ItemType.Building) continue;
            SendProto((uint)InGamePacketType.BuildingBtnResponse, new BuildingBtnResponsePacket
            {
                PlayerId = unit.PlayerId,
                FieldId = unit.FieldId,
                ItemName =  item.ItemId,
                Amount = item.Amount,
            }, clientEp);
        }
        
    }

    private void HandleAnimTrigger(AnimTriggerPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.PlayerId, out var unit)) return;

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.AnimTrigger, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleInterceptTargetRequest(InterceptTargetRequestPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.OwnerId, out var unit)) return;

        
        SendProto((uint)InGamePacketType.InterceptTargetEvent,
            new InterceptTargetEventPacket
            {
                BuildingId      = packet.BuildingId,
                TargetNetworkId = packet.TargetNetworkId,
                TargetType      = packet.TargetType,
            },
            (IPEndPoint)unit.IpEndPoint);
    }

    private void HandleInterceptRotation(InterceptRotationPacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.OwnerId, out var unit)) return;

        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.InterceptRotation, packet);
        inGameData.Broadcast(_socket, buf);
    }

    private void HandleArtilleryInfoRequest(ArtilleryInfoRequestPacket packet, IPEndPoint clientEp)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.BuildingDataMap.TryGetValue(packet.BuildingId, out var building)) return;

        SendProto((uint)InGamePacketType.ArtilleryInfoResponse, new ArtilleryInfoResponsePacket
        {
            BuildingId          = packet.BuildingId,
            NormalMissileCount  = building.NormalMissileCount,
            NuclearMissileCount = building.NuclearMissileCount,
        }, clientEp);
    }

    private void HandleInterceptFire(InterceptFirePacket packet)
    {
        if (!_inGameDataList.TryGetValue(packet.FieldId, out var inGameData)) return;
        if (!inGameData.PlayerUnitDataMap.TryGetValue(packet.OwnerId, out var unit)) return;
        if(!inGameData.BuildingDataMap.TryGetValue(packet.BuildingId, out var building)) return;
        if (building.BuildingType != ItemName.Artillery) return;
        switch ((ItemName)packet.MissileType)
        {
            case ItemName.NormalMissile:
                if (building.NormalMissileCount <= 0) return;
                building.NormalMissileCount--;
                break;
            case ItemName.NuclearMissile:
                if (building.NuclearMissileCount <= 0) return;
                building.NuclearMissileCount--;
                break;
            default: return;
        }
        
        byte[] buf = ProtobufSerializer.Serialize((uint)InGamePacketType.InterceptFire, packet);
        inGameData.Broadcast(_socket, buf);
    }

    #endregion

    private void SendProto<T>(uint packetType, T message, IPEndPoint ep)
    {
        byte[] buffer = ProtobufSerializer.Serialize(packetType, message);
        _socket.SendTo(buffer, ep);
    }
}
