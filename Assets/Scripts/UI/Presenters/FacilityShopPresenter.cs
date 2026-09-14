using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>독립 설비 패널의 행과 표시 수명만 관리하며 구매·닫기 의도를 Controller에 전달한다.</summary>
public sealed class FacilityShopPresenter : MonoBehaviour
{
    /// <summary>현재 단계 제목 표시.</summary>
    [SerializeField] private TextMeshProUGUI titleText;
    /// <summary>세션 현재 잔액 표시.</summary>
    [SerializeField] private TextMeshProUGUI balanceText;
    /// <summary>구매 후 실제 효과 적용 시점을 안내하는 텍스트.</summary>
    [SerializeField] private TextMeshProUGUI activationGuideText;
    /// <summary>구매 처리 결과 안내.</summary>
    [SerializeField] private TextMeshProUGUI feedbackText;
    /// <summary>재사용할 설비 행의 부모.</summary>
    [SerializeField] private Transform content;
    /// <summary>현재 단계별 panel.</summary>
    [SerializeField] private GameObject stage1Panel;
    [SerializeField] private GameObject stage2Panel;
    [SerializeField] private GameObject stage3Panel;
    /// <summary>현재 단계별 일반 설비 목록 부모.</summary>
    [SerializeField] private Transform stage1Content;
    [SerializeField] private Transform stage2Content;
    [SerializeField] private Transform stage3Content;
    /// <summary>현재 단계별 하단 진행 항목 부모.</summary>
    [SerializeField] private Transform stage1ProgressionContent;
    [SerializeField] private Transform stage2ProgressionContent;
    [SerializeField] private Transform stage3ProgressionContent;
    /// <summary>세 panel이 공유하는 ScrollRect.</summary>
    [SerializeField] private ScrollRect scrollRect;
    /// <summary>독립 설비 행 프리팹.</summary>
    [SerializeField] private FacilityItemView itemPrefab;
    /// <summary>날짜 변경 없는 닫기 요청 버튼.</summary>
    [SerializeField] private Button closeButton;
    private readonly List<FacilityItemView> rows = new List<FacilityItemView>();
    private FacilityItemView progressionRow;
    private GameObject[] stagePanels;
    private Transform[] stageContents;
    private Transform[] progressionContents;
    private bool interactive = true;

    /// <summary>세 단계 panel과 ScrollRect의 표시 구조를 준비합니다.</summary>
    private void Awake()
    {
        scrollRect = scrollRect ?? GetComponent<ScrollRect>();
        stageContents = new[] { stage1Content ?? content, stage2Content, stage3Content };
        stagePanels = new[] { stage1Panel, stage2Panel, stage3Panel };
        progressionContents = new[] { stage1ProgressionContent, stage2ProgressionContent, stage3ProgressionContent };
        ensureStageReferences();
        for (int index = 0; index < stagePanels.Length; index++)
            stagePanels[index].SetActive(index == 0 && gameObject.activeSelf);
    }

    /// <summary>단일 설비 PK의 구매 요청.</summary>
    public event Action<uint> OnPurchaseRequested;
    /// <summary>날짜 변경 없이 패널만 닫는 요청.</summary>
    public event Action OnCloseRequested;

    /// <summary>열린 수명에만 버튼·행 요청을 구독한다.</summary>
    private void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(handleClose);
        foreach (var row in rows) row.PurchaseRequested += handlePurchase;
        if (progressionRow != null) progressionRow.PurchaseRequested += handlePurchase;
    }

    /// <summary>닫기와 씬 종료 시 요청 구독을 해제한다.</summary>
    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(handleClose);
        foreach (var row in rows) row.PurchaseRequested -= handlePurchase;
        if (progressionRow != null) progressionRow.PurchaseRequested -= handlePurchase;
    }

    /// <summary>현재 단계 panel의 일반 목록과 하단 진행 항목을 표시한다.</summary>
    /// <param name="data">현재 잔액·단계·설비 스냅샷.</param><param name="feedback">결과 안내.</param>
    /// <exception cref="ArgumentNullException">스냅샷이 없음.</exception>
    public void UpdateView(FacilityShopViewData data, string feedback)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ensureStageReferences();
        int stageIndex = checked((int)data.CurrentStoreStage - 1);
        for (int index = 0; index < stagePanels.Length; index++)
            stagePanels[index].SetActive(index == stageIndex);
        if (titleText != null) titleText.text = $"설비 상점 · {data.CurrentStoreStage}단계";
        if (balanceText != null) balanceText.text = $"가게 단계 {data.CurrentStoreStage}  ·  보유금 {data.CurrentBalance:N0} G";
        if (activationGuideText != null)
            activationGuideText.text = "일반 설비는 다음 영업일부터 적용 · 시민권 구매 시 즉시 엔딩";
        if (feedbackText != null) feedbackText.text = feedback ?? string.Empty;
        Transform activeContent = stageContents[stageIndex];
        updateRegularRows(activeContent, data.RegularItems);
        updateProgressionRow(progressionContents[stageIndex], data.ProgressionItem);
        resizeContent(activeContent, data.RegularItems.Count, data.ProgressionItem.HasValue);
        if (scrollRect != null && activeContent is RectTransform activeRect)
        {
            scrollRect.content = activeRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>구매 중 또는 기술 오류 이후 전체 구매·닫기 입력을 제어한다.</summary>
    /// <param name="enabled">구매 요청 허용 여부.</param><param name="canClose">닫기 허용 여부.</param>
    public void SetInteractionEnabled(bool enabled, bool canClose = true)
    {
        interactive = enabled;
        if (closeButton != null) closeButton.interactable = canClose;
        foreach (var row in rows) row.SetInteractionEnabled(enabled);
        if (progressionRow != null) progressionRow.SetInteractionEnabled(enabled);
    }

    /// <summary>단계별 panel의 일반 행을 필요한 만큼 만들고 재사용합니다.</summary>
    /// <param name="parent">현재 단계 일반 목록 부모.</param>
    /// <param name="items">현재 단계 일반 설비 snapshot.</param>
    private void updateRegularRows(Transform parent, IReadOnlyList<FacilityItemViewData> items)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (index == rows.Count)
            {
                var row = Instantiate(itemPrefab, parent);
                rows.Add(row);
                if (isActiveAndEnabled) row.PurchaseRequested += handlePurchase;
            }
            rows[index].transform.SetParent(parent, false);
            rows[index].transform.SetSiblingIndex(index);
            rows[index].gameObject.SetActive(true);
            rows[index].UpdateView(items[index], interactive);
        }
        for (int index = items.Count; index < rows.Count; index++) rows[index].gameObject.SetActive(false);
    }

    /// <summary>현재 단계의 하단 진행 항목을 하나만 재사용합니다.</summary>
    /// <param name="parent">진행 항목 부모.</param>
    /// <param name="item">진행 항목 snapshot 또는 null.</param>
    private void updateProgressionRow(Transform parent, FacilityItemViewData? item)
    {
        if (!item.HasValue)
        {
            if (progressionRow != null) progressionRow.gameObject.SetActive(false);
            return;
        }
        if (progressionRow == null)
        {
            progressionRow = Instantiate(itemPrefab, parent);
            if (isActiveAndEnabled) progressionRow.PurchaseRequested += handlePurchase;
        }
        progressionRow.transform.SetParent(parent, false);
        progressionRow.gameObject.SetActive(true);
        progressionRow.UpdateView(item.Value, interactive);
    }

    /// <summary>스크롤 content가 일반 행과 진행 행을 모두 감싸도록 높이를 갱신합니다.</summary>
    /// <param name="contentTransform">현재 단계 panel.</param>
    /// <param name="regularCount">일반 행 수.</param>
    /// <param name="hasProgression">진행 행 존재 여부.</param>
    private void resizeContent(Transform contentTransform, int regularCount, bool hasProgression)
    {
        if (!(contentTransform is RectTransform rect)) return;
        float height = Mathf.Max(480f, regularCount * 96f + (hasProgression ? 102f : 0f) + 12f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
    }

    /// <summary>Prefab의 직렬화 참조를 확인하고 누락된 panel은 런타임 구조로 보완합니다.</summary>
    private void ensureStageReferences()
    {
        if (stageContents == null)
        {
            stageContents = new[] { stage1Content ?? content, stage2Content, stage3Content };
            stagePanels = new[] { stage1Panel, stage2Panel, stage3Panel };
            progressionContents = new[] { stage1ProgressionContent, stage2ProgressionContent, stage3ProgressionContent };
        }
        if (stageContents[0] == null)
            throw new InvalidOperationException("1단계 설비 목록 content 참조가 누락되었습니다.");
        stagePanels[0] = stagePanels[0] ?? stageContents[0].gameObject;
        for (int index = 1; index < stageContents.Length; index++)
        {
            if (stageContents[index] == null)
                stagePanels[index] = createRuntimeStagePanel(index + 1, out stageContents[index]);
            else
                stagePanels[index] = stagePanels[index] ?? stageContents[index].gameObject;
        }
        for (int index = 0; index < progressionContents.Length; index++)
            progressionContents[index] = progressionContents[index] ?? findOrCreateLayoutChild(stageContents[index], "ProgressionArea");
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
    }

    /// <summary>기존 panel에 진행 영역을 만들거나 직렬화된 영역을 반환합니다.</summary>
    /// <param name="parent">진행 영역의 부모 panel.</param>
    /// <param name="name">생성할 영역 이름.</param>
    /// <returns>VerticalLayoutGroup이 연결된 진행 영역.</returns>
    private Transform findOrCreateLayoutChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var childObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            child = childObject.transform;
            child.SetParent(parent, false);
        }
        var layout = child.GetComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;
        return child;
    }

    /// <summary>Prefab에 단계 panel이 없을 때 현재 ScrollRect viewport 아래에 하나를 만듭니다.</summary>
    /// <param name="stage">생성할 가게 단계.</param>
    /// <param name="contentTransform">생성한 panel transform.</param>
    /// <returns>생성한 panel GameObject.</returns>
    private GameObject createRuntimeStagePanel(int stage, out Transform contentTransform)
    {
        Transform parent = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport : transform;
        var panel = new GameObject($"Stage{stage}Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 480f);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;
        contentTransform = panel.transform;
        findOrCreateLayoutChild(contentTransform, "ProgressionArea");
        panel.SetActive(false);
        return panel;
    }

    /// <summary>표시 행의 PK를 변경 없이 전달한다.</summary>
    /// <param name="facilityIdx">요청한 설비.</param>
    private void handlePurchase(uint facilityIdx)
    {
        if (interactive && isActiveAndEnabled) OnPurchaseRequested?.Invoke(facilityIdx);
    }

    /// <summary>닫기 버튼이 활성일 때만 닫기 의도를 전달한다.</summary>
    private void handleClose()
    {
        if (isActiveAndEnabled && closeButton.interactable) OnCloseRequested?.Invoke();
    }
}
