using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루 시작 전 또는 영업 중 수시로 확인하는 가격표 팝업 패널 (개발자 3 담당).
/// 기본 상품 가격과 특이사항(세일, 1+1 등)을 표시합니다.
/// </summary>
public class PriceListPanel : MonoBehaviour
{
    // =========================================================================
    // 1. DATA STRUCT
    // =========================================================================

    [Serializable]
    public struct ItemPriceInfo
    {
        public string ItemName;
        public long Price;
        public string SpecialNote; // 예: "20% 세일", "1+1", null
        public Sprite Icon;

        public ItemPriceInfo(string name, long price, string specialNote = null, Sprite icon = null)
        {
            this.ItemName = name;
            this.Price = price;
            this.SpecialNote = specialNote;
            this.Icon = icon;
        }
    }


    // =========================================================================
    // 2. SERIALIZED FIELDS
    // =========================================================================

    [Header("UI References")]
    [Tooltip("패널 본체 오브젝트 (활성화/비활성화 대상)")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("상품 슬롯들이 생성되어 배치될 부모 컨테이너 (예: ScrollView Content)")]
    [SerializeField] private Transform slotContainer;

    [Tooltip("복제할 상품 슬롯 프리팹")]
    [SerializeField] private PriceItemSlot slotPrefab;

    [Tooltip("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Header("Settings")]
    [Tooltip("Start 시 테스트용 샘플 데이터를 자동으로 채울지 여부")]
    [SerializeField] private bool autoPopulateSampleData = true;


    // =========================================================================
    // 3. PRIVATE FIELDS
    // =========================================================================

    private readonly List<PriceItemSlot> activeSlots = new List<PriceItemSlot>();


    // =========================================================================
    // 4. UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (this.closeButton != null)
        {
            this.closeButton.onClick.AddListener(this.Close);
        }
    }

    private void Start()
    {
        if (this.autoPopulateSampleData && this.activeSlots.Count == 0)
        {
            this.PopulateSampleData();
        }
    }


    // =========================================================================
    // 5. PUBLIC METHODS
    // =========================================================================

    /// <summary>가격표 패널 열기</summary>
    public void Open()
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(true);
        }
        else
        {
            this.gameObject.SetActive(true);
        }
    }

    /// <summary>가격표 패널 닫기</summary>
    public void Close()
    {
        if (this.panelRoot != null)
        {
            this.panelRoot.SetActive(false);
        }
        else
        {
            this.gameObject.SetActive(false);
        }
    }

    /// <summary>가격표 패널 열기/닫기 토글</summary>
    public void Toggle()
    {
        bool isActive = this.panelRoot != null ? this.panelRoot.activeSelf : this.gameObject.activeSelf;
        if (isActive)
        {
            this.Close();
        }
        else
        {
            this.Open();
        }
    }

    /// <summary>
    /// 외부(데이터 매니저 또는 게임 매니저)에서 상품 목록을 받아 가격표를 갱신합니다.
    /// </summary>
    public void SetPriceList(IEnumerable<ItemPriceInfo> itemList)
    {
        this.clearSlots();

        if (this.slotContainer == null || this.slotPrefab == null)
        {
            Debug.LogWarning("[PriceListPanel] slotContainer 또는 slotPrefab이 설정되지 않았습니다.");
            return;
        }

        foreach (var item in itemList)
        {
            var slot = Instantiate(this.slotPrefab, this.slotContainer);
            slot.gameObject.SetActive(true);
            slot.SetData(item.ItemName, item.Price, item.SpecialNote, item.Icon);
            this.activeSlots.Add(slot);
        }
    }

    /// <summary>
    /// 테스트용 샘플 데이터를 채워 넣습니다.
    /// </summary>
    public void PopulateSampleData()
    {
        var samples = new List<ItemPriceInfo>
        {
            new ItemPriceInfo("Fresh Apple", 1500, "20% OFF"),
            new ItemPriceInfo("Organic Milk", 2200, "1+1 EVENT"),
            new ItemPriceInfo("Sweet Banana", 3500),
            new ItemPriceInfo("Tuna Riceball", 1300),
            new ItemPriceInfo("Lunch Box", 5500, "HOT"),
            new ItemPriceInfo("Canned Coffee", 1000)
        };

        this.SetPriceList(samples);
    }


    // =========================================================================
    // 6. PRIVATE HELPER METHODS
    // =========================================================================

    private void clearSlots()
    {
        foreach (var slot in this.activeSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        this.activeSlots.Clear();
    }
}
