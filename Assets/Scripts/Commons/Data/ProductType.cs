/// <summary>상품 분류 코드. PK가 아니며 기존 분류의 의미와 숫자를 고정한다.</summary>
public enum ProductType : uint
{
    None = 0,
    Water = 1,
    Food = 2,
    Medicine = 3,
    DailyNecessities = 4,
    Tools = 5,
    ElectricalEquipment = 6,
    ProtectiveEquipment = 7
}
