# 작업 상태: 가계부형 Settlement UI 구현 인계

- 사람 담당자 / 위임받은 역할·범위: 프로젝트 사용자 요청에 따라 Settlement 가계부, 딸 대사와 명성 도장 순차 출력, 제품용 리소스와 Prefab 직렬화 연결을 구현한다. 전체 흐름 Controller는 후속 작업으로 보류한다.
- 마지막 확인 시각: 2026-09-11 (Asia/Seoul)
- 기준 저장소·브랜치: `C:\UnityProject\Cashier`, `Settlement`
- 참조할 기능 명세: [`MAINSCENE_INTEGRATION.md`](../MAINSCENE_INTEGRATION.md), [`DAILY_GUIDELINE_TOTAL_MERGE_HANDOFF.md`](../DAILY_GUIDELINE_TOTAL_MERGE_HANDOFF.md), [`MORALITY_INTEGRATION.md`](../MORALITY_INTEGRATION.md)

## 목적

하루 정산이 시작되면 가계부 배경 위의 두 `TextMeshProUGUI` 영역에 확정된 정산 내용을 표시한다. 왼쪽 페이지의 영업 결산을 먼저 서서히 쓴 다음, 짧은 간격을 두고 오른쪽 페이지의 손님·지침·납부 기록을 이어서 쓴다.

화면은 금액이나 통계를 다시 계산하거나 Manager를 직접 조회하지 않는다. 기존 `GameUIController → DailySettlementViewData → DailySettlementPresenter` 경로에서 전달된 UI snapshot만 사용한다.

## 현재 구현 상태

다음 파일이 구현되어 `SettlementPanel.prefab`과 `DailySettlementPresenter`에 연결됐다.

- `Assets/Scripts/UI/Presenters/DailySettlementLedgerFormatter.cs`
  - `DailySettlementLedgerText`: `LeftPage`, `RightPage` 완성 문자열
  - `DailySettlementLedgerFormatter.Format(DailySettlementViewData)`: 정산 snapshot을 두 페이지로 변환
- `Assets/Scripts/UI/Presenters/DailySettlementLedgerView.cs`
  - 왼쪽과 오른쪽 `TextMeshProUGUI` 참조 소유
  - `Present(DailySettlementLedgerText)`: 왼쪽부터 순차 출력
  - `CompleteImmediately()`: 전체 문구 즉시 공개
  - `RefreshCompleted(DailySettlementLedgerText)`: 같은 날짜의 최신 잔액을 타이핑 재생 없이 갱신
  - `Clear()`: 문구와 진행 중인 연출 초기화
  - `OnPresentationCompleted`: 양쪽 페이지가 모두 공개되면 한 번 발생
  - TMP의 `maxVisibleCharacters`와 `Time.unscaledDeltaTime`을 사용

현재 출력 구분은 다음과 같다.

- 왼쪽: 일차, 판매 수입, 유지비, 지침 벌금, 총지출, 순이익, 현재 보유금
- 오른쪽: 성공·거절·이탈 손님 수, 명성 변화, 지침 위반 벌금 합계, 총 납부 필요액·납부액·납부 상태
- 모든 금액의 통화 단위는 `원`이다.

## 명성 도장

딸 대사 타이핑이 끝나면 `ReputationStampPresenter`가 정산 후 누적 명성에 해당하는 도장을 선택해 0.45초 동안 확대 상태에서 내려찍고 짧게 흔든 뒤 고정한다. 연출은 정산 중 시간이 멈춰도 진행하도록 `Time.unscaledDeltaTime`을 사용한다.

| 정산 후 누적 명성 | Sprite |
|---:|---|
| -100~-61 | `LedgerStampNotorious` (`5.png`) |
| -60~-21 | `LedgerStampUnpopular` (`4.png`) |
| -20~20 | `LedgerStampNeutral` (`3.png`) |
| 21~60 | `LedgerStampPopular` (`2.png`) |
| 61~100 | `LedgerStampExcellent` (`1.png`, 참잘했어요) |

- 기존 도장 4개는 사용자 제공 축소 이미지로 같은 파일명과 GUID를 유지한 채 교체했다.
- `LedgerStampExcellent.png`만 최고 명성 구간용 신규 리소스다.
- `DaughterDialoguePresenter.OnPresentationCompleted`를 `GameUIController`가 받아 `DailySettlementPresenter.PresentReputationStamp()`를 호출한다.
- 도장 고정 뒤 `ReputationStampPresenter.OnPresentationCompleted`가 한 번 발생하고 `DailySettlementPresenter.OnStampPresentationCompleted`가 이를 전달한다.
- 같은 날짜에 정산 화면을 다시 렌더링하면 완료된 도장은 즉시 유지하며 애니메이션과 완료 이벤트를 반복하지 않는다.
- `SettlementPanel/DailyLedgerVisual/ReputationStamp`의 위치와 크기는 사용자가 조절할 임시값이다.

## 정산 상호작용

`SettlementInteractionView`는 정산 화면의 두 버튼을 소유하지만 진행 로직은 호출하지 않고 이벤트만 발행한다.

- `OnFacilityRequested`: `GameUIController`가 기존 설비 업그레이드 UI 진입 경로에 연결한다.
- `OnNextDayRequested`: 후속 `DailySettlementFlowController`가 구독할 공개 계약이며 현재는 `DayProgress` 또는 `GameProgress`에 직접 연결하지 않는다.
- `SetInteractionEnabled(bool)`: 가계부·딸 대사·도장 연출 중과 설비 모달 표시 중 두 버튼을 함께 잠근다.
- 도장 완료 뒤 두 버튼을 해제하고, 설비 UI를 닫으면 완료 상태에 따라 다시 해제한다.
- `FacilityPamphletButton`은 아트 교체 전까지 흰색 `Image`를 사용하는 임시 오브젝트다. 현재 RectTransform은 위치 `(-390, -120)`, 크기 `(140, 100)`이며 Inspector에서 직접 변경한다.
- 기존 `SettlementNext` 버튼은 `DailySettlementPresenter`에서 분리해 `SettlementInteractionView.nextDayButton`에 연결했다.

## 정산 전체 흐름 Controller

`DailySettlementFlowController`가 정산 컴포넌트의 표시 순서와 입력 수명을 전담한다. 금액·명성·대사를 계산하거나 선택하지 않고 `GameUIController`가 생성한 `DailySettlementViewData`와 `DaughterDialogueViewData`만 전달한다.

```text
LedgerPresenting → DaughterPresenting → StampPresenting → ReadyForInteraction
                                                          ├─ FacilityOpen → ReadyForInteraction
                                                          └─ AdvancingDay
```

- 각 완료 이벤트는 현재 상태가 일치할 때만 다음 단계로 넘어가므로 중복·지난 이벤트를 무시한다.
- 같은 날짜에 `Begin`이 다시 호출되면 진행 중인 연출을 재시작하지 않는다.
- 설비 구매 중에는 `FacilityOpen` 상태와 입력 잠금을 유지하고 최신 잔액만 `RefreshSettlement`으로 즉시 갱신한다.
- 설비 UI가 닫히면 `NotifyFacilityClosed`로 `ReadyForInteraction`에 복귀한다.
- 다음 날 버튼은 첫 클릭 즉시 `AdvancingDay`로 바뀌며 입력을 잠근 뒤 주입받은 기존 `GameProgress.CompleteSettlement` 경로를 한 번만 호출한다.
- Flow 오류는 `OnFlowFailed`로 `GameUIController`의 기존 오류 표시 경로에 전달한다.

## 추가된 UI 오브젝트

현재 최소 편집 계층은 다음과 같다. 위치와 글자 크기는 플레이어가 후속 조정할 임시값이며 화면 배치 검증은 하지 않았다.

```text
SettlementPanel
└─ DailyLedgerVisual
   ├─ Background                 Image
   ├─ LeftPageText               TextMeshProUGUI
   └─ RightPageText              TextMeshProUGUI
```

`DailySettlementLedgerView`는 `DailyLedgerVisual`에 부착하고 두 TMP 참조를 직렬화했다.

### Background

- 현재 제품용 이미지: `Assets/Textures/UI/Dystopia/Settlement/DailyLedger.png`
- 컴포넌트: `Image`
- `preserveAspect`: 실제 배치 결과에 따라 설정
- 표시 전용이면 `raycastTarget=false`
- Settlement 화면의 버튼보다 뒤, 두 페이지 텍스트보다 앞을 가리지 않는 형제 순서로 배치
- Astra 원본의 가계부는 `Assets/Prefabs/Dystopia/Templates/FrontView.prefab` 안의 `DailyLedger`에서 위치와 비율을 참고할 수 있다.

### LeftPageText / RightPageText

두 오브젝트 모두 다음 설정을 기준으로 시작한다.

- 컴포넌트: `TextMeshProUGUI`
- Alignment: `Top Left`
- Word Wrapping: 활성
- Overflow: 우선 `Truncate`; 실제 아트 크기 확인 후 확정
- Auto Size: 비활성 권장. 타이핑 중 글자 크기와 줄바꿈이 바뀌지 않아야 한다.
- Rich Text: 활성
- Raycast Target: 비활성
- 양쪽에 같은 TMP Font Asset, font size, line spacing, 글자색 적용
- 페이지 안쪽 여백과 제본 영역을 침범하지 않도록 별도 RectTransform 사용

Astra의 `DystopiaLedgerPage`는 구형 페이지 원근에 맞춰 UI 메시를 변형하는 참고 구현이다. 현재 두 TMP 영역에 반드시 적용해야 하는 요구사항은 아니다. 우선 평면 TMP 배치를 플레이 화면에서 확인하고, 원근 불일치가 명확할 때 별도 이관 여부를 결정한다.

## 후속 Inspector 직렬화

`DailySettlementLedgerView`에 다음 참조와 수치를 연결한다.

| 필드 | 연결 대상 | 권장 초기값 |
|---|---|---:|
| `leftPageText` | `DailyLedger/LeftPageText` | 필수 |
| `rightPageText` | `DailyLedger/RightPageText` | 필수 |
| `charactersPerSecond` | 초당 표시 글자 수 | `35` |
| `pageIntervalSeconds` | 왼쪽 완료 후 오른쪽 시작 간격 | `0.25`초 |

두 TMP 중 하나라도 연결되지 않은 상태에서 `Present` 또는 `CompleteImmediately`를 호출하면 `MissingReferenceException`이 발생한다. 화면 연결 작업에서는 두 참조를 같은 변경에서 반드시 설정한다.

## Presenter 및 Controller 연결 계획

현재 `GameUIController.renderSettlement()`은 `DailySettlementViewData`를 만들어 `DailySettlementPresenter.UpdateView()`에 전달한다. 이 경로는 유지한다.

후속 `DailySettlementPresenter` 변경안:

```csharp
[SerializeField] private DailySettlementLedgerView ledgerView;
```

`UpdateView(DailySettlementViewData viewData)`에서 기존 패널을 연 뒤 다음을 호출한다.

```csharp
DailySettlementLedgerText ledgerText = DailySettlementLedgerFormatter.Format(viewData);
ledgerView.Present(ledgerText);
```

연결 책임은 다음처럼 유지한다.

```text
GameUIController
  └─ DailySettlementViewData 생성 및 전달
      └─ DailySettlementPresenter
          ├─ Settlement 패널·버튼 수명
          ├─ DailySettlementLedgerFormatter 호출
          └─ DailySettlementLedgerView에 완성 문구 전달
```

별도의 Settlement Controller를 만들 경우에도 정산 데이터를 다시 조회하거나 계산하지 말고, 기존 Presenter가 받은 `DailySettlementViewData` 또는 formatter 결과를 명시적으로 전달한다. Controller와 Presenter가 동시에 패널 활성 상태나 타이핑 Coroutine을 소유하지 않도록 한 곳만 화면 수명 소유자로 정한다.

## 이전 Settlement UI 정리

가계부가 기존 항목별 정산 표시를 완전히 대체하도록 확정돼 `SettlementDay`, 수입·지출·순이익·잔액·통계·납부 관련 TMP와 `SettlementReputation`을 제거했다. `DailySettlementPresenter`에서도 이 직렬화 필드와 중복 문자열 출력 코드를 함께 제거했다. 구형 `FacilityOpenButton`은 새 `FacilityPamphletButton`으로 대체했으며 `SettlementNext`, 가계부, 딸 대사, 명성 도장과 FlowController 연결은 유지한다.

## 데이터 사용 시 주의사항

- formatter 입력은 `DailySettlementViewData` 하나다.
- `Expenses`는 현재 ViewData에서 `MaintenanceAmount + GuidelinePenaltyAmount`로 확정된다.
- `NetProfit`도 ViewData에서 확정된 값을 그대로 표시한다.
- `CurrentBalance`는 설비 구매 후 정산 화면이 다시 렌더링되면 최신 세션 잔액으로 바뀐다.
- 정산 직후 불변 잔액인 `DailySettlementResult.BalanceAfterSettlement`은 현재 ViewData에 별도 필드로 전달되지 않는다.
- `MoralityDelta`와 거래별 상세 `Transactions`는 도메인 결과에는 있지만 현재 `DailySettlementViewData`에는 없다. 이를 가계부에 추가하려면 UI 계약 확장과 기존 소비자 검토가 필요하며 formatter가 Manager에서 우회 조회하면 안 된다.
- 지침 위반 문구는 `ProgressViewDataFactory`가 상품 이름과 지침을 조합한 완성 `Content`를 전달한다. formatter에서 Product/DataTable을 다시 조회하지 않는다.

## 상호작용 결정이 필요한 항목

후속 화면 구현 시 다음 UX를 확정한다.

- 타이핑 중 다음 날 버튼을 허용할지, 완료 후에만 활성화할지
- 사용자가 클릭하여 `CompleteImmediately()`로 스킵할 수 있게 할지
- 시설 상점을 열었다 닫아 `UpdateView`가 다시 호출될 때 처음부터 재생할지, 이미 완성된 상태를 유지할지
- 지침 위반 내용이 오른쪽 영역 높이를 넘을 때 글자 크기 축소, 잘라내기, 스크롤 중 무엇을 사용할지
- 미납 상세가 없는 일반일에 빈 공간을 유지할지, 간결한 `납부 완료`만 표시할지

현재 View는 `Present` 재호출 시 기존 Coroutine을 중단하고 처음부터 다시 쓴다. 다른 정책을 선택한다면 화면 소유자가 정산 일차 또는 snapshot 변경 여부를 판단해 `Present` 호출 빈도를 제어하는 편이 좋다.

## 최소 검증 계획

### 코드

- formatter 일반 정산: 양수 순이익과 `납부 완료`
- formatter 적자 정산: 음수 순이익의 부호 유지
- 지침 위반 0건과 복수 항목
- 미납 없음, 유예 중, 유예 만료 문구
- Unity 컴파일 오류 0

### 프리팹·플레이

- 왼쪽 TMP가 먼저 나오고 완료 후 오른쪽 TMP가 시작하는지
- 정산처럼 진행 시간이 멈춘 상태에서도 `unscaledDeltaTime`으로 계속 출력되는지
- 재진입 또는 재표시 시 Coroutine이 중복되지 않는지
- 한글 glyph 누락, 줄바꿈, 페이지 영역 초과 여부
- 16:9와 프로젝트에서 실제 사용하는 추가 화면비에서 제본·테두리 침범 여부
- 시설 구매 후 현재 보유금 갱신 정책이 선택한 UX와 일치하는지

## 현재 검증 및 Git 상태

- 신규 formatter와 View는 Unity 리임포트·컴파일 완료, Console error 0을 확인했다.
- 실제 TMP 참조, 가계부 배경, Presenter 호출이 아직 없으므로 런타임 화면 검증은 미실행이다.
- 현재 상태: `STATIC PASS`.
- 이 인계 작성 시점에 formatter·View·각 `.meta` 및 본 문서는 untracked 상태이며 stage·commit·push·merge하지 않았다.
- 다른 작업자는 시작 전에 실제 `git status`, 최신 브랜치와 해당 파일의 반영 여부를 다시 확인해야 한다.

## 다음 담당자가 바로 할 작업

1. Settlement 전체 화면의 소유자와 기존 UI를 대체할지 여부를 확정한다.
2. `DailyLedger.png`를 사용하는 배경과 두 TMP 오브젝트를 프리팹에 배치한다.
3. `DailySettlementLedgerView`의 두 TMP 참조를 직렬화한다.
4. `DailySettlementPresenter` 또는 확정된 Settlement 화면 소유자에 `ledgerView`를 연결한다.
5. `DailySettlementLedgerFormatter.Format(viewData)` 결과로 `Present`를 한 번 호출한다.
6. 위 최소 플레이 검증 후 글자 속도·RectTransform·폰트 크기만 조정한다.
