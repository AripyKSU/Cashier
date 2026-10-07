using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정산 화면의 딸 대사와 이미지만 표시한다.</summary>
public sealed class DaughterDialoguePresenter : MonoBehaviour
{
    private const float DialogueVoiceDurationSeconds = 0.35f;

    [SerializeField] private Image portrait;
    [SerializeField] private Image speechBubble;
    [SerializeField] private TextMeshProUGUI dialogue;
    [SerializeField, Min(1f)] private float charactersPerSecond = 24f;
    [SerializeField, Min(0f)] private float nodDurationSeconds = 0.8f;
    [SerializeField, Min(0f)] private float nodAngleDegrees = 6f;
    [SerializeField, Min(0f)] private float nodDistancePixels = 5f;
    /// <summary>여러 줄 대본에서 한 줄을 다 읽은 뒤 다음 줄로 넘어가기까지 기다리는 시간(초). 클릭하면 바로 넘어갑니다.</summary>
    [SerializeField, Min(0f)] private float lineHoldSeconds = 1.6f;

    private Tween presentationTween;
    private Tween holdTween;
    // 날짜 전용 대본. 비어 있으면 기존 한 줄 대사를 그대로 사용합니다.
    private string[] beforeStampLines = Array.Empty<string>();
    private string[] afterStampLines = Array.Empty<string>();
    private string[] playingLines = Array.Empty<string>();
    private int playingLineIndex;
    private bool isPlayingAfterStamp;
    private bool isTyping;
    private bool hasCompletedAfterStamp;
    private DaughterDialogueViewData preparedViewData;
    private Vector2 portraitRestPosition;
    private Vector3 portraitRestScale;
    private Quaternion portraitRestRotation;
    private uint presentedDay;
    private bool hasPrepared;
    private bool hasCompleted;
    private bool hasCachedPortraitTransform;

    /// <summary>딸 대사의 모든 문자가 공개됐을 때 한 번 발생합니다.</summary>
    public event Action OnPresentationCompleted;

    /// <summary>도장 뒤에 이어지는 대본이 모두 끝났을 때 한 번 발생합니다.</summary>
    public event Action OnAfterStampCompleted;

    /// <summary>도장 뒤에 이어서 말할 대본이 준비됐는지 여부입니다.</summary>
    public bool HasAfterStampLines => afterStampLines.Length > 0 && !hasCompletedAfterStamp;

    private void Awake()
    {
        ValidateReferences();
        cachePortraitRestTransform();
    }

    /// <summary>직렬화된 필수 참조를 확인한다.</summary>
    /// <exception cref="InvalidOperationException">이미지 또는 텍스트 연결 누락.</exception>
    public void ValidateReferences()
    {
        if (portrait == null || speechBubble == null || dialogue == null)
            throw new InvalidOperationException("DaughterDialoguePanel: 딸 이미지, 말풍선과 대사 참조가 필요합니다.");
    }

    /// <summary>이미 확정된 표시값을 다시 선택하지 않고 반영한다.</summary>
    /// <param name="viewData">딸 대사 표시 스냅샷.</param>
    public void UpdateView(DaughterDialogueViewData viewData)
    {
        ValidateReferences();
        portrait.sprite = viewData.Sprite;
        if (presentedDay == viewData.Day && hasCompleted)
        {
            dialogue.text = viewData.Text;
            dialogue.maxVisibleCharacters = int.MaxValue;
            setBubbleVisible(true);
            dialogue.enabled = true;
            return;
        }

        stopPresentation();
        restorePortrait();
        preparedViewData = viewData;
        presentedDay = viewData.Day;
        hasPrepared = true;
        hasCompleted = false;
        hasCompletedAfterStamp = false;
        beforeStampLines = Array.Empty<string>();
        afterStampLines = Array.Empty<string>();
        dialogue.text = viewData.Text;
        dialogue.maxVisibleCharacters = 0;
        setBubbleVisible(false);
        dialogue.enabled = false;
        cachePortraitRestTransform();
    }

    /// <summary>
    /// 이 날짜에만 쓰는 여러 줄 대본을 준비합니다. UpdateView 뒤에 호출합니다.
    /// 도장 전 대본은 기존 한 줄 대사를 대신하고, 도장 뒤 대본은 <see cref="PresentAfterStamp"/>에서 이어서 말합니다.
    /// </summary>
    /// <param name="beforeStamp">도장 전에 말할 줄들입니다. 비어 있으면 기존 한 줄 대사를 사용합니다.</param>
    /// <param name="afterStamp">도장 뒤에 말할 줄들입니다. 비어 있으면 도장으로 정산 연출을 끝냅니다.</param>
    public void SetScript(string[] beforeStamp, string[] afterStamp)
    {
        if (hasCompleted) return;
        beforeStampLines = beforeStamp ?? Array.Empty<string>();
        afterStampLines = afterStamp ?? Array.Empty<string>();
    }

    /// <summary>준비된 딸과 말풍선을 공개하고 초당 24자로 대사를 출력합니다.</summary>
    /// <exception cref="InvalidOperationException">표시할 대사가 준비되지 않은 경우.</exception>
    public void Present()
    {
        ValidateReferences();
        if (!hasPrepared)
            throw new InvalidOperationException("정산 딸 대사가 준비되지 않았습니다.");
        if (hasCompleted) return;
        isPlayingAfterStamp = false;
        playingLines = beforeStampLines.Length > 0 ? beforeStampLines : new[] { preparedViewData.Text };
        playingLineIndex = 0;
        playLine();
    }

    /// <summary>도장을 찍은 뒤 이어지는 대본을 출력합니다. 대본이 없으면 바로 완료를 알립니다.</summary>
    public void PresentAfterStamp()
    {
        ValidateReferences();
        if (hasCompletedAfterStamp) return;
        if (afterStampLines.Length == 0)
        {
            completeAfterStamp();
            return;
        }

        isPlayingAfterStamp = true;
        playingLines = afterStampLines;
        playingLineIndex = 0;
        playLine();
    }

    private void Update()
    {
        // 여러 줄 대본은 클릭으로 넘길 수 있다. 타이핑 중이면 한 줄을 바로 다 보여 준다.
        if (playingLines.Length <= 1 && !isPlayingAfterStamp) return;
        if (!wasClickedThisFrame()) return;
        if (isTyping)
        {
            presentationTween?.Complete(true);
            return;
        }

        if (holdTween != null && holdTween.IsActive())
        {
            holdTween.Kill();
            holdTween = null;
            advanceLine();
        }
    }

    /// <summary>현재 줄을 말풍선에 넣고 타이핑을 시작합니다.</summary>
    private void playLine()
    {
        stopPresentation();
        setBubbleVisible(true);
        dialogue.enabled = true;
        dialogue.text = playingLines[playingLineIndex];
        dialogue.maxVisibleCharacters = 0;
        dialogue.ForceMeshUpdate(true, true);
        SoundManager.Instance?.PlaySfxForDuration(
            SoundKeys.DialogueVoice,
            DialogueVoiceDurationSeconds);
        playPresentation();
    }

    /// <summary>한 줄을 다 보여 준 뒤 다음 줄 또는 완료로 넘어갑니다.</summary>
    private void handleLineTyped()
    {
        bool isLastLine = playingLineIndex >= playingLines.Length - 1;
        // 기존 한 줄 대사는 예전처럼 타이핑이 끝나자마자 완료한다.
        if (playingLines.Length == 1 && !isPlayingAfterStamp)
        {
            completePresentation();
            return;
        }

        SoundManager.Instance?.StopSfxForDuration(SoundKeys.DialogueVoice);
        // 마지막 줄은 조금 더 오래 남겨 다음 연출(도장 등)과 겹치지 않게 한다.
        float holdSeconds = isLastLine ? lineHoldSeconds * 1.25f : lineHoldSeconds;
        holdTween = DOVirtual.DelayedCall(holdSeconds, () =>
        {
            holdTween = null;
            advanceLine();
        }).SetUpdate(true);
    }

    /// <summary>다음 줄로 넘어가거나, 마지막 줄이었다면 해당 구간 완료를 알립니다.</summary>
    private void advanceLine()
    {
        if (playingLineIndex < playingLines.Length - 1)
        {
            playingLineIndex++;
            playLine();
            return;
        }

        if (isPlayingAfterStamp) completeAfterStamp();
        else completePresentation();
    }

    /// <summary>도장 뒤 대본 완료를 한 번만 알립니다.</summary>
    private void completeAfterStamp()
    {
        SoundManager.Instance?.StopSfxForDuration(SoundKeys.DialogueVoice);
        restorePortrait();
        if (hasCompletedAfterStamp) return;
        hasCompletedAfterStamp = true;
        isPlayingAfterStamp = false;
        OnAfterStampCompleted?.Invoke();
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

    private void OnDisable()
    {
        stopPresentation();
        SoundManager.Instance?.StopSfxForDuration(SoundKeys.DialogueVoice);
        restorePortrait();
    }

    /// <summary>대사 타이핑과 등장 직후 두 번의 짧은 끄덕임을 함께 재생합니다.</summary>
    private void playPresentation()
    {
        int characterCount = dialogue.textInfo.characterCount;
        if (characterCount == 0 && dialogue.text.Length > 0)
            throw new InvalidOperationException("딸 대사의 TMP 문자 정보를 생성하지 못했습니다.");
        float elapsedSeconds = 0f;
        float duration = characterCount / Mathf.Max(1f, charactersPerSecond);
        if (duration <= 0f) { handleLineTyped(); return; }
        isTyping = true;
        presentationTween = DOTween.To(() => elapsedSeconds, value =>
            {
                elapsedSeconds = value;
                dialogue.maxVisibleCharacters = Mathf.Min(characterCount,
                    Mathf.FloorToInt(value * charactersPerSecond));
                updatePortraitNod(value);
            }, duration, duration).SetEase(Ease.Linear).SetUpdate(true)
            .OnComplete(() =>
            {
                isTyping = false;
                dialogue.maxVisibleCharacters = characterCount;
                handleLineTyped();
            });
    }

    /// <summary>타이핑 완료 시 자세·음성을 정리하고 완료를 한 번 통지합니다.</summary>
    private void completePresentation()
    {
        SoundManager.Instance?.StopSfxForDuration(SoundKeys.DialogueVoice);
        restorePortrait();
        presentationTween = null;
        if (hasCompleted) return;
        hasCompleted = true;
        OnPresentationCompleted?.Invoke();
    }

    /// <summary>말풍선 본체와 꼬리 같은 자식 장식 이미지를 함께 보이거나 숨깁니다. 대사 텍스트는 따로 제어합니다.</summary>
    /// <param name="visible">보일지 여부입니다.</param>
    private void setBubbleVisible(bool visible)
    {
        foreach (var image in speechBubble.GetComponentsInChildren<Image>(true))
            image.enabled = visible;
    }

    /// <summary>Astra의 목 중심 보정을 유지한 채 등장 직후 두 번만 고개를 끄덕입니다.</summary>
    /// <param name="elapsedSeconds">대사 표시 시작 후 실제 경과 시간.</param>
    private void updatePortraitNod(float elapsedSeconds)
    {
        if (nodDurationSeconds <= 0f || elapsedSeconds >= nodDurationSeconds)
        {
            restorePortrait();
            return;
        }

        RectTransform rect = portrait.rectTransform;
        float phase = Mathf.Repeat(elapsedSeconds, nodDurationSeconds * 0.5f) / (nodDurationSeconds * 0.5f);
        float nod = Mathf.Sin(phase * Mathf.PI);
        Quaternion turn = Quaternion.Euler(0f, 0f, nod * nodAngleDegrees);
        Vector3 neck = Vector3.Scale(
            new Vector3(rect.rect.center.x, rect.rect.yMin + rect.rect.height * 0.3f, 0f),
            portraitRestScale);
        Vector3 pivotOffset = portraitRestRotation * (neck - turn * neck);
        rect.anchoredPosition = portraitRestPosition + (Vector2)pivotOffset + Vector2.down * nod * nodDistancePixels;
        rect.localRotation = portraitRestRotation * turn;
    }

    /// <summary>현재 Prefab 배치를 연출의 원래 자세로 저장합니다.</summary>
    private void cachePortraitRestTransform()
    {
        RectTransform rect = portrait.rectTransform;
        portraitRestPosition = rect.anchoredPosition;
        portraitRestScale = rect.localScale;
        portraitRestRotation = rect.localRotation;
        hasCachedPortraitTransform = true;
    }

    /// <summary>화면 재진입이나 중단 뒤 딸 이미지의 원래 자세를 복원합니다.</summary>
    private void restorePortrait()
    {
        if (portrait == null || !hasCachedPortraitTransform) return;
        RectTransform rect = portrait.rectTransform;
        rect.anchoredPosition = portraitRestPosition;
        rect.localScale = portraitRestScale;
        rect.localRotation = portraitRestRotation;
    }

    /// <summary>진행 중인 딸 대사 연출을 중복 실행 없이 중단합니다.</summary>
    private void stopPresentation()
    {
        presentationTween?.Kill();
        presentationTween = null;
        holdTween?.Kill();
        holdTween = null;
        isTyping = false;
    }
}
