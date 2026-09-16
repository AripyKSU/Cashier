using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI Image로 그린 상자가 위에서 떨어져 착지할 때, 밑면을 고정한 채 짧게 눌렸다가 복원되고
/// 좌우로 픽셀 먼지가 잠깐 퍼지는 연출입니다. Scene에 저장된 배치(위치·크기·스케일)를 기준값으로 읽어
/// 상대적으로만 움직이며, 연출이 끝나면 원래 값으로 되돌립니다. 연출 중 사용자가 Inspector에서 배치를
/// 바꾸면 즉시 중단하고 그 배치를 보존합니다. 정면 화면(DystopiaTopDownTest.PlaceContainer)과 같은 수치입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CrateLandingEffect : MonoBehaviour
{
    /// <summary>착지시킬 상자 Image입니다. 비워 두면 같은 오브젝트의 Image를 사용합니다.</summary>
    [SerializeField] private Image crate;
    /// <summary>먼지를 만들 부모입니다. 비워 두면 상자의 부모(같은 Canvas 좌표계)를 사용합니다.</summary>
    [SerializeField] private RectTransform dustParent;
    /// <summary>상자가 떨어지기 시작하는 높이입니다. 단위는 Canvas 기준 해상도(1280×720) 픽셀입니다.</summary>
    [SerializeField, Min(0)] private float dropHeight = 76f;
    /// <summary>낙하 시간(초)입니다. 이 시간 동안 dropHeight에서 기준 위치까지 가속하며 내려옵니다.</summary>
    [SerializeField, Min(.01f)] private float dropSeconds = .3f;
    /// <summary>착지 후 눌림·복원·먼지가 진행되는 시간(초)입니다.</summary>
    [SerializeField, Min(.01f)] private float settleSeconds = .55f;
    /// <summary>착지 순간 가로로 늘고 세로로 눌리는 최대 비율입니다. 0.1이면 ±10%입니다.</summary>
    [SerializeField, Range(0, .5f)] private float squash = .10f;
    /// <summary>먼지 조각 수입니다. 좌우로 번갈아 배치됩니다.</summary>
    [SerializeField, Range(0, 24)] private int dustCount = 10;
    /// <summary>먼지 한 조각의 크기(Canvas 픽셀)입니다. 도트 화면에 맞춰 2의 배수 격자에 놓입니다.</summary>
    [SerializeField] private Vector2 dustSize = new Vector2(12, 6);
    /// <summary>먼지 색입니다. 알파는 착지 직후 최대값(0.42)에서 0으로 줄어듭니다.</summary>
    [SerializeField] private Color dustColor = new Color(.34f, .32f, .28f, 1f);
    /// <summary>먼지 조각에 쓸 Sprite입니다. 비워 두면 단색 사각형(uGUI 기본)입니다.</summary>
    [SerializeField] private Sprite dustSprite;
    /// <summary>일시정지 중에는 시간을 진행하지 않습니다. 외부에서 상태를 넣어 줍니다.</summary>
    public System.Func<bool> IsPaused;

    private Image[] dust = System.Array.Empty<Image>();
    private Coroutine running;

    /// <summary>연출이 진행 중이면 true입니다.</summary>
    public bool IsPlaying => running != null;

    /// <summary>현재 배치를 기준으로 착지 연출을 시작합니다. 이미 진행 중이면 먼저 복원한 뒤 다시 시작합니다.</summary>
    public void Play()
    {
        if (crate == null) crate = GetComponent<Image>();
        if (crate == null) { Debug.LogWarning("CrateLandingEffect: 상자 Image가 없습니다.", this); return; }
        Stop();
        EnsureDust();
        running = StartCoroutine(Land());
    }

    /// <summary>진행 중인 연출을 멈추고 상자와 먼지를 시작 상태로 되돌립니다.</summary>
    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        foreach (Image piece in dust) if (piece != null) piece.color = Color.clear;
    }

    private void OnDisable() { Stop(); }

    /// <summary>먼지 Image가 부족하면 만들고, 남으면 숨깁니다. 씬에 미리 만들어 둔 자식 "LandingDust*"도 재사용합니다.</summary>
    private void EnsureDust()
    {
        RectTransform parent = dustParent != null ? dustParent : crate.rectTransform.parent as RectTransform;
        if (dust.Length != dustCount)
        {
            var list = new System.Collections.Generic.List<Image>(dustCount);
            for (int i = 0; i < dustCount; i++)
            {
                Transform existing = parent.Find("LandingDust" + i);
                Image piece = existing != null ? existing.GetComponent<Image>() : null;
                if (piece == null)
                {
                    var go = new GameObject("LandingDust" + i, typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(parent, false);
                    piece = go.GetComponent<Image>();
                    var rect = piece.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.sizeDelta = dustSize;
                    piece.raycastTarget = false;
                    piece.sprite = dustSprite;
                }
                piece.color = Color.clear;
                // 먼지는 상자보다 뒤에 그립니다(상자 바로 앞 형제 순서).
                piece.transform.SetSiblingIndex(Mathf.Max(0, crate.transform.GetSiblingIndex()));
                list.Add(piece);
            }
            dust = list.ToArray();
        }
    }

    /// <summary>낙하 → 착지 눌림·복원 → 먼지 확산. 기준 배치에 상대적으로만 움직입니다.</summary>
    private IEnumerator Land()
    {
        RectTransform box = crate.rectTransform;
        Vector2 rest = box.anchoredPosition;
        Vector3 restScale = box.localScale;
        Vector2 lastPosition = rest;
        Vector3 lastScale = restScale;
        float total = dropSeconds + settleSeconds;
        float elapsed = 0;
        while (elapsed < total)
        {
            // 사용자가 Inspector에서 배치를 바꾸면 연출을 중단하고 그 값을 보존합니다.
            if (box.anchoredPosition != lastPosition || box.localScale != lastScale)
            {
                foreach (Image piece in dust) piece.color = Color.clear;
                running = null;
                yield break;
            }
            if (IsPaused == null || !IsPaused()) elapsed += Time.unscaledDeltaTime;
            float drop = Mathf.Clamp01(elapsed / dropSeconds);
            float impact = Mathf.Clamp01((elapsed - dropSeconds) / settleSeconds);
            // 착지 순간 한 번 크게 눌리고 감쇠 진동으로 복원됩니다.
            float s = Mathf.Sin(impact * Mathf.PI * 2) * Mathf.Exp(-impact * 4) * squash;
            float scaleX = 1 + s;
            float scaleY = 1 - s;
            box.localScale = Vector3.Scale(restScale, new Vector3(scaleX, scaleY, 1));
            // 좌상단 피벗 기준이므로 밑면이 고정되도록 눌린 만큼 위치를 보정합니다.
            box.anchoredPosition = rest + new Vector2(box.sizeDelta.x * (1 - scaleX) * .5f,
                dropHeight * (1 - drop * drop) - box.sizeDelta.y * (1 - scaleY));
            lastPosition = box.anchoredPosition;
            lastScale = box.localScale;
            for (int i = 0; i < dust.Length; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float spread = 80 + i / 2 * 13 + impact * (35 + i * 3);
                dust[i].rectTransform.anchoredPosition = new Vector2(
                    Mathf.Round((rest.x + box.sizeDelta.x * restScale.x * .5f + side * spread) / 2) * 2,
                    Mathf.Round((rest.y - box.sizeDelta.y * restScale.y + 12 + Mathf.Sin(impact * Mathf.PI * .5f) * (12 + i % 3 * 5)) / 2) * 2);
                dust[i].color = new Color(dustColor.r, dustColor.g, dustColor.b, elapsed > dropSeconds ? (1 - impact) * .42f : 0);
            }
            yield return null;
        }
        if (box.anchoredPosition == lastPosition && box.localScale == lastScale)
        {
            box.anchoredPosition = rest;
            box.localScale = restScale;
        }
        foreach (Image piece in dust) piece.color = Color.clear;
        running = null;
    }
}
