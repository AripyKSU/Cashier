using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영업 화면 위에 감독관의 짧은 안내 말풍선과 강조 테두리를 띄우는 Presenter.
/// 줄을 클릭으로 넘기고, 마지막 줄을 넘기면 완료 콜백을 부릅니다. 진행 판단은 하지 않습니다.
/// </summary>
public sealed class TutorialCoachPresenter : MonoBehaviour
{
    /// <summary>새 줄이 뜬 직후 같은 클릭으로 바로 넘어가지 않도록 무시하는 시간(초).</summary>
    private const float ClickGuardSeconds = 0.2f;
    /// <summary>감독관 대사 출력 속도(초당 글자).</summary>
    private const float CharactersPerSecond = 40f;
    /// <summary>감독관 목소리 크기. 영업 전 감독관 패널과 같다.</summary>
    private const float VoiceVolumeScale = 0.75f;

    [Tooltip("안내가 떠 있는 동안 화면 입력을 막는 전체 CanvasGroup")]
    [SerializeField] private CanvasGroup rootGroup;

    [Tooltip("감독관 대사 텍스트")]
    [SerializeField] private TextMeshProUGUI lineText;

    [Tooltip("강조 테두리. 대상이 없으면 숨깁니다.")]
    [SerializeField] private RectTransform highlight;

    [Tooltip("강조 테두리 이미지들. 깜빡임 알파를 적용합니다.")]
    [SerializeField] private Image[] highlightEdges = Array.Empty<Image>();

    private string[] lines = Array.Empty<string>();
    private RectTransform[] targets = Array.Empty<RectTransform>();
    private int lineIndex;
    private float lineShownAt;
    private bool isTyping;
    private Action finished;

    /// <summary>안내가 화면에 떠 있는지 여부입니다.</summary>
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        hide();
    }

    private void Update()
    {
        if (!IsShowing) return;
        updateHighlight();
        updateTyping();
        if (Time.unscaledTime - lineShownAt < ClickGuardSeconds || !wasClickedThisFrame()) return;
        // 글자가 아직 나오는 중이면 한 번 클릭으로 줄 전체를 먼저 보여 준다.
        if (isTyping)
        {
            finishTyping();
            return;
        }

        Advance();
    }

    /// <summary>감독관 대사를 한 글자씩 보여 줍니다.</summary>
    private void updateTyping()
    {
        if (!isTyping || lineText == null) return;
        int total = lineText.textInfo.characterCount;
        int visible = Mathf.Min(total, Mathf.FloorToInt((Time.unscaledTime - lineShownAt) * CharactersPerSecond));
        lineText.maxVisibleCharacters = visible;
        if (visible >= total) finishTyping();
    }

    /// <summary>줄 전체를 보이고 감독관 목소리를 멈춥니다.</summary>
    private void finishTyping()
    {
        isTyping = false;
        if (lineText != null) lineText.maxVisibleCharacters = int.MaxValue;
        SoundManager.Instance?.StopSfxForDuration(SoundKeys.DialogueVoice);
    }

    /// <summary>다음 줄로 넘깁니다. 마지막 줄이면 안내를 닫고 완료 콜백을 부릅니다.</summary>
    public void Advance()
    {
        if (!IsShowing) return;
        lineIndex++;
        if (lineIndex < lines.Length)
        {
            showLine();
            return;
        }

        Action callback = finished;
        hide();
        callback?.Invoke();
    }

    /// <summary>
    /// 여러 줄 안내를 띄웁니다. 줄마다 강조할 화면 영역을 함께 지정할 수 있습니다.
    /// </summary>
    /// <param name="newLines">감독관 대사 줄들입니다.</param>
    /// <param name="newTargets">줄과 같은 순서의 강조 대상. 짧거나 null이면 강조하지 않습니다.</param>
    /// <param name="onFinished">마지막 줄을 넘긴 뒤 호출됩니다.</param>
    public void Show(string[] newLines, RectTransform[] newTargets, Action onFinished)
    {
        if (newLines == null || newLines.Length == 0) throw new ArgumentException("안내 대사가 비어 있습니다.", nameof(newLines));
        lines = newLines;
        targets = newTargets ?? Array.Empty<RectTransform>();
        finished = onFinished;
        lineIndex = 0;
        IsShowing = true;
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        }

        showLine();
    }

    /// <summary>현재 줄과 강조 대상을 표시합니다.</summary>
    private void showLine()
    {
        lineShownAt = Time.unscaledTime;
        if (lineText != null)
        {
            lineText.text = lines[lineIndex];
            lineText.maxVisibleCharacters = 0;
            lineText.ForceMeshUpdate(true, true);
            isTyping = true;
            // 영업 화면 감독관과 같은 목소리로 말해, 그림 없이도 감독관인 줄 알게 한다.
            float seconds = lineText.textInfo.characterCount / CharactersPerSecond;
            SoundManager.Instance?.PlaySfxForDuration(SoundKeys.DialogueVoice, seconds, VoiceVolumeScale);
        }

        updateHighlight();
    }

    /// <summary>강조 테두리를 대상 영역에 맞추고 천천히 깜빡이게 합니다.</summary>
    private void updateHighlight()
    {
        if (highlight == null) return;
        RectTransform target = lineIndex < targets.Length ? targets[lineIndex] : null;
        bool visible = target != null && target.gameObject.activeInHierarchy;
        highlight.gameObject.SetActive(visible);
        if (!visible) return;

        // 대상의 화면 사각형을 테두리 부모 좌표로 옮긴다. 대상이 다른 캔버스여도 같은 방식이다.
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        var parent = (RectTransform)highlight.parent;
        Vector3 min = parent.InverseTransformPoint(corners[0]);
        Vector3 max = parent.InverseTransformPoint(corners[2]);
        const float padding = 8f;
        const float screenInset = 10f;
        // 화면 끝까지 닿는 구역도 네 변이 모두 보이도록 화면 안쪽으로 잘라 낸다.
        Rect bounds = parent.rect;
        float left = Mathf.Max(Mathf.Min(min.x, max.x) - padding, bounds.xMin + screenInset);
        float right = Mathf.Min(Mathf.Max(min.x, max.x) + padding, bounds.xMax - screenInset);
        float bottom = Mathf.Max(Mathf.Min(min.y, max.y) - padding, bounds.yMin + screenInset);
        float top = Mathf.Min(Mathf.Max(min.y, max.y) + padding, bounds.yMax - screenInset);
        highlight.anchorMin = highlight.anchorMax = new Vector2(.5f, .5f);
        highlight.pivot = new Vector2(.5f, .5f);
        highlight.localPosition = new Vector3((left + right) * .5f, (bottom + top) * .5f, 0f);
        highlight.sizeDelta = new Vector2(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom));
        float alpha = .55f + .45f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (var edge in highlightEdges)
            if (edge != null) edge.color = new Color(1f, .82f, .35f, alpha);
    }

    /// <summary>안내를 숨기고 입력 차단을 풉니다.</summary>
    private void hide()
    {
        if (isTyping) finishTyping();
        IsShowing = false;
        finished = null;
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (highlight != null) highlight.gameObject.SetActive(false);
    }

    /// <summary>이번 프레임에 마우스 왼쪽 버튼이 눌렸는지 확인합니다.</summary>
    /// <returns>눌렸으면 true입니다.</returns>
    private static bool wasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
