using System;
using System.Collections.Concurrent;
using System.Net;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class PlayerUnitData
{
    private readonly PlayerData _playerData;
    public bool isDead;

    // PlayerData 위임 프로퍼티
    public int      PlayerId   => _playerData.PlayerId;
    public string?  PlayerName => _playerData.PlayerName;
    public EndPoint IpEndPoint => _playerData.ClientEp;
    public PlayerRank PlayerRank => _playerData.PlayerRank;
    public int     FieldId            => _playerData.FieldId;
    
    public int     Gold { get; set; }
    public int     Level { get; set; }

    private float _exp;

    public float Exp
    {
        get => _exp;
        set
        {
            _exp = value;
            // 레벨업 체크는 set에서
            if (_exp >= RequiredExp)
            {
                _exp -= RequiredExp;
                Level++;  // 레벨업
            }
        }
    }

    public float RequiredExp => CalcMaxExp(Level);

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
    public int GuidedMissileCount { get; set; }
    public int NukeMissileCount { get; set; }

    

    public ConcurrentDictionary<int, InGameBuildingRecord> Buildings { get; } = new();

    public PlayerUnitData(PlayerData playerData, int fieldId)
    {
        _playerData = playerData;
        _playerData.SetFieldId(fieldId);

        Gold = 0;
        Level = 1;
        Exp = 0;
        WeaponPrefabIndex_1 = WeaponType.Rifle;
        WeaponPrefabIndex_2 = WeaponType.None;
        WeaponPrefabIndex_3 = WeaponType.None;
        WeaponPrefabIndex_4 = WeaponType.None;
        
        MaxHp            = 100f;
        CurrentHp        = 100f;
        CurrentWeaponPrefabIndex = WeaponType.Rifle;

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
}
