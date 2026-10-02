# total_merge 로컬 병합 및 보존 검증 (2026-10-02)

사용자가 승인한 범위: total_merge의 현재 기능과 origin/refactor_fix, origin/intro-cutscene의 기능·리소스 통합. 로컬 병합/커밋만 수행하고 push하지 않는다. 기존 IDE 미커밋 변경은 보존한다.

## 기준과 통합 결정

- total_merge 기준: `8b093946a5ffa85864bea734ce6eeab6ccea41a0`
- refactor_fix: `037e56e9dd67a96878d4991686fed2437e755201`
- intro-cutscene: `0586c485112395e3d81703b4e6396bc8a691b23d`
- 분리 검증 작업 디렉터리: `C:/UnityProject/Cashier/Temp/MergeIntegration`
- 원본 Editor를 조작하지 않고 동일 버전 Unity 6000.3.18f1에서 별도 검증했다.
- CustomerVisit의 기존 거절 사유·전용 대사와 새로운 resource/speech 식별자를 모두 유지했다. 고객 생성·큐 표시·reflection 테스트 호출도 함께 이행했다.
- 메인 진입의 WorldVisit/WorldQueueSpeech 풀 22개 준비와 Hub→Intro→Main 경로를 함께 유지했다.
- 현재 장부의 상납금/지침 벌금 분리를 유지했다.
- 인트로 브랜치의 Packages 내 node_modules/MCP 복사본, output, 개인 MyArt 씬, MCP 설정, 불필요한 mixer 편집은 제외했다. 기존 package manifest를 유지했다.
- 자동 테스트 과정에서 생성되는 동적 폰트 atlas와 프로젝트 설정 저장 변화는 병합 결과에 넣지 않는다.
- 상자 이미지 6개와 meta, OperatingPanel 그림자, 가게 정면 3단계 Y 위치(-523)를 반영했다.

## 판정 기준과 증거

아래 표는 이전 기능 목록의 T/R/I ID를 유지한다. STATIC PASS는 소스·직렬화·리소스 보존 확인이다. PASS는 해당 자동 실행 경계까지 확인했다는 뜻이며 전체 UX/미술/음질의 승인과 다르다. PARTIAL은 보존 확인과 미검증 또는 테스트 실패가 함께 있다.

증거는 로컬 `C:/UnityProject/Cashier/Logs/MergeIntegration-20261002`에 있다. XML/로그/이미지는 Git에 추가하지 않았다. 기능별 과거 명세의 오래된 수치 대신 현재 CSV를 보존했다. 예를 들어 설비 12001=60,000, 12002=90,000, 12003=350,000, 12005=750,000, 12007=80,000, 12012=3,000,000원이다.

- `static-audit.json`: 기존 런타임 CSV 18개 동일, MainScene 동일, manifest 동일, Assets GUID 중복 0, meta 짝 누락 0, Addressables 주소 중복/누락 대상 0.
- `baseline-edit.xml`: 병합 전 EditMode 345개 중 334 성공/11 실패/skip 0.
- `merged-edit.xml`: 병합 후 EditMode 346개 중 335 성공/동일한 11 실패/skip 0. 신규 실패 없음.
- `integration-play-2.xml`: 실제 Init→Hub→Intro→Main 두 차례, 중복 완료 차단, 세션 초기화, 단계별 상자 연결, 풀 clear 이후 22개 재준비 성공. 스킵 확인/취소/재시작 음원 정리와 정산 7행 타이밍 성공.
- 전체 PlayMode 최종 결과는 아래 별도 기록에 명시한다.

## 기존 total_merge 체크리스트

| ID | 기능 | 판정 / 범위 |
|---|---|---|
| T01 | 시작 Init→Hub | PASS: 실제 씬 진입 |
| T02 | 스플래시·허브 로딩 | PARTIAL: 경로 실행, 화면 체감 미확인 |
| T03 | 새 게임·허브 복귀·초기화 | PASS: 실제 2회 재시작 |
| T04 | 20일·하루 120초 | PARTIAL: 현재 데이터 보존; 오래된 시계 테스트 차이 기록 |
| T05 | 일과 상태·시간 진행 | PARTIAL: 상태 API 검사, 전체 체감 미확인 |
| T06 | 일일 상품 4/6/8종 | PARTIAL: 코드/CSV 보존; FK commit 이전 Rows를 읽는 기존 테스트 실패 |
| T07 | 상품 16종 가격·원가 | PARTIAL: CSV 동일; 기존 fixture 검증 실패 |
| T08 | 부유층/빈곤층 구매 규칙 | PASS: 고객 계약 EditMode |
| T09 | 초반 손님 수 제한 | PASS: 생성 계약 EditMode |
| T10 | 명성·프로필 고객 생성 | PASS: 생성 계약 EditMode |
| T11 | 프로필별 대사 | PASS: 대사/외형 식별자와 전용 거절 대사 공존 |
| T12 | FIFO·5초 간격 | PASS: 큐 계약 검사; 실제 시각적 간격은 별도 확인 |
| T13 | 월드 손님 위치·원근 | PARTIAL: 풀 실행, 원근 체감 미확인 |
| T14 | 말풍선·거래 반응 | PARTIAL: 큐/반응 계약, 배치 체감 미확인 |
| T15 | 거래·경제·도덕성·지침 | PASS: 해당 도메인 자동 검사 |
| T16 | 판매할 상품 없음 전용 처리 | PASS: 기존 NoSaleItemsTextIdx 보존 |
| T17 | 거래 중복 처리 방지 | PASS: 기존 거래 계약 검사 |
| T18 | 상품 드래그·정렬·제거 | PARTIAL: 정렬 실행 검사, 손 입력 UX 미확인 |
| T19 | 상자 열기·먼지 | PARTIAL: 3단계 Sprite 실행 연결; 먼지 외형 미확인 |
| T20 | 계산기·자동 계산 | PARTIAL: 코드/자동 검사, 버튼 체감 미확인 |
| T21 | 바코드·자동 정렬 | PARTIAL: 풀/정렬 계약, 연출 미확인 |
| T22 | 청소기 | PARTIAL: 현 경로 보존; 기존 테스트가 이전 Vacuum 경로를 참조 |
| T23 | 지침 0/1/2개 | PASS: 지침 계약 자동 검사 |
| T24 | 벌금 UI | PARTIAL: 판정/금액 계약, 화면 확인 필요 |
| T25 | 감독관 일자·단계 이벤트 | PARTIAL: 실제 1일차 진입; 과거 7일차 기대값과 현재 값 차이 |
| T26 | 감독관 원화 | STATIC PASS: 리소스 보존 |
| T27 | 가게 3단계·정면/작업대·시계 | PARTIAL: 실제 3단계 적용; 오래된 시계 fixture 실패 |
| T28 | 시간대 tint·환경 효과 | PARTIAL: 코드 보존; 자동 시간 수명 검사와 시각 점검 분리 |
| T29 | 설비 팸플릿·구매 | PARTIAL: 구매 실패 잠금 실행 성공; 기존 가격 기대값은 과거 사양 |
| T30 | 무료 확장·다음 날 적용 | PASS: 설비/일과 계약 검사 |
| T31 | 장부·도장 | PARTIAL: 현재 상납/벌금 구조 유지; UI 체감 미확인 |
| T32 | 딸 대화 | PARTIAL: 데이터/계약 보존; 연출 미확인 |
| T33 | 상납금·미납 게임오버 | PARTIAL: 계약 보존; 로그 표기 문자열 기존 테스트 차이 |
| T34 | 시민권 300만원·조건·즉시 엔딩 | PASS: 현재 계약/데이터 유지 및 자동 검사 |
| T35 | 4개 엔딩 | PARTIAL: 현재 페이지 보존; 기존 테스트의 옛 SFX ID/문자열 실패 |
| T36 | 사운드 키·매핑 | PARTIAL: 현재 25키 보존; 기존 테스트는 23키를 기대 |
| T37 | BGM 정리 | PARTIAL: 인트로 종료/스킵 정리 검사; 청취 미확인 |
| T38 | 폰트·다음날 레이아웃 | PARTIAL: 기존 atlas 보존, 글리프/가독성 수동 확인 필요 |
| T39 | DV3 디버그 | STATIC PASS: 코드 보존 |
| T40 | 빌드 파이프라인 | STATIC PASS: 설정/스크립트 유지; 배포용 player build 미실행 |

## refactor_fix 체크리스트

| ID | 기능 | 판정 / 범위 |
|---|---|---|
| R01 | WorldVisit 풀 | PASS: 실제 재시작/풀 clear 후 재준비 검사 |
| R02 | WorldQueueSpeech 풀 | PASS: 실제 재시작/풀 clear 후 재준비 검사 |
| R03 | 4419/4420 사전 로딩 | PASS: 실제 재시작/풀 clear 후 재준비 검사 |
| R04 | 각 풀 22개 | PASS: 실제 재시작/풀 clear 후 재준비 검사 |
| R05 | 손님 이동 DOTween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R06 | 정렬 상품 풀 9개 | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R07 | 실패 시 원자성/기술 오류 잠금 | PASS: 구매 알림 실패 후 기술 오류 잠금 검사 |
| R08 | 상자·정렬·계산기 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R09 | 허브 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R10 | 엔딩 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R11 | 감독관 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R12 | 장부 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R13 | 딸 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R14 | 도장 tween | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R15 | idle 업데이트 최적화 | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R16 | 먼지 처리 | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R17 | 바코드·청소기 시각 최적화 | PARTIAL: 코드 통합/컴파일 확인, 최종 연출 체감 미확인 |
| R18 | 테스트·검증 문서 | PARTIAL: 자동 검사 실행 결과/남은 실패를 별도 기록 |

## intro-cutscene 체크리스트

| ID | 기능 | 판정 / 범위 |
|---|---|---|
| I01 | Hub→Intro→Main | PASS: 실제 씬 전환 2회 및 완료 횟수 검사 |
| I02 | 완료 한 번·중복 진입 차단 | PASS: 실제 씬 전환 2회 및 완료 횟수 검사 |
| I03 | 9컷+2.5컷 이미지 | PARTIAL: 이미지 10개 원본 시각 점검; 최종 화면 비율 확인 필요 |
| I04 | 클릭·Space·Enter | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I05 | 대화창·화자·스킵 UI | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I06 | 스킵 확인 중 정지·취소 재개 | PASS: 확인창/정산 타이밍/종료 AudioSource 자동 검사 (청취 제외) |
| I07 | 정산 32,000-14,000-7,000-9,000=2,000 | PASS: 확인창/정산 타이밍/종료 AudioSource 자동 검사 (청취 제외) |
| I08 | 라면 4줄 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I09 | 생활음·종료 정리 | PASS: 확인창/정산 타이밍/종료 AudioSource 자동 검사 (청취 제외) |
| I10 | 행복/슬픔 음악 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I11 | 화자별 blip | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I12 | 차량/팡파르·흔들림 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I13 | 구타/암전 효과음 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I14 | 구름·김 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I15 | 중복 1일차 카드 제거 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |
| I16 | 미확보 음원 빈 슬롯 | PARTIAL: noodleSlurp 원본 슬롯 비어 있음; 병합 유실 아님 |
| I17 | 상자 6종 변경 | PARTIAL: 6개 원본 hash 일치 및 실제 3단계 연결; 화면 크기 변화 확인 필요 |
| I18 | Sprite 영역·GUID | STATIC PASS: 기존 GUID/spriteID와 intro import 유지 |
| I19 | 상자 그림자 | STATIC PASS: intro 변경 반영, 기존 조작 영역과 겹침은 화면 확인 필요 |
| I20 | 정면 Y 위치 | STATIC PASS: intro 변경 반영, 기존 조작 영역과 겹침은 화면 확인 필요 |
| I21 | 인트로 입력·타이밍 테스트 | PARTIAL: 소스/참조 유지, 화면·입력·청취 체감 미확인 |

## 별도 이미지·연출 점검

- 상자 6종을 `crate-contact-sheet.png`로 직접 살펴봤다. 1단계 나무, 2단계 녹슨 금속, 3단계 금속과 열린/닫힌 표현이 모두 있다. 2단계 열린 이미지는 122×81, 닫힌 이미지는 153×102이므로 게임 화면에서 열고 닫을 때 크기·위치가 튀는지 수동 점검한다. 1단계 두 이미지는 166×124, 3단계 두 이미지는 153×102이다.
- 인트로 이미지 10개를 `intro-contact-sheet.png`로 따로 살펴봤다. 2.5컷을 포함해 장면별 인물·환경이 들어 있고, 빈 그림/깨진 파일은 보이지 않았다. 이는 축소된 원본 시각 점검으로 최종 게임 화면의 crop·문구·흔들림 승인과 다르다.
- batchmode의 ScreenCapture는 PNG를 생성하지 않았다. 테스트 내 screenshot 호출을 화면 검증 증거로 사용하지 않는다. 런타임 상태/타이밍 검사와 원본 이미지 점검만 완료했다.
- 실제 청취, 화면 클릭·Enter 체감, 여러 해상도 가독성, 구름/김의 경계와 움직임, 장부/딸/엔딩 tween, 상자 그림자·Y 조정에 따른 가림은 사용자 점검 대상이다.
- 라면 먹는 소리 `noodleSlurp`은 intro 원본 씬에서도 비어 있다. 테스트용 음원을 런타임 인스턴스에만 넣어 이벤트를 검사했으며 제품 씬에 가짜 음원을 넣지 않았다.

## 테스트 실패와 최종 상태

전체 검증을 PASS로 선언하지 않는다. 기존 사양과 다른 테스트/미확인 화면 항목은 PARTIAL로 유지한다. 실패를 숨기기 위해 제품 CSV나 효과 타이밍을 이전 값으로 되돌리지 않았다.

- 사용자 지시에 따라 마지막 전체 PlayMode 실행을 중단했다. 추가 PlayMode 검증은 수행하지 않는다. 중단 실행은 성공으로 계산하지 않는다.
- 완료된 최초 전체 PlayMode: 78개/65 성공/13 실패/skip 0 (`merged-play.xml`). 이후 focused 실행에서 TransactionResult/구매 실패 UI/상자 fixture 3개는 통과했지만 전체 재검증 완료로 확대하지 않는다.
- 마지막 완료 focused 실행: 5개/3 성공/2 실패 (`integration-play-2.xml`). Init-Hub-Intro-Main 두 번과 풀 재준비는 성공, 환경 shader 시간 테스트와 Space 입력 테스트는 실패했다. 환경 테스트는 비활성화 중 MPB를 해제하는 제품 동작과 기대값이 다르다. Space 테스트의 새 줄 입력 프레임 보정은 후속 재검증 미완료다.
- 이전 13개 PlayMode 실패 목록:
  - `GameSessionApiTests.DailyMaintenanceInsufficientBalanceDefersWholePayment`
  - `GameSessionApiTests.FacilityControllerNotificationFailurePreservesPurchase`
  - `GameSessionApiTests.GuidelineOverflowPreventsSessionMutation`
  - `GameSessionApiTests.InspectorDaySevenPagesAdvanceOnceWithoutFacilities`
  - `GameSessionApiTests.InspectorMainClockFollowsDayProgressAndPreviewCannotAdvanceBusiness`
  - `GameSessionApiTests.InspectorWorldEffectsPreserveSuspendedTimeAndFlashLifetime`
  - `IntroMealPlaybackTests.ConfirmSkipPauseResumeRestartAndDisableCleanup`
  - `IntroMealPlaybackTests.FourExactLinesInputEatingOnceAndCollapseOnBlack`
  - `IntroMealPlaybackTests.SettlementTimingsColumnsAndContinuousPrelap`
  - `SoundManagerIntegrationTests.CallerCancellationDoesNotCancelSharedInitialization`
  - `SoundManagerIntegrationTests.InitializeLoadsAllSoundClips`
  - `SoundManagerIntegrationTests.MissingResourceFailsWithoutPublishingPartialCacheAndCanRetry`
  - `StoreStagePresentationTests.StagePouringSpritesReachExistingImageAcrossStages`

- EditMode 실패 11개는 병합 전후 동일:
  - `BusinessClockAndSortingTests.Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings`
  - `BusinessClockAndSortingTests.VacuumAsset_UsesAstraImportContract`
  - `CustomerCsvTests.DailyProductSelectorUsesRebalancedKindsAndStageGuarantee`
  - `CustomerCsvTests.ProductCsvUsesRebalancedPricesAndCosts`
  - `EndingPageTests.ActualPagesHaveFourEndingsSilenceAndFinalBlackPage`
  - `EndingPageTests.InvalidPageTablesAreRejected("sfx-zero")`
  - `EndingPageTests.InvalidPageTablesAreRejected("all-empty")`
  - `FacilityTests.CsvExposesUpgradeKindsAndStageContracts`
  - `InspectorEventTests.ActualCsvHasSevenEventsAndMultilinePages`
  - `SoundResourceDataTests.ResourceDataContainsAllSoundAddresses`
  - `SoundResourceDataTests.SoundKeysAreUniqueResourceIds`

- 최종 판정: **PARTIAL**. 병합/참조 보존과 일부 실제 실행은 확인했지만 모든 기능의 정상 동작, 전체 테스트 통과, 화면/음질 승인을 주장하지 않는다.
- Git: refactor_fix와 intro-cutscene은 각각 로컬 merge commit. 이후 검증 fixture/기록 commit을 포함해 total_merge로 fast-forward 통합한다. push 없음.
