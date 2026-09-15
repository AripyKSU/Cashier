# 2026-09-14 프로토타입 변경·추가사항

## 범위와 실행 환경

- 대상: `Assets/DystopiaPrototype/`, 브랜치 `astra-prototype`.
- Unity 6000.3.18f1 / URP 2D. 기존 Packages·ProjectSettings 변경 없음.
- 이번 문서는 이전 커밋 이후 누적된 이번 작업분을 설명한다. 에셋 추가, 화면 표현 변경, 상품 카탈로그 변경을 함께 포함한다.
- 일반 실행 씬: `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity`.
- 작업 중 열린 씬은 자동 저장·재로드하지 않았다. **일반 씬 파일 자체는 이번 변경에 포함되지 않는다.** 아래 메뉴로 설정을 적용해야 한다.

## 추가: 생존 아이템 16종

PNG와 Unity 메타데이터 위치: `Assets/DystopiaPrototype/Art/Products/`.

| 상품 | 파일명 | 기본 가격 | 필요 설비 | 최소 단계 |
|---|---|---:|---|---:|
| 생수 | DrinkingWater.png | 1,000 | 없음 | 0 |
| 통조림 | CannedFood.png | 2,500 | 없음 | 0 |
| 붕대 | MedicalBandage.png | 3,000 | 없음 | 0 |
| 건전지 | DryBattery.png | 3,500 | 없음 | 0 |
| 군용식량 | MilitaryRation.png | 미설정 | 식량 보관 선반 | 1 |
| 영양바 | NutritionBar.png | 미설정 | 식량 보관 선반 | 1 |
| 약통 | Medicine.png | 미설정 | 약품 보관장 | 1 |
| 응급 주사 | EmergencyInjection.png | 미설정 | 약품 보관장 | 1 |
| 손전등 | Flashlight.png | 미설정 | 공구대 | 2 |
| 접이식 삽 | FoldingShovel.png | 미설정 | 공구대 | 2 |
| 무전기 | Radio.png | 미설정 | 전력·통신 장비 | 2 |
| 배터리 | PowerBattery.png | 미설정 | 전력·통신 장비 | 2 |
| 방독면 | GasMask.png | 미설정 | 핵보호 물품 설비 | 3 |
| 방호복 | ProtectiveSuit.png | 미설정 | 핵보호 물품 설비 | 3 |
| 방사능 측정기 | RadiationDetector.png | 미설정 | 정밀 전자장비 보관장 | 3 |
| 열화상 카메라 | ThermalCamera.png | 미설정 | 정밀 전자장비 보관장 | 3 |

`DystopiaSession.cs`에 고정 `DystopiaProductId`, 설비 비트 플래그 `DystopiaFacility`, `shopStage`, `ownedFacilities`를 추가했다. 상품 배열 인덱스 대신 ID로 일일 규칙을 판정한다. 단계와 설비 보유 조건을 모두 만족하며 가격이 양수이고 Sprite가 있는 상품만 영업일의 판매 목록에 포함한다. 가격 0은 무료 판매가 아니라 **미설정/주문 제외**다. 중복 ID·누락 ID·판매 가능 상품 없음은 오류로 알린다. 기존 건빵/즉석밥 규칙은 영양바/군용식량 ID에 대응한다.

### 기존 씬에서 등록

1. Play를 종료한 상태에서 작업 씬을 연다.
2. `Dystopia > Register 16 Survival Products`를 실행한다. 기존 상품의 가격 등 보존 가능한 값은 연결하면서 카탈로그를 등록한다. 기존 10종 배열을 그대로 실행하면 ID 검증에 실패할 수 있다.
3. 필요한 신규 상품 가격과 `ownedFacilities`를 설정한다. Stage 적용 메뉴만 누른다고 설비를 구입한 것으로 처리되지는 않는다.
4. `Validate Survival Product Colliders`, `Validate Survival Product Unlocks`는 확인용 Editor 메뉴다.

## 추가·정리: Stage 1 / 2 / 3 외형과 적용 기준

현재 권위 기준은 `Assets/DystopiaPrototype/Editor/References/Stage1Reference.unity`, `Stage2Reference.unity`, `Stage3Reference.unity`다. **직접 플레이할 씬이 아니라 적용 메뉴가 읽는 기준 사본**이다. 각 `.meta`를 함께 보존해야 한다.

| 단계 | 외형 | 상자·시계 | 배경 |
|---|---|---|---|
| 1 | 낡은 목재 가판·천막 | 작은 목재 상자, 낮은 오른쪽 시계 | 원래 100% 크기 |
| 2 | 철제 프레임·상판 | Stage2Container, Stage2Clock | 92% 축소 배치 |
| 3 | 보강 철제 프레임·전등·하단 설비 | 새 Stage3Container, 기존 시계 외형 | 철제 가판에 맞춘 92% 배치 |

`Dystopia > Apply Stage 1 Shop`, `Apply Stage 2 Shop`, `Apply Stage 3 Shop`은 `DystopiaTools.ApplyApprovedStageReference`로 연결했다. 이전 메뉴의 고정값 때문에 다른 단계의 크기·숫자 위치가 남던 문제를 수정했다. 프레임, 상자, 시계 몸체/숫자, 배경, 전등 관련 설정을 참조에서 읽고 현재 대상에 적용한다. 단계 변경은 **이 메뉴를 명시적으로 실행할 때만** 일어난다. Play/OnEnable에서 배치를 자동 초기화하지 않는다.

- Stage 3에 남아 하단을 덮던 `Stage2Cabinet` 표시를 끈다. 별도 하부장 표시 상태도 단계 적용 시 확인한다.
- (2026-09-15 변경) Stage 3 배경도 이제 Stage3Reference 자체 값을 쓴다. Stage 2 기준에서 배경을 가져오던 override는 제거했다.
- 적용 전후 사본은 로컬 `output/shop-stage-switch/`에 생성한다. 이 임시 백업 폴더는 커밋 대상이 아니다.
- `Save Current Stage 3 Reference`는 현재 씬 사본과 메뉴용 기준 파일을 함께 갱신한다.
- **현재 Save Current Stage 1/2 Reference 메뉴는 output 사본만 저장한다.** 새 배치를 확정할 때는 Editor/References의 해당 기준 파일까지 별도 갱신해야 한다. 기록만 남기고 메뉴 연결을 빠뜨리지 않도록 주의한다.
- 과거 실험용 `Tune`, `Fit`, `Apply ... Parts`, `Restore ...` 메뉴도 남아 있다. 일반 단계 전환에는 위 세 Apply 메뉴만 사용한다. 실험 메뉴는 배치·임포트 설정을 바꿀 수 있다.

### 최신 Stage 3 상자·시계

- 원본: 사용자가 제공한 `stage3-steel-container.png`. 프로젝트 파일은 `Art/Stage3Container.png`.
- 상자: `FrontContainer`, 위치 `(522,-436)`, Rect 크기 `(360,240)`, Scale `(.66766,.5954192,.66766)`.
- 시계: `CounterClock`, 위치 `(1009,-578)`, Rect 크기 `(170.5,120)`, Scale `(1.34844,1.34844,1.1237)`.
- 두 Image tint는 `(.62,.66,.70,1)`. 동일 배율이어도 원본 색이 달라 보이는 차이는 남을 수 있다.
- 상자는 Point 필터, 512 임포트, Sprite 영역 `(179,121,1178,777)`. 원본 PNG는 변형하지 않고 Sprite 영역으로 여백을 제외한다.
- 이전 상자용 노멀 연결 제거, rim/specular 0, bottomShade .35. 원본의 밝은 부분만 셰이더에서 억제한다.
- 밑면에 짧고 진한 검정 하드 엣지 접촉 그림자를 추가했다. UV를 큰 블록으로 묶던 실험은 흰 픽셀 문제로 제거했다. 흐린 스팟 그림자를 추가한 상태가 아니다.
- `SetStage3Container`가 재적용 때 이 외형 설정을 적용한다. 배치는 기준 씬이 소유한다.

## 변경: 배경과 경비병

- `FARBACKGROUND`, `Dawn`, `Evening`, `SunsetClouded`: 남산타워를 왼쪽 가시 영역으로 옮기고 확대한 버전. 시간대별 배경도 변경했다.
- `BoothBarricade`: 벽 틈이 보이던 영역 보완.
- `MidBackground`: 경비병을 별도 Sprite로 표현할 수 있도록 배경 수정.
- `RearWatchGuard0/1`, `ExtractedWatchTowers` 추가. 별도 경비병 연결과 앞 경비탑 표시 조정용 메뉴 제공.
- `Animate Extracted Rear Guards`, `Inspect Rear Guard Links`: 연결/점검용. 이번 인계에서는 실시간 움직임을 새로 재생 검증하지 않았다.
- `Align Smoke To Inset Background`: 왼쪽 연기 하단을 타워 옆 건물 좌표에 맞추는 수동 메뉴. 전체 시간대 연기 위치는 재확인 대상이다.
- 시간대 아트 일부는 이미지 생성/수정을 거쳤다. 원본과 픽셀 단위 동일성을 보장하지 않는다.

## 변경: UI와 손님 연출

- `DailyInstruction.png`, `InstructionStartStamp.png`를 제공 이미지로 교체.
- 날짜 칸, 제목·지침·가격표·안내문 위치 조정. 영업시작은 이미지에 포함된 글자를 사용해 중복 텍스트를 제거하고 하단 중앙 크기 조정.
- 가계부 벽의 딸 그림(`ledgerDrawing`)을 코드에서 끄던 처리를 제거.
- 대기 손님 전진에 개별 위상 좌우 흔들림·상하 움직임·작은 비율 변화를 추가. 도착 시 기존 배치로 합류.
- 거래 후 손님은 오른쪽으로 이동하며 작아지고 검게 변하면서 페이드아웃. 다음 손님 갱신 전에 상태를 복원해 사라진 손님이 다시 보이는 프레임 방지.

## 변경: 탑다운 아이템 조작

- 상품 ID와 직접 Sprite 연결 사용. 오래된 배열 순서 기반 프리팹을 신규 카탈로그에 잘못 대응하지 않도록 분리.
- 신규 생성 아이템 긴 변 크기 180 → 126 기준으로 축소. Sprite 기반 PolygonCollider2D 사용.
- 이동·회전 감쇠 강화, 회전 속도 제한 축소. 아이템 충돌 시 별도 회전/힘을 더하던 처리를 제거하고 물리 계산에 맡김.
- 잡은 손 이미지는 이동 방향이나 물체 회전 때문에 옆으로 돌아가지 않도록 원본 방향 유지.

## 주요 코드 위치

| 파일 | 책임 |
|---|---|
| Editor/DystopiaTools.cs | 단계 적용·저장, 상품 등록·검증, 에셋 슬라이싱/연결 |
| Scripts/DystopiaSession.cs | 상품 ID, 가격/설비/단계 조건, 주문 목록, 일일 규칙 |
| Scripts/DystopiaScreen.cs | 일일지침, 딸 그림, 손님 진입/퇴장 |
| Scripts/DystopiaPixelStage.cs | 렌더 레이어, 상자 전용 셰이더 분기 연결 |
| Art/TimeOfDay/PixelStageLighting.shader | 상자 하이라이트 억제, 단단한 접촉 그림자 |
| TopDownTest/Scripts/DystopiaTopDownTest.cs | 상품 Sprite/충돌체 생성, 조작 물리, 손 표시 |
| TopDownTest/Scripts/DystopiaTopDownItem.cs | 충돌 처리 |

## 검증과 남은 항목

- 빌드: `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity quiet -clp:ErrorsOnly` 수행. 실제 커밋 전 결과는 커밋 인계 메시지 참조.
- Stage 적용, 시계/상자 Sprite·색상·위치의 MCP 읽기 및 저장 사본 확인을 수행했다. 모든 단계의 왕복 전환을 한 번에 Play 검증한 것은 아니다.
- 이번 인계 전체 상태는 **PARTIAL**: 파일/메뉴 구현과 개별 적용 확인은 있으나 신규 체크아웃에서 아래 실행 검증이 필요하다.
  1. 상품 등록 후 4종 기본 상품으로 시작, 가격 0 상품 제외 확인.
  2. 단계와 설비 각각 부족한 경우 주문 제외, 모두 충족 시 다음 영업일 반영 확인.
  3. Stage 1 → 2 → 3 → 1 순환 시 상자·시계·숫자·하부장·배경 확인.
  4. 낮/저녁 배경의 경비병·연기 연결, 손님 전진·퇴장 반복, 일일지침 해상도별 표시 확인.
- 고양이 꼬리 애니메이션은 미구현. 배경에 합쳐진 원화라 분리 소재가 필요하다.
- 아빠가 단비를 업고 가판을 멘 로딩 애니메이션은 부위 원화 부족으로 미구현. 원본 GIF를 부위 애니메이션으로 완성했다는 뜻이 아니다.
- 원본 자산은 사용자 제공 자료 및 이 작업의 생성/수정 이미지다. 별도 외부 배포 라이선스 증빙은 이번 작업에서 추가 검증하지 않았다.
- 포커스·마우스·키보드 제어 없이 파일과 Unity MCP로만 인계 작업 수행. 일반 씬/Play 상태 변경 없음.

## 2026-09-15 추가: 단계별 상판·설비·상자·작업대

사용자가 제공한 `N:\개인\정총무\설비\` 그림 24장을 아래 이름으로 복사해 임포트했다. 원본 PNG는 변형하지 않고 Sprite 영역만 알파 경계로 잘랐다(Point, 비압축, mipmap 없음, PPU 100). `설비2 상자.png`(closed/open 쌍이 아닌 여분)는 사용하지 않았다.

| 용도 | 파일 |
|---|---|
| 상판 | `Textures/Checkout/Shop/Stage{1,2,3}CounterTop.png` |
| 정면 닫힌 상자 / 쏟는 열린 상자 | `Shop/Stage{N}CrateClosed.png`, `Shop/Stage{N}CrateOpen.png` |
| 탑다운 작업대 | `Textures/Checkout/Workbench/Stage{N}TopDownWorkbench.png` |
| 설비 | `Shop/Facilities/Stage{N}{FoodShelf,MedicineCabinet,ToolBench,PowerCommunications,NuclearProtection,PrecisionElectronics}.png` |

`Editor/DystopiaFacilityTools.cs`가 배치를 만든다. 메뉴 `Dystopia/설비/…`:

1. `1. Import Facility Artwork`: 위 임포트 설정과 Sprite 크롭.
2. `2. Build Stage 1~3 References`: 단계마다 `Apply Stage N Shop` → 배치 생성 → `Editor/References/StageNReference.unity` 갱신. 작업 씬은 3단계 상태로 남는다(저장은 별도).
3. `Build Current Stage N Layout` / `Save Stage N Reference`: 수치 조정 후 단계별로 다시 만들거나 저장.
4. `Capture Front Preview`: Play 없이 편집 중 픽셀 렌더를 `output/facility-preview/`에 저장.

배치 규칙(코드 상수 `CounterTopY = 380`): 상판은 화면 폭 1280에 맞추고 윗면 시작을 y 380에 둔다. 아래 하부장 일부는 화면 밖이다. `DystopiaCanvas` 아래 `FacilityFoodShelf` 등 6개 Image 오브젝트를 Counter 바로 뒤 그리기 순서로 두고 픽셀 레이어로 등록한다. 단계에 없는 설비는 비활성이다(1단계 2종, 2단계 4종, 3단계 6종). `Stage3LeftPillar/RightPillar`는 상판 뒤로 옮겼다. 손님 그림자 경계 `shadowTableY`는 홈 위치(397~689)로 바꿨다.

`DystopiaTools.ApplyApprovedStageReference`는 설비 6종·`PouringContainer`(Image)·`TopDownWorkbench`(SpriteRenderer sprite·scale)·`shadowTableY`도 기준에서 복사한다. 기준에 없는 설비는 숨긴다. 이전에 3단계 적용 때 강제하던 `SetStage3Container` 호출은 제거했다(새 상자를 덮어쓰기 때문). `DystopiaTopDownTest.ResultFlow`의 무조건적인 `tiltedContainer` 복원은 배치된 UI가 없을 때만 실행되게 바꿨다(단계별 열린 상자 유지).

설비 표시는 외형 단계 기준 씬이 소유하며 `ownedFacilities` 구매 상태와는 아직 연결하지 않았다. 설비 오브젝트는 Inspector에서 위치·크기를 편집할 수 있고 `Save Stage N Reference`로 기준에 반영한다.

검증: compile error 0, 세 단계 `Capture Front Preview` 렌더 확인(정면 화면). 탑다운·쏟기 실행 중 상자·작업대 표시와 새 작업대 크기(3단계 그림 비율이 달라 위아래 3% 잘림)는 PlayMode 확인 대기(PENDING).

### 2026-09-15 추가: 2·3단계 배경 전체 채움

사용자 요청으로 2·3단계도 1단계처럼 배경 6장을 화면 전체 크기로 두고 캐노피(`Canopy`)와 양옆 기둥(`Stage3LeftPillar/RightPillar`)을 숨겼다. 3단계 천장등(`Stage3CeilingLamp`)은 사용자가 켜둔 대로 유지했고 2단계에서는 원래대로 꺼져 있다. 메뉴 `Dystopia/설비/Fill Background Like Stage 1`이 현재 씬에 이 처리를 한다(Stage1Reference에서 배경·연기 Rect 복사 → 프레임 숨김 → 경비 위치 재정렬). `Apply Stage 3 Shop`이 배경을 Stage2Reference에서 가져오던 override는 제거해 각 기준 씬이 자기 배경을 소유한다.

## 2026-09-16 추가: 아트 폴더 재편, 손님 60종 교체, 손님 노멀맵

### 아트 폴더

오늘 작업한 아트의 권위 폴더를 `Assets/Textures/art/`로 옮겼다. 기존 `Assets/Textures/Checkout/` 자산은 건드리지 않았다. `.meta`를 함께 옮겨 GUID와 씬 참조를 보존했다.

| 폴더 | 내용 |
|---|---|
| `art/Facility/CounterTop/` | 단계별 상판 3장 |
| `art/Facility/Crate/` | 정면 닫힌 상자·쏟는 열린 상자 6장 |
| `art/Facility/Props/` | 설비 6종 × 단계 = 12장 |
| `art/Facility/Workbench/` | 탑다운 작업대 3장 |
| `art/Customer/Male/`, `art/Customer/Female/` | 손님 각 30종 |
| `art/Customer/Normal/` | 생성한 손님 노멀맵 60장 |

### 손님 60종 교체

기존 `Textures/Checkout/Characters/Customers/` 45장을 모두 삭제하고 사용자가 제공한 `N:\개인\정총무\남자 손님`·`여자 손님`의 60장으로 교체했다. 삭제한 원본 사본은 `output/customer-swap/deleted-originals/`에 있다.

`DystopiaSession.AppearanceType`이 번호 구간으로 연령을 판정하므로 **성인 → 어린이 → 노인** 순서로 번호를 매겼다. 남녀 모두 성인 24종(01~24), 어린이 3종(25~27), 노인 3종(28~30)이다. 성인 구간 안에서는 가격민감 3 / 거지 3 / 급함 3 / 부자 3 / 일반 12 순이다.

- `MaleAppearanceCount` 29 → 30, `FemaleAppearanceCount` 20 → 30. Editor 도구가 같은 권위 값을 쓰도록 `internal` → `public`으로 열었다.
- `AppearanceType`의 성별별 고정 숫자를 `ChildAppearanceStart = 24`, `ElderlyAppearanceStart = 27` 상수로 바꿨다. 남녀 구간이 같아졌다.
- `DystopiaScreen`의 `maleBreathing`/`femaleBreathing`은 삭제된 손님 기준이라 새 번호 구간에 맞춰 다시 채웠다(거지·급함 Heavy, 노인 Elderly, 나머지 Normal).
- 임포트는 기존 손님과 같은 설정(Sprite, Point, 비압축, mipmap 없음, PPU 100)이며 `maxTextureSize` 256으로 제한했다. 원본 PNG는 변형하지 않았다.

### 손님 노멀맵

`Editor/DystopiaCustomerTools.cs`가 원본 PNG에서 직접 생성한다. 외부 이미지 생성이나 3D 베이크가 아니라 절차적 계산이다.

1. 상자 평균으로 계산 해상도(긴 변 256)까지 축소한다.
2. 알파 실루엣을 크게 흐려 가장자리에서 0으로 떨어지는 몸통 부피를 만들고, 제곱근으로 단면을 둥글게 만든다.
3. 원화 명암을 약하게 흐려 옷 주름 정도의 세부만 남기고 82:18로 섞어 높이장을 만든다.
4. Sobel로 기울기를 구해 tangent-space 노멀로 인코딩한다. 실루엣 바깥은 평면(128,128,255)이다.

`DystopiaPixelStage.Layer`에 `normalVariants`(Sprite ↔ 노멀맵 대응표)를 추가했다. 이전에는 단일 `normalSprite`/`normalMap` 한 쌍이라 손님 한 명만 노멀맵을 받았다. 이제 Surface가 Person인 레이어 5개가 60종 전체의 대응표를 가진다. 상판 투영 그림자는 앞 손님만 만들어야 하므로 판정 기준을 `normalSprite != null`에서 오브젝트 이름 `Customer`로 바꿨다.

메뉴 `Dystopia/손님/`: `1. Import Customer Artwork`, `2. Generate Normal Maps`, `3. Register Customers In Scene`, `Report Framing`.

### 얼굴 디테일에 대한 확인

새 손님 원화는 610~1544px인데 화면에서는 약 200px로 그려져 세부가 줄어든다. 임포트 상한이 원인인지 확인하려고 같은 손님을 256px과 2048px로 각각 임포트해 렌더를 비교했고 **차이가 없었다**. 실제 한계는 `DystopiaPixelStage.width = 480`(480×270 렌더 타깃)이다. 같은 손님을 width 640으로 렌더하면 안경테·눈·니트 짜임이 뚜렷해진다. 이 값은 게임 전체의 픽셀 크기를 바꾸므로 480으로 되돌려 두었고 변경 여부는 사용자 결정이다.

검증: compile error 0, Console error 0, 정면 렌더에서 새 손님 표시 확인, 노멀맵 60장 생성·연결 확인. PlayMode에서 손님 교대·대기 손님·야간 조명 반응은 미확인(PENDING).

### 2026-09-16 수정: 같은 손님이 두 칸 건너 다시 나오던 문제

사용자가 대기 줄의 1번과 3번이 같은 얼굴이라고 보고했다. 원인은 `DystopiaSession.CreateCustomer`의 중복 방지가 **바로 앞 손님의 연령 타입만** 제외했기 때문이다.

- 성별은 앞 손님과 반드시 교대한다. 따라서 N번과 N+2번은 항상 같은 성별이다.
- 제외 조건은 `AppearanceType(i) != previous.Type` 하나뿐인데, 남성을 뽑을 때 앞 손님은 여성이라 `previous.Type`이 `AdultFemale`이다. 남성 외형의 타입은 `AdultMale`·`Child`·`Elderly`뿐이라 **아무것도 제외되지 않는다.**
- 결국 성인 구간에서 이 규칙은 한 번도 작동하지 않았다. 실제로 막은 것은 성별 교대와, 어린이·노인이 연속으로 나오지 않는 것뿐이다.

성별별로 최근 사용한 외형 번호 3개를 기억해 함께 제외하도록 고쳤다(`RecentAppearanceMemory = 3`). 성별이 교대하므로 실제로는 연속 6명 안에서 같은 얼굴이 사라진다. 후보는 최소 24개가 남아 고갈되지 않는다.

검증은 `Dystopia/손님/Validate Appearance Variety`가 실제 `DystopiaSession`으로 40일치 320명을 생성해 재등장 간격을 집계한다.

| 상태 | 간격 6 이하 중복 | 최소 재등장 간격 |
|---|---:|---|
| 수정 전(`RecentAppearanceMemory = 0`으로 재현) | 15건 / 320명 | 2 |
| 수정 후 | 0건 | 하루 안에 재등장 없음 |

간격 2는 화면에 동시에 보이는 앞 손님과 대기 2번째가 같은 얼굴인 경우이며 40일 중 6일에서 나왔다.

### 별건: Validate Rules는 이전부터 실패 상태

`Dystopia/Validate Rules`는 `new DystopiaSettings()`의 코드 기본 카탈로그를 쓰는데, 기본 카탈로그에는 Sprite가 없다. `FindActiveProducts`가 `sprite != null`을 요구하므로 항상 "해금된 상품의 가격과 이미지를 설정하세요"로 멈춘다. 2026-09-14 상품 16종 도입 때부터의 상태이며 이번 작업과 무관하다. 수정하지 않았다.

### 2026-09-16 변경: 원화 그대로 보이는 고해상도 렌더 경로

사용자 요청으로 도트 축소 대신 원화 그대로 보이도록 바꿨다. 되돌리려면 아래 항목을 역순으로 되돌린다.

| 항목 | 이전 | 지금 |
|---|---|---|
| `DystopiaPixelStage.matchScreenResolution` | (없음) | **true** — 렌더 타깃을 `Screen.width` 기준 16:9로 매 프레임 맞춰 항상 1:1. 켜면 RT 필터가 Bilinear, 끄면 `width` 고정값과 Point(도트) |
| `DystopiaPixelStage.width` 슬라이더 상한 | 640 | 1920 (`matchScreenResolution`이 꺼졌을 때만 사용) |
| 손님 텍스처 임포트 상한(`DystopiaCustomerTools.DisplayMaxSize`) | 256 | 2048 (원본 보존) |
| 정면 아트 텍스처 필터 | Point | Bilinear 189장 (`Dystopia/텍스처 필터/…` 메뉴로 왕복 가능, 탑다운 작업대·상품은 Point 유지) |
| `PixelStageLighting.shader` 본체 색 샘플러 | `sampler_PointClamp`(고정) | `sampler_MainTex`(텍스처 설정을 따름). 외곽광·그림자 판정은 Point 유지 |

확인한 사실: 렌더 타깃을 원본의 고품질 축소와 같은 배율로 나란히 놓으면 선의 형태가 같다. 게임뷰를 스크롤로 확대해 보면 편집기가 픽셀을 그대로 뻥튀기하므로 대각선이 계단져 보이는데, 이는 렌더 결과에는 없다. 게임뷰 Scale 1x 또는 Play 중 `Dystopia/Capture Game View`로 확인한다. 흰색 하이라이트가 원본보다 어두운 것은 조명 모델(새벽 주변광 0.62, 대비 항) 때문이며 별도 결정 사항이다.
