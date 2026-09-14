# 설비 업그레이드 단계 진행·3화면 UI 구현 계획

작성일: 2026-09-14
조사 기준: 현재 체크아웃 `Sound` 작업 트리
문서 목적: 이 문서만 새 Codex/Luna 작업에 제공해도 현재 구현을 재조사하고, 설비 진행 조건과 UI를 안전하게 구현·검증할 수 있는 실행 명세를 제공한다.

## 1. 작업 목표

1. 설비 업그레이드 구매 가능 여부에서 날짜 경과 조건을 사용하지 않는다.
2. 현재 단계의 일반 업그레이드를 전부 보유해야 다음 단계 확장을 구매할 수 있게 한다.
3. 3단계 일반 업그레이드를 전부 보유해야 시민권을 구매할 수 있게 한다.
4. 하나의 전체 목록 창을 단계별 3개 화면으로 분리한다.
   - 1단계 화면: 1단계 일반 업그레이드 목록 + 하단 `2단계 업그레이드` 버튼
   - 2단계 화면: 2단계 일반 업그레이드 목록 + 하단 `3단계 업그레이드` 버튼
   - 3단계 화면: 3단계 일반 업그레이드 목록 + 하단 `시민권 구매` 버튼
5. 설비 상점을 열 때 현재 가게 단계에 대응하는 화면만 표시한다. 단계 확장 구매가 성공하면 열린 상태에서 곧바로 다음 단계 화면으로 전환한다.

## 2. 시작 전 필수 절차

새 작업자는 구현 전에 다음을 수행한다.

1. `doc/WORK_RULES.md`와 `doc/INDEX.md`를 읽는다.
2. 코드·Prefab·CSV를 수정하므로 다음 문서를 함께 읽는다.
   - `doc/CODING_RULES.md`
   - `doc/FACILITY_INTEGRATION.md`
   - `doc/CITIZENSHIP_ENDING.md`
   - `doc/SETTLEMENT_TECHNICAL.md`
   - `doc/DATA_RULES.md`
   - `doc/CSV_RULES.md`
   - `doc/PREFAB_RESOURCE_RULES.md`
   - `doc/TESTING.md`
3. `git branch --show-current`, `git status --short`, `git stash list`로 기준 상태를 기록한다.
4. 현재 조사 시점에는 사운드 및 UI 관련 다수의 수정·미추적 파일이 이미 존재한다. 특히 `Assets/Scripts/Scene/GameUIController.cs`, `doc/INDEX.md`도 dirty 상태다. 이를 사용자 작업으로 취급하고 덮어쓰기·원복·stash·광범위 stage를 하지 않는다. 실제 작업 시작 시 최신 상태를 다시 확인하고 필요한 경우 별도 작업 브랜치/워크트리를 사용한다.
5. 아래의 “현재 구현”은 2026-09-14 정적 조사 결과다. 최신 코드가 달라졌다면 차이를 먼저 기록한 뒤 같은 불변 조건을 유지하는 최소 수정안을 적용한다.

## 3. 요구사항 해석과 확정 범위

### 3.1 날짜 조건 제거의 정확한 의미

현재 `FacilityData.csv`에는 구매 가능 날짜 컬럼이 없고 `FacilityService.TryPurchase`도 날짜를 구매 잠금 조건으로 검사하지 않는다. 따라서 “특정 날짜가 지나야 업글 가능”이라는 구매 조건은 현재 구현에 이미 존재하지 않는다.

현재 날짜와 관련된 동작은 서로 다른 두 가지다.

- 일반 설비의 **효과 활성 시점**: 구매일이 아니라 다음 영업일부터 활성화된다.
- 상품의 `available_day`: 현재 실제 `ProductData.csv`의 모든 행은 `0`이며, 상품 후보 시스템의 일반 계약으로만 남아 있다.

이 작업의 확정 해석은 다음과 같다.

- 설비 **구매 가능 여부**에는 날짜 조건을 새로 추가하지 않고, 남아 있는 날짜 구매 잠금이 발견되면 제거한다.
- 일반 설비의 “구매 다음 영업일부터 효과 적용” 계약은 구매 가능 날짜 조건이 아니므로 유지한다.
- `ProductData.available_day`, 일일 상품 종류 수, 감독관 이벤트 날짜, 31일차 최종 확인 마감은 이 작업에서 제거하지 않는다.
- 날짜 기반 조건 제거를 이유로 `ActivationDay`, `FacilityActivationDays`, 다음날 상품 해금 테스트를 삭제하거나 즉시 활성로 바꾸지 않는다.

사용자가 “구매 후 다음날 활성”까지 없애고 모든 효과를 즉시 적용하라는 뜻으로 후속 확정할 경우에만 별도 요구사항으로 다룬다. 그 경우 손님 후보 snapshot, 당일 가격표, 편의 설비 수명, 감독관의 전날 보유 snapshot까지 영향이 확장되므로 이 계획에 묶어 임의 변경하지 않는다.

### 3.2 단계별 “모두 구매”의 정의

현재 데이터 기준 일반 업그레이드는 `ProductUnlock` 또는 `Convenience` 종류이며, 해당 단계 화면에 표시되는 완료 대상이다. `StoreStage`와 `Citizenship`은 완료 대상에 포함하지 않는다.

| 단계 | 모두 보유해야 하는 일반 업그레이드 | 완료 후 구매 가능한 진행 항목 |
|---|---|---|
| 1단계 | 12001 식량 보관 선반, 12002 약품 보관장, 12007 막대 | 12008 2단계 확장 |
| 2단계 | 12003 공구대, 12004 전력·통신 장비, 12009 소팅 | 12010 3단계 확장 |
| 3단계 | 12005 핵보호 물품 설비, 12006 정밀 전자장비 보관장, 12011 청소기 | 12012 시민권 |

이 ID 목록은 현재 데이터 검증용 기대값일 뿐 런타임 판정에 하드코딩하지 않는다. 런타임은 `RequiredStoreStage == 현재 단계`이며 `UpgradeKind`가 `ProductUnlock` 또는 `Convenience`인 모든 행을 데이터에서 찾아 보유 여부를 검사한다.

### 3.3 잔액과 선행 조건의 우선순위

- 이미 보유한 항목은 언제나 `AlreadyOwned`를 반환하고 재결제하지 않는다.
- 선행 업그레이드가 미완료이면 잔액과 무관하게 선행 조건 잠금 상태를 반환한다.
- 선행 조건을 만족한 뒤에만 잔액 부족 또는 구매 가능을 판정한다.
- UI 버튼 비활성화는 표현일 뿐이다. 조작된 PK 직접 호출도 `FacilityService`에서 같은 조건으로 거부해야 한다.
- 단계 확장과 시민권 구매 성공은 기존처럼 즉시 보유/적용한다. 일반 업그레이드는 기존처럼 다음 영업일부터 효과가 활성화된다.

## 4. 현재 구현 조사 결과

### 4.1 데이터와 도메인

- `Assets/Datas/FacilityData.csv`
  - 총 12행: 상품 해금 6, 편의성 3, 단계 확장 2, 시민권 1.
  - `required_store_stage`는 일반 설비의 화면 단계 및 현재 단계 잠금에 사용된다.
  - 시민권 12012는 현재 `required_store_stage=1`이라 현 상태에서는 1단계부터 구매 가능하다.
- `Assets/Scripts/Commons/Data/FacilityData.cs`
  - `FacilityUpgradeKind`: `ProductUnlock`, `Convenience`, `StoreStage`, `Citizenship`.
  - 시민권 검증이 현재 `RequiredStoreStage == 1`을 강제한다.
- `Assets/Scripts/Facility/FacilityService.cs`
  - 가격·종류·요구 단계·목표 단계·활성일을 복사해 단일 세션 상태를 소유한다.
  - 현재 구매 잠금은 `CurrentStoreStage < RequiredStoreStage`와 단계 확장의 `TargetStoreStage == CurrentStoreStage + 1`뿐이다.
  - 현재 단계의 다른 일반 업그레이드를 모두 샀는지는 검사하지 않는다. 따라서 12008을 바로 사고, 이어 12010도 바로 살 수 있다.
  - 시민권도 별도 선행 설비 검사 없이 살 수 있다.
  - 일반 설비는 `purchaseElapsedDay + 1`, 단계 확장과 시민권은 `purchaseElapsedDay`를 활성일로 기록한다.
- `Assets/Scripts/Manager/GameSessionManager.cs`와 `Assets/Scripts/Progress/GameProgress.cs`
  - 상위 구매 가능 진행 상태와 시민권 최종일 정책을 검사한 뒤 `FacilityService` 경로를 사용한다.
  - 기존 31일차 정산 중 구매 및 최종 결과 고정 계약은 보존해야 한다.

### 4.2 표시 데이터와 UI

- `Assets/Scripts/UI/ProgressViewDataFactory.cs`
  - 현재 모든 설비를 PK 순으로 하나의 `FacilityShopViewData.Items`에 넣는다.
  - 잠금은 가게 단계와 순차 목표 단계만 계산한다.
  - 단계 완료 선행 조건을 알지 못한다.
- `Assets/Scripts/UI/Contracts/FacilityUIContracts.cs`
  - `FacilityDisplayState`는 `StageLocked`, `Purchasable`, `InsufficientFunds`, `ActivationPending`, `Active`, `OwnedStageUpgrade`만 제공한다.
  - 일반 행과 진행 항목을 분리하지 않은 단일 목록 계약이다.
- `Assets/Scripts/UI/Presenters/FacilityShopPresenter.cs`
  - 단일 `content`에 모든 행을 생성·재사용한다.
  - 상점 전체의 구매/닫기 이벤트만 제공한다.
- `Assets/Scripts/UI/Presenters/FacilityItemView.cs`
  - 모든 종류를 같은 행 Prefab으로 표시한다.
  - 단계 확장·시민권도 일반 설비 행과 같은 `PurchaseButton`을 사용한다.
- `Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab`
  - `Window`, 제목, 보유금, 안내, 피드백, 하나의 `Viewport/Content`, 닫기 버튼으로 구성된다.
  - `FacilityShopPresenter`와 `ScrollRect`가 루트에 연결돼 있다.
- `Assets/Prefabs/GameUI/Facility/FacilityItem.prefab`
  - 이름·가격·분류/해금·활성·상태·구매 버튼을 가진 재사용 행이다.
- `Assets/Prefabs/GameUI/GameUI.prefab`
  - `FacilityShopPanel` 원본 하나를 nested instance로 두고 `GameUIController.facilityShopPresenter` 한 필드에 연결한다.
- `Assets/Scripts/Scene/GameUIController.cs`
  - 정산 중 상점을 열고, 입력 차단·잔액 구독·구매 latch·닫기 복구를 소유한다.
  - `refreshFacilityShop`이 전체 목록 snapshot 하나를 presenter에 전달한다.
  - 현재 열기 피드백은 시민권 가격/부족액을 항상 우선 표시한다.

### 4.3 기존 테스트

- `Assets/Tests/EditMode/FacilityTests.cs`
  - 현재 `StagePurchaseUnlocksSequentiallyAndPublishesOnce`는 1단계 일반 업그레이드 없이 12008 구매 성공, 이어 12010 구매 성공을 기대하므로 새 요구사항에 맞게 바꿔야 한다.
  - `CitizenshipActivatesImmediatelyAndIsUnique`는 1단계 시민권 fixture를 사용한다.
  - 다음날 활성, 원자성, 재진입, 알림 예외, CSV 구조, view snapshot 테스트가 있다.
- `Assets/Tests/PlayMode/GameSessionApiTests.cs`
  - 다수 테스트가 준비 과정 없이 12008→12010을 구매한다. 새 규칙 아래 필요한 일반 업그레이드를 먼저 구매하도록 fixture 자금과 순서를 수정해야 한다.
  - `FacilityPresenterRequestsAndLongValues`, `FacilityControllerPurchaseAndModalBoundaries`, `FacilityControllerNotificationFailurePreservesPurchase`가 단일 패널 구조에 의존한다.
  - 시민권·31일 최종 확인·엔딩 테스트는 시민권 구매 전에 3단계 완료 상태를 준비해야 한다.

## 5. 목표 도메인 계약

### 5.1 단일 권위 판정

`FacilityService`가 다음 판정을 소유한다.

```text
일반 업그레이드(ProductUnlock/Convenience)
  현재 가게 단계 >= required_store_stage이면 구매 가능

단계 확장(StoreStage, 목표 N+1)
  현재 가게 단계 == N
  AND 목표 단계 == N+1
  AND required_store_stage == N
  AND N단계 일반 업그레이드를 모두 보유

시민권(Citizenship)
  현재 가게 단계 == 3
  AND 3단계 일반 업그레이드를 모두 보유
  AND GameSessionManager의 기존 정산/최종일 구매 정책 통과
```

`활성`이 아니라 `보유`를 선행 조건으로 사용한다. 즉 같은 정산 화면에서 1단계 일반 설비를 모두 구매하면, 일반 설비의 효과가 다음날 활성되기 전이어도 2단계 확장 버튼은 즉시 활성화된다. 이는 요구문의 “모두 산 다음”을 따른다.

### 5.2 구현 형태

`FacilityService`에 데이터 기반 helper를 둔다. 이름은 현재 스타일에 맞춰 조정할 수 있으나 역할은 다음과 같아야 한다.

- `areRegularUpgradesOwned(uint storeStage)`: 해당 단계의 `ProductUnlock/Convenience` 설비가 하나 이상 존재하며 모두 `activationDays.ContainsKey`인지 검사.
- `canPurchaseProgression(FacilityUpgradeKind kind, uint requiredStage, uint targetStage)`: 단계 확장/시민권의 선행 조건 검사.

생성자에서 판정에 필요한 메타데이터를 불변 복사한다. 매 구매마다 외부 DTO를 다시 읽지 않는다. 12행 규모이므로 단순 순회로 충분하며, 별도 manager·전역 cache·event bus를 추가하지 않는다.

### 5.3 구매 결과 상태

기존 `StageLocked`를 모든 선행 조건 실패에 그대로 쓰면 “현재 가게 단계”라는 UI 문구가 틀려진다. 다음 중 첫 번째 방식을 채택한다.

- 권장/확정: `FacilityPurchaseStatus`와 `FacilityDisplayState`에 선행 구매 미완료를 뜻하는 명시적 상태(`PrerequisiteLocked` 등)를 추가한다.
- 현재 단계가 낮거나 순차 단계가 아닌 경우는 기존 `StageLocked`를 유지한다.
- 서비스 결과와 화면 상태 이름을 가능한 한 같은 의미로 맞추되, 구매 결과 enum을 화면 상태 enum으로 직접 재사용하지 않는다.

상위 `GameUIController` 피드백은 다음을 구분한다.

- 단계 잠김: `현재 가게 단계에서 잠긴 업그레이드입니다.`
- 선행 미완료: `현재 단계의 설비를 모두 구매해야 진행할 수 있습니다.`
- 잔액 부족, 이미 보유, 구매 성공, 처리 오류: 기존 의미 유지.

### 5.4 데이터 검증 강화

`FacilityData.csv`의 시민권 12012 `required_store_stage`를 3으로 변경하고 `FacilityData.Validate`의 시민권 요구 단계도 3으로 바꾼다. 이는 시민권이 3단계 화면에 속한다는 데이터 의미를 맞추기 위함이다.

`FacilityDataTable` 검증에는 다음 구조를 추가한다.

- 단계 1, 2, 3마다 일반 업그레이드가 최소 1개 존재한다.
- 목표 단계 2와 3의 `StoreStage` 행은 각 하나다(기존 검증 유지).
- 시민권 행은 정확히 하나다(현재 `DataTableManager`/서비스 검사와 중복 책임을 확인해 가장 낮은 적절한 위치 한 곳에 유지).
- 시민권은 요구 단계 3이다.

단계별 일반 설비 개수 3개 자체를 런타임 필수 계약으로 고정하지 않는다. 데이터가 늘어나면 새 행도 자동으로 “모두 구매” 조건에 포함되어야 한다. 단, 실제 CSV 회귀 테스트에서는 현재 기대값 3/3/3과 위 ID 구성을 검사한다.

## 6. 목표 UI 계약

### 6.1 화면 선택

상점 진입 시 `CurrentStoreStage`가 선택 화면의 단일 권위다.

| CurrentStoreStage | 표시 화면 | 일반 목록 | 하단 진행 항목 |
|---:|---|---|---|
| 1 | Stage1Panel | required stage 1의 ProductUnlock/Convenience | target stage 2의 StoreStage |
| 2 | Stage2Panel | required stage 2의 ProductUnlock/Convenience | target stage 3의 StoreStage |
| 3 | Stage3Panel | required stage 3의 ProductUnlock/Convenience | Citizenship |

이전 단계 화면으로 돌아가거나 미래 단계 화면을 미리 보는 네비게이션은 만들지 않는다. 이미 구매한 이전 단계 항목은 현재 단계 화면에 나타나지 않는다. 단계 확장 구매 직후 `CurrentStoreStage`를 다시 조회하여 열린 상점을 다음 화면으로 바꾼다.

### 6.2 Presenter/ViewData 구조

단일 presenter를 유지하고 그 내부에 세 단계 panel 참조를 두는 최소 변경을 권장한다. `GameUIController`는 여전히 하나의 상점 modal만 열고 닫는다.

목표 계층 예시:

```text
FacilityShopPanel (FacilityShopPresenter, modal root)
└─ Window
   ├─ 공통 Header: TitleText / BalanceText / ActivationGuideText / CloseButton
   ├─ Stage1Panel
   │  ├─ Viewport/Content (1단계 일반 행)
   │  └─ ProgressionArea (2단계 업그레이드 정보/상태/구매 버튼)
   ├─ Stage2Panel
   │  ├─ Viewport/Content (2단계 일반 행)
   │  └─ ProgressionArea (3단계 업그레이드 정보/상태/구매 버튼)
   ├─ Stage3Panel
   │  ├─ Viewport/Content (3단계 일반 행)
   │  └─ ProgressionArea (시민권 정보/상태/구매 버튼)
   └─ FeedbackText
```

“창 3개”는 위처럼 한 modal 아래 상호 배타적인 3개 panel로 구현한다. 세 개의 별도 Canvas/EventSystem이나 `GameUIController` 필드 세 개를 만들 필요가 없다. 정말 별도 Prefab 원본 3개가 아트 작업상 필요하더라도 공통 루트 presenter가 한 번에 하나만 활성화하고 구매/닫기 이벤트를 한 경로로 전달해야 한다.

`FacilityShopViewData`는 다음을 명시적으로 분리한다.

- `CurrentStoreStage`
- `CurrentBalance`
- `RegularItems`: 현재 단계의 일반 업그레이드만
- `ProgressionItem`: 현재 단계의 하단 단계 확장 또는 시민권 정확히 하나
- 필요하면 `CompletedRegularCount`, `RequiredRegularCount` 또는 완성된 진행 안내 문자열

Factory가 필터링·선행 조건 상태 계산을 담당하고 presenter는 목록을 재필터링하거나 종류를 판정하지 않는다. 이름/가격/PK를 prefab에 하드코딩하지 않는다.

### 6.3 하단 진행 버튼 상태

하단 진행 항목은 처음부터 보이되 조건에 따라 비활성화한다.

- 일반 업그레이드 미완료: 비활성, `N/N 구매 완료`와 “현재 단계 설비를 모두 구매하세요” 표시.
- 일반 업그레이드 완료 + 잔액 부족: 비활성, 가격과 부족 상태 표시.
- 일반 업그레이드 완료 + 잔액 충분: 활성.
- 구매 처리 중: 상점의 모든 구매·닫기 입력 잠금.
- 단계 확장 구매 성공: 새 단계 panel로 즉시 전환하고 새 snapshot 표시.
- 시민권 구매 성공: 3단계 화면 유지, 버튼 비활성/보유 완료 표시. 기존 31일 최종 확인 계약 유지.

진행 버튼도 일반 행과 동일하게 PK만 `OnPurchaseRequested(uint)`로 전달한다. 별도 `UpgradeToNextStage()`나 `BuyCitizenship()`처럼 가격·단계를 UI에서 정하는 API를 만들지 않는다.

### 6.4 일반 행과 안내 문구

- 현재 단계가 화면 자체로 드러나므로 일반 행 이름 앞의 `[N단계]` 표시는 중복이다. 제거 여부는 아트 확인 사항이지만, 제거하더라도 데이터/판정에는 영향이 없어야 한다.
- 일반 설비의 `DAY N부터 사용`과 상단 “다음 영업일부터 적용” 안내는 유지한다.
- 단계 확장은 `구매 즉시 다음 단계 해금`, 시민권은 `구매 즉시 보유 · 31일차 최종 확인 전까지 구매 가능`을 유지한다.
- 상점 최초 피드백을 시민권 가격만 강조하는 현재 방식은 1·2단계 화면과 맞지 않는다. 단계별 진행 안내로 바꾸고, 시민권 가격/부족액은 3단계 화면에서만 표시한다.

### 6.5 입력·수명 계약

현재 `GameUIController`의 다음 동작을 그대로 보존한다.

- 정산 연출 완료 후, 날짜 완료 전만 상점 진입 가능.
- modal이 열리면 뒤 정산 `CanvasGroup`과 `GameInputRouter` 차단.
- 열린 동안에만 잔액 변경 구독.
- 구매 latch로 중복 입력 방지; 오류가 발생해도 상태 재조회 후 표시.
- 닫기는 날짜·영업 상태를 바꾸지 않고 `NotifyFacilityClosed`로 복귀.
- 단계 panel 전환은 modal 닫기/재열기로 구현하지 않고 같은 열린 수명에서 처리.

## 7. 파일별 변경 계획

### 7.1 필수 코드·데이터

| 파일 | 변경 내용 |
|---|---|
| `Assets/Scripts/Facility/FacilityService.cs` | 단계별 일반 설비 보유 판정, 단계 확장/시민권 선행 조건 강제, 새 구매 결과 상태 반환, 오래된 “고단계도 선행 구매를 요구하지 않는다” XML 설명 정정 |
| `Assets/Scripts/Facility/FacilityPurchaseResult.cs` | 필요 시 `PrerequisiteLocked` 구매 상태 추가 |
| `Assets/Scripts/Commons/Data/FacilityData.cs` | 시민권 요구 단계 3 검증으로 변경, 주석 갱신 |
| `Assets/Scripts/Commons/Data/FacilityDataTable.cs` | 단계별 일반 행/진행 행 구조 검증 보강 |
| `Assets/Datas/FacilityData.csv` | 12012 `required_store_stage` 1→3 |
| `Assets/Scripts/UI/Contracts/FacilityUIContracts.cs` | 선행 잠금 표시 상태, 현재 단계 일반 목록과 하단 진행 항목을 분리한 snapshot 계약 |
| `Assets/Scripts/UI/ProgressViewDataFactory.cs` | 현재 단계 필터링, 진행 항목 선택, 데이터 기반 완료 수/선행 잠금/잔액 상태 계산 |
| `Assets/Scripts/UI/Presenters/FacilityShopPresenter.cs` | 3개 panel 중 하나만 활성, 단계별 row pool 또는 현재 panel row pool 갱신, 하단 진행 버튼 요청 연결 |
| `Assets/Scripts/UI/Presenters/FacilityItemView.cs` | 일반 행 표시만 담당하도록 정리하거나 공용 표시 기능 유지. 하단 진행용 view가 별도라면 중복 로직 최소화 |
| `Assets/Scripts/Scene/GameUIController.cs` | 단계별 최초 피드백, 새 snapshot 전달, 단계 구매 직후 화면 전환, 새 결과 상태 문구 |

하단 진행 영역이 `FacilityItemView`와 구조가 크게 다르면 `Assets/Scripts/UI/Presenters/FacilityProgressionView.cs`를 새로 둘 수 있다. 단 한 곳에서만 쓰는 과도한 interface/factory 계층은 만들지 않는다.

### 7.2 Prefab

| 파일 | 변경 내용 |
|---|---|
| `Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab` | Stage1/2/3 panel, 각 목록 content, 하단 진행 영역, presenter 직렬화 참조 구성. 한 번에 하나만 활성 |
| `Assets/Prefabs/GameUI/Facility/FacilityItem.prefab` | 현재 단계 일반 행에 맞춘 문구/레이아웃. 기존 GUID 유지 가능하면 원본을 수정 |
| `Assets/Prefabs/GameUI/GameUI.prefab` | 기존 nested FacilityShopPanel이 원본 변경을 상속하는지 확인. 불필요한 override 제거는 금지하고, 새 필드 연결이 필요할 때만 최소 수정 |

새 진행 영역 Prefab을 독립 원본으로 만들 경우 본 파일과 Unity 생성 `.meta`를 함께 포함한다. Addressables 등록은 현재 설비 nested prefab 계약상 필요하지 않으며, 필요성이 새로 발견되면 승인 없이 그룹을 편집하지 않는다.

### 7.3 테스트·문서

| 파일 | 변경 내용 |
|---|---|
| `Assets/Tests/EditMode/FacilityTests.cs` | 선행 미완료/완료, 보유 기준, 잔액 우선순위, 시민권 3단계 조건, 데이터 기반 추가 행, snapshot 분리 테스트 |
| `Assets/Tests/PlayMode/GameSessionApiTests.cs` | 기존 단계/시민권 fixture를 새 순서로 준비, 3화면 상호 배타 표시, 진행 버튼 전환, controller 입력/오류 회귀 |
| `doc/FACILITY_INTEGRATION.md` | 날짜 조건 해석, 단계별 완료 게이트, 3화면 UI, 새 상태/API/Prefab 계약으로 현재 명세 갱신 |
| `doc/CITIZENSHIP_ENDING.md` | 시민권 구매 조건을 “3단계 + 3단계 일반 업그레이드 전부 보유”로 갱신 |
| `doc/DATA_CATALOG.md` | FacilityData 시민권 요구 단계 및 UI snapshot 최신 계약 반영 |
| `doc/TESTING.md` | 실제 수행한 테스트 수·결과·로그만 기록 |

문서 색인에 새 영구 명세를 추가하는 것이 아니라 기존 권위 문서를 갱신한다. 이 계획 문서는 작업 완료 후에도 설계 근거/인계 기록으로 `doc/work/`에 유지할 수 있다.

## 8. 구현 순서

1. 최신 dirty 상태와 관련 호출자를 다시 조사하고 작업 파일 allowlist를 확정한다.
2. EditMode 테스트에 새 도메인 기대값을 먼저 추가한다.
   - 12008은 1단계 일반 3개 중 하나라도 미보유면 거부.
   - 12010은 2단계 일반 3개 중 하나라도 미보유면 거부.
   - 시민권은 3단계 일반 3개 중 하나라도 미보유면 거부.
   - 모두 보유하면 같은 정산/같은 경과일에도 진행 구매 가능.
3. `FacilityService`의 데이터 기반 선행 조건을 구현한다. 차감·보유 예약·예외 원자성 순서는 유지한다.
4. 시민권 데이터/검증을 요구 단계 3으로 맞추고 실제 CSV 구조 테스트를 갱신한다.
5. ViewData factory가 현재 단계의 `RegularItems`와 `ProgressionItem`을 생성하도록 바꾸고 EditMode snapshot 테스트를 통과시킨다.
6. Presenter와 Prefab을 3개 panel 구조로 바꾼다. Prefab 변경은 Unity Editor 또는 프로젝트 승인 도구를 사용해 직렬화 참조와 `.meta`를 보존한다.
7. `GameUIController`를 새 snapshot/피드백에 연결하고 기존 modal 수명·잔액 갱신·오류 처리를 보존한다.
8. PlayMode 테스트의 직접 단계/시민권 구매 fixture를 새 순서로 갱신한다. 단지 테스트를 통과시키려고 서비스 검사를 우회하지 않는다.
9. 관련 명세를 현재 계약으로 갱신한다.
10. Unity reimport/compile, EditMode, PlayMode, 실제 화면 수동 검증 순으로 수행한다.

## 9. 상세 테스트 계획

### 9.1 EditMode 도메인

다음 케이스를 독립적으로 검증한다.

1. 새 세션의 현재 단계는 1이고 12008은 `PrerequisiteLocked`다.
2. 1단계 일반 설비를 2/3만 보유하면 12008은 계속 잠긴다.
3. 3/3을 모두 보유하면 효과 활성일이 다음날이어도 12008을 같은 날 구매할 수 있다.
4. 12008 구매 즉시 단계 2가 되고, 12010은 2단계 일반 설비 전부 구매 전까지 잠긴다.
5. 2단계 일반 설비 3/3 뒤 12010을 구매하면 즉시 단계 3이 된다.
6. 시민권은 단계 1·2에서 거부된다.
7. 단계 3이어도 3단계 일반 설비 2/3이면 시민권이 잠긴다.
8. 3단계 일반 설비 3/3이면 시민권을 구매하고 즉시 보유한다.
9. 잠금 실패는 잔액·보유·단계를 변경하거나 이벤트를 발행하지 않는다.
10. 선행 완료 후 잔액이 부족하면 `InsufficientFunds`, 충분하면 1회 차감한다.
11. 구매 이벤트/재정 구독자 예외 시 기존 원자성 계약을 유지한다.
12. 같은 단계 일반 행을 fixture에 하나 더 추가하면 그 행도 자동으로 완료 조건에 포함된다.
13. 날짜 값을 0, 중간값, 큰 값으로 바꿔도 구매 게이트 결과는 동일하다. 일반 효과 활성일 계산만 기존대로 달라진다.

### 9.2 EditMode ViewData/CSV

1. 단계 1 snapshot은 일반 12001/12002/12007과 진행 12008만 포함한다.
2. 단계 2 snapshot은 일반 12003/12004/12009와 진행 12010만 포함한다.
3. 단계 3 snapshot은 일반 12005/12006/12011과 진행 12012만 포함한다.
4. 진행 항목은 정확히 하나이며 일반 목록에 중복되지 않는다.
5. 완료 수 0/3, 2/3, 3/3과 진행 상태가 일치한다.
6. 일반 완료 전에는 진행 가격을 감당해도 선행 잠금이다.
7. 일반 완료 후 잔액 경계 `price-1`, `price`, 큰 long 값이 올바르다.
8. 보유 일반 설비의 `ActivationPending/Active`는 기존 날짜 의미를 유지한다.
9. 실제 CSV에서 시민권 요구 단계 3, 단계별 일반 3개, 단계 진행 항목 1개를 확인한다.
10. 단계별 일반 업그레이드가 0개이거나 진행 항목이 누락/중복된 잘못된 CSV를 거부한다.

### 9.3 PlayMode UI/Controller

1. 새 세션 상점 진입 시 Stage1Panel만 활성이고 Stage2/3Panel은 비활성이다.
2. 1단계 진행 버튼은 처음부터 보이지만 비활성이다.
3. 일반 행을 전부 구매하면 같은 열린 modal에서 진행 버튼이 활성화된다.
4. 2단계 확장 구매 후 modal을 닫지 않고 Stage2Panel만 활성화된다.
5. 같은 흐름으로 Stage3Panel까지 이동한다.
6. 3단계 완료 전 시민권 버튼은 비활성, 완료 후 활성이다.
7. 시민권 구매 뒤 버튼은 보유 완료 상태이며 재결제되지 않는다.
8. 잔액 변경 이벤트가 현재 panel의 행과 진행 버튼을 갱신한다.
9. 구매 중 중복 클릭과 닫기 입력이 차단된다.
10. 구매 알림 예외 뒤 실제 보유/단계 상태를 재조회하고 올바른 panel을 표시한다.
11. 닫기 후 정산 버튼과 `GameInputRouter`가 복구되고 listener가 중복되지 않는다.
12. 31일차 최종 확인에서 상점으로 돌아와 조건을 충족한 시민권을 구매한 뒤 Good 결과로 진행할 수 있다.
13. 조건 미충족/잔액 부족으로 시민권을 못 산 경우 기존 Bad 확인 흐름이 유지된다.

### 9.4 수동 화면 검증

자동 테스트와 별도로 다음을 실제 Game View에서 확인한다.

- 각 단계 화면에 정확히 3개 일반 행과 하단 진행 버튼이 보이는가.
- 세 panel이 겹치거나 동시에 raycast를 받지 않는가.
- 16:9 기준 긴 이름, 1,000,000 G, 해금 상품 목록, 상태 문구가 잘리지 않는가.
- 스크롤 영역이 하단 진행 버튼·헤더·피드백을 가리지 않는가.
- 비활성 진행 버튼이 단순히 회색일 뿐 아니라 잠금 이유/완료 수를 텍스트로 전달하는가.
- 단계 구매 직후 화면 전환이 명확하고 선택 포커스가 숨은 버튼에 남지 않는가.
- 마우스 휠/드래그, 키보드 Enter, 뒤쪽 정산 버튼 차단과 닫기 복귀가 정상인가.

## 10. 완료 조건

다음이 모두 충족돼야 완료다.

- 설비 구매 가능 판정에 날짜 게이트가 없다.
- 일반 설비의 기존 다음 영업일 효과 활성 계약은 유지된다.
- 각 단계의 일반 업그레이드를 모두 보유하기 전 단계 확장/시민권을 API 직접 호출로도 살 수 없다.
- 1→2→3→시민권 순서가 데이터 기반으로 강제된다.
- 현재 단계에 맞는 panel 하나만 표시되고 단계 구매 즉시 다음 panel로 전환된다.
- 시민권의 기존 가격, 31일차 구매 마감, 최종 확인/Good·Bad 엔딩 계약이 유지된다.
- Unity compile error 0.
- 관련 EditMode/PlayMode 테스트가 1건 이상 실제 실행되고 실패·skip·미완료가 0이다. 전체 실행을 했다면 정확한 총 실행 수를 기록한다.
- Prefab missing script/reference 0, 새 asset과 `.meta` 짝 및 GUID 중복 없음.
- 실제 Game View 수동 검증 결과와 미확인 UX를 구분해 보고한다.
- `git diff --check` 통과, allowlist 밖 사용자 변경 보존, stage/commit/push 여부를 각각 보고한다.

## 11. 제외 범위와 주의점

- 저장/불러오기 구현, 시민권 가격 조정, 엔딩 내용/아트 변경은 제외한다.
- 일반 설비 효과를 구매 당일 즉시 활성화하지 않는다.
- 상품 `available_day`, 날짜별 일일 상품 종류 수, 감독관 이벤트 날짜를 제거하지 않는다.
- 단계별 설비 ID를 서비스나 presenter에 하드코딩하지 않는다.
- UI 비활성화만으로 도메인 구매를 보호하지 않는다.
- 새 FacilityManager, 전역 event bus, 세 개의 독립 Canvas/EventSystem, 설비별 presenter 클래스를 만들지 않는다.
- 테스트 편의를 위해 잔액·보유 상태를 제품 코드에서 직접 주입하는 우회 API를 추가하지 않는다.
- `GameUIController.cs`와 `doc/INDEX.md`는 조사 시점에 이미 dirty였다. 관련 줄을 수정할 때 기존 변경을 먼저 대조하고 충돌을 사용자 변경으로 보존한다.

## 12. 구현 완료 보고 형식

최종 보고에는 다음을 포함한다.

1. 변경 파일과 각 파일의 역할.
2. 최종 단계별 완료 대상과 데이터 기반 판정 방식.
3. 날짜 조건에서 제거/유지한 범위.
4. 3개 panel과 현재 단계 선택/전환 방식.
5. 실행한 Unity compile, EditMode, PlayMode, 수동 화면 검증의 정확한 결과.
6. 미실행·미확인 항목과 남은 위험.
7. 기존 dirty 파일 보존 여부 및 commit/push/merge 여부.
