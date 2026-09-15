# 고객 원화·분류·Canvas 배치 개발자 가이드

작성 기준: 2026-09-16, `astra-prototype` 작업의 고객 관련 코드와 저장된 Unity Scene/Prefab을 직접 확인했다.

## 1. 먼저 알아야 할 구조

고객은 **남성 30종 + 여성 30종 = 원화 60종**이다. 각 원화에 대응하는 노멀맵도 60개 있다. 남자 클래스와 여자 클래스를 상속해서 구현한 구조가 아니다. 하나의 `DystopiaCustomer` 데이터에 **성별, 외형 인덱스, 외형 분류, 연령 타입**을 보관하고, 화면이 해당 Sprite를 선택한다.

크기와 위치는 다음 순서로 결정된다.

1. Canvas 아래 `Customer`, `WaitingLeft`, `WaitingRear`의 **편집된 RectTransform**이 기본 배치다.
2. `BindPlacedScreen()`이 시작할 때 그 위치와 Scale을 기억한다.
3. 성인은 기본 배치를 사용한다. 아이는 같은 `Customer` 슬롯에 어린이 배율·높이 보정을 적용한다.
4. PNG 원본의 가로세로 비율과 투명 여백에 따라, 사각형 안에서 실제 보이는 인물의 크기가 달라진다.
5. 대기열 이동·등장·호흡은 그 기본 배치에 상대적인 연출이다.

**아이만 작게 만들려고 Customer의 Width/Height 자체를 줄이면 성인까지 작아진다.** 아이만 조절하려면 `DystopiaScreen`의 `어린이 크기`, `어린이 높이`를 사용한다.

## 2. 파일 위치와 이름

```text
Assets/Textures/art/Customer/
├── Male/       # 남성 원화 30 PNG + 각 .meta
├── Female/     # 여성 원화 30 PNG + 각 .meta
└── NormalMap/  # 남녀 각 원화의 대응 노멀맵 60 PNG + 각 .meta
```

원화 이름은 `{Male|Female}{Class}_{분류내번호:00}.png`, 노멀맵은 `{원화이름}_Normal.png`다.

예:

- `Male/MaleNormal_01.png`
- `Female/FemalePriceSensitive_02.png`
- `Male/MaleChild_03.png`
- `NormalMap/MaleChild_03_Normal.png`

`NormalMap`의 Normal은 표면 법선이라는 뜻이다. 고객 분류의 `Normal`(일반 손님)과 별개다. Inspector의 오래된 sub-sprite 이름이 .meta 내부에 남아 있어도 파일명만 보고 GUID나 내부 ID를 다시 만들지 않는다.

## 3. 클래스·인덱스 매핑

권위 구현: [`DystopiaSession.cs`](../Assets/DystopiaPrototype/Scripts/DystopiaSession.cs)의 `ClassAppearanceCounts`, `AppearanceClass()`, `AppearanceNumberInClass()`, `AppearanceFileName()`.

| enum 값 | 분류 | 한 성별의 0-based appearance | 분류 내 파일 번호 | 남성 수 | 여성 수 |
|---|---|---|---|---:|---:|
| 0 / Normal | 일반 | 0–11 | 01–12 | 12 | 12 |
| 1 / Hasty | 절박 | 12–14 | 01–03 | 3 | 3 |
| 2 / PriceSensitive | 가격민감 | 15–17 | 01–03 | 3 | 3 |
| 3 / Wealthy | 부자 | 18–20 | 01–03 | 3 | 3 |
| 4 / Poor | 가난 | 21–23 | 01–03 | 3 | 3 |
| 5 / Child | 아이 | 24–26 | 01–03 | 3 | 3 |
| 6 / Elder | 노인 | 27–29 | 01–03 | 3 | 3 |

```csharp
// 남녀 공통 구간; 총합 30
ClassAppearanceCounts = new[] { 12, 3, 3, 3, 3, 3, 3 };

// 예: 성별과 appearance를 함께 해석한다.
AppearanceFileName(true, 0);   // MaleNormal_01
AppearanceFileName(false, 15);// FemalePriceSensitive_01
AppearanceFileName(true, 24); // MaleChild_01
AppearanceFileName(false,29);// FemaleElder_03
```

배열을 파일명의 알파벳 순서로 정렬하면 안 된다. `Child`, `Elder`가 `Normal`보다 먼저 오므로 인덱스 의미가 깨진다. 등록 도구는 0부터 29까지 `AppearanceFileName()`을 호출해 위 표 순서로 채운다.

### 3.1 서로 다른 세 가지 분류

| 값 | 역할 | 주의점 |
|---|---|---|
| `DystopiaCustomer.IsMale` | 남녀 Sprite 배열 선택, 성별 관련 판정 | 첫 고객은 랜덤, 이후에는 앞 고객과 교대 |
| `DystopiaCustomer.Class` / `DystopiaCustomerClass` | 위 7개 외형 분류 | appearance에서 계산하는 속성; 별도 상속 클래스가 아님 |
| `DystopiaCustomer.Type` / `DystopiaCustomerType` | AdultMale / AdultFemale / Child / Elderly | Child/Elder를 남녀 공통 연령 타입으로 취급 |

**외형의 Poor 분류와 거래 데이터의 `isPoor`는 동일하지 않다.** `isPoor`는 `settings.poorChance`로 별도 추첨된다. Class가 Wealthy라고 예산이 늘거나, PriceSensitive라고 허용 가격이 자동으로 바뀌지 않는다. 현재 Class는 표시·분류와 그에 따른 연령/호흡 대응에 사용된다. 이 아트 분류를 새 가격·성격 밸런스로 해석해서는 안 된다.

### 3.2 재등장 제한

`CreateCustomer()`는 첫 성별을 랜덤으로 고르고 이후 남녀를 교대한다. `IsEligibleAppearance()`는 바로 앞 고객과 같은 Type, 그리고 **같은 성별에서 최근 사용한 외형 3개**를 후보에서 제외한다. 유효 후보 수를 센 뒤 그 후보 안에서 균등 추첨한다. 거절될 때까지 무한 재추첨하는 방식이 아니다.

따라서 같은 남자아이와 여자아이가 바로 이어지는 것도 막는다. 같은 성별·같은 외형은 정상 교대 생성 흐름에서 방문 간격 최소 8까지 떨어진다. 기억 목록은 세션 필드이므로 '당일에만 초기화되는 목록'으로 가정하지 않는다. 클래스별 등장 확률을 정확히 40%/10%로 고정한 시스템도 아니다. 12:3:3:3:3:3:3의 원화 개수에 최근 외형·앞 타입 제외 조건이 함께 작용한다.

## 4. 클래스별 코드 책임과 연결 흐름

| 파일/타입 | 책임 | 주요 필드·메서드 |
|---|---|---|
| [`DystopiaSession`](../Assets/DystopiaPrototype/Scripts/DystopiaSession.cs) | 고객 데이터 생성, 성별·외형·연령 타입, 대기 순서 | `CreateCustomer`, `AppearanceClass`, `AppearanceType`, `WaitingCustomers` |
| 같은 파일의 `DystopiaCustomer` | 한 고객의 주문과 표시 데이터 | `appearance`, `IsMale`, `Type`, `Class`, `basket`, `isPoor` |
| [`DystopiaScreen`](../Assets/DystopiaPrototype/Scripts/DystopiaScreen.cs) | 정면 고객 Sprite 표시, 어린이 보정, 대기열·호흡 | `maleCustomers`, `femaleCustomers`, `Refresh`, `AnimatePeople`, `AdvanceQueueVisual` |
| [`DystopiaSceneBindings.cs`](../Assets/DystopiaPrototype/Scripts/DystopiaSceneBindings.cs) | 기존 Scene의 실제 UI 오브젝트를 찾고 기준 배치 저장 | `BindPlacedScreen`, `idleOrigins`, `placedPeopleScales` |
| [`DystopiaTopDownTest`](../Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs) | 정면과 세션을 공유하는 작업대 흐름, 자체 고객 배열 | `maleCustomers`, `femaleCustomers`, `PlaceContainer`, `CustomerSprite` |
| [`DystopiaPixelStage`](../Assets/DystopiaPrototype/Scripts/DystopiaPixelStage.cs) | UI 그림을 렌더하고 Sprite별 노멀맵 조회 | `Layer.NormalFor`, `NormalVariant`, `normalVariants` |
| [`DystopiaCustomerTools`](../Assets/DystopiaPrototype/Editor/DystopiaCustomerTools.cs) | 원화 import, 노멀맵 생성, 기존 Scene 배열 등록 | 아래 Editor 메뉴 참조 |
| [`DystopiaCustomerVariety`](../Assets/DystopiaPrototype/Editor/DystopiaCustomerVariety.cs) | seed별 가까운 외형 반복 검사용 Editor 도구 | `Validate` |

```text
DystopiaSession.CreateCustomer
  → IsMale + appearance + Type 결정
  → DystopiaScreen.Refresh
  → CustomerPoolSprite(IsMale, appearance)
  → maleCustomers[appearance] 또는 femaleCustomers[appearance]
  → Customer Image.sprite
  → Type == Child이면 어린이 크기·높이 적용
  → PixelStage가 현재 Image/RectTransform을 렌더
```

`CustomerPoolSprite()`에는 배열 길이 modulo와 이전 `customers` 배열 fallback이 있다. 잘못된 등록 순서가 있어도 에러 없이 엉뚱한 얼굴이 나올 수 있으므로 **길이 30뿐 아니라 각 인덱스의 파일명까지** 확인한다.

### 4.1 Editor와 빌드의 차이

`BindEditorCustomers()`와 작업대의 Editor 전용 연결 코드는 `#if UNITY_EDITOR` 안에서 AssetDatabase로 누락된 항목만 보완한다. 이미 다른 Sprite가 들어 있는 배열 항목을 새 분류 순서로 전부 교체하지는 않는다. 실제 빌드에는 AssetDatabase 로딩이 없다. **빌드 전에 두 컴포넌트의 남녀 배열을 제대로 등록하고 Scene에 저장해야 한다.** 폴더에 PNG를 복사하는 것만으로 빌드 연결이 완성되지 않는다.

## 5. Canvas에서 어디를 조절하는가

정면 계층은 다음과 같다. Scene의 prefab 인스턴스 이름은 `DystopiaCanvas`이며 원본 prefab 파일은 `FrontView.prefab`이다.

```text
DystopiaVerticalSlice  [DystopiaScreen]
└── DystopiaCanvas     [Canvas, CanvasScaler; FrontView prefab 인스턴스]
    ├── WaitingRear    [Image + RectTransform]  뒤 대기 손님
    ├── WaitingLeft    [Image + RectTransform]  첫 대기 손님
    └── Customer       [Image + RectTransform]  현재 응대 손님
```

`BindPlacedScreen()`은 `transform.Find("DystopiaCanvas")`로 찾으므로 이름/부모를 임의로 변경하면 연결이 끊긴다. `idlePeople`의 배열 순서는 **0=WaitingLeft, 1=WaitingRear, 2=Customer**다. Hierarchy 출력 순서와 다르다.

### 5.1 확인된 저장 배치

근거: `Assets/DystopiaPrototype/Prefabs/FrontView.prefab`의 기본값에 `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity`의 prefab override를 합산했다. **Unity의 저장되지 않은 현재 편집 상태를 캡처한 값은 아니다.** 열린 씬에서 다르게 조절했다면 Inspector의 현재 값이 우선이다.

CanvasScaler는 `Scale With Screen Size`, Reference Resolution `1280×720`, Match Width Or Height `0`(너비 기준)이다. 아래는 물리 화면 픽셀이 아니라 이 기준 Canvas 단위다.

| 오브젝트 | Anchored Position X,Y | Width×Height / SizeDelta | Local Scale | Anchor Min/Max | Pivot | Preserve Aspect |
|---|---|---|---|---|---|---|
| Customer | 640.13513, -632 | 550×550 | 1,1,1 | 0,1 / 0,1 | 0.5,0 | 켜짐 |
| WaitingLeft | 385, -580 | 340×340 | 1,1,1 | 0,1 / 0,1 | 0.5,0 | 켜짐 |
| WaitingRear | 240, -580 | 240×240 | 1,1,1 | 0,1 / 0,1 | 0.5,0 | 켜짐 |

세 슬롯의 저장 회전은 identity(0°)다. Customer의 prefab 기본 X는 `580`이고, Scene override가 `640.13513`으로 바꾼다. Prefab만 열어 보고 Scene 위치가 580이라고 문서화하면 틀린다. 저장된 Stage1/2/3 참조에서도 같은 Customer X override가 확인된다.

Anchor `(0,1)`은 부모 왼쪽 위 기준이다. Y가 음수이면 아래로 이동한다. Pivot `(0.5,0)`은 **이미지 사각형의 아래쪽 중앙**이다. 따라서 Customer의 `(640.13513,-632)`는 이미지의 왼쪽 위가 아니라 아래 중앙 기준점이다. 그림 안의 발·손 하단은 PNG 투명 여백 때문에 이 기준점과 다를 수 있다.

전체 성인 크기를 조절하려면 Customer의 Width/Height 또는 기존 Scale을 편집한다. 대기 원근감은 WaitingLeft/WaitingRear에서 각각 편집한다. 공통 Canvas Scale을 바꾸면 배경·상판·UI까지 영향을 받으므로 고객 크기 조정 용도로 사용하지 않는다. Play 시작 때 기억한 기준값으로 호흡이 계속 계산되므로 기본 배치는 **Edit Mode에서 조정**한다.

## 6. 어린이만의 크기와 위치 — 실제 계산

### 6.1 Inspector 설정

선택 대상은 `Customer` Image가 아니라 **DystopiaScreen이 붙은 오브젝트**다. Inspector의 `어린이 손님 표시` 섹션에서 다음 두 값을 조절한다.

| Inspector 이름 | 코드 필드 | 저장/기본값 | 범위 | 의미 |
|---|---|---:|---|---|
| 어린이 크기 | `childPortraitScale` | 0.4 | 0.15–1 | 성인 슬롯의 기준 Scale X/Y에 곱하는 배율 |
| 어린이 높이 | `childPortraitRise` | 0.18 | 0–0.6 | 성인 슬롯 높이×기준 Scale Y에 곱해 위로 더하는 비율 |

별도 `ChildCanvas`나 어린이 전용 RectTransform은 없다. 남자아이 3종과 여자아이 3종 모두 같은 두 값을 사용한다. 이미지별 크기 보정표도 현재는 없다. 노인은 어린이 배율을 적용하지 않는다.

### 6.2 공식

```csharp
// 시작 시 Scene에서 기억한 값
basePosition = idleOrigins[2];
baseScale = placedPeopleScales[2];
slotHeight = idlePeople[2].rect.height;

childPosition = basePosition
    + Vector2.up * (slotHeight * baseScale.y * childPortraitRise);
childScale = Vector3.Scale(baseScale,
    new Vector3(childPortraitScale, childPortraitScale, 1));
```

현재 저장값을 대입하면:

```text
슬롯: 550×550
기준 Scale: (1,1,1)
어린이 Scale: (0.4,0.4,1)
표시 사각형: 550×0.4 = 220 → 220×220 Canvas 단위
위로 이동량: 550×1×0.18 = 99
아래 중앙 기준점: (640.13513, -632+99) = (640.13513, -533)
정사각 PNG 사각형의 왼쪽 위: (530.13513, -313)
```

이 계산에서 **높이 보정 99에 어린이 배율 0.4를 다시 곱하지 않는다.** 기준 성인 슬롯 높이를 사용하는 것이 현재 구현이다. 또 `sizeDelta`를 220으로 저장하는 것이 아니라 550을 유지한 채 Scale을 0.4로 표시한다. Customer Scale도 0.4로 줄여 놓으면 추가 곱셈 때문에 최종 0.16배가 된다.

### 6.3 조절 예제

- 아이만 10% 더 크게: `어린이 크기 0.4 → 0.44`, 표시 사각형 `220 → 242`.
- 아이를 Canvas 기준 10만큼 더 위로: `어린이 높이 0.18 → 0.18 + 10/550 ≈ 0.19818`.
- 기본 Customer Scale Y가 0.8이면 분모는 `550×0.8=440`이므로 같은 10 이동에 `10/440`을 더한다.
- `어린이 크기`만 바꾸면 아래 중앙 기준점은 유지된다. 아래 Pivot 때문에 상단이 위로 커진다.

### 6.4 PNG 여백까지 고려한 실제 보이는 크기

uGUI의 `Preserve Aspect`는 **전체 Sprite 사각형**을 맞춘다. 사람 픽셀의 알파 외접 영역을 자동으로 확대하지 않는다. PNG가 같은 1000×1000이어도 얼굴 주변 여백이 크면 작은 사람처럼 보인다. PPU 100을 바꾸는 것으로 이 수동 Image 사각형 문제를 해결하지 않는다.

슬롯 W,H와 원본 w,h에서 `k=min(W/w,H/h)`. 부모 추가 Scale이 없다는 조건에서 실제 알파 영역 표시 크기는 `(bboxWidth×k×ScaleX, bboxHeight×k×ScaleY)`다. 아이는 ScaleX/Y에 어린이 배율을 포함한다. 아래 수치는 알파 `>8` 기준으로 원본을 직접 측정했고, 호흡·등장 연출을 제외한 `220×220` 최종 사각형을 기준으로 계산했다.

| 원화 | PNG 전체 | 알파 bbox 크기 | 실제 보이는 크기(약, Canvas 단위) |
|---|---|---|---|
| MaleChild_01 | 1254×1254 | 1103×959 | 193.5×168.2 |
| MaleChild_02 | 1254×1254 | 1072×1088 | 188.1×190.9 |
| MaleChild_03 | 1254×1254 | 1135×985 | 199.1×172.8 |
| FemaleChild_01 | 1000×1000 | 765×810 | 168.3×178.2 |
| FemaleChild_02 | 1000×1000 | 815×860 | 179.3×189.2 |
| FemaleChild_03 | 1000×1000 | 770×895 | 169.4×196.9 |

따라서 남녀 아이가 모두 0.4여도 실루엣 높이가 같지 않다. 개별 원화를 교체할 때에는 원본 해상도뿐 아니라 이 bbox와 손 하단 위치를 함께 비교한다. 최종 Game View에서는 상판 가림과 조명도 추가 영향을 준다.

## 7. 어린이 등장과 대기열 동작

- `Refresh()`는 Child가 첫 프레임에 성인 크기로 잠깐 보이지 않도록 바로 어린이 위치/Scale을 적용한다.
- `WaitingLeft`와 `WaitingRear`는 해당 대기 고객이 Child이면 숨긴다. 데이터 대기열에서는 삭제하지 않는다. 어린이는 앞 차례가 되면 테이블 아래에서 나타난다.
- 정면 `AdvanceQueueVisual()` 전체 이동 시간은 0.65초다. 어린이는 첫 0.28초 동안 `1-(1-t)^3` easing으로, 목표점 아래 240에서 목표점으로 올라온다. 표시 기준점 예제라면 Y `-773 → -533`이다.
- 호흡 갱신은 이동 코루틴 중에는 개입하지 않는다. 정지 상태에서 원래 기준점에 렌더 한 픽셀에 해당하는 상하 이동만 더한다.
- 작업대 `PlaceContainer()`는 Child일 때 정면 상자를 숨기고 상자 착지 효과를 생략한다. 자체 고객 Image가 연결된 경로에는 0.28초/아래 240의 등장 처리도 있다. 이후 0.57초를 기다려 총 0.85초 흐름을 유지한다.
- 성인 대기열의 보간 시작 Scale 비율 `340/550`, `240/340`, `0.8`은 코드에 남아 있다. Canvas 슬롯 크기를 크게 변경해도 이 중간 이동 비율이 자동 산출되는 구조는 아니다. 최종 정착 기준값과 이동 도중 원근 비율을 구분해 검토한다.

## 8. 호흡 분류

등록 도구의 `FillBreathing()`은 Sprite 배열과 같은 순서로 남녀 각각 30개 값을 채운다.

| 고객 분류 | BreathStyle |
|---|---|
| Normal / PriceSensitive / Wealthy / Child | Normal (0) |
| Hasty / Poor | Heavy (1) |
| Elder | Elderly (2) |

기본 주기는 Normal 2.9초, Heavy 1.75초, Elderly 3.7초이며 외형/슬롯 seed에 따라 0.88–1.12배가 적용된다. 숨 들이쉬는 비율은 Elderly 0.25, 나머지 0.38이다. 위치·Scale을 매번 랜덤 재설정하는 것이 아니라 Scene에서 기억한 배치에 상대적인 움직임이다.

소스에 남아 있는 초기 호흡 배열은 이전 외형 목록 기준(남 24/여 16)이므로 새 Scene 생성 시 그 기본값을 새 60종의 정답으로 사용하지 않는다. 이 고객 인계의 Scene 직렬화 배열/등록 도구가 새 분류 순서를 제공한다. 호흡 픽셀 이동량 계산은 `GetBreathingPixelStep()`을 참조한다.

## 9. Import 규격과 노멀맵

### 9.1 원화

`Import Customer Artwork` 기준: Sprite Single, PPU 100, Bilinear, Mipmap Off, Clamp, NPOT None, Uncompressed, sRGB On, Alpha Is Transparency On, Max Size 2048. 실제 저장된 .meta도 함께 버전 관리한다. 별도의 Sprite atlas나 Resources 폴더 자동 검색으로 로딩하는 구조가 아니다.

### 9.2 노멀맵

파일 존재만으로 화면에 적용되지는 않는다. `DystopiaPixelStage.Layer.normalVariants`에 Sprite/Texture2D 쌍이 있어야 하고, `relightingTrial`, `useCustomerNormalMap` 등의 조명 조건도 충족해야 한다. `NormalFor(sprite)`는 대응표를 먼저 찾고 없으면 기존 `normalSprite`/`normalMap` 단일 연결로 돌아간다.

노멀맵은 Unity Texture Type **Default**, sRGB Off인 일반 RGB 텍스처다. 셰이더에서 직접 tangent-space 값을 읽으므로 무조건 Unity의 Normal Map 타입으로 바꾸지 않는다. 생성 크기는 최대 변 256이며 Bilinear, Mipmap Off, Clamp, Uncompressed를 목표로 한다. 저장된 예제 .meta는 루트 maxTextureSize 2048, DefaultTexturePlatform 256을 함께 기록한다. 원화와 혼동하여 숫자 하나만 보고 import 상태를 판단하지 않는다.

생성 방식은 원본 비율 유지·최대 변 256 축소 → 알파 평균/명암 계산 → 알파를 넓게 블러해 몸통 부피 추정 → `몸통 0.82 + 명암 0.18`의 높이 → Sobel 기울기 → RGB 법선 인코딩이다. 투명 영역에는 `(128,128,255,255)`를 쓴다. 실제 3D 형상에서 베이크한 노멀은 아니다.

작업 폴더의 저장 Scene에서는 `useCustomerNormalMap=0`, `normalVariants=[]`가 확인됐다. **이번 인계에서 노멀맵을 자동 활성화하지 않는다.** 생성 파일과 연결 기능을 제공하며, 사용할 경우 의도적으로 등록하고 시각 검증한다. 다른 Stage 참조의 조명 설정과 혼동하지 않는다.

## 10. 개발자가 새 원화를 추가하거나 교체하는 절차

### 기존 60종 이미지 교체

1. 같은 경로·파일명으로 PNG를 교체하고 기존 `.meta`/GUID를 보존한다.
2. 크기, 알파 여백, bbox, 얼굴·손 하단 위치를 기존 원화와 비교한다.
3. 필요할 때만 `Dystopia/손님/1. Import Customer Artwork`를 실행한다.
4. 노멀맵을 실제 사용하는 프로젝트라면 해당 원화의 노멀도 갱신한다.
5. 앞 슬롯과 대기 슬롯에서 확인하고, 아이는 어린이 표시 필드로 먼저 맞춘다.

### 종류 수/구간 변경

1. `DystopiaCustomerClass` 순서와 `ClassAppearanceCounts`를 먼저 합의한다.
2. 남녀 두 폴더 모두 해당 구간 파일을 빠짐없이 준비한다.
3. `AppearanceFileName()` 결과가 정확히 기존/새 파일과 일치하는지 확인한다.
4. Edit Mode에서 대상 Scene을 확인하고 `Dystopia/손님/3. Register Customers In Scene`을 실행한다.
5. `DystopiaScreen`, `DystopiaTopDownTest` 양쪽 남녀 배열 길이/순서를 확인한다.
6. 호흡 배열 길이도 남녀 각각 30(또는 새 총수)인지 확인한다.
7. 빌드용 Scene을 사용자가 의도한 시점에 저장한다. 저장되지 않은 다른 배치를 등록 때문에 강제로 덮어쓰지 않는다.

등록 도구는 실행 전 `output/customer-swap/<timestamp>/before.unity`에 복사본을 만들고, 열린 씬을 dirty 상태로 남긴다. 자동으로 원래 씬을 저장하지 않는다. 배열과 호흡/노멀 연결을 교체하며 Transform은 재배치하지 않는다. null인 Customer/Waiting Image에만 초기 Sprite를 채운다. **Register 메뉴는 노멀 대응표도 함께 연결하므로 원화만 바꿀 때 무조건 실행하지 않는다.**

`Unlink Normal Maps` 메뉴는 이름만 보고 고객 전용으로 가정하지 않는다. 구현상 stage.layers에서 연결이 있는 레이어를 폭넓게 비우므로, 다른 소품 노멀 연결을 보존해야 하는 작업에는 실행 전에 범위를 확인한다.

## 11. 검증 방법과 인계 범위

- 원화 60개/노멀 60개, 파일+.meta 짝과 GUID 중복을 확인한다.
- 남녀 각각 0–29를 파일명 함수로 순회해 Sprite 배열 순서를 비교한다.
- Child 구간이 24–26, Elder가 27–29인지 확인한다.
- 아이 6종 모두 앞 슬롯에서 성인 크기 한 프레임 노출, 손 하단 가림, X 정렬, 0.28초 등장과 상자 숨김을 확인한다.
- 대기열에서 Child가 숨겨져도 실제 고객 데이터가 앞 차례로 정상 이동하는지 확인한다.
- `Dystopia/손님/Validate Appearance Variety`는 seed 1–40으로 초기 영업 대기열을 검사한다. 계속 보충되는 하루 전체 고객/여러 날까지 검증했다고 해석하면 안 된다. 출력의 성별 이름은 방문 홀짝에서 만들어 실제 첫 성별과 반대일 수 있지만, 같은 성별 그룹의 반복 간격 검사는 가능하다.
- 실제 화면 검증은 사용자가 Play Mode를 제어한다. 정적 문서 수치나 C# 빌드 통과를 최종 시각 검증으로 대체하지 않는다.

이 인계는 고객 폴더와 분류/표시/등록에 필요한 고객 코드·기존 Scene의 고객 배열만 대상으로 한다. 다른 작업의 상판·설비·상자·시계·카메라·조명·Canvas 배치 변경은 포함하지 않는다. 기존 `Checkout/Characters/Customers` 자산의 로컬 삭제도 이번 푸시에 포함하지 않아 이전 prefab/Stage 참조를 갑자기 끊지 않는다. 새 자산과 옛 자산은 GUID가 다르므로 단순 경로 rename으로 간주하지 않는다.

Unity MCP 연결이 직전 작업부터 시간 초과 상태여서, 본 문서의 배치는 **저장 파일 정적 검사**를 근거로 한다. 해당 작업에서 Play Mode를 실행하거나 열린 씬을 저장·재로드하지 않았다.

### 이번 인계에서 수행한 확인

- 커밋 대상 C# 소스를 Git index에서 별도 폴더로 추출하여 Runtime/Editor 프로젝트를 빌드했다. 최종 빌드 오류 0개. Unity의 재import/Console/Play Mode 결과와는 별도의 정적 컴파일 검증이다.
- 원화 60개와 대응 노멀맵 60개의 .meta 짝을 확인했다. 이 고객 자산 GUID가 커밋 전체의 다른 .meta와 중복되는 경우는 0개다.
- 커밋할 Scene의 남녀 배열 4개(정면/작업대 각 2개)가 각각 30개이며 위 분류/파일명 순서와 일치한다.
- 문서의 로컬 파일 링크가 커밋에 존재하는지 확인했다.
- 코드·Scene·문서의 변경 줄 공백 검사를 통과했다. Unity 생성 .meta의 빈 값 뒤 공백과 CRLF는 값 변경 없이 유지했다.
- 고객 외 파일 경로와 기존 고객 파일 삭제를 커밋에서 제외했다. 열린 Scene 파일을 수정하거나 저장하지 않고, 이미 있던 Scene 변경 중 고객 배열과 어린이 필드만 Git index에 반영했다.

원화는 작업 폴더에 있던 사용자 제공 자산을 인계하며, 이번 문서화 작업에서 새 외부 이미지나 별도 라이선스를 추가하지 않았다. 재배포 권한이 필요한 배포에서는 프로젝트의 원본 출처/권한 기록을 따른다.

## 12. 원본별 해상도·알파 영역 목록

아래 bbox는 원본 PNG의 알파 `>8` 픽셀 외접 사각형이다. 시작점은 **이미지 왼쪽 위 기준 X,Y**, 크기는 W×H다. Unity의 Sprite rect Y(아래 기준)와 좌표계를 혼동하지 않는다. Import Max Size가 적용되면 실제 GPU 텍스처 해상도는 달라질 수 있지만 같은 종횡비/여백 비율로 해석한다.

| 성별/원화 | 원본 W×H | bbox 시작 X,Y | bbox W×H |
|---|---|---|---|
| [MaleNormal_01](../Assets/Textures/art/Customer/Male/MaleNormal_01.png) | 1018×1544 | 15,52 | 981×1492 |
| [MaleNormal_02](../Assets/Textures/art/Customer/Male/MaleNormal_02.png) | 1019×1544 | 25,24 | 977×1520 |
| [MaleNormal_03](../Assets/Textures/art/Customer/Male/MaleNormal_03.png) | 1018×1544 | 42,43 | 954×1501 |
| [MaleNormal_04](../Assets/Textures/art/Customer/Male/MaleNormal_04.png) | 610×925 | 50,25 | 525×900 |
| [MaleNormal_05](../Assets/Textures/art/Customer/Male/MaleNormal_05.png) | 610×925 | 130,30 | 455×895 |
| [MaleNormal_06](../Assets/Textures/art/Customer/Male/MaleNormal_06.png) | 610×925 | 65,45 | 460×880 |
| [MaleNormal_07](../Assets/Textures/art/Customer/Male/MaleNormal_07.png) | 610×925 | 15,25 | 580×900 |
| [MaleNormal_08](../Assets/Textures/art/Customer/Male/MaleNormal_08.png) | 610×925 | 70,15 | 500×910 |
| [MaleNormal_09](../Assets/Textures/art/Customer/Male/MaleNormal_09.png) | 610×925 | 50,35 | 545×890 |
| [MaleNormal_10](../Assets/Textures/art/Customer/Male/MaleNormal_10.png) | 610×925 | 25,25 | 580×900 |
| [MaleNormal_11](../Assets/Textures/art/Customer/Male/MaleNormal_11.png) | 610×925 | 65,55 | 490×870 |
| [MaleNormal_12](../Assets/Textures/art/Customer/Male/MaleNormal_12.png) | 610×925 | 95,30 | 460×895 |
| [MaleHasty_01](../Assets/Textures/art/Customer/Male/MaleHasty_01.png) | 816×996 | 84,24 | 660×972 |
| [MaleHasty_02](../Assets/Textures/art/Customer/Male/MaleHasty_02.png) | 816×996 | 72,42 | 708×954 |
| [MaleHasty_03](../Assets/Textures/art/Customer/Male/MaleHasty_03.png) | 816×996 | 186,24 | 492×972 |
| [MalePriceSensitive_01](../Assets/Textures/art/Customer/Male/MalePriceSensitive_01.png) | 732×1110 | 42,12 | 648×1098 |
| [MalePriceSensitive_02](../Assets/Textures/art/Customer/Male/MalePriceSensitive_02.png) | 659×1000 | 87,33 | 524×966 |
| [MalePriceSensitive_03](../Assets/Textures/art/Customer/Male/MalePriceSensitive_03.png) | 710×1075 | 30,10 | 680×1065 |
| [MaleWealthy_01](../Assets/Textures/art/Customer/Male/MaleWealthy_01.png) | 815×945 | 35,10 | 745×935 |
| [MaleWealthy_02](../Assets/Textures/art/Customer/Male/MaleWealthy_02.png) | 930×1080 | 30,20 | 880×1055 |
| [MaleWealthy_03](../Assets/Textures/art/Customer/Male/MaleWealthy_03.png) | 1116×1296 | 150,48 | 828×1248 |
| [MalePoor_01](../Assets/Textures/art/Customer/Male/MalePoor_01.png) | 762×1062 | 42,6 | 702×1056 |
| [MalePoor_02](../Assets/Textures/art/Customer/Male/MalePoor_02.png) | 816×996 | 48,6 | 720×990 |
| [MalePoor_03](../Assets/Textures/art/Customer/Male/MalePoor_03.png) | 858×948 | 54,6 | 750×942 |
| [MaleChild_01](../Assets/Textures/art/Customer/Male/MaleChild_01.png) | 1254×1254 | 81,227 | 1103×959 |
| [MaleChild_02](../Assets/Textures/art/Customer/Male/MaleChild_02.png) | 1254×1254 | 113,133 | 1072×1088 |
| [MaleChild_03](../Assets/Textures/art/Customer/Male/MaleChild_03.png) | 1254×1254 | 59,186 | 1135×985 |
| [MaleElder_01](../Assets/Textures/art/Customer/Male/MaleElder_01.png) | 810×1235 | 60,25 | 685×1210 |
| [MaleElder_02](../Assets/Textures/art/Customer/Male/MaleElder_02.png) | 915×1095 | 100,35 | 750×1060 |
| [MaleElder_03](../Assets/Textures/art/Customer/Male/MaleElder_03.png) | 915×1095 | 90,90 | 805×1005 |
| [FemaleNormal_01](../Assets/Textures/art/Customer/Female/FemaleNormal_01.png) | 930×1080 | 115,40 | 705×1040 |
| [FemaleNormal_02](../Assets/Textures/art/Customer/Female/FemaleNormal_02.png) | 1164×1351 | 256,35 | 652×1316 |
| [FemaleNormal_03](../Assets/Textures/art/Customer/Female/FemaleNormal_03.png) | 1164×1351 | 228,24 | 752×1327 |
| [FemaleNormal_04](../Assets/Textures/art/Customer/Female/FemaleNormal_04.png) | 1164×1351 | 217,53 | 696×1298 |
| [FemaleNormal_05](../Assets/Textures/art/Customer/Female/FemaleNormal_05.png) | 1164×1351 | 227,34 | 723×1317 |
| [FemaleNormal_06](../Assets/Textures/art/Customer/Female/FemaleNormal_06.png) | 1164×1351 | 249,32 | 702×1319 |
| [FemaleNormal_07](../Assets/Textures/art/Customer/Female/FemaleNormal_07.png) | 1164×1351 | 263,17 | 656×1334 |
| [FemaleNormal_08](../Assets/Textures/art/Customer/Female/FemaleNormal_08.png) | 1164×1351 | 275,23 | 626×1328 |
| [FemaleNormal_09](../Assets/Textures/art/Customer/Female/FemaleNormal_09.png) | 1164×1351 | 217,14 | 754×1337 |
| [FemaleNormal_10](../Assets/Textures/art/Customer/Female/FemaleNormal_10.png) | 1164×1351 | 271,24 | 618×1327 |
| [FemaleNormal_11](../Assets/Textures/art/Customer/Female/FemaleNormal_11.png) | 930×1080 | 115,30 | 745×1045 |
| [FemaleNormal_12](../Assets/Textures/art/Customer/Female/FemaleNormal_12.png) | 1147×1372 | 26,20 | 1080×1326 |
| [FemaleHasty_01](../Assets/Textures/art/Customer/Female/FemaleHasty_01.png) | 930×1080 | 120,30 | 710×1030 |
| [FemaleHasty_02](../Assets/Textures/art/Customer/Female/FemaleHasty_02.png) | 186×216 | 20,4 | 149×211 |
| [FemaleHasty_03](../Assets/Textures/art/Customer/Female/FemaleHasty_03.png) | 186×216 | 21,1 | 162×214 |
| [FemalePriceSensitive_01](../Assets/Textures/art/Customer/Female/FemalePriceSensitive_01.png) | 1002×1164 | 138,30 | 702×1134 |
| [FemalePriceSensitive_02](../Assets/Textures/art/Customer/Female/FemalePriceSensitive_02.png) | 1150×1770 | 60,100 | 1060×1670 |
| [FemalePriceSensitive_03](../Assets/Textures/art/Customer/Female/FemalePriceSensitive_03.png) | 744×1086 | 42,78 | 654×1008 |
| [FemaleWealthy_01](../Assets/Textures/art/Customer/Female/FemaleWealthy_01.png) | 1165×1350 | 25,17 | 1123×1305 |
| [FemaleWealthy_02](../Assets/Textures/art/Customer/Female/FemaleWealthy_02.png) | 972×1134 | 36,18 | 912×1116 |
| [FemaleWealthy_03](../Assets/Textures/art/Customer/Female/FemaleWealthy_03.png) | 1128×1280 | 88,24 | 1000×1232 |
| [FemalePoor_01](../Assets/Textures/art/Customer/Female/FemalePoor_01.png) | 1044×1386 | 156,48 | 732×1338 |
| [FemalePoor_02](../Assets/Textures/art/Customer/Female/FemalePoor_02.png) | 870×1155 | 100,20 | 685×1135 |
| [FemalePoor_03](../Assets/Textures/art/Customer/Female/FemalePoor_03.png) | 912×1212 | 120,36 | 690×1176 |
| [FemaleChild_01](../Assets/Textures/art/Customer/Female/FemaleChild_01.png) | 1000×1000 | 120,125 | 765×810 |
| [FemaleChild_02](../Assets/Textures/art/Customer/Female/FemaleChild_02.png) | 1000×1000 | 95,110 | 815×860 |
| [FemaleChild_03](../Assets/Textures/art/Customer/Female/FemaleChild_03.png) | 1000×1000 | 135,65 | 770×895 |
| [FemaleElder_01](../Assets/Textures/art/Customer/Female/FemaleElder_01.png) | 865×1155 | 100,40 | 665×1115 |
| [FemaleElder_02](../Assets/Textures/art/Customer/Female/FemaleElder_02.png) | 815×1225 | 85,50 | 690×1175 |
| [FemaleElder_03](../Assets/Textures/art/Customer/Female/FemaleElder_03.png) | 650×865 | 30,65 | 565×800 |
