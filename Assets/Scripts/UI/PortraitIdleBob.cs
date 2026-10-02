using UnityEngine;
using DG.Tweening;

/// <summary>
/// 초상 RectTransform을 시작 위치 기준으로 아주 느리게 위아래로 흔들어 가만히 서 있지 않게 하는 유휴 연출 컴포넌트입니다.
/// 정수 픽셀로만 이동하고 누적 오차 없이 매 프레임 기준 위치에서 다시 계산합니다. Time.timeScale 영향을 받지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PortraitIdleBob : MonoBehaviour
{
    [Tooltip("기준 위치에서 위아래로 움직이는 최대 거리(픽셀). 정수로 반올림해 적용합니다.")]
    [SerializeField, Min(0f)] private float moveDistance = 2f;

    [Tooltip("한 번 올라갔다 내려오는 한 사이클의 길이(초).")]
    [SerializeField, Min(0.01f)] private float cycleDuration = 2.6f;

    /// <summary>OnEnable 시점에 반올림해 저장한 기준 anchoredPosition입니다.</summary>
    private Vector2 basePosition;

    /// <summary>활성 수명 동안 반복하는 정수 픽셀 유휴 연출입니다.</summary>
    private Tween idleTween;

    /// <summary>이 컴포넌트가 움직일 RectTransform입니다.</summary>
    private RectTransform rectTransform;

    /// <summary>현재 Inspector 위치를 기준으로 저장하고 연출 시각을 초기화합니다.</summary>
    private void OnEnable()
    {
        this.rectTransform = (RectTransform)this.transform;
        Vector2 current = this.rectTransform.anchoredPosition;
        this.basePosition = new Vector2(Mathf.Round(current.x), Mathf.Round(current.y));
        this.rectTransform.anchoredPosition = this.basePosition;
        float phase = 0f;
        this.idleTween = DOTween.To(() => phase, value =>
        {
            phase = value;
            float offset = Mathf.Round(Mathf.Sin(value * Mathf.PI * 2f) * this.moveDistance);
            this.rectTransform.anchoredPosition = this.basePosition + new Vector2(0f, offset);
        }, 1f, this.cycleDuration).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
    }

    /// <summary>연출을 멈추면 기준 위치로 되돌립니다.</summary>
    private void OnDisable()
    {
        this.idleTween?.Kill();
        this.idleTween = null;
        if (this.rectTransform != null)
            this.rectTransform.anchoredPosition = this.basePosition;
    }

}
