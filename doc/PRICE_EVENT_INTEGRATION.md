# 가격 변동 이벤트 시스템 구현·병합 명세

상태: 2026-09-08 구현 및 개인 씬 최소 실행 검증 완료. 영업 시작 60초 이내 방송·방송 시 가격 반영 기준을 포함한다. Google Docs ID 목록 등록은 아래 사유로 보류.

## 현재 진행 연결 (2026-09-09)

현재 GameUIController → GameProgress/DayProgress가 세션을 명시 주입받는다. DayProgress.Tick은 pause를 제외한 min(deltaSeconds, 남은 영업시간)만 세션 방송 API에 전달하며 Closing에서는 진행하지 않는다. 기본 30초 영업은 그대로라 30초 이후 예약 방송은 취소될 수 있다. 현재 가격표는 ProgressViewDataFactory.CreatePriceListText(day, DailyPriceState)에서 같은 날짜의 현재가를 사용하고 누락 단가 fallback을 금지한다. 기존 Dev3 신문/전단 화면 설명은 아래 과거 연결이며 현 UI에 연결 완료한 의미가 아니다.

EndTradingDay(out DailyAggregationResult result) overload는 기존 long EndTradingDay()와 종료 구현을 공유한다. 일반일 정산/상납 성공 후 GameProgress가 CompleteDay를 한 번 호출하고 다음날 가격을 준비한다. [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)에 실패·Closing 정책과 미연결 경계를 기록했다.

## 목적과 범위

- 상품 CSV의 BasePrice는 고정 기본가격이다. 신문은 하루 시작에, 라디오 효과는 영업 중 방송 시점에 현재가에 반영한다. 거래는 SubmitOffer 시점의 최종 목록과 최신 현재 단가를 고정한다.
- 신문은 주기적인 사건, 라디오는 랜덤 사건을 표현한다. 채널별 최대 1개이며 서로 독립적으로 발생한다.
- 뉴스 부재와 가격 영향 없는 뉴스는 다르다. 신문/라디오 O/O, O/X, X/O, X/X를 허용한다.
- 기존 전단지 UI에 신문 제목·설명을 연결한다. 라디오는 Debug.Log로 출력하며 별도 UI는 만들지 않는다.
- 재고·매입·명성·도덕성·저장 시스템은 이번 범위에 추가하지 않는다.

## 선행 확인

- 사용자가 재생성한 t.ccpln6m1g4kv 탭에서 기존 배정 목록을 확인했다. 단일 권위 링크는 doc/CSV_RULES.md를 따른다.
- 사용자가 9부터 사용하도록 명시적으로 승인하여 PriceEvent=9, PriceEventSchedule=10을 적용했다. 종료 표식은 값을 명시하지 않는 마지막 항목이다.
- 테스트 행은 이벤트 9001~9004, 스케줄 10001~10005, 뉴스 TextData 8042~8049다. 기존 로컬 PK와 충돌하지 않는다. 병합 직전 다른 branch와 다시 확인한다.
- 기존 Default Local Group·Datas 라벨 등록 승인을 받아 두 CSV를 확장자 없는 파일명 주소로 등록했다.
- Google Docs 쓰기 전 필수 trusted-read 도구가 Windows 절대 경로를 거부하여 원격 ID 목록 갱신은 보류했다. 지원 환경에서 권위 목록 등록을 완료해야 한다.

## 데이터 계약

### PriceEventData / PriceEventDataTable

| 컬럼 | 논리 타입 | 규칙 |
|---|---|---|
| idx | uint | 승인된 이벤트 PK |
| nameidx | uint | 필수 TextData FK, 제목 |
| descriptionidx | uint | 필수 TextData FK, 설명 |
| product_idxs | uint 배열 | 기존 밑줄 구분, ProductData FK |
| product_types | ProductType 배열 | 기존 숫자 enum 변환기 사용 |
| change_type | 숫자 enum | 0 효과 없음, 1 비율 증감, 2 금액 증감 |
| change_value | int | 부호로 방향 표시. 비율은 1000=100% |

- 대상 두 목록은 합집합이며 상품당 한 번만 적용한다.
- 각 목록 내 중복·잘못된 FK·정의되지 않은 enum은 거부한다.
- 효과 없음은 change_value=0, 대상 목록은 빈값으로 명확히 구분한다.
- 가격 효과는 대상이 하나 이상 필요하다. 비율의 단일 감소량은 -1000 이상으로 제한한다.
- 여러 채널이 같은 이벤트를 선택해도 같은 이벤트 효과는 하루에 한 번만 적용한다. 채널 표시 자체는 각각 유지한다.
- 같은 상품에 서로 다른 이벤트가 적용되면 합산한다.

### PriceEventScheduleData / PriceEventScheduleDataTable

| 컬럼 | 논리 타입 | 규칙 |
|---|---|---|
| idx | uint | 승인된 스케줄 PK |
| event_idx | uint | 필수 PriceEventData FK |
| channel | 숫자 enum | 1 신문, 2 라디오 |
| start_day | uint | 최초 발생 가능 경과 일수, 시작일 0 |
| end_day | nullable uint | 빈값은 제한 없음. 지정하면 start_day 이상 |
| repeat_days | uint | 신문 반복 간격. 0이면 start_day 한 번 |
| selection_weight | uint | 양수 상대 가중치 |

- 신문 후보: 날짜 범위 내에서 repeat_days=0이면 현재일=start_day, 그 외에는 (현재일-start_day)%repeat_days=0.
- 라디오 후보: 날짜 범위 내. repeat_days는 0으로 고정하며 매일 발생 판정 대상이다.
- 같은 채널·같은 이벤트에 같은 날 여러 스케줄이 겹치면 해당 이벤트의 가중치를 합산한다.
- 채널별 후보는 PK 순으로 정렬해 같은 seed에서 재현 가능하게 한다.
- 신문은 후보가 있으면 가중치로 1개 선택한다.
- 라디오는 후보가 있으면 가중치로 1개 예약한다. 별도의 발생 확률 상수는 사용하지 않는다. 가격 변화 없는 뉴스도 정상 방송이다. 후보가 없으면 방송하지 않는다.
- 영업 시작 시 0~60초 이내의 무작위 방송 시점을 정한다. 게임 일시정지 시간은 제외하며, 방송 전 영업 종료 시 취소하고 다음 날로 이월하지 않는다.
- 채널 후보 가중치 합계는 int.MaxValue 이하이며 초과 시 오류다. 무한 재추첨하지 않는다.

## 현재가 계산

현재가 = max(1, floor(기본가격 × (1000 + 비율 증감 합계) / 1000) + 금액 증감 합계)

- 곱셈은 decimal 중간값, 결과는 기존 CustomerOrderItem.UnitPrice의 uint 범위로 검증한다.
- 합산 효과로 1 미만이 되면 1로 제한한다. uint 상한 초과는 오류이며 조용히 clamp하지 않는다.
- CSV BasePrice를 덮어쓰지 않는다. 전 상품 현재가를 임시 사전에 계산·검증한 뒤 일간 상태를 한 번에 공개한다.
- UI 표시 여부와 가격 적용은 독립적이다. 뉴스 창을 열지 않아도 효과는 적용된다.
- 다음 날은 전날 현재가가 아닌 BasePrice에서 다시 계산한다.

## 코드 구조와 소유권

- Assets/Scripts/Commons/Data/: 이벤트·스케줄 DTO와 각 IDataLoad 구현.
- Assets/Scripts/Events/PriceEventScheduler.cs: 날짜 후보·가중치·채널 추첨.
- Assets/Scripts/Events/DailyPriceState.cs: 확정된 날짜·뉴스·읽기 전용 현재가 snapshot.
- 기존 DataTableManager: 로더 등록과 모든 FK 검증 이후 공개.
- 기존 GameSessionManager: 스케줄러와 일간 가격 상태의 수명, 경과 일수의 단일 권위.
- 현재 GameUIController/ProgressViewDataFactory: 세션 날짜·현재가를 표시한다. 이전 Dev3 신문 표시는 현 UI 연결과 별개다.
- 별도 singleton, interface, event bus, ScriptableObject, 신규 package는 만들지 않는다.

## 공개 API

- GameSessionManager.ElapsedDays: 세션 경과 일수.
- GameSessionManager.EnsureDailyPrices(): 현재일 상태가 있으면 그대로 반환, 없으면 계산·확정.
- GameSessionManager.BeginTradingDay() / EndTradingDay(): 가격 확정 후 영업 시작 및 Finance 정산. 화면에서 Finance의 BeginDay/EndDay를 직접 호출하지 않는다.
- GameSessionManager.AdvanceTradingTime(deltaSeconds, isPaused): 활성 영업 화면 한 곳이 프레임당 한 번 호출한다. 현재 GameUIController.Update → DayProgress.Tick 경로가 기존 Time.deltaTime의 유효 영업시간만 전달한다(진행 Pause 또는 Time.timeScale=0 동안 정지). 방송·가격 교체 시 true를 반환해 화면을 갱신한다. 별도 manager Update에서도 중복 호출하지 않는다.
- GameSessionManager.CompleteDay(uint completedDay): 정산 완료·미납 처리 완료 이후 다음 날로 전환. 완료한 경과일을 전달하며 중복 완료와 역행을 거부한다. 이후 EnsureDailyPrices()로 다음 날 가격을 확정한다.
- DailyPriceState.ElapsedDays / NewspaperEventIdx / RadioEventIdx / Prices: 읽기 전용 결과. RadioEventIdx는 방송 예정 PK이며 IsRadioBroadcast=true인 경우에만 방송 완료다. 방송 시 새 snapshot으로 교체하며 기존 snapshot은 불변이다.
- CustomerGenerator.Generate(..., elapsedDays, getCurrentPrices): 최신 가격표 조회 함수를 주입한다. 최초 표시용 가격과 제출 시 확정 가격은 구분한다.
- 최초 희망 목록 표시 단가는 유지한다. 방송 후 제출하는 기존 손님도 변경된 현재가로 최종 기준액·허용액을 계산한다. 이미 제출한 결과의 단가·원가는 변경하지 않는다.
- 기존 RegularSale enum 숫자는 유지하고 표시명만 '기준가 판매'로 변경한다.

## 하루 시작과 UI 연결

1. InitScene에서 CSV·FK와 세션 초기화를 완료한다.
2. 세션 현재일의 이벤트·가격을 확정한 후 허브를 표시한다.
3. 전단지 첫 페이지 NoticeBanner에 신문 제목·설명을 표시한다. 신문 부재 시 '오늘의 신문 이벤트 없음'을 표시한다.
4. 라디오는 씬 진입 시 출력하지 않는다. OPEN STORE 이후 일시정지를 제외한 60초 이내 한 번 방송하며, 그 시점에 기본가격에서 신문+라디오 효과를 함께 재계산하고 날짜·이벤트 PK·제목·설명을 Log로 출력한다. 계산 실패 시 기존 가격을 유지하고 오류를 보고하며 자동 재시도하지 않는다.
5. 가격표와 상품 카드에 기본가격·현재가를 함께 표시한다.
6. 손님 생성 시 최신 현재가 조회 함수를 전달한다. 생성 당시 가격표를 callback에 캡처하지 않는다.
7. Finance는 성사된 거래의 실제 제안 총액만 한 번 반영한다.
8. 정산·상납금 처리가 끝나면 다음 날을 확정한다.

씬 재진입 시 기존 날짜·현재가·뉴스를 재사용한다. 영업 중 재진입으로 기존 거래가 소실되는 복원 문제는 자동 재개하지 않고 명시적 제한을 둔다. 전체 세션 복원은 이번 범위가 아니다.

## 검증·인계 체크

아래 PASS·Check 실행명·수치는 당시 개인 씬 구현 기록이다. 개별 셸은 이후 제거했고 현재 API 검증은 [TESTING.md](TESTING.md)의 PriceEventTests/GameSessionApiTests를 따른다. 기록을 현재 MainScene/UI PASS로 승격하지 않는다.

- CSV header, PK, enum, 대상·Text·이벤트 FK 오류 시 LogError 및 예외.
- 지정일·반복일·시작일·종료일 경계 및 신문/라디오 4조합.
- 효과 없는 뉴스와 뉴스 없음의 구분.
- 상품·종류 대상 중복의 한 번 적용, 동일 이벤트 중복 방지.
- 비율·금액 합산, 버림, 최저 1, 상한 초과 실패.
- 동일 날짜 재호출·씬 재진입의 재추첨 방지와 다음 날 기본가격 복원.
- 최초 희망 목록 표시 단가 snapshot 유지. 기존·신규 손님 모두 제출 순간 최신 현재가를 적용하며, 제출 완료 결과의 단가·원가는 불변이다.
- 신문 UI·라디오 로그와 거래 판정·Finance 수입 연결을 개인 씬에서 확인.
- PASS: Unity 6000.3.18f1 컴파일 완료, InitScene → GameplaySandbox에서 이벤트 CSV 로드 및 12개 현재가 확정, 버튼 입력 거래 수락·거부·중복 수입 방지·정산·다음 날 전환 확인. 실행 후 제품 Console error 0.
- PASS: 동일 날짜 상태 객체 재사용, 중복 날짜 완료 거부, 새 손님 단가와 현재가 일치 확인.
- Tools/Check-PriceEvents.ps1: 후보 선정·0~60초 범위·방송 전후 불변 snapshot·중복 효과·가격 계산 검증 통과.
- Tools/Check-RadioTiming.ps1: 새 Play 세션에서 실행. 런타임 API에 경과 초를 전달해 진입 시 미방송·일시정지·60초 이내 방송·중복 방지·기존 방문 보존·신규 방문 가격·조기 종료 취소 검증 통과. 두 날짜를 진행하므로 사용자 플레이 도중 실행하지 않는다.
- PASS: GameplaySandbox에서 실제 OPEN STORE 버튼을 누른 뒤 Update 경과로 방송 완료 확인(RADIO_UI_PASS). Console error 0, 검증 후 Play 종료 및 임시 백그라운드 실행 설정 복원.
- Tools/Check-CustomerCsv.ps1: 정상 CSV 및 오류 32/32 거부, Tools/Check-CustomerGenerator.ps1: 기존 손님 생성·거래 회귀 통과. 오류 주입 검사는 의도된 LogError를 남긴다.
- PARTIAL: 신문 텍스트 배치·상품 3줄 가격 표시의 시각적 검수와 동일 날짜 씬 재로드 검증은 미실행. 전체 저장/복원은 미구현이다.

## 테스트 사용법

- 기존 개인 씬 선택을 유지한 채 InitScene부터 Play한다. NEW GAME → 여정 시작 → 영업 시작 → OPEN STORE로 거래한다.
- 전단지 첫 페이지에서 신문 제목·설명과 현재가를 확인한다. 라디오는 발생한 날에만 Console의 `[Radio]` 로그를 확인한다.
- 상품 카드는 기본가격·희망 목록 생성 당시 단가를 표시한다. 최종 기준액은 제출 전 미확정이다. 거래 제안은 구매 목록 전체 총액을 입력한다.
- 거래 판정 후 END DAY → COMPLETE DAY로 다음 날 이벤트를 확정한다. 상납일에는 기존 Finance 납부 절차가 먼저다.
- 초기 신문은 경과 0일부터 2일 간격 식료품 -20%, 경과 3일부터 4일 간격 무효과 뉴스다. 라디오 후보는 물 +30, 의약품 +30%, 무효과 뉴스이며 가중치는 동일하다. 이 수치는 테스트용으로 기획 확정 시 CSV에서 교체한다.
- 게임을 멈춘 상태에서 [TESTING.md](TESTING.md)의 EditMode PriceEventTests와 PlayMode GameSessionApiTests를 실행한다. UI/라디오 로그의 체감 시점은 위 사용법으로 별도 확인한다.

## 병합 시 특이사항

- 이 문서는 가격 변동 이벤트 시스템 전체 인계 문서다. 라디오만의 작업 문서가 아니다.
- 라디오 UI는 의도적으로 미구현이다. 이후 UI는 IsRadioBroadcast를 확인한 뒤 RadioEventIdx로 공용 TextData를 조회하고 가격 재계산·재추첨은 하지 않는다. 구형 CustomerSandbox는 Git 제외 Local 개인 코드로 영업 시계를 구동하지 않는다. 현재 통합 API 검사는 GameProgress/DayProgress를 사용하며 UI 검수는 GameUIController 경로에서 별도 수행한다.
- 기존 신문 역할 UI는 Dev3SandboxTester의 전단지 첫 페이지다. 원격 UI branch의 변경과는 병합 시 호출·계층 충돌 검사가 필요하다.
- 개인 씬은 Git 제외를 유지한다. MainScene·Addressables는 필요한 연결만 별도 검증 후 반영한다.
- 저장 기능이 연결되기 전에는 앱 종료 후 날짜·뉴스·현재가 복원을 지원하지 않는다.
- 현재 작업 시작 시 존재한 TMP fallback font asset 변경은 이 기능과 무관하므로 보존한다.


- API 검사는 승인된 현재 라디오 구현 계약을 확인한다. 9/8 회의록의 '추후 피처' 표기는 최종 기획 활성화 여부이며 테스트 통과가 신규 활성화 승인을 뜻하지 않는다. 원가 차감·명성 산정·정식 지침은 이 시스템에 연결하지 않았다.
