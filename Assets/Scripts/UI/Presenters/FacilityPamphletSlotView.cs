using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>팸플릿 위에서 사람이 이동할 수 있는 가격·구매·SoldOut 표시 한 묶음을 관리한다.</summary>
public sealed class FacilityPamphletSlotView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Image soldOutImage;

    private static readonly Color PaidTextColor = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color PaidPlateColor = new Color(0.27f, 0.06f, 0.05f, 1f);
    private static readonly Color[] StampDustPalette =
    {
        new Color(0.45f, 0.1f, 0.08f), new Color(0.6f, 0.45f, 0.3f), new Color(0.3f, 0.22f, 0.16f)
    };

    private uint facilityIdx;
    private bool canPurchase;
    private FacilityButtonFeedback purchaseFeedback;
    /// <summary>열린 수명 동안 도장이 찍혀 있었는지 기억해 구매 순간에만 연출한다.</summary>
    private bool hasDisplayedOwnership;
    private bool wasOwned;
    private Tween stampTween;
    private Tween pageShakeTween;
    private Vector3 stampRestScale = Vector3.one;

    /// <summary>사용자가 요청한 설비 PK. 화면 문자열이나 슬롯 순서를 식별자로 사용하지 않는다.</summary>
    public event Action<uint> PurchaseRequested;

    /// <summary>튜토리얼 강조용 구매 버튼 영역입니다.</summary>
    public RectTransform PurchaseButtonRect => purchaseButton != null ? (RectTransform)purchaseButton.transform : null;

    /// <summary>튜토리얼 강조용 판매가 영역입니다.</summary>
    public RectTransform PriceRect => priceText != null ? priceText.rectTransform : null;

    /// <summary>글자가 그림에 박힌 구매 버튼에 호버·누름 반응을 붙이고 도장의 원래 크기를 기억한다.</summary>
    private void Awake()
    {
        if (soldOutImage != null) stampRestScale = soldOutImage.rectTransform.localScale;
        Image pamphlet = transform.parent != null ? transform.parent.GetComponent<Image>() : null;
        if (purchaseButton != null && pamphlet != null && pamphlet.sprite != null)
            purchaseFeedback = FacilityButtonFeedback.AttachToBakedArt(purchaseButton, pamphlet);
    }

    /// <summary>활성 수명에만 구매 버튼 요청을 구독한다.</summary>
    private void OnEnable()
    {
        if (purchaseButton != null) purchaseButton.onClick.AddListener(handlePurchase);
    }

    /// <summary>비활성화 시 구매 버튼 요청을 해제하고 진행 중인 도장 연출을 정리한다.</summary>
    private void OnDisable()
    {
        if (purchaseButton != null) purchaseButton.onClick.RemoveListener(handlePurchase);
        hasDisplayedOwnership = false;
        pageShakeTween?.Kill();
        stampTween?.Kill();
        if (soldOutImage == null) return;
        soldOutImage.rectTransform.localScale = stampRestScale;
        Color color = soldOutImage.color;
        color.a = 1f;
        soldOutImage.color = color;
    }

    /// <summary>가격과 구매 완료 도장을 현재 설비 상태로 갱신한다.</summary>
    /// <param name="data">표시할 설비 snapshot.</param>
    /// <param name="interactive">상위 팸플릿이 구매 입력을 허용하는지 여부.</param>
    public void UpdateView(FacilityItemViewData data, bool interactive)
    {
        facilityIdx = data.FacilityIdx;
        canPurchase = data.State == FacilityDisplayState.Purchasable;
        if (priceText != null) priceText.text = data.PurchasePrice.ToString("N0");
        bool isOwned = data.State == FacilityDisplayState.ActivationPending ||
            data.State == FacilityDisplayState.Active ||
            data.State == FacilityDisplayState.OwnedStageUpgrade ||
            data.State == FacilityDisplayState.OwnedProgression;
        if (soldOutImage != null) soldOutImage.gameObject.SetActive(isOwned);
        SetInteractionEnabled(interactive);
        purchaseFeedback?.SetRejectsWhenDisabled(data.State == FacilityDisplayState.InsufficientFunds);
        if (hasDisplayedOwnership && !wasOwned && isOwned && isActiveAndEnabled) playPurchased(data.PurchasePrice);
        hasDisplayedOwnership = true;
        wasOwned = isOwned;
    }

    /// <summary>표시 상태를 바꾸지 않고 처리 중 구매 입력만 잠근다.</summary>
    /// <param name="interactive">상위 화면의 입력 허용 여부.</param>
    public void SetInteractionEnabled(bool interactive)
    {
        if (purchaseButton != null) purchaseButton.interactable = interactive && canPurchase;
    }

    /// <summary>구매 순간 도장을 내려찍고 먼지·결제액·종이 떨림을 낸다.</summary>
    /// <param name="paidPrice">결제한 금액입니다.</param>
    private void playPurchased(long paidPrice)
    {
        if (soldOutImage == null) return;
        stampTween?.Kill();
        RectTransform stamp = soldOutImage.rectTransform;
        stamp.localScale = stampRestScale * 2.6f;
        Color color = soldOutImage.color;
        color.a = 0f;
        soldOutImage.color = color;

        stampTween = DOTween.Sequence()
            .Join(stamp.DOScale(stampRestScale, 0.14f).SetEase(Ease.InQuad))
            .Join(FacilityShopEffects.Fade(soldOutImage, 1f, 0.08f))
            .AppendCallback(landStamp)
            .Append(stamp.DOPunchScale(stampRestScale * -0.12f, 0.2f, 4, 0.5f))
            .SetUpdate(true)
            .SetLink(gameObject);

        // 도장과 겹치지 않도록 가격 칸 오른쪽 끝에서 떠오른다. 슬롯 부모는 팸플릿 그림이므로 글자가 슬롯보다 앞에 그려진다.
        if (priceText == null) return;
        var page = (RectTransform)transform.parent;
        Rect priceRect = priceText.rectTransform.rect;
        Vector2 priceEdge = page.InverseTransformPoint(priceText.rectTransform.TransformPoint(new Vector3(priceRect.xMax, priceRect.center.y, 0f)));
        FacilityShopEffects.FloatText(page, priceEdge, $"-{paidPrice:N0}원", priceText.font, 15f, PaidTextColor, PaidPlateColor);
    }

    /// <summary>도장이 종이에 닿는 순간의 소리와 떨림을 낸다.</summary>
    private void landStamp()
    {
        var page = (RectTransform)transform.parent;
        Vector2 center = page.InverseTransformPoint(soldOutImage.rectTransform.position);
        SoundManager.Instance?.PlaySfx(SoundKeys.ReputationStamp);
        FacilityShopEffects.Burst(page, center, 16, StampDustPalette, 90f, 9f);
        pageShakeTween?.Kill();
        pageShakeTween = FacilityShopEffects.Shake(page, 6f, 0.2f);
    }

    /// <summary>구매 가능한 상태의 PK만 한 번 전달한다.</summary>
    private void handlePurchase()
    {
        if (isActiveAndEnabled && purchaseButton != null && purchaseButton.interactable && canPurchase)
            PurchaseRequested?.Invoke(facilityIdx);
    }
}
