# 정산 화면 클래스 및 API 기술 문서

이 문서는 하루 종료부터 정산 UI 진행, 설비 UI 왕복과 다음 날 전환까지의 코드 연결을 설명한다. 화면 위치와 리소스 교체는 [`SETTLEMENT_UI_LAYOUT.md`](SETTLEMENT_UI_LAYOUT.md)를 참고한다.

## 전체 데이터와 호출 흐름

```text
DayProgress.tryBeginSettlement
├─ GameSessionManager.EndTradingDay
├─ DailyReputationCalculator.Calculate
├─ GameSessionManager.SelectDaughterDialogue
└─ SettlementStarted(DailySettlementResult)
         ↓
GameUIController
├─ ProgressViewDataFactory.CreateDailySettlementViewData
├─ ProgressViewDataFactory.CreateDaughterDialogueViewData
└─ DailySettlementFlowController.Begin
         ↓
Ledger → Daughter → Stamp → Interaction
                            ├─ Facility UI 왕복
                            └─ GameProgress.CompleteSettlement
                                   ↓
                              DayProgress.CompleteSettlement
                                   ↓
                     날짜 완료·명성 적용·다음 날 시작
```

UI 계층은 매출, 비용, 납부액, 명성 변화량과 딸 대사를 다시 계산하거나 선택하지 않는다.

## 주요 클래스

| 클래스 | 경로 | 책임 |
|---|---|---|
| `DayProgress` | `Assets/Scripts/Progress/DayProgress.cs` | 하루 상태와 확정 정산·명성·딸 대사 결과 소유 |
| `GameProgress` | `Assets/Scripts/Progress/GameProgress.cs` | 현재 DayProgress 완료, 명성 적용, 다음 날 생성 |
| `GameUIController` | `Assets/Scripts/Scene/GameUIController.cs` | 도메인 결과를 ViewData로 변환하고 실제 설비 UI/API를 조립 |
| `ProgressViewDataFactory` | `Assets/Scripts/UI/ProgressViewDataFactory.cs` | 확정 도메인 결과를 UI snapshot으로 변환 |
| `DailySettlementFlowController` | `Assets/Scripts/UI/Presenters/DailySettlementFlowController.cs` | 정산 구성요소 순서, 중복 입력, 재진입과 설비 왕복 상태 관리 |
| `DailySettlementPresenter` | `Assets/Scripts/UI/Presenters/DailySettlementPresenter.cs` | 가계부 문구 준비와 명성 도장 View 연결 |
| `DailySettlementLedgerFormatter` | `Assets/Scripts/UI/Presenters/DailySettlementLedgerFormatter.cs` | DailySettlementViewData를 양쪽 가계부 문자열로 변환 |
| `DailySettlementLedgerView` | `Assets/Scripts/UI/Presenters/DailySettlementLedgerView.cs` | 가계부 두 페이지 순차 타이핑 |
| `DaughterDialoguePresenter` | `Assets/Scripts/UI/Presenters/DaughterDialoguePresenter.cs` | 항상 표시되는 가계부 딸 이미지와 대사 타이핑·고개 연출 |
| `ReputationStampPresenter` | `Assets/Scripts/UI/Presenters/ReputationStampPresenter.cs` | 정산 후 명성에 맞는 Sprite 선택과 도장 연출 |
| `SettlementInteractionView` | `Assets/Scripts/UI/Presenters/SettlementInteractionView.cs` | 팜플렛·다음 날 버튼 입력을 이벤트로 전달 |
| `FacilityShopPresenter` | `Assets/Scripts/UI/Presenters/FacilityShopPresenter.cs` | 설비 목록, 구매 요청, 닫기 요청 표시 |

## 표시 데이터 계약

### `DailySettlementViewData`

정산 화면이 소비하는 읽기 전용 snapshot이다.

- 날짜, 판매 수입, 지출, 순이익, 현재 보유금
- 일일 명성 변화량과 정산 후 누적 명성
- 성공·거절·이탈 손님 수
- 유지비, 지침 벌금과 위반 요약. 지침 벌금은 당일 총 판매 금액에 위반 1회당 5%를 적용하며 20회부터 100%로 제한하고 1원 미만은 버린다.
- 총 납부 필요액, 납부액, 미납·유예·게임오버 조건

설비 구매 후에는 확정 정산값을 유지하고 `CurrentBalance`만 최신 세션 값으로 다시 생성한다.

### `DaughterDialogueViewData`

DayProgress가 하루 한 번 선택한 결과를 화면에 전달한다.

- `Day`
- `Text`
- `Sprite`

대사가 없거나 Sprite FK를 찾지 못하는 경우는 정상적인 빈 화면이 아니라 계약 오류다.

## `DailySettlementFlowController`

### 상태

| 상태 | 의미 | 허용 입력 |
|---|---|---|
| `Inactive` | 아직 정산 시작 전 | 없음 |
| `LedgerPresenting` | 가계부 타이핑 중 | 없음 |
| `DaughterPresenting` | 딸 대사 중 | 없음 |
| `StampPresenting` | 도장 애니메이션 중 | 없음 |
| `ReadyForInteraction` | 모든 연출 완료 | 팜플렛, 다음 날 |
| `FacilityOpen` | 설비 UI 표시 중 | 정산 버튼 없음 |
| `AdvancingDay` | 다음 날 요청 접수 완료 | 없음 |
| `Failed` | 정산 UI 흐름 오류 | 없음 |

완료 이벤트는 현재 상태가 해당 단계와 일치할 때만 처리한다. 따라서 중복 완료 이벤트나 이전 날짜 이벤트가 다음 단계를 다시 실행하지 않는다.

### 공개 API

#### `Begin(DayProgress, DailySettlementViewData, DaughterDialogueViewData, Action)`

- DayProgress가 `Settlement` 상태인지 검사한다.
- 세 인자의 날짜가 일치하는지 검사한다.
- 다음 날 진행용 `Action`을 보관한다.
- 두 상호작용 버튼을 잠근다.
- 딸 대사와 도장을 준비하고 가계부 출력을 시작한다.
- 같은 날짜에 다시 호출되면 진행 중인 연출을 재시작하지 않는다.

현재 `GameUIController`는 마지막 Action으로 기존 `GameProgress.CompleteSettlement`를 전달한다.

#### `RefreshSettlement(DailySettlementViewData)`

- 현재 날짜와 snapshot 날짜가 같아야 한다.
- 설비 구매 후 최신 보유금을 가계부에 반영한다.
- 완료된 가계부·딸·도장 애니메이션과 완료 이벤트를 재실행하지 않는다.

#### `NotifyFacilityClosed()`

- `FacilityOpen` 상태에서만 반응한다.
- 상태를 `ReadyForInteraction`으로 복구한다.
- 팜플렛과 다음 날 버튼을 다시 활성화한다.

#### `CancelFacilityOpen()`

외부 조건 때문에 설비 UI를 열 수 없었을 때 `FacilityOpen` 상태를 취소하고 정산 입력을 복구한다.

#### `SetFailed(Exception)`

상태를 `Failed`로 바꾸고 정산 입력을 잠근 뒤 `OnFlowFailed`를 발행한다.

### 공개 이벤트

| 이벤트 | 소비자 | 의미 |
|---|---|---|
| `OnFacilityOpenRequested` | `GameUIController` | 기존 설비 UI를 열어 달라는 요청 |
| `OnDayAdvanceStarted` | 후속 표시/전환 코드 | 다음 날 완료 Action이 성공적으로 호출됨 |
| `OnFlowFailed` | `GameUIController.showError` | 정산 UI 흐름 예외 |

## Presenter와 View API

### `DailySettlementPresenter`

- `UpdateView(DailySettlementViewData)`: 가계부 문자열과 도장 Sprite를 준비한다.
- `PresentReputationStamp()`: 준비된 도장 애니메이션을 시작한다.
- `OnLedgerPresentationCompleted`: 양쪽 가계부 타이핑 완료.
- `OnStampPresentationCompleted`: 도장 고정 완료.

### `DailySettlementLedgerView`

- `Present(DailySettlementLedgerText)`: 왼쪽 페이지 후 오른쪽 페이지를 출력한다.
- `CompleteImmediately()`: 현재 타이핑을 끝내고 전체 문구를 표시한다.
- `RefreshCompleted(DailySettlementLedgerText)`: 완료 이벤트 없이 같은 날짜 문구를 즉시 갱신한다.
- `Clear()`: 문구와 진행 상태를 초기화한다.
- `OnPresentationCompleted`: 양쪽 페이지가 모두 표시된 뒤 한 번 발생한다.

### `DaughterDialoguePresenter`

- `UpdateView(DaughterDialogueViewData)`: 이미지와 대사를 준비하고 표시를 숨긴다.
- `Present()`: 초당 24자로 대사를 출력하고 짧은 고개 연출을 시작한다.
- `OnPresentationCompleted`: 모든 대사 문자가 표시된 뒤 한 번 발생한다.

### `ReputationStampPresenter`

- `UpdateView(int day, int finalReputation)`: 정산 후 명성 구간의 Sprite를 준비한다.
- `Present()`: 0.45초 내려찍기·충격 흔들림 연출을 시작한다.
- `OnPresentationCompleted`: 도장이 원래 Transform으로 고정된 뒤 한 번 발생한다.

명성 구간은 `-100~-61`, `-60~-21`, `-20~20`, `21~60`, `61~100`의 5단계다.

### `SettlementInteractionView`

- `SetInteractionEnabled(bool)`: 팜플렛과 다음 날 버튼을 함께 잠그거나 해제한다.
- `OnFacilityRequested`: 팜플렛 Button 클릭.
- `OnNextDayRequested`: 다음 날 Button 클릭.

이 View는 설비 UI를 직접 열거나 DayProgress를 직접 호출하지 않는다.

## 설비 UI 왕복

1. `ReadyForInteraction`에서 팜플렛을 클릭한다.
2. FlowController가 즉시 `FacilityOpen`으로 바꾸고 두 버튼을 잠근다.
3. `OnFacilityOpenRequested`를 받은 GameUIController가 기존 설비 UI를 연다.
4. 구매 처리와 시설 상태 변경은 기존 `GameProgress.TryPurchaseFacility` 경로가 담당한다.
5. 구매 후 GameUIController가 최신 잔액으로 `RefreshSettlement`을 호출한다.
6. 설비 UI를 닫은 뒤 `NotifyFacilityClosed`를 호출한다.
7. FlowController가 `ReadyForInteraction`과 버튼 상태를 복구한다.

설비 UI 진입과 구매는 날짜를 변경하지 않으며 딸 대사나 도장을 다시 선택하지 않는다.

## 다음 날 연결

1. `ReadyForInteraction`에서 다음 날 버튼을 클릭한다.
2. FlowController가 먼저 `AdvancingDay`로 바꾸고 입력을 잠근다.
3. `Begin`에서 주입받은 `GameProgress.CompleteSettlement`를 한 번 호출한다.
4. GameProgress가 현재 `DayProgress.CompleteSettlement`를 호출한다.
5. DayProgress가 `Completed` 이벤트를 발생시킨다.
6. GameProgress가 날짜 완료, 일일 명성 적용과 다음 DayProgress 생성을 수행한다.

호출이 예외를 던지면 FlowController는 `ReadyForInteraction`으로 복구하고 `OnFlowFailed`를 발행한다.

## 변경 시 지켜야 할 경계

- Formatter와 Presenter에서 Manager를 직접 조회하지 않는다.
- FlowController에서 금액·명성·대사 선택을 계산하지 않는다.
- Interaction View에서 설비 UI 또는 DayProgress를 직접 호출하지 않는다.
- 같은 날짜의 설비 왕복에서 `Begin`으로 애니메이션을 재시작하지 않는다.
- 다음 날 처리는 UI 패널을 먼저 닫지 않고 도메인 완료 호출의 성공 결과를 따른다.
- 새 정산 단계를 추가하면 FlowState, 완료 이벤트와 입력 잠금 구간을 함께 갱신한다.
