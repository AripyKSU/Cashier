# 판매창 시간대 색감과 가게 단계별 아트 연결

2026-09-14. 담당: 강성규 요청에 따른 조정·기술 검토. Astra 변경 통합과 설계 이후, 사용자 승인으로 가게 단계 데이터·본편 외형 연결·기본 시간대 색감 구현을 진행했다. 아래의 과거 조사/통합 검증과 마지막 구현 기록을 구분한다.

## 기획 근거와 범위

[9/14 회의록](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.z16h61jq21i4)을 직접 조회했다. 다음 두 항목을 구분한다.

- 물품 판매창을 시간에 따라 색감 변경: 기능 구현 우선, 강성규 담당.
- 가게·계산대·설비 업그레이드에 따른 아트 교체: 기능 구현 우선, 설비 위치 시안 필요.

청소기 본편 연결, 정산 화면 교체, 설비 상점 페이지, 손님 앵커 등 다른 회의 항목은 이번 판매창 구현 범위로 확대하지 않는다. 회의록 본문은 유지하고, 후속 구현 때 CSV 종류 ID 탭에 StoreStage 예약만 추가했다.

## Git 통합 기준

- 입력: fetch 후 `astra-prototype` = `7300f99236c9e832e4e42f8b43e40aaf88faba39`.
- 대상: `total_merge` = `0e29b2c49bfcc115b5a806c295063af13baac8de`.
- 작업 시작 전 `codex/sales-window-lighting`을 위 대상에서 생성하고 원격 게시했다.
- `4642f3b`의 실제 커밋 본문과 자산을 확인했다. 이미 Astra `21d6982`까지의 Assets 98경로와 인계 문서가 선택 이관돼 있었다. 따라서 이번 실제 내용 통합은 **21d6982 → 7300f99의 Assets/doc 22경로**다.
- 이전 선택 이관에서 제외한 output 백업·캐시, Packages, 과거 공용 코드·설정 변경을 다시 가져오지 않는다. 이번 source 후속 변경도 Assets/doc 외에는 output 기록이다.
- 기존 대상 트리를 병합 기반으로 삼고 검토한 후속 변경을 3-way 적용하는 수동 내용 병합이다. 두 부모 병합 커밋으로 원본 이력도 연결한다. source 전체 트리를 덮어쓰거나 내용 없는 ours 병합으로 완료하지 않는다. 향후 Git에서 source가 조상으로 보이는 것은 이 문서의 채택·제외 결정을 포함한다.
- 기존 `ProjectSettings/EditorBuildSettings.asset` 사용자 변경은 원본 해시를 기록하고 별도로 보존한다. 본편 CSV·가격·시민권·엔딩·사운드·거래 UI 및 MainScene은 이 통합에서 수정하지 않는다.

### 이번에 반영한 변경과 충돌 해소

| 영역 | 내용 |
|---|---|
| Stage 2 | `Stage2Container.png`, `Stage2Shop.png` 재질 색상 변경, `Stage2Table.png` 추가, 기준 씬의 새 상판·광택·노멀 반응 |
| Stage 3 | `Stage3ContainerNormal.png` 추가, 기준 씬 및 적용 도구의 상자 노멀·명암 연결 |
| 프로토타입 씬/도구 | 최신 VerticalSlice 배치·16상품 직렬화·청소기/작업대 연결, 캐노피를 계산대 뒤로 정렬, 2단계 상판 적용 메뉴 |
| 탑뷰 | 청소기 다중 흡입·보관·동일 입구 배출, 두 재질 자산, 입력 버튼 피드백, 계산기 0 처리 |
| 문서 | source의 `doc/TOPDOWN_VACUUM.md` 추가. 프로토타입 전용 사양이며 본편 `VacuumController`와 별개 |

동일 경로의 기존 GUID 8개는 보존하고 새 씬 참조를 그 GUID로 변환했다. 충돌은 다음처럼 해결했다.

- `DystopiaVerticalSlice`: 기존 미리보기 09:00 보존, 나머지 신규 직렬화 연결 채택.
- `Vacuum.png.meta`: source의 Single Sprite/40 PPU 설정 채택. 신규 연결 메뉴가 Single Sprite를 요구한다.
- `DystopiaVacuumController`: 새 다중 보관 구조를 채택하되 기존 취소 시 선속도·각속도 복원을 각 `StoredItem`에 이관. 정상 배출은 새 배출 속도를 사용한다.
- `DystopiaScreen`: 다음 손님 표시 전 이전 반응 코루틴·색상 복원 수정 보존.

`doc/2026-09-14-prototype-handoff.md`는 21d6982 시점 설명이다. 그 문서의 “일반 씬 변경 없음”, “Stage 3 상자 노멀 제거”는 이번 최신 source에는 해당하지 않는다. 위 차이와 현재 코드/기준 씬을 우선 대조한다.

## 기존 시간·조명 연결

| 위치 | 실제 책임과 이번 설계에서의 사용 |
|---|---|
| `Assets/Scripts/Progress/DayProgress.cs` | 영업시간 권위. 남은 시간/총 영업시간 및 일시정지 |
| `Assets/Scripts/Scene/GameUIController.cs` / `refreshFrameViews` | 진행도를 09:00~21:00 표시 시각으로 변환, 시계에 전달 |
| `Assets/Scripts/UI/BusinessClockController.cs` / `DisplayTime` | 전달받은 시각 표시. 본편에서는 별도 타이머로 날짜·마감을 결정하지 않음 |
| `Assets/Scripts/Scene/WorldSceneView.cs` / `RefreshPresentation` | 월드 배경 색/시간대 알파, 계산대 Graphic의 CanvasRenderer tint와 CounterLight 알파 |
| `Assets/Scripts/UI/TimeOfDayUIController.cs` | `GetBlendWeights`, `GetEnvironmentTint` 정적 보간 함수 재사용. 컴포넌트 전체 자동 설치는 하지 않음 |
| `Assets/Scripts/UI/TimeOfDayPixelStage.cs` | 별도 uGUI 메시 캡처·렌더텍스처·노멀 조명 경로. 현재 본편 프리팹/씬에 직렬화 연결되지 않음 |
| `Assets/Scripts/UI/SaleSortingPanel.cs` | 정면↔탑뷰 슬라이드와 쏟기/분류/계산기 흐름. 현재 탑뷰 시간대 색감 연결 없음 |
| `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaWorkbenchLighting.cs` | SpriteRenderer MaterialPropertyBlock용 작업대 노멀/중앙광. DystopiaDayNight 시각을 읽는 프로토타입 전용 경로 |

현재 본편 연결 자산은 `Assets/Prefabs/World/CustomerWorld.prefab`, `Assets/Prefabs/GameUI/OperatingPanel.prefab`, `Assets/Prefabs/GameUI/GameUI.prefab`, `Assets/Scenes/MainScene.unity`다. MainScene이 WorldSceneView와 GameUI의 시계·정면 사각형을 연결한다. 정면은 월드 SpriteRenderer와 uGUI 혼합, 탑뷰 `SaleSortingUI`는 작업대·상품·상자·계산기가 uGUI인 구조다.

정면이 숨겨지면 WorldSceneView는 렌더를 정지한다. 따라서 그 컴포넌트의 마지막 적용 시각이나 Update에 탑뷰 색 갱신까지 의존하면 영업 중 색이 멈출 수 있다. **GameUI가 갱신한 실제 표시 시각을 매번 입력**해야 한다. 시간대 미리보기는 DayProgress를 변경하지 않는다.

## 구현안: 먼저 판매창 색감

1. `GameUI`의 항상 활성인 표현 영역에 판매창 외형 담당 컴포넌트 하나를 둔다. 단계·시간 모델, singleton, 새 패키지는 만들지 않는다.
2. `GameUIController.refreshFrameViews`에서 시계 갱신 직후 같은 hour를 전달한다. 정면/탑뷰가 동시에 보이는 슬라이드 첫 프레임에도 먼저 적용한다.
3. 색 계산은 기존 `TimeOfDayUIController.GetBlendWeights/GetEnvironmentTint`를 재사용한다. WorldSceneView에 저장된 실제 경계·tint를 조회하는 작은 무상태 계산 함수를 노출해 같은 설정을 읽는다. 두 컴포넌트에 색상 설정을 복제하지 않는다. 정면이 비활성이어도 입력 hour만으로 계산할 수 있어야 한다.
4. 첫 구현은 명시적으로 연결한 작업대·상자 등 **장식용 Graphic**의 RGB에만 시간대 tint를 곱한다. Image 고유 색과 알파, CanvasGroup 슬라이드/페이드 알파는 보존한다. 매 프레임 직전 결과에 다시 곱하지 않고 원래 색을 기준으로 계산한다.
5. 가격 숫자, 버튼 상태, 구역 판정 색, 상품 식별색, 손 커서는 첫 tint 대상에서 제외해 읽기·조작 대비를 보존한다. 전체 화면 검정 오버레이로 UI를 한꺼번에 어둡게 하지 않는다.
6. 야간 중앙광·노멀맵은 색감 기본 연결 다음 단계로 분리한다. 기존 `Cashier/WorkbenchLighting` 셰이더는 SpriteRenderer용이며 uGUI Mask/RectMask2D·Stencil 규약이 없다. 필요 시 uGUI 호환을 검증한 별도 재질을 해당 장식에만 적용하고 공유 material asset을 런타임에 수정하지 않는다.
7. 일시정지는 모델 시각이 정지하므로 색도 고정한다. 다음날·새 게임은 새 시각을 즉시 다시 적용한다. 비활성화/파괴 시 원래 tint를 복원하고 생성한 material만 해제한다.

영업 기본 180초와 09~21시를 기준으로 현재 보간 경계는 아래와 같다. 이는 새 밸런스가 아니라 현 구현 설정의 설명이다. Inspector의 실제 설정을 권위로 사용한다.

| 게임 시각 | 기본 영업 경과 | 표현 |
|---|---:|---|
| 09:00 | 0초 | 아침 tint |
| 12:00 | 45초 | 주간, 아침 tint 해제 |
| 15:00 | 90초 | 석양으로 전환 시작 |
| 18:00 | 135초 | 석양 tint, 이후 야간으로 전환 |
| 21:00 | 180초 | 야간 tint/조명 |

## 구현안: 단계별 리소스 연결 준비

가게는 **1단계 시작, 최대 3단계**다. 실제 권위는 `Assets/Scripts/Facility/FacilityService.cs`의 `CurrentStoreStage`. 2→3단계 확장은 구매 즉시 단계가 오르며, 일반 설비 효과는 다음 영업일부터 활성화된다. `target_store_stage=0`은 대상 없음 값이며 초기 외형 0단계를 뜻하지 않는다. 비용·선행 설비·시민권 조건을 외형 코드에서 다시 구현하지 않는다.

| 단계/대상 | 확인한 원본 | 적용 방향 |
|---|---|---|
| Stage 1 정면 | `Editor/References/Stage1Reference.unity`, `Art/Stage1WoodCounter.png`, Stage1 시계 이미지, `TopDownTest/Art/Stage1WoodContainerCompact.png` | 낡은 목재 가판·천막, 작은 상자/시계. 기준 씬의 실제 Sprite/RectTransform/노멀 슬롯을 묶어 추출 |
| Stage 2 정면 | `Editor/References/Stage2Reference.unity`, `Art/Stage2Shop.png`, `Stage2Table.png`, `Stage2Container.png`, `Stage2Clock.png`, `Prefabs/Stage2ShopParts.prefab` | 철제 프레임과 새 상판, 상자/시계 노멀. 배경 92% 배치 포함 |
| Stage 3 정면 | `Editor/References/Stage3Reference.unity`, `Art/Stage3Shop.png`, `Stage3Container.png`와 Normal, 천장 전등 관련 레이어 | 보강 프레임/전등/하부. 최신 상자 노멀 사용, Stage 2 배경 배치 연동 확인 |
| 탑뷰 공통 | `TopDownTest/Art/TopDownWorkbench.png`, `TopDownWorkbenchNormal.png`, `WorkbenchLighting.mat` | 원본 렌더러와 본편 uGUI의 크기·UV·재질 경로 차이를 확인하고 작업대 Image에 맞춰 연결 |
| 탑뷰 단계 차이 | Stage1 목재/상자 계열 등은 존재. 독립된 1·2·3단계 탑뷰 작업대 3세트 완비는 확인되지 않음 | 정면 Stage2Table을 탑뷰 전용 원화로 간주하지 않는다. 공통 작업대 유지 + 준비된 소품만 단계별 교체하는 것을 첫안으로 둠 |
| 상품 | `Art/Products/` 16종 | 본편은 이전 작업의 사용용 사본과 Resource ID를 이미 연결함. source 파일명으로 ID·가격을 재등록하지 않음 |
| 설비 소품 | 회의록에서 위치 시안 필요로 명시 | 구매 보유와 익일 효과 활성 표시를 구분. 확정 시안 없는 소품 위치를 이번 설계에서 고정하지 않음 |

위 경로의 `Editor/References` 등은 모두 `Assets/DystopiaPrototype/` 기준이다. 해당 기준 씬은 **편집 도구가 읽는 참고 씬**이며 Build Settings에 추가하거나 통째로 MainScene에 로드하지 않는다.

연결 순서는 다음과 같다.

1. Editor에서 3개 기준 씬의 표현 자산·배치만 확인하고 기존 사용용 자산 경로/매핑에 맞춰 이관한다. GUID·sprite fileID·슬라이싱을 보존한다. Prototype Session, Clock, 입력 컨트롤러는 본편에 중복 생성하지 않는다.
2. 아래 단계별 외형 데이터 제안에 따라 CSV의 Resource FK로 표시 묶음을 선택한다. `OperatingPanel`의 정면 프레임·상판·상자·시계, `CustomerWorld`의 배경, `GameUI`의 탑뷰 장식을 구분한다. 프리팹은 묶음 내부 배치·참조를 소유하며 상품/계산기 입력 영역과 기존 버튼 이벤트는 유지한다. 시계는 예외로 CSV가 Sprite를 선택하고 프리팹은 배치·숫자 앵커만 소유한다.
3. 초기 준비 완료, 설비 구매 성공 후 UI 갱신, 다음날 갱신 때 `GameProgress.CurrentStoreStage`를 읽어 적용한다. 이전 적용 단계와 같으면 재생성하지 않는다. 새 전역 이벤트가 필요하지 않으며 기존 구매 이벤트를 사용한다면 구독 해제를 함께 구현한다.
4. 단계 적용 후 활성 장식 목록에 같은 시각의 tint를 다시 적용한다. 시계 Sprite가 바뀌어도 숫자 위치·영업시각/마감은 독립적으로 보존한다.
5. 일반 설비의 효과 표시가 필요하면 `IsFacilityEffectActive` 등 실제 활성 조회를 사용한다. 가게 단계만으로 청소기·자동분류를 활성화하지 않는다.

### 2026-09-14 보완: 단계별 외형 데이터와 리소스 FK

사용자 요청에 따라 CSV·DTO·로더·리소스 소비 경계를 재확인했다. **이 절은 구현 전 스키마 제안**이다. 실제 CSV, 데이터 종류 ID, Resource ID, Addressables 등록이나 코드는 이번 조사에서 변경하지 않았다. 위 병합 검증 결과는 이 제안의 구현 검증이 아니다.

#### 현재 존재하는 것과 누락

- `Assets/Datas/FacilityData.csv`의 7열은 구매 가격·종류·요구 단계·효과·목표 단계다. 리소스 FK가 없으며 DTO `Assets/Scripts/Commons/Data/FacilityData.cs`에도 없다.
- 가게 단계별 독립 테이블과 `DataTableType` 등록은 없다. 현재 단계는 `FacilityService`의 상태다.
- 단계 확장 구매 행은 12008(2단계), 12010(3단계)뿐이다. 초기 1단계는 구매 행이 없고, FacilityData 검증은 가격 양수·목표 단계 2/3을 요구한다. 외형 연결을 위해 무료 1단계 구매 행을 억지로 추가하면 구매 계약이 달라진다.
- `ResourceData`는 `idx,path`로 Addressables 키를 해석한다. `ResourceManager.LoadAssetAsync<T>`와 Instantiate/ReleaseInstance 경로를 재사용할 수 있다. ResourceData 행 자체에 에셋 타입 정보는 없으므로 소비자가 실제 타입·필수 구성도 검사해야 한다.
- 상품·손님·딸·엔딩에는 이미 `*_resource_idx` FK 패턴이 있다. 가게 외형도 이 패턴을 따른다.
- 이전 설계의 Inspector 단계별 묶음만으로는 기획자가 CSV에서 단계→리소스 관계를 관리할 수 없다. **단계별 선택은 데이터, 선택한 리소스 내부 배치는 프리팹**으로 책임을 보완한다.

#### 권장 초안: StoreStageData.csv, 3개 행

파일명과 컬럼은 제안이며 신규 ID는 배정하지 않았다. 모든 컬럼은 필수 `uint`로 제안한다.

| 컬럼 | 의미·검증 |
|---|---|
| `idx` | 신규 테이블 종류의 PK. 권위 목록 확인·예약 후 배정하며 단계 숫자 1/2/3을 PK로 사용하지 않음 |
| `store_stage` | 실제 가게 단계 1/2/3. 각 단계 정확히 1행, 초기 1단계도 필수 |
| `world_prefab_resource_idx` | ResourceData FK. 월드 배경의 단계별 SpriteRenderer·배치 묶음 프리팹 |
| `front_prefab_resource_idx` | ResourceData FK. 정면 가판 프레임·계산대·상자와 시계 배치의 uGUI 프리팹 |
| `top_view_prefab_resource_idx` | ResourceData FK. 탑뷰 작업대·상자 장식의 uGUI 프리팹 |
| `clock_resource_idx` | ResourceData FK. 단계별 정면 시계 Sprite. 프리팹은 위치·숫자 앵커만 소유 |

네 FK는 준비된 유효 자산을 가리키며 0/빈값/누락을 임의 기본값으로 처리하지 않는다. 동일 리소스를 여러 단계가 참조하는 것은 허용한다. 예를 들어 탑뷰 2·3단계 전용 아트가 없으면 공통 탑뷰 프리팹의 **실제 Resource ID를 명시적으로 재사용**한다. 이는 단계별 전용 원화가 완성됐다는 뜻이 아니다.

세 갈래를 구분하는 이유는 본편 월드 배경과 uGUI의 좌표계·부모·정렬 책임이 다르기 때문이다. 단일 PNG FK는 시계 숫자 앵커, 단계 2/3의 배경 92% 배치, 노멀맵·재질·전등 위치까지 표현할 수 없다. 시계 Sprite는 CSV의 `clock_resource_idx`로 선택한다. 그 외 묶음 내부 Sprite·Material·NormalMap·Transform은 프리팹에서 직접 연결하고, 모든 세부 이미지를 CSV 컬럼으로 펼치지 않는다. 낮/석양/밤별 CSV 행이나 별도 게임 시계를 추가하지 않으며 시간에 따른 보간은 기존 코드 책임을 유지한다.

구매용 썸네일이 나중에 필요하면 그것은 FacilityData의 별도 UI 요구사항이다. 단계 외형 FK를 구매 아이콘과 겸용하지 않는다. 개별 설비 소품의 구매·활성 상태도 단계 외형 데이터에 섞지 않으며, 이번 스키마에 미확정 소품 컬럼을 추가하지 않는다.

#### 조회·적용 및 필요한 보완

1. `FacilityService.CurrentStoreStage` → `store_stage`로 1행 조회 → 세 프리팹과 시계 Resource FK → `ResourceData.path` → 기존 ResourceManager → 각 표현 부모에 적용한다. 가격·선행 구매·단계 상승은 FacilityService가 계속 소유한다. 새 테이블의 `store_stage`는 표현 조회 키이며 추가 진행 상태가 아니다.
2. 신규 게임의 초기 1단계, 모든 `required_store_stage`, 양수인 `target_store_stage`에 대응 행이 있는지 로딩 완료 경계에서 교차 검증한다. FacilityData의 기존 단계 숫자를 새 테이블 PK로 재해석하지 않는다. 현재 FacilityData 스키마와 가격은 그대로 유지할 수 있다.
3. Prefab 교체 시 `WorldSceneView.layers/counterGraphics/counterLight` 등 기존 참조가 파괴된 이전 객체를 계속 가리키지 않게 해야 한다. 새 묶음의 필수 Sprite/Graphic·시계 표시 앵커·광원 참조를 검증하고 소유 표현 컨트롤러에 함께 다시 연결한다. 런타임 이름 검색 대신 명시적 직렬화 연결을 사용한다.
4. 새 아트가 현재 `SaleSortingPanel`의 입력 RectTransform·슬라이드·물리 구역을 교체하지 않도록 장식 부모 아래에 연결한다. 프리팹에는 별도 GameSessionManager/Clock/EventSystem/구매 로직을 넣지 않는다. 기존 WorldSceneView를 중복 생성하지 않는다.
5. 새 단계의 세 묶음과 시계를 모두 준비·검증한 뒤 함께 교체하고 현재 시간 tint를 다시 적용한다. 로드 실패 시 이전 외형을 보존하고 오류·재시도 경로를 제공한다. 이미 완료한 구매를 표현 코드가 환불/롤백하지 않는다. 진행 중 더 높은 단계 요청이나 새 게임/씬 종료가 발생하면 늦게 도착한 로드 결과가 현재 외형을 덮지 않도록 취소·현재 요청 검사를 적용한다.
6. 같은 단계/동일 프리팹의 중복 갱신은 재생성하지 않는다. 바뀐 장식 인스턴스만 정리하며 공유 ResourceManager의 `ReleaseAll`을 호출하지 않는다. 기존 캐시·인스턴스 해제 계약을 따른다.
7. 구현 시 신규 CSV 종류 ID를 Google Docs 권위 목록에서 확인·예약한 다음 CSV/meta·DTO/DataTable·DataTableType/로더·ResourceData FK·Addressables·표현 소비자·문서를 한 묶음으로 반영한다. 숫자상 다음 enum 값이 비어 있다는 이유만으로 새 ID를 확정하지 않는다. 원격 ID 예약과 자산 등록은 실제 구현 단계의 작업이다.

추가 검증: 1단계 누락/단계 중복·범위 밖/없는 FK/잘못된 에셋 타입/필수 구성 누락 거부, 공유 탑뷰 리소스 재사용, 구매 직후 3영역 동시 갱신, 로드 실패·늦은 완료·새 게임 취소, 이전 조명 참조 제거, 동일 단계 반복 호출, 시계·상품 판정·레이캐스트 보존을 확인한다.

## 완료 조건과 확인할 화면

- 시간: 09/12/15/18/21시, 정면→탑뷰→정면 왕복, 정지/재개, 새 영업일 09시 복귀.
- 색: 초기 색/알파 보존, 전환 중 이중 tint 없음, 상품·숫자·구역 구분 가독성 유지.
- 단계: 신규 게임 1단계, 단계 구매 직후 정면/탑뷰 함께 교체, 1→2→3→1 편집 미리보기에서 잔존 프레임 없음. 재실행·중복 갱신 시 객체/재질 중복 없음.
- 자산: missing script/reference 0, GUID 중복 0, 실제 사용 Sprite/노멀 쌍과 PPU·pivot 검증, MainScene override와 레이캐스트 확인.
- 회귀: 거래·가격·쏟기·분류·계산기 입장·시설 효과/구매·날짜/정산은 기존 결과 유지.
- 구현 후 화면/UX는 사용자 플레이 테스트로 확인한다. API 테스트나 원본 씬 임포트 성공만으로 본편 조명 완성을 주장하지 않는다.

## Astra 병합 시 검증 (단계 데이터 구현 전)

- Unity 6000.3.18f1 실제 임포트/컴파일 완료, compile error 0.
- 기존 회귀 테스트: **EditMode 262/262, PlayMode 60/60**, 실패·skip·미완료 0. `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 300` 실행. 결과는 `Temp/TestResults/20260914-201953-6270e260323e4373b1809d4215a51a35/{EditMode,PlayMode}.xml` 및 `.log`다.
- 전체 Assets GUID 중복 0, 이관 자산/meta 짝 누락 0, 이번 변경으로 새로 생긴 GUID 참조 누락 0. 사용자가 변경한 Build Settings 해시 일치. `Temp/astra-lighting-static.json`에 기록했다.
- Unity에서 3개 단계 기준 씬과 VerticalSlice를 preview scene으로 열어 검사했다. 각각 missing script 0, 단계 적용 도구가 소비하는 15개 레이어의 Sprite 누락 0. 신규 WorkbenchLighting/VacuumWind 재질의 shader 연결과 컴파일 오류 없음. `Temp/astra-lighting-reference-check.txt`.
- 첫 참조 검사는 비소비 슬롯까지 필수 Sprite로 판단해 Stage1 16건을 보고했다. 실제 단계 적용 도구의 명시적 이름 목록으로 검사 범위를 바로잡았다. 참고 씬의 기존 미사용 portrait GUID 6개까지 모두 복구했다는 뜻은 아니다. 원본 미해결 참조 목록은 [DYSTOPIA_RESOURCE_INTEGRATION](../DYSTOPIA_RESOURCE_INTEGRATION.md#원격-원본의-미해결-참조)을 유지한다.
- 병합 취소 계약 검사: 실제 Unity 객체 3개를 보관, 1개 순차 배출 후 나머지 2개 취소 복원, 이미 배출된 물품 속도 유지, 부분 흡입 취소 복원 확인. 위치·활성·물리·선속도·각속도·보관 목록을 확인했다. `Temp/astra-vacuum-check.txt`. Reflection API 검사이며 실제 마우스 흡입/충돌 체감 검증과 구분한다.
- 별도 Sol/low 읽기 전용 기술 검토에서 지정 코드의 필수 회귀 수정은 추가로 발견되지 않았다. 이 검토는 사용자 화면 승인을 대신하지 않는다.
- 실제 Init→Hub→Main 경로 진입, GameUI 1개·GameSessionManager 1개·DayProgress 준비 완료, 이 실행의 Console error 0 확인. `Temp/astra-lighting-main-check.txt`. 마우스로 모든 거래를 수행한 검사는 아니다.
- 일반 에디터 첫 시작에서 전체 임포트에 약 5분이 걸렸다. 시작 중 별도 배치 테스트 시도는 같은 프로젝트를 열 수 없어 종료 코드 1/실행 0건이었다. 그 시도를 성공으로 세지 않았으며, 준비 완료한 원래 에디터에서 위 두 모드를 정상 완료했다.
- 위 병합 시점에는 본편 판매창 색감/단계 자동 교체가 **설계 완료·구현 전**이었다. 후속 구현 상태는 아래를 따른다. Temp 증거는 로컬 파일이며 다른 컴퓨터에서는 위 경로/절차로 재검증한다.

## 승인 후 구현: 가게 단계 데이터와 기본 색감

- CSV 종류 ID 권위 탭 `t.ccpln6m1g4kv`에 `StoreStage=19`, `19001~19003` 예약을 추가하고 다시 조회해 확인했다. 회의록·경제 기획 본문은 변경하지 않았다.
- `Assets/Datas/StoreStageData.csv`는 6개 필수 uint 컬럼을 사용한다. 1/2/3단계 각각 한 행, 외형 프리팹·시계 Resource FK, 설비의 요구/목표 단계 포함을 검증한 뒤 DataTableManager가 공개한다.
- 리소스 매핑: 1단계 World/Front/TopView=`4292/4293/4294`, 2단계=`4295/4296/4294`, 3단계=`4298/4299/4294`. 4297은 사용하지 않는다. ResourceData→Addressables→`Assets/Prefabs/StoreStage/`의 실제 프리팹 7개를 참조한다.
- 단계별 시계 Sprite는 `clock_resource_idx`가 선택한다. 1/2/3단계는 각각 `4300/4301/4302`, 본편 주소는 `Stage1Clock`/`Stage2Clock`/`Stage3Clock`이며 프리팹의 시계 슬롯은 위치·크기·숫자 앵커만 제공한다.
- 현재 세 단계 모두 공통 탑뷰 작업대 이미지를 사용한다. 정면 상자와 시계·가판은 단계별 기준 씬을 따른다. 손님별 동적 상자/쏟기 이미지나 물리 판정은 이 표의 작업대 장식과 별개다.
- 구현을 줄이고 입력 참조를 보존하기 위해 **프리팹을 인스턴스로 교체하는 제안 대신, 준비한 프리팹을 외형 원본으로 읽어 기존 슬롯에 Sprite와 배치를 적용**한다. 세계 배경의 기존 Renderer, 정면 시계·상자 버튼, 작업대 자식의 판정/상품/계산기 오브젝트를 파괴하지 않는다.
- `StoreStagePresentation`은 MainScene의 GameUI와 WorldSceneView에 연결한다. 초기화 덮개가 열린 상태에서 세 단계 모두 타입·슬롯을 준비하고, 완료 후 `GameProgress.CurrentStoreStage`를 적용한다. 설비 구매 뒤 상점 갱신에도 적용한다. 같은 단계의 반복 요청은 작업을 생략한다.
- 로딩 실패는 기존 GameUI 초기화 오류 화면으로 전달한다. 모든 준비가 완료되기 전에는 기존 표시를 바꾸지 않으며 씬 파괴 토큰으로 대기를 취소한다. 단계 구매 후 별도 비동기 로드가 없으므로 늦은 단계 완료가 이전 상태를 덮는 경쟁을 만들지 않는다. 공유 ResourceManager 캐시만 사용하고 추가 인스턴스나 전역 ReleaseAll 호출은 없다.
- `WorldSceneView`의 영업 시계와 보간을 그대로 사용하며, 정면 장식과 탑뷰 작업대에 같은 RGB tint를 적용한다. 탑뷰에서 전면 월드가 비활성화돼도 색은 갱신한다. 상품·가격 숫자·구역 색과 기존 입력 영역은 tint 대상에서 제외한다.
- 이번 범위는 Sprite/배치와 기본 시간대 tint다. 프로토타입의 픽셀 단위 노멀 조명, 상판 반사, 중앙광·접촉 그림자를 uGUI에 재현한 것은 아니다. 해당 셰이더는 기존 계획대로 별도 호환 검증 단계다.
- 새 데이터는 기존 저장 형식을 변경하지 않는다. 현재 진행/구매/가격의 권위와 FacilityData는 유지한다. 신규 파일 생성 도구는 `Cashier/Store Stage/Create Assets`; 생성 이후 재실행으로 기존 아트를 덮지 않으며 변경은 생성 프리팹을 직접 편집한다.

### 외형 연결 중 보완한 사항

- 2단계 참고 씬의 상판은 분리된 신규 이미지이며 하부장 슬롯은 저장돼 있지 않다. `Stage3Reference`에 보존된 `Stage2Cabinet`의 실제 Sprite를 가져와 2단계에서만 표시한다. 1/3단계에서는 꺼진다.
- 새 `Stage2Table.png`는 289×217 중 위 122px이 투명하다. 본편 프리팹에서 이 여백을 보정해 보이는 상판 윗면을 기준 씬의 y에 맞추고, 하부장을 바로 아래에 연결했다. 원본 이미지·참고 씬·import 설정을 수정하지 않았다. 최종 상판 y=-373.7637, 하부장 y=-623.2004다.
- 천장·기둥·하부장은 상판 뒤에 놓았다. 기존 손님/시계/상자 입력 계층은 유지한다.
- 배경 92% 배치에 맞춰 좌우 굴뚝 기준 Transform도 월드 프리팹에서 읽는다. 기존 연기 프레임·누적 시간·Renderer는 보존한다.

### 실제 Unity 확인과 검증 기록

- 정식 Init 부트 완료(`HubScene`, session initialized=true) 후 Main 진입: 가게 외형 `AppliedStage=1`, DayProgress 준비 완료.
- 1→2→3→1→3→3 적용에서 GameUI 전체 Transform 인스턴스 목록, BusinessClock 인스턴스, 상자 버튼 raycast 유지. 없는 4단계 적용 실패 후 기존 3단계 Sprite 유지. `Temp/StoreStageReview/runtime-contract.txt`.
- 정면 비활성 상태의 작업대 tint: 09시 `(1,.9,.8)`, 12/15시 `(1,1,1)`, 18시 `(1,.72,.49)`, 21시 `(.38,.43,.56)`, alpha=1. 디버그 표현 시각만 변경했으며 모델 영업 시간은 변경하지 않았다.
- 감독관 대사/퇴장을 모델 API로 완료하고, 테스트 재정으로 `12001→12002→12007→12008→12003→12004→12009→12010`을 실제 구매 API에 전달했다. UI 갱신 후 모델/표현 단계가 모두 1/2/3으로 일치했다. `Temp/StoreStageReview/purchase-contract.txt`. 상점 버튼을 마우스로 클릭한 수동 검사는 아니다.
- 실제 GameView 캡처: `Temp/StoreStageReview/stage1-noon.png`, `stage2-adjusted.png`, `stage3-noon.png`, `stage3-night.png`, `top-night.png`. 2단계 캡처의 투명 여백 보정은 121px 시험값이며 최종 프리팹은 실제 알파 경계인 122px로 확정했다. 캡처는 표현 미리보기이며 손님 거래/쏟기 전체를 조작한 검사는 아니다.
- 외형 프리팹 7개 타입·슬롯 검증 및 missing script 0. Assets meta GUID 중복 0. 정적 스캔에서 Assets 밖으로 보인 Image.cs/기본 Sprite 재질 GUID 두 종류는 AssetDatabase로 정상 Packages 경로를 확인했다. `Temp/StoreStageReview/asset-contract.txt`.
- 초기 전체 검사 실패는 기존 테스트의 ResourceData 로그 91행 및 enum 끝 번호 19 기대값 때문이었다. 관련 기능 검증은 유지하면서 증가한 데이터에 맞게 로그 기대값/마지막 enum 검사를 갱신했다. 새 CSV 양성·음성 검사는 처음부터 통과했다.
- 중간 회귀 결과: EditMode 268/268, PlayMode 60/60. `Temp/TestResults/20260914-212039-29501f2f09f2458c905ccd1217df1dd0/`. 하부장·굴뚝 및 2단계 배치 보완 후 최종 검사는 별도로 실행했다.
- 첫 수동 진입 시 재컴파일/초기화 중 전환으로 생긴 미준비 세션은 성공으로 세지 않았다. 정식 부트부터 다시 확인했으며 플레이 종료 후 테스트 재정과 임시 `runInBackground`를 정리했다.
- 해상도별 배치·야간 가독성의 사용자 플레이 테스트, 전체 마우스 거래 흐름, Player 빌드와 픽셀 단위 노멀 조명은 미검증/후속 범위다.
- **최종 회귀: EditMode 268/268, PlayMode 60/60, 실패·skip·미완료 0.** 보완 후 `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 300`으로 실행했다. 결과: `Temp/TestResults/20260914-213906-030b11771fb94526acd64ff36d24d808/{EditMode,PlayMode}.xml` 및 `.log`.

### 2026-09-15 후속: 시계 FK와 Textures 이관 검증

- 사용자 요청과 Addressables 이미지 3개 등록 승인을 반영했다. StoreStageData 3행·6컬럼, 시계 FK 4300/4301/4302, ResourceData 총 101행이다. 프리팹 3종 FK와 저장된 가게 단계는 유지한다. 상세 복사 범위·복구 기준은 `doc/DYSTOPIA_RESOURCE_INTEGRATION.md`의 같은 날짜 절을 따른다.
- `PrepareAsync`는 시계 Sprite와 기존 외형 프리팹 전부를 검증한 뒤 새 캐시를 한 번에 공개한다. 준비 실패는 기존 캐시를 바꾸지 않는다. Front의 시계 슬롯은 위치·숫자 앵커만 소유하고 Sprite는 CSV에서 적용한다. 코드·데이터·주소·프리팹 읽기 전용 교차 리뷰에서 차단 문제 없음, 문서의 시계 권위 예외를 보완하는 데 동의했다.
- EditMode **270/270**, PlayMode **60/60**, 실패·skip·미완료 0. `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 420`. 증거: `Temp/TestResults/20260915-100032-96906c4a4f374089bf0bd0d7df52ceb5/{EditMode,PlayMode}.xml` 및 `.log`. 기존 61 Sprite 테스트는 상품·손님 전용 집합이며 추가 시계 3종 로드는 아래 정식 부트에서 별도로 확인했다.
- Init→Hub 새 게임→Main 초기 단계1에서 `Assets/Textures/UI/Dystopia/Stage1BasicClock.png` 로드. 1→2→3→1→3→3 적용 시 CSV FK에 따라 `Stage1BasicClock`/`Stage2Clock`/`시계`로 교체되고, 시각 `10:31`과 시계/GameUI Transform 인스턴스를 유지했다. 없는 단계4 거부 후 3단계 유지. 초기 준비 덮개 아래에서 모든 시계가 정상 로드됐다.
- 런타임 UI Image/월드 SpriteRenderer의 프로토타입 Sprite 0, MainScene과 단계 프리팹 7개의 prototype 의존성 0, 단계 프리팹 Validate 7/7, Main missing script 0, ResourceManager 1개. 새로 복사한 10개와 최신화한 9개를 포함한 총 37개 원본/사본 PNG 해시 및 Sprite fileID 일치, 기존 GUID 보존·전체 GUID 중복 0. 기존 Addressables 매핑 보존 및 승인한 3개 추가 확인.
- 증거: `Temp/StageTextureStatic.txt`, `Temp/StageTexturesRuntime.txt`, `Temp/StageTextures-Stage2.png`, `Temp/StageTextures-Stage3.png`, `Temp/StageTexturesConsole.json`. 최종 제품 Console 오류 0, compile idle, Play 종료·InitScene dirty=False·runInBackground=False. 테스트 중 자동 생성된 TMP fallback 글리프만 작업 전 dirty 사본으로 복원하여 기존 사용자 변경을 보존했다.
- 상태 PASS는 데이터·리소스 로딩 및 단계 전환 API의 최소 실행 기준이다. 단계2 낮/단계3 밤 캡처는 표시 미리보기이며, 구매부터 진행하는 전체 수동 UX·해상도별 가독성·Player build는 미검증이다. 이번 후속은 commit/push하지 않았다.
- 후속 이름 통일에서 원본 `Stage1BasicClock.png`/`Stage2Clock.png`/`시계.png`는 유지하고 본편 사본·주소만 `Stage1Clock.png`/`Stage2Clock.png`/`Stage3Clock.png`으로 변경했다. Resource ID 4300~4302, GUID·Sprite fileID와 StoreStage FK는 유지한다. 위 검증 기록의 당시 실제 경로·주소 표기는 과거 증거로 보존한다.
- 이름 통일 후 재검증(2026-09-15): EditMode **270/270**, 실패·skip·미완료 0 (`Temp/TestResults/20260915-102335-a6c9d9f7032e45eeb41f4db5c068e0de/EditMode.xml` 및 `.log`). 정식 Init→Hub→Main에서 1→2→3→1 적용 시 `Assets/Textures/UI/Dystopia/Stage1Clock.png`/`Stage2Clock.png`/`Stage3Clock.png` 실제 로드·표시와 동일 시계 객체·09:00 유지 확인 (`Temp/StageClockRenameRuntime.txt`). PNG3개는 현재 원본과 바이트 일치, 기존 GUID·Sprite fileID 보존. 최종 Console 오류0, Play 종료·dirty=False·runInBackground=False. 이름·주소 변경만 검증한 이번 후속에서는 전체 PlayMode suite를 반복 실행하지 않았고, commit/push하지 않았다.
