using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다른 모달 창 위에 깜빡이는 강조 테두리를 여러 개 띄우는 오버레이.
/// 테두리는 네 꼭짓점을 잇는 선이라 비스듬히 놓인 종이 같은 사다리꼴 모양에도 맞출 수 있습니다.
/// 필요하면 화면 전체 입력을 막아, 안내 중에 뒤의 버튼이 눌리지 않게 합니다. 진행 판단은 하지 않습니다.
/// </summary>
public sealed class TutorialHighlightOverlay : MonoBehaviour
{
    /// <summary>
    /// 강조할 영역. Corners는 대상 사각형 안의 네 꼭짓점(0~1, 왼쪽 아래 원점)을 둘레 순서대로 적은 것입니다.
    /// </summary>
    public readonly struct Target
    {
        public readonly RectTransform Rect;
        public readonly Vector2[] Corners;

        public Target(RectTransform rect) : this(rect, new Rect(0f, 0f, 1f, 1f)) { }

        public Target(RectTransform rect, Rect normalized) : this(rect, new[]
        {
            new Vector2(normalized.xMin, normalized.yMin), new Vector2(normalized.xMin, normalized.yMax),
            new Vector2(normalized.xMax, normalized.yMax), new Vector2(normalized.xMax, normalized.yMin)
        }) { }

        public Target(RectTransform rect, Vector2[] corners)
        {
            Rect = rect;
            Corners = corners;
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

    /// <summary>주어진 영역들에 강조 테두리를 띄웁니다. 빈 목록이면 모두 숨깁니다.</summary>
    /// <param name="newTargets">강조할 영역들입니다.</param>
    public void Show(params Target[] newTargets)
    {
        targets = newTargets ?? new Target[0];
        while (frames.Count < targets.Length) frames.Add(createFrame(frames.Count));
        for (int index = 0; index < frames.Count; index++)
            frames[index].gameObject.SetActive(index < targets.Length);
        LateUpdate();
    }

    /// <summary>강조 테두리를 모두 숨기고 입력 차단도 풉니다.</summary>
    public void Hide()
    {
        Show();
        SetBlocking(false);
    }

    private void LateUpdate()
    {
        var parent = (RectTransform)transform;
        var corners = new Vector3[4];
        var points = new Vector2[4];
        for (int index = 0; index < targets.Length; index++)
        {
            Target target = targets[index];
            RectTransform frame = frames[index];
            bool visible = target.Rect != null && target.Rect.gameObject.activeInHierarchy && target.Corners?.Length == 4;
            frame.gameObject.SetActive(visible);
            if (!visible) continue;

            // 대상 사각형 안의 꼭짓점을 오버레이 좌표로 옮긴 뒤 바깥으로 조금 넓힌다.
            target.Rect.GetWorldCorners(corners);
            Vector2 bottomLeft = parent.InverseTransformPoint(corners[0]);
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);
            Vector2 size = topRight - bottomLeft;
            for (int corner = 0; corner < 4; corner++)
                points[corner] = bottomLeft + Vector2.Scale(size, target.Corners[corner]);
            expand(points, Padding);
            for (int edge = 0; edge < 4; edge++)
                placeEdge((RectTransform)frame.GetChild(edge), points[edge], points[(edge + 1) % 4]);
        }

        // 감독관 강조처럼 금빛으로 깜빡이되, 밝은 종이 위에서도 보이게 덜 투명하게 한다.
        float alpha = .7f + .3f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (var edge in edges) edge.color = new Color(1f, .78f, .25f, alpha);
    }

    /// <summary>두 점을 잇는 두께 있는 선 하나를 놓습니다. 모서리가 비지 않게 양 끝을 두께만큼 늘립니다.</summary>
    private static void placeEdge(RectTransform edge, Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        edge.localPosition = (from + to) * .5f;
        edge.sizeDelta = new Vector2(delta.magnitude + EdgeThickness, EdgeThickness);
        edge.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    /// <summary>볼록한 사각형의 각 변을 바깥쪽으로 distance만큼 밀어 새 꼭짓점을 구합니다.</summary>
    private static void expand(Vector2[] points, float distance)
    {
        float area = 0f;
        for (int index = 0; index < 4; index++)
        {
            Vector2 a = points[index], b = points[(index + 1) % 4];
            area += a.x * b.y - b.x * a.y;
        }

        // 둘레 방향에 따라 바깥쪽 법선 방향이 바뀐다.
        float side = area > 0f ? 1f : -1f;
        var moved = new Vector2[4];
        for (int index = 0; index < 4; index++)
        {
            Vector2 previous = points[(index + 3) % 4], current = points[index], next = points[(index + 1) % 4];
            Vector2 inDirection = (current - previous).normalized, outDirection = (next - current).normalized;
            Vector2 inNormal = new Vector2(inDirection.y, -inDirection.x) * side;
            Vector2 outNormal = new Vector2(outDirection.y, -outDirection.x) * side;
            // 두 변을 각각 밀어 낸 직선의 교점.
            Vector2 a = previous + inNormal * distance, b = current + outNormal * distance;
            float cross = inDirection.x * outDirection.y - inDirection.y * outDirection.x;
            if (Mathf.Abs(cross) < 1e-4f) { moved[index] = current + outNormal * distance; continue; }
            Vector2 diff = b - a;
            float t = (diff.x * outDirection.y - diff.y * outDirection.x) / cross;
            moved[index] = a + inDirection * t;
        }

        for (int index = 0; index < 4; index++) points[index] = moved[index];
    }

    /// <summary>네 변 선으로 된 빈 테두리 하나를 만듭니다.</summary>
    private RectTransform createFrame(int index)
    {
        var frame = new GameObject("Frame" + index, typeof(RectTransform));
        var rect = (RectTransform)frame.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        for (int edge = 0; edge < 4; edge++) addEdge(rect);
        return rect;
    }

    private void addEdge(RectTransform frame)
    {
        var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)edge.transform;
        rect.SetParent(frame, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
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
