using System;
using System.Collections.Concurrent;
using System.Net;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class PlayerUnitData
{
    
    private readonly PlayerData _playerData;
    public bool IsDead {get; private set;}

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
        _currentHp = hp;
        IsDead = false;
    }
    
    public float    MaxHp            { get; set; }
    public ItemName Shortcut1        { get; set; }
    public ItemName Shortcut2        { get; set; }
    public ItemName Shortcut3        { get; set; }
    public ItemName Shortcut4        { get; set; }
    public int CurrentGrippingItem { get; set; }
    public Vector3 Position          { get; set; }
    public Vector3 Rotation          { get; set; }
    public int KillCount {get; set; }
    public int DeathCount {get; set; }
    public int CSCount {get; set; }
    public int   GuidedMissileCount    { get; set; }
    public int   NukeMissileCount      { get; set; }
    public float FinalBuildingMaxHp     { get; set; }


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

    public ConcurrentDictionary<int, InGameBuildingRecord> Buildings { get; } = new();
    public ConcurrentDictionary<ItemName, ItemData> Inventory = new();

    public PlayerUnitData(PlayerData playerData, int fieldId)
    {
        _playerData = playerData;
        _playerData.SetFieldId(fieldId);

        Gold = 0;
        Level = 1;
        Exp = 0;
        Shortcut1           = ItemName.Rifle;
        Shortcut2           = ItemName.None;
        Shortcut3           = ItemName.None;
        Shortcut4           = ItemName.None;
        CurrentGrippingItem = 1;

        MaxHp     = 100f;
        CurrentHp = 100f;

        Inventory[ItemName.Rifle] = new ItemData
        {
            ItemId = (int)ItemName.Rifle,
            Type   = ItemType.Gun,
            Amount = 1,
        };

        KillCount = 0;
        DeathCount = 0;
        CSCount = 0;
        GuidedMissileCount = 3;
        NukeMissileCount = 1;
        
        
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

    public void AddInventory(ItemName itemName, ShopList shopItem)
    {
        Inventory.AddOrUpdate(
            itemName,
            _ => new ItemData
            {
                ItemId = (int)itemName,
                ShopType = shopItem,
                Type = ShopListToItemType(shopItem),
                Amount = 1
            },
            (_, existing) =>
            {
                existing.Amount++;
                return existing;
            });
    }



    private ItemType ShopListToItemType(ShopList item) => item switch
    {
        ShopList.Grenade => ItemType.Grenade,
        ShopList.Tank => ItemType.Vehicle,
        ShopList.Artillery => ItemType.Building,
        ShopList.Missile => ItemType.Missile,
        _ => throw new ArgumentOutOfRangeException()
    };

    public void LevelDown()
    {
        Level -= 3;
        Exp = 0;
    }
}
