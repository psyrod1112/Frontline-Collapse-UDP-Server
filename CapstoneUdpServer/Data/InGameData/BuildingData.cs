namespace CapstoneUdpServer.Data;

public enum BuildingName
{
    //공격물
    BuriedTurret, //콜라이더 2개로 두고 큰 콜라이더는 플레이어 감지 -> 위로 솟아오름 , 작은 콜라이더는 피격 이벤트
    MissileLauncher, //미사일 발사대 : 다양한 미사일을 탑재해서 날릴 수 있음.
    LandMine, //지뢰 : 특징 - 나도 밟으면 ㅈ됨
    
    //방어물
    MissileIntercepter, //미사일 요격
    Barricade, //건물 보호
    Bunker, //벙커에서 공격 가능
    
    //유틸 
    NeuralDominator, // 유닛 회유 기점
    MedicalUnit, // 의무대
    Teleport, // 모든 이동 유닛 텔레포트 가능 - 적도 텔포 가능하다는 점.
    ControlTower
}

public class BuildingType
{
    
}



public class BuildingData : UnitData
{
    private BuildingType _buildingType;
    
    
    
    
}