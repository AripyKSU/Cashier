# MainScene 진행·세션 API 통합

## Upgrade 통합 (2026-09-10)

- `Upgrade 935cf93`의 상품16종·설비11종·일일 유지비·지침 표시·막대/소팅/청소기를 `total_merge fcf1518`의 이미지·도덕성·대기열과 통합했다. 기존 MainScene 인스턴스 및 Local 원본은 보존하고 공유 GameUI의 새 참조를 연결했다.
- 영업 전 카드는 Controller → 기존 factory → PreOpenPanelPresenter 경로 하나로 현재가·활성 설비·Sprite를 받는다. 최대4종 표시 제한은 유지한다. 5종 이상 해금 시 모든 상품을 표시하는 UI 확장은 미구현이다.
- BusinessClock은 DayProgress의 남은 시간 비율을 09:00~20:00으로 표시하며 자체66초 카운트는 사용하지 않는다. 영업 시간·pause·Closing 권위는 변경하지 않았다.
- DailyGuidelineData는 PK/날짜 중복 및 Text/Product FK를 검증한 뒤 공개한다. 현재 지침 문구 표시만 연결되며 SaleRestriction으로 변환해 거래에 공급하거나 벌칙을 적용하는 기능은 미연결이다.
- 일일 정산의 Expenses/NetProfit과 Transactions/MoralityDelta를 함께 보존한다. 유지비 부족 시 집계가 먼저 종료·초기화되는 것은 Upgrade 원본 동작이며 자동 재시도/복구는 추가하지 않았다. 부족·알림 예외 후 정상 재개 정책은 별도 설계가 필요하다.
- 실제 Main smoke: 기본4카드 Sprite·통조림 현재가200, 가게1→3단계, 당일 효과 잠금→다음날3효과 활성, 유지비200·정산·2일차 대기열1명 확인. 막대/청소기 실제 드래그 감각과 최종 배치는 사용자 수동 확인 대상이다. 증거와 미검증 범위는 [TESTING.md](TESTING.md).

기준: 2026-09-09, `total_merge`에 설비 `65888e1`과 명성 `6976218`을 통합. 원본 브랜치는 보존하고 push는 별도다. MainScene에 공유 GameUI prefab·Camera·InputSystem EventSystem을 연결했다. 개인 Local 코드·씬은 포함하지 않는다.

## 현재 호출 경로

설비 계약은 [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md), 명성 데이터는 [REPUTATION_CUSTOMER_GENERATOR_HANDOFF.md](REPUTATION_CUSTOMER_GENERATOR_HANDOFF.md)를 따른다. 명성 계산·거래/정산 로그·정산 피드백을 설비 UI와 함께 연결했다. 명성에 따른 손님 생성 비율은 아직 미연결이다. 최신 검증은 TESTING.md를 따른다.

GameUI.prefab의 GameUIController → GameProgress → DayProgress → GameSessionManager/EconomyRuntime이 실제 진행 경로다. Dev3SandboxTester는 현재 소스에서 비활성화된 이전 화면이며 이 경로의 검사 대체물이 아니다.

| 경계 | 현재 계약 |
|---|---|
| GameProgress(session, catalog, reputationBalanceTable, random, duration) | 초기화된 session.Economy만 사용한다. CurrentDay는 시작 전 0, 시작 후 checked((int)session.ElapsedDays + 1)이다. 영업 중 진행 재생성은 거부한다. 현재 명성과 로그는 세션에 위임해 화면 재진입에도 유지한다 |
| DayProgress(day, session, catalog, reputationBalanceTable, random, dayStartReputation, duration) | 1기반 day와 session.ElapsedDays의 일치를 검증한다. 생성기 해금 일자와 가격 callback은 같은 세션을 사용한다. 하루 시작 명성은 해당 날짜에 고정한다 |
| OpenBusiness | 첫 방문 준비 → session.BeginTradingDay → 손님 활성화. Progress가 집계 BeginDay를 직접 호출하지 않는다 |
| SubmitOffer | 최종 선택 SaleItem 목록으로 판정하고 visit.Result.Value를 그대로 일일집계에 한 번 전달한다. Outcome/SoldItems/CostTotal/지침 기록을 legacy TransactionResult로 재생성하지 않는다 |
| 접수 실패 | false/예외면 확정 방문 결과를 보존하고 원본 예외를 전달한다. 성공 이벤트·다음 손님·Tick·정산 완료를 차단한다. rollback/자동 재시도 없음. 판정 입력 검증 실패는 올바른 재제출 가능 |
| Tick | Controller의 기존 Time.deltaTime을 한 곳에서 전달. pause는 진행하지 않고 min(delta, 남은 영업초)만 라디오와 영업시간에 적용한다. Closing 이후 라디오 시간을 더 진행하지 않는다 |
| 정산 | DayProgress가 session.EndTradingDay(out result)를 한 번 호출한다. 세션은 해당 표시일 유지비를 자동 차감한 뒤 매출·유지비·순익·차감 후 잔액을 공개하고, 원본 거래 목록으로 명성을 계산한다. 유지비 부족은 오류를 기록하고 정산창 공개와 날짜 진행을 차단한다 |
| 다음 날 | 일일 정산 확인 뒤 CompleteDay(종료한 Day - 1)를 정확히 한 번 호출한다. 별도 상납 상태는 없으며 EnsureDailyPrices 성공 후 다음 DayStarted/가격표를 공개한다 |
| 가격표 | ProgressViewDataFactory.CreatePriceListText(day, session.EnsureDailyPrices())는 동일 날짜의 판매 가능 상품과 현재가를 표시한다. 날짜 불일치·단가 누락/0은 예외이며 BasePrice로 대체하지 않는다 |

CurrentDay는 별도 저장/증가하지 않는다. CompleteDay 이후 새 날짜 가격 계산 실패 시 날짜를 임의 rollback하거나 새 하루 성공 이벤트를 보내지 않는다. 현 UI 오류 처리가 진행을 중단하며 세션 복구는 별도 설계 대상이다.

세션은 날짜 완료 직후 명성을 한 번 반영하고 다음 날 snapshot을 만든다. 유지비 실패일은 정산·날짜·명성을 적용하지 않는다. 새 세션은 명성0/빈 로그이며 기존 세션에서 화면을 재생성하면 명성·설비·로그가 유지된다.

## 유지한 정책과 미연결

- 기본 영업시간 30초를 변경하지 않았다. 라디오 예약이 영업 마감보다 늦으면 방송하지 않는다. 60초 보장은 영업이 그 시점까지 계속되는 경우이며 모든 날 방송 보장이 아니다.
- Closing은 마지막 손님의 제안을 허용한다. 이때 경제 집계는 마지막 거래 종료까지 열려 있지만 라디오 시계는 멈춘다. '제한시간 이후 거래 금지' 정책으로 바꾸지 않았다.
- SaleSortingPanel의 실제 선택 목록 → GameProgress.SubmitOffer 연결을 유지한다. 기존 문서의 '최종 선택 UI 없음'은 현재 경로에 적용하지 않는다. 화면/UX 성공은 사용자 수동 확인 대상이다.
- CustomerQueue API는 존재하지만 현재 DayProgress는 계산대 방문을 순차 생성한다. Dev3의 10명 대기열/자동 결과 표시를 이 UI에 연결했다고 보고하지 않는다.
- 원가·지침 결과는 거래 DTO에 보존하지만 실제 원가 차감·일일 원가 집계·지침 벌칙·정식 지침 공급은 미연결이다. 거래 기반 명성 계산은 연결됐다. 거래 ID 중복 제거 기능은 없다.
- 상품 이미지 빈값/실패 시 기존 UI placeholder 정책은 이번 범위에서 수정하지 않았다. 기존 엄격한 리소스 명세와 실패 fallback의 차이는 별도 검토 대상이다.
- 라디오 전용 UI·저장 파일 복원·Player build는 범위 밖이다. 명성 이미지 자산은 미지정이며 기존 정성적 텍스트 피드백을 사용한다.

## 검증과 사용자 확인

자동 검사는 [TESTING.md](TESTING.md)의 GameSessionApiTests에서 실제 진행 API를 거친다. API 통과를 MainScene 화면 통과로 해석하지 않는다.

사용법: `Use MainScene` 선택 → InitScene에서 Play → Hub를 거쳐 MainScene 진입 → 영업 시작/상품 분류/가격 확정 → 마감 마지막 거래 → 유지비 자동 차감과 정산 표시 → 정산의 설비 버튼 → 구매/닫기 → 정산 완료 → 다음날 해금과 명성 피드백 확인. 최종 화면 가독성·사용감은 사용자 수동 확인 대상이다.

## 후속 설비 구현 완료 범위

- 기존 FinanceService 지출과 세션 소유 상태를 사용하며 정산 단계에서 구매한다. 중복 구매·잔액 부족을 거부하고 결제 알림 예외 시 재결제·임의 환불 없이 상태를 다시 조회한다.
- 다음날 활성일은 세션 경과 일수 권위를 사용한다. 설비 소유 상태·CSV·상품 FK·독립 상점 prefab과 정산 UI 연결을 구현했다.
- 현재 계약·사용법·검증 경계는 [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md)를 따른다.

## 이전 Dev3 통합 기록 (현재 실행 안내 아님)

아래 내용은 이전 작업의 사실 기록이다. 현재 화면 구성·미구현 목록·실행 버튼 이름의 권위는 위 절이며 과거 PASS를 이번 UI 검증에 재사용하지 않는다.

## 사용법

1. START SESSION → 튜토리얼 진행 → START DAY → 가격표 → OPEN STORE.
2. 실제 Customer CSV로 만든 손님의 구매 목록과 입장 대사가 표시된다. 키패드 또는 숫자 키로 전체 목록 총액을 입력하고 CONFIRM한다.
3. CustomerVisit이 한 번 판정한다. 수락이면 Finance의 일일 집계에 판매 수입을 한 번 반영하고, 거절이면 자금은 변하지 않는다. 결과 대사는 TextData에서 읽는다.
4. 5초마다 대기 손님이 입장하며 최대 10명이다. 판정 결과를 3초 표시한 후 FIFO로 다음 손님을 인계한다. NEXT CUSTOMER로 먼저 인계할 수도 있다. 성향별 재촉·만료 이탈과 데이터/API는 [CUSTOMER_QUEUE_INTEGRATION.md](CUSTOMER_QUEUE_INTEGRATION.md)를 따른다.
5. 판정 후 END DAY로 Finance 집계를 닫는다. 납부일에는 CSV의 금액·주기에 따라 PAY MAINTENANCE를 사용한다. 자금 부족 시 금액·회차를 바꾸지 않으며 진행은 납부 단계에서 대기한다. 게임 오버 규칙은 미구현이다.
6. COMPLETE DAY는 GameSessionManager.CompleteDay로 경과일을 늘리고 EnsureDailyPrices로 다음 날 뉴스·현재가를 확정한 뒤 허브로 돌아간다. 날짜와 경제 상태는 세션 소유다. 가격 이벤트 API·라디오 로그·테스트 데이터는 [PRICE_EVENT_INTEGRATION.md](PRICE_EVENT_INTEGRATION.md)를 따른다.

## 소유권과 제외

- CashierSession 파일은 보존하지만 이 화면에서 생성·호출하지 않는다.
- CustomerCatalog/CustomerGenerator/CustomerVisit: 상품·외형·성향·주문·가격 판정·대사.
- GameSessionManager.Economy: 금액·일일 매출·상납금. 화면이 경제 런타임을 재생성하지 않는다.
- MainScene UI는 ProductData 행을 직접 사용한다. 이름은 NameIdx → TextData, 이미지는 ImageResourceIdx → ResourceData로 해석한다. CashierProduct 변환·하드코딩 상품 초기값은 사용하지 않는다. 구형 타입은 실행에서 제외된 CashierSession의 호환 코드로만 남아 있다.
- 저장·이어하기·시민권·명성·도덕성·업그레이드·오디오 설정은 비활성화했다. PlayerPrefs 저장을 읽거나 덮어쓰지 않는다. 기존 전단·튜토리얼·장식 문구에는 UI 원형의 placeholder가 남아 있으며 실제 게임 규칙의 권위가 아니다.
- 한국어 TMP 글꼴은 로컬 Malgun Gothic을 사용한다. 정식 플랫폼용 폰트 배포는 별도 작업이다. 상품 이미지 빈값은 흰 사각형으로 표시하고 잘못된 이미지 FK/로드 실패는 중단한다.
- 거래 처리 중 예외는 입력을 중단하고 Console에 남긴다. 금액을 자동 재반영하지 않는다. 씬 재진입·저장 복원을 지원하는 완성된 세션 복구 기능은 아니다.

## 검증

개별 UI 검사 셸은 제거했다. 새 Play 세션에서 Init → MainScene 로딩 후 위 사용법을 수동으로 수행하고 입장·4단계 판정·입금 1회·퇴장/FIFO·일일 종료·다음 날 진행을 확인한다. 최종 목록 선택 UI는 미구현이라 현재는 최초 희망 목록을 제출한다. API 검사는 [TESTING.md](TESTING.md)를 따르며 MainScene 직렬화 연결·버튼·표시 성공을 대신하지 않는다.

이전 통합 기록에서는 당시 셸 검사와 컴파일, Console 오류·경고 0을 확인했다. Player 빌드, 저장 복구, 상납금 전체 회차 UI 검증은 미실행이다. MainScene 직접 Play 대신 Init 진입을 사용한다.
# MainScene 대기열 통합 (2026-09-10)

- InitScene → HubScene → MainScene으로 실행한다. `Cashier/Gameplay Scene Settings`의 `Use MainScene`으로 개인 씬 선택을 비운다. 개인 씬 파일은 삭제하지 않는다.
- MainScene GameUI 인스턴스의 `useCustomerQueue=true`. 별도 `Customer Queue`의 `CustomerQueueView`가 실제 DayProgress를 관찰하며 모델·금액·방문 시계는 변경하지 않는다.
- `AstraFrontView/Customer/QueueRoot`에 FIFO 슬롯10개와 입장·좌우 퇴장 anchor가 있다. 기존 CustomerPresenter는 계산대 대사·장바구니를 계속 담당하고 외형 Image 렌더만 QueueView가 대체한다.
- 현재·대기 손님은 높이430/aspect 보존, 하단 기준 호흡과 cover12px를 사용한다. 이동0.65초, 퇴장은 기존 QueueExitSeconds(0.45초) 동안 이미지 검정 전환과 전체 alpha fade를 적용한다. pause·다음날·비활성화 시 기존 수명 규칙을 따른다.
- 공유 Scene/코드는 Local 경로나 Local 컴포넌트를 참조하지 않는다. Local 원본은 이전 실험 버전으로 보존하며 공유 변경에 자동 동기화되지 않는다.
- 확인 시나리오: 영업 시작→손님 및 대기열 생성→재촉/이탈→가격 제안·거래 퇴장→pause/resume→마감 후 정산. 실제 수행 결과는 TESTING.md에 기록하며 API 테스트와 화면 사용감 검증은 구분한다.
