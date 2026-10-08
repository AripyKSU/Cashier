using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다른 모달 창 위에 깜빡이는 강조 상자를 여러 개 띄우는 오버레이.
/// 필요하면 화면 전체 입력을 막아, 안내 중에 뒤의 버튼이 눌리지 않게 합니다. 진행 판단은 하지 않습니다.
/// </summary>
public sealed class TutorialHighlightOverlay : MonoBehaviour
{
    /// <summary>강조할 영역. normalized는 대상 사각형 안의 부분 영역(0~1)입니다.</summary>
    public readonly struct Target
    {
        public readonly RectTransform Rect;
        public readonly Rect Normalized;

        public Target(RectTransform rect) : this(rect, new Rect(0f, 0f, 1f, 1f)) { }

        public Target(RectTransform rect, Rect normalized)
        {
            Rect = rect;
            Normalized = normalized;
        }
    }

    private const float EdgeThickness = 6f;
    private const float Padding = 6f;

    private readonly List<RectTransform> frames = new List<RectTransform>();
    private readonly List<Image> edges = new List<Image>();
    private Target[] targets = new Target[0];
    private Image blocker;

    /// <summary>루트 캔버스 아래에 오버레이를 만듭니다.</summary>
    /// <param name="parentCanvas">오버레이를 둘 캔버스입니다.</param>
    /// <param name="sortingOrder">모달 창보다 앞에 그릴 정렬 순서입니다.</param>
    /// <returns>만든 오버레이입니다.</returns>
    public static TutorialHighlightOverlay Create(Canvas parentCanvas, int sortingOrder)
    {
        var root = new GameObject("TutorialHighlightOverlay", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parentCanvas.transform, false);
        stretch(rect);
        var canvas = root.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        root.AddComponent<GraphicRaycaster>();
        var overlay = root.AddComponent<TutorialHighlightOverlay>();
        overlay.blocker = root.AddComponent<Image>();
        overlay.blocker.color = Color.clear;
        overlay.SetBlocking(false);
        return overlay;
    }

    /// <summary>화면 전체 클릭을 막을지 정합니다. 클릭으로 대사를 넘기는 것은 그대로 됩니다.</summary>
    /// <param name="blocking">막을지 여부입니다.</param>
    public void SetBlocking(bool blocking) => blocker.raycastTarget = blocking;

    /// <summary>주어진 영역들에 강조 상자를 띄웁니다. 빈 목록이면 모두 숨깁니다.</summary>
    /// <param name="newTargets">강조할 영역들입니다.</param>
    public void Show(params Target[] newTargets)
    {
        targets = newTargets ?? new Target[0];
        while (frames.Count < targets.Length) frames.Add(createFrame(frames.Count));
        for (int index = 0; index < frames.Count; index++)
            frames[index].gameObject.SetActive(index < targets.Length);
        LateUpdate();
    }

    /// <summary>강조 상자를 모두 숨기고 입력 차단도 풉니다.</summary>
    public void Hide()
    {
        Show();
        SetBlocking(false);
    }

    private void LateUpdate()
    {
        var parent = (RectTransform)transform;
        var corners = new Vector3[4];
        for (int index = 0; index < targets.Length; index++)
        {
            Target target = targets[index];
            RectTransform frame = frames[index];
            bool visible = target.Rect != null && target.Rect.gameObject.activeInHierarchy;
            frame.gameObject.SetActive(visible);
            if (!visible) continue;

            // 대상 사각형의 부분 영역을 오버레이 좌표로 옮긴다.
            target.Rect.GetWorldCorners(corners);
            Vector3 bottomLeft = parent.InverseTransformPoint(corners[0]);
            Vector3 topRight = parent.InverseTransformPoint(corners[2]);
            Vector2 size = topRight - bottomLeft;
            Rect area = target.Normalized;
            float left = bottomLeft.x + size.x * area.xMin - Padding;
            float right = bottomLeft.x + size.x * area.xMax + Padding;
            float bottom = bottomLeft.y + size.y * area.yMin - Padding;
            float top = bottomLeft.y + size.y * area.yMax + Padding;
            frame.localPosition = new Vector3((left + right) * .5f, (bottom + top) * .5f, 0f);
            frame.sizeDelta = new Vector2(right - left, top - bottom);
        }

        // 감독관 강조처럼 금빛으로 깜빡이되, 밝은 종이 위에서도 보이게 덜 투명하게 한다.
        float alpha = .7f + .3f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (var edge in edges) edge.color = new Color(1f, .78f, .25f, alpha);
    }

    /// <summary>네 변 이미지로 된 빈 상자 하나를 만듭니다.</summary>
    private RectTransform createFrame(int index)
    {
        var frame = new GameObject("Frame" + index, typeof(RectTransform));
        var rect = (RectTransform)frame.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        addEdge(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, EdgeThickness));
        addEdge(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, EdgeThickness));
        addEdge(rect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(EdgeThickness, 0f));
        addEdge(rect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(EdgeThickness, 0f));
        return rect;
    }

    private void addEdge(RectTransform frame, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)edge.transform;
        rect.SetParent(frame, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = (anchorMin + anchorMax) * .5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        var image = edge.GetComponent<Image>();
        image.raycastTarget = false;
        edges.Add(image);
    }

    private static void stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
