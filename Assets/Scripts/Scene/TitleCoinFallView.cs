using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 모래시계 목에서 동전이 한 닢씩 아래 더미로 떨어지는 반복 연출.
/// 배경 그림 위치를 정규화 좌표로 받아, 배경 RectTransform 크기가 바뀌어도 같은 자리에 맞춥니다.
/// 하루하루 남은 날이 줄어드는 "하루살이" 분위기를 위한 표현 전용이며 게임 진행과 무관합니다.
/// </summary>
public sealed class TitleCoinFallView : MonoBehaviour
{
    [Tooltip("떨어질 동전 Sprite")]
    [SerializeField] private Sprite coinSprite;

    [Tooltip("배경 그림 안 모래시계 목 중심 (0~1, 왼쪽 아래 기준)")]
    [SerializeField] private Vector2 neckPosition = new Vector2(.498f, .424f);

    [Tooltip("배경 그림 안 아래 동전 더미 윗면 중심 (0~1, 왼쪽 아래 기준)")]
    [SerializeField] private Vector2 pileTopPosition = new Vector2(.498f, .269f);

    [Tooltip("배경 너비 대비 동전 지름 비율")]
    [SerializeField, Min(.001f)] private float coinWidthRatio = .0257f;

    [Tooltip("동전이 떨어지기 시작하는 간격(초)")]
    [SerializeField, Min(.1f)] private float spawnIntervalSeconds = 1.1f;

    [Tooltip("목에서 더미까지 떨어지는 시간(초)")]
    [SerializeField, Min(.1f)] private float fallSeconds = .9f;

    [Tooltip("동시에 화면에 있을 수 있는 최대 동전 수")]
    [SerializeField, Range(1, 8)] private int poolSize = 3;

    private RectTransform area;
    private Image[] coins;
    private float[] coinAges;
    private float spawnTimer;
    private int nextCoin;

    private void Awake()
    {
        area = (RectTransform)transform;
        coins = new Image[poolSize];
        coinAges = new float[poolSize];
        for (int index = 0; index < poolSize; index++)
        {
            var coinObject = new GameObject("FallingCoin" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coinObject.transform.SetParent(area, false);
            coins[index] = coinObject.GetComponent<Image>();
            coins[index].sprite = coinSprite;
            coins[index].raycastTarget = false;
            coins[index].preserveAspect = true;
            coinAges[index] = -1f;
            coinObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (coinSprite == null) return;
        spawnTimer += Time.unscaledDeltaTime;
        if (spawnTimer >= spawnIntervalSeconds)
        {
            spawnTimer = 0f;
            coinAges[nextCoin] = 0f;
            coins[nextCoin].gameObject.SetActive(true);
            nextCoin = (nextCoin + 1) % poolSize;
        }

        Rect rect = area.rect;
        float size = rect.width * coinWidthRatio;
        for (int index = 0; index < poolSize; index++)
        {
            if (coinAges[index] < 0f) continue;
            coinAges[index] += Time.unscaledDeltaTime;
            float t = coinAges[index] / fallSeconds;
            if (t >= 1f)
            {
                // 더미에 닿으면 사라져 더미의 일부가 된 것처럼 보이게 한다.
                coinAges[index] = -1f;
                coins[index].gameObject.SetActive(false);
                continue;
            }

            // 중력처럼 점점 빨라지며 떨어지고, 살짝 돌며 마지막 순간에 흐려진다.
            float fall = t * t;
            Vector2 normalized = Vector2.Lerp(neckPosition, pileTopPosition, fall);
            var coinRect = coins[index].rectTransform;
            coinRect.anchorMin = coinRect.anchorMax = Vector2.zero;
            coinRect.sizeDelta = new Vector2(size, size);
            coinRect.anchoredPosition = new Vector2(normalized.x * rect.width, normalized.y * rect.height);
            coinRect.localScale = new Vector3(Mathf.Cos(t * Mathf.PI * 3f) * .35f + .65f, 1f, 1f);
            coins[index].color = new Color(1f, 1f, 1f, t < .85f ? 1f : 1f - (t - .85f) / .15f);
        }
    }
}
