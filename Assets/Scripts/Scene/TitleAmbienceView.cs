using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 배경 그림 위의 잔잔한 움직임: 감시탑 서치라이트, 탑 불빛, 도시 창문 몇 개의 깜빡임, 머그컵 김.
/// 모래시계를 포함한 그림 자체는 움직이지 않는다. 서치라이트는 배경과 전경 컷아웃 사이에 그려
/// 건물·창틀 뒤로 지나가게 한다. 위치는 배경 그림 기준 정규화 좌표(0~1, 왼쪽 아래 기준)라
/// 배경 RectTransform 크기가 바뀌어도 같은 자리에 맞는다. 표현 전용이며 게임 진행과 무관하다.
/// </summary>
public sealed class TitleAmbienceView : MonoBehaviour
{
    // 연출 수치는 800px 너비 시안에서 맞춘 값이라 배경 너비 대비 비율로 환산한다.
    private const float MockupWidthPixels = 800f;

    [Tooltip("서치라이트 빛줄기. 왼쪽 가운데가 광원이고 +x 방향으로 퍼진다")]
    [SerializeField] private Sprite beamSprite;

    [Tooltip("하늘만 투명한 배경 전경 컷아웃. 빛줄기를 건물 뒤로 보낸다")]
    [SerializeField] private Sprite foregroundSprite;

    [Tooltip("탑 불빛 번짐")]
    [SerializeField] private Sprite glowSprite;

    [Tooltip("머그컵 김 한 덩이")]
    [SerializeField] private Sprite steamSprite;

    [Tooltip("감시탑 불빛 위치 (0~1, 왼쪽 아래 기준)")]
    [SerializeField] private Vector2 lampPosition = new Vector2(.8744f, .7322f);

    [Tooltip("머그컵 김이 시작되는 위치 (0~1, 왼쪽 아래 기준)")]
    [SerializeField] private Vector2 mugPosition = new Vector2(.299f, .3039f);

    [Tooltip("천천히 꺼졌다 켜지는 도시 창문 위치 (0~1, 왼쪽 아래 기준)")]
    [SerializeField] private Vector2[] windowLightPositions =
    {
        new Vector2(.7805f, .627f), new Vector2(.323f, .4995f), new Vector2(.7859f, .4931f),
        new Vector2(.2925f, .4803f), new Vector2(.3535f, .4325f), new Vector2(.2799f, .4293f),
        new Vector2(.7679f, .4293f), new Vector2(.7141f, .4198f), new Vector2(.689f, .4134f)
    };

    [Tooltip("서치라이트 중심 각도(도, 화면 기준 시계 반대 방향, 180이 왼쪽)")]
    [SerializeField] private float beamCenterDegrees = 160f;

    [Tooltip("서치라이트가 좌우로 흔들리는 폭(도)")]
    [SerializeField] private float beamSwingDegrees = 18f;

    [Tooltip("서치라이트 한 번 왕복 시간(초)")]
    [SerializeField, Min(1f)] private float beamPeriodSeconds = 9f;

    private RectTransform area;
    private RectTransform beam;
    private Image glow;
    private Image[] windowLights;
    private float[] windowPhases;
    private Image[] steamPuffs;
    private float elapsedSeconds;

    /// <summary>배경 아래에 빛줄기 → 전경 → 불빛·김 순서로 자식 Image를 만든다.</summary>
    private void Awake()
    {
        if (beamSprite == null || foregroundSprite == null || glowSprite == null || steamSprite == null)
        {
            Debug.LogError("[TitleAmbienceView] 서치라이트·전경·불빛·김 Sprite가 모두 연결돼야 합니다.");
            enabled = false;
            return;
        }

        area = (RectTransform)transform;
        beam = createImage("SearchlightBeam", beamSprite, new Vector2(0f, .5f)).rectTransform;
        Image foreground = createImage("Foreground", foregroundSprite, new Vector2(.5f, .5f));
        RectTransform foregroundRect = foreground.rectTransform;
        foregroundRect.anchorMin = Vector2.zero;
        foregroundRect.anchorMax = Vector2.one;
        foregroundRect.sizeDelta = Vector2.zero;
        foregroundRect.anchoredPosition = Vector2.zero;
        glow = createImage("LampGlow", glowSprite, new Vector2(.5f, .5f));

        windowLights = new Image[windowLightPositions.Length];
        windowPhases = new float[windowLightPositions.Length];
        for (int index = 0; index < windowLights.Length; index++)
        {
            windowLights[index] = createImage("WindowLight" + index, null, new Vector2(.5f, .5f));
            // 창문마다 깜빡이는 때가 겹치지 않게 위상을 고르게 흩어 둔다.
            windowPhases[index] = index * 2.399f;
        }

        steamPuffs = new Image[3];
        for (int index = 0; index < steamPuffs.Length; index++)
            steamPuffs[index] = createImage("Steam" + index, steamSprite, new Vector2(.5f, .5f));
    }

    /// <summary>경과 시간으로 각 연출을 갱신한다. 일시정지와 무관하게 실제 시간으로 흐른다.</summary>
    private void Update()
    {
        elapsedSeconds += Time.unscaledDeltaTime;
        float t = elapsedSeconds;
        Rect rect = area.rect;
        float unit = rect.width / MockupWidthPixels;

        // 서치라이트: 탑에서 하늘 쪽으로 천천히 좌우로 훑는다.
        float length = rect.width * 1.125f;
        beam.sizeDelta = new Vector2(length, length * beamSprite.rect.height / beamSprite.rect.width);
        beam.anchoredPosition = toLocal(lampPosition, rect);
        float angle = beamCenterDegrees + beamSwingDegrees * Mathf.Sin(t * 2f * Mathf.PI / beamPeriodSeconds);
        beam.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 탑 불빛: 숨 쉬듯 밝기만 바뀐다.
        glow.rectTransform.anchoredPosition = toLocal(lampPosition, rect);
        glow.rectTransform.sizeDelta = new Vector2(28f * unit, 20f * unit);
        glow.color = new Color(1f, 1f, 1f, .35f + .25f * Mathf.Sin(t * 3f));

        // 도시 창문: 대부분 그대로이고 가끔 하나씩 꺼지거나 밝아진다.
        for (int index = 0; index < windowLights.Length; index++)
        {
            float wave = .5f + .5f * Mathf.Sin(t * .7f + windowPhases[index]);
            Image light = windowLights[index];
            light.rectTransform.anchoredPosition = toLocal(windowLightPositions[index], rect);
            light.rectTransform.sizeDelta = new Vector2(3f * unit, 3f * unit);
            if (wave < .06f) light.color = new Color(.12f, .09f, .12f, .67f);
            else if (wave > .97f) light.color = new Color(1f, .8f, .51f, .5f);
            else light.color = Color.clear;
        }

        // 머그컵 김: 세 덩이가 번갈아 올라가며 퍼지고 옅어진다.
        Vector2 mug = toLocal(mugPosition, rect);
        for (int index = 0; index < steamPuffs.Length; index++)
        {
            float rise = Mathf.Repeat(t * .55f + index / 3f, 1f);
            float radius = (3f + rise * 6f) * unit;
            Image puff = steamPuffs[index];
            puff.rectTransform.anchoredPosition = mug + new Vector2(Mathf.Sin(rise * 6f + index) * 4f * unit, rise * 40f * unit);
            puff.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            puff.color = new Color(1f, 1f, 1f, .45f * (1f - rise));
        }
    }

    /// <summary>입력을 막지 않는 자식 Image를 왼쪽 아래 기준으로 만든다.</summary>
    /// <param name="objectName">Hierarchy 이름.</param>
    /// <param name="sprite">표시할 Sprite. null이면 흰 사각형.</param>
    /// <param name="pivot">회전·크기 기준점.</param>
    /// <returns>생성한 Image.</returns>
    private Image createImage(string objectName, Sprite sprite, Vector2 pivot)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(area, false);
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = pivot;
        return image;
    }

    /// <summary>정규화 좌표를 배경 왼쪽 아래 기준 로컬 좌표로 바꾼다.</summary>
    /// <param name="normalized">0~1 좌표.</param>
    /// <param name="rect">배경 RectTransform의 rect.</param>
    /// <returns>anchoredPosition 값.</returns>
    private static Vector2 toLocal(Vector2 normalized, Rect rect) =>
        new Vector2(normalized.x * rect.width, normalized.y * rect.height);
}
