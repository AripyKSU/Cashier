# 판매창 시간대 색감과 가게 단계별 아트 연결 설계

2026-09-14. 담당: 강성규 요청에 따른 조정·기술 검토. 이번 범위는 Astra 변경 통합, 기존 구현 조사, 다음 구현 설계다. **본편 판매창 색감과 단계별 아트 자동 교체는 아직 구현하지 않았다.**

## 기획 근거와 범위

[9/14 회의록](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.z16h61jq21i4)을 직접 조회했다. 다음 두 항목을 구분한다.

- 물품 판매창을 시간에 따라 색감 변경: 기능 구현 우선, 강성규 담당.
- 가게·계산대·설비 업그레이드에 따른 아트 교체: 기능 구현 우선, 설비 위치 시안 필요.

청소기 본편 연결, 정산 화면 교체, 설비 상점 페이지, 손님 앵커 등 다른 회의 항목은 이번 판매창 설계의 구현 범위로 확대하지 않는다. 원격 기획 문서는 수정하지 않았다.

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
2. `OperatingPanel`의 정면 프레임·상판·상자·시계, `CustomerWorld`의 배경, `GameUI`의 탑뷰 장식을 구분해 단계별 표시 묶음을 지정한다. 상품/계산기 입력 영역과 기존 버튼 이벤트는 유지한다.
3. 초기 준비 완료, 설비 구매 성공 후 UI 갱신, 다음날 갱신 때 `GameProgress.CurrentStoreStage`를 읽어 적용한다. 이전 적용 단계와 같으면 재생성하지 않는다. 새 전역 이벤트가 필요하지 않으며 기존 구매 이벤트를 사용한다면 구독 해제를 함께 구현한다.
4. 단계 적용 후 활성 장식 목록에 같은 시각의 tint를 다시 적용한다. 시계 Sprite가 바뀌어도 숫자 위치·영업시각/마감은 독립적으로 보존한다.
5. 일반 설비의 효과 표시가 필요하면 `IsFacilityEffectActive` 등 실제 활성 조회를 사용한다. 가게 단계만으로 청소기·자동분류를 활성화하지 않는다.

## 완료 조건과 확인할 화면

- 시간: 09/12/15/18/21시, 정면→탑뷰→정면 왕복, 정지/재개, 새 영업일 09시 복귀.
- 색: 초기 색/알파 보존, 전환 중 이중 tint 없음, 상품·숫자·구역 구분 가독성 유지.
- 단계: 신규 게임 1단계, 단계 구매 직후 정면/탑뷰 함께 교체, 1→2→3→1 편집 미리보기에서 잔존 프레임 없음. 재실행·중복 갱신 시 객체/재질 중복 없음.
- 자산: missing script/reference 0, GUID 중복 0, 실제 사용 Sprite/노멀 쌍과 PPU·pivot 검증, MainScene override와 레이캐스트 확인.
- 회귀: 거래·가격·쏟기·분류·계산기 입장·시설 효과/구매·날짜/정산은 기존 결과 유지.
- 구현 후 화면/UX는 사용자 플레이 테스트로 확인한다. API 테스트나 원본 씬 임포트 성공만으로 본편 조명 완성을 주장하지 않는다.

## 이번 통합 검증

- Unity 6000.3.18f1 실제 임포트/컴파일 완료, compile error 0.
- 기존 회귀 테스트: **EditMode 262/262, PlayMode 60/60**, 실패·skip·미완료 0. `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 300` 실행. 결과는 `Temp/TestResults/20260914-201953-6270e260323e4373b1809d4215a51a35/{EditMode,PlayMode}.xml` 및 `.log`다.
- 전체 Assets GUID 중복 0, 이관 자산/meta 짝 누락 0, 이번 변경으로 새로 생긴 GUID 참조 누락 0. 사용자가 변경한 Build Settings 해시 일치. `Temp/astra-lighting-static.json`에 기록했다.
- Unity에서 3개 단계 기준 씬과 VerticalSlice를 preview scene으로 열어 검사했다. 각각 missing script 0, 단계 적용 도구가 소비하는 15개 레이어의 Sprite 누락 0. 신규 WorkbenchLighting/VacuumWind 재질의 shader 연결과 컴파일 오류 없음. `Temp/astra-lighting-reference-check.txt`.
- 첫 참조 검사는 비소비 슬롯까지 필수 Sprite로 판단해 Stage1 16건을 보고했다. 실제 단계 적용 도구의 명시적 이름 목록으로 검사 범위를 바로잡았다. 참고 씬의 기존 미사용 portrait GUID 6개까지 모두 복구했다는 뜻은 아니다. 원본 미해결 참조 목록은 [DYSTOPIA_RESOURCE_INTEGRATION](../DYSTOPIA_RESOURCE_INTEGRATION.md#원격-원본의-미해결-참조)을 유지한다.
- 병합 취소 계약 검사: 실제 Unity 객체 3개를 보관, 1개 순차 배출 후 나머지 2개 취소 복원, 이미 배출된 물품 속도 유지, 부분 흡입 취소 복원 확인. 위치·활성·물리·선속도·각속도·보관 목록을 확인했다. `Temp/astra-vacuum-check.txt`. Reflection API 검사이며 실제 마우스 흡입/충돌 체감 검증과 구분한다.
- 별도 Sol/low 읽기 전용 기술 검토에서 지정 코드의 필수 회귀 수정은 추가로 발견되지 않았다. 이 검토는 사용자 화면 승인을 대신하지 않는다.
- 실제 Init→Hub→Main 경로 진입, GameUI 1개·GameSessionManager 1개·DayProgress 준비 완료, 이 실행의 Console error 0 확인. `Temp/astra-lighting-main-check.txt`. 마우스로 모든 거래를 수행한 검사는 아니다.
- 일반 에디터 첫 시작에서 전체 임포트에 약 5분이 걸렸다. 시작 중 별도 배치 테스트 시도는 같은 프로젝트를 열 수 없어 종료 코드 1/실행 0건이었다. 그 시도를 성공으로 세지 않았으며, 준비 완료한 원래 에디터에서 위 두 모드를 정상 완료했다.
- 본편 판매창 색감/단계 자동 교체는 **설계 완료·구현 전**. 신규 source 외형의 전체 왕복 전환·해상도별 UX, 청소기 실제 조작감과 Player 빌드는 미검증이므로 전체 화면 품질은 `PARTIAL`이다. Temp 증거는 로컬 파일이며 다른 컴퓨터에서는 위 경로/절차로 재검증한다.
