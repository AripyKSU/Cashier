using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>가계부의 왼쪽 페이지를 먼저 쓴 뒤 오른쪽 페이지를 이어서 표시합니다.</summary>
public sealed class DailySettlementLedgerView : MonoBehaviour
{
    /// <summary>왼쪽 영업 결산을 표시하는 TMP 영역입니다.</summary>
    [SerializeField] private TextMeshProUGUI leftPageText;

    /// <summary>오른쪽 운영 기록을 표시하는 TMP 영역입니다.</summary>
    [SerializeField] private TextMeshProUGUI rightPageText;

    /// <summary>실시간 1초당 공개할 TMP 문자 수입니다.</summary>
    [SerializeField, Min(1f)] private float charactersPerSecond = 35f;

    /// <summary>왼쪽 페이지 완료 후 오른쪽 페이지를 쓰기 전 대기 시간입니다.</summary>
    [SerializeField, Min(0f)] private float pageIntervalSeconds = 0.25f;

    private Coroutine typingCoroutine;
    private bool hasCompleted;

    /// <summary>양쪽 페이지의 모든 문자가 공개됐을 때 한 번 발생합니다.</summary>
    public event Action OnPresentationCompleted;

    /// <summary>완성된 두 페이지 문구를 초기화하고 왼쪽부터 순서대로 공개합니다.</summary>
    /// <param name="ledgerText">Formatter가 만든 양쪽 페이지 문구입니다.</param>
    public void Present(DailySettlementLedgerText ledgerText)
    {
        ensureReferences();
        stopTyping();
        hasCompleted = false;
        setPageText(leftPageText, ledgerText.LeftPage);
        setPageText(rightPageText, ledgerText.RightPage);
        SoundManager.Instance?.PlayLoopSfx(SoundKeys.LedgerWrite);
        typingCoroutine = StartCoroutine(playTyping());
    }

    /// <summary>진행 중인 연출을 중단하고 양쪽 페이지의 전체 문구를 즉시 표시합니다.</summary>
    public void CompleteImmediately()
    {
        ensureReferences();
        stopTyping();
        leftPageText.maxVisibleCharacters = int.MaxValue;
        rightPageText.maxVisibleCharacters = int.MaxValue;
        notifyCompleted();
    }

    /// <summary>완료된 가계부의 갱신된 문구를 타이핑과 완료 이벤트 없이 즉시 표시합니다.</summary>
    /// <param name="ledgerText">같은 날짜의 최신 현재 보유금이 반영된 양쪽 페이지 문구입니다.</param>
    public void RefreshCompleted(DailySettlementLedgerText ledgerText)
    {
        ensureReferences();
        stopTyping();
        setPageText(leftPageText, ledgerText.LeftPage);
        setPageText(rightPageText, ledgerText.RightPage);
        leftPageText.maxVisibleCharacters = int.MaxValue;
        rightPageText.maxVisibleCharacters = int.MaxValue;
        hasCompleted = true;
    }

    /// <summary>진행 중인 연출과 양쪽 페이지 문구를 모두 초기화합니다.</summary>
    public void Clear()
    {
        stopTyping();
        hasCompleted = false;
        if (leftPageText != null) leftPageText.text = string.Empty;
        if (rightPageText != null) rightPageText.text = string.Empty;
    }

    /// <summary>비활성화된 화면에서 타이핑 Coroutine이 남지 않도록 정리합니다.</summary>
    private void OnDisable()
    {
        stopTyping();
    }

    /// <summary>왼쪽 페이지 완료 후 실시간 간격을 두고 오른쪽 페이지를 공개합니다.</summary>
    /// <returns>페이지 타이핑 Coroutine입니다.</returns>
    private IEnumerator playTyping()
    {
        yield return revealPage(leftPageText);

        float remainingSeconds = pageIntervalSeconds;
        while (remainingSeconds > 0f)
        {
            remainingSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return revealPage(rightPageText);
        typingCoroutine = null;
        notifyCompleted();
    }

    /// <summary>레이아웃을 유지한 채 TMP의 표시 문자 수만 실시간으로 늘립니다.</summary>
    /// <param name="pageText">순차 공개할 TMP 영역입니다.</param>
    /// <returns>한 페이지의 타이핑 Coroutine입니다.</returns>
    private IEnumerator revealPage(TextMeshProUGUI pageText)
    {
        pageText.ForceMeshUpdate();
        int characterCount = pageText.textInfo.characterCount;
        float visibleCharacters = 0f;
        while (pageText.maxVisibleCharacters < characterCount)
        {
            visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
            pageText.maxVisibleCharacters = Mathf.Min(characterCount, Mathf.FloorToInt(visibleCharacters));
            yield return null;
        }
    }

    /// <summary>전체 문구를 먼저 배치하고 화면에 보이는 문자 수만 초기화합니다.</summary>
    /// <param name="pageText">초기화할 TMP 영역입니다.</param>
    /// <param name="content">Formatter가 만든 완성 문자열입니다.</param>
    private static void setPageText(TextMeshProUGUI pageText, string content)
    {
        pageText.text = content ?? string.Empty;
        pageText.maxVisibleCharacters = 0;
        pageText.ForceMeshUpdate();
    }

    /// <summary>현재 실행 중인 타이핑 연출만 안전하게 중단합니다.</summary>
    private void stopTyping()
    {
        SoundManager.Instance?.StopLoopSfx(SoundKeys.LedgerWrite);
        if (typingCoroutine == null) return;
        StopCoroutine(typingCoroutine);
        typingCoroutine = null;
    }

    /// <summary>실행 전에 두 TMP 직렬화 참조가 준비됐는지 확인합니다.</summary>
    /// <exception cref="MissingReferenceException">왼쪽 또는 오른쪽 TMP가 연결되지 않은 경우 발생합니다.</exception>
    private void ensureReferences()
    {
        if (leftPageText == null || rightPageText == null)
            throw new MissingReferenceException("가계부의 왼쪽·오른쪽 TextMeshProUGUI 참조가 필요합니다.");
    }

    /// <summary>한 번의 표시 요청에서 완료 이벤트가 중복 발생하지 않게 전달합니다.</summary>
    private void notifyCompleted()
    {
        if (hasCompleted) return;
        SoundManager.Instance?.StopLoopSfx(SoundKeys.LedgerWrite);
        hasCompleted = true;
        this.OnPresentationCompleted?.Invoke();
    }
}
