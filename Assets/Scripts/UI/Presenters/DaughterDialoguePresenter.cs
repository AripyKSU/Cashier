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

    private Tween presentationTween;
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
            speechBubble.enabled = true;
            dialogue.enabled = true;
            return;
        }

        stopPresentation();
        restorePortrait();
        preparedViewData = viewData;
        presentedDay = viewData.Day;
        hasPrepared = true;
        hasCompleted = false;
        dialogue.text = viewData.Text;
        dialogue.maxVisibleCharacters = 0;
        speechBubble.enabled = false;
        dialogue.enabled = false;
        cachePortraitRestTransform();
    }

    /// <summary>준비된 딸과 말풍선을 공개하고 초당 24자로 대사를 출력합니다.</summary>
    /// <exception cref="InvalidOperationException">표시할 대사가 준비되지 않은 경우.</exception>
    public void Present()
    {
        ValidateReferences();
        if (!hasPrepared)
            throw new InvalidOperationException("정산 딸 대사가 준비되지 않았습니다.");
        if (hasCompleted) return;
        stopPresentation();
        speechBubble.enabled = true;
        dialogue.enabled = true;
        dialogue.text = preparedViewData.Text;
        dialogue.maxVisibleCharacters = 0;
        dialogue.ForceMeshUpdate(true, true);
        SoundManager.Instance?.PlaySfxForDuration(
            SoundKeys.DialogueVoice,
            DialogueVoiceDurationSeconds);
        playPresentation();
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
        if (duration <= 0f) { completePresentation(); return; }
        presentationTween = DOTween.To(() => elapsedSeconds, value =>
            {
                elapsedSeconds = value;
                dialogue.maxVisibleCharacters = Mathf.Min(characterCount,
                    Mathf.FloorToInt(value * charactersPerSecond));
                updatePortraitNod(value);
            }, duration, duration).SetEase(Ease.Linear).SetUpdate(true)
            .OnComplete(() =>
            {
                dialogue.maxVisibleCharacters = characterCount;
                completePresentation();
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
    }
}
