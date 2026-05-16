namespace CapstoneUdpServer.Data;
public enum ShopList
{
    Grenade,
    Tank,
    Artillery,
    Missile
}

public enum ItemType
{
    Grenade,
    Vehicle,
    Building,
    Missile,
    Gun
}

public enum ItemName
{
    None,
    Rifle,
    Sniper,
    NormalGrenade,
    Tank,
    Artillery,
    NormalMissile,
    NuclearMissile,
}

public class ItemData
{
    
    public int ItemId { get; set; }
    public ItemType Type { get; set; }
    public int Amount { get; set; }
    public ShopList ShopType { get; set; }
    
    
}