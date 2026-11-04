using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설비 화면에서 쓰는 픽셀 조각·떠오르는 글자·화면 흔들림 연출. 상태를 소유하지 않고 표시용 Tween만 만든다.</summary>
public static class FacilityShopEffects
{
    /// <summary>중심에서 사방으로 튀었다가 사라지는 네모 조각을 만든다. 번짐 없이 또렷하게 보이도록 회전하지 않는다.</summary>
    /// <param name="parent">조각을 넣을 부모입니다.</param>
    /// <param name="center">부모 기준 중심 위치입니다.</param>
    /// <param name="count">조각 수입니다.</param>
    /// <param name="palette">조각마다 무작위로 고르는 색입니다.</param>
    /// <param name="radiusPixels">조각이 날아가는 최대 거리입니다.</param>
    /// <param name="sizePixels">조각 한 변의 최대 길이입니다.</param>
    public static void Burst(RectTransform parent, Vector2 center, int count, Color[] palette, float radiusPixels, float sizePixels)
    {
        for (int index = 0; index < count; index++)
        {
            Image spark = CreateImage("Spark", parent, palette[Random.Range(0, palette.Length)]);
            float size = Mathf.Round(Random.Range(sizePixels * 0.5f, sizePixels));
            spark.rectTransform.sizeDelta = new Vector2(size, size);
            spark.rectTransform.anchoredPosition = center;

            Vector2 direction = Random.insideUnitCircle.normalized;
            Vector2 target = center + direction * Random.Range(radiusPixels * 0.45f, radiusPixels);
            float durationSeconds = Random.Range(0.35f, 0.7f);
            Color baseColor = spark.color;
            GameObject sparkObject = spark.gameObject;
            DOTween.To(() => 0f, progress =>
                {
                    // 위로 튀고 아래로 살짝 떨어지는 궤적이다. 물리 없이 한 값으로 위치와 투명도를 함께 푼다.
                    float eased = 1f - (1f - progress) * (1f - progress);
                    Vector2 position = Vector2.Lerp(center, target, eased);
                    position.y -= progress * progress * radiusPixels * 0.35f;
                    spark.rectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
                    Color color = baseColor;
                    color.a = baseColor.a * (1f - progress * progress);
                    spark.color = color;
                }, 1f, durationSeconds)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetLink(sparkObject)
                .OnComplete(() => Object.Destroy(sparkObject));
        }
    }

    /// <summary>색 판 위의 글자를 위로 띄우며 지운다. 결제된 금액처럼 잠깐 보여 줄 값에 쓴다.</summary>
    /// <param name="parent">글자를 넣을 부모입니다.</param>
    /// <param name="rightEdge">부모 기준 판의 오른쪽 끝 시작 위치입니다.</param>
    /// <param name="text">표시할 문자열입니다.</param>
    /// <param name="font">표시 폰트입니다.</param>
    /// <param name="fontSize">글자 크기입니다.</param>
    /// <param name="color">글자색입니다.</param>
    /// <param name="plateColor">그림 위에서도 읽히도록 글자 뒤에 까는 판 색입니다.</param>
    public static void FloatText(RectTransform parent, Vector2 rightEdge, string text, TMP_FontAsset font, float fontSize, Color color, Color plateColor)
    {
        Image plate = CreateImage("FloatingText", parent, plateColor);
        RectTransform plateRect = plate.rectTransform;
        plateRect.pivot = new Vector2(1f, 0.5f);
        plateRect.anchoredPosition = rightEdge;
        var group = plate.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(plateRect, false);
        var label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = text;
        Vector2 textSize = label.GetPreferredValues(text);
        plateRect.sizeDelta = new Vector2(Mathf.Ceil(textSize.x) + 14f, Mathf.Ceil(textSize.y) + 6f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;

        GameObject plateObject = plate.gameObject;
        DOTween.To(() => 0f, progress =>
            {
                plateRect.anchoredPosition = rightEdge + new Vector2(0f, Mathf.Round(progress * 44f));
                group.alpha = progress < 0.65f ? 1f : 1f - (progress - 0.65f) / 0.35f;
            }, 1f, 1.1f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(plateObject)
            .OnComplete(() => Object.Destroy(plateObject));
    }

    /// <summary>대상을 잠깐 흔들고 원래 위치로 돌려놓는다.</summary>
    /// <param name="target">흔들 대상입니다.</param>
    /// <param name="strengthPixels">처음 흔들림 폭입니다.</param>
    /// <param name="durationSeconds">흔드는 시간입니다.</param>
    /// <returns>완료하면 위치를 복원하는 Tween입니다.</returns>
    public static Tween Shake(RectTransform target, float strengthPixels, float durationSeconds)
    {
        Vector2 rest = target.anchoredPosition;
        return DOTween.To(() => 0f, progress =>
            {
                Vector2 offset = Random.insideUnitCircle * (strengthPixels * (1f - progress));
                target.anchoredPosition = rest + new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
            }, 1f, durationSeconds)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(target.gameObject)
            .OnKill(() => { if (target != null) target.anchoredPosition = rest; });
    }

    /// <summary>입력을 받지 않는 단색 Image를 만든다.</summary>
    /// <param name="name">오브젝트 이름입니다.</param>
    /// <param name="parent">부모입니다.</param>
    /// <param name="color">색입니다.</param>
    /// <returns>중앙 기준으로 놓인 Image입니다.</returns>
    public static Image CreateImage(string name, Transform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Image의 RGB를 유지하고 투명도만 바꾸는 Tween을 만든다.</summary>
    /// <param name="graphic">대상입니다.</param>
    /// <param name="alpha">목표 투명도입니다.</param>
    /// <param name="durationSeconds">시간입니다.</param>
    /// <returns>시간 정지와 무관하게 도는 Tween입니다.</returns>
    public static Tween Fade(Graphic graphic, float alpha, float durationSeconds)
    {
        return DOTween.To(() => graphic.color.a, value =>
            {
                Color color = graphic.color;
                color.a = value;
                graphic.color = color;
            }, alpha, durationSeconds)
            .SetUpdate(true)
            .SetLink(graphic.gameObject);
    }
}
