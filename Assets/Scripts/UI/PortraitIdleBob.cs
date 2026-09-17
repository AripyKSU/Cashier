using UnityEngine;

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

    /// <summary>연출 시작 시각(Time.unscaledTime)입니다.</summary>
    private float startTime;

    /// <summary>이 컴포넌트가 움직일 RectTransform입니다.</summary>
    private RectTransform rectTransform;

    /// <summary>현재 Inspector 위치를 기준으로 저장하고 연출 시각을 초기화합니다.</summary>
    private void OnEnable()
    {
        this.rectTransform = (RectTransform)this.transform;
        Vector2 current = this.rectTransform.anchoredPosition;
        this.basePosition = new Vector2(Mathf.Round(current.x), Mathf.Round(current.y));
        this.rectTransform.anchoredPosition = this.basePosition;
        this.startTime = Time.unscaledTime;
    }

    /// <summary>연출을 멈추면 기준 위치로 되돌립니다.</summary>
    private void OnDisable()
    {
        if (this.rectTransform != null)
            this.rectTransform.anchoredPosition = this.basePosition;
    }

    /// <summary>경과 시간으로부터 정수 픽셀 오프셋을 계산해 적용합니다.</summary>
    private void Update()
    {
        float phase = Mathf.Repeat(Time.unscaledTime - this.startTime, this.cycleDuration) / this.cycleDuration;
        float offset = Mathf.Round(Mathf.Sin(phase * Mathf.PI * 2f) * this.moveDistance);
        this.rectTransform.anchoredPosition = this.basePosition + new Vector2(0f, offset);
    }
}
