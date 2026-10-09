using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마지막 날 정산에서 시민권을 끝내 사지 못했을 때의 연출.
/// 정산 화면 테이블 위에 하루가 쓰러져 있고, "다음 날"을 누르면 아빠가 하루를 부르며 절규한다.
/// 대사창·글자·정산 화면이 줄마다 점점 세게 흔들리고, 마지막 줄 뒤 화면이 어두워지면 완료 콜백을 부른다.
/// 진행 판단은 하지 않고 표시만 합니다.
/// </summary>
public sealed class FinalDayCollapsePresenter : MonoBehaviour
{
    [Tooltip("테이블 위에 쓰러진 하루 그림")]
    [SerializeField] private Image lyingDaughter;

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

    private void Awake()
    {
        if (lyingDaughter != null) lyingDaughter.gameObject.SetActive(false);
        hideOverlay();
    }

    /// <summary>테이블 위에 쓰러진 하루를 보이거나 숨깁니다.</summary>
    /// <param name="visible">보일지 여부입니다.</param>
    public void SetDaughterLying(bool visible)
    {
        lyingDaughter.gameObject.SetActive(visible);
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
        dim.color = new Color(.08f, .01f, .01f, 0f);
        tweenDim(new Color(.08f, .01f, .01f, .55f), .6f);
        isPlaying = true;
        showLine();
    }

    private void Update()
    {
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
