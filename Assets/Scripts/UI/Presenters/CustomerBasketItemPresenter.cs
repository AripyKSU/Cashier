using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프리팹으로 배치된 장바구니 한 항목의 표시를 담당합니다.
/// </summary>
public sealed class CustomerBasketItemPresenter : MonoBehaviour
{
    [Tooltip("상품 Sprite를 표시하는 이미지입니다.")]
    [SerializeField] private Image itemImage;

    /// <summary>
    /// 장바구니 항목 스냅샷을 화면에 반영합니다.
    /// </summary>
    /// <param name="viewData">표시할 상품 Sprite를 담은 스냅샷입니다.</param>
    public void UpdateView(CustomerBasketItemViewData viewData)
    {
        if (this.itemImage != null)
        {
            this.itemImage.sprite = viewData.Icon;
            this.itemImage.color = Color.white;
            this.itemImage.preserveAspect = true;
        }
    }
}
