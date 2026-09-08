# 기능 API 검증

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

## 명세·구현·등록 검사 대응

기준 기획: [문서 - 퍼즐게임](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit)의 9/8 회의록·경제 시스템 구조 개발 문서(2026-09-08 조회, 수정 시각 07:56:55.006Z UTC). 프로젝트 책임자의 후속 합의와 실제 구현 계약을 우선하며 공식 문서는 변경하지 않았다.

| 등록 suite / 로컬 상세 명세 | 실제 API와 확인 사례 | 대조 결과·경계 |
|---|---|---|
| CustomerContractTests / [CUSTOMER_INTEGRATION](CUSTOMER_INTEGRATION.md) | Generate, BeginOffer, SubmitOffer(long, IReadOnlyList<SaleItem>), Result, Depart: 타입 균등/OR 선호/속성/seed/4판정/정가 범위/최종 수량·단가·원가/지침 AND/오류 원자성 | 현재 3차 계약 일치. 가격 공급 1회와 불변 snapshot 검사. 실제 지침 공급·명성·원가 정산·최종 목록 UI는 미연결 |
| CustomerCsvTests / [CSV_RULES](CSV_RULES.md), [DATA_RULES](DATA_RULES.md), CUSTOMER_INTEGRATION | 실제 CsvHelper·전용 LoadData·CustomerCatalog.ValidateAndCommit·enum routing, 정상 1건+오류 56건 | 57건. 추가 종류 생성 없음. PK 중복 사례는 실제 정상 행을 복제해 파싱 오류와 구분. Resource 주소 전체 실자산 검사는 아님 |
| CustomerQueueTests / [CUSTOMER_QUEUE_INTEGRATION](CUSTOMER_QUEUE_INTEGRATION.md) | Start/Advance/TryAdd/TakeNext/Stop/GetSpeech: 5초 입장·10명 정원·FIFO·재촉/이탈 1회·pause/close·긴 프레임 | 시간 계약 일치. 위치/말풍선/3초 결과 후 화면 자동 인계는 수동 UI 확인 |
| PriceEventTests / [PRICE_EVENT_INTEGRATION](PRICE_EVENT_INTEGRATION.md) | IsDue/CreateDay/ApplyRadio/GetRadioDelaySeconds: 실제 CSV/FK 준비, 날짜·4채널 조합·seed·FK/가중치 오류·버림/하한/overflow·불변/중복 효과 | 누락됐던 4조합·seed/FK/가중치 오류 보완. 후보가 있는 라디오는 예약되며 별도 발생 확률 상수 없음. 모든 CSV 음성 조합을 전수 검사한 것은 아님 |
| GameSessionApiTests / 가격 이벤트 명세·9/8 회의록·경제 개발 문서 | 실제 Resource/CSV 초기화→InitializeNewGame→EnsureDailyPrices→BeginTradingDay→AdvanceTradingTime→SubmitOffer→TryApplyTransaction→EndTradingDay→CompleteDay, 종료 정리 | 매출 증가/종료 후 거부/날짜 전이·가격 공급 일치. 중복 초기화·잘못된 시간/조기 날짜 완료 검사 보완. 경제 전체 suite로 확대하지 않음 |
| ResourcePoolTests / [RESOURCE_POOL_CONTRACT](RESOURCE_POOL_CONTRACT.md) | LoadAssetAsync/Task/callback, Release/ReleaseAll, SimplePool/Manager 생성·prewarm·대여·반환·Clear: 취소·실패·retry·타입/소유권·cleanup | 기존 계약 일치. 임시 provider로 실제 Addressables 경유. 원격 장애·플랫폼·제품 prefab 검증 아님 |
| CashierTestRunTests (실행 도구 안전성) | Start 재진입 거부, SessionState/옵션/출력 경로 미변경 | 자체 pipeline 및 독립 TestRunnerApi 실행 중 각각 확인. 제품 기능 suite 아님 |

### 미검증·기획 불일치·외부 통합 보류

- 경제 문서의 FinanceService.CanAfford/TrySpend/BalanceChanged, MaintenanceService의 성공/부족/순차 회차/MaintenancePaid, LogService의 순서·전후 금액/Dispose 구독해제는 코드에 있지만 등록된 세션 fixture가 전체 계약을 독립 검증하지 않는다. 현재 매출 AddIncome·Query 잔고·종료 거부·session teardown 증거를 경제 전체 PASS로 확장하지 않는다. 추가 경제 suite는 별도 범위다.
- TryApplyTransaction은 거래 ID 중복 제거 API가 아니다. UI 호출자가 한 번 전달할 책임을 가지며 이 UI 게이트는 수동 검증 대상이다.
- 상품 원가는 SoldItems/CostTotal에 기록만 한다. 일일 원가 차감·명성 계산·지침 벌칙은 미연결이다. 명성0~100 등의 구형 예시를 기대값으로 복원하지 않았다.
- 과거 손님 문서의 공통 행복도·무작위 이탈과 최신 보류/제거 기획이 충돌한다. 현 대기열은 성향별 시간 만료 계약만 검사한다. 현 Normal/Hasty/PriceSensitive 유지+Wealthy 타입 추가 합의가 오래된 성향 목록보다 우선한다. Wealthy 데이터 행은 없다.
- 회의록의 라디오 추후 피처 표기와 이미 승인·구현된 기능을 구분한다. 현재 API 회귀 통과는 최종 기획 활성화 승인이나 UI 완성을 뜻하지 않는다.
- MainScene bootstrap/직렬화·레이아웃·폰트·이미지 실자산·버튼/연출·Player build·저장 복원은 이번 자동 API 범위 밖이다.

## Assembly·병합 의존

- Cashier.Runtime + Cashier.Scene.Editor + Cashier.EditMode.Tests/Cashier.PlayMode.Tests 및 각각의 Unity 생성 meta를 함께 반영한다. global namespace와 기존 script GUID를 유지한다. 새 package/vendor 변경 없음.
- LoadingScene의 fade는 DOTween Modules 확장 대신 DLL To/SetTarget/Kill을 사용해 0.3초 OutQuad 및 OnDisable 해제를 유지한다. 이는 assembly 경계에 필요한 제품 수정이며 실제 시각 결과는 사용자 확인 대상이다.
- CustomerSandbox/CustomerSandboxSetup의 기존 추적 경로 삭제, .gitignore/AGENTS/SCENE_WORKFLOW 변경을 함께 반영한다. 실제 Assets/Scripts/Local 코드·meta는 보존하고 stage하지 않는다. MainScene에서 참조하는 Dev3SandboxTester는 공유 유지한다.
- 3차 제품 계약은 customer_sys 5ad5fa8까지 이미 push됐고, 이 Test Runner 전환·Local 분리·문서 정리는 그 이후 변경이다. 최신 HEAD/remote 여부는 최종 Git 실행 시 재확인한다.
- 다른 branch에 이식할 때 product cost_price 및 성향 disposition_type/preferred_product_idxs/regular_price_min_rate/regular_price_max_rate를 포함한 현재 CSV와 DTO/loader/catalog/방문·TransactionResult를 부분 복사하지 않는다.
- TMP fallback dirty와 stash, 개인 Local 파일, Temp 검사 증거, 의도치 않은 ProjectSettings 변경은 commit 대상이 아니다. 기본 branch merge는 별도 승인·교차 리뷰 대상이다.

## 최종 검증 기록 (2026-09-08)

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
