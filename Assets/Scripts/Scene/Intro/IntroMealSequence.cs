using System;
using System.Collections;
using UnityEngine;

/// <summary>인트로 전용 고정 정산 표시와 라면 환경음만 담당한다. 플레이 자금에는 접근하지 않는다.</summary>
public sealed class IntroMealSequence : MonoBehaviour
{
    [Header("Settlement rows: revenue, costs x3, divider, net, caption")]
    [SerializeField] private CanvasGroup settlement;
    [SerializeField] private CanvasGroup[] rows;
    [SerializeField] private float[] revealSeconds = { .3f, .8f, 1.3f, 1.8f, 2.3f, 2.6f, 3.3f };
    [SerializeField] private AudioClip[] rowClips;
    [SerializeField] private float[] rowVolumes = { .28f, .3f, .3f, .3f, 0f, .28f, 0f };
    [SerializeField, Min(0f)] private float captionHoldSeconds = 1.5f;
    [SerializeField, Min(0f)] private float simmerPrelapSeconds = .7f;
    [SerializeField, Min(0f)] private float textFadeSeconds = .3f;
    [Header("Existing intro audio / mixer routed sources")]
    [SerializeField] private AudioSource effects;
    [SerializeField] private AudioSource simmer;
    [SerializeField] private AudioSource nightWind;
    [SerializeField, Range(0f, 1f)] private float simmerVolume = .13f;
    [SerializeField, Range(0f, 1f)] private float nightWindVolume = .045f;
    [Header("Optional natural eating sounds; no substitute when missing")]
    [SerializeField] private AudioClip noodleSlurp;
    [SerializeField] private AudioClip dishTick;
    [SerializeField, Range(0f, 1f)] private float eatingVolume = .3f;
    [SerializeField, Min(0f)] private float dishDelaySeconds = .15f;
    private bool paused;

    /// <summary>시작 전 표시와 음원을 정리한다.</summary>
    private void Awake() => ResetPresentation();

    /// <summary>비활성화 시 예약된 작업이 남지 않도록 정리한다.</summary>
    private void OnDisable() => ResetPresentation();

    /// <summary>정산 자동 진행과 라면 소리의 프리랩을 순서대로 재생한다.</summary>
    /// <param name="delta">확인창이 열리면 0을 반환하는 실제 시간 증분.</param>
    /// <returns>전체 정산 연출.</returns>
    public IEnumerator PlaySettlement(Func<float> delta)
    {
        ResetPresentation();
        settlement.gameObject.SetActive(true);
        settlement.alpha = 1f;
        float elapsed = 0f;
        int nextRow = 0;
        bool prelapStarted = false;
        float holdEnd = revealSeconds[revealSeconds.Length - 1] + captionHoldSeconds;
        float prelapAt = holdEnd - simmerPrelapSeconds;
        while (true)
        {
            while (paused) yield return null;
            while (nextRow < rows.Length && elapsed >= revealSeconds[nextRow])
            {
                rows[nextRow].alpha = 1f;
                if (rowClips[nextRow] != null) effects.PlayOneShot(rowClips[nextRow], rowVolumes[nextRow]);
                nextRow++;
            }
            if (!prelapStarted && elapsed >= prelapAt)
            {
                simmer.volume = simmerVolume;
                nightWind.volume = nightWindVolume;
                simmer.Play();
                nightWind.Play();
                prelapStarted = true;
            }
            if (elapsed >= holdEnd) break;
            yield return null;
            elapsed += delta();
        }
        float fade = 0f;
        while (fade < textFadeSeconds)
        {
            fade += delta();
            settlement.alpha = 1f - Mathf.Clamp01(fade / textFadeSeconds);
            yield return null;
        }
        settlement.gameObject.SetActive(false);
    }

    /// <summary>마지막 하루 대사 완성 시 한 번 호출되는 식사 효과음.</summary>
    public void PlayEatingSound()
    {
        if (noodleSlurp != null) StartCoroutine(playEating());
    }

    /// <summary>확인창 동안 환경음과 식사 지연을 정지/재개한다.</summary>
    /// <param name="value">정지 여부.</param>
    public void SetPaused(bool value)
    {
        paused = value;
        if (value) { simmer.Pause(); nightWind.Pause(); }
        else { simmer.UnPause(); nightWind.UnPause(); }
    }

    /// <summary>Scene05 암전 후 버너를 제외한 식사 음원만 종료한다.</summary>
    public void StopRoom()
    {
        StopAllCoroutines();
        if (simmer != null) simmer.Stop();
        if (nightWind != null) nightWind.Stop();
    }

    /// <summary>스킵·재시작·비활성화에서 임시 표시와 모든 예약을 정리한다.</summary>
    public void ResetPresentation()
    {
        StopRoom();
        paused = false;
        if (settlement != null) settlement.gameObject.SetActive(false);
        if (rows != null) foreach (CanvasGroup row in rows) if (row != null) row.alpha = 0f;
    }

    /// <summary>먹는 소리가 실제로 있을 때만 재생하고 뒤에 선택적인 식기음을 잇는다.</summary>
    /// <returns>식기음 지연.</returns>
    private IEnumerator playEating()
    {
        effects.PlayOneShot(noodleSlurp, eatingVolume);
        float remaining = noodleSlurp.length + dishDelaySeconds;
        while (remaining > 0f)
        {
            if (!paused) remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        while (paused) yield return null;
        if (dishTick != null) effects.PlayOneShot(dishTick, eatingVolume);
    }
}
