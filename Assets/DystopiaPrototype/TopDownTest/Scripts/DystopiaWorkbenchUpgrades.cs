using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 거래 세션으로 설비를 구매하고 체크아웃 정렬 및 품목별 미처리 수량만 표시합니다.</summary>
public sealed class DystopiaWorkbenchUpgrades : MonoBehaviour
{
    /// <summary>물품·거래의 기존 소유자입니다. 항상 활성인 설비 오브젝트에서 연결합니다.</summary>
    [SerializeField] private DystopiaTopDownTest checkout;
    /// <summary>원 단위 신규 구매가입니다. -1은 미설정으로 구매를 차단하고 0은 무료입니다.</summary>
    [SerializeField, Min(-1)] private int sortingTrayPrice = -1, quantityCounterPrice = -1;
    /// <summary>기존 정산 화면 밖에서도 유지되는 수동 배치 구매 버튼입니다.</summary>
    [SerializeField] private Button sortingTrayButton, quantityCounterButton;
    /// <summary>구매 가능 여부와 정렬 실패 이유를 표시할 선택적 텍스트입니다.</summary>
    [SerializeField] private Text statusText;
    /// <summary>선택적으로 오른쪽 판매 영역 내부를 더 제한합니다. 비워 두면 기존 판매 영역을 사용합니다. 연결 시 Trigger여야 합니다.</summary>
    [SerializeField] private BoxCollider2D sortingArea;
    /// <summary>물품 외곽 사이의 최소 월드 간격입니다.</summary>
    [SerializeField, Min(.01f)] private float spacing = .08f;
    /// <summary>계산기 외에 정렬 물품이 가리면 안 되는 수동 배치 UI 영역입니다.</summary>
    [SerializeField] private RectTransform[] protectedUi = Array.Empty<RectTransform>();
    /// <summary>고정 상품 ID별 수동 배치 슬롯입니다. 위치·크기는 코드에서 변경하지 않습니다.</summary>
    [SerializeField] private QuantityRow[] quantityRows = Array.Empty<QuantityRow>();

    /// <summary>Editor 실행 중 구매 없이 정렬을 시험합니다. 구매 상태와 빌드에는 영향을 주지 않습니다.</summary>
    [Header("Editor 테스트 — 구매 상태 변경 없음")]
    [SerializeField, InspectorName("테스트용 정렬 활성화")] private bool testSortingTray;
    /// <summary>Editor 실행 중 구매 없이 수량 표시를 시험합니다. 기존 수량 UI 연결은 필요합니다.</summary>
    [SerializeField, InspectorName("테스트용 계수기 활성화")] private bool testQuantityCounter;

    /// <summary>최근 정렬 결과를 테스트 화면에 표시합니다.</summary>
    public string SortingStatus { get; private set; } = "체크아웃에 물품을 놓으면 정렬합니다.";

    // 생성·처리 이벤트에서만 수량을 다시 집계합니다. 비활성 흡입 보관품도 거래 미처리품입니다.
    private readonly Dictionary<DystopiaProductId, int> quantities = new Dictionary<DystopiaProductId, int>();
    private readonly Dictionary<DystopiaProductId, DystopiaProduct> products = new Dictionary<DystopiaProductId, DystopiaProduct>();
    // 세션 교체·정산/잔액 변화 감지만 수행하며 프레임별 씬 검색이나 정렬은 하지 않습니다.
    private DystopiaSession observedSession;
    private int observedRevision = -1;
    // 실행 중 Inspector 체크 변경을 한 번만 처리합니다.
    private bool observedSortingActive, observedCounterActive;

#if UNITY_EDITOR
    /// <summary>설비 테스트 창에서 기존 계산대의 테스트 주문 교체를 요청합니다.</summary>
    /// <returns>분류 중인 현재 거래를 교체했으면 true입니다.</returns>
    public bool RestartTestCustomer() => checkout != null && checkout.RestartEquipmentTest();

    /// <summary>설비 테스트 창에 기존 계수 결과를 제공합니다. 별도로 재집계하지 않습니다.</summary>
    public IReadOnlyDictionary<DystopiaProductId, int> TestQuantities => quantities;
    /// <summary>설비 테스트 창에서 계수 결과의 실제 상품 이름과 아이콘을 표시합니다.</summary>
    public IReadOnlyDictionary<DystopiaProductId, DystopiaProduct> TestProducts => products;
#endif

    /// <summary>기존 물품 수명 이벤트와 구매 버튼에 구독합니다.</summary>
    private void OnEnable()
    {
        if (checkout == null) { Debug.LogError("설비의 Checkout 연결이 필요합니다.", this); return; }
        checkout.TableContentsChanged += RefreshQuantities;
        checkout.CheckoutContentsChanged += SortCheckoutItems;
        if (sortingTrayButton != null) sortingTrayButton.onClick.AddListener(BuySortingTray);
        if (quantityCounterButton != null) quantityCounterButton.onClick.AddListener(BuyQuantityCounter);
        RefreshQuantities();
        RefreshButtons();
    }

    /// <summary>기존 Revision과 테스트 체크 변경을 감지해 표시와 최초 활성화 정렬을 갱신합니다.</summary>
    private void Update()
    {
        var session = checkout != null ? checkout.Session : null;
        bool sortingActive = IsUpgradeActive(DystopiaWorkbenchUpgrade.SortingTray);
        bool counterActive = IsUpgradeActive(DystopiaWorkbenchUpgrade.QuantityCounter);
        if (session == observedSession && (session == null || session.Revision == observedRevision)
            && sortingActive == observedSortingActive && counterActive == observedCounterActive) return;
        bool sortingActivated = sortingActive && !observedSortingActive;
        observedSortingActive = sortingActive;
        observedCounterActive = counterActive;
        observedSession = session;
        observedRevision = session != null ? session.Revision : -1;
        RefreshButtons();
        RefreshQuantities();
        if (sortingActivated && checkout.IsSorting && !session.IsPaused) SortCheckoutItems();
    }

    /// <summary>컴포넌트 비활성화 시 구독과 수량 표시를 해제합니다.</summary>
    private void OnDisable()
    {
        if (checkout != null)
        {
            checkout.TableContentsChanged -= RefreshQuantities;
            checkout.CheckoutContentsChanged -= SortCheckoutItems;
        }
        if (sortingTrayButton != null) sortingTrayButton.onClick.RemoveListener(BuySortingTray);
        if (quantityCounterButton != null) quantityCounterButton.onClick.RemoveListener(BuyQuantityCounter);
        foreach (var row in quantityRows) if (row != null && row.root != null) row.root.SetActive(false);
    }

    /// <summary>정산 중 자동 정렬 트레이만 구매합니다.</summary>
    public void BuySortingTray() => Buy(DystopiaWorkbenchUpgrade.SortingTray, sortingTrayPrice);

    /// <summary>정산 중 품목별 수량 계수기만 구매합니다.</summary>
    public void BuyQuantityCounter() => Buy(DystopiaWorkbenchUpgrade.QuantityCounter, quantityCounterPrice);

    /// <summary>기존 현금 소유자에게 단일 구매를 요청하고 표시를 갱신합니다.</summary>
    /// <param name="upgrade">구매할 독립 설비입니다.</param>
    /// <param name="price">Inspector에 설정한 구매가입니다.</param>
    private void Buy(DystopiaWorkbenchUpgrade upgrade, int price)
    {
        bool purchased = checkout != null && checkout.Session != null && checkout.Session.TryBuyWorkbenchUpgrade(upgrade, price);
        if (statusText != null) statusText.text = purchased ? "설비 구매 완료" : "정산 중 구매 가능: 가격 설정, 잔액, 보유 여부와 일시정지를 확인하세요.";
        RefreshButtons();
        RefreshQuantities();
    }

    /// <summary>세션이 보유한 한 설비의 구매 여부를 확인합니다.</summary>
    /// <param name="upgrade">확인할 설비입니다.</param>
    /// <returns>현재 세션이 설비를 소유하면 true입니다.</returns>
    private bool Owns(DystopiaWorkbenchUpgrade upgrade) => checkout != null && checkout.Session != null && (checkout.Session.WorkbenchUpgrades & upgrade) != 0;

    /// <summary>실제 보유 상태 또는 Editor 전용 테스트 활성화를 확인합니다.</summary>
    /// <param name="upgrade">확인할 독립 설비입니다.</param>
    /// <returns>연결된 세션에서 해당 기능을 사용할 수 있으면 true입니다.</returns>
    private bool IsUpgradeActive(DystopiaWorkbenchUpgrade upgrade)
    {
        if (checkout == null || checkout.Session == null) return false;
        return Owns(upgrade) || (Application.isEditor && (upgrade == DystopiaWorkbenchUpgrade.SortingTray
            ? testSortingTray : testQuantityCounter));
    }

    /// <summary>가격과 독립 구매 상태에 맞춰 구매 버튼만 갱신합니다.</summary>
    private void RefreshButtons()
    {
        var session = checkout != null ? checkout.Session : null;
        bool allowed = isActiveAndEnabled && session != null && session.Phase == DystopiaPhase.Settlement && !session.IsPaused;
        if (sortingTrayButton != null) sortingTrayButton.interactable = allowed && sortingTrayPrice >= 0 && session.Cash >= sortingTrayPrice && !Owns(DystopiaWorkbenchUpgrade.SortingTray);
        if (quantityCounterButton != null) quantityCounterButton.interactable = allowed && quantityCounterPrice >= 0 && session.Cash >= quantityCounterPrice && !Owns(DystopiaWorkbenchUpgrade.QuantityCounter);
    }

    /// <summary>기존 물품 목록에서 확정 처리되지 않은 단위만 고정 상품 ID별로 셉니다.</summary>
    private void RefreshQuantities()
    {
        quantities.Clear();
        products.Clear();
        if (IsUpgradeActive(DystopiaWorkbenchUpgrade.QuantityCounter) && checkout.HasCountableTableItems)
        {
            foreach (var item in checkout.Items)
            {
                if (item == null || item.State == TopDownItemState.Excluded) continue;
                var product = checkout.Session.ActiveProducts[item.ProductId];
                quantities.TryGetValue(product.id, out int count);
                quantities[product.id] = count + 1;
                products[product.id] = product;
            }
        }
        foreach (var row in quantityRows)
        {
            if (row == null || row.root == null || row.label == null) continue;
            quantities.TryGetValue(row.productId, out int count);
            row.root.SetActive(count > 0);
            if (count == 0) continue;
            var product = products[row.productId];
            bool hasIcon = row.icon != null && product.sprite != null;
            if (row.icon != null) { row.icon.sprite = product.sprite; row.icon.enabled = hasIcon; }
            row.label.text = hasIcon ? "× " + count : product.name + " × " + count;
        }
    }

    /// <summary>체크아웃 구성 변경 시 해당 영역의 물품만 검증하고 동일 품목 순으로 이동합니다.</summary>
    private void SortCheckoutItems()
    {
        if (!IsUpgradeActive(DystopiaWorkbenchUpgrade.SortingTray) || checkout.Items.Count == 0) return;
        if (sortingArea != null && !sortingArea.isTrigger) { ReportSortingFailure("Sorting Area에 Trigger 영역을 연결하세요."); return; }
        var ordered = new List<DystopiaTopDownItem>();
        foreach (var item in checkout.Items) if (checkout.IsCheckoutSortingItem(item)) ordered.Add(item);
        if (ordered.Count == 0) return;
        ordered.Sort((a, b) =>
        {
            int id = checkout.Session.ActiveProducts[a.ProductId].id.CompareTo(checkout.Session.ActiveProducts[b.ProductId].id);
            return id != 0 ? id : a.InstanceId.CompareTo(b.InstanceId);
        });
        Physics2D.SyncTransforms();
        var bounds = new Bounds[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            // PolygonCollider2D는 알파 외곽을 사용하므로 투명 이미지 여백이 간격에 포함되지 않습니다.
            bounds[i] = ordered[i].GetComponent<Collider2D>().bounds;
            foreach (var collider in ordered[i].GetComponentsInChildren<Collider2D>())
                if (collider.enabled) bounds[i].Encapsulate(collider.bounds);
        }
        // 실제로 겹치는 이동·판매 영역에서 시작하고, 큰 물품 하나의 크기를 모든 칸에 강제하지 않습니다.
        Rect area = checkout.CheckoutSortingBounds;
        if (sortingArea != null)
        {
            Rect requested = DystopiaTopDownTest.UpgradeAreaRect(sortingArea.transform, sortingArea.offset, sortingArea.size);
            area = Rect.MinMaxRect(Mathf.Max(area.xMin, requested.xMin), Mathf.Max(area.yMin, requested.yMin),
                Mathf.Min(area.xMax, requested.xMax), Mathf.Min(area.yMax, requested.yMax));
        }
        var destinations = new List<Vector2>(ordered.Count);
        var occupied = new List<Rect>();
        float gap = Mathf.Max(.01f, spacing);
        for (int first = 0; first < ordered.Count;)
        {
            int end = first + 1;
            while (end < ordered.Count && ordered[end].ProductId == ordered[first].ProductId) end++;
            int count = end - first;
            Vector2 cell = Vector2.zero;
            for (int i = first; i < end; i++) cell = Vector2.Max(cell, bounds[i].size);
            cell += Vector2.one * gap;
            // 같은 품목의 묶음 전체가 들어갈 사각형을 찾습니다. 다른 품목의 빈칸에 낱개를 흩어 놓지 않습니다.
            var columnOptions = new List<int>();
            for (int columns = 1; columns <= count; columns++) columnOptions.Add(columns);
            columnOptions.Sort((a, b) =>
            {
                float shapeA = Mathf.Abs(a * cell.x - Mathf.CeilToInt((float)count / a) * cell.y);
                float shapeB = Mathf.Abs(b * cell.x - Mathf.CeilToInt((float)count / b) * cell.y);
                int comparison = shapeA.CompareTo(shapeB);
                return comparison != 0 ? comparison : a.CompareTo(b);
            });
            bool found = false;
            foreach (int columns in columnOptions)
            {
                int rows = Mathf.CeilToInt((float)count / columns);
                Vector2 groupSize = new Vector2(columns * cell.x, rows * cell.y);
                float step = Mathf.Max(.03f, gap);
                for (float top = area.yMax; top >= area.yMin + groupSize.y - .0001f && !found; top -= step)
                {
                    for (float left = area.xMin; left <= area.xMax - groupSize.x + .0001f; left += step)
                    {
                        var group = new Rect(left, top - groupSize.y, groupSize.x, groupSize.y);
                        bool blocked = false;
                        foreach (var used in occupied) if (used.Overlaps(group)) { blocked = true; break; }
                        if (blocked) continue;
                        var candidate = new Bounds(new Vector3(group.center.x, group.center.y, bounds[first].center.z), new Vector3(group.width, group.height, 0));
                        if ((sortingArea != null && !DystopiaTopDownTest.UpgradeAreaContains(sortingArea, candidate)) || !checkout.IsUpgradePlacementSafe(candidate, protectedUi)) continue;
                        foreach (var hit in Physics2D.OverlapBoxAll(group.center, groupSize, 0))
                        {
                            if (hit.isTrigger) continue;
                            var item = hit.GetComponentInParent<DystopiaTopDownItem>();
                            if (item != null && ordered.Contains(item)) continue;
                            blocked = true;
                            break;
                        }
                        if (blocked) continue;
                        for (int i = 0; i < count; i++)
                            destinations.Add(new Vector2(left + (i % columns + .5f) * cell.x, top - (i / columns + .5f) * cell.y));
                        occupied.Add(group);
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            if (!found)
            {
                ReportSortingFailure($"{checkout.Session.ActiveProducts[ordered[first].ProductId].name} {count}개를 모아 놓을 공간이 부족합니다. 기존 배치를 유지합니다.");
                return;
            }
            first = end;
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            Vector2 position = item.Body.position + destinations[i] - (Vector2)bounds[i].center;
            item.Body.position = position;
            item.transform.position = new Vector3(position.x, position.y, item.transform.position.z);
            item.Body.linearVelocity = Vector2.zero;
            item.Body.angularVelocity = 0;
            item.Body.Sleep();
        }
        Physics2D.SyncTransforms();
        SortingStatus = $"체크아웃 {ordered.Count}개 정렬 완료";
    }

    /// <summary>안전하지 않은 정렬은 물품을 건드리지 않고 연결 문제를 알립니다.</summary>
    /// <param name="reason">수동 설정에서 해결할 실패 이유입니다.</param>
    private void ReportSortingFailure(string reason)
    {
        SortingStatus = reason;
        if (statusText != null) statusText.text = reason;
        Debug.LogWarning("자동 정렬 트레이: " + reason, this);
    }

    /// <summary>한 상품의 아이콘·수량 표시를 기존 Canvas 배치에 연결합니다.</summary>
    [Serializable]
    private sealed class QuantityRow
    {
        /// <summary>표시할 고정 상품 ID입니다. 같은 ID는 한 번만 연결합니다.</summary>
        public DystopiaProductId productId;
        /// <summary>수량이 0이거나 미구매이면 숨기는 해당 행의 루트입니다.</summary>
        public GameObject root;
        /// <summary>상품 아이콘을 표시할 선택적 Image입니다.</summary>
        public Image icon;
        /// <summary>수량 또는 아이콘이 없을 때 이름과 수량을 표시합니다.</summary>
        public Text label;
    }
}
