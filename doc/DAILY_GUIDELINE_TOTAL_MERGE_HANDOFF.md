# DailyInstruction 브랜치 최종 통합 인계 지침

이 문서는 `DailyInstruction` 브랜치의 일일 상품·일일지침·거래·정산 구현을 `total_merge`에 통합할 때 기능과 직렬화 연결이 누락되지 않도록 하는 인계 문서다. 실제 병합은 이 문서만으로 수행하지 않으며, 공통 통합 기준인 [`BRANCH_INTEGRATION_RULES.md`](BRANCH_INTEGRATION_RULES.md), 프로젝트 공통 규칙인 [`AGENTS.md`](../AGENTS.md), 데이터 규격인 [`DATA_RULES.md`](DATA_RULES.md)·[`CSV_RULES.md`](CSV_RULES.md), Prefab 규격인 [`PREFAB_RESOURCE_RULES.md`](PREFAB_RESOURCE_RULES.md)를 함께 적용한다.

## 1. 기준과 범위

작성 시점의 기준은 다음과 같다.

| 항목 | 값 |
|---|---|
| 소스 브랜치 | `DailyInstruction` |
| 소스 기준 HEAD | `380a71ed0c48c95a538404f25962627b91d03db7` (`일일 지침 시스템 구현`) |
| 통합 대상 | `total_merge` 또는 병합 시 지정된 최신 대상 ref |
| 공통 조상 | `935cf9333d75c54ac61f258d0af22680c378def5` |
| 소스 기능 커밋 순서 | `32a22a9` → `b401732` → `380a71e` |

병합 직전에 소스와 대상의 최신 SHA를 다시 기록한다. 위 SHA는 현재 구현을 식별하기 위한 기준일 뿐, 이후 커밋이 생겼을 때 자동으로 최신 기준으로 간주하지 않는다. 현재 브랜치에서 이미 검증된 가중치 조정 등 후속 변경이 있다면 최종 소스 HEAD에 포함됐는지 확인한다.

통합 기준은 `BRANCH_INTEGRATION_RULES.md`의 다음 원칙을 따른다.

- UI·Prefab·Scene은 지정된 첫 번째 자산 기준 브랜치의 계층과 직렬화 연결을 보존한다.
- 코드·API·시스템은 공통 조상 이후 호출자와 데이터 흐름을 비교해 최신 유효 구현을 기능 단위로 통합한다.
- CSV·enum·DTO·parser·loader는 파일별 최신본을 섞지 않고 하나의 데이터 계약으로 통합한다.
- `.meta`, GUID, fileID, Addressables address는 임의 재생성·치환하지 않는다.
- 충돌이 없더라도 의미 충돌, Inspector override, Addressables 누락을 별도로 검사한다.

이 문서는 병합 실행이나 push 승인을 의미하지 않는다. 원본 브랜치 수정·삭제, reset/clean, 강제 push, 임의 stash는 수행하지 않는다.

## 2. 통합 후 반드시 보존할 기능

### 2.1 일일 등장 상품과 가격 snapshot

- 상품 원본은 총 16종이며, 설비 `12001`~`12006`이 각각 상품 해금군을 제공한다. `ProductData.RequiredFacilityIdx`와 `FacilityData`의 상품 해금 계약을 유지한다.
- `CustomerProductAvailability`가 `is_available`, `available_day`, 설비 활성 여부를 함께 검사한다. 설비 요구 상품은 해당 설비가 활성화되기 전 후보에 포함하지 않는다.
- `DailyProductSelector`의 최대 종류 수는 경과일 기준으로 고정한다.
  - 경과일 `0`~`8` (표시 1~9일차): 최대 4종
  - 경과일 `9`~`18` (표시 10~19일차): 최대 6종
  - 경과일 `19` 이상 (표시 20일차 이후): 최대 8종
- 같은 날짜의 추첨은 비복원이며 결과를 상품 ID 순으로 정렬한다. 후보가 최대치보다 적으면 가능한 수만 선택한다. 특정 해금 단계 상품을 강제로 한 개 포함하는 규칙은 없다.
- 상품 등장 상대 가중치는 현재 구현을 그대로 유지한다.
  - 기본 상품: `100`
  - 1단계 해금 상품: `220`
  - 2단계 해금 상품: `280`
  - 3단계 해금 상품: `350`
- `PriceEventScheduler.CreateDay(..., dailyProductIds)`는 선택된 상품만 `DailyPriceState.Prices`에 넣는다. 전체 상품 가격표로 되돌리지 않는다.
- 신문·라디오 이벤트는 기존 방식대로 날짜별 예약·가중치 선택을 유지하고, 라디오 효과는 영업 중 한 번만 기본가격에서 재계산한다. 이전 가격에 누적 곱연산하지 않는다.

주요 코드:

- `Assets/Scripts/Customer/CustomerProductAvailability.cs`
- `Assets/Scripts/Events/DailyPriceState.cs`
- `Assets/Scripts/Events/PriceEventScheduler.cs`
- `Assets/Scripts/Manager/GameSessionManager.cs`

### 2.2 일일지침 데이터와 생성

`DailyGuidelineData.csv`는 예전의 고정 일차·문구·대상 상품 스키마를 사용하지 않는다. 최종 스키마는 다음과 같다.

```text
idx,rule_type,allowed_quantity,penalty_amount
13001,1,0,500
13002,2,1,500
```

- `13001`: `SaleProhibited`, 거래당 허용 수량 `0`, 위반 1건 벌금 `500`
- `13002`: `QuantityLimited`, 거래당 허용 수량 `1`, 위반 1건 벌금 `500`
- 두 규칙 유형은 모두 존재해야 하며, `DailyGuidelineDataTable`이 중복 유형·누락 유형·잘못된 수량·비양수 벌금을 거부한다.
- 표시 일차별 지침 수는 경과일 기준으로 1~9일차 0개, 10~19일차 1개, 20일차 이후 2개다.
- 생성 대상 상품은 그 날짜의 `DailyPriceState.Prices.Keys`만 사용한다. 지침 대상 상품이 당일 등장 상품 밖으로 나가면 생성과 UI snapshot을 실패시킨다.
- 상품 충돌 방지 규칙은 지침의 손님 속성이나 규칙 유형과 무관하게 `TargetProductIdx`가 같으면 충돌로 본다. 20일차 이후 지침 2개는 서로 다른 상품을 대상으로 해야 한다.
- 같은 규칙 유형·같은 지침이 다른 날짜에 다시 등장하는 것은 허용한다.
- 현재 손님 대상 추첨은 조합 속성을 생성하지 않는다.
  - 모든 손님 (`CustomerAttributes.None`): 상대 가중치 `70` (전체 70%)
  - 남자·여자·아이·성인·노인 단일 속성: 각각 상대 가중치 `6` (각 6%)
  - 성별+연령 조합은 후보에서 제거됐다.
- `None`은 평가식에서 모든 완전한 손님 속성과 일치한다. `Normal` 특수 속성은 지침 후보로 사용하지 않는다.

주요 코드:

- `Assets/Scripts/Commons/Data/DailyGuidelineData.cs`
- `Assets/Scripts/Commons/Data/DailyGuidelineDataTable.cs`
- `Assets/Scripts/Commons/SaleRestriction.cs`의 `DailyGuideline`, `DailyGuidelineEvaluator`, `DailyGuidelineExclusionAllowance`, `DailyGuidelineGenerator`

### 2.3 거래 판정과 지침 위반

- `GameSessionManager.DailyGuidelines`가 하루 동안 유지되는 지침 snapshot의 권위다. UI 재진입이나 손님 생성 때 다시 추첨하지 않는다.
- `DayProgress.createCustomer()`는 현재 가격과 동일한 세션의 지침 snapshot을 `CustomerGenerator`에 callback으로 전달한다.
- `CustomerVisit.SubmitOffer()`는 최종 판매 목록을 기준으로 지침을 평가한다.
  - 결제가 거절된 거래는 지침을 평가하지 않으며 벌금도 없다.
  - 수락된 거래에서 조건과 상품 수량이 맞으면 지침별로 위반을 기록한다.
  - 한 거래에서 두 지침을 동시에 어기면 위반 2건으로 보존한다.
  - 정상 거래가 지침을 어겨도 거래 수락 자체를 취소하지 않는다.
  - `GetGuidelineAllowedExclusionQuantity()`는 판매 금지 또는 1개 제한에 맞춰 정상적인 상품 제외량을 계산한다.
- `TransactionResult`는 거래 결과와 함께 `WereDailyGuidelinesEvaluated`, `DailyGuidelineViolations`, `DailyGuidelinePenaltyAmount`를 보존한다. 벌금은 이 단계에서 잔액을 직접 차감하지 않는다.
- `DailyAggregationService`는 성립 거래에서 지침 위반 건수·상세·벌금 예정액을 누적하고, `DailyAggregationResult`에서 지침별 요약을 만든다.

주요 코드:

- `Assets/Scripts/Customer/CustomerGenerator.cs`
- `Assets/Scripts/Customer/CustomerVisit.cs`
- `Assets/Scripts/Progress/DayProgress.cs`
- `Assets/Scripts/Finance/TransactionResult.cs`
- `Assets/Scripts/Finance/DailyAggregationService.cs`
- `Assets/Scripts/Finance/DailyAggregationResult.cs`

### 2.4 통합 정산·미납 유예

하루 종료 시 금액 흐름은 다음 하나의 계약으로 유지한다.

```text
오늘 지불액 = 오늘 유지비 + 오늘 지침 벌금
총 납부 필요액 = 기존 미납액 + 오늘 지불액
```

- `GameSessionManager.EndTradingDay()`가 위 금액을 계산하고 `MaintenanceService.TryPaySettlement()`로 전액 자동 납부를 시도한다.
- 부분 납부는 없다. 잔액이 총 납부 필요액보다 작으면 이번 납부액은 `0`이고 금액 전체가 미납으로 남는다.
- 최초 미납 시 미납 원금과 함께 표시 일차+3의 고정 유예 종료일을 만든다.
- 유예 중 다시 미납하면 오늘 지불액만 기존 미납에 더하고 기존 유예 종료일은 연장하지 않는다. 따라서 미납 회차가 겹쳐도 종료일은 하나의 고정 기한으로 유지된다.
- 미납을 전액 납부하면 미납액과 유예 기준을 모두 지운다. 이후 다시 미납하면 새로 3일 유예를 시작한다.
- `DailySettlementResult`는 유지비, 당일 지침 벌금, 기존 미납, 총액, 실제 납부액, 잔여 미납, 유예 종료일·잔여일, 게임오버 조건 flag를 함께 전달한다.
- 현재 구현 범위에서는 게임오버 상태로의 Scene 전환이나 실제 게임오버 처리는 구현하지 않는다. `IsGameOverConditionMet`는 정산 결과 표시용 조건 flag다.
- `SettlementDebtState`, `DailySettlementResult`, `GameSessionManager`의 미납·지침 snapshot은 런타임 상태다. 이 기능에서 저장·복원 데이터나 migration을 추가하지 않는다.

주요 코드:

- `Assets/Scripts/Finance/MaintenanceService.cs`
- `Assets/Scripts/Finance/EconomyRuntime.cs`
- `Assets/Scripts/Finance/EconomyQueryService.cs`
- `Assets/Scripts/Finance/FinanceChangeReason.cs`
- `Assets/Scripts/Manager/GameSessionManager.cs`

### 2.5 손님·명성 데이터 계약

일일지침이 손님 속성을 평가하려면 손님 구성 snapshot이 완전해야 한다.

- `CustomerComposition`이 외형·성향·성별·연령·특수 속성, 가격 규칙, 구매 목록, 생성 당시 판매 가능 상품 목록을 복사한다.
- `CustomerCompositionSelector`가 명성 구간별 성향 가중치를 선택하고 성별을 방문 간 교대한다. 실제 방문에는 성별·연령·`Normal` 특수 속성이 각각 하나씩 존재한다.
- `CustomerVisit`과 `TransactionResult`는 거래 시점의 성향 타입과 `CustomerAttributes`를 보존한다. 원본 CSV를 다시 조회해 판정을 바꾸지 않는다.
- `ReputationBalanceData.csv`의 최종 헤더는 `normal_weight`, `price_sensitive_weight`, `wealthy_weight`, `hasty_weight`, `poor_weight`를 포함하며 다섯 가중치의 합은 `1000`이다. 현재 행의 값과 `CustomerDispositionData.csv`의 `minimum_price_tolerance`를 함께 보존한다.
- `CustomerDispositionData.csv`는 6001~6015 행과 `minimum_price_tolerance` 열을 포함한다. 6008 이후의 추가 가격 민감·가난 성향 행을 기존 6001~6007 행만 남긴 파일로 되돌리지 않는다.
- 같은 성향 타입의 여러 행은 명성 판정에 사용하는 가격 규칙이 같아야 한다. 선호 상품·대사 차이만 행별로 다를 수 있다.

주요 코드:

- `Assets/Scripts/Commons/CustomerProfileTypes.cs`
- `Assets/Scripts/Customer/CustomerComposition.cs`
- `Assets/Scripts/Customer/CustomerCompositionSelector.cs`
- `Assets/Scripts/Customer/Data/CustomerDispositionData.cs`
- `Assets/Scripts/Finance/ReputationDispositionRules.cs`
- `Assets/Scripts/Finance/ReputationTransactionClassifier.cs`
- `Assets/Scripts/Finance/Data/ReputationBalanceData.cs`
- `Assets/Datas/Customer/CustomerDispositionData.csv`
- `Assets/Datas/ReputationBalanceData.csv`
- `Assets/Datas/TextData.csv` (성향 표시명·문구 추가/수정 포함)

## 3. UI·Prefab 통합 규칙

### 3.1 영업 시작 패널

최종 영업 시작 화면은 가격과 지침을 같은 snapshot에서 표시한다.

- `PreOpenGuidelineViewData`는 표시 일차, 최대 8개 상품, 최대 2개 지침, 영업 시작 후 다시 볼 수 없다는 단일 안내 문구, 영업 시작 가능 여부를 가진다.
- 상품 슬롯 한 칸은 `아이콘(Image) + 상품 이름(TMP) + 당일 가격(TMP)` 세트다. 사용하지 않는 슬롯은 비활성화한다.
- `PreOpenPanelPresenter`에는 최대 8개 상품 슬롯과 최대 2개 지침 텍스트 슬롯이 직렬화되어야 한다. 상품·지침 목록은 Presenter가 임의로 재계산하지 않는다.
- 지침 표시 문구는 `ProgressViewDataFactory.formatGuideline()`이 생성한다. `None`은 “모든 손님”, 단일 속성은 해당 성별 또는 연령으로 표시한다. 판매 금지와 1개 제한 문구를 구분한다.
- 지침 재확인 안내는 하나만 남긴다. 예전 가격표 재확인 문구와 중복된 구형 지침 문구를 되살리지 않는다.
- `ProgressViewDataFactory.CreatePriceListText()`는 기존 테스트·호환 호출 때문에 남아 있을 수 있지만, 신규 영업 시작 UI의 권위 경로는 `CreatePreOpenGuidelineViewData()`다.

### 3.2 정산 패널

`SettlementPanel.prefab`과 `DailySettlementPresenter`는 정산 snapshot의 필드를 모두 표시할 수 있어야 한다.

- 판매 수입, 총 지출, 순이익, 정산 후 잔액, 명성 변화, 성공·거절·이탈 통계
- 오늘 유지비, 오늘 지침 벌금, 지침별 위반 횟수·벌금 상세
- 기존 미납액, 총 납부 필요액, 실제 납부액, 남은 미납액
- 유예 종료일·남은 유예일 또는 납부 완료·게임오버 조건 문구

정산 UI는 금액을 다시 계산하지 않고 `DailySettlementViewData`를 그대로 표시한다. `GameUIController`는 `DayProgress.SettlementStarted`에서 받은 최종 `DailySettlementResult`를 ViewData로 변환해 Presenter에 전달한다.

### 3.3 부모 Prefab 보호

일일지침 UI 작업의 의도는 작은 패널 Prefab 단위 변경이다.

- `PreOpenPanel.prefab`와 `SettlementPanel.prefab`의 계층·직렬화 참조를 우선 반영한다.
- `GameUI.prefab`의 nested instance를 일괄 덮어쓰거나 Prefab을 unpack하지 않는다. 대상 브랜치의 부모 계층·기존 override를 보존한 뒤 child Prefab 변경이 정상 상속되는지 확인한다.
- `GameUI.prefab`와 `OperatingPanel.prefab`의 변경은 이전 통합 정산·시설 기능 커밋(`b401732`)에서 발생한 범위다. 일일지침 커밋(`380a71e`)의 child Prefab 변경과 한 덩어리로 취급하지 말고, 대상 브랜치의 최신 parent UI와 의미적으로 통합한다.
- `PreOpenPanel`의 임시 `DebugDay10`, `DebugDay20` 버튼은 `UNITY_EDITOR || DEVELOPMENT_BUILD`에서만 노출되고 코드로 구독한다. 일반 빌드에 노출하거나 persistent `UnityEvent`를 추가하지 않는다. 수동 검증이 끝나면 제거할지 유지할지 별도 결정한다.
- `Assets/Scenes/Local/`이나 `Assets/Scripts/Local/`의 개인 실험 결과를 공유 Prefab·Scene에 연결하지 않는다.

주요 UI 파일:

- `Assets/Scripts/UI/Contracts/UIContracts.cs`
- `Assets/Scripts/UI/ProgressViewDataFactory.cs`
- `Assets/Scripts/UI/Presenters/PreOpenPanelPresenter.cs`
- `Assets/Scripts/UI/Presenters/DailySettlementPresenter.cs`
- `Assets/Scripts/Scene/GameUIController.cs`
- `Assets/Prefabs/GameUI/PreOpenPanel.prefab`
- `Assets/Prefabs/GameUI/SettlementPanel.prefab`
- `Assets/Prefabs/GameUI/GameUI.prefab` (이전 정산·시설 통합 범위를 별도 비교)
- `Assets/Prefabs/GameUI/OperatingPanel.prefab` (이전 통합 범위를 별도 비교)

## 4. 데이터·Addressables 통합 주의점

- `Assets/Datas/DailyGuidelineData.csv`의 새 헤더와 두 행을 보존한다. 대상 브랜치의 예전 `day,nameidx,descriptionidx,target_product_idx,param_value` 스키마와 부분 병합하지 않는다.
- `Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset`에 다음 Addressables 항목이 있어야 한다.
  - address: `DailyGuidelineData`
  - label: `Datas`
  - source GUID: `Assets/Datas/DailyGuidelineData.csv.meta`의 현재 GUID
- `DataTableManager`는 `Datas` 라벨의 `TextAsset` 위치를 읽은 뒤 `asset.name`으로 테이블을 분류한다. CSV가 파일로 존재하는 것만으로는 충분하지 않으며 Addressables 등록이 빠지면 10일차 생성 시 “일일지침 생성에 사용할 규칙 설정이 없습니다.” 오류가 발생한다.
- Addressables 설정·group·address는 보호 변경이다. 기존 group/label 정책을 확인하고, 병합 후 Editor 설정과 실제 Player content catalog를 각각 확인한다. Player build를 할 때는 새 catalog/content build가 필요하다.
- `ReputationBalanceData.csv`, `CustomerDispositionData.csv`, `TextData.csv`는 DTO·DataTable·FK 검증과 함께 통합한다. 헤더만 한쪽에서 가져오거나 행만 다른 쪽에서 가져오지 않는다.
- 모든 변경 CSV의 `.meta`는 1:1로 보존하고 PK·종류 ID·FK를 재번호화하지 않는다.

## 5. 권장 통합 순서

1. 대상과 소스의 checkout, dirty/untracked/stash, Unity 미저장 Scene·Prefab을 기록한다. 사용자 변경을 임의 stash하거나 덮어쓰지 않는다.
2. 병합 시점의 소스·대상 SHA와 공통 조상을 기록한다. 대상 `total_merge`의 최신 MainScene·부트스트랩·Inspector 이벤트 변경을 먼저 읽는다.
3. 데이터 계약을 먼저 맞춘다: `DailyGuidelineData` 새 스키마, Reputation/CustomerDisposition 헤더, Product·Facility FK, Addressables `Datas` 등록을 함께 반영한다.
4. 공용 코드 의존 순서로 통합한다: `Commons/Data`·`SaleRestriction` → 상품/손님 selector·visit → price/session → transaction/aggregation/maintenance → `GameProgress`·`DayProgress` → UI ViewData/Presenter/Controller.
5. `PreOpenPanel.prefab`과 `SettlementPanel.prefab`을 Unity Editor에서 별도로 열어 직렬화 참조를 반영한다. parent `GameUI.prefab`은 target 기준 계층을 유지하고 nested override를 확인한다.
6. 테스트와 문서를 함께 반영한다. 특히 `DailyGuidelineTests`가 구형 CSV schema를 기대하지 않는지, 기존 고객·명성·정산 테스트의 fixture가 새 FK를 로드하는지 확인한다.
7. 아래 정적·컴파일·최소 실행 확인을 끝낸 뒤에만 승인된 경로를 stage한다. commit·push는 별도 지시 범위에서만 수행한다.

기능 커밋을 선택적으로 옮겨야 한다면 의존성 때문에 `32a22a9` → `b401732` → `380a71e` 순서를 사용한다. 단, target에 이미 더 최신 구현이 있으면 파일 전체를 덮어쓰지 말고 기능 계약별로 비교한다.

## 6. 병합 후 확인 체크리스트

### 6.1 Git·정적 검사

- [ ] `git diff --check` 통과
- [ ] conflict marker(`<<<<<<<`, `=======`, `>>>>>>>`) 없음
- [ ] 중복 `DailyGuideline`, `DailyGuidelineRuleType`, `DailyGuidelineGenerator`, `SettlementDebtState` 타입/API 없음
- [ ] 소스의 핵심 파일이 의도 없이 삭제되지 않았고, 구형 호환 API를 제거할 경우 모든 호출자와 테스트를 확인함
- [ ] 변경 파일과 허용 범위가 일치함. 개인 `Local` 파일·Temp 결과·임의 ProjectSettings가 포함되지 않음
- [ ] 모든 수정 asset에 본 파일과 `.meta`가 함께 존재함
- [ ] GUID가 경로 이동이나 충돌로 바뀌지 않았고, 동일 GUID가 여러 경로에 나타나지 않음
- [ ] Prefab의 script GUID·fileID·array 순서가 유지되고 missing script/reference가 없음

### 6.2 CSV·DataTable·Addressables

- [ ] `DailyGuidelineData.csv` 헤더가 `idx,rule_type,allowed_quantity,penalty_amount`임
- [ ] PK `13001`, `13002`가 중복 없이 존재하고 두 규칙 유형·허용 수량·벌금 `500`이 맞음
- [ ] `DailyGuidelineDataTable`이 두 행을 로드하고 두 rule type 조회에 성공함
- [ ] Reputation/CustomerDisposition 헤더와 DTO mapping이 일치하고 가중치 합·FK 검증이 통과함
- [ ] 상품 16종·설비 12종 해금 FK와 `ProductUnlockStage` 계산이 통과함
- [ ] `DailyGuidelineData.csv`의 Addressables address가 `DailyGuidelineData`, label이 `Datas`임
- [ ] Addressables에서 `Datas` TextAsset locations에 `DailyGuidelineData`가 포함됨
- [ ] address·label 중복이 없고, Player build 대상이면 content catalog를 새로 생성함

### 6.3 Unity import·컴파일·자동 테스트

- [ ] 통합 checkout을 연 Unity가 `6000.3.18f1` 기준으로 reimport를 끝냄
- [ ] compile error 0, 신규 제품 Console error 0임. 기존 오류와 신규 오류를 구분해 기록함
- [ ] `DailyGuidelineTests` 전체 통과
- [ ] 상품 추첨, 가격 이벤트, Customer/Transaction, DailyAggregation/Maintenance, Reputation 관련 기존 EditMode suite 통과
- [ ] 테스트 실행 수가 0이 아니며 skip·미완료를 PASS로 처리하지 않음
- [ ] PlayMode를 실행하지 않았다면 STATIC PASS 또는 PARTIAL로 보고하고, UI·입력 성공을 자동 테스트 성공으로 확대하지 않음

### 6.4 UI·직렬화 확인

- [ ] `PreOpenPanel.prefab`의 상품 슬롯 배열이 최대 8칸이며 각 칸의 root/icon/name/price 참조가 모두 연결됨
- [ ] 지침 텍스트 슬롯이 최대 2칸이며 공지 텍스트는 하나만 연결됨
- [ ] `SettlementPanel.prefab`의 정산 필드가 `DailySettlementPresenter`의 유지비·벌금·미납·유예 참조와 일치함
- [ ] `GameUI.prefab` parent의 기존 nested instance와 override가 보존되고, child Prefab 변경이 상속됨
- [ ] `GameUIController`의 `preOpenPanelPresenter`, `dailySettlementPresenter`, 패널 root·버튼 참조가 누락되지 않음
- [ ] 임시 날짜 점프 버튼은 Editor/Development에서만 보이고 일반 빌드에는 비활성임
- [ ] 버튼에 중복 persistent listener가 없고 Controller의 구독·해제가 한 번씩만 일어남

### 6.5 최소 실행 시나리오

사용자 수동 확인 또는 승인된 PlayMode에서 다음 결과를 확인한다.

| 시나리오 | 기대 결과 |
|---|---|
| InitScene → HubScene → MainScene 진입 | DataTable·Resource·GameSession manager가 한 번만 초기화되고 Console 오류가 없음 |
| 1일차 영업 전 | 상품 최대 4종, 지침 0개, 상품별 아이콘·이름·가격 표시 |
| 10일차 점프 후 영업 전 | 상품 최대 6종, 지침 정확히 1개, 지침 대상 상품이 당일 상품 목록에 존재 |
| 20일차 점프 후 영업 전 | 상품 최대 8종, 지침 정확히 2개, 두 지침의 대상 상품이 서로 다름 |
| 반복 UI 재진입 | 같은 날짜의 상품·가격·지침이 재추첨되지 않음 |
| 판매 금지/1개 제한 위반 | 거래는 수락될 수 있고 위반 1건마다 벌금 500이 누적됨 |
| 한 거래에서 두 지침 위반 | 위반 건수 2, 각 지침 상세가 정산에 남음 |
| 거래 결렬 | 지침 평가와 지침 벌금이 발생하지 않음 |
| 유지비만 있는 정산 | 총 납부 필요액이 유지비와 같고 자동 전액 납부됨 |
| 유지비+지침 벌금 정산 | 두 금액이 하나의 총 납부액으로 합산됨 |
| 잔액 부족 | 부분 납부 없이 실제 납부액 0, 미납액 전체 이월, 3일 유예 시작 |
| 유예 중 재차 미납 | 기존 미납에 오늘 금액만 더하고 기존 유예 종료일 유지 |
| 완납 후 재차 미납 | 기존 유예를 지우고 새로운 3일 유예 시작 |
| 유예 종료일 도달 | 정산 결과의 게임오버 조건 flag만 성립하며 실제 게임오버 전환은 없음 |
| 설비 해금 후 다음 영업일 | 활성 설비 상품이 후보에 들어오고 단계별 가중치가 적용됨 |

### 6.6 저장·빌드 범위

- [ ] 일일지침·당일 상품·미납·유예에 새 저장 필드를 추가하지 않음
- [ ] 새 게임 시작 시 런타임 상태가 초기화되고, 저장 복원 시나리오를 이 기능의 PASS 근거로 사용하지 않음
- [ ] 일반 Player build를 검증할 경우 Addressables content catalog를 재생성한 뒤 `Datas` 라벨에서 지침 CSV가 로드되는지 확인함
- [ ] 실제 게임오버 상태 전환은 이 구현의 완료 조건으로 포함하지 않음

## 7. 현재 브랜치에서 확인된 최소 증거와 미완료

작성 시점의 `DailyInstruction` 브랜치에서는 다음을 확인했다.

- `DailyGuidelineTests` 4/4 통과
- Unity compile error 0
- Console error 0
- 일일지침 Addressables entry: address `DailyGuidelineData`, label `Datas`, CSV rows 2
- 10일차 지침 1개·20일차 지침 2개 생성, 조합 속성 0건, 모든 손님 대상 약 70% 분포

전체 MainScene 수동 조작, 최종 UI 사용감, 일반 Player build와 실제 게임오버 전환은 이 문서 작성 시점에 완료된 것으로 간주하지 않는다. 통합 후 위 체크리스트의 미확인 항목은 `PENDING`으로 보고한다.

## 8. 충돌 시 보류해야 하는 결정

다음 차이가 발견되면 YAML이나 CSV를 임의로 한쪽 전체 선택하지 말고 차이와 권장안을 기록한다.

- 대상 `total_merge`가 기존 일일지침 고정일차 스키마를 유지하려는 경우: 새 무작위 규칙형 스키마와 소비자 변경을 함께 비교한다.
- 대상 branch가 `GameUI.prefab` parent 계층 또는 Inspector 이벤트를 다르게 가진 경우: child Prefab 연결을 수동 재배치하고 부모 기준을 임의로 덮어쓰지 않는다.
- 대상 branch에 다른 `GameSessionManager`, `DailyAggregationService`, `MaintenanceService` 권위 구현이 있는 경우: 상태 소유자와 호출 순서를 하나로 정한 뒤 중복 wrapper를 만들지 않는다.
- Addressables group·label 정책이 다른 경우: 보호 설정 변경으로 분리해 승인받고, Editor 설정과 Player catalog의 차이를 함께 검증한다.
- 저장·복원 구현을 추가하자는 요구가 생긴 경우: 현재 일일지침 범위를 넘어서는 별도 설계·migration 승인 없이는 이 문서의 통합 범위에 넣지 않는다.
