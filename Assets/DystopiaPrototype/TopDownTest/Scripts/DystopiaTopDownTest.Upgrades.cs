using System.Collections.Generic;
using UnityEngine;

/// <summary>작업대 설비에 기존 판매·거절·이동 경계와 계산기 보호 검사를 제공합니다.</summary>
public sealed partial class DystopiaTopDownTest
{
#if UNITY_EDITOR
    /// <summary>분류 중인 거래를 3종·각 3개 테스트 주문으로 교체하고 기존 쏟기 경로를 실행합니다.</summary>
    /// <returns>진행 중인 연출이나 정산을 방해하지 않고 시작했으면 true입니다.</returns>
    public bool RestartEquipmentTest()
    {
        if (!Application.isPlaying || state != ViewState.Sorting || isPaused || Session == null || Session.IsPaused
            || Session.Phase != DystopiaPhase.Trading) return false;
        ResetGrabInput();
        Session.PrepareEquipmentTestCustomer();
        state = ViewState.Pouring;
        flowRoutine = StartCoroutine(PourEquipmentTest());
        return true;
    }

    /// <summary>기존 물품 생성과 수량 이벤트를 그대로 사용하고 쏟기 완료 후 조작을 재개합니다.</summary>
    private System.Collections.IEnumerator PourEquipmentTest()
    {
        yield return PourItems();
        state = ViewState.Sorting;
        pouringContainerImage.gameObject.SetActive(false);
        ResetGrabInput();
        RefreshUi();
    }
#endif

    // 직전 판정의 구성원을 보존하여 드래그·흡입기가 직접 변경한 상태도 감지합니다.
    private readonly HashSet<DystopiaTopDownItem> checkoutContents = new HashSet<DystopiaTopDownItem>();

    /// <summary>기존 오른쪽 판매 영역의 월드 외곽입니다.</summary>
    internal Rect CheckoutSortingBounds
    {
        get
        {
            Rect sale = placedSaleZone != null
                ? UpgradeAreaRect(placedSaleZone.transform, placedSaleZone.offset, placedSaleZone.size)
                : UpgradeAreaRect(itemRoot, SaleZone.center, SaleZone.size);
            Rect movement = placedMovementZone != null
                ? UpgradeAreaRect(placedMovementZone.transform, placedMovementZone.offset, placedMovementZone.size)
                : UpgradeAreaRect(itemRoot, Vector2.zero, new Vector2(11.9f, 6.3f));
            return UnityEngine.Rect.MinMaxRect(Mathf.Max(sale.xMin, movement.xMin), Mathf.Max(sale.yMin, movement.yMin),
                Mathf.Min(sale.xMax, movement.xMax), Mathf.Min(sale.yMax, movement.yMax));
        }
    }

    /// <summary>오른쪽 영역에 놓인 조작 중이 아닌 판매 후보인지 확인합니다.</summary>
    /// <param name="item">기존 물품 목록의 항목입니다.</param>
    /// <returns>자동 정렬에 포함할 수 있으면 true입니다.</returns>
    internal bool IsCheckoutSortingItem(DystopiaTopDownItem item) => item != null && item != heldItem
        && item.gameObject.activeInHierarchy && !item.IsBeingVacuumed && item.State == TopDownItemState.ForSale
        && ZoneContains(placedSaleZone, SaleZone, item.transform.localPosition);

    /// <summary>기존 분류 직후 진입·이탈이 있을 때만 정렬 구독자에게 알립니다.</summary>
    private void RefreshCheckoutContents()
    {
        bool changed = checkoutContents.RemoveWhere(item => !IsCheckoutSortingItem(item)) > 0;
        foreach (var item in items)
            if (IsCheckoutSortingItem(item)) changed |= checkoutContents.Add(item);
        if (changed) CheckoutContentsChanged?.Invoke();
    }

    /// <summary>물품 외곽이 기존 처리 영역과 UI를 침범하지 않는지 보수적으로 검사합니다.</summary>
    /// <param name="bounds">간격을 포함한 후보 셀의 월드 외곽입니다.</param>
    /// <param name="protectedUi">계산기 외에 Inspector에서 지정한 UI 영역입니다.</param>
    /// <returns>기존 경계 내부이고 모든 보호 영역 밖이면 true입니다.</returns>
    internal bool IsUpgradePlacementSafe(Bounds bounds, RectTransform[] protectedUi)
    {
        Rect movement = placedMovementZone != null ? new Rect(placedMovementZone.offset - placedMovementZone.size * .5f, placedMovementZone.size) : new Rect(-5.95f, -3.15f, 11.9f, 6.3f);
        Rect local = UpgradeLocalBounds(bounds, placedMovementZone != null ? placedMovementZone.transform : itemRoot);
        if (local.xMin < movement.xMin || local.xMax > movement.xMax || local.yMin < movement.yMin || local.yMax > movement.yMax) return false;
        Rect sale = placedSaleZone != null ? new Rect(placedSaleZone.offset - placedSaleZone.size * .5f, placedSaleZone.size) : SaleZone;
        Rect saleLocal = UpgradeLocalBounds(bounds, placedSaleZone != null ? placedSaleZone.transform : itemRoot);
        if (saleLocal.xMin < sale.xMin || saleLocal.xMax > sale.xMax || saleLocal.yMin < sale.yMin || saleLocal.yMax > sale.yMax) return false;
        if (UpgradeOverlapsZone(bounds, placedExcludedZone, ExcludedZone)) return false;
        if (UpgradeOverlapsUi(bounds, keypadRect)) return false;
        foreach (var rect in protectedUi) if (UpgradeOverlapsUi(bounds, rect)) return false;
        return true;
    }

    /// <summary>무효화된 Collider라도 기존 판정에 사용하는 로컬 사각형과 비교합니다.</summary>
    /// <param name="bounds">물품 월드 외곽입니다.</param>
    /// <param name="zone">기존에 연결된 처리 영역입니다.</param>
    /// <param name="fallback">영역 미연결 시 기존 판정값입니다.</param>
    /// <returns>처리 영역과 겹치면 true입니다.</returns>
    private bool UpgradeOverlapsZone(Bounds bounds, BoxCollider2D zone, Rect fallback)
    {
        Rect local = UpgradeLocalBounds(bounds, zone != null ? zone.transform : itemRoot);
        Rect region = zone != null ? new Rect(zone.offset - zone.size * .5f, zone.size) : fallback;
        return local.Overlaps(region, true);
    }

    /// <summary>실제 물품 카메라와 UI Canvas 카메라로 투영한 화면 외곽을 비교합니다.</summary>
    /// <param name="bounds">물품 월드 외곽입니다.</param>
    /// <param name="rect">보호할 UI의 수동 배치입니다.</param>
    /// <returns>화면상 영역이 겹치면 true입니다. 숨긴 UI도 예약 영역으로 취급합니다.</returns>
    private bool UpgradeOverlapsUi(Bounds bounds, RectTransform rect)
    {
        if (rect == null) return false;
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        Rect ui = UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        min = Vector2.positiveInfinity; max = Vector2.negativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector2 point = worldCamera.WorldToScreenPoint(new Vector3(i % 2 == 0 ? bounds.min.x : bounds.max.x, i < 2 ? bounds.min.y : bounds.max.y, bounds.center.z));
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return ui.Overlaps(UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y), true);
    }

    /// <summary>회전과 스케일을 포함한 영역의 월드 외접 사각형을 반환합니다.</summary>
    /// <param name="transform">영역의 수동 배치입니다.</param>
    /// <param name="offset">Collider의 로컬 중심입니다.</param>
    /// <param name="size">Collider의 로컬 크기입니다.</param>
    /// <returns>월드 XY 평면의 외접 사각형입니다.</returns>
    internal static Rect UpgradeAreaRect(Transform transform, Vector2 offset, Vector2 size)
    {
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = offset + new Vector2(i % 2 == 0 ? -size.x : size.x, i < 2 ? -size.y : size.y) * .5f;
            Vector2 point = transform.TransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>후보의 네 모서리가 지정한 Trigger 영역 안에 있는지 검사합니다.</summary>
    /// <param name="area">Inspector에서 지정한 안전 영역입니다.</param>
    /// <param name="bounds">후보 셀의 월드 외접 사각형입니다.</param>
    /// <returns>모든 모서리가 영역 안에 있으면 true입니다.</returns>
    internal static bool UpgradeAreaContains(BoxCollider2D area, Bounds bounds)
    {
        Rect local = UpgradeLocalBounds(bounds, area.transform);
        Vector2 min = area.offset - area.size * .5f, max = area.offset + area.size * .5f;
        return local.xMin >= min.x && local.xMax <= max.x && local.yMin >= min.y && local.yMax <= max.y;
    }

    /// <summary>월드 외곽의 네 모서리를 기존 판정 영역의 로컬 좌표로 변환합니다.</summary>
    /// <param name="bounds">월드 XY 후보 외접 사각형입니다.</param>
    /// <param name="transform">판정 좌표의 기준입니다.</param>
    /// <returns>영역 로컬 좌표에서 보수적으로 계산한 외접 사각형입니다.</returns>
    private static Rect UpgradeLocalBounds(Bounds bounds, Transform transform)
    {
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector2 point = transform.InverseTransformPoint(new Vector3(i % 2 == 0 ? bounds.min.x : bounds.max.x, i < 2 ? bounds.min.y : bounds.max.y, bounds.center.z));
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
