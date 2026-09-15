using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
/// <summary>계산기 키를 가볍게 밝히고 누를 때 축소한 뒤 복원합니다.</summary>
public sealed class DystopiaKeyFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    /// <summary>확대와 색상 반응을 적용할 실제 시각 요소입니다.</summary>
    public RectTransform Visual;
    /// <summary>호버·누름 배율과 반응 속도입니다.</summary>
    public float HoverScale = 1.025f, PressedScale = .91f, Response = 24;
    private bool hovered, pressed;
    private RectTransform trackedVisual;
    private Vector3 baseScale;
    private Image graphic;
    private Color baseColor;

    /// <summary>Inspector에 연결된 시각 요소의 편집 배율과 색상을 기준값으로 보존합니다.</summary>
    private void Awake() { CaptureVisual(); }

    /// <summary>호버와 누름 상태를 편집된 기준 배율에 상대적으로 보간합니다.</summary>
    private void Update()
    {
        if (Visual == null) Visual = transform as RectTransform;
        if (trackedVisual != Visual) CaptureVisual();
        float scale = pressed ? PressedScale : hovered ? HoverScale : 1;
        Visual.localScale = Vector3.Lerp(Visual.localScale, baseScale * scale, 1 - Mathf.Exp(-Response * Time.unscaledDeltaTime));
        if (graphic != null) graphic.color = pressed ? baseColor * new Color(.78f,.82f,.75f) : hovered ? baseColor * new Color(1,.96f,.82f) : baseColor;
    }

    /// <summary>현재 시각 요소의 배율과 색상을 런타임 반응의 기준으로 저장합니다.</summary>
    private void CaptureVisual()
    {
        if (Visual == null) Visual = transform as RectTransform;
        trackedVisual = Visual;
        if (Visual == null) return;
        baseScale = Visual.localScale;
        graphic = Visual.GetComponent<Image>();
        baseColor = graphic != null ? graphic.color : Color.white;
    }

    /// <summary>포인터가 들어오면 호버 확대를 시작합니다.</summary>
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    /// <summary>포인터가 나가면 모든 포인터 반응을 해제합니다.</summary>
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    /// <summary>왼쪽 버튼을 누르는 동안 눌림 배율을 적용합니다.</summary>
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    /// <summary>포인터 버튼을 놓으면 호버 배율로 돌아갑니다.</summary>
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    /// <summary>비활성화할 때 편집된 배율과 색상으로 정확히 복원합니다.</summary>
    private void OnDisable()
    {
        hovered = pressed = false;
        if (Visual != null) Visual.localScale = baseScale;
        if (graphic != null) graphic.color = baseColor;
    }
}

