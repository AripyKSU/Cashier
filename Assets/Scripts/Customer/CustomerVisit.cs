using System.Collections.Generic;
using System;

/// <summary>계산대 방문 상태. 판정 후 명시적으로 퇴장하며 재제안을 금지한다.</summary>
public enum CustomerState
{
    Entering,
    AwaitingOffer,
    Accepted,
    Rejected,
    Departed,
    Queued,
    Abandoned
}

/// <summary>진행 상태와 별개로 퇴장 후에도 보존하는 가격 판정 결과.</summary>
public enum CustomerTradeOutcome
{
    None = 0,
    RegularSale = 1,
    DiscountSale = 2,
    ExploitativeSale = 3,
    PaymentRefused = 4
}

/// <summary>한 방문에서 확정된 손님 조합과 변경 불가능한 구매 목록.</summary>
public sealed class CustomerVisit
{
    private readonly IReadOnlyDictionary<uint, ProductData> products;
    private readonly Func<IReadOnlyDictionary<uint, uint>> getCurrentPrices;
    /// <summary>제출 시 한 번 조회하는 지침 공급자. null은 미연결이며 방문이 수명을 소유하지 않는다.</summary>
    private readonly Func<IReadOnlyList<SaleRestriction>> getSaleRestrictions;
    private bool isSubmitting;
    /// <summary>생성 시 선택한 수락 대사.</summary>
    private readonly uint regularSaleTextIdx;
    /// <summary>생성 시 선택한 저가 판매 대사.</summary>
    private readonly uint discountSaleTextIdx;
    /// <summary>생성 시 선택한 착취 판매 대사.</summary>
    private readonly uint exploitativeSaleTextIdx;
    /// <summary>생성 시 선택한 거절 대사.</summary>
    private readonly uint rejectTextIdx;
    /// <summary>제출 완료 결과. null은 미판정이며 재정 반영 완료를 의미하지 않는다.</summary>
    public TransactionResult? Result { get; private set; }
    /// <summary>방문의 현재 상태. 이 객체의 API만 변경한다.</summary>
    public CustomerState State { get; private set; } = CustomerState.Entering;
    /// <summary>최종 목록의 제출 시점 기준 총액. 제출 전 null.</summary>
    public long? BaseTotal => Result?.ReferenceTotal;
    /// <summary>입장 시 고정한 가격 허용 배율. 1000=100%.</summary>
    public int PriceTolerance { get; }
    /// <summary>방문 생성 시 복사한 정가 인정 하한. 1000=100%.</summary>
    public int RegularPriceMinRate { get; }
    /// <summary>방문 생성 시 복사한 정가 인정 상한. 결제 거부 판정이 우선한다.</summary>
    public int RegularPriceMaxRate { get; }
    /// <summary>제출 시 확정한 수락 상한. 제출 전 null이며 UI에 자동 노출하지 않는다.</summary>
    public long? AllowedTotal { get; private set; }
    /// <summary>유효한 제안 총액. 미제안 상태는 null.</summary>
    public long? OfferedTotal { get; private set; }
    /// <summary>제안 후 결과. 퇴장 후에도 유지하며 판정 전은 null이다.</summary>
    public bool? WasAccepted => Outcome == CustomerTradeOutcome.None ? (bool?)null : Outcome != CustomerTradeOutcome.PaymentRefused;
    /// <summary>제안 시 확정하고 퇴장 후에도 유지하는 거래 결과.</summary>
    public CustomerTradeOutcome Outcome { get; private set; }
    /// <summary>테스트 UI용 결과명. 정식 현지화는 TextData로 이관한다.</summary>
    public string OutcomeLabel => Outcome switch
    {
        CustomerTradeOutcome.RegularSale => "기준가 판매",
        CustomerTradeOutcome.DiscountSale => "저가 판매",
        CustomerTradeOutcome.ExploitativeSale => "착취 판매",
        CustomerTradeOutcome.PaymentRefused => "결제 거부",
        _ => "판정 대기"
    };
    /// <summary>입장 대사의 TextData FK.</summary>
    public uint EntryTextIdx { get; }
    /// <summary>현재 결과에 대응하는 대사. 제안 전은 입장 대사다.</summary>
    public uint FeedbackTextIdx => Outcome switch
    {
        CustomerTradeOutcome.RegularSale => regularSaleTextIdx,
        CustomerTradeOutcome.DiscountSale => discountSaleTextIdx,
        CustomerTradeOutcome.ExploitativeSale => exploitativeSaleTextIdx,
        CustomerTradeOutcome.PaymentRefused => rejectTextIdx,
        _ => EntryTextIdx
    };
    /// <summary>이번 방문의 외형 ID. 동일 외형의 재등장은 동일 인물을 뜻하지 않는다.</summary>
    public uint AppearanceIdx { get; }
    /// <summary>이번 방문 생성에 사용한 성향 ID.</summary>
    public uint DispositionIdx { get; }
    /// <summary>방문 생성 시 복사한 성향 타입. 이후 원본 DTO 변경에 영향받지 않는다.</summary>
    public CustomerDispositionType DispositionType { get; }
    /// <summary>외형·성향과 독립 추첨한 방문 속성. 가격·대기 수치를 암묵적으로 변경하지 않는다.</summary>
    public CustomerAttributes Attributes { get; }
    /// <summary>상품별 한 항목만 존재하는 구매 목록.</summary>
    public IReadOnlyList<CustomerOrderItem> Items { get; }

    /// <summary>생성기가 검증한 결과를 복사하여 외부 변경으로부터 보호한다.</summary>
    /// <param name="appearanceIdx">선정된 외형 ID.</param>
    /// <param name="dispositionIdx">선정된 성향 ID.</param>
    /// <param name="items">중복 없는 검증된 구매 목록.</param>
    /// <param name="priceTolerance">검증된 양수 가격 배율.</param>
    /// <param name="entryTextIdx">입장 대사.</param>
    /// <param name="regularSaleTextIdx">수락 대사.</param>
    /// <param name="discountSaleTextIdx">저가 판매 대사.</param>
    /// <param name="exploitativeSaleTextIdx">착취 판매 대사.</param>
    /// <param name="rejectTextIdx">거절 대사.</param>
    /// <param name="products">최종 목록의 상품·원가를 조회할 catalog.</param>
    /// <param name="getCurrentPrices">최신 현재가 조회 함수. 생성 시 가격표를 캡처하지 않는다.</param>
    /// <param name="dispositionType">검증 후 복사할 성향 타입.</param>
    /// <param name="attributes">검증 후 복사할 독립 속성.</param>
    /// <param name="regularPriceMinRate">생성기가 검증한 정가 인정 하한 배율.</param>
    /// <param name="regularPriceMaxRate">생성기가 검증한 정가 인정 상한 배율.</param>
    /// <param name="getSaleRestrictions">제출 시 지침 조회. null은 미연결.</param>
    /// <exception cref="ArgumentException">성향 타입 또는 속성이 유효하지 않음.</exception>
    internal CustomerVisit(uint appearanceIdx, uint dispositionIdx, List<CustomerOrderItem> items,
        int priceTolerance, uint entryTextIdx, uint regularSaleTextIdx, uint discountSaleTextIdx, uint exploitativeSaleTextIdx, uint rejectTextIdx,
        IReadOnlyDictionary<uint, ProductData> products, Func<IReadOnlyDictionary<uint, uint>> getCurrentPrices,
        CustomerDispositionType dispositionType, CustomerAttributes attributes,
        int regularPriceMinRate, int regularPriceMaxRate, Func<IReadOnlyList<SaleRestriction>> getSaleRestrictions)
    {
        CustomerProfileValidation.ValidateType(dispositionType);
        CustomerProfileValidation.ValidateAttributes(attributes);
        DispositionType = dispositionType;
        Attributes = attributes;
        AppearanceIdx = appearanceIdx;
        DispositionIdx = dispositionIdx;
        Items = new List<CustomerOrderItem>(items).AsReadOnly();
        PriceTolerance = priceTolerance;
        RegularPriceMinRate = regularPriceMinRate;
        RegularPriceMaxRate = regularPriceMaxRate;
        EntryTextIdx = entryTextIdx;
        this.regularSaleTextIdx = regularSaleTextIdx;
        this.discountSaleTextIdx = discountSaleTextIdx;
        this.exploitativeSaleTextIdx = exploitativeSaleTextIdx;
        this.rejectTextIdx = rejectTextIdx;
        this.products = products;
        this.getCurrentPrices = getCurrentPrices;
        this.getSaleRestrictions = getSaleRestrictions;
    }

    /// <summary>입장 피드백을 표시한 뒤 한 번만 가격 제안 대기로 전환한다.</summary>
    /// <exception cref="InvalidOperationException">이미 입장이 끝난 방문.</exception>
    public void BeginOffer()
    {
        if (State != CustomerState.Entering) throw new InvalidOperationException("이미 입장한 손님입니다.");
        State = CustomerState.AwaitingOffer;
    }

    /// <summary>생성된 방문을 대기열에 한 번 등록한다.</summary>
    /// <exception cref="InvalidOperationException">이미 활성화된 방문.</exception>
    internal void JoinQueue()
    {
        if (State != CustomerState.Entering) throw new InvalidOperationException("새 방문만 줄에 설 수 있습니다.");
        State = CustomerState.Queued;
    }

    /// <summary>대기열 소유자가 계산대로 인계하거나 이탈·영업 종료로 정리한다.</summary>
    /// <param name="abandoned">대기 만료 여부.</param>
    /// <param name="toCounter">계산대 인계 여부.</param>
    /// <exception cref="InvalidOperationException">대기 중이 아닌 방문.</exception>
    internal void LeaveQueue(bool abandoned, bool toCounter = false)
    {
        if (State != CustomerState.Queued) throw new InvalidOperationException("대기 중인 방문만 처리할 수 있습니다.");
        State = toCounter ? CustomerState.Entering : abandoned ? CustomerState.Abandoned : CustomerState.Departed;
    }

    /// <summary>전체 구매 목록의 총액을 한 번 판정한다. 입력 오류는 기회를 소모하지 않는다.</summary>
    /// <param name="offeredTotal">양의 정수 제안 총액.</param>
    /// <param name="saleItems">최종 상품·수량. 최초 희망 목록과 달라도 허용한다.</param>
    /// <returns>수락 여부.</returns>
    /// <exception cref="ArgumentOutOfRangeException">0 또는 음수 입력.</exception>
    /// <exception cref="InvalidOperationException">대기 상태가 아닌 방문.</exception>
    /// <exception cref="ArgumentException">빈 목록·잘못된 상품·수량·지침 조건 또는 중복 지침.</exception>
    /// <exception cref="OverflowException">합산 수량·기준액·허용액·원가 범위 초과.</exception>
    public bool SubmitOffer(long offeredTotal, IReadOnlyList<SaleItem> saleItems)
    {
        if (State != CustomerState.AwaitingOffer || isSubmitting) throw new InvalidOperationException("가격을 다시 제안할 수 없습니다.");
        if (offeredTotal <= 0) throw new ArgumentOutOfRangeException(nameof(offeredTotal));
        if (saleItems == null || saleItems.Count == 0) throw new ArgumentException("최종 판매 목록이 필요합니다.", nameof(saleItems));
        isSubmitting = true;
        try
        {
            var quantities = new Dictionary<uint, int>();
            foreach (var item in saleItems)
            {
                if (item.ProductId == 0 || item.Quantity <= 0 || !products.ContainsKey(item.ProductId))
                    throw new ArgumentException("상품 또는 수량 오류", nameof(saleItems));
                quantities.TryGetValue(item.ProductId, out int count);
                quantities[item.ProductId] = checked(count + item.Quantity);
            }
            // 조회 callback은 제출당 한 번만 호출한다. 검증 중 실패하면 공개 상태를 변경하지 않는다.
            var prices = getCurrentPrices() ?? throw new InvalidOperationException("현재가 조회 실패");
            var sold = new List<SoldItem>(quantities.Count);
            long reference = 0;
            foreach (var pair in quantities)
            {
                if (!prices.TryGetValue(pair.Key, out uint price) || price == 0)
                    throw new InvalidOperationException($"상품 {pair.Key}: 현재가 누락 또는 0");
                var product = products[pair.Key];
                if (product == null || product.Idx != pair.Key || product.CostPrice == 0)
                    throw new InvalidOperationException($"상품 {pair.Key}: 원가 또는 상품 참조 오류");
                sold.Add(new SoldItem(pair.Key, pair.Value, price, product.CostPrice));
                reference = checked(reference + (long)price * pair.Value);
            }
            long allowed = checked((long)decimal.Floor((decimal)reference * PriceTolerance / 1000m));
            var outcome = offeredTotal > allowed ? CustomerTradeOutcome.PaymentRefused
                : (decimal)offeredTotal * 1000m < (decimal)reference * RegularPriceMinRate ? CustomerTradeOutcome.DiscountSale
                : (decimal)offeredTotal * 1000m > (decimal)reference * RegularPriceMaxRate ? CustomerTradeOutcome.ExploitativeSale
                : CustomerTradeOutcome.RegularSale;
            // 정상 위반은 수락을 취소하지 않는다. 조회·검증 실패는 공개 상태 확정 전에 전파한다.
            bool evaluated = outcome != CustomerTradeOutcome.PaymentRefused && getSaleRestrictions != null;
            IReadOnlyList<SaleRestrictionViolation> violations = evaluated ? evaluateRestrictions(sold) : Array.Empty<SaleRestrictionViolation>();
            var result = new TransactionResult(outcome, offeredTotal, sold, evaluated, violations, DispositionType, Attributes);
            Result = result;
            AllowedTotal = allowed;
            OfferedTotal = offeredTotal;
            Outcome = outcome;
            State = outcome == CustomerTradeOutcome.PaymentRefused ? CustomerState.Rejected : CustomerState.Accepted;
            return State == CustomerState.Accepted;
        }
        finally { isSubmitting = false; }
    }

    /// <summary>결과 확인 후 퇴장한다. 결과·가격 snapshot은 유지한다.</summary>
    /// <exception cref="InvalidOperationException">거래 결과가 없거나 이미 퇴장한 방문.</exception>
    public void Depart()
    {
        if (State != CustomerState.Accepted && State != CustomerState.Rejected)
            throw new InvalidOperationException("거래 판정 후에만 퇴장할 수 있습니다.");
        State = CustomerState.Departed;
    }

    /// <summary>지침을 한 번 복사·검증한 뒤 최종 상품과 방문 속성의 AND 조건을 검사한다.</summary>
    /// <param name="sold">중복 합산·가격 검증을 마친 최종 판매 후보.</param>
    /// <returns>조건-상품별 한 건의 불변 값 기록. 빈 지침은 빈 결과다.</returns>
    /// <exception cref="InvalidOperationException">지침 목록 또는 상품 분류 조회 실패. 공급자의 예외도 그대로 전달한다.</exception>
    /// <exception cref="ArgumentException">지침 조건 오류 또는 중복.</exception>
    private IReadOnlyList<SaleRestrictionViolation> evaluateRestrictions(IReadOnlyList<SoldItem> sold)
    {
        var restrictions = new List<SaleRestriction>(getSaleRestrictions() ?? throw new InvalidOperationException("판매 지침 조회가 null을 반환했습니다."));
        var seen = new HashSet<(CustomerAttributes, ProductType)>();
        foreach (var restriction in restrictions)
        {
            restriction.Validate();
            if (!seen.Add((restriction.RequiredAttributes, restriction.ProductType)))
                throw new ArgumentException("동일 속성·분류의 판매 지침이 중복되었습니다.");
        }
        var violations = new List<SaleRestrictionViolation>();
        foreach (var item in sold)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || product == null || product.Idx != item.ProductId ||
                product.ProductType == ProductType.None || !Enum.IsDefined(typeof(ProductType), product.ProductType))
                throw new InvalidOperationException($"상품 {item.ProductId}: 판매 지침 분류 참조 오류");
            foreach (var restriction in restrictions)
                if ((Attributes & restriction.RequiredAttributes) == restriction.RequiredAttributes && product.ProductType == restriction.ProductType)
                    violations.Add(new SaleRestrictionViolation(restriction, item.ProductId, item.Quantity));
        }
        return violations;
    }
}

/// <summary>동일 상품의 구매를 하나로 표현하는 불변 상품 ID·수량 쌍.</summary>
public sealed class CustomerOrderItem
{
    /// <summary>입장 시 고정한 상품 단가. 원본 CSV 변경과 무관하다.</summary>
    public uint UnitPrice { get; }
    /// <summary>상품 데이터 ID.</summary>
    public uint ProductIdx { get; }
    /// <summary>해당 상품의 구매 개수.</summary>
    public int Quantity { get; }

    /// <summary>생성기가 검증한 상품과 수량을 보관한다.</summary>
    /// <param name="productIdx">선택된 상품 ID.</param>
    /// <param name="quantity">검증된 양수 수량.</param>
    /// <param name="unitPrice">검증된 상품 단가.</param>
    internal CustomerOrderItem(uint productIdx, int quantity, uint unitPrice)
    {
        ProductIdx = productIdx;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
