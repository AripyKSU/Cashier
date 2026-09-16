# store-resource-exchange 병합 명세서

## 1. 기준과 현재 상태

- 작성일: 2026-09-16. 담당: 성규 / 가게 리소스·손님 표시 작업.
- 입력 브랜치: `codex/store-resource-exchange`.
- 확인한 HEAD: `08c7083f09386e2f5d8a29584809efdeb3142da2`. 로컬의 `origin/codex/store-resource-exchange`와 일치하며 문서 작성 전 작업 트리는 clean이었다.
- 비교 대상: 로컬에 기록된 `origin/total_merge`의 `0006cee83bf336d9e259c86a27be1fd122469869`.
- 공통 조상: `5c4d46d53db23bf324c643b5192d80c06dc7c6b1`. 공통 조상 대비 입력 변경은 323파일이며 아트 이동·metadata·프로토타입 문서를 포함한다. 실제 게임 기능 323개를 뜻하지 않는다.
- 이번 문서 작성에서 fetch·checkout·merge·commit·push·Unity 조작은 하지 않았다. 위 원격 추적 ref는 서버의 실시간 최신 상태를 보증하지 않는다. 실제 병합 직전에 승인된 fetch로 입력과 대상을 다시 고정한다.
- 이 문서는 위 입력 브랜치의 인계다. `hubimage` 등 다른 브랜치의 내용·우선순위는 조사하거나 확정하지 않았다.
- 통합 완료 상태는 **PARTIAL**: 아래 기능별 검증은 존재하지만 최신 total_merge와의 통합 실행 및 최종 UI/UX 확인은 남아 있다.

| 주요 커밋 | 포함 내용 |
|---|---|
| `9c66aa9d` | 단계별 가게·설비 외형, 상자·쏟기 이미지, 빈 이미지 처리 및 관련 검증 |
| `5ea8b5cc` | total_merge `5c4d46d` 동기화, 새 손님 외형·노멀맵 계약과 가게 변경 공존 |
| `b8788217` | 고정 슬롯 원근 배율 및 GameUI 딸 패널 폭 override |
| `f918f16d` | astra-prototype `a43c1aec` 병합, Stage1 원본·아트 경로 재편 |
| `08c7083f` | STAGE1_HANDOFF 1안의 가게 UI 적용, 연령별 손님 하향 오프셋 |

## 2. 반영할 기능과 필수 파일 묶음

### A. 가게 단계·설비 외형

`StoreStageData / FacilityData → ResourceData → 외형 Prefab → StoreStagePresentation` 경로를 유지한다.

- `FacilityData.csv`의 필수 header에 `stage1_resource_idx`, `stage2_resource_idx`, `stage3_resource_idx`를 추가했다. `uint`이며 0은 해당 단계 외형 없음, 비0은 **이미지가 아닌 외형 Prefab Resource FK**다.
- 설비 외형 12개는 기존 구매 설비 6종의 단계별 그림이다. 새 구매 설비 12개가 아니다. Prefab이 이미지·위치·크기를 소유하고 세션의 `IsFacilityActive`가 표시 여부를 결정한다.
- 같은 가게 단계에서도 설비 활성 상태를 재평가한다. 미구매·미활성 설비는 숨기며 참조 씬처럼 전부 강제로 표시하지 않는다.
- 가게 확장은 기존 구매 당일 반영, 상품·편의 설비 효과는 기존 다음 영업일 활성 규칙을 유지한다. 구매 가격·경제·해금 규칙을 변경하는 작업이 아니다.
- Stage1/2/3의 Front 및 TopView를 갱신하고 Stage2/3 TopView를 별도로 추가했다. Stage1 상판 확장면은 0개, Stage2/3은 RawImage 2개다.
- 시계 Resource 4300은 `Stage1Clock`, 4301은 `Stage2RustedClock`, 4302는 `Stage3GunmetalClock`이다.
- 설비·상판 확장면은 기존 `WorldSceneView.SetStageGraphics`의 시간대 색 처리에 포함한다.

필수 묶음:

- `Assets/Datas/FacilityData.csv`, `ResourceData.csv`, `StoreStageData.csv`
- `Assets/Scripts/Commons/Data/FacilityData.cs`
- `Assets/Scripts/Scene/StoreStageVisual.cs`, `StoreStagePresentation.cs`, `WorldSceneView.cs`, `GameUIController.cs`
- `Assets/Prefabs/StoreStage/`의 Front3·TopView3·Facilities12와 각 `.meta`
- 관련 운영 Texture, 기존 `Default Local Group.asset`의 승인된 등록16개

### B. 정면 상자와 PouringContainer

- `StoreStagePresentation.Apply(uint stage, Func<uint, bool> isFacilityActive, SaleSortingPanel sortingPanel = null)`로 호출 계약이 바뀐다. 기존 `Apply(stage)` 호출을 남기지 않는다.
- `SaleSortingPanel.ContainerOpenChanged`를 GameUIController가 구독/해제하고 `SetContainerOpen`으로 정면 상자의 닫힘/열림을 전환한다. 새 방문·비활성화·단계 전환 시 상태 초기화를 보존한다.
- `StoreStageVisual`의 `openContainerSprite`, `tiltedContainerSprite`, `emptyContainerSprite`, `counterExtensions` 직렬화 값과 소비자를 함께 반영한다.
- TopView의 두 쏟기 Sprite를 기존 `SaleSortingPanel.SetContainerSprites`에 전달한다. TopView Prefab 자체를 화면에 인스턴스화하는 방식이 아니다.
- 단계별 열린 상자 Sprite를 tilted/empty 양쪽에 임시 공용 사용한다. 전용 이미지가 생기면 이 슬롯만 교체한다.
- 기존 PouringContainer 객체·버튼·자식 입력·상품 물리 영역·쏟기/퇴장 동작을 교체하지 않는다. 상자 단계 이미지를 바꾸기 위해 새 CSV 컬럼을 추가하지 않는다.

### C. 외형 이미지 빈값 처리

- 손님 `CustomerAppearanceData.ImageResourceIdx`와 딸 `DaughterAppearanceData.ResourceIdx`, `DaughterDialogueResult.ResourceIdx`는 `uint?`다. **빈 셀만** 사각형 표시로 처리하고 0·잘못된 FK·지정된 주소의 로딩 실패는 오류로 유지한다.
- 손님의 `normal_resource_idx`는 통합된 외형/노멀맵 계약대로 필수다. 컬러 이미지가 비었다고 노멀 FK 검증까지 해제하지 않는다.
- GameUIController의 공유 흰색 Sprite를 재사용하며 화면 종료 시 소유 Sprite만 해제한다. 딸의 결과→ViewDataFactory→Presenter까지 nullable 계약을 함께 반영한다.
- 현재 실제 딸 CSV 17001/17002/17003의 `resource_idx`는 빈칸이다. 최신 손님 CSV 60종의 이미지 연결은 유지했다. CustomerAppearanceData.csv의 비교 차이는 마지막 개행 유무뿐이며 손님 데이터를 구형 임시 행으로 되돌리지 않는다.
- 감독관 이미지 계약은 변경하지 않는다. 예전 `FemaleCustomer_01` 주소를 일반 fallback으로 복원하지 않는다.

관련 파일: CustomerAppearanceData/DataTable, CustomerCatalog, DaughterAppearanceData/DataTable, DaughterDialogueService, GameUIController, ProgressViewDataFactory, DaughterDialoguePresenter 및 관련 CSV·테스트. 상세 계약은 [이미지 연결](../IMAGE_RESOURCE_INTEGRATION.md), [딸 대화](../DAUGHTER_DIALOGUE_SYSTEM.md)를 따른다.

### D. 손님 원근감·연령별 하향 오프셋

- `Assets/Prefabs/World/CustomerWorld.prefab`의 `CustomerWorldQueueView`에 적용한다. 구형 uGUI `CustomerQueueView`는 이번 변경 대상이 아니다.
- 성인 기준 현재 손님 높이550, Slot05 높이340, Slot10 높이240. 실제 대기 인원수와 무관한 고정 슬롯 보간이다. Slot01~10: 508/466/424/382/340/320/300/280/260/240.
- 입구는 마지막 슬롯 배율, 이동 중 크기는 0.65초 보간, 퇴장은 이탈 시 크기 고정이다. 아이는 각 위치의 성인 높이0.6배와 기존 상승/하단8px 보정을 유지한다.
- Inspector의 `adultDownOffsetPixels`, `elderlyDownOffsetPixels`, `childDownOffsetPixels`는 각각 성인·노인·아이 추가 하향량이다. **커밋 기본값은 모두0**이며 아직 성인·노인을 실제로 낮춘 수치가 저장된 것은 아니다.
- 양수일수록 아래로 이동한다. 전면 로컬 픽셀 단위이며 원근 배율을 곱하지 않는다. Visit.Attributes로 선택하여 입장·대기·현재·퇴장 모두 동일한 계산에 적용한다. 대사·이모지도 따라간다.
- Play 중 Inspector 조정은 바로 표시되나 종료하면 복구된다. 영구 수치는 EditMode에서 Prefab에 저장한다.
- 필수 묶음은 CustomerWorldQueueView.cs + CustomerWorld.prefab + 기존 WorldSceneTests/GameSessionApiTests다. 상세는 [대기열 명세](../CUSTOMER_QUEUE_INTEGRATION.md)를 따른다.

### E. STAGE1_HANDOFF 적용 범위와 아트 이관

- 사용자가 선택한 1안: 최신 이미지·위치·크기·표시 여부를 기존 UI/GameObject 분리 구조에 반영하고 현재 조명을 유지한다.
- 완료: `StoreStage1Front`, `Stage1FoodShelfVisual`, `Stage1MedicineCabinetVisual`의 UI 배치. 상판 y=-483, 정면 상자 y=-498, 시계 (1077,13) 및 숫자 영역 등을 참조 씬의 최종 override 기준으로 반영했다.
- `art/Facility/Frame/BoothCanopy.png`와 `art/Facility/Clock/Stage1BasicClock.png`를 각각 운영 폴더의 `Environment/Dystopia/BoothCanopy.png`, `UI/Dystopia/Stage1Clock.png`로 갱신했다. 원본은 art에 보존하고 운영 사본의 GUID·Sprite fileID·import·주소를 유지했다.
- 최신 Stage1 상판·닫힘/열림 상자·선반·약품장 PNG5개는 직전 Astra 병합에서 운영 사본에 반영했다.
- **미반영:** Stage1 문서의 배경·안개·경비병 재배치. World 배치의 적용 가능성만 조사했다. 기존 공용 World 값을 바꿔 2·3단계에까지 적용하지 않는다.
- **미도입:** PixelStage 전용 조명·접촉 그림자·480×270 픽셀 렌더링. 프로토타입 쪽 구현 파일이 병합 이력에 있다는 사실을 본편 활성화로 해석하지 않는다.
- 현재 Canvas/참조 씬의 authoring 좌표1280×720과 목표 출력1600×900을 구분한다. 좌표에1.25를 일괄 중복 적용하지 않는다.
- Astra 병합은 `a43c1aec`까지만 포함한다. 로컬 추적 astra-prototype의 후속 Stage2 문서 `8cb24c47`은 이 입력 브랜치에 반영하지 않았다.
- `Checkout → art` 경로 재편과 기존 운영 사본 보존이 섞여 있다. 파일명 유사성만으로 PNG/meta를 덮어쓰지 말고 [경로 보정 기록](../CHECKOUT_ASSET_LAYOUT.md)을 확인한다. `.meta` 재생성·중복 GUID·구형 상품 이미지의 임의 복원을 피한다.

## 3. Resource·Addressables 계약

기존 Default Local Group에 총16개(설비 Prefab12·TopView Prefab2·시계 이미지2)를 등록했다. 새 그룹·라벨은 없다. 승인된 주소·GUID·파일의 권위 목록은 [STORE_RESOURCE_REGISTRATION.csv](../data/STORE_RESOURCE_REGISTRATION.csv)다.

| 소비자 | Resource ID |
|---|---|
| 기존 시계 연결 갱신 | 4301 / 4302 |
| Stage2 / Stage3 TopView | 4378 / 4379 |
| 식량 선반 Stage1/2/3 | 4380 / 4382 / 4386 |
| 약품장 Stage1/2/3 | 4381 / 4383 / 4387 |
| 공구대 Stage1/2/3 | 0 / 4384 / 4388 |
| 전력·통신 Stage1/2/3 | 0 / 4385 / 4389 |
| 핵보호 Stage1/2/3 | 0 / 0 / 4390 |
| 정밀 전자장비 Stage1/2/3 | 0 / 0 / 4391 |

4377까지 다른 담당자의 손님 리소스 사용 범위이며 재할당하지 않는다. 최신 대상의 PK·주소·GUID와 다시 대조한다. 충돌 시 CSV만 재번호화하지 않고 모든 FK·주소 소비자를 묶어 처리한다. 기존 구형 시계 자산·등록은 다른 참조를 위해 보존했다. 신규 등록 확대는 이 인계로 승인되지 않는다.

## 4. total_merge와 겹치는 변경·보존 기준

확인한 대상에는 입력에 없는 커밋4개가 있다: 로딩 흐름 `6adcb75f`, 연령별 대사 `0f72c972`, UI·폰트·딸 창 변경 `e5a20c7d`, 폰트 정상화 `0006cee8`. 입력 브랜치 파일 전체로 대상을 덮어쓰면 안 된다.

아래는 공통 조상 대비 **양쪽이 수정한 파일**이며 실제 Git 충돌 발생을 확인한 결과는 아니다.

| 겹치는 파일 | 통합 시 확인 |
|---|---|
| GameUI.prefab | 대상의 최신 폰트·딸 UI 연결과 입력의 DaughterDialoguePanel 폭0 override를 의미 단위로 대조. 폭0을 최신 패널 구조에 무조건 이식하지 않는다. |
| CustomerWorld.prefab | 대상 폰트/표시 연결과 입력의 높이550·중간/후방 배율·연령별 오프셋3개를 함께 보존. |
| CustomerCatalog.cs | 대상의 연령별 대사 검증과 입력의 nullable 이미지 FK 검증을 결합. 노멀맵 필수 검증 유지. |
| CustomerCsvTests.cs | 두 데이터 계약에 맞춰 사례와 기대값 통합. 테스트 통과를 위해 제품 데이터나 검증을 완화하지 않는다. |
| DATA_CATALOG.md | 최신 대사 컬럼과 단계별 설비/선택적 이미지 계약 모두 기록. |

텍스트 충돌이 없어도 딸 CSV의 빈 이미지와 최신 딸 패널의 표시 방식, 가게 단계 이미지와 최신 UI 빛/정렬, 로딩 완료 덮개와 자산 preload 완료 시점은 실행 확인이 필요하다. 입력의 CustomerWorldBody material/meta 변경은 공백 정리에 한정되며 다른 material 값 변경을 덮어쓸 근거가 아니다.

## 5. 권장 병합·조립 순서

1. 사용자 승인 범위에서 최신 원격을 확인하고 입력/대상 SHA를 고정한다. dirty·Local·stash와 Unity 미저장 상태를 먼저 보존한다. 화면/자산 우선순위가 필요한 충돌은 [통합 규칙](../BRANCH_INTEGRATION_RULES.md)에 따라 결정한다.
2. CSV·DTO·nullable 결과/소비자·Resource ID·등록16개·Prefab/meta를 계약 단위로 함께 반영한다.
3. StoreStagePresentation의 새 Apply 호출과 ContainerOpenChanged 구독/해제를 대조한다. 공유 manager나 동일 기능의 중복 컴포넌트를 추가하지 않는다.
4. 단계별 Prefab/운영 사본을 연결하고 최신 GameUI/CustomerWorld의 폰트·UI 참조를 보존한다. 프로토타입 스크립트를 본편에 붙이지 않는다.
5. 최종 통합 씬은 대상의 `Assets/Scenes/MainScene.unity`를 기준으로 확인한다. 이 입력 작업은 MainScene 파일을 직접 변경하지 않았지만 공유 Prefab·데이터는 MainScene에서도 로드된다. Local 씬을 통합 씬으로 복사하지 않는다.
6. 컴파일·CSV/FK·GUID·missing reference 검사 후 관련 Test Runner 실행, Init→Hub→Main 진입과 단계별 표시를 확인한다. 테스트의 start scene·background 등 임시 설정을 복원한다.
7. 최종 사용자 확인과 승인된 commit/push 후, 본 병합 전용 문서의 필요한 내용을 영구 명세/PR에 보존했을 때만 정리한다. 영구 명세·원본 아트 인계 문서는 삭제하지 않는다.

## 6. 기존 검증 증거와 남은 확인

아래는 구현 단계별 실행 기록이다. 이번 문서 작성 중 테스트를 새로 실행하지 않았으며 서로 다른 시점의 결과를 합산해 최신 통합본 전체 PASS로 보고하지 않는다. Temp 증거는 Git 제외여서 다른 작업자의 PC에는 없을 수 있다.

| 범위 | 기록된 결과 | 증거 |
|---|---|---|
| 가게 데이터·설비 API | EditMode45/45 (10+35) | 기존 [작업 기록](store-resource-exchange.md) §7 |
| 단계별 쏟기 데이터 | EditMode10/10 | `Temp/stage-pouring-edit.json` |
| Stage1 UI 적용 후 자산/표시 API | PlayMode4/4, 실제 Prefab21·시계3 로드 | `Temp/stage1-handoff-play.json` |
| 손님 빈 외형 | 당시 EditMode70/70·PlayMode1/1 | `Temp/customer-placeholder-edit.json`, `customer-placeholder-play.json` |
| 딸 빈 외형 | 당시 EditMode6/6·PlayMode2/2 | `Temp/daughter-placeholder-edit.json`, `daughter-placeholder-play.json` |
| 원근감·연령별 오프셋 포함 월드 | 최종 EditMode12/12·PlayMode1/1, 실패/skip0 | `Temp/queue-age-offset-edit-pass.json`, `queue-age-offset-play.json` |

- 마지막 오프셋 검증 당시 compile error0·Console error0·CustomerWorld missing script/reference0. MainScene와 font 파일 hash를 보존했다. 실제 표시 상태의 연령별 Y 변화·0 복귀·크기/기존 보정 불변·대사 추종 및 기존 입퇴장/이모지 수명을 확인했다.
- 과거 전체 EditMode는 293/297, 이후 total_merge 동기화 실행은307/313이었다. 후자의 계약 테스트2개는 수정 후57/57로 재검증했고 WorldSceneTests의 구형 FogBack 경로/없는 shader property 검사도 최종12/12에서 해소했다. 전체 suite를 다시 실행하지 않았으므로 나머지 상태를 새로 확정하지 않는다.
- 미해결로 기록된 기존 항목: `BusinessClockAndSortingTests.VacuumAsset_UsesAstraImportContract`, `Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings`, `PriceEventTests.ReproducibleSelectionAndInvalidReferences`. 통합 시 현재 재현 여부 확인 필요.
- 프로토타입 Stage1Reference에는 이전 검사상 끊긴 참조4개(딸1·지침 상품3)가 남는다. 본편 MainScene의 새 결함이나 완료된 프로토타입 표시로 혼동하지 않는다.
- 배경/안개/경비병의 Stage1 전용 재배치, 전체 PlayMode, Player build, 최종 배치·조작감·실제 하향량은 미검증 또는 미구현이다.

통합 후 최소 재검증:

- Test Runner: StoreStageDataTests, FacilityTests, CustomerCsvTests, DaughterDialogueTests, WorldSceneTests, CustomerAppearancePlaceholderTests, StoreStagePresentationTests, GameSessionApiTests의 월드 대기열 사례. 대상의 연령별 대사/생성 테스트도 함께 확인한다.
- 1→2→3→1 단계 전환, 같은 단계의 설비 활성 변화, 시계·열린/닫힌 상자·PouringContainer, 비활성화와 늦은 알림, 시간대 색·입력 영역 보존.
- 성인·노인·아이 각각의 입장/대기/계산대/퇴장, 오프셋0 유지 및 개별 조정, 대사·이모지 추종. 손님·딸 빈 이미지와 명시된 잘못된 FK 오류의 구분.
- Init→Hub→Main의 준비 덮개·감독관·일일지침 순서 및 최신 폰트/딸 UI와 공존. 최종 UI/UX 판정은 사용자가 한다.

기존 [가게 작업 기록](store-resource-exchange.md)의 오래된 HEAD·구현 예정·commit/push 미실행 문구는 각 기록 작성 당시 상태다. 본 문서의 고정 SHA와 실제 병합 시점의 Git 상태를 기준으로 판단한다.
