# Cashier 스크립트·API 안내

기준: 2026-09-09 total_merge 기반 진행 통합 코드. 상세 계약을 복제하지 않고 기능별 권위 문서와 호출 순서를 연결한다. 명세의 예정 기능을 구현 완료로 해석하지 않는다.

## 경로와 assembly

| 경로 | 책임 |
|---|---|
| Assets/Scripts/Commons/Data | 공용 상품·분류·텍스트·리소스·가격 이벤트 DTO/DataTable |
| Assets/Scripts/Customer | 생성·방문·최종 판매·대기열 |
| Assets/Scripts/Customer/Data | 손님 DTO/DataTable/CustomerCatalog |
| Assets/Scripts/Events | PriceEventScheduler·DailyPriceState |
| Assets/Scripts/Finance | FinanceService·DailyAggregationService·MaintenanceService·Query/Log·TransactionResult |
| Assets/Scripts/Finance/Data | 경제 CSV DTO/DataTable |
| Assets/Scripts/Manager | Resource/DataTable/GameScene/GameSession/SimplePool manager |
| Assets/Scripts/Scene | 공유 Scene component |
| Assets/Scripts/UI | ProgressViewDataFactory·Presenter·SaleSortingPanel 등 실제 UI 코드. Dev3는 현재 비활성화된 이전 코드 |
| Assets/Scripts/Progress | GameProgress·DayProgress: 명시 주입된 세션의 날짜·경제·방송을 연결 |
| Assets/Scripts/Scene/Editor | 공유 Editor 설정 도구 |
| Assets/Scripts/Utils | 공용 유틸·SimplePool·Singleton·CSV converter |
| Assets/Scripts/Local | 개인 CustomerSandbox 및 Editor 설치기. Git 제외·공유 코드에서 참조 금지 |
| Assets/Tests/EditMode, PlayMode | NUnit API 검사. UI 자동검사 아님 |

공유 runtime은 Cashier.Runtime, 공유 Editor는 Cashier.Scene.Editor, 검사는 Cashier.EditMode.Tests/Cashier.PlayMode.Tests assembly다. asmdef와 Unity 생성 meta를 함께 반영한다. namespace는 global 유지. 개인 Editor assembly는 Local에 보존하되 Git 제외한다.

## 초기화·날짜·거래 호출 순서

1. InitScene이 ResourceManager.InitAsync()와 DataTableManager.EnsureDataLoadedAsync()를 기다린다. 실패하면 입력을 활성화하지 않는다.
2. GameSessionManager.InitializeNewGame(dataTables)가 검증된 경제 데이터로 EconomyRuntime 하나와 당일 가격을 준비한다. 중복 초기화는 예외다.
3. GameSceneManager의 Hub/Main 또는 개인 씬 전환을 따른다. UI가 manager/CSV loader/EconomyRuntime을 중복 생성하지 않는다.
4. GameProgress(session, catalog, random, duration)의 CurrentDay는 세션 경과일+1이다. DayProgress가 BeginTradingDay를 호출하고 Tick에서 pause/남은 영업시간으로 제한한 시간을 AdvanceTradingTime에 전달한다. Closing 마지막 거래 정책은 유지한다.
5. CustomerGenerator.Generate에 최신 가격 조회 함수와 선택적 지침 공급자를 전달한다. 현 DayProgress는 방문을 순차 생성해 BeginOffer()를 호출하며 CustomerQueue는 현 UI에 연결하지 않았다.
6. SubmitOffer(long offeredTotal, IReadOnlyList<SaleItem> saleItems)로 한 번 판정한다. 수락 결과 visit.Result.Value를 Economy.DailyAggregationService.TryApplyTransaction에 호출자가 한 번만 전달한다. 이 API 자체에는 거래 ID 중복 제거가 없다. DayProgress는 방문 상태로 재접수를 거부하고 false/예외면 실패 latch로 Tick/결과완료/정산 진행을 차단한다.
7. 결과 표시 후 Depart(). EndTradingDay(out DailyAggregationResult)로 원본 집계 결과를 받아 닫고 필요한 상납 완료 후 CompleteDay(completedDay). 다음 EnsureDailyPrices()는 기본가격에서 다시 계산한다.
8. 세션 종료 시 GameSessionManager가 EconomyRuntime.Dispose()로 로그 구독을 해제한다. 화면은 소유 구독·임시 객체만 정리한다.

주문 Items는 최초 희망 표시값이고 SoldItems/CostTotal은 확정 결과다. 명성 산정·원가 차감·일일 원가 집계·정식 지침 공급은 미연결이다. 선택 목록 UI는 GameUIController/SaleSortingPanel 경로가 존재하며 실제 UX는 수동 확인한다. 자세한 값·오류·스냅샷 계약은 [CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md), [PRICE_EVENT_INTEGRATION.md](PRICE_EVENT_INTEGRATION.md), [CUSTOMER_QUEUE_INTEGRATION.md](CUSTOMER_QUEUE_INTEGRATION.md)를 따른다.

## 데이터 API

- DataTableManager.GetDB<T>(uint idx) / GetDB<T>(DataTableType), GetDataCount<T>(DataTableType), Customers로 검증 완료 데이터를 조회한다.
- 각 전용 DataTable은 IDataLoad.GetDataCount(), LoadData(string), Release()를 구현한다. 파싱·공개·Release는 manager 소유이며 소비자가 hot reload 용도로 호출하지 않는다.
- TextData의 문자열 속성은 Text다(Kr 아님). ProductCategory는 enum 분류의 표시 이름 연결용이다. ResourceDataTable.TryGetResource(uint, out ResourceData) / GetResourcePath(uint)는 기존 주소를 제공한다.
- 현재 DataTableType은 Product=1, EconomyBalance=2, MaintenanceBalance=3, Resource=4, CustomerAppearance=5, CustomerDisposition=6, ProductCategory=7, Text=8, PriceEvent=9, PriceEventSchedule=10이다. 이는 현재 구현 확인이며 신규 배정 권한이 아니다. PlayerData는 제거됐다.
- idx / 1000 routing과 실제 loader 등록을 함께 검사한다. DataTableType_End는 숫자를 명시하지 않는 종료 표식으로 데이터·loader에 사용하지 않는다.
- 신규 CSV는 [CSV_RULES.md](CSV_RULES.md)·[DATA_RULES.md](DATA_RULES.md)의 ID 승인, header/PK/FK/문자열 경계, meta/Addressables 규칙을 적용한다. 사용 중인 대역을 예제 번호로 재사용하지 않는다.

## Resource·Pool API

[RESOURCE_POOL_CONTRACT.md](RESOURCE_POOL_CONTRACT.md)가 상세 권위다. ResourceManager.LoadAssetAsync<T>(key[, token]) / LoadAssetAsyncTask<T>(key)의 공유 자산은 manager 소유다. callback 실패와 Task 예외를 구분하고 개별 화면은 공유 자산을 직접 Destroy/Release하지 않는다.

SimplePoolManager.CreatePoolAsync<T>(key, capacity, prewarmCount, parent, onGet, onRelease), TryGetPool<T>, Get<T>, Release<T>, ClearPool/ClearAll을 사용한다. SimplePool.Clear는 대여 중 객체까지 정리하는 영구 종료다. 비소유 객체 반환은 파괴하지 않고 예외로 거부한다.

## Scene·UI 경계

- Scene 전환은 [SCENE_WORKFLOW.md](SCENE_WORKFLOW.md), MainScene 수동 검수는 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)를 따른다.
- LoadingScene은 DOTween DLL의 To/SetTarget/Kill로 CanvasGroup alpha를 0.3초 OutQuad 보간하고 OnDisable에서 target tween을 해제한다. runtime asmdef가 predefined assembly의 Modules 확장에 의존하지 않도록 한 동등 동작 변경이다.
- 현재 UI 조립은 GameUI.prefab의 GameUIController다. Dev3SandboxTester는 비활성화된 기존 파일이며 이 작업에서 이동/삭제하지 않는다. 가격표는 CreatePriceListText(day, session.EnsureDailyPrices())로 날짜 일치와 단가 존재를 검증한다.
- 자동 API 검사 실행·결과·미검증 영역과 병합 의존은 [TESTING.md](TESTING.md)를 따른다. UI 성공·Player build·저장 복구를 API 검사로 대신하지 않는다.
