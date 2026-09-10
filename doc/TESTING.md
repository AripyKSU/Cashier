# 기능 API 검증

## total_merge 대기열·이미지·도덕성 통합 (2026-09-10)

- EditMode187/187: `Temp/TestResults/20260910-154916-4bd90ea490c04d7c94ddeeb6e0ff7145/EditMode.xml` 및 `.log`.
- 최종 PlayMode34/34: `Temp/TestResults/20260910-160336-90e8c723e12b4bdda3f4df1363320458/PlayMode.xml` 및 `.log`. 실패·skip·미완료0, 실제54Sprite 로드와 명성/성별교대·도덕성·큐/정산 경계 포함.
- 최초 Play 두 실행(155547-b057d3ebc0bb46eca958db1c5b6ac2f9, 155818-1ae3bda81277406592f0c99612608c73)은 각각34중29 SetUp 실패였다. 병합 후 Editor의 오래된 Addressables 상태에 Morality CSV·54이미지 등록이 없었다. dirty group을 `Temp/MainMerge-StaleGroup.asset.txt`에 보관하고 승인된 HEAD 등록을 복원했다. 첫 ForceUpdate 코드는 namespace 오류로 실행되지 않았으며 이후 정확한 import로 실제 메모리70entries/54images/Morality Datas 라벨을 확인했다. 제품 오류 검증을 완화하지 않았다.
- 등록 복원 후160035-104e7ecdd91d4dd994c02cf9490aa3e4는33/34였다. 기존 성별교대 테스트의 1원 수락 가정만 기존 acceptedOffer helper로 수정했다. 가격민감형은 정가만 수락한다는 제품 규칙과 성별교대 assert는 보존했다. 실패 XML·로그도 Temp에 보존한다.
- 실제 Init→Hub→Main, GameUI PreOpen/UsesCustomerQueue=true 확인. `Temp/MainQueue-Smoke.txt`: 높이430, 거래퇴장 중간색/alpha701표본·대기이탈297표본, pause 시 색/alpha/위치/시계 보존. 마감→Settlement 후 waiting/leaving/visuals0 확인. `Temp/MainQueue-Operating.png` 화면 확인, 실제 실행 Console Error0. 이 API 조작 검사는 전체 마우스 사용감이나 다음날 수동 조작 완료를 의미하지 않는다.
- 최종 Play/compile 종료·오류0·MainScene dirty=False. 개인 씬 선택은 Use Main으로 비우고 이전 GUID는 Editor SessionState `MainMerge.PreviousLocal`에 보관했다. start scene은 InitScene, 임시 runInBackground는false로 복원했다. Local 씬·코드 hash는 사전 백업과 동일하다. Main의 추가/수정 YAML 행만 trailing whitespace를 정리했다.

## 상품·외형 이미지 migration (2026-09-10)

- 컴파일 오류 없음. CustomerCsvTests에 실제 CSV 두 FK·45외형·빈값/누락·표시 선택 검사를, GameSessionApiTests에 실제 Sprite54개 ResourceManager 로드를 추가했다.
- 최초 EditMode 시작은 개인 씬 dirty gate로 거부됐다(0건/XML없음). 저장 요청도 안전 검토에서 차단됐으나 직접 승인 작업에서 백업·저장 완료 후 재개했다.
- 최종 EditMode174/174: `Temp/TestResults/20260910-140531-7c3304297493471bb17cf481bc41c3f6/EditMode.xml`. 최종 PlayMode33/33: `Temp/TestResults/20260910-140803-fee27b1d673648d7819b27c78d3be31f/PlayMode.xml` 및 각 로그. 실패·skip·미완료0. 실제 Sprite54개 로드 포함.
- 초기 EditMode 두 실행의 실패는 테스트 가격 공급/정상 Resource 로그 기대 누락이었다. 이후 Play3실패는 기존1프레임 초기화 가정으로, 실제 GameUI 준비까지20초 제한 대기로 수정했다. 제품 계약을 완화하지 않았다. 실패 증거는 `20260910-140305-085bf35bee744f0886f26acfca201591`, `20260910-140411-bebca359348549598a9b3b21c666e561`, `20260910-140531-7c3304297493471bb17cf481bc41c3f6`에 보존한다.
- 최종 compile 오류 없음, Console Error4건은 기대된 알림1/ResourcePool3 실패 주입. 사용자 씬으로 복귀(dirty=False), Play 종료, 개인 playModeStartScene=InitScene 복원. 실제 화면 배치·마우스 사용감은 별도 사용자 확인 대상이다.
- 연결·migration·UI 수동 확인 범위는 [IMAGE_RESOURCE_INTEGRATION.md](IMAGE_RESOURCE_INTEGRATION.md)를 따른다.

## 일일 도덕성 정산 검증 (2026-09-10)

- EditMode 165/165, PlayMode 32/32, 실패·skip·미완료 0. 증거: `Temp/TestResults/20260910-123818-a7254722d2044b398dedfe5636e87a4c/`의 두 XML 및 로그.
- 실제 CSV 방문의 양수·음수 소수점/0/거절, legacy null, 정산 snapshot·일일값 초기화·다음날·거래 없는 날·총누적 유지, 알림 관찰/예외, 일일 decimal overflow 사전 차단을 검사했다. overflow 사례는 누적 필드만 한계로 설정하고 실제 상품·방문 경로를 사용한다.
- 컴파일 완료 후 기존 runner의 assembly 단위 실행을 한 차례 사용했다. 개인 시작 씬을 테스트 동안 임시 해제하고 종료 후 복원했다. UI 조작은 수행하지 않았다.

## 도덕성 거래 연동 검증 (2026-09-10)

- 사용자 인게임 확인 완료. 도덕성 값 변경 시 현재 값과 변화량을 출력하는 로그를 포함한다. 로그 추가 후 컴파일 오류 없음 확인; 아래 자동 테스트 결과는 로그 추가 전 실행이다.
- 최종 EditMode `20260910-115037-ae34232897084aa784c82babd5be055a`: 165/165, 실패·skip·미완료 0. 추가된 PriceSensitive 실제 제출 경계 검사를 포함한다.
- 최초 전체 실행 `20260910-114237-fe402f4d842447a6879f646c92d87897`은 EditMode 164건 중 기존 기대값 3건이 신규 ID·수락 상한과 달라 실패했고 PlayMode는 시작하지 않았다.
- 수정 후 `20260910-114413-594cfc38ff1841faac99cbaa9db07d87`: EditMode 164/164 통과. PlayMode는 옛 1원 수락 가정 3건이 실패했다.
- 제품 규칙을 유지하고 기존 테스트 입력을 성향별 수락가로 수정했다. PriceSensitive 현재가-1/동일/+1 검사는 1/1 통과했다.
- 최종 PlayMode `20260910-114825-735336072f404f3c8bbb12cf1eda7bcd`: 30/30, 실패·skip·미완료 0. 실제 Datas 로더, decimal 누적, 정상 재정 알림 관찰값과 알림 예외 후 거래·잔고·도덕성 동시 보존을 포함한다.
- 상세 계약은 [MORALITY_INTEGRATION.md](MORALITY_INTEGRATION.md)를 따른다. UI 노출은 구현·검증 범위가 아니다.

## 대기열 진행 연결 검증 (2026-09-09)

- `codex/customer-queue-scene`: EditMode 162/162, PlayMode 28/28, 실패·skip·미완료 0.
- 증거: `Temp/TestResults/20260909-161215-f9d249073a0041899dd3ce93677922f5/`의 XML 및 로그. Temp는 Git 제외다.
- 추가 4건은 FIFO·빈 계산대·pause, 긴 프레임 만료 우선·Closing 마지막 거래, 빈 계산대의 만료 방문 배제, 실제 GameUI 옵션·빈 화면 정리·최종 퇴장 후 정산 표시를 검증한다.
- 테스트 중 개인 playModeStartScene을 임시 해제하고 종료 후 복원한다. 개인 씬의 실제 이동·말풍선·배치는 이번 자동 결과로 검증했다고 간주하지 않으며 사용자 확인이 남는다.

Unity Test Framework 1.6.0의 NUnit/Test Runner를 사용한다. UI/UX 배치·문구·버튼·연출은 사용자 수동 확인이며 API 통과와 분리한다.

## 실행 및 결과

컴파일이 끝난 EditMode에서 Window → General → Test Runner의 Cashier.EditMode.Tests/Cashier.PlayMode.Tests를 실행한다. Save Results로 XML을 저장할 수 있다.

반복 로컬 검증에서 두 모드의 XML·로그 자동 보관과 0건/실패/skip 종료 판정이 필요하면 유일한 셸 진입점을 사용한다.

```powershell
./Tools/Run-Tests.ps1
./Tools/Run-Tests.ps1 -Mode EditMode
./Tools/Run-Tests.ps1 -Mode PlayMode
```

- 기존 Check-CustomerGenerator/Csv/Queue/PriceEvents/RadioTiming 및 ValidateResourcePool 6개 셸은 대응 suite 전환 후 제거했다. UI 전용 Check-CustomerOutcomes/TradeUI/MainSceneIntegration 3개도 제거했고 UI는 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)·[CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md)의 수동 확인 절차를 따른다. API가 UI 검증을 대체했다는 의미가 아니다. 삭제 전 버전은 Git 이력에서 복구할 수 있다.
- Run-Tests.ps1은 테스트 본문을 포함하지 않는다. CashierTestRun.Start 호출과 완료 XML 검증만 한다. 표준 GUI는 자동 모드 순차 실행·고유 경로 로그 보관·셸 실패 종료를 함께 제공하지 않으므로 이 한 개만 유지한다. 새 runner/framework는 추가하지 않았다.
- 자동 결과는 Temp/TestResults/<실행ID>/EditMode.xml 또는 PlayMode.xml과 .log다. 매번 새 ID를 사용한다. Temp는 Git 제외이며 장기 증거는 별도 보관한다.
- total=0, failed>0, skip/미완료, 비정상 루트 결과, 시간초과, XML 읽기 실패는 성공이 아니다. timeout 후 기존 Unity job 종료를 확인하기 전 재실행하지 않는다.
- 사용자 Play·컴파일·미저장 씬·자체 pending·다른 GUI/API job이 있으면 시작 전에 거부한다. 다른 job 조회가 없거나 실패해도 옵션·SessionState·폴더를 변경하기 전에 거부한다.
- Test Framework 1.6.0의 RunFinished는 씬/옵션 cleanup보다 먼저 온다. 설치된 내부 읽기 전용 TestRunnerApi.IsRunActive 및 runInBackground 복원 확인 후 임시 XML을 최종 경로로 이동한다. 패키지 갱신 시 이 의존을 재검토한다. 최종 이동 실패는 경로·예외를 한 번 기록하고 pending을 해제하며 완료 XML이 없으므로 외부 실행은 실패한다.
- PlayMode fixture는 임시 씬에 소유 manager·provider·locator·객체만 생성/해제한다. 사용자 씬·prefs·세이브는 수정하지 않는다. GUI 직접 실행 시 자동 경로 보관은 적용되지 않는다.

## 현재 진행 통합 검증 (2026-09-09)

기준 checkout: codex/equipment-upgrades, total_merge 9b0a55a. 상세 호출 계약·Closing 정책·설비 후속 경계는 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)를 따른다.

- EditMode 123/123, PlayMode 18/18, Failed=0, skip/미완료=0. 증거: Temp/TestResults/20260909-100839-4f7cdc689523421194b222074527f894/{EditMode,PlayMode}.xml 및 .log.
- 기존 GameSessionApiTests 4건에 실제 GameProgress/DayProgress 경유 9건을 추가해 13건이며 ResourcePoolTests 5건은 유지한다. 기존 132개만 실행해 새 진행 통합을 PASS로 주장하지 않았다.
- 추가 사례: 원본 거래 결과/판매 수량/원가 보존·입력 오류 재제출·중복 제출 거부, 지침 snapshot 보존, 정산 false/overflow 실패 후 판정 보존·진행/재접수 차단(2건), 일반일/상납 성공·부족/중복 완료(2건), pause/60초 방송/손님 snapshot, 긴 프레임과 Closing 마지막 거래, 현재가 가격표/해금일/누락 단가·날짜 불일치.
- 지침 검사는 테스트 생성기로 만든 방문을 진행 경계에 주입하며 실제 지침 공급을 제품에 추가하지 않는다. 긴 프레임 검사는 테스트에서 예약 대기값만 고정한다. 제품 난수·CSV·기획수치는 변경하지 않았다.
- DayProgress는 visit.Result.Value를 그대로 TryApplyTransaction에 전달한다(호출부 정적 확인). 경제 집계는 아직 상세 판매/위반을 저장하지 않고 매출·명성 변화만 사용한다. 결과 보존 검사 통과가 원가 차감·지침 벌칙 구현을 뜻하지 않는다.
- API 실행 후 상태·컴파일·Console 확인은 작업 보고에 남긴다. UI/prefab/Scene/Addressables는 수정하지 않았으며 UI/UX 실제조작은 미실행이다. 최초 Unity heartbeat 지연은 컴파일 증거로 쓰지 않고 연결 복구 후 컴파일·실행했다.

## 명세·구현·등록 검사 대응

기준 기획: [문서 - 퍼즐게임](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit)의 9/8 회의록·경제 시스템 구조 개발 문서(2026-09-08 조회, 수정 시각 07:56:55.006Z UTC). 프로젝트 책임자의 후속 합의와 실제 구현 계약을 우선하며 공식 문서는 변경하지 않았다.

| 등록 suite / 로컬 상세 명세 | 실제 API와 확인 사례 | 대조 결과·경계 |
|---|---|---|
| CustomerContractTests / [CUSTOMER_INTEGRATION](CUSTOMER_INTEGRATION.md) | Generate, BeginOffer, SubmitOffer(long, IReadOnlyList<SaleItem>), Result, Depart: 타입 균등/OR 선호/속성/seed/4판정/정가 범위/최종 수량·단가·원가/지침 AND/오류 원자성 | 현재 3차 계약 일치. 가격 공급 1회와 불변 snapshot 검사. 실제 지침 공급·명성·원가 정산은 미연결. 최종 목록 UI 경로는 존재하되 UX는 수동 확인 |
| CustomerCsvTests / [CSV_RULES](CSV_RULES.md), [DATA_RULES](DATA_RULES.md), CUSTOMER_INTEGRATION | 실제 CsvHelper·전용 LoadData·CustomerCatalog.ValidateAndCommit·enum routing, 정상 1건+오류 56건 | 57건. 추가 종류 생성 없음. PK 중복 사례는 실제 정상 행을 복제해 파싱 오류와 구분. Resource 주소 전체 실자산 검사는 아님 |
| CustomerQueueTests / [CUSTOMER_QUEUE_INTEGRATION](CUSTOMER_QUEUE_INTEGRATION.md) | Start/Advance/TryAdd/TakeNext/Stop/GetSpeech: 5초 입장·10명 정원·FIFO·재촉/이탈 1회·pause/close·긴 프레임 | 시간 계약 일치. 위치/말풍선/3초 결과 후 화면 자동 인계는 수동 UI 확인 |
| PriceEventTests / [PRICE_EVENT_INTEGRATION](PRICE_EVENT_INTEGRATION.md) | IsDue/CreateDay/ApplyRadio/GetRadioDelaySeconds: 실제 CSV/FK 준비, 날짜·4채널 조합·seed·FK/가중치 오류·버림/하한/overflow·불변/중복 효과 | 누락됐던 4조합·seed/FK/가중치 오류 보완. 후보가 있는 라디오는 예약되며 별도 발생 확률 상수 없음. 모든 CSV 음성 조합을 전수 검사한 것은 아님 |
| GameSessionApiTests / 가격 이벤트 명세·9/8 회의록·경제 개발 문서 | 실제 Resource/CSV 초기화→InitializeNewGame→EnsureDailyPrices→BeginTradingDay→AdvanceTradingTime→SubmitOffer→TryApplyTransaction→EndTradingDay→CompleteDay, 종료 정리 | 매출 증가/종료 후 거부/날짜 전이·가격 공급 일치. 중복 초기화·잘못된 시간/조기 날짜 완료 및 위 실제 Progress 통합 사례 추가. 경제 전체 suite로 확대하지 않음 |
| ResourcePoolTests / [RESOURCE_POOL_CONTRACT](RESOURCE_POOL_CONTRACT.md) | LoadAssetAsync/Task/callback, Release/ReleaseAll, SimplePool/Manager 생성·prewarm·대여·반환·Clear: 취소·실패·retry·타입/소유권·cleanup | 기존 계약 일치. 임시 provider로 실제 Addressables 경유. 원격 장애·플랫폼·제품 prefab 검증 아님 |
| CashierTestRunTests (실행 도구 안전성) | Start 재진입 거부, SessionState/옵션/출력 경로 미변경 | 자체 pipeline 및 독립 TestRunnerApi 실행 중 각각 확인. 제품 기능 suite 아님 |

### 미검증·기획 불일치·외부 통합 보류

- 경제 문서의 FinanceService.CanAfford/TrySpend/BalanceChanged, MaintenanceService의 성공/부족/순차 회차/MaintenancePaid, LogService의 순서·전후 금액/Dispose 구독해제는 코드에 있지만 등록된 세션 fixture가 전체 계약을 독립 검증하지 않는다. 현재 매출 AddIncome·Query 잔고·종료 거부·session teardown 증거를 경제 전체 PASS로 확장하지 않는다. 추가 경제 suite는 별도 범위다.
- TryApplyTransaction은 거래 ID 중복 제거 API가 아니다. 현 DayProgress가 방문 상태로 중복 전달을 거부하고 접수 실패 latch를 소유한다. UI 버튼의 실제 조작/오류표시는 수동 검증 대상이다.
- 상품 원가는 SoldItems/CostTotal에 기록만 한다. 일일 원가 차감·명성 계산·지침 벌칙은 미연결이다. 명성0~100 등의 구형 예시를 기대값으로 복원하지 않았다.
- 과거 손님 문서의 공통 행복도·무작위 이탈과 최신 보류/제거 기획이 충돌한다. 현 대기열은 성향별 시간 만료 계약만 검사한다. 현 Normal/Hasty/PriceSensitive 유지+Wealthy 타입 추가 합의가 오래된 성향 목록보다 우선한다. Wealthy 데이터 행은 없다.
- 회의록의 라디오 추후 피처 표기와 이미 승인·구현된 기능을 구분한다. 현재 API 회귀 통과는 최종 기획 활성화 승인이나 UI 완성을 뜻하지 않는다.
- MainScene bootstrap/직렬화·레이아웃·폰트·이미지 실자산·버튼/연출·Player build·저장 복원은 이번 자동 API 범위 밖이다.

## Assembly·병합 의존

- Cashier.Runtime + Cashier.Scene.Editor + Cashier.EditMode.Tests/Cashier.PlayMode.Tests 및 각각의 Unity 생성 meta를 함께 반영한다. global namespace와 기존 script GUID를 유지한다. 새 package/vendor 변경 없음.
- LoadingScene의 fade는 DOTween Modules 확장 대신 DLL To/SetTarget/Kill을 사용해 0.3초 OutQuad 및 OnDisable 해제를 유지한다. 이는 assembly 경계에 필요한 제품 수정이며 실제 시각 결과는 사용자 확인 대상이다.
- CustomerSandbox/CustomerSandboxSetup의 기존 추적 경로 삭제, .gitignore/AGENTS/SCENE_WORKFLOW 변경을 함께 반영한다. 실제 Assets/Scripts/Local 코드·meta는 보존하고 stage하지 않는다. 현 실제 UI는 GameUI.prefab의 GameUIController이며 비활성화된 Dev3 파일은 이번 범위에서 정리하지 않았다.
- 3차 제품 계약은 customer_sys 5ad5fa8까지 이미 push됐고, 이 Test Runner 전환·Local 분리·문서 정리는 그 이후 변경이다. 최신 HEAD/remote 여부는 최종 Git 실행 시 재확인한다.
- 다른 branch에 이식할 때 product cost_price 및 성향 disposition_type/preferred_product_idxs/regular_price_min_rate/regular_price_max_rate를 포함한 현재 CSV와 DTO/loader/catalog/방문·TransactionResult를 부분 복사하지 않는다.
- TMP fallback dirty와 stash, 개인 Local 파일, Temp 검사 증거, 의도치 않은 ProjectSettings 변경은 commit 대상이 아니다. 기본 branch merge는 별도 승인·교차 리뷰 대상이다.

## 손님 속성 세 축 검증 (2026-09-09)

- `CustomerAttributes`는 기존 비트 유지+Adult16/Normal32만 추가했다. 성별2×연령3×특수1=6조합, 특수 속성 Wealthy/Poor는 없고 성향 enum은 변경하지 않았다.
- Unity 6000.3.18f1, Cashier PID29172, `Tools/Run-Tests.ps1`: EditMode139/139, PlayMode20/20, 실패0·skip/미완료0.
- 증거: `Temp/TestResults/20260909-124924-72e789a824f34e5d80227f188ac3806b/`의 EditMode.xml/PlayMode.xml 및 각각 .log. Git 제외 임시 증거다.
- CustomerContractTests에서6조합 도달·균등성·외형/성향 독립·seed 재현, 부분조건/완전프로필 분리, 미정의bits·동축중복·축누락 및 실제 방문 생성자 거부, Adult/Normal/Child 단독·세 축 AND 지침을 검사했다. 기존 성향 Wealthy도 Normal 속성과 별개이며 금액 효과가 없음을 확인했다.
- 최초 테스트 작성에서 internal CustomerOrderItem 생성자 접근 컴파일 오류를 정상 생성기 결과 재사용으로 수정한 뒤 위 전체 실행이 통과했다. 최종 compiling=false, scriptCompilationFailed=false, Play 종료/InitScene 복귀 확인.
- Console Error3건은 ResourcePoolTests의 기존 실패 주입(LogAssert.Expect)이며 제품 오류와 구분한다. CSV·Scene/prefab·Addressables·Local·저장 데이터는 수정하지 않았고 UI/UX는 미검증이다. 새 저장 migration과 Git 작업은 수행하지 않았다.

## 설비 구매·다음날 해금 검증 (2026-09-09)

- Unity 6000.3.18f1, Cashier PID16200, `Tools/Run-Tests.ps1`: EditMode137/137, PlayMode20/20, 실패0·skip/미완료0. 컴파일 완료 후 실행했다.
- 증거: `Temp/TestResults/20260909-121421-ae5019f5ba46414c872ce55c0161bdf9/`의 EditMode.xml/PlayMode.xml 및 각각 .log. Temp 파일은 Git에 넣지 않는다.
- 신규 FacilityTests14건과 GameSessionApiTests의 실제 CSV·Addressables 로딩/다음날 해금/새 세션 초기화를 포함한다. 처음 실패한 구형 비활성상품 제출 fixture와 영업 전 null 방문 캡처는 테스트 입력·순서만 고친 뒤 재실행했다. 제품 잠금 규칙은 완화하지 않았다.
- 종료 Console Error3건은 ResourcePoolTests의 명시적 실패 주입(LogAssert.Expect)이다. 신규 제품 오류와 구분한다. 컴파일 오류 없음; 화면 조작·실제 씬 전환·설비 구매 UI는 검증하지 않았다.
- 데이터·사용법·배포 묶음은 [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md)를 따른다.

## 설비 상점 UI 검증 (2026-09-09)

- EditMode 141/141: `Temp/TestResults/20260909-133440-6d6d6167362148548c3ca3a76ccee187/EditMode.xml`.
- PlayMode 23/23, 실패·skip·미완료 0: `Temp/TestResults/20260909-134450-0c7ecbb9dff14f0597961e0382dcb326/PlayMode.xml` 및 `.log`.
- 최초 Play 실행은 개인 `playModeStartScene=InitScene`으로 실제 Local 게임 씬이 시작되어 180초 timeout, 결과 XML 미생성으로 BLOCKED였다. 해당 job만 CancelTestRun으로 정리하고 idle 확인 후 개인 시작 씬 설정을 임시 해제해 새 runId로 재검증했다. 종료 후 InitScene 설정을 복원했다. 테스트 실행 시 개인 playModeStartScene을 보존→임시 null→종료 후 복원한다. 공유 runner나 제품 흐름은 변경하지 않는다.
- 상점 스냅샷/FK/날짜 상한, 실제 prefab 버튼 구독 수명, 중복 구매 방지, 잔고 변경, 모달 뒤 입력 차단, 다음날 활성화, 결제 알림 예외 후 소유권 보존과 UI 잠금을 검사했다. 기대된 실패 주입 로그는 제품 오류와 구분한다.
- 실제 Local 부트스트랩의 상점 열기·구매·닫기·다음날 활성화도 확인했다. `Temp/FacilityShop-Open.png`, `Temp/FacilityShop-Purchased.png`는 임시 화면 증거이며 Git 제외다. 최종 사용자 UI/UX 확인은 별도다.

## total_merge 설비·명성·MainScene 통합 검증 (2026-09-09)

- 입력: 설비 `65888e1083d3592788210f295bb0d2bdad4d3814`, 명성 `6976218c38b2678f635248a1a5e7c7880e72615a`; 대상 최초 `9b0a55a071d4172ffbf88ff0d6912702e26e73f7`. 설비 fast-forward 후 명성 merge 이력을 보존하며 원본 branch·Local·stash는 변경하지 않는다.
- Unity 6000.3.18f1/Cashier PID29172, 컴파일 오류0. `Temp/TestResults/20260909-143834-2a5a84abea594862a0c5c63ca57594ae/`: EditMode162/162, PlayMode24/24, 실패0·skip/미완료0. 두 XML과 .log 보존. 테스트 중 개인 playModeStartScene을 null로 하고 종료 후 InitScene을 복원했다.
- 최초 통합 EditMode162중 명성21건은 동일 SetUp FK 오류였다. 명성 fixture가 새 FacilityData CSV를 로드하지 않아 product1005→facility12001 참조 검증에 실패했다. 실제 테이블 로드·주입만 보완하고 제품 검증은 완화하지 않았다. 실패 증거 `20260909-143708-86a644876086423b9036d6a9548169a5`도 보존한다.
- 새 복합 테스트는 할인5건+거절1건의 원본 거래/원가/로그 보존, 설비1회차감, 날짜 완료와 명성1회 적용, 다음날 시설 활성 및 GameProgress 재생성 시 명성·로그 보존을 검사한다. 기존 상납 성공/실패·라디오·시설모달·오류주입 회귀도 통과했다.
- 실제 Init→Hub→Main bootstrap에서 UI ready와 manager 각각1개를 확인했다. 버튼 listener와 Progress API로 할인5건 정산→설비12001 구매→닫기→NEXT를 실행: 잔액95005G, 정산명성+10 피드백, 당일미활성/현재명성0, 다음날 elapsed1/명성10/snapshot10/시설활성을 확인했다. 최종 제품 Console Error0.
- 최초 실제 화면 검사에서 정산 명성 텍스트 참조가 없음을 발견해 기존 GameUI에 TMP1개를 연결했다. 자산 연결 후 동일 실제 정산·구매·다음날 경로를 다시 확인했다. `Temp/IntegratedMain-PreOpen.png`, `Temp/IntegratedMain-Facility.png`, `Temp/IntegratedMain-Settlement-Final.png`는 Git 제외 화면 증거다.
- 명성 이미지·명성에 따른 생성비율·지침 벌칙·원가차감·저장 복원·Player build는 미구현/미검증 범위를 유지한다. 전체 기능이라는 표현은 두 입력 branch에서 구현된 기능의 통합이며 보류 기능 신규 구현을 뜻하지 않는다. 실제 마우스/키보드 사용감은 사용자 확인 대상이다.

## 이전 Test Runner 전환 검증 기록 (2026-09-08)

| suite | Passed | Failed | skip/미완료 |
|---|---:|---:|---:|
| CustomerContractTests | 54 | 0 | 0 |
| CustomerCsvTests | 57 | 0 | 0 |
| CustomerQueueTests | 3 | 0 | 0 |
| PriceEventTests | 8 | 0 | 0 |
| CashierTestRunTests | 1 | 0 | 0 |
| GameSessionApiTests | 4 | 0 | 0 |
| ResourcePoolTests | 5 | 0 | 0 |

- EditMode 123/123, PlayMode 9/9: Temp/TestResults/20260908-171209-4c20b8585d4e41e6b3db55c51b578fa4/ 아래 각 XML 및 .log. 기존 기록117/8 이후 누락 사례와 runner guard를 추가해 재실행했다.
- 독립 TestRunnerApi 필터 실행: CashierTestRunTests.RejectStartWhileRunnerActive 1/1. 자체 pipeline과 별개로 진행 중 API job의 중복 시작 차단 확인(GUI 클릭 자체를 수행한 것은 아님).
- 최종 File.Move 실패 주입: 없는 tmp 경로에서 finishAfterCleanup 두 번 호출, FINALIZE_GUARD_PASS logs=1 pending=empty no_final_xml background_restored. 예상 Error 1건을 별도로 발생시켰고 세션 플래그 복원. update 반복 오류 없음.
- CSV 오류 56건과 Play provider/풀 오류 3건은 LogAssert.Expect로 확인한다. 이 예상 로그 및 위 실패 주입 로그가 Console에 남을 수 있으며 제품 오류와 구분한다.
- 이전 전환 중 NUnit 인자 타입/fixture FK·기대 로그·XML 쓰기 경합/cleanup 순서 문제는 수정 후 위 실행이 통과했다. 초기 실패 XML은 기존 Temp 폴더에 보존했다.

## 손님 구성 선택 통합 검증 (2026-09-09)

- `unity-cli editor refresh --compile --ignore-version-mismatch`로 최신 runtime/test assembly 컴파일 완료를 확인했다.
- EditMode 175/175, PlayMode 25/25, 실패·skip 0. 새 selector 명성 가중치·설비 선호·성별 교대, 타입 전용 명성 매핑과 기존 손님·CSV·시설·진행 회귀를 포함한다. 증거: `Temp/TestResults/20260909-customer-spawn-edit/EditMode.xml`, `Temp/TestResults/20260909-customer-spawn/PlayMode.xml`.
- 실제 성별별 이미지 asset 연결과 UI/UX 표현은 범위 밖이며 수동 확인 대기다. 선호 타입 상품의 명성 정산은 `ReputationDispositionRules`가 선호 필드를 참조하지 않는 것으로 고정한다.
