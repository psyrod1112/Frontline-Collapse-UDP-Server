using System.Net;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class PlayerUnitData
{
    private readonly PlayerData _playerData;

    // PlayerData 위임 프로퍼티
    public int      PlayerId   => _playerData.PlayerId;
    public string?  PlayerName => _playerData.PlayerName;
    public EndPoint IpEndPoint => _playerData.ClientEp;
    public PlayerRank PlayerRank => _playerData.PlayerRank;
    public int     FieldId           { get; }
    
    public int     Gold { get; set; }
    public int     Level { get; set; }
    public float   Exp { get; set; }
    public float   RequiredExp         { get; set; }
    public float   CurrentHp         { get; set; }
    public float   MaxHp             { get; set; }
    public WeaponType     WeaponPrefabIndex_1 { get; set; }
    public WeaponType     WeaponPrefabIndex_2 { get; set; }
    public WeaponType     WeaponPrefabIndex_3 { get; set; }
    public WeaponType     WeaponPrefabIndex_4 { get; set; }
    public WeaponType     CurrentWeaponPrefabIndex { get; }
    public Vector3 Position          { get; set; }
    public Vector3 Rotation          { get; set; }
    public int KillCount {get; set; }
    public int DeathCount {get; set; }
    public int CSCount {get; set; }

    public PlayerUnitData(PlayerData playerData, int fieldId)
    {
        _playerData      = playerData;
        FieldId          = fieldId;

        Gold = 0;
        Level = 1;
        Exp = 0;
        RequiredExp = CalcMaxExp(Level);
        WeaponPrefabIndex_1 = WeaponType.Rifle;
        WeaponPrefabIndex_2 = WeaponType.None;
        WeaponPrefabIndex_3 = WeaponType.None;
        WeaponPrefabIndex_4 = WeaponType.None;
        
        MaxHp            = 100f;
        CurrentHp        = 100f;
        CurrentWeaponPrefabIndex = WeaponType.Rifle;
        Position         = Vector3.Zero;
        Rotation         = Vector3.Zero;

        KillCount = 0;
        DeathCount = 0;
        CSCount = 0;
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
}
