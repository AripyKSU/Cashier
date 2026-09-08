using TMPro;
using UnityEngine;

/// <summary>
/// 프리팹으로 배치된 장바구니 한 항목의 표시를 담당합니다.
/// </summary>
public sealed class CustomerBasketItemPresenter : MonoBehaviour
{
    [Tooltip("상품 이름과 수량을 표시하는 텍스트입니다.")]
    [SerializeField] private TextMeshProUGUI itemText;

    /// <summary>
    /// 장바구니 항목 스냅샷을 화면에 반영합니다.
    /// </summary>
    /// <param name="viewData">표시할 상품 이름과 수량을 담은 스냅샷입니다.</param>
    public void UpdateView(CustomerBasketItemViewData viewData)
    {
        if (this.itemText != null)
        {
            this.itemText.text = $"{viewData.DisplayName} x {viewData.Quantity}";
        }
    }
}
