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

    public int     FieldId           { get; }
    public float   CurrentHp         { get; set; }
    public float   MaxHp             { get; }
    public int     WeaponPrefabIndex { get; }
    public Vector3 Position          { get; set; }
    public Vector3 Rotation          { get; set; }

    public PlayerUnitData(PlayerData playerData, int fieldId)
    {
        _playerData      = playerData;
        FieldId          = fieldId;
        MaxHp            = 100f;
        CurrentHp        = 100f;
        WeaponPrefabIndex = 0;
        Position         = Vector3.Zero;
        Rotation         = Vector3.Zero;
    }
}
