using System;
using DG.Tweening;
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

    /// <summary>
    /// 글씨를 따라 움직이는 펜 쥔 손. 피벗이 펜 끝이어야 합니다. 비어 있으면 손 연출 없이 글씨만 씁니다.
    /// </summary>
    [SerializeField] private RectTransform penHand;

    /// <summary>손이 다음 글자 위치로 따라붙는 속도. 클수록 빠르게 스르륵 따라갑니다.</summary>
    [SerializeField, Min(1f)] private float penFollowSharpness = 22f;

    private Sequence typingTween;
    private bool hasCompleted;
    private bool isPenWriting;
    private Vector3 penTargetWorld;
    private float penWriteSeconds;

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
        playTyping();
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

    /// <summary>펜 끝이 마지막으로 쓴 글자를 부드럽게 따라가고, 쓰는 동안 살짝 흔들리게 합니다.</summary>
    private void LateUpdate()
    {
        if (penHand == null || !isPenWriting) return;
        penWriteSeconds += Time.unscaledDeltaTime;
        float follow = 1f - Mathf.Exp(-penFollowSharpness * Time.unscaledDeltaTime);
        // 글자를 쓰듯 펜 끝이 좌우로 짧게 왔다 갔다 하고, 획마다 살짝 위아래로 긁는다. 손 크기에 비례한다.
        float stroke = Mathf.Sin(penWriteSeconds * 14f) * penHand.rect.width * 0.035f;
        float scratch = Mathf.Sin(penWriteSeconds * 31f) * penHand.rect.height * 0.015f;
        Vector3 target = penTargetWorld + (penHand.right * stroke + penHand.up * scratch) * penHand.lossyScale.y;
        penHand.position = Vector3.Lerp(penHand.position, target, follow);
    }

    /// <summary>비활성화된 화면에서 타이핑 트윈이 남지 않도록 정리합니다.</summary>
    private void OnDisable()
    {
        stopTyping();
    }

    /// <summary>왼쪽 페이지 완료 후 실시간 간격을 두고 오른쪽 페이지를 공개합니다.</summary>
    private void playTyping()
    {
        typingTween = DOTween.Sequence().SetUpdate(true)
            .Append(revealPage(leftPageText))
            .AppendInterval(Mathf.Max(0f, pageIntervalSeconds))
            .Append(revealPage(rightPageText))
            .OnComplete(() => { typingTween = null; notifyCompleted(); });
    }

    /// <summary>레이아웃을 유지한 채 TMP의 표시 문자 수만 실시간으로 늘립니다.</summary>
    /// <param name="pageText">순차 공개할 TMP 영역입니다.</param>
    /// <returns>시퀀스에 포함할 한 페이지의 문자 공개 트윈입니다.</returns>
    private Tween revealPage(TextMeshProUGUI pageText)
    {
        pageText.ForceMeshUpdate();
        int characterCount = pageText.textInfo.characterCount;
        float visibleCharacters = 0f;
        return DOTween.To(() => visibleCharacters, value =>
            {
                visibleCharacters = value;
                int visibleCount = Mathf.Min(characterCount, Mathf.FloorToInt(value));
                pageText.maxVisibleCharacters = visibleCount;
                if (!isPenWriting) beginPen(pageText);
                movePenTo(pageText, Mathf.Max(1, visibleCount));
            }, characterCount, characterCount / Mathf.Max(1f, charactersPerSecond))
            .SetEase(Ease.Linear)
            .OnComplete(() => pageText.maxVisibleCharacters = characterCount);
    }

    /// <summary>페이지 첫 글자 위치에서 손을 보이게 합니다. 페이지가 바뀌면 손이 스르륵 옮겨 갑니다.</summary>
    /// <param name="pageText">이제 쓰기 시작할 페이지입니다.</param>
    private void beginPen(TextMeshProUGUI pageText)
    {
        if (penHand == null) return;
        bool wasHidden = !penHand.gameObject.activeSelf;
        penHand.gameObject.SetActive(true);
        movePenTo(pageText, 1);
        // 처음 나타날 때는 순간이동해 엉뚱한 곳에서 날아오지 않게 한다.
        if (wasHidden) penHand.position = penTargetWorld;
        isPenWriting = true;
    }

    /// <summary>방금 공개된 마지막 글자의 오른쪽 아래를 펜 목표로 정합니다. 공백과 줄바꿈은 건너뜁니다.</summary>
    /// <param name="pageText">쓰는 중인 페이지입니다.</param>
    /// <param name="visibleCount">현재 보이는 문자 수입니다.</param>
    private void movePenTo(TextMeshProUGUI pageText, int visibleCount)
    {
        if (penHand == null) return;
        TMP_TextInfo info = pageText.textInfo;
        for (int index = Mathf.Min(visibleCount, info.characterCount) - 1; index >= 0; index--)
        {
            TMP_CharacterInfo character = info.characterInfo[index];
            if (!character.isVisible) continue;
            penTargetWorld = pageText.rectTransform.TransformPoint(
                new Vector3(character.bottomRight.x, character.baseLine, 0f));
            return;
        }
    }

    /// <summary>쓰기가 끝나면 손을 치웁니다.</summary>
    private void hidePen()
    {
        isPenWriting = false;
        if (penHand != null) penHand.gameObject.SetActive(false);
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
        typingTween?.Kill();
        typingTween = null;
        hidePen();
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
        hidePen();
        SoundManager.Instance?.StopLoopSfx(SoundKeys.LedgerWrite);
        hasCompleted = true;
        this.OnPresentationCompleted?.Invoke();
    }
}
