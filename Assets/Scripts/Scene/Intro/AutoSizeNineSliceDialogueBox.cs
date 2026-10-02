using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 9-Slice 대화창의 크기를 TMP Preferred Size로 직접 계산해 RectTransform에 적용한다.
/// ContentSizeFitter·LayoutGroup에 의존하지 않으므로 레이아웃 루프가 발생하지 않으며,
/// 새 대사마다 전체 문장 기준으로 한 번만 크기를 확정해 Typewriter 중 박스가 흔들리지 않는다.
/// 스타일은 강제하지 않는다. Sprite·색·정렬은 모두 Inspector가 소유한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AutoSizeNineSliceDialogueBox : MonoBehaviour
{
    [Header("References")]
    /// <summary>크기를 적용할 대화창 RectTransform. 보통 Background Image와 같은 오브젝트다.</summary>
    [SerializeField] private RectTransform boxRect;
    /// <summary>Image Type이 Sliced인 9-Slice 배경.</summary>
    [SerializeField] private Image background;
    /// <summary>대사 본문. boxRect의 자식이어야 한다.</summary>
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Width limits (px)")]
    /// <summary>짧은 문장에서도 유지할 최소 가로 길이.</summary>
    [SerializeField, Min(1f)] private float minWidth = 320f;
    /// <summary>이 길이에 도달하면 더 늘리지 않고 줄바꿈으로 전환한다.</summary>
    [SerializeField, Min(1f)] private float maxWidth = 1050f;

    [Header("Padding (px)")]
    /// <summary>텍스트 영역 좌우 여백. 박스 가로 = 텍스트 가로 + 이 값 * 2.</summary>
    [SerializeField, Min(0f)] private float horizontalPadding = 28f;
    /// <summary>텍스트 영역 상하 여백. 박스 세로 = 텍스트 세로 + 이 값 * 2.</summary>
    [SerializeField, Min(0f)] private float verticalPadding = 18f;

    [Header("Options")]
    /// <summary>본문 RectTransform을 여백만큼 stretch로 맞춘다. 끄면 배경 크기만 바꾼다.</summary>
    [SerializeField] private bool driveTextRect = true;
    /// <summary>한 줄짜리 대사에서도 유지할 최소 세로 길이. 0이면 텍스트 높이를 따른다.</summary>
    [SerializeField, Min(0f)] private float minHeight;

    private bool hasWarnedAboutSlicing;

    /// <summary>마지막으로 확정한 대화창 크기.</summary>
    public Vector2 LastBoxSize { get; private set; }

    /// <summary>직렬화된 필수 참조를 확인한다.</summary>
    /// <returns>대화창 크기를 계산할 수 있으면 true.</returns>
    public bool ValidateReferences()
    {
        if (boxRect == null || background == null || bodyText == null)
        {
            Debug.LogError("[AutoSizeNineSliceDialogueBox] boxRect, background, bodyText 참조가 모두 필요합니다.", this);
            return false;
        }

        if (maxWidth < minWidth)
        {
            Debug.LogError("[AutoSizeNineSliceDialogueBox] maxWidth는 minWidth보다 작을 수 없습니다.", this);
            return false;
        }

        // 스타일을 코드로 바꾸지 않고, 9-Slice가 아닌 설정만 한 번 경고한다.
        if (!hasWarnedAboutSlicing && background.type != Image.Type.Sliced)
        {
            hasWarnedAboutSlicing = true;
            Debug.LogWarning("[AutoSizeNineSliceDialogueBox] 배경 Image Type을 Sliced로 설정해야 9-Slice로 늘어납니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 전체 문장 기준으로 대화창 크기를 확정한다.
    /// Typewriter를 시작하기 전에 한 번만 호출해야 글자마다 박스가 덜컹거리지 않는다.
    /// </summary>
    /// <param name="fullText">이번 대사의 완성된 전체 문자열.</param>
    /// <returns>적용한 대화창 크기. 참조가 없으면 Vector2.zero.</returns>
    public Vector2 Apply(string fullText)
    {
        if (!ValidateReferences()) return Vector2.zero;

        string measured = fullText ?? string.Empty;
        float innerMaxWidth = Mathf.Max(1f, maxWidth - horizontalPadding * 2f);

        // 1) 줄바꿈 없이 필요한 가로 길이를 먼저 잰다.
        bodyText.textWrappingMode = TextWrappingModes.NoWrap;
        Vector2 unwrapped = bodyText.GetPreferredValues(measured, 0f, 0f);

        float textWidth;
        float textHeight;
        if (unwrapped.x <= innerMaxWidth)
        {
            // maxWidth 안에 들어가면 한 줄로 두고 가로만 늘린다.
            textWidth = unwrapped.x;
            textHeight = unwrapped.y;
        }
        else
        {
            // 2) maxWidth를 넘기면 그 폭에서 Word Wrap 후 필요한 세로를 다시 잰다.
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            textWidth = innerMaxWidth;
            textHeight = bodyText.GetPreferredValues(measured, innerMaxWidth, 0f).y;
        }

        float boxWidth = Mathf.Clamp(textWidth + horizontalPadding * 2f, minWidth, maxWidth);
        float boxHeight = Mathf.Max(textHeight + verticalPadding * 2f, minHeight);

        boxRect.sizeDelta = new Vector2(boxWidth, boxHeight);
        if (driveTextRect) applyTextRect();

        LastBoxSize = new Vector2(boxWidth, boxHeight);
        return LastBoxSize;
    }

    /// <summary>본문 RectTransform을 배경 안쪽에 여백만큼 맞춘다.</summary>
    private void applyTextRect()
    {
        RectTransform textRect = bodyText.rectTransform;
        if (textRect == boxRect) return;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        textRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
    }
}
