# 가게 리소스 교체·적용 작업 범위

## ART_UPDATE_20260916 4단계: 상자 착지·먼지·전환 (2026-09-16)

- 기존 `SaleSortingPanel.playContainerArrival`을 76px 높이의 0.3초 가속 낙하와 0.55초 감쇠 눌림·복원으로 변경했다. 총0.85초 및 기존 손님 도착 대기·자동 전환 시간은 유지한다. Rect의 피벗·배율·회전으로 하단 중앙을 계산해 눌림 중 접점을 고정하고 착지 시 SFX와 먼지를 한 번 시작한다.
- 새 방문·ClearCustomer·결과 표시·OnDisable/OnDestroy는 진행 중 착지를 정리하고 원래 위치·배율·회전을 복원한다. 연출 중 Inspector 등 외부에서 배치를 바꾸면 그 값을 보존하고 정면 클릭 대기로 돌아간다. 기존 표시 차단 조회를 먼지에도 전달하며 해제 시 새 컴포넌트를 생성하지 않는다. 새 게임 일시정지 기능을 추가한 것은 아니다.
- `LandingDustEffect.CalculateContactPoint`는 현재 상자의 하단 중앙을 부모 좌상단 좌표계로 변환한다. BaseContactPoint는 null 상자/수동 테스트 fallback, DustOffset은 추가 보정으로 유지한다. 먼지10개·12×6·0.55초·기본 색(.34,.32,.28)·알파.42→0·좌우 교대 분산·2px 스냅을 적용했다. Image.color와 CanvasRenderer에 alpha를 중복 적용하던 부분을 제거했다. GameUI.prefab의 시간·색 직렬화2곳도 함께 갱신했다.
- 탑뷰 전환은 이미 현재 단계의 `workbench`가 포함된 `sortingView` 자체를 이동한다. 별도 덮개 Sprite 교체 시스템을 추가하지 않았고 기존 쏟기·상자 열림/퇴장 코드는 유지한다. Preview Scene 정적 확인에서 MainScene과 StoreResourceSandbox의 작업대가 이동 영역 자식이며 새 먼지 설정을 상속한다. 구형 SpriteWorldSandbox에는 StoreStagePresentation이 없어 단계별 통합 확인은 StoreResourceSandbox를 사용한다.
- 구현 담당: `/root/crate_landing_step4` (gpt-5.6-sol/medium). 기존 지속 프로그래머 작업이 무출력 종료하여 제한 범위로 재배정했다. 설계 담당이 취소·외부 편집·조회 해제 경로를 리뷰하고 프리팹·문서·최종 컴파일을 담당했다.
- 검증 상태 **PARTIAL**: 컴파일 오류0·Console error0·missing script0. GameUI 전체에서 기존 이미지 참조2개(`FrontContainer.m_Sprite`, `ReputationStamp.m_Sprite`)가 누락되어 있다. 이번 GameUI diff는 먼지 시간·색2줄뿐이며 두 참조는 이전과 동일하다. 정상 단계 적용 시 정면 상자는 StoreStagePresentation에서 갱신되지만 실행 확인은 아직 하지 않았다. 근거: `Temp/art-update-step4/verification.json`, `existing-missing-references.json`, `scene-bindings.json`, `preservation.json`, `backup/`.
- 기존 EditMode 먼지2개 검사를 보완하고 PlayMode `ContainerArrivalPausesAndRestoresAcrossRestartAndCancel` 1개를 준비했다. 접점의 비단위 scale/pivot/rotation·fallback, 상자/먼지 차단·재시작·취소·정상 복원 경계를 대상으로 한다. **Test Runner/Play 실행0회**이며 통과 결과로 보고하지 않는다.
- 1~3단계 산출물과 MainScene/Local씬 등 보호 파일38개를 보존했다. 신규 자산·Addressables·CSV·씬·고객 큐·그림자 변경은 없다. 남은 단계는 **5단계 누적 API/수명 검증, 기존 누락 참조 영향 확인, 문서·병합 기록 정리**다. 화면·조작감은 사용자 확인 대상이며 commit/push 미실행.

## ART_UPDATE_20260916 3단계: 월드 배경 배치 (2026-09-16)

- 최신 Stage1/2/3Reference의 월드 배치가 동일함을 확인했다(군중·바리케이드의 부동소수점 미세 차이 제외). 기존 `CustomerWorld`와 `StoreStage1/2/3World` 총4개 Prefab에 반영했으며 단계별 공용 API나 런타임 코드는 추가하지 않았다.
- 단계 World3개의 배경6개·연기 기준점2개를 갱신했다. 공용 World에는 배경·연기·안개3개·군중3개·바리케이드·경비병2명·총구2개의 배치를 반영하고 새5마리의 렌더 영역을 하늘 영역에 맞췄다. Image의 pivot·회전·preserveAspect와 Sprite bounds를 계산해 SpriteRenderer의 중심·배율로 변환했다.
- 탐조등 시작점은 각각 경비병 중심 `(126.20337,-172.6695)`, `(1159.7751,-234.81252)`에 맞췄다. 표시 순서는 기준 씬처럼 안개 → 중경 → 탐조등 → 군중 → 경비병 → 바리케이드다. 기준 씬에서 비활성인 감시탑 중복 이미지2개와 Front UI가 소유하는 천막의 구형 World 사본은 비활성화했다. 석양 초기 Sprite도 기존 사용 폴더의 `SunsetClouded`로 맞췄다.
- 경비병의 `Origin/WidthPixels/HeightPixels`, 연기의 `Origin`도 함께 갱신하여 애니메이션 시 종전 배치로 복귀하지 않도록 했다. 기존 시간대 색·셰이더·프레임 전환·사격·일시정지 동작은 유지한다. 원본 art·프로토타입 경로를 직접 참조하지 않으며 새 이미지·Addressables·CSV 변경은 없다.
- **STATIC PASS**: Prefab4개의 배치102건을 3개 권위 씬과 대조(최대 모서리 오차 약0.000126px), 표시 순서·애니메이션 기준점 정합, missing script/reference0·원본 경로 의존0, meta4개 보존, 컴파일 실패 없음·Console error0. 증거: `Temp/art-update-step3/verification.json`, `layout-before.json`, `layout-after.json`, `preservation.json`, `backup/`. Preview Scene의 Canvas는 월드 행렬이 0이 될 수 있어 저장된 로컬 TRS로 모서리를 대조했다.
- 문서 갱신 전 보호 파일38개(1/2단계 산출물·MainScene/Local씬·권위 씬)의 해시 일치, 대기열 컴포넌트와 루트/대기열 Transform17개 보존을 확인했다. 성인·노인·아이 오프셋은 변경하지 않았다. MainScene에 이번 배치/표시 순서를 덮는 별도 override가 없음을 정적으로 확인했으며 Scene 파일은 저장하지 않았다.
- Test Runner/Play 실행 **0회**, 최종 화면·시간대·단계 전환 UX는 미확인이다. 다음은 **4단계 착지 높이·찌그러짐·먼지·탑뷰 전환 연출**, 이후 **5단계 누적 기능 검증·문서 정리**다. 전용 PixelStage 조명·접촉 그림자는 기존 합의대로 별도 범위이며 이번에는 도입하지 않았다. commit/push 미실행.

## ART_UPDATE_20260916 2단계: 정면 배치 (2026-09-16)

- 권위는 최신 `STAGE1/2/3_HANDOFF.md`와 대응 `Stage1/2/3Reference.unity`의 저장값이다. Unity Preview Scene으로 원본 prefab override를 포함한 값을 읽고, Prefab 편집 API로 본편 자산을 보정했다. 1단계의 미커밋 PNG/import 변경은 보존했다.
- 변경 Prefab5개: Front3개의 상자를 `(522,-505)`, size `(360,240)`, scale `(0.72995913,0.72995913,0.66766)`으로 통일했다. Stage1 상판 y=-498, 시계 숫자 y=-45, 식량 선반 y=-467, 약품장 y=-466으로 갱신했다. 나머지 설비10개의 배치는 이미 최신 기준과 일치해 저장하지 않았다.
- 가림 순서 차이를 함께 보정했다. Front의 `facilityDrawOrder`가 Facility ID로 뒤→앞 순서를 소유한다: Stage1 `[12001,12002]`, Stage2 `[12003,12004,12001,12002]`, Stage3 `[12005,12006,12003,12004,12001,12002]`. 표시 코드는 이 순서로 형제 인덱스를 부여한다. 기존 빈 배열은 종전 순서와 호환되며, 명시한 배열의 중복·미등록·누락은 PrepareAsync에서 준비 결과 공개 전에 거부한다. 비용·보유·활성일·같은 단계 활성 갱신 계약은 유지한다.
- **STATIC PASS**: Front3+설비12 총15개 Prefab의 배치40곳(정면 슬롯21·시계 숫자3·상판 확장4·설비12)이 권위 씬과 일치한다. 확장 UV·표시 여부·Sprite 연결 확인, 대상 missing script/reference0, art 직접 의존0, prefab meta15개 보존, 컴파일 실패 없음·최종 Console error0. `Temp/art-update-step2/verification.json`, `layout-before.json`, `changes.json`, `backup/`.
- `StoreStagePresentationTests.ApplyRefreshesFacilitiesAndResetsContainerAcrossStages`에 2·3단계 실제 생성 순서 기대를 반영했다. 사용자 요청에 따라 Test Runner/Play 실행은 **0회**이며 최종 단계에서 실행할 예정이다. 명시 순서 검증의 정상·중복·누락 실패 경로도 최종 검증에 포함한다. 전체 표시/UX 성공으로 판단하지 않는다.
- MainScene·Local씬·원본 참조 씬·폰트·CSV·Addressables 변경 없음. 배경·탐조등·손님·연령 오프셋은 다음 단계 범위이며, 착지/먼지 연출과 그림자·전용 조명은 이번 단계에서 수정하지 않았다. commit/push 미실행.

## ART_UPDATE_20260916 1단계: 이미지·import 갱신 (2026-09-16)

- 기준: `codex/store-resource-exchange fa4033f2`, `Assets/Textures/art/ART_UPDATE_20260916.md` 및 최신 `STAGE1/2/3_HANDOFF.md`. 사용자 승인 범위는 1단계만이며, 기능 테스트는 후속 구현을 마친 마지막 단계에서 모아서 실행한다.
- 원본 PNG를 기존 사용 경로에 복사: `art/Facility/Clock/Stage1BasicClock.png` → `UI/Dystopia/Stage1Clock.png`, `art/Facility/Frame/BoothCanopy.png` → `Environment/Dystopia/BoothCanopy.png`, `art/Facility/Frame/Stage3Shop.png` → `Environment/Dystopia/Stage3Shop.png` (모두 `Assets/Textures/` 기준). 크기가 동일한 원본을 사용하고 기존 파일명·GUID·Sprite 식별자/영역을 보존했다. 원본 art는 수정하지 않았다.
- 직전 병합으로 사용 폴더에 반영된 PNG24개는 다시 복사하지 않았다. 합계27개 중 Bilinear였던 1·2단계 이미지14개의 `filterMode`만 Point로 보정했다. 나머지13개는 이미 Point이며, `.meta`의 다른 값은 모두 보존했다. 대상별 목록은 `Temp/art-update-step1/manifest.json`, `filter-changes.json`에 있다.
- **STATIC PASS**: Unity Editor import/load27개 성공, Point27개, 전후 Sprite GUID/fileID/rect/pivot 일치, PNG3개 원본 해시 일치, `.meta`27개는 필터14곳 외 바이트 동일. 컴파일 실패 없음·Console error0, InitScene clean 유지. 근거/백업: `Temp/art-update-step1/import-before.json`, `import-after.json`, `backup/`.
- 이번 변경은 PNG3개·기존 meta14개 및 이 기록이다. 배치·Prefab·Scene·CSV·Addressables·게임 코드 변경, 기능 테스트, commit/push는 수행하지 않았다. 다음은 단계별 정면 배치 갱신이며, 최종 시각 품질과 기능 통과를 이번 import 검사로 판정하지 않는다.

## STAGE1_HANDOFF 1안 적용 (2026-09-16)

- 사용자 선택: 이미지·위치·크기·표시 여부를 현재 UI/GameObject 분리 구조에 맞춘다. 원본은 art에 보존하며 실제 연결은 기존 사용 폴더로 이관·갱신한다. 전용 PixelStage 조명/접촉 그림자/480×270 렌더링은 도입하지 않는다.
- 가게 본체 반영: `StoreStage1Front`, `Stage1FoodShelfVisual`, `Stage1MedicineCabinetVisual`의 기준은 `STAGE1_HANDOFF.md`와 그 권위 씬의 최종 override 합산값이다. 상판 y=-483, 상자 y=-498, 천막 위치(-15.2,31)·크기(1344,720)·x배율0.975, 선반(65,-454), 약품장(983.84,-451)·배율1.17347193, 시계(1077,13)와 숫자 표시창을 반영했다. 비활성 기둥/천장등·상판 확장면은 숨기며 설비의 실제 표시 여부는 기존 활성 판정을 유지한다.
- 아트 갱신: `art/Facility/Frame/BoothCanopy.png` → `Environment/Dystopia/BoothCanopy.png`, `art/Facility/Clock/Stage1BasicClock.png` → `UI/Dystopia/Stage1Clock.png`. 기존 GUID·Sprite fileID·import·주소를 유지하며 Resource4300은 계속 Stage1Clock이다. 상판·상자2개·설비2개의 최신 PNG는 직전 Astra 병합에서 이미 갱신됐다. 새 Addressables 등록·CSV·셰이더·게임 코드·MainScene 수정 없음.
- 현재 Canvas와 기준 씬은 모두1280×720 좌표다. 1600×900 출력에 별도1.25배 좌표 보정을 중복 적용하지 않는다. 현재 UI/조명 경로에서 가게 본체 미리보기를 생성했다(`Temp/stage1-handoff-preview.png`; 고객·시간 텍스트·실제 조작을 포함한 전체 게임 화면 검증은 아님).
- 검증: 원본 대비 Rect10개 일치, 대상 prefab의 art 직접 의존0·missing script0, 시계 Sprite rect(200,314,1002,494), 컴파일 실패 없음. PlayMode `StoreStagePresentationTests` **4/4**, 실패·skip0: 실제 ResourceManager로 Prefab21·시계3 로드, 설비 활성화·단계 왕복·쏟기 상태·수명 정리. 증거 `Temp/stage1-handoff-verification.json`, `Temp/stage1-handoff-resource-update.json`, `Temp/stage1-handoff-play.json`. 최종 게임 화면의 가독성/사용감은 사용자 확인 대상이다. commit/push 미실행.

### UI / GameObject 배치 적용 경계 확인

- UI: 천막·상판·상자·시계는 StoreStage1Front, 설비는 각 단계별 외형 prefab. 이번 가게 본체 변경은 이 경계에 적용했다.
- GameObject: 원경/시간대 배경/중경6개와 좌우 연기는 StoreStage1World의 `worldLayers`/`smokeAnchors`로 단계별 적용 가능하다. Image 사각형을 SpriteRenderer의 중심·bounds·Transform으로 변환해야 하며 RectTransform 값을 그대로 localScale에 넣지 않는다.
- 안개·경비병·총구·군중·탐조등 등의 현재 배치는 CustomerWorld 공용이며 단계별 외형 계약에 포함되지 않는다. 1단계만 재배치하려면 이 객체들의 기준 Transform/표시값을 단계별 prefab에서 전달하도록 기존 계약을 확장해야 한다. 공용 prefab 값만 바꾸면2·3단계에도 영향을 준다. 애니메이션 위상/시간·숨김·일시정지·총구의 부모 관계는 기존 WorldSceneView가 계속 소유한다.
- 이 World 영역은 이번 사용자 후속 요청에 따라 적용 가능성만 확인했으며 아직 변경하지 않았다. 손님/대기열은 기존550/340/240 합의를 유지한다.

## astra-prototype 병합 (2026-09-16)

- 입력: `codex/store-resource-exchange b878821` + fetch한 `origin/astra-prototype a43c1ae`. 시작 시 양쪽 대상/upstream 일치·작업 트리 clean, stash3개 보존. 최신 Stage1 기준 씬·아트·접촉 그림자·인계 문서와 프로토타입 경로 재편을 통합한다.
- 같은 자산의 폴더 이동 충돌은 기존 게임 사용 경로/GUID를 보존하고 별도 프로토타입 자산은 새 art 경로에 두었다. Stage1CounterTop·Stage1CrateClosed/Open·Stage1FoodShelf·Stage1MedicineCabinet의 PNG5개는 최신 입력과 해시가 일치한다. 기존 시계 Stage2RustedClock/Stage3GunmetalClock은 `Assets/Textures/UI/Dystopia/`를 유지한다. Editor 도구의 경로도 병합 결과에 맞췄다.
- 본편 `Assets/Scripts`, `Datas`, `Prefabs`, 공유/Local 씬, Addressables, Packages/ProjectSettings는 변경하지 않았다. 대기열550/340/240·설비 외형 FK·단계별 시계/상자 연결을 보존한다. 원본 Stage1Reference와 프로토타입 전용 shader는 입력 브랜치 변경을 반영했다.
- 컴파일/셰이더 오류0, 변경 이미지100개 로드 성공, GUID 중복0·meta 누락0. EditMode 대기열4/4·StoreStageData10/10, 실패/skip0. 증거: `Temp/astra-merge-static.json`, `Temp/astra-merge-unity-assets.json`, `Temp/astra-merge-queue-edit.json`, `Temp/astra-merge-store-edit.json`.
- Stage1Reference의250개 객체 검사에서 기존 끊긴 참조4개가 남는다: FrontCounter.daughter, InstructionProduct0/1/2.m_Sprite. 딸 FK는 양쪽 입력에서 동일하며 지침 상품 참조를 가진 FrontView.prefab은 이번 병합에서 변경되지 않았다. 본편 MainScene의 새 결함으로 해석하지 않는다. 프로토타입 표시 전체는 PARTIAL이며 이번에 임의 이미지를 연결하지 않았다. PlayMode·전체 suite·최종 화면/UX는 재검증하지 않았다.
- 원격 push는 이번 요청 범위 밖이다. 원본 아트 인계 문서는 보존하며 아래 경로 보정은 `doc/CHECKOUT_ASSET_LAYOUT.md`에 함께 기록한다.

## total_merge 동기화 (2026-09-16)

- 대상 `codex/store-resource-exchange 9c66aa9`, 입력 `origin/total_merge 5c4d46d`(fetch 확인). 작업 트리는 시작 시 clean이었고 기존 stash 3개와 Local 씬을 보존했다. 원격 push는 요청 범위에 포함하지 않는다.
- 새 손님 외형 60행, 컬러/노멀 120개 주소, 성향·성별·연령 선택과 어린이 배치, 전량 제외 시 거래 거부·계산기 즉시 닫기·지침 확률 변경을 반영했다. 기존 가게 단계/설비/PouringContainer와 손님·딸의 빈 이미지 허용은 유지했다. 손님 노멀 FK는 새 데이터의 필수 계약을 따른다.
- Resource4201~4245는 새 손님 주소로 갱신하고 4303~4377을 추가했다. 기존 가게용 4301/4302와 4378~4391을 유지하여 Resource191행의 ID 중복0을 확인했다. Addressables217개 주소/entry 중복0, 손님 FK와 실제 Sprite/Texture 자산120개 검사 통과. 가게 등록16개·단계 외형6개와 GameUI/OperatingPanel/CustomerWorld의 script·material 검사 통과.
- 구형 상품 metadata6개의 GUID 충돌은 양쪽 GUID의 사용 참조가 없음을 확인하고 total_merge 기준으로 통일했다. MainScene 파일은 변경하지 않았다. UI/자산 전체 ours/theirs 선택은 하지 않았다.
- 컴파일 통과. 전체 EditMode 307/313, 실패6·skip0. 이 중 전량 제외 계약과 달랐던 테스트2개를 거부 확정·매출0·재제출 금지 검사로 갱신하고 CustomerContractTests **57/57** 재검증 통과했다(`Temp/store-total-merge-contracts.json`). 손님 외형60종 변경에 따라 Sprite 로드 기대61→76과 딸 이미지의 고정4201 기대도 현 데이터 계약에 맞췄다.
- 기존 실패4개는 유지: `BusinessClockAndSortingTests.Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings`, `VacuumAsset_UsesAstraImportContract`, `PriceEventTests.ReproducibleSelectionAndInvalidReferences`, `WorldSceneTests.EffectsKeepSpriteTimeAndSourceBoundaries`. 이전 가게 리소스 검증에서도 확인된 범위 밖 문제이며 전체 테스트 PASS로 보고하지 않는다. 전체 suite 재실행은 하지 않았다.
- 자산 검사 증거: `Temp/store-total-merge-assets.json`, `Temp/store-asset-verification.json`. 이 병합에서는 손님 아트 작업의 검증 경계를 따라 PlayMode·게임 실행·화면 UX·Player 빌드를 수행하지 않았다. 기술 통합 검증은 정적 검사/컴파일/EditMode이며 시각 검증은 별도다.

## PouringContainer 단계 연결 (2026-09-16 후속)

- CSV 컬럼 추가 없이 기존 `StoreStageData.top_view_prefab_resource_idx → StoreStageVisual`을 사용한다. TopView 프리팹의 `tiltedContainerSprite`와 `emptyContainerSprite`가 기존 `SaleSortingPanel.SetContainerSprites`로 전달된다.
- 1단계 `Stage1CrateOpen`, 2단계 `Stage2RustedCrateOpen`, 3단계 `Stage3CrateOpen`을 두 슬롯에 공통으로 연결했다. 전용 쏟기/빈 상태 이미지가 추가되면 해당 슬롯만 교체한다. 사용 경로는 `Assets/Textures/UI/Dystopia/`이며 기존 Prefab 의존성으로 로드하므로 신규 Addressables 등록은 없다.
- 기존 PouringContainer 객체, 위치·크기, 회전·퇴장·페이드와 상품 분류는 유지한다. 단계 이미지 전달은 다음 쏟기/퇴장에 사용할 참조를 갱신하며 진행 중인 연출을 다시 시작하지 않는다. TopView 프리팹은 인스턴스화하지 않는다. MainScene/CSV/이미지 원본은 이번 후속에서 수정하지 않는다.
- 검증: 컴파일 통과, `StoreStageDataTests` EditMode 10/10, `StoreStagePresentationTests` PlayMode 4/4, 실패·skip 0. 실제 Addressables 로드와 1→2→3→1 전환의 기존 쏟기·퇴장 Image/배치 복원을 확인했다. 결과: `Temp/stage-pouring-edit.json`, `Temp/stage-pouring-play.json`. 전용 각도 이미지가 없는 상태의 시각적 적합성과 플레이 UX는 사용자 확인 대상이다.

## 기준과 상태

- 2026-09-16, `codex/store-resource-exchange`, HEAD `53fc9fc`. upstream은 `origin/codex/store-resource-exchange`.
- 상태: 사용자 실행 승인 후 구현 중. 단계별 설비 Resource FK 3개는 **외형 Prefab**을 참조하는 방식으로 확정했다. Git 작업은 수행하지 않았다.
- 사용자 원칙: 작업 씬은 Local에서 검증하고 MainScene 자체 편집은 최종 통합 시 수행한다. 공유 Prefab 수정이 MainScene의 표시에도 상속된다는 점은 구분한다.
- 기존 TMP fallback과 제품 Legacy metadata 6개는 이번 범위 밖이다.
- 근거: `Assets/Textures/ART_HANDOFF.md`, `ART_IMAGE_INVENTORY.md`, `STAGE_LAYOUT_HANDOFF.md`, `doc/data/ART_RESOURCE_MOVE_MAP.json`, 현재 StoreStage/Facility/SaleSorting 코드와 CSV.
- 삭제한 `output` 사본에 의존하지 않는다. `Assets/DystopiaPrototype/Editor/References/Stage1Reference.unity`, `Stage2Reference.unity`, `Stage3Reference.unity`는 모두 존재한다.

## 1. 적용 리소스: 27개

이관한 가게 PNG 29개 중 구형 `Stage2CrateClosed/Open` 2개는 적용 대상에서 제외한다. 삭제를 추가 요청하는 문서는 아니다.

| 역할 | Stage1 | Stage2 | Stage3 | 수량 |
| --- | --- | --- | --- | ---: |
| 상판 | Stage1CounterTop | Stage2CounterTop | Stage3CounterTop | 3 |
| 닫힌 상자 | Stage1CrateClosed | Stage2RustedCrateClosed | Stage3CrateClosed | 3 |
| 열린 상자 | Stage1CrateOpen | Stage2RustedCrateOpen | Stage3CrateOpen | 3 |
| 프레임 | 기존 유지 | Stage2RustedFrame | 기존 Stage3Shop 유지 | 1 |
| 시계 | 기존 Stage1Clock 유지 | Stage2RustedClock | Stage3GunmetalClock | 2 |
| 탑뷰 | Stage1TopDownWorkbench | Stage2TopDownWorkbench | Stage3TopDownWorkbench | 3 |
| 설비 소품 | FoodShelf, MedicineCabinet | 앞의 2개 + ToolBench, PowerCommunications | 앞의 4개 + NuclearProtection, PrecisionElectronics | 12 |

파일 확장자는 모두 `.png`. 소품 파일명은 `Stage{단계}{설비명}.png`다.

- 상판·설비·Stage2 프레임: `Assets/Textures/Environment/Dystopia/`
- 작업대: `Assets/Textures/Environment/Dystopia/TopDown/`
- 상자·시계: `Assets/Textures/UI/Dystopia/`
- 기존 Stage1 시계, Stage1/3 프레임, 배경·연기·새·안개·경비병 등은 재사용한다. 새 27개에 포함되지 않으며 필요 배치와 겹침만 검토한다.
- **기본 외형 12개:** 상판3 + 닫힘3 + 프레임1 + 시계2 + 탑뷰3.
- **상태 연결 15개:** 열림3 + 설비 소품12.

## 2. 현재 구조에서 필요한 변경

### 기본 가게 외형

`StoreStageData → ResourceData → StoreStageVisual Prefab → StoreStagePresentation.Apply` 흐름을 유지한다.

- `StoreStage{1,2,3}Front.prefab`: 상판, 닫힌 상자, Stage2 프레임, 시계 몸체 및 숫자 배치를 갱신한다.
- `StoreStage1TopView.prefab` 수정 및 Stage2/3 TopView 2개 추가를 기본안으로 한다. 현재 3단계 모두 Resource 4294를 공유하므로 CSV 연결을 나눈다.
- 기존 clock_resource_idx 4300~4302의 의미는 유지하고 Stage2/3의 새 시계 경로·주소를 맞춘다. 실제 Sprite 로드와 subasset 식별을 검증한다.
- ResourceData/StoreStageData 2개 CSV와 기존 Addressables 그룹을 수정 대상으로 예상한다. Stage2/3 TopView 신규 2개 및 시계 교체 2개가 기본 등록 변경 후보다. 정확한 ID·주소·GUID는 구현 전 목록으로 확정하고 프로젝트 승인 절차를 따른다.
- Prefab 직접 의존인 상판·상자·소품을 전부 개별 Addressables로 등록할 필요는 없다.

### 배치와 시간대 색

- 기존 Stage3Shop은 천장·기둥·전등까지 포함한다. 상판만 별도 교체하며 atlas 전체를 덮어쓰지 않는다.
- Stage2Shop 하부장 등은 다른 단계와 공유하므로 Stage2RustedFrame으로 전역 치환하지 않는다.
- 상판 좌우 확장은 참조 씬의 RawImage/UV와 함께 반영한다. 현재 StoreStageVisual의 Image 8개 복사만으로 RawImage 자식은 옮겨지지 않는다.
- Stage3 최신 기준은 08:10 건메탈 재승인 + 이후 열린 상자 수정 + 08:23 건메탈 시계다. 과거 취소된 복원 기록을 최종본으로 사용하지 않는다.
- Stage3 소품의 중복 그림자 해제·균일 상자 배율·시계 숫자 비율을 유지한다. 참조 씬 숫자를 현재 Canvas 좌표계에 무조건 대입하지 않는다.
- 해상도 경계: 현재 GameUI CanvasScaler의 referenceResolution은 **1280×720**이다. 사용자가 정한 목표 **1600×900**과 구분해야 한다. 이번에는 1600×900 출력에서 가게 배치를 확인하되, 프로젝트 전체 uGUI 기준 좌표 전환은 별도 `ugui-layout-standard` 작업과 조율한다. 기존 좌표에 1.25를 일괄 곱하는 변경은 하지 않으며, 전체 Canvas 규격 전환을 포함할 경우 아래 견적을 다시 산정한다.
- 기존 `WorldSceneView.SetStageGraphics`의 시간대 색 적용에 신규 설비와 확장면을 포함한다. 기존 UI/월드 조명 경로를 우선 재사용한다.
- 프로토타입의 노멀맵·금속광·그림자를 완전히 동일하게 재현하는 새 렌더링 작업은 기본 견적 밖이다. 시간대 색 및 기존 조명과의 호환 확인은 포함한다.
- 탑뷰 그림의 종횡비가 달라지므로 기존 144px 상품, 드래그/분류 영역, 계산기, 청소기·자동분류 배치를 유지하며 겹침을 검토한다.

### 설비 구매에 따른 소품 표시

- 현재 Front는 고정 Image 8개이고 별도 설비 소품 슬롯이 없다.
- 확정: FacilityData의 `stage1_resource_idx`, `stage2_resource_idx`, `stage3_resource_idx`에서 ResourceData를 거쳐 단계별 외형 Prefab을 선택한다. Prefab은 이미지·배치·크기를 소유하며 StoreStageVisual에 같은 설비 슬롯을 중복 등록하지 않는다. 12개 외형이 새 구매 설비 12개를 뜻하지는 않는다.
- 현재 CSV/TextData 대응: 12001 식량 선반, 12002 약품장, 12003 공구대, 12004 전력·통신, 12005 핵보호, 12006 정밀 전자장비. 이 ID로 연결하고 파일명으로 런타임 구매 상태를 판정하지 않는다.
- 기존 `FacilityService`는 가게 확장을 구매 당일 반영하고 상품/편의 설비 효과를 다음 영업일에 활성화한다. 기본안은 이 권위를 유지하고 소품도 `IsActive`에 맞춘다. 구매 직후 설치 예정 모습을 보여주는 연출은 별도 정책이다.
- 같은 가게 단계에서 설비만 활성화되어도 갱신해야 한다. 현재 `Apply(stage)`의 동일 단계 조기 반환에 표시 갱신이 묶이지 않도록 분리한다.
- Stage1/2/3에서 각 2/4/6종의 단계별 그림을 쓰되, 미구매/비활성 설비는 숨긴다. 참조 씬에 전부 보인다는 이유로 런타임에서도 전부 표시하지 않는다.

### 상자 열림 전환

- 기존 `SaleSortingPanel.playContainerArrival`의 착지 → FrontWaiting → `playEntryFlow` 전환을 재사용한다.
- 새 손님 도착 시 닫힘, 작업대 진입 시 열림, 다음 방문·취소·비활성화 시 초기화를 기본안으로 잡는다. 새 거래 상태나 입력 방식은 추가하지 않는다.
- 클릭 진입과 자동 진입이 같은 열린 상자 전환을 거치도록 한다. 일시정지·연타·중단에도 이미지가 이전 방문 상태로 남지 않아야 한다.
- 기존 착지 먼지의 이미지 크기/접점 가정을 확인한다. `LandingDustEffect`의 종전 상자 크기 기준이 새 Sprite에도 유효한지 보정한다.
- 뚜껑을 별도 관절로 여는 애니메이션이나 신규 쏟기 연출 제작은 범위 밖이다.

## 3. 예상 변경량과 순서

아래 시간은 기존 구조를 재사용하는 프로그래머 1인의 집중 작업 추정이다. 실측이나 납기 보장이 아니며 사용자 시각 확인 대기·타 작업자의 데이터 연결·병합 충돌 대응은 제외한다.

| 단계 | 산출물 | 추정 |
| --- | --- | ---: |
| 1. 기본 외형 12개·데이터 연결 | Front3 갱신, TopView1 수정/2추가, clock/Resource/Addressables 연결 | 5~8시간 |
| 2. 배치·시간대 색 정합 | 상판 UV 확장, 시계 숫자, 소품 색 경로, 탑뷰 입력/계산기 영역 보존 | 3~5시간 |
| 3. 설비 소품 12개 | 단계별 binding, 실제 활성 상태, 같은 단계 갱신 | 4~6시간 |
| 4. 열린 상자 3개 | 도착/열림/초기화 연결, 먼지 접점 확인 | 2~3시간 |
| 5. 검증·문서 | API/리소스 로드·상태 경계 검증, 로컬 확인 환경, 병합 인계 | 3~5시간 |
| **전체 27개** | 단계별 가게 외형 + 구매 소품 + 상자 상태 연결 | **17~27시간, 대략 2~4작업일** |

먼저 기본 외형 12개와 필수 배치/검증만 완료하는 1차 범위는 약 **10~16시간**으로 예상한다. 소품·열림은 기본 외형 계약 확정 후 진행한다.

파일 규모 예상(정확한 구현 목록은 설계 확정 후):

- 단계별 Prefab 6개(Front3, TopView3) + GameUI/관련 공용 표시 Prefab 약1개.
- runtime/Editor 스크립트 약6~8개: StoreStageVisual/Presentation, GameUIController, SaleSortingPanel, WorldSceneView, LandingDustEffect 및 필요한 기존 setup 경로.
- CSV 2개, Addressables 그룹1개, 기존 테스트2~3개, 관련 문서2~3개. 새 자산의 `.meta` 포함.
- 로컬 검증 씬1개. MainScene 직접 변경은 최종 통합에서 수행한다.

## 4. 검증과 타 작업 의존

- 정적/Editor: 27개 파일과 Sprite/crop, 단계3세트·시계 FK·Addressables load, 중복 GUID/주소·missing reference 확인.
- API: 1→2→3단계 선택, 동일 단계 설비 추가 활성, 구매 당일/다음 영업일, 미구매 숨김, 열린 상자 초기화·중단 및 중복 입력 경계.
- UI/UX: 사용자 확인 대상. 정면/탑뷰, 오전/석양/밤, 상판 가림·시계 숫자·설비 겹침·계산기/분류 영역을 로컬 씬에서 확인하도록 준비한다.
- 새 테스트 프레임워크나 검증용 shell 실행파일을 도입하지 않는다. 기존 Unity Test Runner의 기능/API 테스트를 확장한다.
- **현재 손님 이미지 연결은 미완료:** 53fc9fc에서 구형 원화/Addressables를 제거했지만 손님 CSV 및 딸 4201 참조는 남아 있다. 가게 Prefab/import/상태 API 독립 검증은 먼저 가능하지만 정상 새 게임 전체 흐름 검증은 담당자의 손님/딸 리소스 연결 완료에 의존한다. 이 브랜치가 임의로 해당 데이터를 고치지 않는다.
- 기존 구매 가격·해금·정산·명성·도덕성·손님 생성 규칙, 새 아트 제작, 손님 외형 연결, 프로토타입 렌더러 전체 도입은 제외한다.

## 5. 담당 배분안

- 아키텍처 담당: 단계별 외형/설비 활성 상태 전달 계약, 상자 상태 초기화 경계, 영향 검토와 최종 리뷰.
- 프로그래머: 기본12개 연결을 먼저 응집된 작업으로 구현·검증한 뒤 설비/상자 연결. 불확실한 수명·공용 UI 상태 부분은 6astra-light에 우선 배정한다.
- 구조가 확정된 Prefab 배치·명시적 FK 연결은 sol-light에 맡길 수 있으나 동일 GameUI/StoreStage 파일과 Unity Editor를 동시에 수정하지 않는다.
- 기존 프로그래머 작업에 C#·API 테스트 구현을 배정했다. 설계 담당은 데이터·Prefab·Addressables·Unity 검증을 소유한다. Unity와 동일 파일의 동시 수정은 하지 않는다.

## 6. 구현 계약 및 진행 기록 (2026-09-16)

- `FacilityData` 신규 컬럼 3개는 `uint`, 필수 header, 값 0은 해당 단계의 외형 없음이다. 비0 값은 Resource FK 및 순수 외형 Prefab의 RectTransform/Graphic을 검증한다. 기존 구매 가격·해금·세이브·활성 시점은 바꾸지 않는다.
- 설비 Prefab은 `Assets/Prefabs/StoreStage/Facilities/Stage{N}{FacilityName}Visual.prefab`. 기준 씬의 RectTransform과 이미지 값만 가져오며 프로토타입 gameplay·조명 component는 복사하지 않는다.
- 사용자 예약 정보: Resource 4377까지 다른 작업에서 사용 예정. 이번 신규 ID는 **4378~4391**, 기존 시계4301/4302는 신규 이미지 주소로 연결한다. 병합 시 최신 통합본과 중복을 다시 검사한다.
- 사용자 승인: 기존 Default Local Group에 설비12 + TopView2 + 시계2, 총16개 등록. 새 group/label 없음. 기존 구형 시계 등록/원본은 다른 참조를 위해 보존했다.
- 승인된 자산·GUID·주소·Resource ID는 [등록 목록](../data/STORE_RESOURCE_REGISTRATION.csv)에 보존한다.

| 소비자 | Resource FK |
| --- | --- |
| StoreStage2 / 3 TopView | 4378 / 4379 |
| 12001 식량 선반 (1/2/3단계) | 4380 / 4382 / 4386 |
| 12002 약품장 (1/2/3단계) | 4381 / 4383 / 4387 |
| 12003 공구대 (1/2/3단계) | 0 / 4384 / 4388 |
| 12004 전력·통신 (1/2/3단계) | 0 / 4385 / 4389 |
| 12005 핵보호 (1/2/3단계) | 0 / 0 / 4390 |
| 12006 정밀 전자장비 (1/2/3단계) | 0 / 0 / 4391 |

- 3단계 Front 외형을 최신 참조 씬의 실제 저장값으로 갱신했다. 문서의 반올림 수치나 과거 취소된 PNG 복원 기록은 사용하지 않는다. Stage1 상판 확장면은 0개, Stage2/3은 RawImage 2개다.
- 상자는 기존 `images[5]`가 닫힘 Sprite, `openContainerSprite`가 열림 Sprite다. 기존 버튼·입력 자식은 보존한다.
- 1600×900은 이번 출력 검증 기준이다. GameUI 전체 Canvas 논리 좌표 전환은 별도 `ugui-layout-standard` 범위로 유지한다.
- 자산·CSV·C# 연결 반영 완료. 검증 범위와 제한은 아래를 따른다.

## 7. 검증과 병합 전 확인

- Unity 6000.3.18f1 기존 Editor에서 컴파일 완료. 신규 PlayMode 테스트의 UniTask 확장 using 누락은 수정 후 컴파일 및 실행 성공.
- Editor 자산 검사: 신규 등록16개·단계 Front/TopView6개, missing reference0·프로토타입/Local 경로 의존0. 증거 `Temp/store-asset-verification.json`.
- 관련 EditMode **45/45**: StoreStageDataTests10/10, FacilityTests35/35. 실제 CSV, 단계 FK, 비0 FK 실패 시 이전 공개값 보존, 설비 구매/활성 및 기존 계약 포함.
- 전체 EditMode **293/297**, 실패4·skip0. `Temp/TestResults/store-resource-edit-20260916-b/EditMode.xml` 및 `.log`. 최초 실행283/297에서 설비 테스트10건의 옛 가격/행 치환 의존을 테스트 입력에서 보완했다. 제품 가격을 테스트에 맞춰 바꾸지 않았다.
- 범위 밖 실패4: `BusinessClockAndSortingTests.VacuumAsset_UsesAstraImportContract`, `Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings`(구 프로토타입 청소기 자산 경로), `PriceEventTests.ReproducibleSelectionAndInvalidReferences`(overflow 기대), `WorldSceneTests.EffectsKeepSpriteTimeAndSourceBoundaries`(구 참조 null). 전체 suite PASS로 보고하지 않는다.
- 신규 PlayMode **3/3**, 실패·skip0. `Temp/store-play-tests-a.json`. 실제 ResourceManager를 통한 **Prefab21개·시계Sprite3개**의 Editor Addressables 로드, 실제 자산을 사용하는 Apply1→2→3→1, 동일 단계 활성 재평가·인스턴스 유지, 상자 열림/단계 초기화, 확장면0↔2, raycast 차단 해제, 시간대 색 대상 포함, 파괴 후 늦은 알림·소유 객체 정리를 확인했다.
- 전체 PlayMode·Player 빌드는 미실행. 위 테스트는 전체 게임 진행/UI·UX의 사용자 승인 증거를 대신하지 않는다.
- 1600×900 정적 가게 미리보기: `Temp/store-stage1-1600x900.png`, `store-stage2-1600x900.png`, `store-stage3-1600x900.png`. 설비를 모두 표시한 배치 확인용이며 시간대 효과·손님·입력·시계 숫자의 최종 인게임 확인은 별도다.
- 개인 확인용 `Assets/Scenes/Local/StoreResourceSandbox.unity`를 MainScene에서 복사했고 새 GUID/meta 및 Git 제외를 확인했다. 기존 개인 씬·선택 prefs·Play 시작 씬 설정은 보존했다. 기존 손님/딸 이미지 주소 연결 전에는 새 게임 진입에서 오류가 발생하므로 무단 placeholder로 우회하지 않았다.
- MainScene 및 meta 직접 변경 없음. 공유 단계 Prefab 수정은 MainScene에서도 로드된다. 다른 작업자의 TMP fallback 및 Legacy 상품 meta6개는 이번 commit 범위에서 제외한다.
- 병합 시 CSV3개·FacilityData DTO/FK 검증·표시 코드·단계/설비 Prefab·주소 등록을 함께 반영한다. Resource4378~4391의 중복을 최신 total_merge에서 다시 확인하고, 관련 기능 테스트와 사용자 정면/탑뷰 확인 후 통합한다. 이번 작업의 commit/push는 수행하지 않았다.
