namespace CapstoneUdpServer.Data;

public enum MovingUnitName
{
    //이동물체
    Tank,
    ScanningDrone,
    AttackDrone,
    
    //플레이어
    SwordSoldier,
    RifleSoldier,
    FireSoldier
    
}

public enum MovingUnitType
{
    VehicleUnit,
    SoldierUnit
}


public class MovingUnitData
{
    private MovingUnitName _name;
    private MovingUnitType _type;
}