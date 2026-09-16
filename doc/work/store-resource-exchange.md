# 가게 리소스 교체·적용 작업 범위

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
