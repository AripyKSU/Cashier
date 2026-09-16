# Stage 2 개발자 인계 — 정면 가판 씬 전체 명세

작성 2026-09-16 17:49 · 브랜치 `astra-prototype` · HEAD `53e8d196` (작업 트리 미커밋 변경 포함) · 근거 파일 SHA1 `54b1632dc699`

이 문서 하나로 **Stage 2 정면 화면을 Unity에서 그대로 재현**할 수 있도록, 씬에 저장된 모든 오브젝트·컴포넌트·수치·이미지·스크립트·머티리얼·셰이더·폰트·프리팹과 적용 방법을 기록했다. 값은 사람이 손으로 옮긴 것이 아니라 아래 근거 파일을 파싱해 프리팹 기본값과 씬 override를 합산한 결과다.

## 0. 근거(권위) 파일과 읽는 법

| 항목 | 값 |
|---|---|
| Stage 2 권위 씬 | `Assets/DystopiaPrototype/Editor/References/Stage2Reference.unity` (2026-09-16 12:59 저장 + 17:2x~17:4x 상자 통일·탐조등·그림자 반영) |
| 실제 플레이 씬 | `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity` — 현재는 Stage 3 상태로 저장되어 있다. Stage 2는 `Dystopia > Apply Stage 2 Shop` 메뉴가 위 권위 씬에서 값을 복사해 적용한다 |
| 프리팹 원본 | `Assets/DystopiaPrototype/Prefabs/FrontView.prefab` (DystopiaCanvas), `CheckoutUI.prefab` (TopDownTestCanvas), `Workbench.prefab` (TopDownWorkbench). **이 문서의 값은 프리팹 기본값 + 씬 override를 합산한 최종값**이므로 프리팹만 열어 보고 판단하면 틀린다 |
| 기준 해상도 | 1280×720 (CanvasScaler Scale With Screen Size, Match Width 0). 모든 좌표는 이 캔버스 단위 |
| 좌표계 | uGUI RectTransform. 이 씬의 거의 모든 요소는 anchorMin=anchorMax=(0,1), pivot=(0,1) = **화면 좌상단 기준, Y는 아래로 갈수록 음수**. 손님 3종만 pivot (0.5,0)=아래 중앙 |
| 실제 표시 크기 | sizeDelta × localScale. preserveAspect가 켜진 Image는 그 사각형 안에 Sprite 전체를 비율 유지로 맞춘다(알파 여백 포함) |
| Sprite 영역 | `.meta`의 spriteMode 2(Multiple)면 표의 sprite rect(좌하단 기준 px)만 그린다. spriteMode 1(Single)이면 PNG 전체를 그린다(메타에 남은 옛 spriteSheet 항목은 무시) |
| 배치 검증 이미지 | `Assets/Textures/art/_Reference/Stage2_LayoutCheck.png` — 이 문서 값만으로 Python에서 합성한 1280×720 결과(조명 없음, 낮 상태). Unity Game View와 배치가 같아야 한다 |

**절대 규칙(AGENTS.md):** 배치는 씬이 소유한다. `Awake/Start/OnEnable/OnValidate/Update`에서 RectTransform·Canvas 값을 코드 고정값으로 덮어쓰지 않는다. 런타임 연출(호흡·대기열 이동·퇴장·연기·경비병)은 씬 시작 시 읽은 배치에 **상대적으로** 움직이고 종료 시 되돌린다.

## 1. 프로젝트·렌더 설정

| 항목 | 값 | 근거 |
|---|---|---|
| Unity | 6000.3.18f1 | `ProjectSettings/ProjectVersion.txt` |
| Render Pipeline | URP 17.3.0, 2D Renderer | `Assets/Settings/UniversalRP.asset` (HDR on, MSAA 1, RenderScale 1, SRP Batcher on), `Assets/Settings/Renderer2D.asset` (TransparencySortMode 0, DepthStencil on) |
| Color Space | Linear (`m_ActiveColorSpace: 1`) | `ProjectSettings/ProjectSettings.asset` |
| 기본 창 | 1920×1080, fullscreenMode 1 | 같은 파일 |
| 입력 | Input System 1.19 (`InputSystemUIInputModule`, 패키지 기본 `DefaultInputActions`) | 씬 EventSystem |
| UI | uGUI 2.0 (`Image`, `Text`, `Button`, `Shadow`, `Outline`, `RectMask2D`, `RawImage`) — TextMeshPro 미사용 | 씬 |
| 한글 폰트 | `Assets/Fonts/Checkout/Mulmaru.otf` (GUID b3f0a6c94e8e4fc3a1f40d741190c0e6, TrueTypeFontImporter fontSize 16, includeFontData 1, dynamic). 없으면 코드가 OS `Malgun Gothic`으로 대체하고 에러를 남긴다 | `DystopiaScreen.Awake` |
| Sorting Layer | Default 만 존재 | `ProjectSettings/TagManager.asset` |
| 빌드 씬 목록 | `Assets/Scenes/InitScene.unity` 만 등록. 프로토타입 씬은 `Dystopia > Play Vertical Slice` 메뉴 또는 씬 직접 열기로 실행 | `EditorBuildSettings.asset` |

## 2. 화면이 만들어지는 원리 (반드시 이해할 것)

1. `FrontCounter/DystopiaCanvas`(Screen Space Overlay, 1280×720)에 배경·손님·가판·설비를 uGUI `Image`로 **편집 가능하게** 배치한다. Hierarchy 순서 = 그리기 순서(뒤→앞).
2. `TopDownCheckout`의 **DystopiaPixelStage**가 위 Image들을 `DystopiaPixelSource`(BaseMeshEffect)로 가로채 임시 월드 메시로 옮기고, 전용 셰이더 `Cashier/PixelStageLighting`으로 시간대 조명을 입혀 RenderTexture에 그린 뒤 화면 전체 `RawImage`(sortingOrder -100 Canvas)로 출력한다. 원본 Image는 메시가 비워져 화면에 직접 그려지지 않는다.
3. 렌더 크기: `width`=480, `matchScreenResolution`=False, `ceilingLamp`=Stage3CeilingLamp. `UseScreenResolution = matchScreenResolution || ceilingLamp.name == "Stage3CeilingLamp"` 이므로 이 씬의 실제 렌더는 **화면 해상도 1:1(Bilinear)** 이다(5.1 참조). 텍스트·계산기·문서 UI는 픽셀 스테이지 레이어가 아니므로 원래 Canvas에 선명하게 남는다.
4. **DystopiaDayNight**가 영업 시각(09~21시)으로 Dawn/Sunset/Evening/CityLights/Searchlight/CounterLamplight 레이어의 canvasRenderer 알파와 PixelStage 시간 가중치를 매 프레임 갱신한다. 편집 중에는 `preview`가 꺼져 있으면 낮(알파 0)으로 복원한다. **즉 아래 표의 밤 레이어들은 낮에는 보이지 않는 것이 정상이다.**
5. 손님 3슬롯(`Customer`, `WaitingLeft`, `WaitingRear`)의 Sprite는 `DystopiaScreen`이 거래 데이터에 따라 남/여 30종 배열에서 교체한다. 위치·크기는 씬 값이며 호흡은 렌더 1픽셀 상하 이동뿐이다.
6. 정면 상자(`FrontContainer`)와 시계(`CounterClock`)는 별도 Canvas `TopDownCheckout/TopDownTestCanvas`에 있지만 같은 1280×720 좌표계이며 PixelStage 레이어 목록에 포함되어 함께 렌더된다.

## 3. 아트 폴더 구조 (2026-09-16 재편) 와 사용 이미지 전체 목록

옛 `Assets/Textures/Checkout/` 100장을 모두 `Assets/Textures/art/` 아래 종류별 폴더로 옮겼다(2026-09-16, 커밋 a43c1aec; .meta 동반 이동, GUID 보존, 씬·프리팹 참조 변경 없음). 이동 목록: `output/art-move/moved.csv`, 옛→새 경로 표: `doc/CHECKOUT_ASSET_LAYOUT.md`.

```text
Assets/Textures/art/
├── Background/        원경·중경·감시탑·바리케이드
├── Effects/TimeOfDay/ 아침·석양·저녁 배경, 도시불빛, 탐조등, 가판등
├── Effects/Fog/       안개 3층
├── Effects/Smoke/     굴뚝 연기 4프레임
├── Characters/Crowd/  군중 3줄
├── Characters/Guard/  감시탑 경비병
├── Characters/Inspector/ 감독관 팝업 원화
├── Customer/Male|Female|NormalMap|Legacy  손님 원화 60 + 노멀 60 + 구형 1
├── Facility/Frame/    가판 천막·프레임 시트 (Stage 1~3)
├── Facility/Clock/    시계 (Stage 1~3)
├── Facility/CounterTop/ 상판
├── Facility/Crate/    정면 상자·열린 상자
├── Facility/Props/    설비 6종 × 단계
├── Facility/Workbench/ 탑다운 작업대
├── Products/ (+Legacy) 상품 16종 + 구형
├── UI/                지침서·가계부·대사창·표정·손
├── Workbench/         계산기·토글·디바이더·청소기·탑다운 물품
├── _Reference/        배치 검증 이미지 Stage{N}_LayoutCheck.png
└── STAGE{N}_HANDOFF.md 단계별 명세 문서(이 문서)
```

### 3.1 Stage 2 씬이 참조하는 이미지 (PNG + .meta 짝을 함께 전달)

아래는 권위 씬(프리팹 포함)이 GUID로 직접 참조하는 PNG 전부다. `PPU`=Pixels Per Unit, `Filter` 0=Point 1=Bilinear, `Max`=maxTextureSize(플랫폼 override 있으면 괄호), `Comp` 0=Uncompressed. Sprite rect는 좌하단 기준 (x, y, w, h) px.

| 파일 | PNG 크기 | Type/Mode | PPU | Filter | Max | Comp | Mip | sRGB | AlphaIsTransp | 씬이 쓰는 Sprite | Sprite rect (Multiple만) | GUID |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `Assets/Textures/art/Background/BoothBarricade.png` | 2172×724 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | BoothBarricade |  | `c10c73fc054ac19498fffbbacf16b031` |
| `Assets/Textures/art/Background/FARBACKGROUND.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FARBACKGROUND |  | `8828d23a57ac09d4db48ac618820ad7b` |
| `Assets/Textures/art/Background/LeftWatchTower.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | LeftWatchTower |  | `1ca5f6632c398c14ba179258cead1796` |
| `Assets/Textures/art/Background/MidBackground.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MidBackground |  | `0675361a18134594e9e942f04ff98660` |
| `Assets/Textures/art/Background/RightWatchTower.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | RightWatchTower |  | `e00f255da7894164497dfd5df66938dd` |
| `Assets/Textures/art/Characters/Crowd/CrowdBack.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | CrowdBack |  | `7c746110a1d6bff44bb5e422328cb82c` |
| `Assets/Textures/art/Characters/Crowd/CrowdFront.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | CrowdFront |  | `38fc8765ba9bcc14e9475d993dc60520` |
| `Assets/Textures/art/Characters/Crowd/CrowdMiddle.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | CrowdMiddle |  | `27ea243b3280a6d48a50257d78565fb9` |
| `Assets/Textures/art/Characters/Guard/WatchGuard.png` | 1025×783 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | WatchGuard |  | `72f801b7e9d8ad44c8b4fe24b01f89f4` |
| `Assets/Textures/art/Characters/Inspector/Inspector.png` | 134×359 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | Inspector |  | `5ec7b88773b5f754b9c36efbac851495` |
| `Assets/Textures/art/Customer/Female/FemaleChild_01.png` | 1000×1000 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleChild_01 |  | `4ef53298fa5e2e445877e6bbe8e8ac1e` |
| `Assets/Textures/art/Customer/Female/FemaleChild_02.png` | 1000×1000 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleChild_02 |  | `569adf108d89ed540ae2983c6436d37e` |
| `Assets/Textures/art/Customer/Female/FemaleChild_03.png` | 1000×1000 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleChild_03 |  | `af60726775e7c164c8c7d8a0b1697bc0` |
| `Assets/Textures/art/Customer/Female/FemaleElder_01.png` | 865×1155 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleElder_01 |  | `3c0ad79631dbbe541a256c1d05466940` |
| `Assets/Textures/art/Customer/Female/FemaleElder_02.png` | 815×1225 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleElder_02 |  | `cd76e44a98e83cc4c97d348280c73a1a` |
| `Assets/Textures/art/Customer/Female/FemaleElder_03.png` | 650×865 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleElder_03 |  | `91e98a30763e9364ba35d0b3caf5f531` |
| `Assets/Textures/art/Customer/Female/FemaleHasty_01.png` | 930×1080 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleHasty_01 |  | `5e21aad58c1984a4a83466f2d8e5d28e` |
| `Assets/Textures/art/Customer/Female/FemaleHasty_02.png` | 186×216 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleHasty_02 |  | `f31911dd43f506c4784814953c3fa76d` |
| `Assets/Textures/art/Customer/Female/FemaleHasty_03.png` | 186×216 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleHasty_03 |  | `e97f16af821019047b79d8424f3e91cc` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_01.png` | 930×1080 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_01 |  | `ea3a29c8680920d4a8a262cbae9548e8` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_02.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_02 |  | `52aab4e4bcb723b41be8d77414ea2013` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_03.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_03 |  | `8bdba0f732897db4aa9644766aba1cde` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_04.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_04 |  | `6d7191a8a46661b449e3946d9c1c01b2` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_05.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_05 |  | `50320d9b9c294a24893a7b18da21d27f` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_06.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_06 |  | `0fab33a33d325804f9b2858f044b0a59` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_07.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_07 |  | `2c2bf0c99f6a49044946f4fe5cafadc1` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_08.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_08 |  | `1743f0c476075e444b0d962a9c03d83d` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_09.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_09 |  | `417e6bb9a1348ae42bcdf2b7dc148d6e` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_10.png` | 1164×1351 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_10 |  | `9e0a709a2bac33d42a165bf0f71791a9` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_11.png` | 930×1080 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_11 |  | `af63428575396834ba3d6669b3216018` |
| `Assets/Textures/art/Customer/Female/FemaleNormal_12.png` | 1147×1372 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleNormal_12 |  | `fb402c376754a0b46b351a2bf0eab33e` |
| `Assets/Textures/art/Customer/Female/FemalePoor_01.png` | 1044×1386 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemalePoor_01 |  | `3e3270d64c28bff4385263875d8df56f` |
| `Assets/Textures/art/Customer/Female/FemalePoor_02.png` | 870×1155 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemalePoor_02 |  | `5222a50ecf2a3da4089907b4d87643ad` |
| `Assets/Textures/art/Customer/Female/FemalePoor_03.png` | 912×1212 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemalePoor_03 |  | `8fb2aac44352f0d48abe38981f6fa7b0` |
| `Assets/Textures/art/Customer/Female/FemalePriceSensitive_01.png` | 1002×1164 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemalePriceSensitive_01 |  | `5f9c7f83da89ded498ecef1664611641` |
| `Assets/Textures/art/Customer/Female/FemalePriceSensitive_02.png` | 1150×1770 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemalePriceSensitive_02 |  | `33714d2e5489a764d93de6789110b67d` |
| `Assets/Textures/art/Customer/Female/FemalePriceSensitive_03.png` | 744×1086 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemalePriceSensitive_03 |  | `b5e44c2f4866a524a9c3c6786816f80e` |
| `Assets/Textures/art/Customer/Female/FemaleWealthy_01.png` | 1165×1350 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleWealthy_01 |  | `b83317e5439a0914ea32d9bf0d141c36` |
| `Assets/Textures/art/Customer/Female/FemaleWealthy_02.png` | 972×1134 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleWealthy_02 |  | `9ed0be4fe74e9bd4897c2aa13f18cdeb` |
| `Assets/Textures/art/Customer/Female/FemaleWealthy_03.png` | 1128×1280 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FemaleWealthy_03 |  | `d44eb373a89c41a49986c7921ecfc182` |
| `Assets/Textures/art/Customer/Legacy/MaleCustomer0.png` | 128×128 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleCustomer0 |  | `475588eff93a49a1bb6368d7b6a6f2ae` |
| `Assets/Textures/art/Customer/Male/MaleChild_01.png` | 1254×1254 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleChild_01 |  | `984c8c0f6eaae6a47a9015fc317c63bc` |
| `Assets/Textures/art/Customer/Male/MaleChild_02.png` | 1254×1254 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleChild_02 |  | `99690a59f8968854db3ef070adb04953` |
| `Assets/Textures/art/Customer/Male/MaleChild_03.png` | 1254×1254 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleChild_03 |  | `12f66c2b6f0bd8749871d1945ef1aa90` |
| `Assets/Textures/art/Customer/Male/MaleElder_01.png` | 810×1235 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleElder_01 |  | `d7dcd96907457b54c8e5b04905c7e743` |
| `Assets/Textures/art/Customer/Male/MaleElder_02.png` | 915×1095 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleElder_02 |  | `4bff2fba9e7655c49b6d72d3a80ac98b` |
| `Assets/Textures/art/Customer/Male/MaleElder_03.png` | 915×1095 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleElder_03 |  | `d7e365c7c6f28634fb1a1f55d322d96e` |
| `Assets/Textures/art/Customer/Male/MaleHasty_01.png` | 816×996 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleHasty_01 |  | `043abd62e9254a048bc9c5a1fbf8c8ed` |
| `Assets/Textures/art/Customer/Male/MaleHasty_02.png` | 816×996 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleHasty_02 |  | `071c8db916ef39746a4d9e9e02efc8d9` |
| `Assets/Textures/art/Customer/Male/MaleHasty_03.png` | 816×996 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleHasty_03 |  | `649cadc3f6da8c8448bf166f6f4fb12c` |
| `Assets/Textures/art/Customer/Male/MaleNormal_01.png` | 1018×1544 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_01 |  | `b872187a3c6fd15479d47c52c33dd3ec` |
| `Assets/Textures/art/Customer/Male/MaleNormal_02.png` | 1019×1544 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_02 |  | `726a78dac2402c5409335d3e6b53a5c7` |
| `Assets/Textures/art/Customer/Male/MaleNormal_03.png` | 1018×1544 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_03 |  | `d3ff24ae084dc3741b40269c39d206a5` |
| `Assets/Textures/art/Customer/Male/MaleNormal_04.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_04 |  | `c4f62cbad85764c4d881f4392fcff3be` |
| `Assets/Textures/art/Customer/Male/MaleNormal_05.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_05 |  | `2d9a95f2a7541e34ebaa74e59d88fd3b` |
| `Assets/Textures/art/Customer/Male/MaleNormal_06.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_06 |  | `9f04867c6466020499e54f3c536a6939` |
| `Assets/Textures/art/Customer/Male/MaleNormal_07.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_07 |  | `a0c5992e46f2d1d42a8a290b6609a8f9` |
| `Assets/Textures/art/Customer/Male/MaleNormal_08.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_08 |  | `39bec8fa39c04cc4d92905fb2825188d` |
| `Assets/Textures/art/Customer/Male/MaleNormal_09.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_09 |  | `3ff4d9a67e54ec94695bd7852ce1e438` |
| `Assets/Textures/art/Customer/Male/MaleNormal_10.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_10 |  | `1c5cb739ccb924f438f666c5b07b20c1` |
| `Assets/Textures/art/Customer/Male/MaleNormal_11.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_11 |  | `83d51b01f314d024b8b7ff1904f62540` |
| `Assets/Textures/art/Customer/Male/MaleNormal_12.png` | 610×925 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleNormal_12 |  | `65f75d23988981243a9b6a1a8624f590` |
| `Assets/Textures/art/Customer/Male/MalePoor_01.png` | 762×1062 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MalePoor_01 |  | `82b46e377a44b0b42aa3d685f39f91c3` |
| `Assets/Textures/art/Customer/Male/MalePoor_02.png` | 816×996 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MalePoor_02 |  | `1673ec41fefc68248be8b2a042cf031e` |
| `Assets/Textures/art/Customer/Male/MalePoor_03.png` | 858×948 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MalePoor_03 |  | `42b7858db365e0d4aa4c999e48f72986` |
| `Assets/Textures/art/Customer/Male/MalePriceSensitive_01.png` | 732×1110 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MalePriceSensitive_01 |  | `ad8a5b86163973b43b643a9c77727172` |
| `Assets/Textures/art/Customer/Male/MalePriceSensitive_02.png` | 659×1000 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MalePriceSensitive_02 |  | `64e2cf5ec61def34ca16f7a2f1273e3d` |
| `Assets/Textures/art/Customer/Male/MalePriceSensitive_03.png` | 710×1075 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MalePriceSensitive_03 |  | `208535dcfcd434c489e41ff42d2839f7` |
| `Assets/Textures/art/Customer/Male/MaleWealthy_01.png` | 815×945 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleWealthy_01 |  | `5f8faaf3cb1633c4593c73a04d3a3ab5` |
| `Assets/Textures/art/Customer/Male/MaleWealthy_02.png` | 930×1080 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleWealthy_02 |  | `65fc5187a2f15e140bd2458cc4f16961` |
| `Assets/Textures/art/Customer/Male/MaleWealthy_03.png` | 1116×1296 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | MaleWealthy_03 |  | `9171060c32a73d344837df1ee94dc4ad` |
| `Assets/Textures/art/Effects/Fog/FogBack.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FogBack |  | `ef4c55532956466b9df1c26a4fd8fc4e` |
| `Assets/Textures/art/Effects/Fog/FogFront.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FogFront |  | `5f176efe93944680a2c22cc7bbc8ae11` |
| `Assets/Textures/art/Effects/Fog/FogMid.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | FogMid |  | `224e6b0c3cfb4a32a560db7fbde73e52` |
| `Assets/Textures/art/Effects/Smoke/ChimneySmoke0.png` | 487×871 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | ChimneySmoke0 |  | `8146c36afd06422a92ae965371323f2e` |
| `Assets/Textures/art/Effects/Smoke/ChimneySmoke1.png` | 450×1069 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | ChimneySmoke1 |  | `a732901af7a142c68d5ff1e17b15dc79` |
| `Assets/Textures/art/Effects/Smoke/ChimneySmoke2.png` | 537×1146 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | ChimneySmoke2 |  | `9ee90dc6331641169fb2748b57111bac` |
| `Assets/Textures/art/Effects/Smoke/ChimneySmoke3.png` | 676×1138 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | ChimneySmoke3 |  | `06504071d22a47b386d3a3a7053d2364` |
| `Assets/Textures/art/Effects/TimeOfDay/CityLights.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | CityLights |  | `d9322dd776712984fbbee219b96cc8ce` |
| `Assets/Textures/art/Effects/TimeOfDay/CounterLight.png` | 1774×887 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | CounterLight |  | `2579725b5a01a4244a5914b55017c014` |
| `Assets/Textures/art/Effects/TimeOfDay/Dawn.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | Dawn |  | `7769da782da17f345a6e5a8141c2e56d` |
| `Assets/Textures/art/Effects/TimeOfDay/Evening.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | Evening |  | `21ee1b35d9ae7b245b8dfeaba460a7b0` |
| `Assets/Textures/art/Effects/TimeOfDay/Searchlight.png` | 2172×724 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | Searchlight |  | `27268933eeab74b41a932f1049dbab1a` |
| `Assets/Textures/art/Effects/TimeOfDay/SunsetClouded.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | SunsetClouded |  | `1a0433ce38e502b49a4f95fb6808d588` |
| `Assets/Textures/art/Facility/Clock/CounterClock.png` | 128×128 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | CounterClock |  | `594304eb42c84524a1909e1dd7546641` |
| `Assets/Textures/art/Facility/Clock/Stage2RustedClock.png` | 1774×887 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2Clock | Stage2Clock (377,162,1023,500) pivot(0.5, 0.5) | `2ab2087055f53fa4f8dd3eea7283e27f` |
| `Assets/Textures/art/Facility/CounterTop/BoothCounter.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | BoothCounter |  | `dec0edb6a0a29eb4ba990f92dff08f2b` |
| `Assets/Textures/art/Facility/CounterTop/Counter.png` | 1672×941 | Sprite/Single | 100 | 1 | 2048 | None | off | off | on | Counter |  | `b8b405d086908a542a68b296eb646229` |
| `Assets/Textures/art/Facility/CounterTop/Stage2CounterTop.png` | 1448×1086 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2CounterTop +Texture2D | Stage2CounterTop (0,0,1448,465) pivot(0.5, 0.5) | `44538b2a1a06f114bb8294dd24650810` |
| `Assets/Textures/art/Facility/Crate/FrontContainerFemale.png` | 1254×1254 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | FrontContainerFemale |  | `c560433c0142a2e4596b7f7cbc13fb5a` |
| `Assets/Textures/art/Facility/Crate/FrontContainerMale.png` | 122×102 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | FrontContainerMale |  | `47c07e51f9acabe45b7dfb3fa9362df5` |
| `Assets/Textures/art/Facility/Crate/Stage2RustedCrateClosed.png` | 1533×1026 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2RustedCrateClosed | Stage2RustedCrateClosed (0,84,1472,942) pivot(0.5, 0.5) | `5268afbe20d276643b4315f256369733` |
| `Assets/Textures/art/Facility/Crate/Stage2RustedCrateOpen.png` | 1534×1025 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2RustedCrateOpen | Stage2RustedCrateOpen (0,33,1533,992) pivot(0.5, 0.5) | `fbeac823711e39e45935051ffac7df5e` |
| `Assets/Textures/art/Facility/Frame/BoothCanopy.png` | 1670×942 | Sprite/Single | 100 | 0 | 2048 | None | off | off | on | BoothCanopy |  | `6978fa40aee53b643836c083c2dd4d52` |
| `Assets/Textures/art/Facility/Frame/Stage2RustedFrame.png` | 1672×941 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2Ceiling, Stage2LeftPillar, Stage2RightPillar | Stage2Ceiling (0,761,1672,180) pivot(0.5, 0.5); Stage2LeftPillar (0,342,183,419) pivot(0.5, 0.5); Stage2RightPillar (1493,342,179,419) pivot(0.5, 0.5) | `c20925f51e6f4824e9e9177278d521c9` |
| `Assets/Textures/art/Facility/Frame/Stage3Shop.png` | 1672×941 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage3CeilingLamp | Stage3CeilingLamp (738,843,196,27) pivot(0.5, 0.5) | `20d1fc7e6767539498ac36a33edbc4a4` |
| `Assets/Textures/art/Facility/Props/Stage2FoodShelf.png` | 1254×1254 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2FoodShelf | Stage2FoodShelf (166,223,963,829) pivot(0.5, 0.5) | `b5917054c7dcfb0428354bc4d5375dfb` |
| `Assets/Textures/art/Facility/Props/Stage2MedicineCabinet.png` | 1448×1086 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2MedicineCabinet | Stage2MedicineCabinet (250,184,958,684) pivot(0.5, 0.5) | `57ebf5252bd5b4e42ad43110e76e904c` |
| `Assets/Textures/art/Facility/Props/Stage2PowerCommunications.png` | 1448×1086 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2PowerCommunications | Stage2PowerCommunications (248,5,883,959) pivot(0.5, 0.5) | `4a7bb1d7a50585b4894d4fde7dff5fca` |
| `Assets/Textures/art/Facility/Props/Stage2ToolBench.png` | 1448×1086 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on | Stage2ToolBench | Stage2ToolBench (72,164,1314,592) pivot(0.5, 0.5) | `21533c02f68f65e4eb137f0b8905139f` |
| `Assets/Textures/art/Facility/Props/Stage3NuclearProtection.png` | 1448×1086 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | Stage3NuclearProtection | Stage3NuclearProtection (78,91,1296,872) pivot(0.5, 0.5) | `56021a32eecae564ca4d5a558d712c43` |
| `Assets/Textures/art/Facility/Props/Stage3PrecisionElectronics.png` | 1672×941 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | Stage3PrecisionElectronics | Stage3PrecisionElectronics (2,43,1660,892) pivot(0.5, 0.5) | `92e018ba77d83b2468945383b01f7aab` |
| `Assets/Textures/art/Facility/Workbench/Stage2TopDownWorkbench.png` | 335×187 | Sprite/Single | 100 | 0 | 2048 | None | off | off | on | Stage2TopDownWorkbench |  | `b0a564e79bf7f3941875c1e7372b90aa` |
| `Assets/Textures/art/Facility/Workbench/TopDownWorkbench.png` | 209×117 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | TopDownWorkbench |  | `2a170e436543a5647a171a435e7ba07d` |
| `Assets/Textures/art/Products/CannedFood.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | CannedFood | CannedFood (84,72,1087,1153) pivot(0.5, 0.5) | `5de934797e6d0f04a9ef01fc3692fa97` |
| `Assets/Textures/art/Products/DrinkingWater.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | DrinkingWater | DrinkingWater (103,0,1035,1233) pivot(0.5, 0.5) | `57fd43ccaee8ecd4a8736fb5297b22df` |
| `Assets/Textures/art/Products/DryBattery.png` | 1230×1278 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | DryBattery | DryBattery (0,26,1210,1222) pivot(0.5, 0.5) | `6aaa838615306cf4d8ba25c85ead22d6` |
| `Assets/Textures/art/Products/EmergencyInjection.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | EmergencyInjection | EmergencyInjection (97,40,1103,1161) pivot(0.5, 0.5) | `9e219d8862ff7c54bb9a76b477f3838c` |
| `Assets/Textures/art/Products/Flashlight.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | Flashlight | Flashlight (97,26,1095,1207) pivot(0.5, 0.5) | `feaf852d7b25364469a2f4c7db58631f` |
| `Assets/Textures/art/Products/FoldingShovel.png` | 1215×1295 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | FoldingShovel | FoldingShovel (0,0,932,1257) pivot(0.5, 0.5) | `23fc9344777e016438d26b8c7b9a8fc2` |
| `Assets/Textures/art/Products/GasMask.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | GasMask | GasMask (0,0,1185,1210) pivot(0.5, 0.5) | `4d7494db5f40d4a448a7b2acdb6d3cdc` |
| `Assets/Textures/art/Products/Legacy/Can.png` | 1254×1254 | Sprite/Single | 100 | 0 | 128 | None | off | off | on | Can |  | `0bd04b7017c4fa84094c818943317338` |
| `Assets/Textures/art/Products/Legacy/Crackers.png` | 1254×1254 | Sprite/Single | 100 | 0 | 128 | None | off | off | on | Crackers |  | `80a3439022bd3054ab1d7a3a9a33cc0f` |
| `Assets/Textures/art/Products/Legacy/Rice.png` | 1536×1024 | Sprite/Single | 100 | 0 | 128 | None | off | off | on | Rice |  | `a6836116f5e94f448a0af82c6e258f8b` |
| `Assets/Textures/art/Products/Legacy/Water.png` | 1024×1536 | Sprite/Single | 100 | 0 | 128 | None | off | off | on | Water |  | `2ed767a5d47f8114088e0e9fae6c333f` |
| `Assets/Textures/art/Products/MedicalBandage.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | MedicalBandage | MedicalBandage (59,58,1132,1146) pivot(0.5, 0.5) | `cddff39dd9750b14593a5dfb0ddb772b` |
| `Assets/Textures/art/Products/Medicine.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | Medicine | Medicine (55,72,994,1068) pivot(0.5, 0.5) | `303dc58656fbec341a80658cbda9b548` |
| `Assets/Textures/art/Products/MilitaryRation.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | MilitaryRation | MilitaryRation (49,40,1155,1161) pivot(0.5, 0.5) | `334584a1119da584ab0346d127869d09` |
| `Assets/Textures/art/Products/NutritionBar.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | NutritionBar | NutritionBar (210,0,751,1212) pivot(0.5, 0.5) | `51da5fd132c97d54383f70be61cac3b1` |
| `Assets/Textures/art/Products/PowerBattery.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | PowerBattery | PowerBattery (59,26,1133,1205) pivot(0.5, 0.5) | `8b23b5ed8030f824abf68d908b57607e` |
| `Assets/Textures/art/Products/ProtectiveSuit.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | ProtectiveSuit | ProtectiveSuit (0,0,1200,1233) pivot(0.5, 0.5) | `b07599151bbd35f48bc6b995e34f3af1` |
| `Assets/Textures/art/Products/RadiationDetector.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | RadiationDetector | RadiationDetector (97,16,952,1200) pivot(0.5, 0.5) | `a0a1efba266304b44814d2b8d7750bf2` |
| `Assets/Textures/art/Products/Radio.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | Radio | Radio (41,0,1151,1203) pivot(0.5, 0.5) | `0acb55395f9655a4d97c529d2b1d0288` |
| `Assets/Textures/art/Products/ThermalCamera.png` | 1254×1254 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | ThermalCamera | ThermalCamera (89,32,1121,1203) pivot(0.5, 0.5) | `3a45fc018e0f0f74ea54d25b62394157` |
| `Assets/Textures/art/UI/DailyInstruction.png` | 1122×1402 | Sprite/Single | 100 | 0 | 2048 | None | off | off | on | DailyInstruction |  | `91cbf5fa51af4690bed80b78400fcbdb` |
| `Assets/Textures/art/UI/DailyLedger.png` | 334×188 | Sprite/Single | 100 | 0 | 2048 | None | off | off | on | DailyLedger |  | `8d3c27e5fb984a4e979b295e18ee51b2` |
| `Assets/Textures/art/UI/DialogueFrame.png` | 2172×724 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | DialogueFrame | DialogueFrame (78,212,2016,306) pivot(0.5, 0.5) | `c098d249b6e102c46a26d2be06dbd308` |
| `Assets/Textures/art/UI/InstructionStartStamp.png` | 1660×948 | Sprite/Single | 100 | 0 | 2048 | None | off | off | on | InstructionStartStamp |  | `92448e477998d824896a965dabe399bf` |
| `Assets/Textures/art/UI/LedgerDaughter.png` | 144×108 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on | LedgerDaughter_0 | LedgerDaughter_0 (23,9,102,92) pivot(0, 0) | `1c6d94798c72c534b87845aed78eac43` |
| `Assets/Textures/art/UI/LedgerDrawing.png` | 134×168 | Sprite/Multiple | 100 | 0 | 2048 | None | off | off | on |  +Texture2D |  | `55949d20b4695ef4cb924f3ad2ff79e3` |
| `Assets/Textures/art/UI/TradeReactions.png` | 1254×1254 | Sprite/Multiple | 100 | 1 | 2048 | None | off | off | on |  +Texture2D |  | `330cb57e802eb214088fe60f7e5eb063` |
| `Assets/Textures/art/Workbench/Calculator.png` | 1254×1254 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | Calculator |  | `e4e4da47c41746288e9e526d48ba51cd` |
| `Assets/Textures/art/Workbench/CalculatorToggle.png` | 1254×1254 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | CalculatorToggle |  | `8b11ce334813452e934fcdfb82c8c277` |
| `Assets/Textures/art/Workbench/DividerBar.png` | 1774×887 | Sprite/Single | 500 | 0 | 2048 | None | off | off | on | DividerBar |  | `5c4c5257f2ef8b646b0183430ca65218` |
| `Assets/Textures/art/Workbench/TopDownCan.png` | 128×128 | Sprite/Single | 128 | 0 | 128 | None | off | off | on | TopDownCan |  | `ba5d7de8656dcad438db313d714cd6d1` |
| `Assets/Textures/art/Workbench/TopDownContainerEmpty.png` | 1254×1254 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | TopDownContainerEmpty |  | `5058e7ded0fcdd049997cb7d4bef30f6` |
| `Assets/Textures/art/Workbench/TopDownContainerTilted.png` | 1254×1254 | Sprite/Single | 128 | 0 | 2048 | None | off | off | on | TopDownContainerTilted |  | `6866e4272ad30b44c92d59358a3a12f6` |
| `Assets/Textures/art/Workbench/TopDownCrackers.png` | 128×128 | Sprite/Single | 128 | 0 | 128 | None | off | off | on | TopDownCrackers |  | `4050b76a7ca45de4ab139d77bfbfdd2b` |
| `Assets/Textures/art/Workbench/TopDownRiceRound.png` | 128×128 | Sprite/Single | 128 | 0 | 128 | None | off | off | on | TopDownRiceRound |  | `b5572b8887a505448a991dbc0fe7aaa4` |
| `Assets/Textures/art/Workbench/TopDownWater.png` | 128×128 | Sprite/Single | 128 | 0 | 128 | None | off | off | on | TopDownWater |  | `85116844fe2300d47a1e7ed17365fdbf` |
| `Assets/Textures/art/Workbench/Vacuum.png` | 102×128 | Sprite/Single | 40 | 0 | 2048 | None | off | off | on | Vacuum |  | `8c5509423fa570745bc62cafae857f42` |

참고: `art/Customer/Male|Female` 60장은 `DystopiaScreen.maleCustomers/femaleCustomers`와 `DystopiaTopDownTest`의 같은 두 배열에 30개씩 순서대로 들어 있다(순서 규칙은 `doc/CUSTOMER_ARTWORK_GUIDE.md` 3절). 노멀맵 60장은 폴더에 있지만 이 씬의 `normalVariants`는 비어 있어 **이 단계에서는 사용하지 않는다**.

### 3.2 씬이 직접 참조하지 않지만 함께 전달해야 하는 파일

| 파일 | 이유 |
|---|---|
| `art/Characters/Inspector/InspectorHead.png` | `InspectorPortrait.prefab`의 비활성 `Head` RawImage가 참조 |
| `art/Customer/NormalMap/*` 60장 | 노멀맵 대응표 등록 시 사용(현재 미등록) |
| `art/Facility/{Frame,Clock,Crate,CounterTop,Props}/Stage*` 중 3.1 표에 없는 파일 | 다른 단계 기준 씬과 적용 메뉴가 사용. 이 씬에도 비활성 오브젝트가 다른 단계 Sprite를 참조할 수 있다(3.1 표에 포함) |
| `art/UI/Hands.png`, `art/Workbench/*` | 탑다운 작업 화면(`DystopiaTopDownTest`)이 에디터에서 경로로 보완 로드 |
| `art/Products/Legacy/*` | `Product0~3.prefab`(탑다운 물리 물품)이 Can/Crackers/Rice/Water 참조 |

## 4. DystopiaPixelStage — 렌더 레이어 54개와 조명 설정

컴포넌트 위치: `TopDownCheckout` (DystopiaTopDownTest, DystopiaDayNight, DystopiaPixelStage 3개가 같은 오브젝트). `layers` 배열 순서가 **최종 그리기 순서(0=가장 뒤)** 이며 Hierarchy 순서와 별개로 이 배열이 우선한다.

### 4.1 전역 설정 (Inspector 저장값)

| 필드 | 값 | 의미 |
|---|---|---|
| `frontCanvas` | → `FrontCounter/DystopiaCanvas` (RectTransform) | 이 Canvas가 비활성이면 렌더 정지 |
| `lightingShader` | `Assets/Shaders/Checkout/PixelStageLighting.shader` | 픽셀 조명 셰이더 |
| `width` | 480 | 고정 렌더 폭(높이 = 9/16) |
| `matchScreenResolution` | 0 | true면 화면 해상도 1:1 |
| `previewInEditor` | 1 | 편집 중에도 렌더 |
| `lampPosition` | (640, 64) | ceilingLamp가 없을 때 전등 위치(좌상단 기준) |
| `ceilingLamp` | → `FrontCounter/DystopiaCanvas/Stage3CeilingLamp` (Image) | 연결된 천장등(비활성 오브젝트) — 이름이 Stage3CeilingLamp면 화면 해상도 렌더로 전환됨. Stage 1 씬에서는 오브젝트가 비활성이라 위치만 참조되고 해상도 전환은 `UseScreenResolution`이 이름만 보므로 **주의: 이름 조건이 참이면 480 폭 무시** — 아래 5.1 참고 |
| `lampHeight` | 240 | 가상 광원 깊이 |
| `lampRadius` | 700 | 전등 영향 반경 |
| `lampIntensity` | 1.6 | 밤 전등 강도 |
| `lampColor` | RGBA(0.95, 0.92, 0.82, 1) #F2EBD1FF | 전등 색 |
| `lightingSteps` | 6 | 명암 단계 |
| `rimIntensity` | 1.3 | 외곽광 |
| `dawnFog` | RGBA(1, 0.84, 0.67, 1) #FFD6ABFF | 아침 안개색 |
| `sunsetFog` | RGBA(1, 0.54, 0.28, 1) #FF8A47FF | 석양 안개색 |
| `nightFog` | RGBA(0.28, 0.39, 0.59, 1) #476396FF | 밤 안개색 |
| `skyGlowIntensity` | 0.3 | 구름빛 |
| `daylightContrast` | 0.65 | 낮 대비 |
| `sunsetContrast` | 0.8 | 석양 대비 |
| `daylightShadowOpacity` | 0.3 | 낮 손님 그림자 |
| `sunsetShadowOpacity` | 0.55 | 석양 손님 그림자 |
| `hourlyAmbient` | AnimationCurve[(9→1), (21→1)] | 시간별 곡선(전용 편집창) |
| `hourlySunlight` | AnimationCurve[(9→1), (21→1)] | 시간별 곡선(전용 편집창) |
| `hourlyNormal` | AnimationCurve[(9→1), (21→1)] | 시간별 곡선(전용 편집창) |
| `hourlyHardness` | AnimationCurve[(9→1), (21→1)] | 시간별 곡선(전용 편집창) |
| `hourlyFill` | AnimationCurve[(9→0.15), (21→0.15)] | 시간별 곡선(전용 편집창) |
| `hourlyShadow` | AnimationCurve[(9→1), (21→1)] | 시간별 곡선(전용 편집창) |
| `hourlySunX` | AnimationCurve[(9→0), (21→0)] | 시간별 곡선(전용 편집창) |
| `hourlySunY` | AnimationCurve[(9→0), (21→0)] | 시간별 곡선(전용 편집창) |
| `relightingTrial` | 0 | 노멀맵·실내 반사광 시험 스위치 |
| `useCustomerNormalMap` | 0 | 손님 노멀맵 |
| `normalStrength` | 0.65 | 인물 노멀 강도 |
| `propNormalStrength` | 1 | 소품 노멀 강도 |
| `propNightFill` | 0.3 | 소품 야간 최소 밝기 |
| `roomLightStrength` | 1.2 | 실내 반사광 |
| `eveningSpotlight` | 1 | 저녁 집중광 |
| `spotOrigin` | (640, 64) | 집중광 시작(좌상단 기준) |
| `spotTarget` | (640, 530) | 집중광 목표 |
| `spotIntensity` | 1.4 | 집중광 세기 |
| `spotHalfAngle` | 32 | 원뿔 반각 |
| `spotSoftness` | 0.65 | 경계 부드러움 |
| `spotHaze` | 0.009 | 빛줄기 불투명도 |
| `towerBacklight` | 3.2 | 감시탑 역광 |
| `customerShadowOpacity` | 0.65 | 상판 손님 그림자 |
| `shadowTableY` | (397, 689) | 상판 뒤/앞 경계 Y(좌상단 기준) |

### 4.2 레이어 표 (배열 순서 = 그리기 순서)

**주의:** 이 권위 씬은 `softContactShadow, projectedContactShadow` 필드가 코드에 추가되기 전에 저장되어 레이어에 그 값이 없다. Unity가 로드하면 기본값 false로 읽히고, `Apply Stage 2 Shop`이 리플렉션으로 복사할 때도 false가 적용된다. 아래 표의 soft/projected 열 0은 그 기본값이다.

Surface: 0 Unlit · 1 Environment · 2 Person · 3 Metal · 4 CityLights · 5 OriginalEffect(원본 머티리얼 유지, 안개) · 6 Hidden(렌더 제외) · 7 Sky. contactShadow=(접점X, 접점Y, 폭배율, 높이배율), 높이 0이면 미사용.

| # | source (오브젝트) | Surface | lamp | room | rimW | rim | normal | highlight | specular | emission | bottomShade | contactShadow | soft | projected | edgeTrim | normalSprite/normalMap |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | `FrontCounter/DystopiaCanvas/FarBackground` | Sky | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 1 | `FrontCounter/DystopiaCanvas/DawnBackground` | Sky | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 2 | `FrontCounter/DystopiaCanvas/SunsetBackground` | Sky | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 3 | `FrontCounter/DystopiaCanvas/EveningBackground` | Sky | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 4 | `FrontCounter/DystopiaCanvas/CityLights` | CityLights | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 5 | `FrontCounter/DystopiaCanvas/Fog_Back` | OriginalEffect | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 6 | `FrontCounter/DystopiaCanvas/Fog_Mid` | OriginalEffect | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 7 | `FrontCounter/DystopiaCanvas/Fog_Front` | OriginalEffect | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 8 | `FrontCounter/DystopiaCanvas/MidBackground` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 9 | `FrontCounter/DystopiaCanvas/LeftChimneySmoke` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 10 | `FrontCounter/DystopiaCanvas/RightChimneySmoke` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 11 | `FrontCounter/DystopiaCanvas/LeftWatchGuard` | Person | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 12 | `FrontCounter/DystopiaCanvas/RightWatchGuard` | Person | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 13 | `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1/Core` | Unlit | 0 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 14 | `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1/Spark` | Unlit | 0 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 15 | `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1/Flame` | Unlit | 0 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 16 | `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0/Core` | Unlit | 0 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 17 | `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0/Spark` | Unlit | 0 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 18 | `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0/Flame` | Unlit | 0 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 19 | `FrontCounter/DystopiaCanvas/LeftSearchlight` | Unlit | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 20 | `FrontCounter/DystopiaCanvas/RightSearchlight` | Unlit | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 21 | `FrontCounter/DystopiaCanvas/CrowdRow0` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 22 | `FrontCounter/DystopiaCanvas/CrowdRow1` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 23 | `FrontCounter/DystopiaCanvas/CrowdRow2` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 24 | `FrontCounter/DystopiaCanvas/Barricade` | Environment | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 25 | `FrontCounter/DystopiaCanvas/WaitingRear` | Person | 0.2 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 26 | `FrontCounter/DystopiaCanvas/WaitingLeft` | Person | 0.45 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 27 | `FrontCounter/DystopiaCanvas/Customer` | Person | 1 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 28 | `FrontCounter/DystopiaCanvas/Canopy` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 29 | `FrontCounter/DystopiaCanvas/Stage3LeftPillar` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 30 | `FrontCounter/DystopiaCanvas/Stage3RightPillar` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 31 | `FrontCounter/DystopiaCanvas/Counter/CounterLeftExtension` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 32 | `FrontCounter/DystopiaCanvas/Counter/CounterRightExtension` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 33 | `FrontCounter/DystopiaCanvas/Counter` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 34 | `FrontCounter/DystopiaCanvas/FacilityNuclearProtection` | Metal | 0.65 | 0.3 | 1 | 0 | 0.55 | 0.9 | 0.2 | 0 | 0.3 | (0.5, 0, 1.06, 0.22) | 0 | 0 | (0, 0) |  |
| 35 | `FrontCounter/DystopiaCanvas/FacilityPrecisionElectronics` | Metal | 0.65 | 0.3 | 1 | 0 | 0.55 | 0.9 | 0.2 | 0 | 0.3 | (0.5, 0, 1.06, 0.22) | 0 | 0 | (0, 0) |  |
| 36 | `FrontCounter/DystopiaCanvas/FacilityToolBench` | Metal | 0.65 | 0.3 | 1 | 0 | 0.55 | 0.9 | 0.2 | 0 | 0.3 | (0.5, 0, 1.06, 0.22) | 0 | 0 | (0, 0) |  |
| 37 | `FrontCounter/DystopiaCanvas/FacilityPowerCommunications` | Metal | 0.65 | 0.3 | 1 | 0 | 0.55 | 0.9 | 0.2 | 0 | 0.3 | (0.5, 0, 1.06, 0.22) | 0 | 0 | (0, 0) |  |
| 38 | `FrontCounter/DystopiaCanvas/FacilityFoodShelf` | Metal | 0.65 | 0.3 | 1 | 0 | 0.55 | 0.9 | 0.2 | 0 | 0.3 | (0.5, 0, 1.06, 0.22) | 0 | 0 | (0, 0) |  |
| 39 | `FrontCounter/DystopiaCanvas/FacilityMedicineCabinet` | Metal | 0.65 | 0.3 | 1 | 0 | 0.55 | 0.9 | 0.2 | 0 | 0.3 | (0.5, 0, 1.06, 0.22) | 0 | 0 | (0, 0) |  |
| 40 | `FrontCounter/DystopiaCanvas/CounterLamplight` | Hidden | 0.15 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 41 | `FrontCounter/DystopiaCanvas/Stage3CeilingLamp` | Unlit | 0.4 | 0 | 1 | 0.1 | 0.55 | 0.75 | 0.12 | 1.5 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) | `Assets/Textures/art/Facility/Frame/Stage3Shop.png` → Sprite `Stage3CeilingLamp` / 없음(None) |
| 42 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust0` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 43 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust1` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 44 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust2` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 45 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust3` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 46 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust4` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 47 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust5` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 48 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust6` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 49 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust7` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 50 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust8` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 51 | `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust9` | Environment | 0.7 | 0 | 3 | 1 | 1 | 1 | 1 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |
| 52 | `TopDownCheckout/TopDownTestCanvas/FrontView/FrontContainer` | Metal | 0.65 | 0 | 1 | 0 | 0 | 0.9 | 0.08 | 0 | 0.3 | (0.5, 0, 1.06, 0.26) | 0 | 0 | (0, 0) |  |
| 53 | `TopDownCheckout/TopDownTestCanvas/CounterClock` | Metal | 0.65 | 0.3 | 1 | 0.05 | 0 | 0.8 | 0.08 | 0 | 0 | (0, 0, 0, 0) | 0 | 0 | (0, 0) |  |

레이어에 없는 Canvas 오브젝트(텍스트, DialoguePanel, 버튼, 지침서, 가계부, Modal)는 픽셀 렌더를 거치지 않고 원래 Canvas에 그려진다.

## 5. DystopiaDayNight — 시간대 혼합 설정

| 필드 | 값 |
|---|---|
| `clock` | → `TopDownCheckout` (DystopiaTopDownTest) |
| `pixelStage` | → `TopDownCheckout` (DystopiaPixelStage) |
| `dawn` | → `FrontCounter/DystopiaCanvas/DawnBackground` (Image) |
| `evening` | → `FrontCounter/DystopiaCanvas/EveningBackground` (Image) |
| `cityLights` | → `FrontCounter/DystopiaCanvas/CityLights` (Image) |
| `counterLight` | → `FrontCounter/DystopiaCanvas/CounterLamplight` (Image) |
| `sunset` | → `FrontCounter/DystopiaCanvas/SunsetBackground` (Image) |
| `sunsetStart` | 15 |
| `sunsetTint` | RGBA(1, 0.72, 0.49, 1) #FFB87DFF |
| `leftBeam` | → `FrontCounter/DystopiaCanvas/LeftSearchlight` (Image) |
| `rightBeam` | → `FrontCounter/DystopiaCanvas/RightSearchlight` (Image) |
| `environment` | [→ `FrontCounter/DystopiaCanvas/MidBackground` (Image), → `FrontCounter/DystopiaCanvas/Fog_Back` (Image), → `FrontCounter/DystopiaCanvas/Fog_Mid` (Image), → `FrontCounter/DystopiaCanvas/Fog_Front` (Image), → `FrontCounter/DystopiaCanvas/LeftWatchTower` (Image), → `FrontCounter/DystopiaCanvas/RightWatchTower` (Image), → `FrontCounter/DystopiaCanvas/Canopy` (Image), → `FrontCounter/DystopiaCanvas/Barricade` (Image), → `FrontCounter/DystopiaCanvas/Counter` (Image), → `FrontCounter/DystopiaCanvas/CrowdRow0` (DystopiaCrowdImage), → `FrontCounter/DystopiaCanvas/CrowdRow1` (DystopiaCrowdImage), → `FrontCounter/DystopiaCanvas/CrowdRow2` (DystopiaCrowdImage)] |
| `people` | [→ `FrontCounter/DystopiaCanvas/Customer` (Image), → `FrontCounter/DystopiaCanvas/WaitingLeft` (Image), → `FrontCounter/DystopiaCanvas/WaitingRear` (Image)] |
| `preview` | 1 |
| `previewHour` | 9 |
| `dawnStart` | 9 |
| `dayStart` | 12 |
| `eveningStart` | 18 |
| `nightEnd` | 21 |
| `dawnTint` | RGBA(1, 0.9, 0.8, 1) #FFE6CCFF |
| `nightTint` | RGBA(0.384078, 0.473474, 0.59434, 1) #627998FF |
| `peopleBrightness` | 0.72 |
| `cityIntensity` | 0.7 |
| `beamIntensity` | 0.11 |
| `counterIntensity` | 0.12 |

시간 흐름: `dawnStart`~`dayStart` 아침→낮, `sunsetStart`~`eveningStart` 석양, `eveningStart`~`nightEnd` 밤. 낮(12~15시)은 원본 색 그대로. Play 중 시각 권위는 `DystopiaTopDownTest.BusinessMinute`.

### 5.1 주의: ceilingLamp 이름 조건

`DystopiaPixelStage.UseScreenResolution => matchScreenResolution || (ceilingLamp != null && ceilingLamp.name == "Stage3CeilingLamp")`. 이 씬의 `ceilingLamp`는 `FrontCounter/DystopiaCanvas/Stage3CeilingLamp` (비활성 오브젝트) 이므로 조건은 **참** → 실제 렌더 폭은 `Screen.width`(고해상도 1:1, Bilinear). `width` 값은 조건이 거짓일 때만 쓰인다. 도트/고해상도를 바꾸려면 `ceilingLamp` 연결 또는 `matchScreenResolution`을 조정한다(이번 인계에서는 저장값 그대로 둔다).

## 6. 씬 전체 계층 — 모든 오브젝트·컴포넌트·값

표기: `anchoredPosition (x, y)`는 부모 앵커 기준. 좌상단 앵커/피벗 오브젝트는 `→` 줄에 1280×720 좌상단 기준 픽셀 사각형을 환산해 두었다. 자식은 부모 사각형 기준이다. `⛔ 비활성`은 GameObject가 꺼진 상태(저장값)이며 코드가 조건부로 켠다.

### `DystopiaCamera`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **Camera**
  - clearFlags 2 · background RGBA(0, 0, 0, 1) #000000FF · orthographic true · size 5 · near 0.3 · far 1000 · depth 0 · cullingMask {m_Bits=4294967295} · viewport {x=0, y=0, width=1, height=1} · HDR 1 · MSAA 1 · targetDisplay 0
- **UniversalAdditionalCameraData**
  - m_RenderShadows = 1
  - m_RequiresDepthTextureOption = 2
  - m_RequiresOpaqueTextureOption = 2
  - m_CameraType = 0
  - m_Cameras = [] (비어 있음)
  - m_RendererIndex = -1
  - m_VolumeLayerMask = {m_Bits=1}
  - m_VolumeTrigger = 없음(None)
  - m_VolumeFrameworkUpdateModeOption = 2
  - m_RenderPostProcessing = 0
  - m_Antialiasing = 0
  - m_AntialiasingQuality = 2
  - m_StopNaN = 0
  - m_Dithering = 0
  - m_ClearDepth = 1
  - m_AllowXRRendering = 1
  - m_AllowHDROutput = 1
  - m_UseScreenCoordOverride = 0
  - m_ScreenSizeOverride = (0, 0, 0, 0)
  - m_ScreenCoordScaleBias = (0, 0, 0, 0)
  - m_RequiresDepthTexture = 0
  - m_RequiresColorTexture = 0
  - m_TaaSettings = {m_Quality=3, m_FrameInfluence=0.1, m_JitterScale=1, m_MipBias=0, m_VarianceClampScale=0.9, m_ContrastAdaptiveSharpening=0}
  - m_Version = 2

### `FrontCounter`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **DystopiaScreen**
  - childPortraitScale = 0.4
  - childPortraitRise = 0.18
  - maleBreathing = [30개] 0:Normal, 1:Normal, 2:Normal, 3:Normal, 4:Normal, 5:Normal, 6:Normal, 7:Normal, 8:Normal, 9:Normal, 10:Normal, 11:Normal, 12:Heavy, 13:Heavy, 14:Heavy, 15:Normal, 16:Normal, 17:Normal, 18:Normal, 19:Normal, 20:Normal, 21:Heavy, 22:Heavy, 23:Heavy, 24:Normal, 25:Normal, 26:Normal, 27:Elderly, 28:Elderly, 29:Elderly (Sprite 배열과 같은 순서; 0 Normal 1 Heavy 2 Elderly)
  - femaleBreathing = [30개] 0:Normal, 1:Normal, 2:Normal, 3:Normal, 4:Normal, 5:Normal, 6:Normal, 7:Normal, 8:Normal, 9:Normal, 10:Normal, 11:Normal, 12:Heavy, 13:Heavy, 14:Heavy, 15:Normal, 16:Normal, 17:Normal, 18:Normal, 19:Normal, 20:Normal, 21:Heavy, 22:Heavy, 23:Heavy, 24:Normal, 25:Normal, 26:Normal, 27:Elderly, 28:Elderly, 29:Elderly (Sprite 배열과 같은 순서; 0 Normal 1 Heavy 2 Elderly)
  - calculatorArtwork = `Assets/Textures/art/Workbench/Calculator.png` → Sprite `Calculator`
  - calculatorToggleArtwork = `Assets/Textures/art/Workbench/CalculatorToggle.png` → Sprite `CalculatorToggle`
  - counterClockArtwork = `Assets/Textures/art/Facility/Clock/CounterClock.png` → Sprite `CounterClock`
  - calculatorLayout = {x=900, y=310, width=360, height=360}
  - calculatorToggleLayout = {x=1214, y=659, width=54, height=58}
  - counterClockLayout = {x=1090, y=380, width=180, height=180}
  - settings = {products=[{id=1, requiredFacility=0, name=`생수`, price=1000, firstDay=1, sprite=`Assets/Textures/art/Products/DrinkingWater.png` → Sprite `DrinkingWater`}, {id=2, requiredFacility=0, name=`통조림`, price=2500, firstDay=1, sprite=`Assets/Textures/art/Products/CannedFood.png` → Sprite `CannedFood`}, {id=3, requiredFacility=0, name=`붕대`, price=3000, firstDay=1, sprite=`Assets/Textures/art/Products/MedicalBandage.png` → Sprite `MedicalBandage`}, {id=4, requiredFacility=0, name=`건전지`, price=3500, firstDay=1, sprite=`Assets/Textures/art/Products/DryBattery.png` → Sprite `DryBattery`}, {id=5, requiredFacility=1, name=`군용식량`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/MilitaryRation.png` → Sprite `MilitaryRation`}, {id=6, requiredFacility=1, name=`영양바`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/NutritionBar.png` → Sprite `NutritionBar`}, {id=7, requiredFacility=2, name=`약통`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/Medicine.png` → Sprite `Medicine`}, {id=8, requiredFacility=2, name=`응급 주사`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/EmergencyInjection.png` → Sprite `EmergencyInjection`}, {id=9, requiredFacility=4, name=`손전등`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/Flashlight.png` → Sprite `Flashlight`}, {id=10, requiredFacility=4, name=`접이식 삽`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/FoldingShovel.png` → Sprite `FoldingShovel`}, {id=11, requiredFacility=8, name=`무전기`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/Radio.png` → Sprite `Radio`}, {id=12, requiredFacility=8, name=`배터리`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/PowerBattery.png` → Sprite `PowerBattery`}, {id=13, requiredFacility=16, name=`방독면`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/GasMask.png` → Sprite `GasMask`}, {id=14, requiredFacility=16, name=`방호복`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/ProtectiveSuit.png` → Sprite `ProtectiveSuit`}, {id=15, requiredFacility=32, name=`방사능 측정기`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/RadiationDetector.png` → Sprite `RadiationDetector`}, {id=16, requiredFacility=32, name=`열화상 카메라`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/ThermalCamera.png` → Sprite `ThermalCamera`}], shopStage=2, ownedFacilities=0, citizenshipPrice=300000, firstTribute=50000, secondTribute=80000, laterTribute=110000, baseVisitors=8, minVisitors=6, maxVisitors=12, gaugeStart=70, greenThreshold=70, gaugeDecay=2, gaugeRecovery=12, departureRecovery=30, departureCount=2, departureReputation=-5, greenReputation=1, toleranceReputation=-2, budgetReputation=-1, refusalMorality=-1, unpopularReputationMin=20, neutralReputationMin=40, popularReputationMin=60, trustedReputationMin=80, discountMorality=2, generousMorality=4, markupMorality=-2, poorChance=0.18, poorBudgetRatio=0.85, normalBudgetMinPercent=110, normalBudgetMaxPercent=150, resultSeconds=0.85}
  - background = `Assets/Textures/art/Background/FARBACKGROUND.png` → Sprite `FARBACKGROUND`
  - counter = `Assets/Textures/art/Facility/CounterTop/BoothCounter.png` → Sprite `BoothCounter`
  - daughter = **끊어진 참조** (guid e4dd2b06ebb59a14b9fe472c625c82c7, fileID 21300000)
  - inspector = `Assets/Textures/art/Characters/Inspector/Inspector.png` → Sprite `Inspector`
  - inspectorPortraitPrefab = `Assets/DystopiaPrototype/Prefabs/InspectorPortrait.prefab` (fileID 5937818634303120731)
  - midBackground = `Assets/Textures/art/Background/MidBackground.png` → Sprite `MidBackground`
  - leftWatchTower = `Assets/Textures/art/Background/LeftWatchTower.png` → Sprite `LeftWatchTower`
  - rightWatchTower = `Assets/Textures/art/Background/RightWatchTower.png` → Sprite `RightWatchTower`
  - canopy = `Assets/Textures/art/Facility/Frame/BoothCanopy.png` → Sprite `BoothCanopy`
  - crowdRows = [`Assets/Textures/art/Characters/Crowd/CrowdBack.png` → Sprite `CrowdBack`, `Assets/Textures/art/Characters/Crowd/CrowdMiddle.png` → Sprite `CrowdMiddle`, `Assets/Textures/art/Characters/Crowd/CrowdFront.png` → Sprite `CrowdFront`]
  - watchGuard = `Assets/Textures/art/Characters/Guard/WatchGuard.png` → Sprite `WatchGuard`
  - leftWatchGuardRect = {x=141, y=174, width=44, height=34}
  - rightWatchGuardRect = {x=1154, y=232, width=32, height=24}
  - dailyLedger = `Assets/Textures/art/UI/DailyLedger.png` → Sprite `DailyLedger`
  - ledgerPageInset = (4, 3)
  - ledgerDaughter = `Assets/Textures/art/UI/LedgerDaughter.png` → Sprite `LedgerDaughter_0`
  - ledgerSpeechBubble = 없음(None)
  - ledgerDrawing = `Assets/Textures/art/UI/LedgerDrawing.png` (Texture2D)
  - ledgerStamp = 없음(None)
  - ledgerStampPopular = 없음(None)
  - ledgerStampNeutral = 없음(None)
  - ledgerStampUnpopular = 없음(None)
  - ledgerStampNotorious = 없음(None)
  - ledgerDrawingLayout = {x=1035, y=24, width=190, height=238}
  - ledgerStampLayout = {x=27, y=51, width=136, height=136}
  - ledgerStampOpacity = 1
  - ledgerStampTint = RGBA(0.5, 0.65, 0.6, 1) #80A699FF
  - dailyInstruction = `Assets/Textures/art/UI/DailyInstruction.png` → Sprite `DailyInstruction`
  - instructionStartStamp = `Assets/Textures/art/UI/InstructionStartStamp.png` → Sprite `InstructionStartStamp`
  - uiFont = `Assets/Fonts/Checkout/Mulmaru.otf`
  - guardTone = `Assets/Materials/Checkout/GuardNeutral.mat`
  - leftTowerTone = `Assets/Materials/Checkout/LeftTowerNeutral.mat`
  - rightTowerTone = `Assets/Materials/Checkout/RightTowerNeutral.mat`
  - chimneySmokeFrames = [`Assets/Textures/art/Effects/Smoke/ChimneySmoke0.png` → Sprite `ChimneySmoke0`, `Assets/Textures/art/Effects/Smoke/ChimneySmoke1.png` → Sprite `ChimneySmoke1`, `Assets/Textures/art/Effects/Smoke/ChimneySmoke2.png` → Sprite `ChimneySmoke2`, `Assets/Textures/art/Effects/Smoke/ChimneySmoke3.png` → Sprite `ChimneySmoke3`]
  - barricade = `Assets/Textures/art/Background/BoothBarricade.png` → Sprite `BoothBarricade`
  - fogBack = `Assets/Textures/art/Effects/Fog/FogBack.png` → Sprite `FogBack`
  - fogMid = `Assets/Textures/art/Effects/Fog/FogMid.png` → Sprite `FogMid`
  - fogFront = `Assets/Textures/art/Effects/Fog/FogFront.png` → Sprite `FogFront`
  - fogBackMaterial = `Assets/Materials/Checkout/FogBack.mat`
  - fogMidMaterial = `Assets/Materials/Checkout/FogMid.mat`
  - fogFrontMaterial = `Assets/Materials/Checkout/FogFront.mat`
  - customers = [`Assets/Textures/art/Customer/Legacy/MaleCustomer0.png` → Sprite `MaleCustomer0`]
  - maleCustomers = [`Assets/Textures/art/Customer/Male/MaleNormal_01.png` → Sprite `MaleNormal_01`, `Assets/Textures/art/Customer/Male/MaleNormal_02.png` → Sprite `MaleNormal_02`, `Assets/Textures/art/Customer/Male/MaleNormal_03.png` → Sprite `MaleNormal_03`, `Assets/Textures/art/Customer/Male/MaleNormal_04.png` → Sprite `MaleNormal_04`, `Assets/Textures/art/Customer/Male/MaleNormal_05.png` → Sprite `MaleNormal_05`, `Assets/Textures/art/Customer/Male/MaleNormal_06.png` → Sprite `MaleNormal_06`, `Assets/Textures/art/Customer/Male/MaleNormal_07.png` → Sprite `MaleNormal_07`, `Assets/Textures/art/Customer/Male/MaleNormal_08.png` → Sprite `MaleNormal_08`, `Assets/Textures/art/Customer/Male/MaleNormal_09.png` → Sprite `MaleNormal_09`, `Assets/Textures/art/Customer/Male/MaleNormal_10.png` → Sprite `MaleNormal_10`, `Assets/Textures/art/Customer/Male/MaleNormal_11.png` → Sprite `MaleNormal_11`, `Assets/Textures/art/Customer/Male/MaleNormal_12.png` → Sprite `MaleNormal_12`, `Assets/Textures/art/Customer/Male/MaleHasty_01.png` → Sprite `MaleHasty_01`, `Assets/Textures/art/Customer/Male/MaleHasty_02.png` → Sprite `MaleHasty_02`, `Assets/Textures/art/Customer/Male/MaleHasty_03.png` → Sprite `MaleHasty_03`, `Assets/Textures/art/Customer/Male/MalePriceSensitive_01.png` → Sprite `MalePriceSensitive_01`, `Assets/Textures/art/Customer/Male/MalePriceSensitive_02.png` → Sprite `MalePriceSensitive_02`, `Assets/Textures/art/Customer/Male/MalePriceSensitive_03.png` → Sprite `MalePriceSensitive_03`, `Assets/Textures/art/Customer/Male/MaleWealthy_01.png` → Sprite `MaleWealthy_01`, `Assets/Textures/art/Customer/Male/MaleWealthy_02.png` → Sprite `MaleWealthy_02`, `Assets/Textures/art/Customer/Male/MaleWealthy_03.png` → Sprite `MaleWealthy_03`, `Assets/Textures/art/Customer/Male/MalePoor_01.png` → Sprite `MalePoor_01`, `Assets/Textures/art/Customer/Male/MalePoor_02.png` → Sprite `MalePoor_02`, `Assets/Textures/art/Customer/Male/MalePoor_03.png` → Sprite `MalePoor_03`, `Assets/Textures/art/Customer/Male/MaleChild_01.png` → Sprite `MaleChild_01`, `Assets/Textures/art/Customer/Male/MaleChild_02.png` → Sprite `MaleChild_02`, `Assets/Textures/art/Customer/Male/MaleChild_03.png` → Sprite `MaleChild_03`, `Assets/Textures/art/Customer/Male/MaleElder_01.png` → Sprite `MaleElder_01`, `Assets/Textures/art/Customer/Male/MaleElder_02.png` → Sprite `MaleElder_02`, `Assets/Textures/art/Customer/Male/MaleElder_03.png` → Sprite `MaleElder_03`]
  - femaleCustomers = [`Assets/Textures/art/Customer/Female/FemaleNormal_01.png` → Sprite `FemaleNormal_01`, `Assets/Textures/art/Customer/Female/FemaleNormal_02.png` → Sprite `FemaleNormal_02`, `Assets/Textures/art/Customer/Female/FemaleNormal_03.png` → Sprite `FemaleNormal_03`, `Assets/Textures/art/Customer/Female/FemaleNormal_04.png` → Sprite `FemaleNormal_04`, `Assets/Textures/art/Customer/Female/FemaleNormal_05.png` → Sprite `FemaleNormal_05`, `Assets/Textures/art/Customer/Female/FemaleNormal_06.png` → Sprite `FemaleNormal_06`, `Assets/Textures/art/Customer/Female/FemaleNormal_07.png` → Sprite `FemaleNormal_07`, `Assets/Textures/art/Customer/Female/FemaleNormal_08.png` → Sprite `FemaleNormal_08`, `Assets/Textures/art/Customer/Female/FemaleNormal_09.png` → Sprite `FemaleNormal_09`, `Assets/Textures/art/Customer/Female/FemaleNormal_10.png` → Sprite `FemaleNormal_10`, `Assets/Textures/art/Customer/Female/FemaleNormal_11.png` → Sprite `FemaleNormal_11`, `Assets/Textures/art/Customer/Female/FemaleNormal_12.png` → Sprite `FemaleNormal_12`, `Assets/Textures/art/Customer/Female/FemaleHasty_01.png` → Sprite `FemaleHasty_01`, `Assets/Textures/art/Customer/Female/FemaleHasty_02.png` → Sprite `FemaleHasty_02`, `Assets/Textures/art/Customer/Female/FemaleHasty_03.png` → Sprite `FemaleHasty_03`, `Assets/Textures/art/Customer/Female/FemalePriceSensitive_01.png` → Sprite `FemalePriceSensitive_01`, `Assets/Textures/art/Customer/Female/FemalePriceSensitive_02.png` → Sprite `FemalePriceSensitive_02`, `Assets/Textures/art/Customer/Female/FemalePriceSensitive_03.png` → Sprite `FemalePriceSensitive_03`, `Assets/Textures/art/Customer/Female/FemaleWealthy_01.png` → Sprite `FemaleWealthy_01`, `Assets/Textures/art/Customer/Female/FemaleWealthy_02.png` → Sprite `FemaleWealthy_02`, `Assets/Textures/art/Customer/Female/FemaleWealthy_03.png` → Sprite `FemaleWealthy_03`, `Assets/Textures/art/Customer/Female/FemalePoor_01.png` → Sprite `FemalePoor_01`, `Assets/Textures/art/Customer/Female/FemalePoor_02.png` → Sprite `FemalePoor_02`, `Assets/Textures/art/Customer/Female/FemalePoor_03.png` → Sprite `FemalePoor_03`, `Assets/Textures/art/Customer/Female/FemaleChild_01.png` → Sprite `FemaleChild_01`, `Assets/Textures/art/Customer/Female/FemaleChild_02.png` → Sprite `FemaleChild_02`, `Assets/Textures/art/Customer/Female/FemaleChild_03.png` → Sprite `FemaleChild_03`, `Assets/Textures/art/Customer/Female/FemaleElder_01.png` → Sprite `FemaleElder_01`, `Assets/Textures/art/Customer/Female/FemaleElder_02.png` → Sprite `FemaleElder_02`, `Assets/Textures/art/Customer/Female/FemaleElder_03.png` → Sprite `FemaleElder_03`]
  - tradeReactionImage = 없음(None)
  - tradeReactionSprites = [] (비어 있음)
  - tradeReactionSheet = `Assets/Textures/art/UI/TradeReactions.png` (Texture2D)

#### `FrontCounter/DystopiaCanvas`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (0, 0) · localScale (0, 0, 0)
  - anchorMin (0, 0) · anchorMax (0, 0) (좌하단 고정) · pivot (0, 0)
  - localRotation 0° (identity)
- **Canvas**
  - renderMode 0 (0=ScreenSpaceOverlay) · sortingOrder 100 · pixelPerfect false · targetDisplay 0 · additionalShaderChannels 0 · vertexColorAlwaysGammaSpace false
- **CanvasScaler**
  - uiScaleMode 1 (1=ScaleWithScreenSize) · referenceResolution (1280, 720) · screenMatchMode 1 · matchWidthOrHeight 0 · referencePixelsPerUnit 100 · dynamicPixelsPerUnit 1
- **GraphicRaycaster**
  - ignoreReversedGraphics true · blockingObjects 0 · blockingMask {m_Bits=4294967295}

##### `FrontCounter/DystopiaCanvas/FarBackground`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (52, -42) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 42, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/FARBACKGROUND.png` → Sprite `FARBACKGROUND`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/DawnBackground`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (52, -42) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 42, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/Dawn.png` → Sprite `Dawn`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/SunsetBackground`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (52, -42) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 42, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/SunsetClouded.png` → Sprite `SunsetClouded`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/EveningBackground`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (52, -42) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 42, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/Evening.png` → Sprite `Evening`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/CityLights`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (52, -42) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 42, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/CityLights.png` → Sprite `CityLights`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/CityLights.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/LeftChimneySmoke`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (253.036, -80.0241) · sizeDelta(W×H) (56.9625, 125.869) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 253.036, 위 80.0241, 폭 56.9625, 높이 125.869 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/Smoke/ChimneySmoke0.png` → Sprite `ChimneySmoke0`
  - color RGBA(0.67, 0.7, 0.73, 0.48) #ABB2BA7A · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/RightChimneySmoke`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1110.4, -63.1313) · sizeDelta(W×H) (43.1812, 97.3875) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1110.4, 위 63.1313, 폭 43.1812, 높이 97.3875 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/Smoke/ChimneySmoke1.png` → Sprite `ChimneySmoke1`
  - color RGBA(0.67, 0.7, 0.73, 0.48) #ABB2BA7A · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Fog_Back`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (52, 316.312) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 -316.312, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/Fog/FogBack.png` → Sprite `FogBack`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/FogBack.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Fog_Mid`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (52, 316.312) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 -316.312, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/Fog/FogMid.png` → Sprite `FogMid`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/FogMid.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Fog_Front`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (52, 316.312) · sizeDelta(W×H) (1176, 661.5) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 -316.312, 폭 1176, 높이 661.5 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/Fog/FogFront.png` → Sprite `FogFront`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/FogFront.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/MidBackground`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (52, 0.262512) · sizeDelta(W×H) (1176, 529.2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 52, 위 -0.262512, 폭 1176, 높이 529.2 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/MidBackground.png` → Sprite `MidBackground`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/LeftSearchlight`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (126.203, -172.669) · sizeDelta(W×H) (1653.75, 330.75) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 0.5)
  - localRotation z = -15.35°
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/Searchlight.png` → Sprite `Searchlight`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/RightSearchlight`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (1159.78, -234.813) · sizeDelta(W×H) (1653.75, 336.875) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 0.5)
  - localRotation z = -169.2°
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/Searchlight.png` → Sprite `Searchlight`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/CrowdRow0`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (48.325, 67.3312) · sizeDelta(W×H) (1183.35, 899.456) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 48.325, 위 -67.3312, 폭 1183.35, 높이 899.456 (scale 적용)
- **DystopiaCrowdImage**
  - sprite `Assets/Textures/art/Characters/Crowd/CrowdBack.png` → Sprite `CrowdBack`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/CrowdRow1`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (48.325, 67.3312) · sizeDelta(W×H) (1183.35, 899.456) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 48.325, 위 -67.3312, 폭 1183.35, 높이 899.456 (scale 적용)
- **DystopiaCrowdImage**
  - sprite `Assets/Textures/art/Characters/Crowd/CrowdMiddle.png` → Sprite `CrowdMiddle`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/CrowdRow2`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (48.325, 67.3312) · sizeDelta(W×H) (1183.35, 899.456) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 48.325, 위 -67.3312, 폭 1183.35, 높이 899.456 (scale 적용)
- **DystopiaCrowdImage**
  - sprite `Assets/Textures/art/Characters/Crowd/CrowdFront.png` → Sprite `CrowdFront`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/LeftWatchTower` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/LeftWatchTower.png` → Sprite `LeftWatchTower`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/LeftTowerNeutral.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/RightWatchTower` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/RightWatchTower.png` → Sprite `RightWatchTower`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/RightTowerNeutral.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/LeftWatchGuard`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (126.203, -172.669) · sizeDelta(W×H) (31.6507, 24.1824) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/Textures/art/Characters/Guard/WatchGuard.png` → Sprite `WatchGuard`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/GuardNeutral.mat`
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/RightWatchGuard`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1159.78, -234.813) · sizeDelta(W×H) (25.3206, 19.1209) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/Textures/art/Characters/Guard/WatchGuard.png` → Sprite `WatchGuard`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/GuardNeutral.mat`
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/LeftWatchRailMask` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (62, -171) · sizeDelta(W×H) (132, 57) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 62, 위 171, 폭 132, 높이 57 (scale 적용)
- **RectMask2D**
  - padding (0, 0, 0, 0) · softness (0, 0)

###### `FrontCounter/DystopiaCanvas/LeftWatchRailMask/LeftWatchRail` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-62, 171) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -62, 위 -171, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/LeftWatchTower.png` → Sprite `LeftWatchTower`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/LeftTowerNeutral.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/RightWatchRailMask` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1132, -228) · sizeDelta(W×H) (80, 35) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1132, 위 228, 폭 80, 높이 35 (scale 적용)
- **RectMask2D**
  - padding (0, 0, 0, 0) · softness (0, 0)

###### `FrontCounter/DystopiaCanvas/RightWatchRailMask/RightWatchRail` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-1132, 228) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -1132, 위 -228, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/RightWatchTower.png` → Sprite `RightWatchTower`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material `Assets/Materials/Checkout/RightTowerNeutral.mat`
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (0, 0) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 0, 높이 0 (scale 적용)

###### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0/Flame`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-2.75625, 0.91875) · sizeDelta(W×H) (9.1875, 1.8375) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -2.75625, 위 -0.91875, 폭 9.1875, 높이 1.8375 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 0.48, 0.12, 1) #FF7A1FFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0/Spark`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 2.75625) · sizeDelta(W×H) (2.75625, 5.5125) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 -2.75625, 폭 2.75625, 높이 5.5125 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 0.7, 0.2, 1) #FFB233FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash0/Core`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-1.8375, 0.91875) · sizeDelta(W×H) (4.59375, 1.8375) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -1.8375, 위 -0.91875, 폭 4.59375, 높이 1.8375 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 0.78, 1) #FFFFC7FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (0, 0) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 0, 높이 0 (scale 적용)

###### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1/Flame`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-2.75625, 0.91875) · sizeDelta(W×H) (9.1875, 1.8375) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -2.75625, 위 -0.91875, 폭 9.1875, 높이 1.8375 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 0.48, 0.12, 1) #FF7A1FFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1/Spark`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 2.75625) · sizeDelta(W×H) (2.75625, 5.5125) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 -2.75625, 폭 2.75625, 높이 5.5125 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 0.7, 0.2, 1) #FFB233FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `FrontCounter/DystopiaCanvas/WatchMuzzleFlash1/Core`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-1.8375, 0.91875) · sizeDelta(W×H) (4.59375, 1.8375) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -1.8375, 위 -0.91875, 폭 4.59375, 높이 1.8375 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 0.78, 1) #FFFFC7FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Barricade`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (39.1376, -322.219) · sizeDelta(W×H) (1201.72, 248.062) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 39.1376, 위 322.219, 폭 1201.72, 높이 248.062 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Background/BoothBarricade.png` → Sprite `BoothBarricade`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/WaitingRear`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (240, -580) · sizeDelta(W×H) (240, 240) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0)
  - localRotation 0° (identity)
  - → 좌상단 기준: 아래 중앙 기준점 X 240, Y(아래방향) 580, 표시 폭 240, 높이 240
- **Image**
  - sprite `Assets/Textures/art/Customer/Male/MalePriceSensitive_01.png` → Sprite `MalePriceSensitive_01`
  - color RGBA(0.6, 0.65, 0.7, 1) #99A6B2FF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/WaitingLeft`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (385, -580) · sizeDelta(W×H) (340, 340) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0)
  - localRotation 0° (identity)
  - → 좌상단 기준: 아래 중앙 기준점 X 385, Y(아래방향) 580, 표시 폭 340, 높이 340
- **Image**
  - sprite `Assets/Textures/art/Customer/Female/FemalePriceSensitive_01.png` → Sprite `FemalePriceSensitive_01`
  - color RGBA(0.6, 0.65, 0.7, 1) #99A6B2FF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Customer`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (640.135, -632) · sizeDelta(W×H) (550, 550) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0)
  - localRotation 0° (identity)
  - → 좌상단 기준: 아래 중앙 기준점 X 640.135, Y(아래방향) 632, 표시 폭 550, 높이 550
- **Image**
  - sprite `Assets/Textures/art/Customer/Male/MalePriceSensitive_01.png` → Sprite `MalePriceSensitive_01`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Canopy`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (-25.6, 19) · sizeDelta(W×H) (1331.2, 96.4593) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -25.6, 위 -19, 폭 1331.2, 높이 96.4593 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Frame/Stage2RustedFrame.png` → Sprite `Stage2Ceiling`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Stage3LeftPillar`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (-25.6, -74) · sizeDelta(W×H) (145.7, 417.541) · localScale (1, 1.07, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -25.6, 위 74, 폭 145.7, 높이 446.769 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Frame/Stage2RustedFrame.png` → Sprite `Stage2LeftPillar`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Stage3RightPillar`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (1163.09, -78) · sizeDelta(W×H) (142.515, 417.541) · localScale (1, 1.05, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1163.09, 위 78, 폭 142.515, 높이 438.418 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Frame/Stage2RustedFrame.png` → Sprite `Stage2RightPillar`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/Counter`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (113.14, -483) · sizeDelta(W×H) (1280, 394.187) · localScale (0.82323, 0.82323, 0.82323)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 113.14, 위 483, 폭 1053.73, 높이 324.506 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/CounterTop/Stage2CounterTop.png` → Sprite `Stage2CounterTop`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `FrontCounter/DystopiaCanvas/Counter/CounterLeftExtension`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (137.434, 394.187) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (1, 1)
  - localRotation 0° (identity)
- **RawImage**
  - texture `Assets/Textures/art/Facility/CounterTop/Stage2CounterTop.png` (Texture2D) · uvRect {x=0.107371, y=0, width=-0.107371, height=0.428177}
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `FrontCounter/DystopiaCanvas/Counter/CounterRightExtension`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (138.26, 394.187) · localScale (1, 1, 1)
  - anchorMin (1, 1) · anchorMax (1, 1) (우상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
- **RawImage**
  - texture `Assets/Textures/art/Facility/CounterTop/Stage2CounterTop.png` (Texture2D) · uvRect {x=1, y=0, width=-0.108015, height=0.428177}
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/FacilityNuclearProtection` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (27, -198) · sizeDelta(W×H) (399, 208.822) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 27, 위 198, 폭 399, 높이 208.822 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Props/Stage3NuclearProtection.png` → Sprite `Stage3NuclearProtection`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/FacilityPrecisionElectronics` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (841, -260) · sizeDelta(W×H) (438, 244.391) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 841, 위 260, 폭 438, 높이 244.391 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Props/Stage3PrecisionElectronics.png` → Sprite `Stage3PrecisionElectronics`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/FacilityToolBench`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (-9, -438) · sizeDelta(W×H) (354, 159.489) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 -9, 위 438, 폭 354, 높이 159.489 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Props/Stage2ToolBench.png` → Sprite `Stage2ToolBench`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.02, 0.015, 0.01, 0.8) #050403CC · effectDistance (10, -12) · useGraphicAlpha true · **비활성**
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/FacilityPowerCommunications`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (985.82, -360) · sizeDelta(W×H) (204, 221.558) · localScale (1.09, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 985.82, 위 360, 폭 222.36, 높이 221.558 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Props/Stage2PowerCommunications.png` → Sprite `Stage2PowerCommunications`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.02, 0.015, 0.01, 0.8) #050403CC · effectDistance (10, -12) · useGraphicAlpha true · **비활성**
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/FacilityFoodShelf`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (53, -548) · sizeDelta(W×H) (204, 175.614) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 53, 위 548, 폭 204, 높이 175.614 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Props/Stage2FoodShelf.png` → Sprite `Stage2FoodShelf`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.02, 0.015, 0.01, 0.8) #050403CC · effectDistance (10, -12) · useGraphicAlpha true · **비활성**
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/FacilityMedicineCabinet`

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (1029, -559) · sizeDelta(W×H) (221, 157.791) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1029, 위 559, 폭 221, 높이 157.791 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Props/Stage2MedicineCabinet.png` → Sprite `Stage2MedicineCabinet`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.02, 0.015, 0.01, 0.8) #050403CC · effectDistance (10, -12) · useGraphicAlpha true · **비활성**
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/CounterLamplight` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (355, -455) · sizeDelta(W×H) (600, 180) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 355, 위 455, 폭 600, 높이 180 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Effects/TimeOfDay/CounterLight.png` → Sprite `CounterLight`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

##### `FrontCounter/DystopiaCanvas/DialoguePanel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (360, -359) · sizeDelta(W×H) (560, 70) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 360, 위 359, 폭 560, 높이 70 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/UI/DialogueFrame.png` → Sprite `DialogueFrame`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Sliced · preserveAspect false · fillCenter true · pixelsPerUnitMultiplier 4 · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DialoguePanel/Dialogue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (-36, -20) · localScale (1, 1, 1)
  - anchorMin (0, 0) · anchorMax (1, 1) (스트레치) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Text**
  - text `이 물건들로 주세요. 얼마나 드리면 될까요?`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 24 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Truncate · bestFit true · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Feedback`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (273, -674) · sizeDelta(W×H) (655, 35) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 273, 위 674, 폭 655, 높이 35 (scale 적용)
- **Text**
  - text 없음(None)
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 14 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/BasketHint`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (350, -449) · sizeDelta(W×H) (485, 24) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 350, 위 449, 폭 485, 높이 24 (scale 적용)
- **Text**
  - text `물품을 눌러 판매 제외 / 복구`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 14 · style Normal · alignment MiddleCenter · color RGBA(0.78, 0.8, 0.78, 1) #C7CCC7FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Basket` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (275, -477) · sizeDelta(W×H) (635, 138) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 275, 위 477, 폭 635, 높이 138 (scale 적용)

##### `FrontCounter/DystopiaCanvas/Register` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (947, -405) · sizeDelta(W×H) (305, 300) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 947, 위 405, 폭 305, 높이 300 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.045, 0.065, 0.075, 0.97) #0B1113F7 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

##### `FrontCounter/DystopiaCanvas/PriceHeading` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (962, -416) · sizeDelta(W×H) (120, 23) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 962, 위 416, 폭 120, 높이 23 (scale 적용)
- **Text**
  - text `받을 금액`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 16 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/DailyInstructionButton` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1110, -413) · sizeDelta(W×H) (127, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1110, 위 413, 폭 127, 높이 28 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable false · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/DailyInstructionButton` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/DailyInstructionButton/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (119, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 119, 높이 28 (scale 적용)
- **Text**
  - text `오늘 지침`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 14 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/PriceInput` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (962, -443) · sizeDelta(W×H) (276, 40) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 962, 위 443, 폭 276, 높이 40 (scale 적용)
- **Text**
  - text `금액 입력`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 30 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit1` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (961, -491) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 961, 위 491, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit1` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit1/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 1
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit2` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1055, -491) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1055, 위 491, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit2` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit2/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 2
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit3` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1149, -491) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1149, 위 491, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit3` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit3/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 3
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit4` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (961, -529) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 961, 위 529, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit4` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit4/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 4
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit5` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1055, -529) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1055, 위 529, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit5` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit5/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 5
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit6` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1149, -529) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1149, 위 529, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit6` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit6/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 6
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit7` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (961, -567) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 961, 위 567, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit7` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit7/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 7
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit8` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1055, -567) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1055, 위 567, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit8` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit8/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 8
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit9` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1149, -567) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1149, 위 567, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit9` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit9/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 9
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Erase` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (961, -605) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 961, 위 605, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Erase` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Erase/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text `지우기`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 16 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Digit0` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1055, -605) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1055, 위 605, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Digit0` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Digit0/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text 0
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Pause` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1149, -605) · sizeDelta(W×H) (88, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1149, 위 605, 폭 88, 높이 33 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Pause` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Pause/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (80, 33) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 80, 높이 33 (scale 적용)
- **Text**
  - text `일시정지`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 15 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Confirm` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (961, -646) · sizeDelta(W×H) (276, 44) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 961, 위 646, 폭 276, 높이 44 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable false · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Confirm` (Image)
  - colors normal RGBA(0.35, 0.48, 0.45, 1) #597A73FF / highlighted RGBA(0.46, 0.6, 0.55, 1) #75998CFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Confirm/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (268, 44) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 268, 높이 44 (scale 적용)
- **Text**
  - text `판매 확정  ↵`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 20 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/DailyInstruction` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)

###### `FrontCounter/DystopiaCanvas/DailyInstruction/InputBlocker`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0.42) #0000006B · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (368, -20) · sizeDelta(W×H) (544, 680) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 368, 위 20, 폭 544, 높이 680 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/UI/DailyInstruction.png` → Sprite `DailyInstruction`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/DayPaper`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (430, -100) · sizeDelta(W×H) (114, 24) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 430, 위 100, 폭 114, 높이 24 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.81, 0.8, 0.76, 1) #CFCCC2FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionDay`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (430, -98) · sizeDelta(W×H) (112, 31) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 430, 위 98, 폭 112, 높이 31 (scale 적용)
- **Text**
  - text `1일차`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleCenter · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/MemoryHeading`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -151) · sizeDelta(W×H) (407, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 151, 폭 407, 높이 34 (scale 적용)
- **Text**
  - text `영업 전, 가격을 기억하세요`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 24 · style Bold · alignment MiddleCenter · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/RuleTitle`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -194) · sizeDelta(W×H) (407, 26) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 194, 폭 407, 높이 26 (scale 적용)
- **Text**
  - text `오늘의 지침`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleCenter · color RGBA(0.34, 0.34, 0.32, 1) #575752FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/Rule`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -222) · sizeDelta(W×H) (407, 58) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 222, 폭 407, 높이 58 (scale 적용)
- **Text**
  - text `제한 없음.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment UpperCenter · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionProduct0`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (146, -300) · sizeDelta(W×H) (50, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 146, 위 300, 폭 50, 높이 48 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Products/Legacy/Water.png` → Sprite `Water`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionName0`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (202, -299) · sizeDelta(W×H) (137, 24) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 202, 위 299, 폭 137, 높이 24 (scale 적용)
- **Text**
  - text `생수`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Normal · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionPrice0`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (202, -323) · sizeDelta(W×H) (137, 25) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 202, 위 323, 폭 137, 높이 25 (scale 적용)
- **Text**
  - text `1,000원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionProduct1`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (351, -300) · sizeDelta(W×H) (50, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 351, 위 300, 폭 50, 높이 48 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Products/Legacy/Crackers.png` → Sprite `Crackers`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionName1`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (407, -299) · sizeDelta(W×H) (137, 24) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 407, 위 299, 폭 137, 높이 24 (scale 적용)
- **Text**
  - text `건빵`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Normal · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionPrice1`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (407, -323) · sizeDelta(W×H) (137, 25) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 407, 위 323, 폭 137, 높이 25 (scale 적용)
- **Text**
  - text `1,500원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionProduct2`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (146, -364) · sizeDelta(W×H) (50, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 146, 위 364, 폭 50, 높이 48 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Products/Legacy/Can.png` → Sprite `Can`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionName2`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (202, -363) · sizeDelta(W×H) (137, 24) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 202, 위 363, 폭 137, 높이 24 (scale 적용)
- **Text**
  - text `통조림`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Normal · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionPrice2`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (202, -387) · sizeDelta(W×H) (137, 25) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 202, 위 387, 폭 137, 높이 25 (scale 적용)
- **Text**
  - text `2,500원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionProduct3`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (351, -364) · sizeDelta(W×H) (50, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 351, 위 364, 폭 50, 높이 48 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Products/Legacy/Rice.png` → Sprite `Rice`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionName3`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (407, -363) · sizeDelta(W×H) (137, 24) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 407, 위 363, 폭 137, 높이 24 (scale 적용)
- **Text**
  - text `즉석밥`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Normal · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/InstructionPrice3`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (407, -387) · sizeDelta(W×H) (137, 25) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 407, 위 387, 폭 137, 높이 25 (scale 적용)
- **Text**
  - text `2,000원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleLeft · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/PriceRestriction`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -454) · sizeDelta(W×H) (407, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 454, 폭 407, 높이 28 (scale 적용)
- **Text**
  - text `영업이 시작되면 가격표를 다시 볼 수 없습니다.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 15 · style Normal · alignment MiddleCenter · color RGBA(0.34, 0.34, 0.32, 1) #575752FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/RecheckGuide`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -484) · sizeDelta(W×H) (407, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 484, 폭 407, 높이 28 (scale 적용)
- **Text**
  - text `당일 지침은 영업 중에도 다시 확인할 수 있습니다.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 15 · style Normal · alignment MiddleCenter · color RGBA(0.34, 0.34, 0.32, 1) #575752FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/OpenShopBorder` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (204, -546) · sizeDelta(W×H) (204, 46) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 204, 위 546, 폭 204, 높이 46 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.34, 0.34, 0.32, 1) #575752FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/OpenShop`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (206, -548) · sizeDelta(W×H) (200, 42) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 206, 위 548, 폭 200, 높이 42 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/UI/InstructionStartStamp.png` → Sprite `InstructionStartStamp`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/OpenShop` (Image)
  - colors normal RGBA(0.78, 0.76, 0.69, 1) #C7C2B0FF / highlighted RGBA(0.88, 0.85, 0.77, 1) #E0D9C4FF / pressed RGBA(0.62, 0.6, 0.53, 1) #9E9987FF / selected RGBA(0.78, 0.76, 0.69, 1) #C7C2B0FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/OpenShop/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (192, 42) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 192, 높이 42 (scale 적용)
- **Text**
  - text `영업 시작`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 19 · style Normal · alignment MiddleCenter · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/ViolationCount` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -374) · sizeDelta(W×H) (407, 30) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 374, 폭 407, 높이 30 (scale 적용)
- **Text**
  - text `오늘 적발 0회`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Bold · alignment MiddleCenter · color RGBA(0.18, 0.18, 0.17, 1) #2E2E2BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/CloseInstruction` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (206, -548) · sizeDelta(W×H) (268, 42) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 206, 위 548, 폭 268, 높이 42 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/CloseInstruction` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/CloseInstruction/Label` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (260, 42) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 260, 높이 42 (scale 적용)
- **Text**
  - text `지침 닫기`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 19 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyInstruction/Sheet/LatestViolation` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (137, -408) · sizeDelta(W×H) (407, 55) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 137, 위 408, 폭 407, 높이 55 (scale 적용)
- **Text**
  - text `최근 위반`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 14 · style Normal · alignment UpperLeft · color RGBA(0.34, 0.34, 0.32, 1) #575752FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/DailyLedger` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)

###### `FrontCounter/DystopiaCanvas/DailyLedger/InputBlocker`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (145, -30) · sizeDelta(W×H) (990, 660) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 145, 위 30, 폭 990, 높이 660 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/UI/DailyLedger.png` → Sprite `DailyLedger`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/LedgerDay`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -94) · sizeDelta(W×H) (350, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 94, 폭 350, 높이 28 (scale 적용)
- **Text**
  - text `영업 1일차`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/Heading오늘의 장사`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -128) · sizeDelta(W×H) (350, 36) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 128, 폭 350, 높이 36 (scale 적용)
- **Text**
  - text `오늘의 장사`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 27 · style Bold · alignment MiddleLeft · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/HeadingRule오늘의 장사`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -167) · sizeDelta(W×H) (350, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 167, 폭 350, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.26, 0.27, 0.27, 0.52) #42454585 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/HandledLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -180) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 180, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `응대한 손님`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/HandledValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (274, -180) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 274, 위 180, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text `0명`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MarkupCountLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -228) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 228, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `폭리 거래`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MarkupCountValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (274, -228) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 274, 위 228, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text `0건`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MarkupAmountLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -276) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 276, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `폭리 금액`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MarkupAmountValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (274, -276) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 274, 위 276, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text `0원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TradeDivider`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -322) · sizeDelta(W×H) (350, 1) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 322, 폭 350, 높이 1 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.26, 0.27, 0.27, 0.52) #42454585 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MoralityLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -344) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 344, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `도덕성 변화`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MoralityValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (274, -344) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 274, 위 344, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text 0
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 23 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/ReputationLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (92, -394) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 92, 위 394, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `명성 변화`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/ReputationValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (274, -394) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 274, 위 394, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text 0
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 23 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/Heading오늘의 정산`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -128) · sizeDelta(W×H) (350, 36) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 128, 폭 350, 높이 36 (scale 적용)
- **Text**
  - text `오늘의 정산`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 27 · style Bold · alignment MiddleLeft · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/HeadingRule오늘의 정산`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -167) · sizeDelta(W×H) (350, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 167, 폭 350, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.26, 0.27, 0.27, 0.52) #42454585 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/RevenueLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -180) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 180, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `판매수익`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/RevenueValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (730, -180) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 730, 위 180, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text `0원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 24 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/GoodsCostLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -226) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 226, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `물품대금`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/GoodsCostMissingValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (878, -242) · sizeDelta(W×H) (20, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 878, 위 242, 폭 20, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/RentLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -266) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 266, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `임대료`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/RentMissingValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (878, -282) · sizeDelta(W×H) (20, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 878, 위 282, 폭 20, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MedicalLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -306) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 306, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `치료비`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/MedicalMissingValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (878, -322) · sizeDelta(W×H) (20, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 878, 위 322, 폭 20, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TributeLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -346) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 346, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `상납금`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TributeValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (730, -346) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 730, 위 346, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text `0원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Bold · alignment MiddleRight · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/SettlementDivider`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -389) · sizeDelta(W×H) (350, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 389, 폭 350, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.26, 0.27, 0.27, 0.52) #42454585 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/NetProfitLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -403) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 403, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `오늘 순이익`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 22 · style Normal · alignment MiddleLeft · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/NetProfitMissingValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (878, -419) · sizeDelta(W×H) (20, 2) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 878, 위 419, 폭 20, 높이 2 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/CashLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -449) · sizeDelta(W×H) (196, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 449, 폭 196, 높이 34 (scale 적용)
- **Text**
  - text `현재 보유금`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 22 · style Normal · alignment MiddleLeft · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/CashValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (730, -449) · sizeDelta(W×H) (168, 34) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 730, 위 449, 폭 168, 높이 34 (scale 적용)
- **Text**
  - text `0원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 30 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TributeGuideDivider`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -488) · sizeDelta(W×H) (350, 1) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 488, 폭 350, 높이 1 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.26, 0.27, 0.27, 0.3) #4245454C · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/NextTributeLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (548, -494) · sizeDelta(W×H) (100.44, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 548, 위 494, 폭 100.44, 높이 28 (scale 적용)
- **Text**
  - text `다음 상납까지`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 14 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/NextTributeValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (625.76, -494) · sizeDelta(W×H) (84.24, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 625.76, 위 494, 폭 84.24, 높이 28 (scale 적용)
- **Text**
  - text `6일`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TributeGuideSplit`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (722, -495) · sizeDelta(W×H) (1, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 722, 위 495, 폭 1, 높이 28 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.26, 0.27, 0.27, 0.3) #4245454C · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TributeDueLabel`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (736, -494) · sizeDelta(W×H) (100.44, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 736, 위 494, 폭 100.44, 높이 28 (scale 적용)
- **Text**
  - text `납부 예정`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 14 · style Normal · alignment MiddleLeft · color RGBA(0.37, 0.38, 0.38, 1) #5E6161FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/TributeDueValue`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (813.76, -494) · sizeDelta(W×H) (84.24, 28) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 813.76, 위 494, 폭 84.24, 높이 28 (scale 적용)
- **Text**
  - text `50,000원`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 17 · style Bold · alignment MiddleRight · color RGBA(0.15, 0.16, 0.17, 1) #26292BFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/LedgerConfirm`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (708, -526) · sizeDelta(W×H) (190, 38) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 708, 위 526, 폭 190, 높이 38 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/DailyLedger/Book/LedgerConfirm` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/DailyLedger/Book/LedgerConfirm/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (182, 38) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 182, 높이 38 (scale 적용)
- **Text**
  - text `확인`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Bold · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Modal` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (270, -111) · sizeDelta(W×H) (740, 510) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 270, 위 111, 폭 740, 높이 510 (scale 적용)

###### `FrontCounter/DystopiaCanvas/Modal/Border`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (740, 510) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 740, 높이 510 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.46, 0.51, 0.51, 1) #758282FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/Modal/Paper`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (2, -2) · sizeDelta(W×H) (736, 506) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 2, 위 2, 폭 736, 높이 506 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.075, 0.095, 0.105, 0.99) #13181BFC · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/Modal/ModalTitle`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (35, -28) · sizeDelta(W×H) (670, 44) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 35, 위 28, 폭 670, 높이 44 (scale 적용)
- **Text**
  - text `정산 확인`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 31 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/ModalSubtitle`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (35, -85) · sizeDelta(W×H) (670, 55) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 35, 위 85, 폭 670, 높이 55 (scale 적용)
- **Text**
  - text `다음 단계로 진행하세요.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/Inspector` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (488, -150) · sizeDelta(W×H) (245, 350) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 488, 위 150, 폭 245, 높이 350 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Characters/Inspector/Inspector.png` → Sprite `Inspector`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true

###### `FrontCounter/DystopiaCanvas/Modal/TributeCash` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (40, -225) · sizeDelta(W×H) (480, 70) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 40, 위 225, 폭 480, 높이 70 (scale 적용)
- **Text**
  - text `보유 현금  0원\n상납 처리 후 시민권을 구매할 수 있습니다.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/PayTribute` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (80, -420) · sizeDelta(W×H) (400, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 80, 위 420, 폭 400, 높이 48 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Modal/PayTribute` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Modal/PayTribute/Label` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (392, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 392, 높이 48 (scale 적용)
- **Text**
  - text `상납금 정산`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/Ending` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (50, -185) · sizeDelta(W×H) (640, 195) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 50, 위 185, 폭 640, 높이 195 (scale 적용)
- **Text**
  - text `시민권을 손에 넣었습니다.\n\n당신이 매긴 가격들이 이곳까지 데려왔습니다.\n\n명성과 도덕성, 그리고 남겨진 사람들을 기억하며.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 23 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/Restart` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (170, -420) · sizeDelta(W×H) (400, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 170, 위 420, 폭 400, 높이 48 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Modal/Restart` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Modal/Restart/Label` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (392, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 392, 높이 48 (scale 적용)
- **Text**
  - text `처음부터`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/Failure` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (40, -190) · sizeDelta(W×H) (470, 170) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 40, 위 190, 폭 470, 높이 170 (scale 적용)
- **Text**
  - text `필요한 상납금 50,000원\n보유 현금 0원\n\n기억과 선택을 되짚어 다시 시작하세요.`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 22 · style Normal · alignment UpperLeft · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/Resume` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (190, -320) · sizeDelta(W×H) (360, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 190, 위 320, 폭 360, 높이 48 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Modal/Resume` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Modal/Resume/Label` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (352, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 352, 높이 48 (scale 적용)
- **Text**
  - text `계속하기`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/ReopenLedger`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (190, -350) · sizeDelta(W×H) (360, 44) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 190, 위 350, 폭 360, 높이 44 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Modal/ReopenLedger` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Modal/ReopenLedger/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (352, 44) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 352, 높이 44 (scale 적용)
- **Text**
  - text `가계부 다시 보기`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 18 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/NextDay`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (390, -420) · sizeDelta(W×H) (300, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 390, 위 420, 폭 300, 높이 48 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Modal/NextDay` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Modal/NextDay/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (292, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 292, 높이 48 (scale 적용)
- **Text**
  - text `다음 날`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

###### `FrontCounter/DystopiaCanvas/Modal/BuyCitizenship`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (40, -420) · sizeDelta(W×H) (320, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 40, 위 420, 폭 320, 높이 48 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable false · transition 1 · targetGraphic → `FrontCounter/DystopiaCanvas/Modal/BuyCitizenship` (Image)
  - colors normal RGBA(0.19, 0.25, 0.28, 1) #304047FF / highlighted RGBA(0.3, 0.39, 0.42, 1) #4C636BFF / pressed RGBA(0.42, 0.5, 0.51, 1) #6B8082FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 0 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)

###### `FrontCounter/DystopiaCanvas/Modal/BuyCitizenship/Label`

Layer 0 · Tag `Untagged` · 원본 FrontView.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (4, 0) · sizeDelta(W×H) (312, 48) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 4, 위 0, 폭 312, 높이 48 (scale 적용)
- **Text**
  - text `시민권 구매`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 21 · style Normal · alignment MiddleCenter · color RGBA(0.91, 0.93, 0.91, 1) #E8EDE8FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Overflow · bestFit false · raycastTarget false

##### `FrontCounter/DystopiaCanvas/Stage3CeilingLamp` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged`

- **RectTransform**
  - anchoredPosition (557.47, -3) · sizeDelta(W×H) (150.048, 20.6589) · localScale (1.1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 557.47, 위 3, 폭 165.053, 높이 20.6589 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Frame/Stage3Shop.png` → Sprite `Stage3CeilingLamp`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

#### `FrontCounter/DystopiaEventSystem`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **EventSystem**
  - m_FirstSelected = 없음(None)
  - m_sendNavigationEvents = 0
  - m_DragThreshold = 10
- **InputSystemUIInputModule**
  - m_SendPointerHoverToParent = 1
  - m_MoveRepeatDelay = 0.5
  - m_MoveRepeatRate = 0.1
  - m_XRTrackingOrigin = 없음(None)
  - m_ActionsAsset = → `?203058901` (InputActionAsset)
  - m_PointAction = → `?581728348` (InputActionReference)
  - m_MoveAction = → `?646507011` (InputActionReference)
  - m_SubmitAction = → `?2058972186` (InputActionReference)
  - m_CancelAction = → `?2135571030` (InputActionReference)
  - m_LeftClickAction = → `?798320296` (InputActionReference)
  - m_MiddleClickAction = → `?1028404266` (InputActionReference)
  - m_RightClickAction = → `?1913585418` (InputActionReference)
  - m_ScrollWheelAction = → `?414779098` (InputActionReference)
  - m_TrackedDevicePositionAction = → `?1132078629` (InputActionReference)
  - m_TrackedDeviceOrientationAction = → `?797704233` (InputActionReference)
  - m_DeselectOnBackgroundClick = 1
  - m_PointerBehavior = 0
  - m_CursorLockBehavior = 0
  - m_ScrollDeltaPerTick = 6

### `TopDownCheckout`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **DystopiaTopDownTest**
  - placedHostScreen = → `FrontCounter` (DystopiaScreen)
  - placedSaleZone = → `TopDownCheckout/SaleArea` (BoxCollider2D)
  - placedExcludedZone = → `TopDownCheckout/ExcludedArea` (BoxCollider2D)
  - placedMovementZone = → `TopDownCheckout/MovementArea` (BoxCollider2D)
  - placedItemPrefabs = [`Assets/DystopiaPrototype/Prefabs/Product0.prefab` (fileID 3484001219767234060), `Assets/DystopiaPrototype/Prefabs/Product1.prefab` (fileID 5760939923749310461), `Assets/DystopiaPrototype/Prefabs/Product2.prefab` (fileID 5679815024812442827), `Assets/DystopiaPrototype/Prefabs/Product3.prefab` (fileID 6952130976575046213)]
  - transitionSeconds = 0.35
  - pourDuration = 1.2
  - pourSpreadSeconds = 0.55
  - frontArrivalSeconds = 2
  - reactionSeconds = 0.9
  - dividerBar = → `TopDownCheckout/DividerBar` (DividerBarController2D)
  - vacuum = → `TopDownCheckout/Vacuum` (DystopiaVacuumController)
  - handArtwork = 없음(None)
  - handSizePixels = 128
  - pourCenter = (-1.5, 0)
  - pourSpread = (2.6, 2.2)
  - cursorRadius = 0.42
  - cursorForce = 0.032
  - maximumItemSpeed = 5.2
  - itemFriction = 6.5
  - rotationDamping = 4.5
  - pourForce = 2.5
  - gameMinutesPerRealSecond = 6
  - settings = {products=[{id=1, requiredFacility=0, name=`생수`, price=1000, firstDay=1, sprite=`Assets/Textures/art/Products/DrinkingWater.png` → Sprite `DrinkingWater`}, {id=2, requiredFacility=0, name=`통조림`, price=2500, firstDay=1, sprite=`Assets/Textures/art/Products/CannedFood.png` → Sprite `CannedFood`}, {id=3, requiredFacility=0, name=`붕대`, price=3000, firstDay=1, sprite=`Assets/Textures/art/Products/MedicalBandage.png` → Sprite `MedicalBandage`}, {id=4, requiredFacility=0, name=`건전지`, price=3500, firstDay=1, sprite=`Assets/Textures/art/Products/DryBattery.png` → Sprite `DryBattery`}, {id=5, requiredFacility=1, name=`군용식량`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/MilitaryRation.png` → Sprite `MilitaryRation`}, {id=6, requiredFacility=1, name=`영양바`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/NutritionBar.png` → Sprite `NutritionBar`}, {id=7, requiredFacility=2, name=`약통`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/Medicine.png` → Sprite `Medicine`}, {id=8, requiredFacility=2, name=`응급 주사`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/EmergencyInjection.png` → Sprite `EmergencyInjection`}, {id=9, requiredFacility=4, name=`손전등`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/Flashlight.png` → Sprite `Flashlight`}, {id=10, requiredFacility=4, name=`접이식 삽`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/FoldingShovel.png` → Sprite `FoldingShovel`}, {id=11, requiredFacility=8, name=`무전기`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/Radio.png` → Sprite `Radio`}, {id=12, requiredFacility=8, name=`배터리`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/PowerBattery.png` → Sprite `PowerBattery`}, {id=13, requiredFacility=16, name=`방독면`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/GasMask.png` → Sprite `GasMask`}, {id=14, requiredFacility=16, name=`방호복`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/ProtectiveSuit.png` → Sprite `ProtectiveSuit`}, {id=15, requiredFacility=32, name=`방사능 측정기`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/RadiationDetector.png` → Sprite `RadiationDetector`}, {id=16, requiredFacility=32, name=`열화상 카메라`, price=0, firstDay=1, sprite=`Assets/Textures/art/Products/ThermalCamera.png` → Sprite `ThermalCamera`}], shopStage=0, ownedFacilities=0, citizenshipPrice=300000, firstTribute=50000, secondTribute=80000, laterTribute=110000, baseVisitors=8, minVisitors=6, maxVisitors=12, gaugeStart=70, greenThreshold=70, gaugeDecay=2, gaugeRecovery=12, departureRecovery=30, departureCount=2, departureReputation=-5, greenReputation=1, toleranceReputation=-2, budgetReputation=-1, refusalMorality=-1, unpopularReputationMin=20, neutralReputationMin=40, popularReputationMin=60, trustedReputationMin=80, discountMorality=2, generousMorality=4, markupMorality=-2, poorChance=0.18, poorBudgetRatio=0.85, normalBudgetMinPercent=110, normalBudgetMaxPercent=150, resultSeconds=0.85}
  - frontBackground = `Assets/Textures/art/Background/FARBACKGROUND.png` → Sprite `FARBACKGROUND`
  - frontCounter = `Assets/Textures/art/Facility/CounterTop/Counter.png` → Sprite `Counter`
  - workbench = `Assets/Textures/art/Facility/Workbench/TopDownWorkbench.png` → Sprite `TopDownWorkbench`
  - frontContainerMale = `Assets/Textures/art/Facility/Crate/FrontContainerMale.png` → Sprite `FrontContainerMale`
  - frontContainerFemale = `Assets/Textures/art/Facility/Crate/FrontContainerFemale.png` → Sprite `FrontContainerFemale`
  - tiltedContainer = `Assets/Textures/art/Workbench/TopDownContainerTilted.png` → Sprite `TopDownContainerTilted`
  - emptyContainer = `Assets/Textures/art/Workbench/TopDownContainerEmpty.png` → Sprite `TopDownContainerEmpty`
  - productSprites = [`Assets/Textures/art/Workbench/TopDownWater.png` → Sprite `TopDownWater`, `Assets/Textures/art/Workbench/TopDownCrackers.png` → Sprite `TopDownCrackers`, `Assets/Textures/art/Workbench/TopDownCan.png` → Sprite `TopDownCan`, `Assets/Textures/art/Workbench/TopDownRiceRound.png` → Sprite `TopDownRiceRound`]
  - maleCustomers = [`Assets/Textures/art/Customer/Male/MaleNormal_01.png` → Sprite `MaleNormal_01`, `Assets/Textures/art/Customer/Male/MaleNormal_02.png` → Sprite `MaleNormal_02`, `Assets/Textures/art/Customer/Male/MaleNormal_03.png` → Sprite `MaleNormal_03`, `Assets/Textures/art/Customer/Male/MaleNormal_04.png` → Sprite `MaleNormal_04`, `Assets/Textures/art/Customer/Male/MaleNormal_05.png` → Sprite `MaleNormal_05`, `Assets/Textures/art/Customer/Male/MaleNormal_06.png` → Sprite `MaleNormal_06`, `Assets/Textures/art/Customer/Male/MaleNormal_07.png` → Sprite `MaleNormal_07`, `Assets/Textures/art/Customer/Male/MaleNormal_08.png` → Sprite `MaleNormal_08`, `Assets/Textures/art/Customer/Male/MaleNormal_09.png` → Sprite `MaleNormal_09`, `Assets/Textures/art/Customer/Male/MaleNormal_10.png` → Sprite `MaleNormal_10`, `Assets/Textures/art/Customer/Male/MaleNormal_11.png` → Sprite `MaleNormal_11`, `Assets/Textures/art/Customer/Male/MaleNormal_12.png` → Sprite `MaleNormal_12`, `Assets/Textures/art/Customer/Male/MaleHasty_01.png` → Sprite `MaleHasty_01`, `Assets/Textures/art/Customer/Male/MaleHasty_02.png` → Sprite `MaleHasty_02`, `Assets/Textures/art/Customer/Male/MaleHasty_03.png` → Sprite `MaleHasty_03`, `Assets/Textures/art/Customer/Male/MalePriceSensitive_01.png` → Sprite `MalePriceSensitive_01`, `Assets/Textures/art/Customer/Male/MalePriceSensitive_02.png` → Sprite `MalePriceSensitive_02`, `Assets/Textures/art/Customer/Male/MalePriceSensitive_03.png` → Sprite `MalePriceSensitive_03`, `Assets/Textures/art/Customer/Male/MaleWealthy_01.png` → Sprite `MaleWealthy_01`, `Assets/Textures/art/Customer/Male/MaleWealthy_02.png` → Sprite `MaleWealthy_02`, `Assets/Textures/art/Customer/Male/MaleWealthy_03.png` → Sprite `MaleWealthy_03`, `Assets/Textures/art/Customer/Male/MalePoor_01.png` → Sprite `MalePoor_01`, `Assets/Textures/art/Customer/Male/MalePoor_02.png` → Sprite `MalePoor_02`, `Assets/Textures/art/Customer/Male/MalePoor_03.png` → Sprite `MalePoor_03`, `Assets/Textures/art/Customer/Male/MaleChild_01.png` → Sprite `MaleChild_01`, `Assets/Textures/art/Customer/Male/MaleChild_02.png` → Sprite `MaleChild_02`, `Assets/Textures/art/Customer/Male/MaleChild_03.png` → Sprite `MaleChild_03`, `Assets/Textures/art/Customer/Male/MaleElder_01.png` → Sprite `MaleElder_01`, `Assets/Textures/art/Customer/Male/MaleElder_02.png` → Sprite `MaleElder_02`, `Assets/Textures/art/Customer/Male/MaleElder_03.png` → Sprite `MaleElder_03`]
  - femaleCustomers = [`Assets/Textures/art/Customer/Female/FemaleNormal_01.png` → Sprite `FemaleNormal_01`, `Assets/Textures/art/Customer/Female/FemaleNormal_02.png` → Sprite `FemaleNormal_02`, `Assets/Textures/art/Customer/Female/FemaleNormal_03.png` → Sprite `FemaleNormal_03`, `Assets/Textures/art/Customer/Female/FemaleNormal_04.png` → Sprite `FemaleNormal_04`, `Assets/Textures/art/Customer/Female/FemaleNormal_05.png` → Sprite `FemaleNormal_05`, `Assets/Textures/art/Customer/Female/FemaleNormal_06.png` → Sprite `FemaleNormal_06`, `Assets/Textures/art/Customer/Female/FemaleNormal_07.png` → Sprite `FemaleNormal_07`, `Assets/Textures/art/Customer/Female/FemaleNormal_08.png` → Sprite `FemaleNormal_08`, `Assets/Textures/art/Customer/Female/FemaleNormal_09.png` → Sprite `FemaleNormal_09`, `Assets/Textures/art/Customer/Female/FemaleNormal_10.png` → Sprite `FemaleNormal_10`, `Assets/Textures/art/Customer/Female/FemaleNormal_11.png` → Sprite `FemaleNormal_11`, `Assets/Textures/art/Customer/Female/FemaleNormal_12.png` → Sprite `FemaleNormal_12`, `Assets/Textures/art/Customer/Female/FemaleHasty_01.png` → Sprite `FemaleHasty_01`, `Assets/Textures/art/Customer/Female/FemaleHasty_02.png` → Sprite `FemaleHasty_02`, `Assets/Textures/art/Customer/Female/FemaleHasty_03.png` → Sprite `FemaleHasty_03`, `Assets/Textures/art/Customer/Female/FemalePriceSensitive_01.png` → Sprite `FemalePriceSensitive_01`, `Assets/Textures/art/Customer/Female/FemalePriceSensitive_02.png` → Sprite `FemalePriceSensitive_02`, `Assets/Textures/art/Customer/Female/FemalePriceSensitive_03.png` → Sprite `FemalePriceSensitive_03`, `Assets/Textures/art/Customer/Female/FemaleWealthy_01.png` → Sprite `FemaleWealthy_01`, `Assets/Textures/art/Customer/Female/FemaleWealthy_02.png` → Sprite `FemaleWealthy_02`, `Assets/Textures/art/Customer/Female/FemaleWealthy_03.png` → Sprite `FemaleWealthy_03`, `Assets/Textures/art/Customer/Female/FemalePoor_01.png` → Sprite `FemalePoor_01`, `Assets/Textures/art/Customer/Female/FemalePoor_02.png` → Sprite `FemalePoor_02`, `Assets/Textures/art/Customer/Female/FemalePoor_03.png` → Sprite `FemalePoor_03`, `Assets/Textures/art/Customer/Female/FemaleChild_01.png` → Sprite `FemaleChild_01`, `Assets/Textures/art/Customer/Female/FemaleChild_02.png` → Sprite `FemaleChild_02`, `Assets/Textures/art/Customer/Female/FemaleChild_03.png` → Sprite `FemaleChild_03`, `Assets/Textures/art/Customer/Female/FemaleElder_01.png` → Sprite `FemaleElder_01`, `Assets/Textures/art/Customer/Female/FemaleElder_02.png` → Sprite `FemaleElder_02`, `Assets/Textures/art/Customer/Female/FemaleElder_03.png` → Sprite `FemaleElder_03`]
  - uiFont = `Assets/Fonts/Checkout/Mulmaru.otf`
  - calculatorArtwork = `Assets/Textures/art/Workbench/Calculator.png` → Sprite `Calculator`
  - calculatorToggleArtwork = `Assets/Textures/art/Workbench/CalculatorToggle.png` → Sprite `CalculatorToggle`
  - counterClockArtwork = `Assets/Textures/art/Facility/Clock/CounterClock.png` → Sprite `CounterClock`
  - calculatorLayout = {x=900, y=310, width=360, height=360}
  - calculatorToggleLayout = {x=1214, y=659, width=54, height=58}
  - counterClockLayout = {x=1090, y=380, width=180, height=180}
- **DystopiaDayNight**
  - clock = → `TopDownCheckout` (DystopiaTopDownTest)
  - pixelStage = → `TopDownCheckout` (DystopiaPixelStage)
  - dawn = → `FrontCounter/DystopiaCanvas/DawnBackground` (Image)
  - evening = → `FrontCounter/DystopiaCanvas/EveningBackground` (Image)
  - cityLights = → `FrontCounter/DystopiaCanvas/CityLights` (Image)
  - counterLight = → `FrontCounter/DystopiaCanvas/CounterLamplight` (Image)
  - sunset = → `FrontCounter/DystopiaCanvas/SunsetBackground` (Image)
  - sunsetStart = 15
  - sunsetTint = RGBA(1, 0.72, 0.49, 1) #FFB87DFF
  - leftBeam = → `FrontCounter/DystopiaCanvas/LeftSearchlight` (Image)
  - rightBeam = → `FrontCounter/DystopiaCanvas/RightSearchlight` (Image)
  - environment = [→ `FrontCounter/DystopiaCanvas/MidBackground` (Image), → `FrontCounter/DystopiaCanvas/Fog_Back` (Image), → `FrontCounter/DystopiaCanvas/Fog_Mid` (Image), → `FrontCounter/DystopiaCanvas/Fog_Front` (Image), → `FrontCounter/DystopiaCanvas/LeftWatchTower` (Image), → `FrontCounter/DystopiaCanvas/RightWatchTower` (Image), → `FrontCounter/DystopiaCanvas/Canopy` (Image), → `FrontCounter/DystopiaCanvas/Barricade` (Image), → `FrontCounter/DystopiaCanvas/Counter` (Image), → `FrontCounter/DystopiaCanvas/CrowdRow0` (DystopiaCrowdImage), → `FrontCounter/DystopiaCanvas/CrowdRow1` (DystopiaCrowdImage), → `FrontCounter/DystopiaCanvas/CrowdRow2` (DystopiaCrowdImage)]
  - people = [→ `FrontCounter/DystopiaCanvas/Customer` (Image), → `FrontCounter/DystopiaCanvas/WaitingLeft` (Image), → `FrontCounter/DystopiaCanvas/WaitingRear` (Image)]
  - preview = 1
  - previewHour = 9
  - dawnStart = 9
  - dayStart = 12
  - eveningStart = 18
  - nightEnd = 21
  - dawnTint = RGBA(1, 0.9, 0.8, 1) #FFE6CCFF
  - nightTint = RGBA(0.384078, 0.473474, 0.59434, 1) #627998FF
  - peopleBrightness = 0.72
  - cityIntensity = 0.7
  - beamIntensity = 0.11
  - counterIntensity = 0.12
- **DystopiaPixelStage**
  - frontCanvas = → `FrontCounter/DystopiaCanvas` (RectTransform)
  - layers: 54개 (아래 5장 표 참조)
  - lightingShader = `Assets/Shaders/Checkout/PixelStageLighting.shader`
  - width = 480
  - matchScreenResolution = 0
  - previewInEditor = 1
  - lampPosition = (640, 64)
  - ceilingLamp = → `FrontCounter/DystopiaCanvas/Stage3CeilingLamp` (Image)
  - lampHeight = 240
  - lampRadius = 700
  - lampIntensity = 1.6
  - lampColor = RGBA(0.95, 0.92, 0.82, 1) #F2EBD1FF
  - lightingSteps = 6
  - rimIntensity = 1.3
  - dawnFog = RGBA(1, 0.84, 0.67, 1) #FFD6ABFF
  - sunsetFog = RGBA(1, 0.54, 0.28, 1) #FF8A47FF
  - nightFog = RGBA(0.28, 0.39, 0.59, 1) #476396FF
  - skyGlowIntensity = 0.3
  - daylightContrast = 0.65
  - sunsetContrast = 0.8
  - daylightShadowOpacity = 0.3
  - sunsetShadowOpacity = 0.55
  - hourlyAmbient = AnimationCurve[(9→1), (21→1)]
  - hourlySunlight = AnimationCurve[(9→1), (21→1)]
  - hourlyNormal = AnimationCurve[(9→1), (21→1)]
  - hourlyHardness = AnimationCurve[(9→1), (21→1)]
  - hourlyFill = AnimationCurve[(9→0.15), (21→0.15)]
  - hourlyShadow = AnimationCurve[(9→1), (21→1)]
  - hourlySunX = AnimationCurve[(9→0), (21→0)]
  - hourlySunY = AnimationCurve[(9→0), (21→0)]
  - relightingTrial = 0
  - useCustomerNormalMap = 0
  - normalStrength = 0.65
  - propNormalStrength = 1
  - propNightFill = 0.3
  - roomLightStrength = 1.2
  - eveningSpotlight = 1
  - spotOrigin = (640, 64)
  - spotTarget = (640, 530)
  - spotIntensity = 1.4
  - spotHalfAngle = 32
  - spotSoftness = 0.65
  - spotHaze = 0.009
  - towerBacklight = 3.2
  - customerShadowOpacity = 0.65
  - shadowTableY = (397, 689)

#### `TopDownCheckout/TopDownCamera` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, -10) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **Camera**
  - clearFlags 2 · background RGBA(0.035, 0.035, 0.035, 1) #090909FF · orthographic true · size 3.6 · near 0.3 · far 1000 · depth 0 · cullingMask {m_Bits=4294967295} · viewport {x=0, y=0, width=1, height=1} · HDR 1 · MSAA 1 · targetDisplay 0
- **UniversalAdditionalCameraData**
  - m_RenderShadows = 1
  - m_RequiresDepthTextureOption = 2
  - m_RequiresOpaqueTextureOption = 2
  - m_CameraType = 0
  - m_Cameras = [] (비어 있음)
  - m_RendererIndex = -1
  - m_VolumeLayerMask = {m_Bits=1}
  - m_VolumeTrigger = 없음(None)
  - m_VolumeFrameworkUpdateModeOption = 2
  - m_RenderPostProcessing = 0
  - m_Antialiasing = 0
  - m_AntialiasingQuality = 2
  - m_StopNaN = 0
  - m_Dithering = 0
  - m_ClearDepth = 1
  - m_AllowXRRendering = 1
  - m_AllowHDROutput = 1
  - m_UseScreenCoordOverride = 0
  - m_ScreenSizeOverride = (0, 0, 0, 0)
  - m_ScreenCoordScaleBias = (0, 0, 0, 0)
  - m_RequiresDepthTexture = 0
  - m_RequiresColorTexture = 0
  - m_TaaSettings = {m_Quality=3, m_FrameInfluence=0.1, m_JitterScale=1, m_MipBias=0, m_VarianceClampScale=0.9, m_ContrastAdaptiveSharpening=0}
  - m_Version = 2

#### `TopDownCheckout/TopDownWorkbench` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 Workbench.prefab (씬 override 병합됨)

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (3.8209, 3.8209, 1)
- **SpriteRenderer**
  - sprite `Assets/Textures/art/Facility/Workbench/Stage2TopDownWorkbench.png` → Sprite `Stage2TopDownWorkbench` · color RGBA(1, 1, 1, 1) #FFFFFFFF · materials [`Assets/Materials/Checkout/WorkbenchLighting.mat`]
  - sortingLayerID 0 · sortingOrder -20 · flipX false · flipY false · drawMode 0 · size (13.0625, 7.35156) · maskInteraction 0 · spriteSortPoint 0
- **DystopiaWorkbenchLighting**
  - dayNight = → `TopDownCheckout` (DystopiaDayNight)
  - lightCenter = (0.5, 0.5)
  - lightRadius = 1
  - normalStrength = 0.55

#### `TopDownCheckout/Items`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)

#### `TopDownCheckout/Boundary_Top`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 3.55, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (12.8, 0.1) · isTrigger false · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/Boundary_Bottom`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, -3.55, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (12.8, 0.1) · isTrigger false · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/Boundary_Left`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (-6.35, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (0.1, 7.2) · isTrigger false · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/Boundary_Right`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (6.35, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (0.1, 7.2) · isTrigger false · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/TopDownTestCanvas`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (0, 0) · localScale (0, 0, 0)
  - anchorMin (0, 0) · anchorMax (0, 0) (좌하단 고정) · pivot (0, 0)
  - localRotation 0° (identity)
- **Canvas**
  - renderMode 0 (0=ScreenSpaceOverlay) · sortingOrder 1000 · pixelPerfect false · targetDisplay 0 · additionalShaderChannels 0 · vertexColorAlwaysGammaSpace false
- **CanvasScaler**
  - uiScaleMode 1 (1=ScaleWithScreenSize) · referenceResolution (1280, 720) · screenMatchMode 2 · matchWidthOrHeight 0 · referencePixelsPerUnit 100 · dynamicPixelsPerUnit 1
- **GraphicRaycaster**
  - ignoreReversedGraphics true · blockingObjects 0 · blockingMask {m_Bits=4294967295}

##### `TopDownCheckout/TopDownTestCanvas/FrontView`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)

###### `TopDownCheckout/TopDownTestCanvas/FrontView/ExistingInputBlocker`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0.001) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust0`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (12, 6) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 12, 높이 6 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust1`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (16, 10) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 16, 높이 10 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust2`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (20, 6) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 20, 높이 6 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust3`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (12, 10) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 12, 높이 10 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust4`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (16, 6) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 16, 높이 6 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust5`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (20, 10) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 20, 높이 10 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust6`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (12, 6) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 12, 높이 6 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust7`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (16, 10) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 16, 높이 10 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust8`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (20, 6) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 20, 높이 6 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/LandingDust9`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (12, 10) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 12, 높이 10 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 0) #00000000 · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/FrontView/FrontContainer`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (522, -505) · sizeDelta(W×H) (360, 240) · localScale (0.729959, 0.729959, 0.66766)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 522, 위 505, 폭 262.785, 높이 175.19 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Crate/Stage2RustedCrateClosed.png` → Sprite `Stage2RustedCrateClosed`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.02, 0.015, 0.01, 0.8) #050403CC · effectDistance (10, -12) · useGraphicAlpha true · **비활성**
- **DystopiaPixelSource**

##### `TopDownCheckout/TopDownTestCanvas/WorkViewUI` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/CustomerClue`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (28, -20) · sizeDelta(W×H) (440, 54) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 28, 위 20, 폭 440, 높이 54 (scale 적용)
- **Text**
  - text 없음(None)
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 20 · style Normal · alignment MiddleLeft · color RGBA(0.87, 0.86, 0.79, 1) #DEDBC9FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Truncate · bestFit false · raycastTarget true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/Notice`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (315, -640) · sizeDelta(W×H) (620, 44) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 315, 위 640, 폭 620, 높이 44 (scale 적용)
- **Text**
  - text 없음(None)
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 20 · style Normal · alignment MiddleCenter · color RGBA(0.92, 0.77, 0.55, 1) #EBC48CFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Truncate · bestFit false · raycastTarget true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (900, -310) · sizeDelta(W×H) (360, 360) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 900, 위 310, 폭 360, 높이 360 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Workbench/Calculator.png` → Sprite `Calculator`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/PriceInput`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (51, -50) · sizeDelta(W×H) (250, 51) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 51, 위 50, 폭 250, 높이 51 (scale 적용)
- **Text**
  - text 0
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 25 · style Normal · alignment MiddleRight · color RGBA(0.4, 0.58, 0.43, 1) #66946EFF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Truncate · bestFit false · raycastTarget true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit1`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (42.201, -122.871) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 42.201, 위 122.871, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit1/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit1/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit1/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice0.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit2`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (111.962, -122.871) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 111.962, 위 122.871, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit2/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit2/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit2/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice1.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit3`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (181.722, -122.871) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 181.722, 위 122.871, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit3/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit3/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit3/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice2.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit4`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (42.201, -170.526) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 42.201, 위 170.526, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit4/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit4/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit4/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice3.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit5`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (111.962, -170.526) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 111.962, 위 170.526, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit5/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit5/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit5/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice4.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit6`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (181.722, -170.526) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 181.722, 위 170.526, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit6/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit6/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit6/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice5.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit7`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (42.201, -218.182) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 42.201, 위 218.182, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit7/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit7/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit7/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice6.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit8`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (111.962, -218.182) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 111.962, 위 218.182, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit8/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit8/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit8/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice7.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit9`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (181.722, -218.182) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 181.722, 위 218.182, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit9/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit9/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit9/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice8.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Backspace`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (251.77, -122.871) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 251.77, 위 122.871, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Backspace/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Backspace/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Backspace/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice9.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit000`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (251.77, -170.526) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 251.77, 위 170.526, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit000/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit000/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit000/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice10.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit00`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (251.77, -218.182) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 251.77, 위 218.182, 폭 61.4354, 높이 43.3493 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit00/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit00/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Digit00/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (30.7177, -21.6746) · sizeDelta(W×H) (61.4354, 43.3493) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice11.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Clear`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (42.201, -264.689) · sizeDelta(W×H) (130.335, 54.8325) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 42.201, 위 264.689, 폭 130.335, 높이 54.8325 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Clear/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Clear/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Clear/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (65.1675, -27.4163) · sizeDelta(W×H) (130.335, 54.8325) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice12.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Confirm`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (182.297, -264.689) · sizeDelta(W×H) (130.909, 54.8325) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 182.297, 위 264.689, 폭 130.909, 높이 54.8325 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0.035, 0.035, 0.032, 1) #090908FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Confirm/Key` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Confirm/Key` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/RegisterMotion/Register/Confirm/Key`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (65.4545, -27.4163) · sizeDelta(W×H) (130.909, 54.8325) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice13.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget false · maskable true

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/CalculatorToggle`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1214, -659) · sizeDelta(W×H) (54, 58) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1214, 위 659, 폭 54, 높이 58 (scale 적용)
- **Image**
  - sprite `Assets/DystopiaPrototype/Prefabs/UiSlice14.asset` (fileID 21300000)
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true
- **Button**
  - interactable true · transition 0 · targetGraphic → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/CalculatorToggle` (Image)
  - colors normal RGBA(1, 1, 1, 1) #FFFFFFFF / highlighted RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / pressed RGBA(0.784314, 0.784314, 0.784314, 1) #C8C8C8FF / selected RGBA(0.960784, 0.960784, 0.960784, 1) #F5F5F5FF / disabled RGBA(0.784314, 0.784314, 0.784314, 0.501961) #C8C8C880 · multiplier 1 · fade 0.1
  - navigation mode 3 · onClick 리스너는 코드가 런타임에 연결(BindPlacedScreen/BindPlacedUi)
- **DystopiaKeyFeedback**
  - Visual = → `TopDownCheckout/TopDownTestCanvas/WorkViewUI/CalculatorToggle` (RectTransform)

###### `TopDownCheckout/TopDownTestCanvas/WorkViewUI/PouringContainer` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (350, -150) · sizeDelta(W×H) (450, 450) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation z = -90°
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 350, 위 150, 폭 450, 높이 450 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Crate/Stage2RustedCrateOpen.png` → Sprite `Stage2RustedCrateOpen`
  - color RGBA(0.92, 0.88, 0.8, 1) #EBE0CCFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.1, 0.08, 0.06, 0.32) #1A140F52 · effectDistance (0, -3) · useGraphicAlpha true

##### `TopDownCheckout/TopDownTestCanvas/CounterClock`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (1078, 19) · sizeDelta(W×H) (170.5, 144) · localScale (0.918842, 0.918842, 0.765702)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 1078, 위 -19, 폭 156.663, 높이 132.313 (scale 적용)
- **Image**
  - sprite `Assets/Textures/art/Facility/Clock/Stage2RustedClock.png` → Sprite `Stage2Clock`
  - color RGBA(1, 1, 1, 1) #FFFFFFFF · material 기본(UI/Default)
  - type Simple · preserveAspect true · fillCenter true · raycastTarget false · maskable true
- **Shadow**
  - effectColor RGBA(0.015, 0.012, 0.008, 0.95) #040302F2 · effectDistance (0, -6) · useGraphicAlpha true · **비활성**
- **DystopiaPixelSource**

###### `TopDownCheckout/TopDownTestCanvas/CounterClock/BusinessClock`

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (85.25, -49.1667) · sizeDelta(W×H) (122.76, 31.6667) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0.5, 0.5)
  - localRotation 0° (identity)
- **Text**
  - text `09:00`
  - font `Assets/Fonts/Checkout/Mulmaru.otf` · size 24 · style Normal · alignment MiddleCenter · color RGBA(0.36, 0.49, 0.35, 1) #5C7D59FF
  - lineSpacing 1 · richText true · hOverflow Wrap · vOverflow Truncate · bestFit false · raycastTarget true

##### `TopDownCheckout/TopDownTestCanvas/Transition` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged` · 원본 CheckoutUI.prefab (씬 override 병합됨)

- **RectTransform**
  - anchoredPosition (0, 0) · sizeDelta(W×H) (1280, 720) · localScale (1, 1, 1)
  - anchorMin (0, 1) · anchorMax (0, 1) (좌상단 고정) · pivot (0, 1)
  - localRotation 0° (identity)
  - → 1280×720 좌상단 기준 표시 사각형: 왼쪽 0, 위 0, 폭 1280, 높이 720 (scale 적용)
- **Image**
  - sprite 없음(None)
  - color RGBA(0, 0, 0, 1) #000000FF · material 기본(UI/Default)
  - type Simple · preserveAspect false · fillCenter true · raycastTarget true · maskable true

#### `TopDownCheckout/EventSystem` — ⛔ 비활성(SetActive false)

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **EventSystem**
  - m_FirstSelected = 없음(None)
  - m_sendNavigationEvents = 1
  - m_DragThreshold = 10
- **InputSystemUIInputModule**
  - m_SendPointerHoverToParent = 1
  - m_MoveRepeatDelay = 0.5
  - m_MoveRepeatRate = 0.1
  - m_XRTrackingOrigin = 없음(None)
  - m_ActionsAsset = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID -944628639613478452)
  - m_PointAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID -1654692200621890270)
  - m_MoveAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID -8784545083839296357)
  - m_SubmitAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID 392368643174621059)
  - m_CancelAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID 7727032971491509709)
  - m_LeftClickAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID 3001919216989983466)
  - m_MiddleClickAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID -2185481485913320682)
  - m_RightClickAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID -4090225696740746782)
  - m_ScrollWheelAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID 6240969308177333660)
  - m_TrackedDevicePositionAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID 6564999863303420839)
  - m_TrackedDeviceOrientationAction = `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions` (fileID 7970375526676320489)
  - m_DeselectOnBackgroundClick = 1
  - m_PointerBehavior = 0
  - m_CursorLockBehavior = 0
  - m_ScrollDeltaPerTick = 6
- **BaseInput**

#### `TopDownCheckout/SaleArea`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (4.2, -0.0249999, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (4.2, 6.55) · isTrigger true · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/ExcludedArea`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (-5.775, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (0.95, 5.7) · isTrigger true · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/MovementArea`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 0, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **BoxCollider2D**
  - offset (0, 0) · size (11.9, 6.3) · isTrigger true · enabled true · usedByComposite false · edgeRadius 0

#### `TopDownCheckout/DividerBar`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (-5.7, 0, 0) · localRotation(quat) (0, 0, 0.707107, 0.707107) · localScale (1, 1, 1)
- **Rigidbody2D**
  - bodyType 0 · mass 1 · linearDamping 0 · angularDamping 0.05 · gravityScale 1 · interpolate 0 · constraints 0 · collisionDetection 0
- **CapsuleCollider2D**
  - offset (0, 0) · size (3.1, 0.32) · direction 1 · isTrigger false
- **SpriteRenderer**
  - sprite `Assets/Textures/art/Workbench/DividerBar.png` → Sprite `DividerBar` · color RGBA(1, 1, 1, 1) #FFFFFFFF · materials [`Library/PackageCache/com.unity.render-pipelines.universal@35356061dd01/Runtime/Materials/Sprite-Lit-Default.mat`]
  - sortingLayerID 0 · sortingOrder 100 · flipX false · flipY false · drawMode 0 · size (3.548, 1.774) · maskInteraction 0 · spriteSortPoint 0
- **DividerBarController2D**
  - tiltSensitivity = 1.5
  - rotationDamping = 1
  - pushPower = 1.1
  - maxPushForce = 3
  - maxItemVelocity = 5
  - pushableLayers = {m_Bits=1}
  - useMovementBounds = 1
  - minWorldPosition = (-5.95, -3.15)
  - maxWorldPosition = (5.95, 3.15)

#### `TopDownCheckout/Vacuum`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, -4.596, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)
- **SpriteRenderer**
  - sprite `Assets/Textures/art/Workbench/Vacuum.png` → Sprite `Vacuum` · color RGBA(1, 1, 1, 1) #FFFFFFFF · materials [`Library/PackageCache/com.unity.render-pipelines.universal@35356061dd01/Runtime/Materials/Sprite-Lit-Default.mat`]
  - sortingLayerID 0 · sortingOrder 110 · flipX false · flipY false · drawMode 0 · size (2.55, 3.2) · maskInteraction 0 · spriteSortPoint 0
- **DystopiaVacuumController**
  - checkout = → `TopDownCheckout` (DystopiaTopDownTest)
  - grip = → `TopDownCheckout/Vacuum/Grip` (Transform)
  - nozzle = → `TopDownCheckout/Vacuum/Nozzle` (Transform)
  - windMaterial = `Assets/Materials/Checkout/VacuumWind.mat`
  - gripRadius = 0.4
  - suctionRadius = 1.9
  - captureRadius = 0.36
  - suctionAcceleration = 52
  - swallowSeconds = 0.16
  - spitSpeed = 4.8
  - spitInterval = 0.07
  - turnSmoothSeconds = 0.11
  - gripTopViewport = 0.25

##### `TopDownCheckout/Vacuum/Grip`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, 1.216, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)

##### `TopDownCheckout/Vacuum/Nozzle`

Layer 0 · Tag `Untagged`

- **Transform**
  - localPosition (0, -1.472, 0) · localRotation(quat) (0, 0, 0, 1) · localScale (1, 1, 1)

## 7. 머티리얼·셰이더

| 머티리얼 | 셰이더 | 사용 위치 | 저장된 속성 |
|---|---|---|---|
| `Assets/Materials/Checkout/FogBack.mat` | Cashier/2D/PixelFog | DystopiaCanvas/Fog_Back | _MainTex=art/Effects/Fog/FogBack.png, _UseUI 1, _ZTest 8(Always), _FlowSpeedX 0.020335, _FlowSpeedY 0.003675, _Opacity 0.55, _SwirlScale 5, _SwirlStrength 0.4, _SwirlSpeed 0.196, _BottomFadePixels 96, _PixelSize 1, _Seed 17, _Color (1,1,1,1) |
| `Assets/Materials/Checkout/FogMid.mat` | Cashier/2D/PixelFog | DystopiaCanvas/Fog_Mid | _MainTex=FogMid.png, _FlowSpeedX 0.0245, _FlowSpeedY 0.003675, _Opacity 0.65, _SwirlScale 8, _SwirlStrength 1.4, _SwirlSpeed 0.196, _BottomFadePixels 96, _Seed 130, 나머지 동일 |
| `Assets/Materials/Checkout/FogFront.mat` | Cashier/2D/PixelFog | DystopiaCanvas/Fog_Front | _MainTex=FogFront.png, _FlowSpeedX 0.030625, _Opacity 0.35, _SwirlScale 14, _SwirlStrength 0.9, _Seed 243, 나머지 동일 |
| `Assets/Materials/Checkout/CityLights.mat` | Cashier/UI/CityLights | DystopiaCanvas/CityLights | 속성 없음(무채색 픽셀 제거 셰이더, chroma<0.18 알파 0) |
| `Assets/Materials/Checkout/GuardNeutral.mat` | Dystopia/WatchNeutral | LeftWatchGuard, RightWatchGuard (`DystopiaScreen.guardTone`) | _Brightness 0.65, _GrayRegion (0,0,1,1), _PixelGrid (20,15,0,0) |
| `Assets/Materials/Checkout/LeftTowerNeutral.mat` | Dystopia/WatchNeutral | LeftWatchTower(비활성), LeftWatchRail | _Brightness 0.8, _GrayRegion (0.0622, 0, 0.1483, 0.6791) |
| `Assets/Materials/Checkout/RightTowerNeutral.mat` | Dystopia/WatchNeutral | RightWatchTower(비활성), RightWatchRail | _Brightness 0.8, _GrayRegion (0.884, 0, 0.942, 0.6366) |
| `Assets/Materials/Checkout/WorkbenchLighting.mat` | Cashier/WorkbenchLighting | TopDownWorkbench(탑다운, 비활성) | _NormalMap=art/Facility/Workbench/TopDownWorkbenchNormal.png, _LightRadius 1, _NormalStrength 0.55, _SpotIntensity 0.55, _AmbientTint (0.72,0.72,0.72,1), _LightCenter (0.5,0.5), _SpotTint (1,0.92,0.76,1) |
| `Assets/Materials/Checkout/VacuumWind.mat` | Sprites/Default(내장) | Vacuum(탑다운) | 기본값 |
| `URP 패키지 Sprite-Lit-Default.mat` | Universal Render Pipeline/2D/Sprite-Lit-Default | DividerBar, Vacuum SpriteRenderer | 패키지 기본 머티리얼(guid a97c1056…) |

셰이더 파일(모두 전달): `Assets/Shaders/Checkout/PixelStageLighting.shader` (GUID 4bb3a3480b9d7864ba03db51d2f31812, PixelStage 전용, Universal2D 패스, 249줄), `Assets/Shaders/PixelFog.shader` (6816116619de75b4db7736c3398ba82a), `Assets/Shaders/Checkout/CityLights.shader` (52df7d4a0b3c4104c96cd7e96b8422c4), `Assets/Shaders/Checkout/WatchNeutral.shader` (03b26e74212f4726ba6484795982af38), `Assets/Shaders/Checkout/WorkbenchLighting.shader` (bd543c809b02b6d4ab4020947c3be635). PixelStageLighting은 씬의 `DystopiaPixelStage.lightingShader` 필드로 직접 참조되어 빌드에 포함된다.

## 8. 스크립트 (전달 대상 전체)와 시각에 영향을 주는 동작

| 파일 | 역할 | 배치에 미치는 영향 |
|---|---|---|
| `Assets/DystopiaPrototype/Scripts/DystopiaScreen.cs` | 정면 화면 로직: 손님 Sprite 교체, 호흡·대기열 전진·퇴장 연출, 연기·경비병 애니메이션, 지침서·가계부·모달 생성/재사용, 키 입력 | 씬 배치를 읽어 기준값으로 저장(`idleOrigins`, `placedPeopleScales`)하고 상대 이동만 한다. `Rect()`가 이미 있는 자식 오브젝트를 찾으면 새로 만들지 않고 재사용하므로 지침서·가계부 내부 배치도 씬이 소유한다 |
| `Assets/DystopiaPrototype/Scripts/DystopiaSceneBindings.cs` | `BindPlacedScreen()`/`BindPlacedUi()`: 이름으로 씬 오브젝트를 찾아 연결. 이름·부모를 바꾸면 연결 실패 | 찾는 이름: DystopiaCanvas, Customer, WaitingLeft, WaitingRear, CrowdRow0~2, Left/RightChimneySmoke, Left/RightWatchGuard, WatchMuzzleFlash0/1, Basket, PriceInput, DialoguePanel/Dialogue, Feedback, Confirm, DailyInstructionButton, DailyInstruction, DailyLedger, Modal; TopDownTestCanvas/FrontView/FrontContainer, Customer, LandingDust0~9, WorkViewUI/…, CounterClock/BusinessClock, Transition, EventSystem |
| `Assets/DystopiaPrototype/Scripts/DystopiaSession.cs` | 거래·손님 생성·경제 규칙. 외형 번호→파일명 규칙(`AppearanceFileName`) | 손님 Sprite 인덱스 결정 |
| `Assets/DystopiaPrototype/Scripts/DystopiaPixelStage.cs` | 픽셀 렌더·조명(4장 참조) | 레이어 순서·표면·그림자 |
| `Assets/DystopiaPrototype/Scripts/DystopiaPixelSource.cs` | 레이어 Image의 메시를 가로채는 BaseMeshEffect. PixelStage가 자동 추가 | 없음 |
| `Assets/DystopiaPrototype/Scripts/DystopiaDayNight.cs` | 시간대 알파·색 혼합(5장) | canvasRenderer 색만 변경, Inspector 색은 보존 |
| `Assets/DystopiaPrototype/Scripts/DystopiaCrowdImage.cs` | 군중 한 줄을 256열 메시로 만들어 머리·어깨만 미세하게 흔드는 Image 파생 클래스 | CrowdRow0~2는 `Image` 대신 이 컴포넌트를 쓴다(Sprite/색 필드는 Image와 동일) |
| `Assets/DystopiaPrototype/Scripts/DystopiaInspectorPortrait.cs` | 감독관 팝업 호흡(InspectorPortrait.prefab) | 밑단 고정 스케일 호흡 |
| `Assets/DystopiaPrototype/Scripts/DystopiaInstructionText.cs` | 지침서 작은 글자를 32px 글리프로 재생성하는 BaseMeshEffect | 없음(메시만 교체) |
| `Assets/DystopiaPrototype/Scripts/DystopiaLedgerPage.cs` | 가계부 글자·선을 공책 원근으로 투영 | 없음(메시만 변형) |
| `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs (+.Upgrades.cs)` | 탑다운 작업대·상자 착지·계산기·시계 표시 흐름, 정면과 세션 공유 | 정면 상자 `FrontContainer`의 표시/숨김, 착지 먼지 연출(상대), 시계 텍스트 |
| `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaKeyFeedback.cs` | 버튼 호버 확대·눌림 축소(계산기 키, 영업 시작) | 상대 스케일 |
| `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownItem.cs, DystopiaVacuumController.cs, DividerBarController2D.cs, DystopiaWorkbenchLighting.cs, DystopiaWorkbenchUpgrades.cs` | 탑다운 물품 물리·청소기·디바이더·작업대 조명 | 정면 Stage 1 화면과 무관(탑다운 화면) |
| `Assets/DystopiaPrototype/Editor/DystopiaTools.cs, DystopiaFacilityTools.cs, DystopiaCustomerTools.cs, DystopiaTextureFiltering.cs, DystopiaValidation.cs, DystopiaPlayVerification.cs, DystopiaCustomerVariety.cs, TopDownTest/Editor/DystopiaTopDownTestTools.cs` | 에디터 메뉴: 단계 적용/저장, 설비 배치, 손님 등록, 텍스처 필터, 검증, 미리보기 캡처 | 빌드 미포함. `Dystopia > Apply Stage 1 Shop`이 단계 전환의 유일한 공식 경로 |
| `Assets/Scripts/** (Commons/Manager/Scene/Utils)` | 기존 프로젝트 공용 코드(Addressables, 풀링, 씬 전환). 프로토타입 정면 화면은 사용하지 않음 | 없음 |

### 8.1 런타임 연출의 정확한 수치 (코드 상수)

- 손님 호흡: 주기 Normal 2.9s / Heavy 1.75s / Elderly 3.7s × 0.88~1.12, 들숨 비율 0.38(노인 0.25). 이동량은 **렌더 세로 1픽셀**(`GetBreathingPixelStep`), 크기 변화 없음.
- 대기열 전진(`AdvanceQueueVisual`): 0.65s SmoothStep. 시작 스케일 비율 Customer 340/550, WaitingLeft 240/340, WaitingRear 0.8; WaitingRear 시작 위치 = WaitingRear 기준 + (-65, 12). 보행 흔들림 sin×9(가로), |cos|×15(세로), 폭 ±2.5%.
- 어린이(Class Child): `childPortraitScale` 0.4, `childPortraitRise` 0.18 → 표시 220×220, 기준점 Y +99. 대기 슬롯에서는 숨김, 등장 시 0.28s 동안 아래 240에서 상승.
- 거래 후 퇴장: `resultSeconds`(0.85)×0.95 동안 오른쪽으로 폭×0.9 이동, 스케일 1→0.55, 검게 페이드. 표정 아이콘은 (0.8,0.78) 앵커 56×56.
- 군중(`DystopiaCrowdImage`): 줄별 인물 수 34/25/16, 머리·어깨(높이 0.36~0.54 이상)만 ±1~2px.
- 연기: 4프레임을 1.1s+0.13i 간격으로 순환, 기준 위치 ±2/±1.5px 흔들림.
- 경비병: 좌우 회전 `sin(t·0.12)` 스케일 X ±(0.12~1), 외곽을 향할 때 9s 주기 0.12s 총구 불꽃(WatchMuzzleFlash 자식 3장). 탐조등(밤): 렌더 메시만 ±14° 왕복.
- 장바구니 상품: `Basket` 아래에 런타임 생성, 폭 192/높이 216 이내 비율 유지, x=317.5+(slot%5-…)×92±22, y=74~126, 기울기 ±6~18°, 군용식량 1.35배.

## 9. 프리팹

| 프리팹 | GUID | 씬 인스턴스 | 비고 |
|---|---|---|---|
| `Assets/DystopiaPrototype/Prefabs/FrontView.prefab` | f7d7176ddb30caf46b5519836f319e93 | `FrontCounter/DystopiaCanvas` | 정면 Canvas 전체. 씬에 override 다수(위 6장에 병합) |
| `Assets/DystopiaPrototype/Prefabs/CheckoutUI.prefab` | a110fce3f8e0a46438e2a83f4712286b | `TopDownCheckout/TopDownTestCanvas` | 정면 상자·시계·계산기·쏟는 상자 |
| `Assets/DystopiaPrototype/Prefabs/Workbench.prefab` | d07d1bbc9d1adae47ae8a5c1767b0415 | `TopDownCheckout/TopDownWorkbench` | 탑다운 작업대(비활성) |
| `Assets/DystopiaPrototype/Prefabs/InspectorPortrait.prefab` | 39ab40cfac55c8641babdc719bf3c098 | 런타임 생성(감독관 모달) | Body RawImage 134×359 = `art/Characters/Inspector/Inspector.png`, Head(비활성) = InspectorHead.png, `DystopiaInspectorPortrait` breathDepth 0.012, period 3.7 |
| `Product0~3.prefab` | 7753d36c…, 239e805f…, a89a7bee…, 47031f42… | `DystopiaTopDownTest.placedItemPrefabs` | 탑다운 물리 물품(스케일 1.8, LinearDamping 6.5, AngularDamping 0.9, Gravity 0). Legacy Water/Crackers/Can/Rice Sprite |
| `UiSlice0~14.asset` | — | CheckoutUI 계산기 키 Sprite | `art/Workbench/Calculator.png`를 잘라 만든 Sprite 에셋 15개 |
| `Stage2ShopParts.prefab` | 865f407af4229d548bb9e18c8e409df5 | 사용 안 함 | Stage 2 실험용, 참조 없음 |

## 10. 다른 프로젝트/개발자가 똑같이 만드는 절차

1. **그대로 복사하는 경우(권장):** `Assets/DystopiaPrototype/`, `Assets/Textures/art/`, `Assets/Materials/Checkout/`, `Assets/Shaders/`, `Assets/Fonts/Checkout/`, `Assets/Settings/` 를 `.meta`와 함께 복사한다. GUID가 유지되므로 씬·프리팹 참조가 그대로 살아난다. Packages: URP 17.3, Input System 1.19, uGUI 2.0, 2D 패키지가 필요하다.
2. `DystopiaVerticalSlice.unity`를 열고 Play를 종료한 상태에서 `Dystopia > Apply Stage 2 Shop`을 실행한다. 이 메뉴가 `Stage2Reference.unity`에서 4.2 표의 레이어(상판·천막·상자·시계·배경·안개·군중·경비·설비)의 RectTransform·Sprite·색·효과·레이어 설정과 `PouringContainer`, `TopDownWorkbench`, `shadowTableY`를 복사한다. 실행 전 사본을 `output/shop-stage-switch/`에 남긴다.
3. 씬을 저장한다. 이후 Play. `TopDownCheckout`의 DystopiaDayNight `preview`를 켜고 `previewHour`를 9~21로 바꾸면 편집 중에도 시간대를 확인할 수 있다(저장 시 preview는 꺼 둔다).
4. **새로 만드는 경우:** 6장의 계층을 위에서 아래 순서대로 같은 이름으로 만들고(이름이 곧 코드 연결 키), 각 RectTransform·Image 값을 그대로 입력한 뒤, `TopDownCheckout`에 DystopiaTopDownTest/DystopiaDayNight/DystopiaPixelStage를 붙이고 4.1·4.2·5장의 값을 입력한다. PixelStage `layers`의 `source`는 해당 Image 컴포넌트를 드래그해 연결한다. Canvas는 Screen Space Overlay + CanvasScaler(1280×720, match 0).
5. Stage 2에서 **보여야 하는 레이어(활성)**: `FarBackground` (FARBACKGROUND), `DawnBackground` (Dawn), `SunsetBackground` (SunsetClouded), `EveningBackground` (Evening), `CityLights` (CityLights), `Fog_Back` (FogBack), `Fog_Mid` (FogMid), `Fog_Front` (FogFront), `MidBackground` (MidBackground), `LeftChimneySmoke` (ChimneySmoke0), `RightChimneySmoke` (ChimneySmoke1), `LeftWatchGuard` (WatchGuard), `RightWatchGuard` (WatchGuard), `LeftSearchlight` (Searchlight), `RightSearchlight` (Searchlight), `CrowdRow0` (CrowdBack), `CrowdRow1` (CrowdMiddle), `CrowdRow2` (CrowdFront), `Barricade` (BoothBarricade), `WaitingRear` (MalePriceSensitive_01), `WaitingLeft` (FemalePriceSensitive_01), `Customer` (MalePriceSensitive_01), `Canopy` (Stage2Ceiling), `Stage3LeftPillar` (Stage2LeftPillar), `Stage3RightPillar` (Stage2RightPillar), `CounterLeftExtension`, `CounterRightExtension`, `Counter` (Stage2CounterTop), `FacilityToolBench` (Stage2ToolBench), `FacilityPowerCommunications` (Stage2PowerCommunications), `FacilityFoodShelf` (Stage2FoodShelf), `FacilityMedicineCabinet` (Stage2MedicineCabinet), `LandingDust0`, `LandingDust1`, `LandingDust2`, `LandingDust3`, `LandingDust4`, `LandingDust5`, `LandingDust6`, `LandingDust7`, `LandingDust8`, `LandingDust9`, `FrontContainer` (Stage2RustedCrateClosed), `CounterClock` (Stage2Clock).
   **보이면 안 되는 레이어(비활성 저장 또는 Surface Hidden)**: `Core`, `Spark`, `Flame`, `Core`, `Spark`, `Flame`, `FacilityNuclearProtection` (Stage3NuclearProtection), `FacilityPrecisionElectronics` (Stage3PrecisionElectronics), `CounterLamplight` (CounterLight), `Stage3CeilingLamp` (Stage3CeilingLamp). 밤 전용 레이어(Dawn/Sunset/Evening/CityLights/Searchlight)는 활성이어도 낮에는 알파 0이다.

## 11. 검증 상태와 알려진 한계

- STATIC PASS: 권위 씬 + 프리팹 3개 파싱, 참조 자산 174개 존재 확인. 끊어진 참조 1개: `DystopiaScreen.daughter`(guid e4dd2b06…, 삭제된 옛 딸 Sprite) — 코드에서 사용하지 않는 필드라 화면 영향 없음.
- 아트 폴더 재편(2026-09-16, 커밋 a43c1aec)과 코드 경로 교체·컴파일 확인은 Stage 1 인계에서 완료했다.
- 배치 검증 이미지 `_Reference/Stage2_LayoutCheck.png`는 이 문서의 수치만으로 합성한 것이며 조명·시간대·호흡은 포함하지 않는다. 밤 전용 레이어(Dawn/Sunset/Evening/CityLights/Searchlight)는 알파 1로 그려져 실제 낮 화면보다 밝게 보일 수 있다.
- Play Mode 시각 검증(손님 교대, 조명, 그림자)은 사용자가 수행한다. Unity MCP 연결이 시간 초과 상태라 편집기 상태(열린 씬의 미저장 편집)는 반영하지 못했다. **열린 씬에 저장하지 않은 편집이 있다면 Inspector 값이 이 문서보다 우선한다.**
- `Validate Rules` 메뉴는 이전부터 실패 상태(코드 기본 카탈로그에 Sprite 없음). 이번 작업과 무관.

## 12. 관련 문서

- `doc/CUSTOMER_ARTWORK_GUIDE.md` — 손님 60종 분류·배열 순서·어린이 배율
- `Assets/Textures/STAGE_LAYOUT_HANDOFF.md` — Stage 2/3 배치 작업 기록(사람이 쓴 이력; 수치는 단계별 명세 문서가 최신)
- `Assets/Textures/art/STAGE1_HANDOFF.md`, `STAGE2_HANDOFF.md`, `STAGE3_HANDOFF.md` — 단계별 명세(있는 것만)
- `Assets/Textures/ART_HANDOFF.md`, `ART_IMAGE_INVENTORY.md` — 이전 이미지 인계(경로는 재편 전 기준, 3장의 새 경로로 읽을 것)
- `doc/2026-09-14-prototype-handoff.md`, `Assets/DystopiaPrototype/EDITABLE_SCENE.md`, `IMPLEMENTATION_NOTES.md` — 구현 이력
- `AGENTS.md` — 배치 보호 규칙

