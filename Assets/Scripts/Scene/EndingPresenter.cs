using System;
using System.Collections.Generic;
using System.Linq;
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
    private EndingPageData[] pages;
    private TextDataTable texts;
    private readonly Dictionary<uint, Sprite> backgrounds = new Dictionary<uint, Sprite>();
    private GameEndingResult result;
    private int pageIndex;
    private float nextInputTime;
    private bool isReady;
    private bool isLoading;

    /// <summary>페이지를 자동으로 넘기지 않고 표시 준비 후 입력을 받는다.</summary>
    private void Start()
    {
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
        nextButton.interactable = false;
        try
        {
            var session = GameSessionManager.Instance;
            if (session == null || !session.EndingResult.HasValue)
                throw new InvalidOperationException("게임 본편에서 확정된 엔딩을 열어 주세요.");
            result = session.EndingResult.Value;
            if (result.Kind != EndingKind.Good && result.Kind != EndingKind.Bad)
                throw new InvalidOperationException("굿·배드 엔딩 결과가 필요합니다.");
            var tables = DataTableManager.Instance;
            await tables.EnsureDataLoadedAsync().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
            texts = tables.GetDB<TextDataTable>(DataTableType.Text);
            pages = tables.GetDB<EndingPageDataTable>(DataTableType.EndingPage).Rows.Values
                .Where(row => row.Kind == result.Kind).OrderBy(row => row.PageOrder).ToArray();
            if (pages.Length == 0) throw new InvalidOperationException("엔딩 페이지가 없습니다.");
            var resources = tables.GetDB<ResourceDataTable>(DataTableType.Resource);
            foreach (uint idx in pages.Select(p => p.BackgroundResourceIdx).Distinct())
            {
                if (backgrounds.ContainsKey(idx)) continue;
                if (!resources.TryGetResource(idx, out var resource))
                    throw new InvalidOperationException($"Ending Resource FK={idx} 누락");
                var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(resource.Path, this.GetCancellationTokenOnDestroy());
                if (sprite == null) throw new InvalidOperationException($"Ending Sprite FK={idx} 누락");
                backgrounds.Add(idx, sprite);
            }
            isReady = true;
            pageIndex = 0;
            showPage();
        }
        catch (OperationCanceledException) when (this == null) { }
        catch (Exception exception)
        {
            if (this == null) return;
            Debug.LogError($"Ending load failed: {exception}");
            heading.text = "엔딩을 불러오지 못했습니다";
            dialogue.text = "결과는 보존되어 있습니다.\n다시 시도해 주세요.";
            nextButton.GetComponentInChildren<TMP_Text>().text = "다시 시도";
            nextButton.interactable = true;
        }
        finally { isLoading = false; }
    }

    /// <summary>입력 한 번에 한 페이지 진행하고 마지막에는 결과와 새 게임 선택을 표시한다.</summary>
    private void advance()
    {
        if (isLoading || !nextButton.gameObject.activeSelf || Time.unscaledTime < nextInputTime) return;
        nextInputTime = Time.unscaledTime + 0.2f;
        if (!isReady) { loadPagesAsync().Forget(); return; }
        if (++pageIndex < pages.Length) { showPage(); return; }
        heading.text = "CASHIER";
        speaker.text = result.Kind == EndingKind.Good ? "굿 엔딩" : "배드 엔딩";
        dialogue.text = $"{result.DisplayDay}일간의 영업이 끝났습니다.\n시민권 {(result.HasCitizenship ? "보유" : "미보유")} · 남은 돈 {result.Balance:N0} G\n명성 {result.Reputation} · 도덕성 {result.Morality:0.##}";
        pageIndicator.text = "플레이해 주셔서 감사합니다.";
        nextButton.gameObject.SetActive(false);
        newGameButton.gameObject.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    /// <summary>현재 페이지의 이미지와 약 세 줄의 문구를 표시한다.</summary>
    private void showPage()
    {
        var page = pages[pageIndex];
        heading.text = result.Kind == EndingKind.Good ? "문 안으로" : "문 밖에서";
        speaker.text = texts.Rows[page.SpeakerNameIdx].Text;
        dialogue.text = texts.Rows[page.TextIdx].Text;
        background.sprite = backgrounds[page.BackgroundResourceIdx];
        pageIndicator.text = $"{pageIndex + 1} / {pages.Length} · 임시 이미지";
        nextButton.GetComponentInChildren<TMP_Text>().text = pageIndex + 1 == pages.Length ? "마무리" : "다음";
        nextButton.interactable = true;
        nextInputTime = Time.unscaledTime + 0.2f;
        fadePageAsync().Forget();
    }

    /// <summary>짧은 표시 페이드만 적용하고 페이지는 자동으로 넘기지 않는다.</summary>
    /// <returns>페이드 완료.</returns>
    private async UniTask fadePageAsync()
    {
        pageGroup.alpha = 0;
        try
        {
            while (pageGroup.alpha < 1)
            {
                await UniTask.Yield(this.GetCancellationTokenOnDestroy());
                pageGroup.alpha = Mathf.Min(1, pageGroup.alpha + Time.unscaledDeltaTime / 0.15f);
            }
        }
        catch (OperationCanceledException) when (this == null) { }
    }
}
