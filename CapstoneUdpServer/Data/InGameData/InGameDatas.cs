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
    Tank,
    Bullet,
    Shell
}

public enum UnitType { Player, Npc, Boss, Building, Environment,
    Missile
}
public enum AnimTriggerType { Jump = 0, ThrowGrenade = 1, Hit = 2, Shot = 3 }


public class BuildingStat
{
    public float MaxHp { get; set; }
    public int Defense { get; set; }
}

public static class CombatData
{
    // BuildingType 없이 ItemName으로 직접 스탯 관리
    public static readonly Dictionary<ItemName, BuildingStat> BuildingStats = new()
    {
        [ItemName.Artillery]  = new BuildingStat { MaxHp = 500f, Defense = 30 },
        [ItemName.GyunInPo]   = new BuildingStat { MaxHp = 500f, Defense = 30 },
        [ItemName.Turret]     = new BuildingStat { MaxHp = 420f, Defense = 35 },
        [ItemName.CheckPoint] = new BuildingStat { MaxHp = 800f, Defense = 40 },
    };

    private static readonly Dictionary<ItemName, int> ItemDamageMap = new()
    {
        [ItemName.Rifle_Normal]    = 50,
        [ItemName.Rifle_HK416]    = 70,
        [ItemName.Sniper_Normal]   = 65,
        [ItemName.Sniper_Barret]   = 80,
        [ItemName.NormalGrenade]   = 90,
        [ItemName.Bazuka]          = 120,
        [ItemName.NormalMissile]   = 450,
        [ItemName.NuclearMissile]  = 3500,
        [ItemName.Bullet]          = 60,
        [ItemName.Shell]           = 120,
        [ItemName.AirPlane]        = 200,
    };

    public static int GetDamageByItemName(ItemName itemName) =>
        ItemDamageMap.TryGetValue(itemName, out var dmg) ? dmg : 10;

    public static BuildingStat GetBuildingStat(ItemName item)
    {
        return BuildingStats.TryGetValue(item, out var stat) ? stat : new BuildingStat { MaxHp = 300f, Defense = 10 };
    }
}


public class KillReward
{
    public int Gold { get; set; }
    public int Exp  { get; set; }
}

public static class RewardData
{
    public static readonly KillReward NpcKillReward    = new() { Gold = 100, Exp = 100 };
    public static readonly KillReward PlayerKillReward = new() { Gold = 200, Exp = 200 };

    private static readonly Dictionary<ItemName, KillReward> BuildingKillRewards = new()
    {
        [ItemName.Turret]     = new KillReward { Gold = 100, Exp =  80 },
        [ItemName.Artillery]  = new KillReward { Gold = 150, Exp = 100 },
        [ItemName.GyunInPo]   = new KillReward { Gold = 120, Exp =  80 },
        [ItemName.CheckPoint] = new KillReward { Gold = 300, Exp = 200 },
    };

    public static KillReward GetBuildingReward(ItemName type) =>
        BuildingKillRewards.TryGetValue(type, out var r) ? r : new KillReward { Gold = 50, Exp = 30 };
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