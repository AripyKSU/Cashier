# FinanceScene 경제 런타임 구조

이 문서는 `FinanceScene`의 현재 테스트 실행 구조와 실제 게임 세션에 통합할 때의 소유권 경계를 설명한다.
현재 `FinanceScene`은 `InitScene`을 거치지 않고 직접 실행할 수 있는 경제 검증 씬이며, 실제 게임 세션과 연결된 최종 통합 씬이 아니다.

## 핵심 소유권

실제 게임 세션에서는 `GameSessionManager`가 세션 수명 동안 하나의 `EconomyRuntime`을 소유한다.

```text
InitScene
  ↓
DataTableManager가 경제 CSV 로드
  ↓
GameSessionManager.InitializeNewGame(...)
  ↓
GameSessionManager가 EconomyRuntime 생성·소유
  ↓
GameSessionManager.Instance.Economy
```

`EconomyRuntime`은 다음 경제 서비스를 하나의 세션 상태로 묶는다.

```text
EconomyRuntime
├─ FinanceService
├─ DailyAggregationService
├─ MaintenanceService
└─ EconomyQueryService
```

상태의 원본은 `EconomyRuntime`과 그 하위 서비스에 있다. UI나 버튼은 보유금·일일 수입·영업 상태를 별도로 저장하지 않고 조회 서비스에서 읽어야 한다.

## 현재 FinanceScene 직접 실행 구조

현재 씬은 `GameSessionManager`를 생성하거나 조회하지 않는다. 이는 계획상 `InitScene`과 실제 Addressables 부팅을 FinanceScene 검증의 선행 조건으로 삼지 않기 위한 의도적인 테스트 경로다.

```text
FinanceSceneTestController.Start()
  ↓
economyBalanceCsv.text 직접 파싱
  ↓ EconomyBalanceDataTable.LoadData()
maintenanceBalanceCsv.text 직접 파싱
  ↓ MaintenanceBalanceDataTable.LoadData()
  ↓
new EconomyRuntime(...)
  ↓
FinanceSceneTestController.query = economy.QueryService
```

현재 직접 실행 경로에서 만들어지는 `EconomyRuntime`은 해당 `FinanceSceneTestController`가 소유하는 테스트 전용 인스턴스다. 따라서 이 경로의 경제 상태는 실제 `GameSessionManager`가 가진 경제 상태와 공유되지 않는다.

직접 CSV 참조를 사용하는 이유는 다음과 같다.

- `FinanceScene`을 단독으로 열어 즉시 PlayMode 검증할 수 있다.
- `InitScene`, 기존 Scene 스크립트와 실제 부트스트랩에 의존하지 않는다.
- Addressables 설정을 변경하지 않는다.
- Reset 버튼이 CSV의 시작 보유금으로 테스트 런타임을 재생성할 수 있다.

## 현재 Scene 계층과 역할

```text
FinanceScene
├─ Main Camera
├─ FinanceRoot
│  └─ FinanceSceneTestController
├─ Canvas
│  └─ SafeArea
│     ├─ TitleText
│     ├─ FinanceStatePanel             실제 Finance 화면에 남길 상태 표시 영역
│     │  ├─ BalanceText
│     │  ├─ DailySaleIncomeText
│     │  ├─ DayOpenStateText
│     │  ├─ MaintenanceCycleText
│     │  └─ NextMaintenanceAmountText
│     └─ FinanceTestPanel               통합 전에 제거하거나 비활성화할 테스트 영역
│        ├─ FinanceTestActions
│        │  ├─ BeginDayButton
│        │  ├─ AddSale5000Button
│        │  ├─ AddSale20000Button
│        │  ├─ EndDayButton
│        │  ├─ PayMaintenanceButton
│        │  └─ ResetTestButton
│        └─ FinanceTestFeedback
│           ├─ LastActionText
│           └─ LogText
└─ EventSystem
```

`FinanceStatePanel`과 그 하위 상태 텍스트는 실제 세션 연결 후에도 재사용할 UI다. `FinanceTestPanel`의 버튼과 피드백은 경제 서비스 검증을 위한 임시 영역이며 실제 거래 판정, 날짜 진행, 게임 오버 처리를 구현하지 않는다.

## 버튼의 현재 역할

| 버튼 | 현재 호출 | 실제 세션 통합 시 의미 |
|---|---|---|
| `BeginDayButton` | `DailyAggregationService.BeginDay()` | 실제 영업 시작 호출자 또는 상위 게임 흐름으로 대체 |
| `AddSale5000Button` | `TryApplyTransaction(new TransactionResult(5000, 0))` | 실제 거래 판정 결과가 호출해야 하며 테스트 버튼은 제거 |
| `AddSale20000Button` | `TryApplyTransaction(new TransactionResult(20000, 0))` | 실제 거래 판정 결과가 호출해야 하며 테스트 버튼은 제거 |
| `EndDayButton` | `DailyAggregationService.EndDay()` | 하루 집계 종료 흐름과 연결; 날짜 증가는 담당하지 않음 |
| `PayMaintenanceButton` | 다음 회차 `MaintenanceService.TryPay()` | 실제 하루 진행 시스템이 결정한 시점에 호출 |
| `ResetTestButton` | CSV 재파싱 후 테스트 `EconomyRuntime` 재생성 | 실제 세션에서는 사용하지 않음 |

테스트 버튼은 정상 서비스 API의 호출 경로를 확인하기 위한 어댑터다. 실제 거래 판정이나 날짜 진행 로직을 대신하지 않는다.

## 실제 세션 통합 시 전환할 부분

실제 `GameSessionManager` 연결 작업에서는 다음 순서를 따른다.

1. `InitScene`에서 `DataTableManager`의 경제 데이터 로드가 완료된 뒤 `GameSessionManager.InitializeNewGame()`이 한 번 실행된다.
2. `FinanceScene`은 새 `EconomyRuntime`을 생성하지 않고 `GameSessionManager.Instance.Economy`를 사용한다.
3. 상태 표시 UI는 `GameSessionManager.Instance.Economy.QueryService`를 읽는다.
4. 실제 판매 결과는 거래 판정 시스템이 `DailyAggregationService.TryApplyTransaction()`에 전달한다.
5. 날짜 진행과 상납 시점은 하루 진행 시스템이 소유한다.
6. `FinanceTestPanel`과 직접 CSV 필드는 테스트 통합이 끝난 뒤 제거하거나 Editor 전용 검증 경로로 분리한다.

실제 세션에서 `new EconomyRuntime(...)`을 두 번째로 호출하면 UI와 게임 세션이 서로 다른 경제 상태를 보게 된다. 따라서 통합 후에는 `GameSessionManager` 외의 객체가 `EconomyRuntime`을 생성하지 않는 것을 불변 조건으로 둔다.

## 현재 범위 밖인 항목

- `InitScene`, `HubScene`, `MainScene`, `LoadingScene`과 기존 Scene 스크립트 변경
- 실제 Addressables 부팅 연결
- 날짜 증가, 주차 계산, 게임 오버 전환
- 실제 거래 판정 및 명성 시스템
- `GameSessionManager` 공용 API 변경

이 문서는 현재 FinanceScene 테스트 구조를 설명하는 문서다. `GameSessionManager`에 Finance UI를 연결하는 작업은 실제 부트스트랩 경로와 소유권을 함께 검토하는 별도 통합 작업으로 진행한다.

## 검증 기록

- Unity `6000.3.18f1` 컴파일 오류 0
- `FinanceScene` 직접 PlayMode에서 CSV 파싱 및 초기 보유금 표시 확인
- 테스트 버튼을 통한 판매·일일 집계·상납금 성공/실패·Reset 확인
- `FinanceStatePanel`은 `SafeArea`의 실제 상태 표시 영역으로 유지되고, 테스트 버튼·피드백은 `FinanceTestPanel` 아래에 격리됨
- Scene 직렬화 참조 누락 0
