using System;

/// <summary>개별 성향 설정 PK와 별개인 손님 타입. 종료 표식은 유효 타입이 아니다.</summary>
public enum CustomerDispositionType
{
    None = 0,
    Normal = 1,
    Hasty = 2,
    PriceSensitive = 3,
    Wealthy = 4,
    CustomerDispositionType_End
}

/// <summary>외형·성향과 독립인 방문 속성. 성별·연령군 내부의 배타 조합은 허용하지 않는다.</summary>
[Flags]
public enum CustomerAttributes
{
    None = 0,
    Male = 1,
    Female = 2,
    Child = 4,
    Elderly = 8
}

/// <summary>생성 정책과 무관한 타입·속성 계약 검증. 등장 확률이나 None 추첨 여부를 결정하지 않는다.</summary>
public static class CustomerProfileValidation
{
    /// <summary>명시된 실제 타입만 허용한다.</summary>
    /// <param name="type">검증할 타입.</param>
    /// <exception cref="ArgumentOutOfRangeException">None·종료 표식·미정의 타입.</exception>
    public static void ValidateType(CustomerDispositionType type)
    {
        if (type != CustomerDispositionType.Normal && type != CustomerDispositionType.Hasty &&
            type != CustomerDispositionType.PriceSensitive && type != CustomerDispositionType.Wealthy)
            throw new ArgumentOutOfRangeException(nameof(type), type, "유효한 손님 타입이 필요합니다.");
    }

    /// <summary>속성의 비트와 배타 조합만 검증한다. None은 표현 가능한 값이며 생성 허용 정책과 별개다.</summary>
    /// <param name="attributes">검증할 조합.</param>
    /// <exception cref="ArgumentException">미정의 비트 또는 배타 속성이 함께 지정됨.</exception>
    public static void ValidateAttributes(CustomerAttributes attributes)
    {
        const CustomerAttributes Gender = CustomerAttributes.Male | CustomerAttributes.Female;
        const CustomerAttributes Age = CustomerAttributes.Child | CustomerAttributes.Elderly;
        if ((attributes & ~(Gender | Age)) != 0 || (attributes & Gender) == Gender || (attributes & Age) == Age)
            throw new ArgumentException("정의되지 않거나 배타적인 손님 속성입니다.", nameof(attributes));
    }
}
