using System;
using UnityEngine;

/// <summary>
/// 1일차 정산에서 딸이 설비 업그레이드를 직접 열어 보게 하며 설명하는 진행자.
/// 팜플렛 강조 → "눌러 봐" → 창 안의 위 두 설비 → 판매가·구매 버튼 → 분류 막대 → 창 닫기 순서로 진행합니다.
/// </summary>
public sealed class SettlementFacilityGuide
{
    // 1단계 팸플릿 그림 안에서 위 두 설비(제목·선반 그림·신규 품목 줄)가 차지하는 영역(0~1).
    private static readonly Rect FoodShelfArea = new Rect(0.054f, 0.52f, 0.442f, 0.294f);
    private static readonly Rect MedicineShelfArea = new Rect(0.517f, 0.52f, 0.436f, 0.294f);
    // 위 두 설비의 "판매가 ○○원 구매" 줄 영역(0~1).
    private static readonly Rect FoodPurchaseArea = new Rect(0.059f, 0.448f, 0.428f, 0.045f);
    private static readonly Rect MedicinePurchaseArea = new Rect(0.524f, 0.448f, 0.42f, 0.045f);
    // 분류 막대 제목과 막대 그림, 판매가 줄까지의 영역(0~1).
    private static readonly Rect SortingBarArea = new Rect(0.042f, 0.249f, 0.852f, 0.205f);
    // 설비 창을 말풍선 오른쪽으로 비켜 둘 때 둘 사이 간격(화면 너비 비율).
    private const float WindowGapRatio = 0.01f;

    private readonly DaughterDialoguePresenter daughter;
    private readonly DailySettlementFlowController flow;
    private readonly SettlementInteractionView interaction;
    private readonly FacilityShopPresenter shop;
    private readonly TutorialHighlightOverlay overlay;
    private readonly int frontSortingOrder;
    private readonly Func<uint[], string[]> resolveTexts;
    private readonly Action closeShop;
    private Action finished;
    private bool isWaitingForShop;
    private RectTransform movedWindow;
    private Vector3 windowRestPosition;

    public SettlementFacilityGuide(DaughterDialoguePresenter daughter, DailySettlementFlowController flow,
        SettlementInteractionView interaction, FacilityShopPresenter shop, Canvas rootCanvas,
        Func<uint[], string[]> resolveTexts, Action closeShop)
    {
        this.daughter = daughter;
        this.flow = flow;
        this.interaction = interaction;
        this.shop = shop;
        this.resolveTexts = resolveTexts;
        this.closeShop = closeShop;
        // 말풍선은 설비 창 위, 강조 상자는 그보다 더 위에 그린다.
        frontSortingOrder = rootCanvas.sortingOrder + 5;
        overlay = TutorialHighlightOverlay.Create(rootCanvas, frontSortingOrder + 2);
    }

    /// <summary>안내를 시작합니다. 창을 닫고 나면 onFinished를 부릅니다.</summary>
    /// <param name="onFinished">안내가 모두 끝나면 호출됩니다.</param>
    public void Run(Action onFinished)
    {
        finished = onFinished;
        overlay.Show(new TutorialHighlightOverlay.Target(interaction.FacilityPamphletRect));
        daughter.Say(resolveTexts(DaughterDayScript.FacilityPointLines), true, () =>
        {
            isWaitingForShop = true;
            flow.AllowGuideFacilityOpen();
        });
    }

    /// <summary>설비 창이 열린 직후 호출됩니다. "눌러 봐"를 기다리던 중이면 창 안 설명을 이어 갑니다.</summary>
    public void NotifyShopOpened()
    {
        if (!isWaitingForShop) return;
        isWaitingForShop = false;
        overlay.SetBlocking(true);
        daughter.SetBubbleInFront(true, frontSortingOrder);
        moveWindowClearOfBubble();
        RectTransform panel = shop.Stage1PanelRect;
        overlay.Show(new TutorialHighlightOverlay.Target(panel, FoodShelfArea),
            new TutorialHighlightOverlay.Target(panel, MedicineShelfArea));
        daughter.Say(resolveTexts(DaughterDayScript.FacilityShelfLines), false, explainPurchase);
    }

    /// <summary>위 두 설비의 판매가와 구매 버튼을 강조합니다.</summary>
    private void explainPurchase()
    {
        RectTransform panel = shop.Stage1PanelRect;
        overlay.Show(new TutorialHighlightOverlay.Target(panel, FoodPurchaseArea),
            new TutorialHighlightOverlay.Target(panel, MedicinePurchaseArea));
        daughter.Say(resolveTexts(DaughterDayScript.FacilityPurchaseLines), false, explainSorting);
    }

    /// <summary>아래 분류 막대를 강조합니다.</summary>
    private void explainSorting()
    {
        overlay.Show(new TutorialHighlightOverlay.Target(shop.Stage1PanelRect, SortingBarArea));
        daughter.Say(resolveTexts(DaughterDayScript.FacilitySortingLines), false, finish);
    }

    /// <summary>
    /// 앞에 그린 말풍선이 설비 그림을 가리지 않도록 설비 창을 말풍선 오른쪽으로 잠시 비켜 둡니다.
    /// </summary>
    private void moveWindowClearOfBubble()
    {
        movedWindow = shop.Stage1PanelRect;
        while (movedWindow.parent != null && movedWindow.parent != shop.transform)
            movedWindow = (RectTransform)movedWindow.parent;
        windowRestPosition = movedWindow.position;
        float shift = worldXRange(daughter.BubbleRect).y + Screen.width * WindowGapRatio - worldXRange(movedWindow).x;
        if (shift > 0f) movedWindow.position += new Vector3(shift, 0f, 0f);
    }

    /// <summary>사각형의 화면 가로 범위(왼쪽, 오른쪽)를 구합니다.</summary>
    private static Vector2 worldXRange(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return new Vector2(corners[0].x, corners[2].x);
    }

    /// <summary>강조를 지우고 창을 닫은 뒤 완료를 알립니다.</summary>
    private void finish()
    {
        overlay.Hide();
        daughter.SetBubbleInFront(false, 0);
        if (movedWindow != null) movedWindow.position = windowRestPosition;
        movedWindow = null;
        closeShop();
        Action callback = finished;
        finished = null;
        callback?.Invoke();
    }
}
