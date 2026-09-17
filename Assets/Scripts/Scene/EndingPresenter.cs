using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>세션의 동결된 결말과 CSV 페이지를 표시한다. 구매·정산·결말을 다시 판정하지 않는다.</summary>
public sealed class EndingPresenter : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI heading;
    [SerializeField] private TextMeshProUGUI speaker;
    [SerializeField] private TextMeshProUGUI dialogue;
    [SerializeField] private TextMeshProUGUI pageIndicator;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private CanvasGroup pageGroup;
    [SerializeField] private Image blackCover;
    [SerializeField] private Image dialoguePanelBackground;
    private EndingPageData[] pages;
    private TextDataTable texts;
    private readonly Dictionary<uint, Sprite> backgrounds = new Dictionary<uint, Sprite>();
    private GameEndingResult result;
    private int pageIndex;
    private float nextInputTime;
    private bool isReady;
    private bool isLoading;
    private uint? currentBackground;
    private uint? currentSfx;
    private CancellationTokenSource lifetime;
    private bool started;
    private bool isPageDelaying;

    /// <summary>활성 수명에 묶인 비동기 표시 취소 토큰을 준비한다.</summary>
    private void OnEnable()
    {
        lifetime = new CancellationTokenSource();
        if (started) loadPagesAsync().Forget();
    }

    /// <summary>비활성화 뒤 늦은 로드·페이드가 화면을 덮지 않도록 취소한다.</summary>
    private void OnDisable()
    {
        SoundManager.Instance?.StopBgm();
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        isLoading = false;
        isReady = false;
        isPageDelaying = false;
        stopPageSfx();
        currentBackground = null;
    }

    /// <summary>페이지를 자동으로 넘기지 않고 표시 준비 후 입력을 받는다.</summary>
    private void Start()
    {
        started = true;
        nextButton.onClick.AddListener(advance);
        newGameButton.gameObject.SetActive(false);
        loadPagesAsync().Forget();
    }

    /// <summary>마우스와 같은 경로로 Enter를 처리하고 중복 넘김을 막는다.</summary>
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)) advance();
#endif
    }

    /// <summary>기존 로더에서 페이지·문구·이미지를 준비한 뒤 표시한다.</summary>
    /// <returns>표시 시작 또는 재시도 가능한 오류 안내 완료.</returns>
    private async UniTask loadPagesAsync()
    {
        if (isLoading) return;
        isLoading = true;
        CancellationToken token = lifetime.Token;
        nextButton.gameObject.SetActive(true);
        newGameButton.gameObject.SetActive(false);
        pageIndicator.gameObject.SetActive(true);
        nextButton.interactable = false;
        try
        {
            var session = GameSessionManager.Instance;
            if (session == null || !session.EndingResult.HasValue)
                throw new InvalidOperationException("게임 본편에서 확정된 엔딩을 열어 주세요.");
            result = session.EndingResult.Value;
            if (result.Kind <= EndingKind.None || result.Kind >= EndingKind.EndingKind_End)
                throw new InvalidOperationException("확정된 엔딩 결과가 필요합니다.");
            var tables = DataTableManager.Instance;
            await tables.EnsureDataLoadedAsync().AttachExternalCancellation(token);
            texts = tables.GetDB<TextDataTable>(DataTableType.Text);
            pages = tables.GetDB<EndingPageDataTable>(DataTableType.EndingPage).Rows.Values
                .Where(row => row.Kind == result.Kind).OrderBy(row => row.PageOrder).ToArray();
            if (pages.Length == 0) throw new InvalidOperationException("엔딩 페이지가 없습니다.");
            var resources = tables.GetDB<ResourceDataTable>(DataTableType.Resource);
            foreach (uint idx in pages.Where(p => p.BackgroundResourceIdx.HasValue)
                .Select(p => p.BackgroundResourceIdx.Value).Distinct())
            {
                if (backgrounds.ContainsKey(idx)) continue;
                if (!resources.TryGetResource(idx, out var resource))
                    throw new InvalidOperationException($"Ending Resource FK={idx} 누락");
                var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(resource.Path, token);
                if (sprite == null) throw new InvalidOperationException($"Ending Sprite FK={idx} 누락");
                backgrounds.Add(idx, sprite);
            }
            var sounds = SoundManager.Instance;
            foreach (uint idx in pages.Where(page => page.SfxResourceIdx.HasValue).Select(page => page.SfxResourceIdx.Value).Distinct())
                if (sounds == null || !sounds.CachedClips.TryGetValue(idx, out AudioClip clip) || clip == null)
                    throw new InvalidOperationException($"Ending SFX FK={idx}가 SoundManager cache에 없습니다.");
            if (sounds == null || !sounds.CachedClips.ContainsKey(GetEndingBgmResourceIdx(result.Kind)))
                throw new InvalidOperationException("Ending BGM이 SoundManager cache에 없습니다.");
            sounds.PlayBgm(GetEndingBgmResourceIdx(result.Kind));
            isReady = true;
            pageIndex = 0;
            showPageAsync().Forget();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (this == null || token.IsCancellationRequested) return;
            Debug.LogError($"Ending load failed: {exception}");
            pageGroup.alpha = 1;
            heading.gameObject.SetActive(true);
            dialogue.gameObject.SetActive(true);
            heading.text = "엔딩을 불러오지 못했습니다";
            dialogue.text = "결과는 보존되어 있습니다.\n다시 시도해 주세요.";
            nextButton.GetComponentInChildren<TMP_Text>().text = "다시 시도";
            nextButton.interactable = true;
        }
        finally { if (!token.IsCancellationRequested) isLoading = false; }
    }

    /// <summary>입력 한 번에 한 페이지 진행하고 마지막에는 결과와 새 게임 선택을 표시한다.</summary>
    private void advance()
    {
        if (!isActiveAndEnabled || isLoading || isPageDelaying || !nextButton.gameObject.activeSelf ||
            !nextButton.interactable || Time.unscaledTime < nextInputTime) return;
        nextInputTime = Time.unscaledTime + 0.2f;
        if (!isReady) { loadPagesAsync().Forget(); return; }
        if (++pageIndex < pages.Length)
        {
            stopPageSfx();
            if (!pages[pageIndex].BackgroundResourceIdx.HasValue && currentBackground.HasValue) showBlackPageAsync().Forget();
            else showPageAsync().Forget();
            return;
        }
        nextButton.gameObject.SetActive(false);
        newGameButton.gameObject.SetActive(true);
        SoundManager.Instance?.StopBgm();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    /// <summary>현재 페이지의 이미지와 약 세 줄의 문구를 표시한다.</summary>
    private async UniTask showPageAsync()
    {
        var page = pages[pageIndex];
        CancellationToken token = lifetime.Token;
        isPageDelaying = page.DelaySecond.GetValueOrDefault() > 0;
        nextButton.interactable = false;
        heading.text = string.Empty;
        heading.gameObject.SetActive(false);
        speaker.gameObject.SetActive(!isPageDelaying && page.SpeakerNameIdx.HasValue);
        speaker.text = page.SpeakerNameIdx.HasValue ? texts.Rows[page.SpeakerNameIdx.Value].Text : string.Empty;
        dialogue.text = page.TextIdx.HasValue ? texts.Rows[page.TextIdx.Value].Text : string.Empty;
        dialogue.gameObject.SetActive(!isPageDelaying && page.TextIdx.HasValue);
        setPanelBackgroundVisible(!isPageDelaying && page.TextIdx.HasValue);
        pageIndicator.gameObject.SetActive(!isPageDelaying);
        nextButton.gameObject.SetActive(true);
        bool startedFade = false;
        if (page.BackgroundResourceIdx.HasValue)
        {
            blackCover.color = new Color(0, 0, 0, 0);
            bool changed = currentBackground != page.BackgroundResourceIdx;
            background.sprite = backgrounds[page.BackgroundResourceIdx.Value];
            background.color = Color.white;
            currentBackground = page.BackgroundResourceIdx;
            if (changed)
            {
                startedFade = true;
                fadePageAsync().Forget();
            }
        }
        else
        {
            background.sprite = null;
            background.color = Color.black;
            currentBackground = null;
            blackCover.color = Color.black;
        }

        if (isPageDelaying)
        {
            float elapsed = 0;
            try
            {
                while (elapsed < page.DelaySecond.Value)
                {
                    await UniTask.Yield(token);
                    elapsed += Time.unscaledDeltaTime;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            isPageDelaying = false;
            speaker.gameObject.SetActive(page.SpeakerNameIdx.HasValue);
            dialogue.gameObject.SetActive(page.TextIdx.HasValue);
            setPanelBackgroundVisible(page.TextIdx.HasValue);
            pageIndicator.gameObject.SetActive(true);
            if (!startedFade || pageGroup.alpha >= 1) nextButton.interactable = true;
        }

        pageIndicator.text = $"{pageIndex + 1} / {pages.Length}";
        nextButton.GetComponentInChildren<TMP_Text>().text = pageIndex + 1 == pages.Length ? "마무리" : "다음";
        if (!startedFade && !isPageDelaying) nextButton.interactable = true;
        nextInputTime = Time.unscaledTime + 0.2f;
        playPageSfx(page);
        if (!page.BackgroundResourceIdx.HasValue && pageIndex + 1 == pages.Length) finishFinalPage();
    }

    /// <summary>이미지에서 검은 화면으로 전환한 뒤 해당 페이지의 대사·효과음을 시작한다.</summary>
    /// <returns>0.6초 암전과 페이지 표시 완료.</returns>
    private async UniTask showBlackPageAsync()
    {
        isLoading = true;
        nextButton.interactable = false;
        nextButton.gameObject.SetActive(false);
        CancellationToken token = lifetime.Token;
        try
        {
            heading.gameObject.SetActive(false);
            speaker.gameObject.SetActive(false);
            dialogue.gameObject.SetActive(false);
            pageIndicator.gameObject.SetActive(false);
            setPanelBackgroundVisible(false);
            float elapsed = 0;
            while (elapsed < 0.6f)
            {
                await UniTask.Yield(token);
                elapsed += Time.unscaledDeltaTime;
                blackCover.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / 0.6f));
            }
            background.sprite = null;
            background.color = Color.black;
            currentBackground = null;
            await showPageAsync();
            blackCover.color = Color.black;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { if (this != null && !token.IsCancellationRequested) isLoading = false; }
    }

    /// <summary>최종 CSV 문구를 유지한 채 입력을 새 게임 선택으로 전환한다.</summary>
    private void finishFinalPage()
    {
        setPanelBackgroundVisible(false);
        pageIndicator.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        newGameButton.gameObject.SetActive(true);
        SoundManager.Instance?.StopBgm();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    /// <summary>동결된 엔딩 종류를 해당 엔딩 화면의 BGM 리소스로 변환한다.</summary>
    /// <param name="kind">검증된 엔딩 종류.</param>
    /// <returns>Good은 GoodEndingBgm, 나머지 세 종료 종류는 BadEndingBgm.</returns>
    /// <exception cref="ArgumentOutOfRangeException">유효한 종료 종류가 아닌 경우 발생합니다.</exception>
    internal static uint GetEndingBgmResourceIdx(EndingKind kind) => kind switch
    {
        EndingKind.Good => SoundKeys.GoodEndingBgm,
        EndingKind.GameOver or EndingKind.Bad or EndingKind.CitizenshipNegative => SoundKeys.BadEndingBgm,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "유효한 엔딩 종류가 필요합니다.")
    };

    /// <summary>이전 페이지 효과음을 정리하고 현재 페이지 효과음을 한 번 시작한다.</summary>
    /// <param name="page">표시가 완료된 페이지.</param>
    private void playPageSfx(EndingPageData page)
    {
        stopPageSfx();
        if (!page.SfxResourceIdx.HasValue) return;
        currentSfx = page.SfxResourceIdx;
        SoundManager.Instance.PlaySfxUntilStopped(currentSfx.Value);
    }

    /// <summary>이 Presenter가 시작한 엔딩 페이지 효과음만 정지한다.</summary>
    private void stopPageSfx()
    {
        if (!currentSfx.HasValue) return;
        SoundManager.Instance?.StopSfxForDuration(currentSfx.Value);
        currentSfx = null;
    }

    /// <summary>설정된 색상·알파를 보존하고 대사 패널 배경만 표시하거나 숨긴다.</summary>
    /// <param name="visible">대사 패널 배경 표시 여부.</param>
    private void setPanelBackgroundVisible(bool visible)
    {
        dialoguePanelBackground.enabled = visible;
    }

    /// <summary>짧은 표시 페이드만 적용하고 페이지는 자동으로 넘기지 않는다.</summary>
    /// <returns>페이드 완료.</returns>
    private async UniTask fadePageAsync()
    {
        CancellationToken token = lifetime.Token;
        nextButton.interactable = false;
        pageGroup.alpha = 0;
        try
        {
            while (pageGroup.alpha < 1)
            {
                await UniTask.Yield(token);
                pageGroup.alpha = Mathf.Min(1, pageGroup.alpha + Time.unscaledDeltaTime / 0.15f);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            if (this != null && !token.IsCancellationRequested && isReady && !isLoading && !isPageDelaying)
                nextButton.interactable = true;
        }
    }
}
