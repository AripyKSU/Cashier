using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>독립 설비 패널의 행과 표시 수명만 관리하며 구매·닫기 의도를 Controller에 전달한다.</summary>
public sealed class FacilityShopPresenter : MonoBehaviour
{
    /// <summary>세션 현재 잔액 표시.</summary>
    [SerializeField] private TextMeshProUGUI balanceText;
    /// <summary>구매 처리 결과 안내.</summary>
    [SerializeField] private TextMeshProUGUI feedbackText;
    /// <summary>재사용할 설비 행의 부모.</summary>
    [SerializeField] private Transform content;
    /// <summary>독립 설비 행 프리팹.</summary>
    [SerializeField] private FacilityItemView itemPrefab;
    /// <summary>날짜 변경 없는 닫기 요청 버튼.</summary>
    [SerializeField] private Button closeButton;
    private readonly List<FacilityItemView> rows = new List<FacilityItemView>();
    private bool interactive = true;

    /// <summary>단일 설비 PK의 구매 요청.</summary>
    public event Action<uint> OnPurchaseRequested;
    /// <summary>날짜 변경 없이 패널만 닫는 요청.</summary>
    public event Action OnCloseRequested;

    /// <summary>열린 수명에만 버튼·행 요청을 구독한다.</summary>
    private void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(handleClose);
        foreach (var row in rows) row.PurchaseRequested += handlePurchase;
    }

    /// <summary>닫기와 씬 종료 시 요청 구독을 해제한다.</summary>
    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(handleClose);
        foreach (var row in rows) row.PurchaseRequested -= handlePurchase;
    }

    /// <summary>행을 필요한 만큼만 만들고 재사용한다. UI 갱신은 구매 요청을 발생시키지 않는다.</summary>
    /// <param name="data">현재 잔액·설비 스냅샷.</param><param name="feedback">결과 안내.</param>
    /// <exception cref="ArgumentNullException">스냅샷이 없음.</exception>
    public void UpdateView(FacilityShopViewData data, string feedback)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        balanceText.text = $"보유금  {data.CurrentBalance:N0} G";
        feedbackText.text = feedback ?? string.Empty;
        for (int i = 0; i < data.Items.Count; i++)
        {
            if (i == rows.Count)
            {
                var row = Instantiate(itemPrefab, content);
                rows.Add(row);
                if (isActiveAndEnabled) row.PurchaseRequested += handlePurchase;
            }
            rows[i].gameObject.SetActive(true);
            rows[i].UpdateView(data.Items[i], interactive);
        }
        for (int i = data.Items.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
    }

    /// <summary>구매 중 또는 기술 오류 이후 전체 구매·닫기 입력을 제어한다.</summary>
    /// <param name="enabled">구매 요청 허용 여부.</param><param name="canClose">닫기 허용 여부.</param>
    public void SetInteractionEnabled(bool enabled, bool canClose = true)
    {
        interactive = enabled;
        if (closeButton != null) closeButton.interactable = canClose;
        foreach (var row in rows) row.SetInteractionEnabled(enabled);
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
