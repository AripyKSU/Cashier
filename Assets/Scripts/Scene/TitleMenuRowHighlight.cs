using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>타이틀 메뉴 항목에 마우스가 올라가거나 선택되면 그 줄 전체의 밝은 띠를 켠다. 잠긴 항목은 켜지 않는다.</summary>
public class TitleMenuRowHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("상태를 따라갈 메뉴 항목")]
    [SerializeField] private Selectable item;

    [Tooltip("항목 글자 뒤에 깔리는 줄 이미지")]
    [SerializeField] private Graphic rowGraphic;

    private bool isPointerInside;
    private bool isSelected;

    /// <summary>첫 입력 전에는 띠를 끈 상태로 시작한다.</summary>
    private void Awake() => refresh();

    /// <summary>비활성화 중 놓친 포인터 이탈로 띠가 남지 않게 상태를 비운다.</summary>
    private void OnDisable()
    {
        isPointerInside = false;
        isSelected = false;
        refresh();
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        refresh();
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        refresh();
    }

    /// <inheritdoc />
    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        refresh();
    }

    /// <inheritdoc />
    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        refresh();
    }

    /// <summary>현재 상태에 맞춰 띠를 켜거나 끈다.</summary>
    private void refresh() => rowGraphic.enabled = (isPointerInside || isSelected) && item.IsInteractable();
}
