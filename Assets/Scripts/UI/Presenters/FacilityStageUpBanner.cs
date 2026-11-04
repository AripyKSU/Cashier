using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>가게 단계가 오른 사실을 알리는 절제된 배너와 낮은 타격음. 단계 변경의 권위는 아니며 입력만 잠깐 막는다.</summary>
public sealed class FacilityStageUpBanner : MonoBehaviour, IPointerClickHandler
{
    private const float BandHeightPixels = 230f;
    private const float StripHeightPixels = 8f;
    private const float FirstHitVolume = 0.75f;
    private const float SecondHitDelaySeconds = 0.2f;
    private const float HoldUntilSeconds = 1.7f;
    private const float SkippableAfterSeconds = 0.4f;

    private static readonly Color DimColor = new Color(0.03f, 0.02f, 0.02f, 0.74f);
    private static readonly Color BandColor = new Color(0.27f, 0.08f, 0.07f, 0.97f);
    private static readonly Color StripColor = new Color(0.67f, 0.5f, 0.24f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color SubTextColor = new Color(0.8f, 0.68f, 0.48f, 1f);

    private Image dim;
    private RectTransform band;
    private TextMeshProUGUI title;
    private TextMeshProUGUI subtitle;
    private AudioSource soundSource;
    private AudioClip soundClip;
    private Sequence sequence;
    private Action finished;
    private float startedAtRealtime;

    /// <summary>패널 위를 덮는 숨겨진 배너를 만든다.</summary>
    /// <param name="parent">화면 전체를 덮는 설비 패널입니다.</param>
    /// <param name="font">글자 폰트입니다.</param>
    /// <param name="source">알림음을 낼 mixer 연결 source입니다.</param>
    /// <param name="clip">알림음 클립입니다.</param>
    /// <returns>만들어진 배너입니다.</returns>
    public static FacilityStageUpBanner Create(RectTransform parent, TMP_FontAsset font, AudioSource source, AudioClip clip)
    {
        var root = new GameObject("StageUpBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        var banner = root.AddComponent<FacilityStageUpBanner>();
        banner.soundSource = source;
        banner.soundClip = clip;
        banner.build(font);
        root.SetActive(false);
        return banner;
    }

    /// <summary>숨겨질 때 예약된 연출을 끝내고 완료를 알려 입력이 잠긴 채 남지 않게 한다.</summary>
    private void OnDisable()
    {
        sequence?.Kill();
        sequence = null;
        Action callback = finished;
        finished = null;
        callback?.Invoke();
    }

    /// <summary>배너를 재생한다. 재생 중이면 처음부터 다시 시작한다.</summary>
    /// <param name="stage">새 가게 단계입니다.</param>
    /// <param name="onFinished">배너가 걷힌 뒤 호출됩니다.</param>
    public void Play(uint stage, Action onFinished)
    {
        sequence?.Kill();
        finished = onFinished;
        startedAtRealtime = Time.unscaledTime;
        title.text = $"가게 레벨 {stage}단계로 업그레이드";
        subtitle.text = "새 설비를 들여올 수 있습니다";
        resetLook();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        // 축하보다 통보에 가깝게, 띠가 펼쳐지는 순간 낮은 타격을 "두둥" 두 번만 낸다.
        soundSource.PlayOneShot(soundClip, FirstHitVolume);
        sequence.InsertCallback(SecondHitDelaySeconds, () => soundSource.PlayOneShot(soundClip));
        sequence.Append(FacilityShopEffects.Fade(dim, DimColor.a, 0.14f));
        sequence.Join(band.DOScaleY(1f, 0.14f).SetEase(Ease.OutQuad));
        sequence.Join(FacilityShopEffects.Fade(title, 1f, 0.14f));
        sequence.Join(FacilityShopEffects.Fade(subtitle, 1f, 0.14f));
        sequence.AppendInterval(Mathf.Max(0f, HoldUntilSeconds - sequence.Duration()));
        appendClose();
    }

    /// <summary>일정 시간이 지난 뒤 클릭하면 대기 없이 걷어 낸다.</summary>
    /// <param name="eventData">입력 정보입니다.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (sequence == null || Time.unscaledTime - startedAtRealtime < SkippableAfterSeconds) return;
        if (sequence.Elapsed() >= HoldUntilSeconds) return;
        sequence.Kill();
        sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        appendClose();
    }

    /// <summary>배너를 접고 어둠을 걷은 뒤 완료를 알리는 구간을 이어 붙인다.</summary>
    private void appendClose()
    {
        sequence.Append(band.DOScaleY(0f, 0.16f).SetEase(Ease.InBack));
        sequence.Join(FacilityShopEffects.Fade(dim, 0f, 0.2f));
        sequence.OnComplete(() =>
        {
            sequence = null;
            // OnDisable이 완료 콜백을 처리한다. 비활성화 경로를 하나로 맞춰 중복 호출을 막는다.
            gameObject.SetActive(false);
        });
    }

    /// <summary>재생 시작 모습으로 되돌린다.</summary>
    private void resetLook()
    {
        setAlpha(dim, 0f);
        band.localScale = new Vector3(1f, 0f, 1f);
        band.anchoredPosition = Vector2.zero;
        setAlpha(title, 0f);
        setAlpha(subtitle, 0f);
    }

    /// <summary>배너 구성 요소를 코드로 만든다.</summary>
    /// <param name="font">글자 폰트입니다.</param>
    private void build(TMP_FontAsset font)
    {
        dim = GetComponent<Image>();
        dim.color = DimColor;
        dim.raycastTarget = true;
        fill((RectTransform)transform);

        Image bandImage = FacilityShopEffects.CreateImage("Band", transform, BandColor);
        band = bandImage.rectTransform;
        band.anchorMin = new Vector2(0f, 0.5f);
        band.anchorMax = new Vector2(1f, 0.5f);
        band.sizeDelta = new Vector2(0f, BandHeightPixels);

        addStrip(true);
        addStrip(false);

        title = createLabel("Title", font, 72f, TextColor, new Vector2(0f, 26f));
        subtitle = createLabel("Subtitle", font, 34f, SubTextColor, new Vector2(0f, -66f));
    }

    /// <summary>띠 위아래의 놋쇠색 줄을 만든다.</summary>
    /// <param name="isTop">위쪽 줄인지 여부입니다.</param>
    private void addStrip(bool isTop)
    {
        Image strip = FacilityShopEffects.CreateImage(isTop ? "TopStrip" : "BottomStrip", band, StripColor);
        RectTransform rect = strip.rectTransform;
        float anchorY = isTop ? 1f : 0f;
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, anchorY);
        rect.sizeDelta = new Vector2(0f, StripHeightPixels);
        rect.anchoredPosition = new Vector2(0f, isTop ? -8f : 8f);
    }

    /// <summary>띠 안의 가운데 정렬 글자를 만든다.</summary>
    private TextMeshProUGUI createLabel(string objectName, TMP_FontAsset font, float fontSize, Color color, Vector2 position)
    {
        var labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(band, false);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(1700f, fontSize * 1.4f);
        label.rectTransform.anchoredPosition = position;
        return label;
    }

    /// <summary>부모를 가득 채운다.</summary>
    private static void fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    /// <summary>RGB를 유지하고 투명도만 바꾼다.</summary>
    private static void setAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
