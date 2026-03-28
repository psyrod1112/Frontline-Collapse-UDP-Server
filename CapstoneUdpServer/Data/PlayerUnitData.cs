using System.Net;
using System.Numerics;

namespace CapstoneUdpServer.Data;

public class PlayerUnitData
{
    private readonly PlayerData _playerData;
    private readonly int _fieldId;
    private readonly int _currentHp;
    private readonly int _weaponPrefabIndex;
    private readonly Vector3 _position;
    private readonly Vector3 _rotation;

    // PlayerData 위임 프로퍼티
    public int PlayerId => _playerData.PlayerId;
    public string? PlayerName => _playerData.PlayerName;
    public EndPoint IpEndPoint => _playerData.ClientEp;

    public int FieldId => _fieldId;
    public int CurrentHp => _currentHp;
    public int WeaponPrefabIndex => _weaponPrefabIndex;
    public Vector3 Position => _position;
    public Vector3 Rotation => _rotation;

    public PlayerUnitData(PlayerData playerData, int fieldId)
    {
        _playerData = playerData;
        _fieldId = fieldId;
        _currentHp = 100;
        _weaponPrefabIndex = 0;
        _position = Vector3.Zero;
        _rotation = Vector3.Zero;
    }
}
