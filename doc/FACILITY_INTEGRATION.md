# 설비 구매·상품 해금 인계

기준: 2026-09-10, `Upgrade`. 세션·재정·손님·현재 가격표와 일일 정산의 설비 상점을 연결한다. 공유 GameUI와 독립 설비 프리팹만 변경하며 공유 Scene은 수정하지 않는다.

## 사용법과 책임

- 초기화한 세션을 사용하는 기존 `GameProgress`에서 `Start()` 이후 `TryPurchaseFacility(12001, out var result)`를 호출한다. 외부 입력은 설비 PK 하나이며 가격·날짜를 받지 않는다.
- `true/Purchased`: CSV 가격 즉시 차감, `PaidAmount`에 지출, 일반 업그레이드는 `ActivationDay`에 현재 경과일+1을 등록한다. 단계 상승은 현재 단계와 상점 잠금을 즉시 변경한다.
- `false/AlreadyOwned`: 재결제하지 않고 기존 활성일 반환. `false/InsufficientFunds`: 무변경, 활성일 null. `false/StageLocked`: 요구 단계 또는 순차 단계 조건을 만족하지 못한 상태로 무변경이다.
- 0·미등록 ID, 구매 재진입, 날짜 overflow는 예외다. 진행의 Initializing/Failed/Completed 상태는 구매를 거부한다. 공개 API의 그 밖 상태는 유지하되 실제 구매 UI는 일일 정산(`DayInProgress` + `Settlement`)에서만 노출한다.
- 재정 이벤트 구독자가 예외를 던지면 원래 예외를 전달한다. 이미 차감 완료했다면 보유도 유지한다. 실패를 보고 무조건 재결제하지 말고 `session.FacilityActivationDays`를 조회한다.
- 보유·활성일·가게 단계의 단일 권위는 세션 내부 FacilityService다. `CurrentStoreStage`, `IsFacilityOwned`, `IsFacilityUpgradeActive`, `IsFacilityEffectActive`, `TryGetFacilityActivationDay`, `IsFacilityActive`와 읽기 전용 `FacilityActivationDays`를 노출한다. 같은 세션의 표현 객체 교체는 보유를 유지하고 새 세션은 초기화한다. 저장 파일 복원은 미구현이다.

## 데이터와 배포 단위

| 설비 PK / 이름 | 임시 가격 | 종류·효과 | 해금 상품 PK |
|---|---:|---|---|
| 12001 식량 보관 선반 | 1000 | 상품 해금 / 요구 단계1 | 1005,1006 |
| 12002 약품 보관장 | 1200 | 상품 해금 / 요구 단계1 | 1013,1014 |
| 12003 공구대 | 1500 | 상품 해금 / 요구 단계2 | 1015,1016 |
| 12004 전력·통신 장비 | 1800 | 상품 해금 / 요구 단계2 | 1018,1019 |
| 12005 핵보호 물품 설비 | 2000 | 상품 해금 / 요구 단계3 | 1020,1021 |
| 12006 정밀 전자장비 보관장 | 2200 | 상품 해금 / 요구 단계3 | 1022,1023 |
| 12007 막대 | 800 | 편의성 / DividerBar / 요구 단계1 | 없음 |
| 12008 2단계 확장 | 1500 | 단계 상승 / 요구 단계1 → 목표 단계2 | 없음 |
| 12009 소팅 | 1200 | 편의성 / AutoSorting / 요구 단계2 | 없음 |
| 12010 3단계 확장 | 2500 | 단계 상승 / 요구 단계2 → 목표 단계3 | 없음 |
| 12011 청소기 | 1500 | 편의성 / Vacuum / 요구 단계3 | 없음 |

각 행은 일회성 업그레이드다. 현재 단계 1~3은 구매 가능 항목을 제한하며, 개별 구매 보유 여부와 별개다. 2단계 확장은 요구 단계1, 3단계 확장은 요구 단계2이며 현재 단계+1만 구매할 수 있다. 가격과 신규 상품 분류는 승인된 임시 값이다. 신규 의약품은 Medicine, 공구·전력·핵 보호 물품은 임시 DailyNecessities다. 기본 건전지1010과 설비 배터리1019는 서로 다른 상품이다.

- FacilityData: `idx:uint,nameidx:uint,purchase_price:long,upgrade_kind:FacilityUpgradeKind,required_store_stage:uint,effect_type:ConvenienceEffectType,target_store_stage:uint`. 종류12, PK12001~12011. `required_store_stage`는 1~3이며, 단계 상승만 `target_store_stage` 2 또는 3을 사용한다. 일반 업그레이드의 `target_store_stage=0`은 실제 가게 단계 0이 아닌 대상 없음 sentinel이다. 통합 checkout에는 ReputationBalance11도 함께 등록된다. enum 종료값은 자동 증가한다.
- ProductData에 `required_facility_idx:uint?` 추가. 빈 셀은 기본상품,0은 오류. 기본4개는1001/1004/1007/1010이며, 최종 상품은16행이고 비활성 행은 보존하지 않는다. 1022와1023은 설비12006을 요구한다.
- TextData에는 상품·설비 이름 8075(열화상 카메라), 8076(정밀 전자장비 보관장), 8077(막대), 8078(2단계 확장), 8079(소팅), 8080(3단계 확장), 8081(청소기)을 추가·연결한다. 실제 가격·전체 행은 [DATA_CATALOG.md](DATA_CATALOG.md) 참조.
- DTO·CSV·DataTableManager·CustomerCatalog를 함께 반영한다. 구형 Product header는 오류다. PK·가격·설비 FK·Text FK가 모두 검증되기 전 공개하지 않는다.
- 승인된 Addressables 연결: 기존 Default Local Group의 `FacilityData` address, 기존 `Datas` label, GUID `965dc884f32f1514c8fa36b64a8c93cb`. 기존 entry/group/schema는 변경하지 않았다.

## 생성·판매·가격표 계약

`CustomerProductAvailability`가 활성 여부·등장일·설비 활성 여부를 함께 판단한다. `DayProgress` 생성기와 `GameUIController`의 가격표 factory는 세션의 `IsFacilityActive`를 전달한다. 독립 호출에서 callback을 생략하면 설비 상품은 닫힌 상태로 처리한다. 편의성 효과는 동일한 세션의 `IsFacilityEffectActive` 조회를 사용한다.

방문 생성 시 판매 가능한 **전체 상품 PK**를 복사한다. 최종 제출은 이 범위 안에서 희망 목록과 다른 상품도 가능하다. 잠긴 상품이나 방문 이후 해금 상품은 거부하며 새 방문에서 새 후보를 사용한다. 선호 FK 검증은 전체 카탈로그 기준이므로 잠긴 선호 상품 자체는 오류가 아니다. 가격 이벤트 현재가 계산, 제출 시 가격 스냅샷, 지침 위반 기록·정산 계약은 유지한다.

## 검증과 남은 범위

최신 실행 기록은 [TESTING.md](TESTING.md)를 참조한다. 구매 성공·중복·잔액 부족·재진입·알림 예외·가격 복사·다음날 해금·판매 스냅샷·CSV/FK 실패와 실제 Datas 로딩을 검증한다.

설비 설치 시각화·애니메이션, 묶음/랜덤 판매, 재고, 저장 복원은 이번 범위가 아니다. API 성공은 화면/UX 또는 실제 씬 전환 성공을 의미하지 않는다. 개인 씬과 공유 Scene은 보존한다. GameUI 인스턴스는 원본 프리팹의 새 상점 연결을 받는다.

## 설비 UI 제작·병합 구현 (2026-09-10)

아래 UI 코드·독립 프리팹·정산 연결은 현재 구현이다. 현재 GameUI의 `GameUIController → Presenter.UpdateView(ViewData)`와 Presenter의 요청 event 패턴을 따른다. 11행 목록은 단계·종류·잠금·적용 대기·사용 중 상태를 snapshot으로 표시하며 ScrollRect 컨테이너를 사용한다.

### UI 목록

| 요소 / 실제 이름 | 표시·동작 | 필수 여부 |
|---|---|---|
| 진입 버튼 `FacilityOpenButton` | 일일 정산에서 날짜 완료 전 설비 목록 열기 | 필수 |
| 목록 패널 `FacilityShopPanel` | 제목, 보유금, 다음 영업일 적용 안내, 설비 목록, 닫기 | 필수 |
| 설비 행 `FacilityItem` | 요구 단계, 이름, 구매 가격, 상품/편의성/단계 효과, 보유 상태, 구매 버튼 | 필수 |
| 보유금 `BalanceText` | 현재 세션 잔액. 패널 진입·구매 결과·잔액 변경 때 갱신 | 필수 |
| 효과 안내 `UnlockProductsText` | 이 설비가 해금하는 상품 목록. 상품 FK로 조회 | 필수 |
| 적용 안내 `ActivationText` | 미보유는 다음 영업일부터 적용, 구매 후에는 실제 사용 가능 날짜 | 필수 |
| 상태 표시 `StatusText` | 구매 가능 / 잔액 부족 / 구매 완료·적용 대기 / 사용 중 | 필수 |
| 결과 안내 `FeedbackText` | 성공·중복 보유·잔액 부족·처리 오류. 색상 외 문구로도 구분 | 필수; 패널 내부 한 곳 재사용 |
| 닫기 버튼 `CloseButton` | 이전 화면으로 복귀. 날짜 진행·영업 시작·추가 결제를 하지 않음 | 필수 |
| 이미지 `Icon` | 설비 그림 | 후속. 현재 FacilityData에는 이미지 FK가 없어 필수 참조로 두지 않음 |
| 구매 확인 팝업 | 이름·금액 확인 후 구매 확정 | 미구현. 별도 확인 팝업 없이 행에서 구매 요청 |

상세 페이지 없이 각 행에서 이름·가격·해금 품목을 확인하고 구매한다. 기존 패널의 행 재사용 구조는 유지한다. 11개 행에 대한 가독성·스크롤·레이아웃은 이번 데이터 단계에서 확인하거나 수정하지 않는다.

### 프리팹과 코드 경계

```text
GameUI (기존 루트 / GameUIController)
└─ ProgressCanvas (기존 Canvas)
   ├─ SettlementPanel의 FacilityOpenButton
   └─ FacilityShopPanel (별도 프리팹 인스턴스, 기본 비활성)
      ├─ Background / TitleText / BalanceText / CloseButton
      ├─ ActivationGuideText
      ├─ Content (ScrollRect 목록)
      │  └─ FacilityItem × 데이터 행 수
      │     ├─ NameText / PriceText / UnlockProductsText
      │     └─ ActivationText / StatusText / PurchaseButton
      └─ FeedbackText
```

`FacilityShopPanel`과 `FacilityItem`은 독립 원본이며 GameUI에는 상점의 nested instance와 정산 진입 버튼·직렬화 참조만 추가했다. 설비 패널은 기존 Canvas·EventSystem을 사용하며 별도 Canvas나 EventSystem을 프리팹에 중복 생성하지 않는다. 표시 전용 Text/Image는 raycastTarget을 끄고, 열린 패널은 뒤쪽 거래·날짜 진행 버튼 입력을 막는다.

| 책임 | 구현 위치 / 역할 | 금지할 결합 |
|---|---|---|
| UI 작업자 | `Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab`, `FacilityItem.prefab`: 배치·폰트·버튼·직렬화 참조 | GameUI 원본·Scene·Addressables 직접 동시 편집, 서비스 직접 호출 |
| 프로그래머 | `Assets/Scripts/UI/Presenters/FacilityShopPresenter.cs`: ViewData 표시, 행 재사용, `OnPurchaseRequested(uint facilityIdx)`·닫기 요청 event | 차감·날짜 변경·보유 목록 소유 |
| 프로그래머 | `Assets/Scripts/UI/Contracts/FacilityUIContracts.cs`: 설비 표시 snapshot. 기존 공용 UIContracts 파일의 동시 편집을 피함 | FacilityData의 public setter를 UI 편집 모델로 사용 |
| 병합 담당 | 기존 `ProgressViewDataFactory`: 설비 목록을 이름·가격·효과·표시 상태로 변환 | UI에서 이름/문자열을 ID로 역추적 |
| 병합 담당 | 기존 `GameUIController`: Presenter 직렬화 연결, event 구독·해제, 기존 gameProgress로 구매 전달 | 다른 GameProgress/FacilityService 생성, 테스트용 reflection 재사용 |

별도 FacilityManager, event bus, interface/factory 계층, 설비별 Presenter 클래스는 필요 없다. `FacilityItemView`는 행 직렬화 참조와 단일 PK 클릭만 관리한다. Presenter는 행을 재사용하며 OnEnable/OnDisable에서 버튼·행 요청을 구독/해제한다.

### 표시 데이터와 요청 계약

- 행 snapshot은 `FacilityIdx:uint`, 표시 이름, `PurchasePrice:long`, 업그레이드 종류·요구 단계·효과·목표 단계, 해금 상품 표시 목록, 화면용 상태, 사용 가능 날짜를 제공한다. 금액은 입력 문자열에서 역산하지 않는다.
- 화면용 상태는 세션 단계·보유 여부·활성일·현재 잔액으로 계산한다. `FacilityPurchaseStatus`는 구매 요청 결과이므로 적용 대기/사용 중 표시 상태로 그대로 재사용하지 않는다.
- 화면 상태는 `StageLocked`, `Purchasable`, `InsufficientFunds`, `ActivationPending`, `Active`, `OwnedStageUpgrade`다. 잠긴 행도 해금 효과를 보여주며 버튼만 비활성화한다.
- 설비명은 FacilityData.NameIdx → TextData, 해금 목록은 `ProductData.RequiredFacilityIdx == FacilityIdx`이며 활성 데이터 행만 표시한다. 이름·가격·상품 ID를 프리팹에 하드코딩하지 않는다. 외형 Sprite는 현재 필수 데이터가 아니다.
- `ActivationDay`는 0부터 센 경과일이다. 화면의 DAY는 `ActivationDay + 1`이다. 예: DAY 1에 구매하면 결과 활성 경과일1, 화면에는 DAY 2부터 사용으로 표시한다.
- 패널 진입과 구매 완료 시 설비 행·단계·잔액을 새로 읽는다. 패널이 열린 동안 잔액이 바뀌면 같은 경로로 갱신한다. 기존 경제 표시도 갱신하되 일일 매출에 설비 비용을 더하거나 정산 지출을 UI에서 임의 재계산하지 않는다. UI는 단계·가격 규칙을 자체 판정하지 않고 factory snapshot을 표시한다.

구매 흐름은 `PurchaseButton → FacilityShopPresenter.OnPurchaseRequested(idx) → GameUIController → 기존 GameProgress.TryPurchaseFacility(idx, out result)`다. UI는 비용·날짜를 요청에 넣지 않는다. Presenter.UpdateView는 요청 event를 발생시키지 않는다.

처리 중에는 구매 버튼의 중복 입력을 막고, 정상 결과를 받은 뒤 상태를 다시 읽는다. UI 버튼 비활성화와 별개로 서비스의 중복·잔액 검증을 유지한다. 알림 예외는 이미 차감·보유가 완료된 경우도 있으므로 자동 재시도/환불하지 않고 세션을 다시 조회해 보유 상태를 표시한 뒤 오류를 알린다. 기존 GameUIController의 오류 처리 경계를 우회하지 않는다. 닫기·씬 종료 시 구독을 해제하고 재진입 후 listener가 중복 등록되지 않게 한다.

### 승인된 구매 화면 시점

채택한 배치는 **일일 정산 화면에서 날짜 완료 버튼을 누르기 전**이다. 이때 구매하면 다음 영업일에 사용할 수 있다. 패널을 닫은 뒤 기존 정산 완료 흐름을 계속한다. 별도 게임 진행 상태를 추가하지 않는 범위에서 먼저 연결한다.

현재 모든 영업일은 유지비 자동 차감과 정산 표시 후 `CompleteSettlement`에서 날짜를 완료하고 다음 날 PreOpen으로 이동한다. 따라서 UI를 다음 날 PreOpen에만 두면서 '곧 시작할 오늘부터 사용'으로 표시하면 현재 계약과 다르다. 예: DAY 2 PreOpen 구매는 DAY 3 적용이다.

유지비는 정산창을 열기 전에 이미 차감된다. 정산 중 설비 구매는 남은 잔액을 사용하며 유지비를 다시 계산하지 않는다. 영업 전·영업 중에는 진입 버튼을 노출하지 않는다. 닫기는 날짜를 변경하지 않으며 이후 기존 정산 완료 흐름을 계속한다.

패널이 열린 동안 뒤쪽 정산 CanvasGroup의 interactable/blocksRaycasts를 끄고 GameInputRouter를 일시 비활성화한다. 선택된 버튼을 해제하며 Controller 진행 요청도 차단한다. 닫을 때 입력을 복원하되 기술 오류 잠금은 해제하지 않는다. 구매 중 재진입은 latch로 막고 finally에서 해제·상태 재조회한다. 잔액 이벤트는 열린 패널 수명에만 구독하며 구매 중에는 중첩 렌더를 미룬다. 정산 잔액도 갱신하지만 매출·비용·순익은 기존 확정 집계를 그대로 사용한다.

### total_merge 통합 (2026-09-09)

- 설비 `65888e1` + 명성 `6976218` 이력을 통합하고 MainScene에 공유 GameUI를 연결했다. PreOpenPanel 최신 배치를 보존하며 설비 모달을 유지한다.
- 정산의 명성 피드백은 설비 구매 후에도 유지한다. 날짜 완료 시 설비 활성과 명성 변화가 각각 한 번 반영된다. 현재 명성·적용일 marker·명성 로그는 GameSessionManager 수명으로 유지한다.
- 명성 생성 가중치의 손님 생성기 연결·정식 이미지·저장 복원은 별도다. 검증 근거는 TESTING.md를 따른다.

### 배포와 검증 기준

1. UI/프로그래머: 위 snapshot·구매/닫기 event·직렬화 필드 계약을 맞추고 별도 설비 패널·행 프리팹을 제작한다. .meta/GUID를 포함하며 GameUI 루트는 수정하지 않는다.
2. 병합 담당: 완성된 프리팹과 스크립트를 확인한 뒤 한 명이 GameUIController/ProgressViewDataFactory/GameUI.prefab 연결을 맡는다. 승인된 구매 시점에 진입 버튼을 연결하며 기존 참조·GUID를 보존한다. 개인 Local 씬/코드/테스트 표시를 공유 자산으로 옮기지 않는다.
3. API 검증: ID 전달·1회 차감, 부족/중복/예외 후 상태 재조회, 같은 날 대기/다음 날 활성 표시, 날짜 표기, 재진입 listener 중복 없음. 기존 NUnit/Test Runner 경로를 사용한다.
4. 수동 검증: 최종 사용자 확인에서 11행 정보 가독성, 긴 이름·금액, 클릭/스크롤/뒤쪽 입력 차단과 닫기 복귀를 확인한다. 이번 구현에서는 세부 플레이테스트를 별도로 확대하지 않는다.

기존 PlayMode 테스트 assembly에는 승인된 `Unity.ugui`, `Unity.TextMeshPro` 참조만 추가했다. 새 package나 runtime assembly는 없다. 기존 LocalDebug는 개인 씬에 유지하고 공유 프리팹으로 옮기지 않았다. 최신 자동 검증 XML/로그는 [TESTING.md](TESTING.md)를 따른다. UI의 최종 사용성 승인은 별도다. 공용 변경의 작업 branch 리뷰·기본 branch 통합 절차는 AGENTS.md와 기존 통합 규칙을 따른다.
