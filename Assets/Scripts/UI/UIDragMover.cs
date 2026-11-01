using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 끌면 붙어 있는 RectTransform을 부모 안에서 옮깁니다. 부모 밖으로는 나가지 않습니다.
/// 자식 버튼에서 시작한 끌기도 여기로 올라오며, 끌기가 시작되면 그 버튼의 클릭은 취소됩니다.
/// </summary>
public sealed class UIDragMover : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform target;
    private Vector2 grabOffset;
    private bool isDragging;

    /// <summary>끌기를 허용할지 묻습니다. 비어 있으면 항상 허용합니다.</summary>
    public Func<bool> CanDrag;

    /// <summary>끌어서 위치가 바뀔 때마다 알립니다.</summary>
    public event Action Moved;

    private void Awake()
    {
        this.target = (RectTransform)this.transform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        this.isDragging = false;
        if (this.CanDrag != null && !this.CanDrag() || !this.tryGetPointer(eventData, out Vector2 start)) return;
        this.isDragging = true;
        this.grabOffset = this.target.anchoredPosition - start;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!this.isDragging || !this.tryGetPointer(eventData, out Vector2 pointer)) return;
        this.target.anchoredPosition = pointer + this.grabOffset;
        this.ClampToParent();
        this.Moved?.Invoke();
    }

    /// <summary>부모 영역을 벗어난 만큼 안쪽으로 되돌립니다. 부모보다 크면 왼쪽·위를 맞춥니다.</summary>
    public void ClampToParent()
    {
        if (!(this.target.parent is RectTransform parent)) return;
        var corners = new Vector3[4];
        this.target.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        Rect bounds = parent.rect;
        Vector2 shift = Vector2.zero;
        if (max.x > bounds.xMax) shift.x = bounds.xMax - max.x;
        if (min.x + shift.x < bounds.xMin) shift.x = bounds.xMin - min.x;
        if (min.y < bounds.yMin) shift.y = bounds.yMin - min.y;
        if (max.y + shift.y > bounds.yMax) shift.y = bounds.yMax - max.y;
        this.target.anchoredPosition += shift;
    }

    private bool tryGetPointer(PointerEventData eventData, out Vector2 pointer)
    {
        pointer = default;
        return this.target.parent is RectTransform parent
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out pointer);
    }
}
