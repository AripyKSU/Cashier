using TMPro;
using UnityEngine;

/// <summary>
/// 래스터(Point) 도트 글꼴 TMP를 화면 배율과 상관없이 또렷하게 찍는다.
/// 글꼴 1px이 화면 정수 px이 되도록 글씨 크기를 캔버스 배율에 맞춘다.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class PixelFontText : MonoBehaviour
{
    private TextMeshProUGUI text;
    // 프리팹에 저장된 글씨 크기. 실제 크기는 이 값에 가장 가까운 정수 배율로 맞춘다.
    private float designFontSize;
    private float appliedScaleFactor = -1f;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        designFontSize = text.fontSize;
    }

    private void LateUpdate()
    {
        snapFontSize();
    }

    /// <summary>캔버스 배율이 바뀌면 글꼴 1px = 화면 정수 px이 되는 가장 가까운 글씨 크기로 바꾼다.</summary>
    private void snapFontSize()
    {
        Canvas canvas = text.canvas;
        if (canvas == null) return;
        float scaleFactor = canvas.rootCanvas.scaleFactor;
        if (Mathf.Approximately(scaleFactor, appliedScaleFactor)) return;

        appliedScaleFactor = scaleFactor;
        float fontPixelSize = text.font.faceInfo.pointSize;
        int screenPixelsPerFontPixel = Mathf.Max(1, Mathf.RoundToInt(designFontSize * scaleFactor / fontPixelSize));
        text.fontSize = screenPixelsPerFontPixel * fontPixelSize / scaleFactor;
    }
}
