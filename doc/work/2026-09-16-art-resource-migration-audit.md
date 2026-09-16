# 아트 리소스 이관 사전 대조 — 2026-09-16

## 커밋 인계 — 2026-09-16

사용자가 리소스 정리 결과의 commit-push를 요청했다. 이 체크포인트는 149개 이관·경로 갱신과 구형 외형/Addressables 제거를 묶으며, **게임 데이터·화면 연결 완료 커밋이 아니다.** 후속 작업자는 새 원화의 성별/연령을 기준으로 CustomerAppearanceData/ResourceData/Addressables를 연결하고 딸 이미지 4201의 공유 참조를 확인해야 한다. 기존 데이터·Scene·Prefab을 변경하지 않은 상태를 의도적으로 유지한다.

커밋 대상: 이동/삭제 PNG·metadata, 경로 소비자 5개, 승인된 Addressables 등록 제거, 인계 문서 4개와 이 검토 기록·이동 명세. 사용자의 추가 지시로 기존 `output/` 삭제분 1,339개도 포함한다. 기존 TMP fallback, 상품 Legacy metadata 6개, Temp 백업·검증 스크립트는 제외한다. 검증 결과와 미실행 범위는 아래 실행 기록을 따른다.

## 후속 상태: 기존 손님 외형 제거 완료

2026-09-16 사용자 지시로 기존 손님 외형을 제거했으며, 별도 승인받은 구형 Addressables 등록도 함께 정리했다. 아래 이관 완료 기록의 '기존 45개 유지'는 이 후속 작업 이전 상태다.

- 삭제: `Assets/Textures/Customer/Dystopia/`의 `FemaleCustomer_*` 18개, `MaleCustomer_*` 26개와 `MaleCustomer0` 1개, `NormalMaps/MaleCustomer_18_Normal` 1개. PNG 46개와 각각의 `.meta` 총 92파일.
- 보존: 새 손님 원화 60개·노멀맵 60개, Inspector/InspectorBody/InspectorHead/InspectorNormal. 기존 다른 폴더 사본은 이번 삭제 대상이 아니다.
- Addressables: `Default Local Group`의 삭제 대상 GUID에 해당하는 45개 entry만 제거. 나머지 81개 entry의 주소는 그대로다.
- **각 작업자 후속 연결 필요:** `CustomerAppearanceData` 5001~5045 → `ResourceData` 4201~4245는 삭제된 구형 주소를 가리킨다. `DaughterAppearanceData` 17001~17003도 4201을 공유한다. CSV를 이번에 변경하지 않았으므로 새 리소스 연결 전에는 이 손님·딸 이미지의 로드가 완료된 상태가 아니다.
- 삭제 목록/복구 백업: `Temp/LegacyCustomerRemoval.json`, `Temp/LegacyCustomerRemoval-before.zip`. 데이터·Prefab·Scene·신규 외형/감독관 관련 보존 파일 403개 해시 동일, 대상 46개와 `.meta` 잔존 0, 구형 Addressables entry 잔존 0 확인.
- Unity InitScene 유지, scene dirty=false, 제품 Console Error 0. 게임 데이터 연결을 의도적으로 후속 작업에 남겼으므로 게임 플레이 검증은 수행하지 않았다. Commit/push 없음.

## 현재 상태: 149개 이관·경로 정리 완료

2026-09-16 사용자 후속 지시로 **149개 이관과 경로 정리까지만** 수행했다. 아래 사전 대조는 이관 전의 기록이다. 당시 보류로 분류한 Stage2 구형 상자 2개도 이번 명시적 149개 범위에 포함해 이동했으며, 운영 화면에 새로 연결하지 않았다.

| 현재 위치 (`Assets/Textures/` 기준) | 이번에 이동한 PNG 수 |
| --- | ---: |
| `Customer/Dystopia/` | 60 |
| `Customer/Dystopia/NormalMaps/` | 60 |
| `Environment/Dystopia/` | 16 |
| `Environment/Dystopia/TopDown/` | 3 |
| `UI/Dystopia/` | 10 |

- [149개 원본→목적지·GUID·SHA-256 목록](../data/ART_RESOURCE_MOVE_MAP.json)을 기준으로 기존 Cashier Editor의 `AssetDatabase.MoveAsset`을 사용했다. 대상 파일 덮어쓰기 없이 PNG와 `.meta`를 함께 이동했다.
- 원본 백업: `Temp/ArtMove-20260916-before.zip`. 원래 `art` 하위 PNG는 0개이며 폴더 metadata는 삭제하지 않았다.
- 경로 소비자 5개 수정: `DystopiaCustomerTools`, `DystopiaFacilityTools`, `DystopiaTextureFiltering`, `DystopiaScreen`, `DystopiaTopDownTest`. 뒤의 두 클래스는 `UNITY_EDITOR` 이미지 복구 문자열 4곳만 변경했다.
- 필터 도구는 이전 art 대상 143개만 명시적으로 선택한다. 커진 목적 폴더의 기존 손님·배경·시계·작업대를 추가로 순회하지 않는다. 기존 Checkout 폴더 필터 동작은 유지했다.
- ART_HANDOFF / ART_IMAGE_INVENTORY / STAGE_LAYOUT_HANDOFF / CUSTOMER_ARTWORK_GUIDE의 현재 경로를 갱신했다. 인벤토리에 누락된 Stage3GunmetalClock을 추가해 149개로 맞췄다.
- **게임 데이터·Addressables·Prefab·Scene의 이미지 연결은 다음 작업이다.** 기존 손님 후보 45개, 딸 리소스 4201, 가게 단계 외형 연결, 노멀맵 활성화 및 구매 표시 동작은 변경하지 않았다.

검증 결과(이번 실행):

- 파일 정적 검사: 149개 PNG + 149개 metadata, 총 **298개 해시 동일**, GUID 149개 보존·중복 없음. 기존 데이터·Prefab·Scene·Addressables·이미지·보호 변경 **735개 파일 해시 동일**. `Temp/art_move_static_verification.json`.
- Unity import/로드 검사: **Texture2D 149/149**, Sprite 97개, RGB 노멀맵 60개, 오류 0. `Temp/art_move_unity_verification.json`.
- 실제 Editor 메서드의 읽기 전용 열거 검사: 필터 대상 **143개**, 손님 대상 **60개**, 기대 목록과 정확히 일치. 필터 적용이나 Scene 등록 메뉴는 실행하지 않았다. `Temp/art_move_path_verification.json`.
- 인벤토리 경로·크기·GUID **149/149**, 문서 로컬 링크 **69/69** 확인. 검증 스크립트 `Temp/verify_art_inventory.py`.
- C# refresh/컴파일 완료, `scriptCompilationFailed=false`, 제품 Console Error 0. InitScene 유지, Play=false, scene dirty=false.
- `git diff --check` 공백 오류 없음. Unity Test Runner·PlayMode·최종 화면 검증은 이번 이관 범위에서 미실행. 실행한 것은 파일/import/경로 메서드 검증이다.
- Git stage/commit/push 없음. 기존 TMP fallback, 제품 Legacy metadata 6개, output 삭제와 stash를 보존했다.

## 이하: 이관 전 사전 대조 기록

- 기준: Cashier `total_merge`, HEAD `96bebba`와 현재 작업 폴더. 이동·교체·CSV/Addressables·Scene 수정·Git 작업은 수행하지 않았다.
- 사용자 요청: 아트 결과물을 실제 사용 폴더로 옮기기 전에 충돌·갱신·신규 항목을 분류한다.
- 기준 문서: `Assets/Textures/ART_HANDOFF.md`, `ART_IMAGE_INVENTORY.md`, `STAGE_LAYOUT_HANDOFF.md`. 손님 세부 규격은 `doc/CUSTOMER_ARTWORK_GUIDE.md`도 대조했다.
- 아래 대상 경로는 **제안**이다. 기존 연결 후보는 metadata의 옛 Sprite 이름과 현재 CSV를 대조한 결과이며, 모든 원화의 시각적 동일성을 확정한 것은 아니다.

## 1. 수량과 분류

| 대상 | 실제 PNG | 판정 |
| --- | ---: | --- |
| 손님 원화 | 60 | 기존 44종의 갱신 후보 + 기존 대응을 찾지 못한 신규 후보 16종 |
| 손님 노멀맵 | 60 | 기존 `MaleCustomer_18_Normal` 갱신 후보 1개 + 신규 후보 59개. 60개 모두 운영 손님에 연결되지 않음 |
| 계산대 상판 | 3 | 1·2·3단계 기존 표시 슬롯 갱신 |
| 상자 | 8 | 단계별 닫힘 3개 갱신, 열림 3개 신규 상태, Stage2 구형 2개 보류 |
| 프레임·시계 | 3 | Stage2 프레임, Stage2/3 시계 갱신 |
| 설비 소품 | 12 | 기존 구매 기능에 붙일 신규 시각 리소스 |
| 탑뷰 작업대 | 3 | Stage1 갱신, Stage2/3 단계별 표현 신규 |
| **합계** | **149** | 자산 역할 기준 갱신 후보 55 / 신규 후보 92 / 구형 보류 2. 구현 규모나 자동 덮어쓰기 허가를 뜻하지 않음 |

- `ART_IMAGE_INVENTORY.md`에는 148개가 기재되어 있고 **`Facility/Frame/Stage3GunmetalClock.png` 1개가 누락**됐다. 기재된 148개의 경로·원본 크기·GUID는 현재 파일과 일치한다.
- 149개 모두 `.meta`가 있고 GUID가 고유하다. Assets 내 다른 `.meta`와 GUID 중복은 발견되지 않았다.
- `Assets/Textures/art`와 그 밖의 `Assets/Textures` PNG를 비교했을 때 동일 파일명·동일 GUID·동일 SHA-256 파일은 각각 0개다. 픽셀을 디코딩한 시각적 동일성 검사는 아니다.
- 현재 art GUID의 Addressables 직접 등록은 0개다. 직렬화 참조는 프로토타입 참조 씬/작업 씬과 `_Recovery/0.unity`에서만 확인됐다. 복구 씬의 참조를 제품 연결 완료로 세지 않았다.
- 원화 60개와 설비 27개는 프로토타입에서 참조된다. 노멀맵 60개와 Stage2 구형 상자 2개는 조사한 직렬화 GUID 참조가 없다. 미참조가 삭제 승인을 뜻하지는 않는다.

## 2. 실제 사용 폴더와 연결

| 역할 | 운영 경로/연결 | 잘못 갱신하기 쉬운 별도 사본 |
| --- | --- | --- |
| 손님 | `CustomerAppearanceData.image_resource_idx` → `ResourceData` 4201~4245 → `Assets/Textures/Customer/Dystopia/` | 프로토타입의 원화 배열과 별개 |
| 프레임·상판 | `Assets/Prefabs/StoreStage/StoreStage{1,2,3}Front.prefab` → `Assets/Textures/Environment/Dystopia/` | `Assets/Textures/Checkout/Shop/` |
| 앞쪽 상자 | 같은 Front 프리팹 → `Assets/Textures/UI/Dystopia/Stage*Container*.png` | `Checkout/Shop/` 동명 사본 |
| 시계 | `StoreStageData.clock_resource_idx` → 4300/4301/4302 → `Assets/Textures/UI/Dystopia/Stage{1,2,3}Clock.png` | `Checkout/Shop/Stage1BasicClock`, `Stage2Clock`, `Stage3Clock` |
| 탑뷰 작업대 | 모든 단계의 `top_view_prefab_resource_idx=4294` → `StoreStage1TopView` → `Assets/Textures/Environment/Dystopia/TopDown/TopDownWorkbench.png` | `Checkout/Workbench/TopDownWorkbench.png` |

`Checkout` 사본을 수정하는 것만으로 위 운영 참조가 바뀌지 않는다. 실제 프리팹 GUID와 Addressables 주소를 기준으로 교체해야 한다. Stage1 시계는 이번 art 폴더에 교체본이 없으므로 유지한다.

## 3. 충돌·별도 연결이 필요한 항목

1. **옛 파일명과 새 분류의 연령 충돌 8건:** 아래 손님 표의 `연령 재검토` 행. 새 그림은 성인 분류인데 기존 appearance 행은 아이/노인이다. 파일만 바꾸면 아이/노인으로 생성된 손님에게 성인 그림이 표시될 수 있다. 이는 옛 Sprite 이름으로 찾은 연결 후보를 채택할 때의 충돌이다.
2. **45개 운영 외형과 60개 아트의 대응 차이:** 44개는 옛 Sprite 이름으로 후보를 찾았고, 기존 `MaleCustomer0`(appearance 5045 / resource 4245)은 새 아트 대응이 없다. 새 16개를 모두 반영할지와 5045 유지/대체를 정해야 한다. 기존 ID를 자동 재번호화하지 않는다.
3. **외형 분류와 거래 성향은 다른 계약:** 새 파일명의 Hasty/Wealthy/Poor 등은 아트 분류다. 현재 `CustomerAppearanceData`는 gender/age로 연결한다. 그림 이관만으로 거래 성향이나 생성 확률을 변경하면 안 된다. 성향별 그림 고정이 필요하면 별도 명세·데이터 변경이다.
4. **손님 노멀맵 60개는 배치만으로 적용되지 않음:** 현재 `CustomerWorldQueueView`는 `WorldSprite` 재질과 SpriteRenderer를 사용하며 외형별 노멀맵 전달 경로가 없다. 프로토타입의 `DystopiaPixelStage.Layer.normalVariants`는 별도 기능이고 인계 문서도 자동 활성화를 금지한다. 원화 이관과 조명 활성화를 분리한다.
5. **노멀맵 importer 유지:** 이 60개는 Texture Type Default(0), sRGB Off(0)인 RGB 텍스처다. Unity Normal Map 타입으로 일괄 변경하지 않는다. 루트 maxTextureSize와 플랫폼별 설정을 함께 보존한다.
6. **Stage2 구형 상자 우선순위:** `Stage2CrateClosed/Open`은 보존본이며 최신 선택은 `Stage2RustedCrateClosed/Open`. 두 쌍을 동시에 최신 슬롯에 연결하지 않는다.
7. **Stage3 수정 이력 우선순위:** 08:07 복원 기록 다음의 08:10 재승인 건메탈 상태를 사용한다. 열린 상자는 후속 거친 픽셀 수정본, 시계는 08:23 `Stage3GunmetalClock`이 최신이다. 08:07 배치·균일 scale·그림자 해제 조건은 후속 명세대로 유지한다.
8. **여러 단계가 공유하는 atlas:** 기존 `Stage2Shop`의 하부장 등은 다른 단계 Front에서도 사용된다. Stage2RustedFrame은 기존과 같은 이름/ID의 Sprite 9개를 가진다. atlas 전체 덮어쓰기는 다른 단계까지 바꾸므로 해당 단계 슬롯을 새 GUID로 연결하는 편이 영향 범위가 작다.
9. **Stage3Shop 통째로 교체 금지:** 새 `Stage3CounterTop`은 상판만 교체한다. Stage3Shop의 천장·기둥·전등은 별도 유지한다. Stage3 프레임을 Stage2RustedFrame으로 대체한 이전 시도는 인계서에서 취소됐다.
10. **PNG 크기·crop·pivot 차이:** 상판·상자·시계가 기존 시트/저해상도 이미지와 다른 크기다. 예: Stage3CounterTop 1446×1087, Stage3GunmetalClock 1254×1254. 기존 metadata를 무조건 유지하거나 새 metadata로 통째로 덮어쓰는 방식 모두 위험하다. GUID/fileID 연결과 새 crop을 함께 검토한다.
11. **열림 상자·설비 구매 표시 연결 부족:** `StoreStageVisual`의 Front는 고정 8개 슬롯이며 별도의 상자 열림 슬롯/설비 12종 표시 계약은 없다. 기존 구매 시스템과 아트의 표시 시점을 연결해야 한다. 12개 이미지는 새로운 구매 상품 12개를 뜻하지 않는다.
12. **Stage2/3 탑뷰는 현재 같은 프리팹:** 신규 작업대 이미지만 옮겨서는 단계별로 바뀌지 않는다. 단계별 TopView 참조 또는 기존 표현 경로 보완이 필요하다. 변경되는 종횡비에 맞춰 상품 분류/물리 영역도 실제 화면에서 확인해야 한다.
13. **배치·그림자·UV는 PNG가 아님:** 참조 씬의 계산대 좌우 확장 RawImage, UV, 시계 숫자, 소품 위치, 그림자 설정을 별도로 반영해야 한다. Stage3 최종 시계 위치는 참조 좌표 `(1055, 6)`이며 현재 Canvas/좌표계에 맞춰 이전해야 한다. Awake/Start/OnValidate에서 배치를 초기화하지 않는다는 인계 조건을 유지한다.
14. **이동 후 문자열 경로:** GUID를 보존해도 `DystopiaCustomerTools`, `DystopiaFacilityTools`, `DystopiaTextureFiltering`에는 `Assets/Textures/art/...`가 하드코딩되어 있다. 이동할 경우 이 도구와 인계 문서의 경로도 같이 갱신해야 한다.

## 4. 설비 29개 개별 목록

원본 접두 경로는 `Assets/Textures/art/Facility/`이다. 이관 대상은 기존 운영 폴더 안에 원본 이름을 유지하는 제안이며, 같은 역할의 옛 이미지를 즉시 삭제한다는 뜻이 아니다.

| 원본 상대 경로 | 분류 | 현재 대응 / 작업 내용 | 제안 대상 폴더 |
| --- | --- | --- | --- |
| `CounterTop/Stage1CounterTop.png` | 갱신 | Stage1WoodCounter → 상판 슬롯. 원본 시트 전체 교체 금지 | `Assets/Textures/Environment/Dystopia/` |
| `CounterTop/Stage2CounterTop.png` | 갱신 | Stage2Table → 상판 슬롯. 원본 시트 전체 교체 금지 | `Assets/Textures/Environment/Dystopia/` |
| `CounterTop/Stage3CounterTop.png` | 갱신 | Stage3Shop의 Counter Sprite → 상판 슬롯. 원본 시트 전체 교체 금지 | `Assets/Textures/Environment/Dystopia/` |
| `Crate/Stage1CrateClosed.png` | 갱신 | Stage1WoodContainerCompact의 닫힘 외형 후보 | `Assets/Textures/UI/Dystopia/` |
| `Crate/Stage1CrateOpen.png` | 신규 상태 | 같은 단계 닫힘 이미지와 쌍. 열림 전환 연결 필요 | `Assets/Textures/UI/Dystopia/` |
| `Crate/Stage2CrateClosed.png` | 보류 | 구형 보존본. Rusted 쌍 우선, 삭제하지 않음 | `Assets/Textures/이관 제외 후보` |
| `Crate/Stage2CrateOpen.png` | 보류 | 구형 보존본. Rusted 쌍 우선, 삭제하지 않음 | `Assets/Textures/이관 제외 후보` |
| `Crate/Stage2RustedCrateClosed.png` | 갱신 | Stage2Container의 닫힘 외형 후보 | `Assets/Textures/UI/Dystopia/` |
| `Crate/Stage2RustedCrateOpen.png` | 신규 상태 | 같은 단계 닫힘 이미지와 쌍. 열림 전환 연결 필요 | `Assets/Textures/UI/Dystopia/` |
| `Crate/Stage3CrateClosed.png` | 갱신 | Stage3Container의 닫힘 외형 후보 | `Assets/Textures/UI/Dystopia/` |
| `Crate/Stage3CrateOpen.png` | 신규 상태 | 같은 단계 닫힘 이미지와 쌍. 열림 전환 연결 필요 | `Assets/Textures/UI/Dystopia/` |
| `Frame/Stage2RustedClock.png` | 갱신 | Stage2Clock (resource 4301) 및 숫자 배치/crop 갱신 | `Assets/Textures/UI/Dystopia/` |
| `Frame/Stage2RustedFrame.png` | 갱신 | Stage2Shop 대응 Sprite 9개. 다른 단계의 atlas 공유 참조 주의 | `Assets/Textures/Environment/Dystopia/` |
| `Frame/Stage3GunmetalClock.png` | 갱신 | Stage3Clock (resource 4302) 및 숫자 배치/crop 갱신 | `Assets/Textures/UI/Dystopia/` |
| `Props/Stage1FoodShelf.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage1MedicineCabinet.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage2FoodShelf.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage2MedicineCabinet.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage2PowerCommunications.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage2ToolBench.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage3FoodShelf.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage3MedicineCabinet.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage3NuclearProtection.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage3PowerCommunications.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage3PrecisionElectronics.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Props/Stage3ToolBench.png` | 신규 표시 | 설비 구매 상태·가게 단계별 노출, 참조 씬 배치 연결 필요 | `Assets/Textures/Environment/Dystopia/` |
| `Workbench/Stage1TopDownWorkbench.png` | 갱신 | TopDownWorkbench 교체 | `Assets/Textures/Environment/Dystopia/TopDown/` |
| `Workbench/Stage2TopDownWorkbench.png` | 신규 단계 표현 | 단계별 TopView 연결 필요; 현재 4294 공용 | `Assets/Textures/Environment/Dystopia/TopDown/` |
| `Workbench/Stage3TopDownWorkbench.png` | 신규 단계 표현 | 단계별 TopView 연결 필요; 현재 4294 공용 | `Assets/Textures/Environment/Dystopia/TopDown/` |

## 5. 손님 원화 60개 + 대응 노멀맵 60개

- 원본 원화: `Assets/Textures/art/Customer/Female/` 또는 `Male/`.
- 원본 노멀맵: `Assets/Textures/art/Customer/NormalMap/`.
- 제안 대상: 원화 `Assets/Textures/Customer/Dystopia/`, 노멀맵 `Assets/Textures/Customer/Dystopia/NormalMaps/`. 새 파일명 유지.
- 표의 기존 후보는 `Assets/Textures/Customer/Dystopia/` 안의 PNG이다. **metadata에 남은 옛 Sprite 이름 기준의 후보**이며 단순 덮어쓰기 확정표가 아니다.
- 노멀맵은 모두 현재 연결 미구현. `MaleNormal_06_Normal.png`만 기존 `NormalMaps/MaleCustomer_18_Normal.png` 대응 후보가 있고, 나머지 59개는 신규 후보다.
- CSV 연령 값: 아이 4 / 노인 8 / 성인 16. 새 원화의 기대 연령은 인계 아트 분류 기준이다.

| 새 원화 | 대응 노멀맵 | 기존 연결 후보 (appearance/resource) | 판정·주의 |
| --- | --- | --- | --- |
| `FemaleChild_01.png` | `FemaleChild_01_Normal.png` | 대응 없음 | 신규 후보 / 아이 |
| `FemaleChild_02.png` | `FemaleChild_02_Normal.png` | 대응 없음 | 신규 후보 / 아이 |
| `FemaleChild_03.png` | `FemaleChild_03_Normal.png` | 대응 없음 | 신규 후보 / 아이 |
| `FemaleElder_01.png` | `FemaleElder_01_Normal.png` | 대응 없음 | 신규 후보 / 노인 |
| `FemaleElder_02.png` | `FemaleElder_02_Normal.png` | 대응 없음 | 신규 후보 / 노인 |
| `FemaleElder_03.png` | `FemaleElder_03_Normal.png` | 대응 없음 | 신규 후보 / 노인 |
| `FemaleHasty_01.png` | `FemaleHasty_01_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `FemaleHasty_02.png` | `FemaleHasty_02_Normal.png` | `FemaleCustomer_08.png` (5007/4207) | 갱신 후보 / 성인 |
| `FemaleHasty_03.png` | `FemaleHasty_03_Normal.png` | `FemaleCustomer_09.png` (5008/4208) | 갱신 후보 / 성인 |
| `FemaleNormal_01.png` | `FemaleNormal_01_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `FemaleNormal_02.png` | `FemaleNormal_02_Normal.png` | `FemaleCustomer_14.png` (5012/4212) | 갱신 후보 / 성인 |
| `FemaleNormal_03.png` | `FemaleNormal_03_Normal.png` | `FemaleCustomer_15.png` (5013/4213) | 갱신 후보 / 성인 |
| `FemaleNormal_04.png` | `FemaleNormal_04_Normal.png` | `FemaleCustomer_16.png` (5014/4214) | 갱신 후보 / **연령 재검토: 노인 → 성인** |
| `FemaleNormal_05.png` | `FemaleNormal_05_Normal.png` | `FemaleCustomer_17.png` (5015/4215) | 갱신 후보 / **연령 재검토: 아이 → 성인** |
| `FemaleNormal_06.png` | `FemaleNormal_06_Normal.png` | `FemaleCustomer_18.png` (5016/4216) | 갱신 후보 / **연령 재검토: 아이 → 성인** |
| `FemaleNormal_07.png` | `FemaleNormal_07_Normal.png` | `FemaleCustomer_19.png` (5017/4217) | 갱신 후보 / **연령 재검토: 노인 → 성인** |
| `FemaleNormal_08.png` | `FemaleNormal_08_Normal.png` | `FemaleCustomer_20.png` (5018/4218) | 갱신 후보 / **연령 재검토: 노인 → 성인** |
| `FemaleNormal_09.png` | `FemaleNormal_09_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `FemaleNormal_10.png` | `FemaleNormal_10_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `FemaleNormal_11.png` | `FemaleNormal_11_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `FemaleNormal_12.png` | `FemaleNormal_12_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `FemalePoor_01.png` | `FemalePoor_01_Normal.png` | `FemaleCustomer_04.png` (5004/4204) | 갱신 후보 / 성인 |
| `FemalePoor_02.png` | `FemalePoor_02_Normal.png` | `FemaleCustomer_05.png` (5005/4205) | 갱신 후보 / 성인 |
| `FemalePoor_03.png` | `FemalePoor_03_Normal.png` | `FemaleCustomer_06.png` (5006/4206) | 갱신 후보 / 성인 |
| `FemalePriceSensitive_01.png` | `FemalePriceSensitive_01_Normal.png` | `FemaleCustomer_01.png` (5001/4201) | 갱신 후보 / 성인 |
| `FemalePriceSensitive_02.png` | `FemalePriceSensitive_02_Normal.png` | `FemaleCustomer_02.png` (5002/4202) | 갱신 후보 / **연령 재검토: 노인 → 성인** |
| `FemalePriceSensitive_03.png` | `FemalePriceSensitive_03_Normal.png` | `FemaleCustomer_03.png` (5003/4203) | 갱신 후보 / 성인 |
| `FemaleWealthy_01.png` | `FemaleWealthy_01_Normal.png` | `FemaleCustomer_10.png` (5009/4209) | 갱신 후보 / 성인 |
| `FemaleWealthy_02.png` | `FemaleWealthy_02_Normal.png` | `FemaleCustomer_11.png` (5010/4210) | 갱신 후보 / **연령 재검토: 노인 → 성인** |
| `FemaleWealthy_03.png` | `FemaleWealthy_03_Normal.png` | `FemaleCustomer_12.png` (5011/4211) | 갱신 후보 / 성인 |
| `MaleChild_01.png` | `MaleChild_01_Normal.png` | `MaleCustomer_25.png` (5041/4241) | 갱신 후보 / 아이 |
| `MaleChild_02.png` | `MaleChild_02_Normal.png` | `MaleCustomer_26.png` (5042/4242) | 갱신 후보 / 아이 |
| `MaleChild_03.png` | `MaleChild_03_Normal.png` | 대응 없음 | 신규 후보 / 아이 |
| `MaleElder_01.png` | `MaleElder_01_Normal.png` | `MaleCustomer_28.png` (5043/4243) | 갱신 후보 / 노인 |
| `MaleElder_02.png` | `MaleElder_02_Normal.png` | `MaleCustomer_29.png` (5044/4244) | 갱신 후보 / 노인 |
| `MaleElder_03.png` | `MaleElder_03_Normal.png` | 대응 없음 | 신규 후보 / 노인 |
| `MaleHasty_01.png` | `MaleHasty_01_Normal.png` | `MaleCustomer_07.png` (5025/4225) | 갱신 후보 / 성인 |
| `MaleHasty_02.png` | `MaleHasty_02_Normal.png` | `MaleCustomer_08.png` (5026/4226) | 갱신 후보 / 성인 |
| `MaleHasty_03.png` | `MaleHasty_03_Normal.png` | `MaleCustomer_09.png` (5027/4227) | 갱신 후보 / 성인 |
| `MaleNormal_01.png` | `MaleNormal_01_Normal.png` | `MaleCustomer_13.png` (5030/4230) | 갱신 후보 / 성인 |
| `MaleNormal_02.png` | `MaleNormal_02_Normal.png` | `MaleCustomer_14.png` (5031/4231) | 갱신 후보 / 성인 |
| `MaleNormal_03.png` | `MaleNormal_03_Normal.png` | `MaleCustomer_15.png` (5032/4232) | 갱신 후보 / 성인 |
| `MaleNormal_04.png` | `MaleNormal_04_Normal.png` | `MaleCustomer_16.png` (5033/4233) | 갱신 후보 / 성인 |
| `MaleNormal_05.png` | `MaleNormal_05_Normal.png` | `MaleCustomer_17.png` (5034/4234) | 갱신 후보 / 성인 |
| `MaleNormal_06.png` | `MaleNormal_06_Normal.png` | `MaleCustomer_18.png` (5035/4235) | 갱신 후보 / 성인 |
| `MaleNormal_07.png` | `MaleNormal_07_Normal.png` | `MaleCustomer_19.png` (5036/4236) | 갱신 후보 / 성인 |
| `MaleNormal_08.png` | `MaleNormal_08_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `MaleNormal_09.png` | `MaleNormal_09_Normal.png` | `MaleCustomer_21.png` (5037/4237) | 갱신 후보 / 성인 |
| `MaleNormal_10.png` | `MaleNormal_10_Normal.png` | `MaleCustomer_22.png` (5038/4238) | 갱신 후보 / 성인 |
| `MaleNormal_11.png` | `MaleNormal_11_Normal.png` | `MaleCustomer_23.png` (5039/4239) | 갱신 후보 / 성인 |
| `MaleNormal_12.png` | `MaleNormal_12_Normal.png` | `MaleCustomer_24.png` (5040/4240) | 갱신 후보 / **연령 재검토: 노인 → 성인** |
| `MalePoor_01.png` | `MalePoor_01_Normal.png` | `MaleCustomer_04.png` (5022/4222) | 갱신 후보 / 성인 |
| `MalePoor_02.png` | `MalePoor_02_Normal.png` | `MaleCustomer_05.png` (5023/4223) | 갱신 후보 / 성인 |
| `MalePoor_03.png` | `MalePoor_03_Normal.png` | `MaleCustomer_06.png` (5024/4224) | 갱신 후보 / 성인 |
| `MalePriceSensitive_01.png` | `MalePriceSensitive_01_Normal.png` | `MaleCustomer_01.png` (5019/4219) | 갱신 후보 / 성인 |
| `MalePriceSensitive_02.png` | `MalePriceSensitive_02_Normal.png` | `MaleCustomer_02.png` (5020/4220) | 갱신 후보 / 성인 |
| `MalePriceSensitive_03.png` | `MalePriceSensitive_03_Normal.png` | `MaleCustomer_03.png` (5021/4221) | 갱신 후보 / 성인 |
| `MaleWealthy_01.png` | `MaleWealthy_01_Normal.png` | `MaleCustomer_10.png` (5028/4228) | 갱신 후보 / 성인 |
| `MaleWealthy_02.png` | `MaleWealthy_02_Normal.png` | 대응 없음 | 신규 후보 / 성인 |
| `MaleWealthy_03.png` | `MaleWealthy_03_Normal.png` | `MaleCustomer_12.png` (5029/4229) | 갱신 후보 / 성인 |

기존 `MaleCustomer0.png`(appearance 5045/resource 4245)는 새 60종에서 대응을 찾지 못했다. 자동 제거하거나 다른 새 외형을 임의로 배정하지 않는다.

## 6. 이관 방식 제안과 검증 범위

1. 최신 147개와 구형 보류 2개를 분리한다. 우선 원화·시설 외형만 연결하고 노멀맵 조명 활성화는 별도 범위로 둔다.
2. 기본은 **새 PNG와 원본 `.meta`를 함께 이동하고 운영 소비자의 참조를 명시적으로 갱신**한다. 프로토타입의 기존 GUID 참조를 유지하면서 실제 사용하는 파일을 하나로 모을 수 있다. 이동 전 source→target 허용 목록을 확정한다.
3. 기존 파일을 덮어써야 하는 경우에는 기존 GUID/fileID 보존과 새 crop/import 설정의 호환을 개별 검증한다. 특히 공유 atlas 전체 덮어쓰기를 피한다. 옛 자산은 참조 0 검증 및 별도 정리 승인 전까지 유지한다.
4. 손님 appearance/resource 매핑, 연령 충돌 8건, 기존 5045, 신규 16개 정책을 먼저 확정한다. 설비는 닫힘/열림/구매 표시와 단계별 TopView 연결을 구분한다.
5. Addressables 변경 목록은 실제 선택한 로딩 방식에 맞춰 별도로 산정한다. 프리팹의 직접 의존 이미지까지 149개 전부를 Addressables에 개별 등록할 필요는 없다. 현재 요청은 등록·이동 승인이 아니다.
6. 참조 씬은 배치의 근거다. DystopiaPrototype의 게임 로직/초기화 코드를 통째로 도입하지 않는다. 소스 하드코딩 경로·인계 목록도 이동 결과와 맞춘다.

이번 검토는 파일/metadata/CSV/프리팹/소스의 **정적 대조**다. PNG 해시·크기·GUID·149개 metadata 및 목록 누락을 확인했으며, Unity import·컴파일·PlayMode·최종 화면 검증은 수행하지 않았다. SHA 비교는 픽셀 비교가 아니다. 인계 문서의 과거 Editor 검증을 이번 통합 검증 결과로 대신하지 않는다.

자산 출처는 ART_HANDOFF의 기존 프로젝트 제공 이미지 및 일부 image_gen 편집 기록을 따른다. 이 문서는 별도의 신규 라이선스 확인을 주장하지 않는다.

기존 TMP fallback, Products/Legacy metadata, output 정리 변경을 보존했다. 생성한 것은 이 대조 문서와 `Temp/art_audit.py`, `Temp/art_audit.json`, `Temp/write_art_report.py`뿐이다. Commit/push 및 자산 이동/삭제 없음.
