using System;
using System.Collections.Concurrent;
using System.Net;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class PlayerUnitData
{
    
    private readonly PlayerData _playerData;
    public bool IsDead          { get; private set; }
    /// <summary>HandleDeathRequest 에서 사망 처리 완료 시 true — 중복 처리 방지</summary>
    public bool DeathProcessed  { get; set; }

    // PlayerData 위임 프로퍼티
    public int      PlayerId   => _playerData.PlayerId;
    public string?  PlayerName => _playerData.PlayerName;
    public EndPoint IpEndPoint => _playerData.ClientEp;
    public PlayerRank PlayerRank => _playerData.PlayerRank;
    public int     FieldId            => _playerData.FieldId;

    private int _gold;
    public int Gold
    {
        get => _gold;
        set
        {
            int delta = value - _gold;
            _gold = value;
            if (_gold < 0) _gold = 0;
            if (delta > 0) TotalGold += delta;
        }
    }
    public int TotalGold { get; private set; }

    private int _level;
    public int Level
    {
        get => _level;
        private set
        {
            _level = value;
            if (_level <= 1)
            {
                _level = 1;
            }
        }
    }

    private float _exp;

    public float Exp
    {
        get => _exp;
        set
        {
            _exp = value;
            // 레벨업 체크는 set에서
            while (_exp >= RequiredExp)
            {
                _exp -= RequiredExp;
                Level++;  // 레벨업
            }
        }
    }

    public float RequiredExp => CalcMaxExp(Level);

    private float _currentHp;
    public float CurrentHp
    {
        get => _currentHp;
        set
        {
            _currentHp = value;
            if (_currentHp <= 0)
            {
                _currentHp = 0;
                IsDead = true;
            }
        }
    }
    
    public void Revive(float hp)
    {
        _currentHp     = hp;
        IsDead         = false;
        DeathProcessed = false;
    }
    
    public float MaxHp   => 100f + (Level - 1) * 10f; // 레벨당 +10 HP
    public ItemName Shortcut1        { get; set; }
    public ItemName Shortcut2        { get; set; }
    public ItemName Shortcut3        { get; set; }
    public ItemName Shortcut4        { get; set; }
    public ItemName CurrentGrippingItem { get; set; }
    public Vector3 Position          { get; set; }
    public Vector3 Rotation          { get; set; }
    public int KillCount {get; set; }
    public int DeathCount {get; set; }
    public int CSCount {get; set; }
    public int   GuidedMissileCount    { get; set; }
    public int   NukeMissileCount      { get; set; }
    public float FinalBuildingMaxHp     { get; set; } 

    public int HotkeyIndex { get; set; } // 1, 2, 3, 4임

    private float _finalBuildingCurrentHp;

    public float FinalBuildingCurrentHp
    {
        get => _finalBuildingCurrentHp;
        set
        {
            _finalBuildingCurrentHp = value;
            if (_finalBuildingCurrentHp < 0) 
                _finalBuildingCurrentHp = 0;
        }
    }

    public int CurrentSlotIndex { get; set; }
    public int Defense => (Level - 1) * 3;             // 레벨당 +3 방어

    public ConcurrentDictionary<ItemName, ItemData> Inventory = new();

    public PlayerUnitData(PlayerData playerData, int fieldId)
    {
        _playerData = playerData;
        _playerData.SetFieldId(fieldId);

        Gold = 99999;
        Level = 1;
        Exp = 0;
        Shortcut1           = ItemName.Rifle_Normal;
        Shortcut2           = ItemName.None;
        Shortcut3           = ItemName.None;
        Shortcut4           = ItemName.None;
        CurrentGrippingItem = Shortcut1;
        CurrentSlotIndex = 0;

        CurrentHp = MaxHp;

        Inventory[ItemName.Rifle_Normal] = new ItemData
        {
            ItemId = (int)ItemName.Rifle_Normal,
            Type   = ItemType.Weapon,
            Amount = 1,
        };

        KillCount = 0;
        DeathCount = 0;
        CSCount = 0;
        GuidedMissileCount = 0;
        NukeMissileCount = 0;
        HotkeyIndex = 0; // 0-based


    }
    
    private float CalcMaxExp(int level)
    {
        return 100f * (float)Math.Pow(level, 1.5);
    }

    public void SetPosition(Vector3 pos, Vector3 rot)
    {
        Position = pos;
        Rotation = rot;
    }

    public void AddInventory(ItemName itemName)
    {
        Inventory.AddOrUpdate(
            itemName,
            _ => new ItemData
            {
                ItemId = (int)itemName,
                Type   = InGameDatas.ItemNameToItemType(itemName),
                Amount = 1
            },
            (_, existing) =>
            {
                existing.Amount++;
                return existing;
            });
    }

    public bool RemoveInventory(ItemName itemName)
    {
        Inventory[itemName].Amount--;
        if (Inventory[itemName].Amount <= 0)
        {
            Inventory.TryRemove(itemName, out _);
            return true;
        }

        return false;
    }

    public bool HasItemInventory(ItemName itemName)
    {
        return Inventory.ContainsKey(itemName) && Inventory[itemName].Amount >= 1;
    }

    // index: 0-based (0~3)
    public ItemName SetAndReturnGrippingItem(int index)
    {
        CurrentSlotIndex = index;
        switch (index)
        {
            case 0: CurrentGrippingItem = Shortcut1; break;
            case 1: CurrentGrippingItem = Shortcut2; break;
            case 2: CurrentGrippingItem = Shortcut3; break;
            case 3: CurrentGrippingItem = Shortcut4; break;
            default:
                CurrentGrippingItem = ItemName.None;
                break;
        }
        return CurrentGrippingItem;
    }
    

    public void LevelDown()
    {
        Level -= 3;
        Exp = 0;
    }
}
