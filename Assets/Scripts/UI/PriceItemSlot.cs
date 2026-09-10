using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가격표 패널 내 개별 상품 항목 1줄을 표시하는 UI 슬롯 (개발자 3 담당).
/// </summary>
public class PriceItemSlot : MonoBehaviour
{
    // =========================================================================
    // 1. SERIALIZED FIELDS
    // =========================================================================

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemPriceText;
    [SerializeField] private Image itemIconImage;

    [Header("Special Note Badge")]
    [Tooltip("세일, 1+1 등 특이사항이 있을 때 활성화되는 뱃지 오브젝트")]
    [SerializeField] private GameObject badgeRoot;
    [SerializeField] private TextMeshProUGUI badgeText;


    // =========================================================================
    // 2. PUBLIC METHODS
    // =========================================================================

    /// <summary>
    /// 슬롯 UI에 상품 정보 및 특이사항 데이터를 설정합니다.
    /// </summary>
    /// <param name="itemName">상품 이름 (예: 신선한 사과)</param>
    /// <param name="price">상품 가격 (예: 1200)</param>
    /// <param name="specialNote">특이사항 텍스트 (예: '20% 세일', '1+1', 없으면 null 또는 빈 문자열)</param>
    /// <param name="icon">상품 아이콘 스프라이트 (선택)</param>
    public void SetData(string itemName, long price, string specialNote = null, Sprite icon = null)
    {
        if (this.itemNameText != null)
        {
            this.itemNameText.text = itemName;
        }

        if (this.itemPriceText != null)
        {
            this.itemPriceText.text = $"{price:N0} G";
        }

        if (this.itemIconImage != null)
        {
            if (icon != null)
            {
                this.itemIconImage.sprite = icon;
                this.itemIconImage.gameObject.SetActive(true);
            }
            else
            {
                this.itemIconImage.gameObject.SetActive(false);
            }
        }

        // 특이사항 뱃지 처리 (세일, 1+1 등)
        bool hasBadge = !string.IsNullOrEmpty(specialNote);
        if (this.badgeRoot != null)
        {
            this.badgeRoot.SetActive(hasBadge);
        }

        if (hasBadge && this.badgeText != null)
        {
            this.badgeText.text = specialNote;
        }
    }
}
