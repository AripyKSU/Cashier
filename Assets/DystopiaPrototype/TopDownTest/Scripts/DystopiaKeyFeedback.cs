using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
/// <summary>계산기 키를 가볍게 밝히고 누를 때 축소한 뒤 복원합니다.</summary>
public sealed class DystopiaKeyFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform Visual;
    private bool hovered, pressed;
    private void Update()
    {
        if (Visual == null) Visual = transform as RectTransform;
        float scale = pressed ? .91f : hovered ? 1.025f : 1;
        Visual.localScale = Vector3.Lerp(Visual.localScale, Vector3.one * scale, 1 - Mathf.Exp(-24 * Time.unscaledDeltaTime));
        var graphic = Visual.GetComponent<Image>();
        if (graphic != null) graphic.color = pressed ? new Color(.78f,.82f,.75f) : hovered ? new Color(1,.96f,.82f) : Color.white;
    }
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    private void OnDisable() { hovered = pressed = false; if (Visual != null) Visual.localScale = Vector3.one; }
}

