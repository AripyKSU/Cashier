using System;

/// <summary>손님 외형의 성인 기준 높이와 Child 표시 보정만 계산하는 순수 값 형식.</summary>
public readonly struct CustomerPortraitLayout
{
    public const float DefaultChildPortraitScale = .6f;
    public const float DefaultChildPortraitRise = .001090909f;
    public const float DefaultChildCounterSinkPixels = 8f;
    public const float MinChildPortraitScale = .15f;
    public const float MaxChildPortraitScale = 1f;
    public const float MinChildPortraitRise = 0f;
    public const float MaxChildPortraitRise = .6f;

    public float DisplayScale { get; }
    public float DisplayHeight { get; }
    /// <summary>양수는 상승, 음수는 계산대 뒤로 내려가는 최종 수직 오프셋.</summary>
    public float RisePixels { get; }

    private CustomerPortraitLayout(float displayScale, float displayHeight, float risePixels)
    {
        DisplayScale = displayScale;
        DisplayHeight = displayHeight;
        RisePixels = risePixels;
    }

    /// <summary>방문 속성과 성인 기준 높이로 표시 결과를 계산한다.</summary>
    /// <param name="attributes">CustomerVisit.Attributes의 완전 또는 부분 속성.</param>
    /// <param name="adultDisplayHeight">Child 보정 전 기준 높이.</param>
    /// <param name="childPortraitScale">Child 표시 배율.</param>
    /// <param name="childPortraitRise">성인 기준 높이에 곱할 Child 상승 비율.</param>
    /// <returns>표시 배율·높이·상승량.</returns>
    public static CustomerPortraitLayout Calculate(
        CustomerAttributes attributes,
        float adultDisplayHeight,
        float childPortraitScale = DefaultChildPortraitScale,
        float childPortraitRise = DefaultChildPortraitRise)
    {
        CustomerProfileValidation.ValidateAttributes(attributes);
        ValidateParameters(adultDisplayHeight, childPortraitScale, childPortraitRise);

        bool isChild = (attributes & CustomerAttributes.Child) != 0;
        float displayScale = isChild ? childPortraitScale : 1f;
        return new CustomerPortraitLayout(
            displayScale,
            adultDisplayHeight * displayScale,
            isChild ? adultDisplayHeight * childPortraitRise - DefaultChildCounterSinkPixels : 0f);
    }

    /// <summary>두 view가 공유하는 성인 높이·Child 보정 입력을 검사한다.</summary>
    public static void ValidateParameters(float adultDisplayHeight, float childPortraitScale, float childPortraitRise)
    {
        if (!isFinitePositive(adultDisplayHeight))
            throw new ArgumentOutOfRangeException(nameof(adultDisplayHeight), adultDisplayHeight, "성인 표시 높이는 유한한 양수여야 합니다.");
        if (!isFinite(childPortraitScale) || childPortraitScale < MinChildPortraitScale || childPortraitScale > MaxChildPortraitScale)
            throw new ArgumentOutOfRangeException(nameof(childPortraitScale), childPortraitScale, "Child 표시 배율은 0.15~1 범위여야 합니다.");
        if (!isFinite(childPortraitRise) || childPortraitRise < MinChildPortraitRise || childPortraitRise > MaxChildPortraitRise)
            throw new ArgumentOutOfRangeException(nameof(childPortraitRise), childPortraitRise, "Child 상승 비율은 0~0.6 범위여야 합니다.");
    }

    /// <summary>MonoBehaviour 초기화 조건에서 사용할 공용 유효성 검사.</summary>
    public static bool AreParametersValid(float adultDisplayHeight, float childPortraitScale, float childPortraitRise)
    {
        return isFinitePositive(adultDisplayHeight) &&
            isFinite(childPortraitScale) && childPortraitScale >= MinChildPortraitScale && childPortraitScale <= MaxChildPortraitScale &&
            isFinite(childPortraitRise) && childPortraitRise >= MinChildPortraitRise && childPortraitRise <= MaxChildPortraitRise;
    }

    private static bool isFinitePositive(float value) => value > 0 && isFinite(value);

    private static bool isFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
