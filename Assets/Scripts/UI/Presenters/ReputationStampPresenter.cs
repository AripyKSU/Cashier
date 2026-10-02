using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정산 후 누적 명성에 맞는 도장을 선택하고 내려찍기 연출을 재생합니다.</summary>
public sealed class ReputationStampPresenter : MonoBehaviour
{
    [SerializeField] private Image stampImage;
    [SerializeField] private Sprite notoriousSprite;
    [SerializeField] private Sprite unpopularSprite;
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite popularSprite;
    [SerializeField] private Sprite excellentSprite;
    [SerializeField, Min(0.01f)] private float durationSeconds = 0.45f;
    [SerializeField, Min(1f)] private float startScale = 1.65f;
    [SerializeField, Min(0f)] private float impactRotationDegrees = 4f;

    private Tween presentationTween;
    private Vector3 restScale;
    private Quaternion restRotation;
    private int preparedDay = -1;
    private bool hasPrepared;
    private bool hasCompleted;
    private bool impactSoundPlayed;

    public event Action OnPresentationCompleted;

    private void Awake()
    {
        ValidateReferences();
        restScale = stampImage.rectTransform.localScale;
        restRotation = stampImage.rectTransform.localRotation;
    }

    public void ValidateReferences()
    {
        if (stampImage == null || notoriousSprite == null || unpopularSprite == null || neutralSprite == null
            || popularSprite == null || excellentSprite == null)
            throw new InvalidOperationException("ReputationStampPresenter: 도장 Image와 5개 등급 Sprite가 필요합니다.");
    }

    public void UpdateView(int day, int finalReputation)
    {
        ValidateReferences();
        if (day <= 0) throw new ArgumentOutOfRangeException(nameof(day));
        if (finalReputation < -100 || finalReputation > 100)
            throw new ArgumentOutOfRangeException(nameof(finalReputation), "명성은 -100~100 범위여야 합니다.");
        if (preparedDay == day && hasCompleted)
        {
            stampImage.sprite = selectSprite(finalReputation);
            stampImage.enabled = true;
            restoreTransform();
            return;
        }
        stopPresentation();
        preparedDay = day;
        hasPrepared = true;
        hasCompleted = false;
        stampImage.sprite = selectSprite(finalReputation);
        stampImage.enabled = false;
        restoreTransform();
    }

    /// <summary>준비된 도장의 트윈을 재시작합니다. 완료된 날짜는 다시 통지하지 않습니다.</summary>
    /// <exception cref="InvalidOperationException">도장 데이터 또는 필수 참조가 준비되지 않은 경우입니다.</exception>
    public void Present()
    {
        ValidateReferences();
        if (!hasPrepared) throw new InvalidOperationException("정산 명성 도장이 준비되지 않았습니다.");
        if (hasCompleted) return;
        stopPresentation();
        impactSoundPlayed = false;
        stampImage.enabled = true;
        playPresentation();
    }

    /// <summary>도장의 충격·반동을 하나의 트윈으로 진행하고 끝에서만 완료를 통지합니다.</summary>
    private void playPresentation()
    {
        RectTransform rect = stampImage.rectTransform;
        float progressValue = 0f;
        presentationTween = DOTween.To(() => progressValue, progress =>
        {
            progressValue = progress;
            float impactProgress = Mathf.Clamp01(progress / 0.58f);
            float scale = Mathf.Lerp(startScale, 1f, 1f - Mathf.Pow(1f - impactProgress, 3f));
            float shake = progress < 0.58f ? 0f : Mathf.Sin((progress - 0.58f) * Mathf.PI * 8f)
                * (1f - progress) * impactRotationDegrees;
            if (!impactSoundPlayed && progress >= 0.58f)
            {
                impactSoundPlayed = true;
                SoundManager.Instance?.PlaySfx(SoundKeys.ReputationStamp);
            }
            rect.localScale = restScale * scale;
            rect.localRotation = restRotation * Quaternion.Euler(0f, 0f, shake);
        }, 1f, durationSeconds).SetEase(Ease.Linear).SetUpdate(true).OnComplete(() =>
        {
            restoreTransform();
            presentationTween = null;
            if (hasCompleted) return;
            hasCompleted = true;
            OnPresentationCompleted?.Invoke();
        });
    }

    private Sprite selectSprite(int reputation)
    {
        if (reputation <= -61) return notoriousSprite;
        if (reputation <= -21) return unpopularSprite;
        if (reputation <= 20) return neutralSprite;
        if (reputation <= 60) return popularSprite;
        return excellentSprite;
    }

    private void restoreTransform()
    {
        if (stampImage == null) return;
        stampImage.rectTransform.localScale = restScale;
        stampImage.rectTransform.localRotation = restRotation;
    }

    /// <summary>완료 이벤트 없이 현재 표시 트윈만 중단합니다.</summary>
    private void stopPresentation()
    {
        presentationTween?.Kill();
        presentationTween = null;
    }

    private void OnDisable()
    {
        stopPresentation();
        restoreTransform();
    }
}
