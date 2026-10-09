using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마지막 날 영업을 마쳤는데 총 자산이 시민권 가격보다 적을 때의 연출.
/// 먼저 검은 화면에 시민권 가격·총 자산·부족한 금액을 한 줄씩 찍고 "시민권 구매 실패"를 보여 준 뒤 정산으로 넘긴다.
/// 정산에서 "다음 날"을 누르면 아빠가 쓰러진 하루를 부르며 절규한다.
/// 대사창·글자·정산 화면이 줄마다 점점 세게 흔들리고, 마지막 줄 뒤 화면이 어두워지면 완료 콜백을 부른다.
/// 진행 판단은 하지 않고 표시만 합니다.
/// </summary>
public sealed class FinalDayCollapsePresenter : MonoBehaviour
{
    [Tooltip("시민권 구매 실패 검은 화면")]
    [SerializeField] private CanvasGroup shortfallGroup;

    [Tooltip("검은 화면 줄: 시민권 가격, 총 자산, 부족한 금액, 구매 실패")]
    [SerializeField] private TextMeshProUGUI[] shortfallLines;

    [Tooltip("절규 연출 전체(입력 차단 포함)")]
    [SerializeField] private CanvasGroup overlayGroup;

    [Tooltip("화면을 붉게 어둡게 덮는 판")]
    [SerializeField] private Image dim;

    [Tooltip("아빠 대사창")]
    [SerializeField] private RectTransform dialogueBox;

    [Tooltip("대사 글자")]
    [SerializeField] private TextMeshProUGUI dialogue;

    [Tooltip("함께 흔들 정산 화면")]
    [SerializeField] private RectTransform shakeTarget;

    [Tooltip("줄이 넘어갈 때마다 세지는 흔들림 세기(픽셀)")]
    [SerializeField] private float[] shakePixels = { 4f, 10f, 14f, 24f };

    private string[] lines = Array.Empty<string>();
    private int lineIndex;
    private float lineShownAt;
    private float strength;
    private bool isPlaying;
    private Action finished;
    private Vector2 boxRest;
    private Vector2 targetRest;
    private bool waitingShortfall;
    private Action shortfallDone;

    private void Awake()
    {
        hideOverlay();
    }

    /// <summary>
    /// 검은 화면에 시민권 가격, 총 자산, 부족한 금액을 차례로 찍고 "시민권 구매 실패"를 보여 줍니다.
    /// 다 나온 뒤 클릭하거나 잠시 기다리면 화면이 걷히며 콜백을 부릅니다.
    /// </summary>
    /// <param name="labels">줄 이름 네 개: 시민권 가격, 총 자산, 부족한 금액, 구매 실패 문구입니다.</param>
    /// <param name="price">시민권 가격입니다.</param>
    /// <param name="assets">정산 뒤 총 자산입니다.</param>
    /// <param name="onFinished">검은 화면이 걷히면 한 번 호출됩니다.</param>
    public void ShowShortfall(string[] labels, long price, long assets, Action onFinished)
    {
        shortfallLines[0].text = $"{labels[0]}   {price:N0}원";
        shortfallLines[1].text = $"{labels[1]}   {assets:N0}원";
        shortfallLines[2].text = $"{labels[2]}   <color=#a8473c>{price - assets:N0}원</color>";
        shortfallLines[3].text = labels[3];
        foreach (var line in shortfallLines) line.alpha = 0f;
        setBoxVisible(false);
        dim.color = Color.clear;
        overlayGroup.alpha = 1f;
        overlayGroup.blocksRaycasts = true;
        shortfallGroup.alpha = 1f;

        // 한 줄씩 "딱" 하고 찍힌다. 마지막 실패 문구는 조금 더 뜸을 들인다.
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        float[] delays = { .8f, .8f, .8f, 1.2f };
        for (int index = 0; index < shortfallLines.Length; index++)
        {
            TextMeshProUGUI line = shortfallLines[index];
            sequence.AppendInterval(delays[index]);
            sequence.AppendCallback(() =>
            {
                line.alpha = 1f;
                line.rectTransform.localScale = Vector3.one * 1.25f;
                line.rectTransform.DOScale(1f, .15f).SetUpdate(true);
                SoundManager.Instance?.PlaySfx(SoundKeys.ReputationStamp);
            });
        }

        sequence.AppendCallback(() => waitingShortfall = true);
        sequence.AppendInterval(3f);
        sequence.AppendCallback(endShortfall);
        shortfallDone = onFinished;
    }

    /// <summary>검은 화면을 걷고 정산으로 넘깁니다. 한 번만 동작합니다.</summary>
    private void endShortfall()
    {
        if (shortfallDone == null) return;
        waitingShortfall = false;
        DOTween.Kill(this);
        Action callback = shortfallDone;
        shortfallDone = null;
        DOTween.To(() => shortfallGroup.alpha, value => shortfallGroup.alpha = value, 0f, .8f).SetUpdate(true)
            .OnComplete(hideOverlay);
        callback();
    }

    private void setBoxVisible(bool visible)
    {
        if (dialogueBox.TryGetComponent(out CanvasGroup boxGroup)) boxGroup.alpha = visible ? 1f : 0f;
    }

    /// <summary>아빠의 절규를 한 줄씩 보여 주고, 끝나면 화면을 어둡게 한 뒤 콜백을 부릅니다.</summary>
    /// <param name="screamLines">아빠 대사 줄들입니다. 뒤로 갈수록 세게 흔들립니다.</param>
    /// <param name="onFinished">마지막 줄 뒤 화면이 어두워지면 한 번 호출됩니다.</param>
    public void Play(string[] screamLines, Action onFinished)
    {
        if (screamLines == null || screamLines.Length == 0) throw new ArgumentException("절규 대사가 비어 있습니다.", nameof(screamLines));
        lines = screamLines;
        finished = onFinished;
        lineIndex = 0;
        boxRest = dialogueBox.anchoredPosition;
        targetRest = shakeTarget != null ? shakeTarget.anchoredPosition : Vector2.zero;
        overlayGroup.alpha = 1f;
        overlayGroup.blocksRaycasts = true;
        shortfallGroup.alpha = 0f;
        setBoxVisible(true);
        dim.color = new Color(.08f, .01f, .01f, 0f);
        tweenDim(new Color(.08f, .01f, .01f, .55f), .6f);
        isPlaying = true;
        showLine();
    }

    private void Update()
    {
        if (waitingShortfall && wasClickedThisFrame())
        {
            endShortfall();
            return;
        }

        if (!isPlaying) return;
        shake();
        if (Time.unscaledTime - lineShownAt < .35f || !wasClickedThisFrame()) return;
        if (lineIndex < lines.Length - 1)
        {
            lineIndex++;
            showLine();
            return;
        }

        finish();
    }

    private void showLine()
    {
        dialogue.text = lines[lineIndex];
        strength = shakePixels[Mathf.Min(lineIndex, shakePixels.Length - 1)];
        lineShownAt = Time.unscaledTime;
        // 줄이 바뀌는 순간 크게 한 번 튀었다가 계속 떤다.
        dialogueBox.DOKill();
        dialogueBox.localScale = Vector3.one * (1f + strength * .006f);
        dialogueBox.DOScale(1f, .25f).SetUpdate(true);
        dim.DOKill();
        dim.color = new Color(.35f, .02f, .02f, Mathf.Min(.75f, .4f + lineIndex * .1f));
        tweenDim(new Color(.08f, .01f, .01f, Mathf.Min(.7f, .4f + lineIndex * .1f)), .5f);
    }

    /// <summary>대사창과 정산 화면을 흔들고, 글자 하나하나를 따로 떨게 합니다.</summary>
    private void shake()
    {
        float time = Time.unscaledTime;
        dialogueBox.anchoredPosition = boxRest + UnityEngine.Random.insideUnitCircle * strength * .6f;
        if (shakeTarget != null) shakeTarget.anchoredPosition = targetRest + UnityEngine.Random.insideUnitCircle * strength * .35f;

        dialogue.ForceMeshUpdate();
        TMP_TextInfo info = dialogue.textInfo;
        for (int index = 0; index < info.characterCount; index++)
        {
            TMP_CharacterInfo character = info.characterInfo[index];
            if (!character.isVisible) continue;
            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(index * 1.7f, time * 18f) - .5f,
                Mathf.PerlinNoise(time * 18f, index * 2.3f) - .5f) * strength * .9f;
            for (int corner = 0; corner < 4; corner++) vertices[character.vertexIndex + corner] += offset;
        }

        for (int mesh = 0; mesh < info.meshInfo.Length; mesh++)
        {
            info.meshInfo[mesh].mesh.vertices = info.meshInfo[mesh].vertices;
            dialogue.UpdateGeometry(info.meshInfo[mesh].mesh, mesh);
        }
    }

    private void finish()
    {
        isPlaying = false;
        dialogueBox.anchoredPosition = boxRest;
        if (shakeTarget != null) shakeTarget.anchoredPosition = targetRest;
        if (dialogueBox.TryGetComponent(out CanvasGroup boxGroup)) DOTween.To(() => boxGroup.alpha, value => boxGroup.alpha = value, 0f, .8f).SetUpdate(true);
        dim.DOKill();
        tweenDim(Color.black, 1.2f).OnComplete(() =>
        {
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        });
    }

    private Tween tweenDim(Color to, float duration) =>
        DOTween.To(() => dim.color, value => dim.color = value, to, duration).SetUpdate(true).SetTarget(dim);

    private void hideOverlay()
    {
        if (overlayGroup == null) return;
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
    }

    private static bool wasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
