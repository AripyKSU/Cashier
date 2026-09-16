# 2026-09-16 오후 아트·배치 갱신 — Stage 1·2·3 공통

앞서 올린 Stage 1(a43c1aec), Stage 2(8cb24c47) 인계 이후 바뀐 내용 전부다. 수치의 최종 권위는 각 단계 명세 `STAGE1_HANDOFF.md`, `STAGE2_HANDOFF.md`, `STAGE3_HANDOFF.md`(이 커밋에서 재생성)이며, 이 문서는 무엇이 왜 바뀌었는지와 적용·조절 방법을 정리한다.

## 1. 아트 3px 도트화 (PNG 자체를 교체)

정면 설비·상자·상판·프레임·시계가 배경(1.4텍셀/px)·손님(2텍셀/px)보다 3~5배 촘촘한 고해상도 일러스트라 화면에서 "HD 스티커"처럼 떠 보였다. 화면에서 **도트 1개가 캔버스 3px**가 되도록 원본을 축소 → 48색 양자화 → 1px 어두운 외곽선 → nearest 확대로 되돌려 저장했다. **PNG 크기·GUID·`.meta`·Sprite 자르기 영역은 그대로**라 씬·프리팹 참조는 바뀌지 않는다. 임포트는 기존대로 Point 필터.

| 단계 | 파일(`Assets/Textures/art/…`) | 표시 크기(캔버스 px) → 도트 수 |
|---|---|---|
| 1 | `Facility/CounterTop/Stage1CounterTop.png` | 1280×325 → 427×115 |
| 1 | `Facility/Crate/Stage1CrateClosed.png` / `Stage1CrateOpen.png` | 263×156 → 88×52 / 450×267 → 150×89 |
| 1 | `Facility/Props/Stage1FoodShelf.png` / `Stage1MedicineCabinet.png` | 213×183 → 71×61 / 259×185 → 86×61 |
| 1 | `Facility/Frame/BoothCanopy.png` (나무 천막) | 1310×720 → 437×246 |
| 1 | `Facility/Clock/Stage1BasicClock.png` | 157×77 → 52×26 |
| 2 | `Facility/CounterTop/Stage2CounterTop.png` | 1054×325 → 351×113 |
| 2 | `Facility/Crate/Stage2RustedCrateClosed.png` / `Stage2RustedCrateOpen.png` | 263×168 → 88×56 / 450×291 → 150×97 |
| 2 | `Facility/Props/Stage2ToolBench.png`, `Stage2PowerCommunications.png`, `Stage2FoodShelf.png`, `Stage2MedicineCabinet.png` | 354×159 → 118×53, 204×222 → 68×74, 204×176 → 68×59, 221×158 → 74×53 |
| 2 | `Facility/Frame/Stage2RustedFrame.png` (Stage2Ceiling, Stage2LeftPillar, Stage2RightPillar 영역만) | 1331×96 → 444×48, 146×447 → 49×112, 143×438 → 48×112 |
| 2 | `Facility/Clock/Stage2RustedClock.png` | 157×77 → 52×25 |
| 3 | `Facility/CounterTop/Stage3CounterTop.png` | 1054×325 → 351×111 |
| 3 | `Facility/Crate/Stage3CrateClosed.png` / `Stage3CrateOpen.png` | 263×167 → 88×56 / 450×261 → 150×87 |
| 3 | `Facility/Props/Stage3NuclearProtection.png`, `Stage3PrecisionElectronics.png`, `Stage3ToolBench.png`, `Stage3PowerCommunications.png`, `Stage3FoodShelf.png`, `Stage3MedicineCabinet.png` | 346×233 → 115×77, 438×235 → 146×78, 298×134 → 99×45, 180×195 → 60×65, 197×169 → 66×57, 213×152 → 71×51 |
| 3 | `Facility/Frame/Stage3Shop.png` (Stage3Ceiling, Stage3LeftPillar, Stage3RightPillar, Stage3CeilingLamp 영역만) | 1398×96 → 466×46, 146×474 → 49×121, 143×460 → 48×121, 165×21 → 55×8 |
| 3 | `Facility/Clock/Stage3GunmetalClock.png` | 157×62 → 52×20 |

- 도트화 전 원본: 로컬 `output/stage3-pixelize/before/`, `output/stage-pixelize/before/` (커밋 대상 아님). 4·5·6px 비교 시안은 `output/stage3-pixelize/`.
- 상자를 어둡고 매트하게 바꾸는 시도(`output/crate-matte/`)는 사용자 검토 후 **되돌렸다**. 현재 상자는 도트화만 적용된 상태다.
- 새 아트를 같은 규칙으로 만들 때: 표시 폭 ÷ 3 = 가로 도트 수로 축소, 48색, 외곽선 1px, 원본 크기로 nearest 확대. 도구는 저장소에 없다(세션 스크립트). 필요하면 Editor 메뉴로 옮겨 달라고 요청한다.

## 2. 정면 상자 배치 통일 (세 단계 동일)

`TopDownCheckout/TopDownTestCanvas/FrontView/FrontContainer` RectTransform — Stage 1에서 사용자가 맞춘 값을 Stage 2·3 기준 씬에 복사했다.

| 항목 | 값 |
|---|---|
| anchoredPosition | (522, −505) |
| sizeDelta | (360, 240) |
| localScale | (0.72995913, 0.72995913, 0.66766) |
| 표시 크기 | 263×175 (preserveAspect, 상자 Sprite 비율에 따라 높이 156~175) |

상판(`Counter`)은 세 단계 모두 윗면 Y −483(Stage 1은 사용자 조정으로 −498), 표시 높이 324.5로 같고, Stage 1만 폭 1280(나머지 1053)이다. 폭까지 맞추지는 않았다.

## 3. 탐조등 위치 (밤 전용 레이어)

`FrontCounter/DystopiaCanvas/LeftSearchlight`, `RightSearchlight`의 pivot(0, 0.5)=빔 시작점을 경비 중심에 맞췄다. 세 기준 씬 공통.

| 오브젝트 | anchoredPosition |
|---|---|
| LeftSearchlight | (126.20337, −172.6695) = LeftWatchGuard 중심 |
| RightSearchlight | (1159.7751, −234.81252) = RightWatchGuard 중심 |

회전(−15.4°, −169.2°)과 크기는 그대로다.

## 4. 그림자 (상자·설비 6종)

`TopDownCheckout` → Dystopia Pixel Stage → `Layers`의 해당 항목. 세 기준 씬 공통.

| 대상 | contactShadow (x,y,z,w) | soft | projected | bottomShade | lampResponse | highlightResponse |
|---|---|---|---|---|---|---|
| FrontContainer | (0.5, 0, 1.06, **0.26**) | 0 | 0 | 0.3 | 0.65 | 0.9 |
| Facility 6종 | (0.5, 0, 1.06, **0.22**) | 0 | 0 | 0.3 | 0.65 | 0.9 |

- w=높이 비율(0이면 끔). soft=0은 번짐 없는 선명한 접지 그림자.
- 각 오브젝트의 uGUI `Shadow` 컴포넌트에는 실루엣 드롭 섀도 값(색 (0.02,0.015,0.01,0.8), 거리 (10,−12))을 넣어 두었지만 **비활성**이다. Inspector에서 체크만 하면 켜진다.
- `DystopiaPixelStage.cs`: 레이어에 contactShadow가 저장돼 있으면 그 값을 쓰고, 없을 때만 Stage 3 소품용 얇은 기본 그림자를 넣도록 수정했다(이전에는 코드가 항상 얇은 값으로 덮어써서 Inspector 값이 무시됐다).
- 메뉴 `Dystopia/설비/Pixel Contact Shadows (Facilities + Crate, Current Stage)`가 현재 단계에 같은 값을 다시 적용한다.

## 5. 상자 착지 연출

- 낙하 시작 높이 38 → **76**(2배). `DystopiaTopDownTest.cs`의 `LandingDropHeight` 상수.
- 독립 컴포넌트 `Assets/DystopiaPrototype/Scripts/Effects/CrateLandingEffect.cs`와 사용법 `CrateLandingEffect.md`(커밋 53e8d196).

## 6. 그 밖의 코드

- 탑다운 전환 덮개가 옛 기본 작업대가 아니라 현재 단계 작업대 그림을 쓰도록 수정 (`DystopiaTopDownTest.ScrollToWork`). 도착 후 그림이 바뀌던 현상 제거.
- `DystopiaFacilityTools.cs`: 위 그림자 메뉴 추가(단계 무관).

## 7. 적용·저장 방법

1. Play 종료 상태에서 `Dystopia > Apply Stage N Shop` — 기준 씬의 배치·Sprite·그림자·탐조등을 열린 씬에 복사한다.
2. Inspector에서 값을 바꿨으면 같은 단계에서 `Dystopia > 설비 > Save Stage N Reference`로 기준 씬에 반영한다. 안 하면 다음 Apply 때 되돌아간다.
3. 배치 검증 이미지: `_Reference/Stage1_LayoutCheck.png`, `Stage2_LayoutCheck.png`, `Stage3_LayoutCheck.png` (조명 없음, 낮 기준, 이 커밋 시점의 수치로 합성).

## 8. 이 갱신에 포함된 파일

- PNG 33장(위 표), `Editor/References/Stage1~3Reference.unity`, `Scenes/DystopiaVerticalSlice.unity`(Stage 3 상태, 저장됨)
- `Scripts/DystopiaPixelStage.cs`, `Editor/DystopiaFacilityTools.cs`, `TopDownTest/Scripts/DystopiaTopDownTest.cs`, `Shaders/Checkout/PixelStageLighting.shader`(다른 작업자의 Stage 3 전경 보정 포함)
- `STAGE1_HANDOFF.md`, `STAGE2_HANDOFF.md`(재생성), `STAGE3_HANDOFF.md`(신규), 이 문서
