using System.Collections.Generic;

namespace CapstoneUdpServer.Data;


public enum ItemType
{
    None,
    Weapon,
    Vehicle,
    Building,
    Missile,
    Heal,
}

public enum ItemName
{
    None,
    Rifle_Normal,
    Rifle_HK416,
    Sniper_Normal,
    Sniper_Barret,
    NormalGrenade,
    AirPlane,
    Bazuka,
    GyunInPo,
    Turret,
    CheckPoint,
    Artillery,
    HealPack,
    NormalMissile,
    NuclearMissile,
}


public enum WeaponType
{
    None,
    Rifle,
    Sniper,
    Grenade,
    SmokeGrenade,
    MissileGuided,
    MissileNuke,
    MissileChem,
}

public enum BuildingType
{
    Artillery,
    RadarArtillery,
    Interceptor,
    Barricade,
    Shelter,
    MedBay,
    TrainingCenter,
    Workshop,
    Warehouse,
    ControlTower,
}



public enum NPCType
{
    Normal,
    Chaos,
}

public class WeaponStat
{
    public int Damage { get; set; }
}

public class BuildingStat
{
    public float MaxHp { get; set; }
    public int Defense { get; set; }
}

public static class CombatData
{
    public static readonly Dictionary<WeaponType, WeaponStat> WeaponStats = new()
    {
        [WeaponType.Rifle] = new WeaponStat { Damage = 20 },
        [WeaponType.Sniper] = new WeaponStat { Damage = 65 },
        [WeaponType.Grenade] = new WeaponStat { Damage = 90 },
        [WeaponType.MissileGuided] = new WeaponStat { Damage = 180 },
        [WeaponType.MissileNuke] = new WeaponStat { Damage = 400 },
    };

    // BuildingType 없이 ItemName으로 직접 스탯 관리
    public static readonly Dictionary<ItemName, BuildingStat> BuildingStats = new()
    {
        [ItemName.Artillery]  = new BuildingStat { MaxHp = 500f, Defense = 30 },
        [ItemName.GyunInPo]   = new BuildingStat { MaxHp = 500f, Defense = 30 },
        [ItemName.Turret]     = new BuildingStat { MaxHp = 420f, Defense = 35 },
        [ItemName.CheckPoint] = new BuildingStat { MaxHp = 800f, Defense = 70 },
    };

    public static int GetWeaponDamage(WeaponType type)
    {
        return WeaponStats.TryGetValue(type, out var stat) ? stat.Damage : 10;
    }

    // ItemName → Damage (HitRequestPacket 용)
    private static readonly Dictionary<ItemName, int> ItemDamageMap = new()
    {
        [ItemName.Rifle_Normal]   = 20,
        [ItemName.Rifle_HK416]   = 25,
        [ItemName.Sniper_Normal]  = 65,
        [ItemName.Sniper_Barret]  = 80,
        [ItemName.NormalGrenade]  = 90,
        [ItemName.Bazuka]         = 70,
    };

    public static int GetDamageByItemName(ItemName itemName) =>
        ItemDamageMap.TryGetValue(itemName, out var dmg) ? dmg : 10;

    public static BuildingStat GetBuildingStat(ItemName item)
    {
        return BuildingStats.TryGetValue(item, out var stat) ? stat : new BuildingStat { MaxHp = 300f, Defense = 10 };
    }
}


public class ItemData
{

    public int ItemId { get; set; }
    public ItemType Type { get; set; }
    public int Amount { get; set; }

}

public class InGameDatas
{
    public static ItemType ItemNameToItemType(ItemName item) => item switch
    {
        ItemName.Rifle_Normal  or ItemName.Rifle_HK416  or
            ItemName.Sniper_Normal or ItemName.Sniper_Barret or
            ItemName.NormalGrenade or ItemName.Bazuka        => ItemType.Weapon,

        ItemName.Tank                                    => ItemType.Vehicle,

        ItemName.GyunInPo or ItemName.Turret or
            ItemName.CheckPoint or ItemName.Artillery        => ItemType.Building,

        ItemName.HealPack                                => ItemType.Heal,

        ItemName.NormalMissile or ItemName.NuclearMissile => ItemType.Missile,

        _ => ItemType.None
    };
    
    
}