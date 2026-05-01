using System.Collections.Generic;

namespace CapstoneUdpServer.Data;

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

    public static readonly Dictionary<BuildingType, BuildingStat> BuildingStats = new()
    {
        [BuildingType.Artillery] = new BuildingStat { MaxHp = 500f, Defense = 30 },
        [BuildingType.RadarArtillery] = new BuildingStat { MaxHp = 450f, Defense = 25 },
        [BuildingType.Interceptor] = new BuildingStat { MaxHp = 420f, Defense = 35 },
        [BuildingType.Barricade] = new BuildingStat { MaxHp = 650f, Defense = 60 },
        [BuildingType.Shelter] = new BuildingStat { MaxHp = 800f, Defense = 70 },
        [BuildingType.MedBay] = new BuildingStat { MaxHp = 350f, Defense = 15 },
        [BuildingType.TrainingCenter] = new BuildingStat { MaxHp = 350f, Defense = 15 },
        [BuildingType.Workshop] = new BuildingStat { MaxHp = 400f, Defense = 20 },
        [BuildingType.Warehouse] = new BuildingStat { MaxHp = 450f, Defense = 20 },
        [BuildingType.ControlTower] = new BuildingStat { MaxHp = 1000f, Defense = 80 },
    };

    public static int GetWeaponDamage(WeaponType type)
    {
        return WeaponStats.TryGetValue(type, out var stat) ? stat.Damage : 10;
    }

    public static BuildingStat GetBuildingStat(BuildingType type)
    {
        return BuildingStats.TryGetValue(type, out var stat) ? stat : new BuildingStat { MaxHp = 300f, Defense = 10 };
    }
}
