using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정산 화면의 설비 팜플렛 강조 이미지를 부드럽게 반복 점멸시킵니다.</summary>
public sealed class FacilityPamphletShine : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.06f;
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.48f;
    [SerializeField, Min(0.01f)] private float fadeInDurationSeconds = 0.9f;
    [SerializeField, Min(0.01f)] private float fadeOutDurationSeconds = 0.9f;
    [SerializeField, Min(0f)] private float intervalSeconds = 0.35f;

    private Sequence shineSequence;

    /// <summary>오브젝트가 표시될 때 현재 설정으로 반복 점멸을 시작합니다.</summary>
    private void OnEnable()
    {
        play();
    }

    /// <summary>오브젝트가 숨겨진 뒤 Tween이 UI를 계속 갱신하지 않도록 정리합니다.</summary>
    private void OnDisable()
    {
        stop();
    }

    /// <summary>직렬화된 알파와 시간 범위를 유효한 값으로 유지합니다.</summary>
    private void OnValidate()
    {
        maximumAlpha = Mathf.Max(minimumAlpha, maximumAlpha);
        fadeInDurationSeconds = Mathf.Max(0.01f, fadeInDurationSeconds);
        fadeOutDurationSeconds = Mathf.Max(0.01f, fadeOutDurationSeconds);
        intervalSeconds = Mathf.Max(0f, intervalSeconds);
    }

    /// <summary>현재 색조는 보존하고 알파만 변경하는 반복 Tween을 생성합니다.</summary>
    private void play()
    {
        if (targetImage == null)
        {
            Debug.LogError("FacilityPamphletShine: 반짝임 대상 Image가 필요합니다.", this);
            enabled = false;
            return;
        }

        stop();
        setAlpha(minimumAlpha);

        shineSequence = DOTween.Sequence()
            .Append(DOTween.To(getAlpha, setAlpha, maximumAlpha, fadeInDurationSeconds).SetEase(Ease.InOutSine))
            .Append(DOTween.To(getAlpha, setAlpha, minimumAlpha, fadeOutDurationSeconds).SetEase(Ease.InOutSine))
            .AppendInterval(intervalSeconds)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true)
            .SetTarget(this);
    }

    /// <summary>실행 중인 반복 Tween을 종료하고 최소 알파로 복원합니다.</summary>
    private void stop()
    {
        if (shineSequence != null)
        {
            shineSequence.Kill();
            shineSequence = null;
        }

        if (targetImage != null)
            setAlpha(minimumAlpha);
    }

    /// <summary>대상 Image의 RGB 값은 유지한 채 알파만 지정합니다.</summary>
    /// <param name="alpha">적용할 0~1 범위의 알파값입니다.</param>
    private void setAlpha(float alpha)
    {
        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
    }

    /// <summary>DOTween이 보간을 시작할 현재 Image 알파값을 반환합니다.</summary>
    /// <returns>대상 Image의 현재 알파값입니다.</returns>
    private float getAlpha()
    {
        return targetImage.color.a;
    }
}
