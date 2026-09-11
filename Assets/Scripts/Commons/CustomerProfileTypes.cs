using System;

/// <summary>개별 성향 설정 PK와 별개인 손님 타입. 종료 표식은 유효 타입이 아니다.</summary>
public enum CustomerDispositionType
{
    None = 0,
    Normal = 1,
    Hasty = 2,
    PriceSensitive = 3,
    Wealthy = 4,
    Poor = 5,
    CustomerDispositionType_End
}

/// <summary>외형·성향과 독립인 성별·연령·특수 속성. 각 축 내부의 배타 조합은 허용하지 않는다.</summary>
[Flags]
public enum CustomerAttributes
{
    None = 0,
    Male = 1,
    Female = 2,
    Child = 4,
    Elderly = 8,
    Adult = 16,
    Normal = 32
}

/// <summary>생성 정책과 무관한 타입·속성 계약 검증. 등장 확률이나 None 추첨 여부를 결정하지 않는다.</summary>
public static class CustomerProfileValidation
{
    private const CustomerAttributes Gender = CustomerAttributes.Male | CustomerAttributes.Female;
    private const CustomerAttributes Age = CustomerAttributes.Child | CustomerAttributes.Elderly | CustomerAttributes.Adult;
    private const CustomerAttributes Special = CustomerAttributes.Normal;

    /// <summary>명시된 실제 타입만 허용한다.</summary>
    /// <param name="type">검증할 타입.</param>
    /// <exception cref="ArgumentOutOfRangeException">None·종료 표식·미정의 타입.</exception>
    public static void ValidateType(CustomerDispositionType type)
    {
        if (type != CustomerDispositionType.Normal && type != CustomerDispositionType.Hasty &&
            type != CustomerDispositionType.PriceSensitive && type != CustomerDispositionType.Wealthy &&
            type != CustomerDispositionType.Poor)
            throw new ArgumentOutOfRangeException(nameof(type), type, "유효한 손님 타입이 필요합니다.");
    }

    /// <summary>지침의 부분조건에 사용할 비트와 축별 배타 조합을 검증한다. None도 표현 가능하나 완전한 방문 속성은 아니다.</summary>
    /// <param name="attributes">검증할 조합.</param>
    /// <exception cref="ArgumentException">미정의 비트 또는 배타 속성이 함께 지정됨.</exception>
    public static void ValidateAttributes(CustomerAttributes attributes)
    {
        if ((attributes & ~(Gender | Age | Special)) != 0 || hasMultipleBits(attributes & Gender) ||
            hasMultipleBits(attributes & Age) || hasMultipleBits(attributes & Special))
            throw new ArgumentException("정의되지 않거나 배타적인 손님 속성입니다.", nameof(attributes));
    }

    /// <summary>실제 방문에는 성별·연령·특수 속성이 각각 정확히 하나씩 지정되어야 한다.</summary>
    /// <param name="attributes">방문에 복사할 완전한 조합.</param>
    /// <exception cref="ArgumentException">미정의·배타 속성 또는 누락된 축.</exception>
    public static void ValidateCompleteAttributes(CustomerAttributes attributes)
    {
        ValidateAttributes(attributes);
        if ((attributes & Gender) == 0 || (attributes & Age) == 0 || (attributes & Special) == 0)
            throw new ArgumentException("방문에는 성별·연령·특수 속성이 각각 필요합니다.", nameof(attributes));
    }

    /// <summary>한 축에서 두 개 이상의 비트가 선택됐는지 검사한다.</summary>
    /// <param name="attributes">하나의 축에 해당하는 비트.</param>
    /// <returns>동시에 여러 값이 지정됐으면true.</returns>
    private static bool hasMultipleBits(CustomerAttributes attributes)
    {
        int bits = (int)attributes;
        return (bits & (bits - 1)) != 0;
    }
}
