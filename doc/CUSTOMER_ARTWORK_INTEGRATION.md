# 완성 손님 아트 60종 실게임 통합 구현 계획

> 2026-09-16 `codex/store-resource-exchange`에 `total_merge 5c4d46d`를 병합하면서 기존 승인된 `image_resource_idx` 빈칸 사각형 계약을 유지했다. 컬러 FK는 `uint?`, 지정 값의 오류는 계속 거부한다. `normal_resource_idx`는 이번 60행의 필수 계약을 유지한다. 아래 필수 컬러 설명은 원본 작업 계획이며 이 브랜치의 빈칸 허용 계약은 [이미지 연결 명세](IMAGE_RESOURCE_INTEGRATION.md)를 따른다. 새 60행과 120개 컬러/노멀 주소를 채택하고 가게용 4301/4302·4378~4391은 기존 작업 결과를 보존한다.

기준일: 2026-09-16\
대상 브랜치: `customer_fit`\
대상 작업자: 이 문서만 전달받은 구현 담당자(Luna 포함)

## 1. 목적과 완료 조건

`Assets/Textures/Customer/Dystopia/`에 이미 반입된 손님 컬러 Sprite 60종과 `NormalMaps/`의 대응 노멀맵 60종을 현재 게임의 손님 생성, 데이터 로드, Addressables, 월드 표시 경로에 연결한다. 새 고객 시스템을 만들지 않고 현재 `CustomerCompositionSelector → CustomerComposition → CustomerVisit → GameUIController → CustomerWorldQueueView` 흐름을 확장한다.

완료 시 다음이 모두 성립해야 한다.

- 실제 runtime roster는 새 아트 60종이며 남녀 각각 30종이다.
- 손님 성향, 성별, 연령과 선택된 외형이 항상 일치한다.
- `CustomerAppearanceData.csv`가 컬러 Sprite와 노멀맵의 `ResourceData.idx`를 모두 소유한다.
- 컬러 Sprite와 노멀맵 모두 `ResourceData.csv` 및 Addressables를 통해 로드된다.
- 월드 손님 `SpriteRenderer`가 방문별 컬러 Sprite와 대응 노멀맵을 함께 사용한다.
- `CustomerVisit.Attributes`가 `Child`인 경우 크기 `0.6`을 적용하고, 크기 증가용 하단 보존값 위에 계산대 sink `8px`를 추가한다. Adult와 Elderly는 기존 크기를 유지한다.
- 플레이 테스트는 하지 않는다. Unity Editor import/직렬화/Addressables 검사, 컴파일, EditMode 테스트만 수행한다.

이 문서는 구현 계획이며 현재 코드, CSV, Prefab, Addressables가 이미 수정됐다는 뜻은 아니다.

## 2. 필수 저장소 규칙

작업 시작 전에 `doc/WORK_RULES.md`, `doc/INDEX.md`, `doc/CODING_RULES.md`, `doc/DATA_RULES.md`, `doc/CSV_RULES.md`, `doc/PREFAB_RESOURCE_RULES.md`를 읽고 적용한다. 테스트 실행·로그 보관은 `doc/TESTING.md`를 따른다.

작업 전 `git status`, 현재 branch, dirty/untracked 파일을 기록하고 무관한 사용자 변경을 보존한다. Addressables group/address 변경은 보호 변경이므로 저장소 규칙의 승인·교차 검토 절차를 따른다. 신규 PK는 빈 숫자를 임의 사용하지 말고 `CSV_RULES.md`가 지정하는 ID 권위에서 예약 상태를 확인한다. 기존 `.meta`와 GUID를 재생성하지 않는다.

## 3. 조사로 확인된 현재 상태

### 3.1 반입 완료 아트

컬러 파일은 `Assets/Textures/Customer/Dystopia/`, 노멀맵은 그 아래 `NormalMaps/`에 있다. Inspector 전용 파일은 roster에서 제외한다.

```text
컬러:   (Male|Female)(Normal|Hasty|PriceSensitive|Wealthy|Poor|Child|Elder)_NN.png
노멀맵: (동일한 컬러 basename)_Normal.png
```

현재 실수량은 컬러 60, 대응 노멀맵 60이다.

| 성별별 클래스 | 수량 | 게임 성향 | 연령 |
|---|---:|---|---|
| Normal | 12 | `Normal` | `Adult` |
| Hasty | 3 | `Hasty` | `Adult` |
| PriceSensitive | 3 | `PriceSensitive` | `Adult` |
| Wealthy | 3 | `Wealthy` | `Adult` |
| Poor | 3 | `Poor` | `Adult` |
| Child | 3 | `Normal` | `Child` |
| Elder | 3 | `Normal` | `Elderly` |

전체는 Adult 48, Child 6, Elderly 6이고, `Normal` 성향 안에서는 성별마다 Adult:Child:Elderly가 12:3:3이다. Child/Elder를 새 성향으로 만들지 않는다.

### 3.2 현재 데이터와 표시 경로

```text
CustomerAppearanceData.csv
  → CustomerAppearanceDataTable
  → CustomerCatalog.ValidateAndCommit
  → CustomerCompositionSelector
  → CustomerComposition / CustomerVisit.AppearanceIdx
  → GameUIController.loadDisplaySpritesAsync
  → ResourceData.csv의 address
  → ResourceManager.LoadAssetAsync
  → CustomerWorldQueueView의 방문별 SpriteRenderer
```

현재 외형 CSV는 `idx,nameidx,image_resource_idx,gender,age`이고 45행이다. Selector는 외형을 `Gender + Age`로만 찾으며 연령은 3종 균등 추첨이다. `GameUIController`는 외형 컬러 Sprite만 preload하고 `CustomerWorldQueueView`는 `WorldSprite.mat` 하나를 공유한다. 손님별 노멀맵 소비 계약은 없다.

새 60종을 기존 45행에 append하면 105종이 모두 후보가 되므로 금지한다. 이번 목표는 완성된 60종을 실제 roster로 쓰는 것이므로 외형 CSV의 기존 45행은 새 60행으로 교체한다. 기존 원본 이미지나 `.meta`는 별도 삭제 승인이 없는 한 삭제하지 않되 새 외형 CSV 후보에서는 제외한다.

## 4. 확정 데이터 계약

### 4.1 `CustomerAppearanceData.csv` schema

헤더를 다음 순서로 확장한다.

```csv
idx,nameidx,image_resource_idx,normal_resource_idx,gender,age,disposition_type
```

- `idx`: `CustomerAppearance` PK.
- `nameidx`: `TextData.idx` FK. 유효한 승인 PK를 사용한다.
- `image_resource_idx`: 컬러 Sprite의 필수 `ResourceData.idx` FK.
- `normal_resource_idx`: 대응 노멀 Texture의 필수 `ResourceData.idx` FK. 이번 60행에서는 nullable/0을 허용하지 않는다.
- `gender`: `CustomerAttributes.Male` 또는 `Female` 단일 값.
- `age`: `Adult`, `Child`, `Elderly` 단일 값.
- `disposition_type`: 기존 `CustomerDispositionType`의 `Normal`, `Hasty`, `PriceSensitive`, `Wealthy`, `Poor` 중 하나.

`CustomerAppearanceData`에 `[Name("normal_resource_idx")] uint NormalResourceIdx`와 `[Name("disposition_type")] CustomerDispositionType DispositionType`을 추가한다. 별도 문자열 enum이나 새 성향 enum을 만들지 않는다.

`ValidateClassification()`은 성별·연령의 단일 허용값, `CustomerProfileValidation.ValidateType` 통과, `Normal`만 Adult/Child/Elderly 허용, 나머지 네 성향은 Adult만 허용함을 검사한다. Child/Elderly가 비-Normal이면 즉시 데이터 오류다.

`CustomerAppearanceDataTable.LoadData()`는 두 Resource FK 모두 Resource 종류 대역이고 0/천 단위 예약값이 아님을 검사한다. 새 header 누락을 이전 값으로 묵인하지 않는다.

### 4.2 60행 작성 규칙

파일명으로 다음 값을 기계적으로 매핑하고 최종 CSV diff를 사람이 대조한다.

- `Male*` → Male, `Female*` → Female.
- `Child` → `Normal + Child`.
- `Elder` → `Normal + Elderly`.
- 나머지 클래스명 → 같은 이름의 성향 + Adult.
- 한 행의 컬러 basename과 노멀 basename은 동일하며 노멀만 `_Normal` suffix를 가진다.

PK, Resource PK, Text PK의 실제 숫자는 권위 문서와 최신 branch를 확인해 배정한다. 구현자가 임의 숫자를 이 문서에서 추론하지 않는다. 최종 결과는 정확히 60행이며 성별마다 30행, `Normal Adult 12`, `Normal Child 3`, `Normal Elderly 3`, 나머지 네 성향 Adult 각 3이어야 한다.

### 4.3 ResourceData와 Addressables

`ResourceData`의 `idx,path` schema는 바꾸지 않는다. 컬러와 노멀 각각 별도 행을 추가하고 `path`를 실제 Addressables address와 일치시킨다. 기본 address는 확장자를 제외한 고유 파일명이다.

```text
image_resource_idx  → ResourceData.idx → 컬러 address  → Sprite asset
normal_resource_idx → ResourceData.idx → normal address → Texture2D asset
```

기존 Addressables `Datas`/리소스 group과 label 관행을 먼저 확인하고 같은 구조를 사용한다. 별도 group을 임의 생성하지 않는다. 120개 asset의 `.meta`/GUID 1:1, 중복 address, 누락 entry를 검사한다.

컬러 import 목표는 Sprite Single, PPU 100, Bilinear, Mipmap Off, Clamp, NPOT None, Uncompressed, sRGB On, Alpha Is Transparency On, Max Size 2048이다. 노멀맵은 기존 고객 노멀 `.meta`와 현재 `PixelStageLighting` shader 계약을 우선한다. 이 shader가 linear RGB를 `rgb * 2 - 1`로 직접 읽으므로 packed-normal import와 혼용하지 말고 linear/uncompressed texture 계약을 확인한다. 불일치하면 60개에 같은 import 기준을 적용하되 GUID는 보존한다.

## 5. Selector와 catalog 변경

### 5.1 선택 순서와 연령 비율

`CustomerCompositionSelector`의 실제 구성 선택 순서를 다음처럼 고정한다.

1. 현재 명성/날짜 규칙으로 `CustomerDispositionData`를 골라 `DispositionType`을 확정한다.
2. 첫 성별은 무작위, 이후 성별은 기존 규칙대로 교대한다.
3. 확정 성향에 허용되는 연령을 파일 개수 가중치로 고른다.
4. `Gender + Age + DispositionType`이 모두 일치하는 외형 후보 중 균등 선택한다.
5. 모든 선택이 성공한 뒤에만 `previousGender`를 commit한다.

연령의 기존 1/3 균등 추첨을 제거한다.

- `Normal`: Adult:Child:Elderly = 12:3:3, 즉 2/3:1/6:1/6.
- `Hasty`, `PriceSensitive`, `Wealthy`, `Poor`: 파일이 모두 Adult이므로 Adult 100%.

숫자 상수를 중복 선언하기보다 확정된 성별·성향 후보를 연령별로 그룹화하고 각 그룹의 실제 행 개수를 가중치로 사용한다. 연령 선택 뒤 그 그룹의 행을 균등 선택하면 각 호환 외형은 같은 확률을 갖는다.

`SelectAttributes()` 단독 API는 호출자/테스트를 확인한다. 성향 입력 없이 새 정확한 규칙을 만들 수 없으므로 실제 구성 선택 경로가 새 helper를 사용하게 하고, 단독 API가 필요하면 승인 없이 제거하지 말고 호환 동작을 명시한다.

### 5.2 catalog 완전성

`CustomerCatalog.ValidateAndCommit()`은 기존 `Gender + Age` 존재 검사 대신 다음을 검사한다.

- Male/Female 각각 `Normal × Adult/Child/Elderly`.
- Male/Female 각각 `Hasty/PriceSensitive/Wealthy/Poor × Adult`.
- 비-Normal × Child/Elderly 금지.
- 각 행의 컬러/노멀 Resource FK 존재.

runtime catalog는 필수 조합 존재와 잘못된 조합 금지를 담당한다. 정확히 60개라는 현재 roster 수량은 실제 CSV 통합 테스트에서 검증하여 향후 catalog 확장을 불필요하게 막지 않는다.

### 5.3 어린이 표시 크기 보정

이 항목은 외형 선택 확률과 별개인 필수 표시 기능이다. Astra Dystopia 브랜치의 `Assets/DystopiaPrototype/Scripts/DystopiaScreen.cs`와 `doc/CUSTOMER_ARTWORK_GUIDE.md`를 참고하되, 그 브랜치의 `childPortraitRise=0.18`은 550×550 Canvas Image와 별도 `idleOrigins[2]`를 쓰는 좌상단 기준 배치용 값이다. 현재 게임은 Sprite 하단을 계산대 가림선에 직접 맞추므로 그 상승값을 그대로 이식하면 안 된다.

```csharp
childPortraitScale = 0.6f; // 0.4의 1.5배, 허용 범위 0.15~1
childPortraitRise = 0.001090909f; // 크기 증가 전 하단 좌표 보존값
childCounterSinkPixels = 8f; // 모든 Child를 계산대 뒤로 내리는 공통값

childDisplayHeight = adultDisplayHeight * childPortraitScale;
childVerticalOffset = adultDisplayHeight * childPortraitRise - childCounterSinkPixels;
```

핵심 계약은 다음과 같다.

- 어린이 판정은 파일명이나 `DispositionType`이 아니라 `CustomerVisit.Attributes`의 `CustomerAttributes.Child` flag로 한다.
- Adult와 Elderly에는 배율과 높이 보정을 적용하지 않는다. Elderly는 `Normal` 성향이지만 어린이가 아니다.
- `childRise`는 크기 증가로 `3×displayHeight/550` 가림 여유가 늘어난 만큼만 상쇄한다. 축소 후 높이가 아니라 **성인 기준 표시 높이**에 비율을 곱하며 `0.6`을 다시 곱하지 않는다.
- Sprite 자체, import PPU, `RectTransform.sizeDelta` 원본, CSV에 개별 scale 컬럼을 추가하여 해결하지 않는다.
- 모든 남녀 Child 6종에 같은 값을 사용한다. 이미지별 보정표는 만들지 않는다.
- Astra의 `WaitingLeft/WaitingRear에서 Child 숨김`, 테이블 아래 240px pop-up, 상자 숨김은 기존 프로토타입 화면 전용 동작이다. 이번 누락 수정은 크기와 기준점 보정만 이식하며 현재 게임의 큐 존재·이동·거래 규칙은 바꾸지 않는다.

두 현재 표시 경로가 서로 다른 좌표계를 쓰므로 계산 계약은 공유하되 적용 지점은 각각 둔다. `childPortraitScale`과 `childPortraitRise`의 기본값·허용 범위·Child 판정·계산식을 중복 구현하지 않도록, 새 작은 순수 계산형(예: `CustomerPortraitLayout`)을 `Assets/Scripts/Scene/`에 둔다. 이 계산형은 `CustomerAttributes`와 성인 기준 높이를 받아 `displayScale`, `displayHeight`, `risePixels`를 반환하고 Unity object나 runtime 상태를 소유하지 않는다. 각 view의 Inspector 필드는 Astra처럼 조정 가능하게 유지하되 같은 기본값을 사용하며, 값 검증과 계산은 공용 helper를 호출한다.

#### 공유 MainScene: `CustomerWorldQueueView`

현재 실제 월드 경로의 성인 기준은 `heightPixels=430`이다. 크기 `0.6`을 적용하면 어린이 표시 높이는 `258`이다. 하단 보존 상승량은 약 `0.46909`지만 공통 sink 8을 뺀 최종 수직 오프셋은 `-7.53091`이다.

```text
Adult/Elderly: displayHeight=430, rise=0
Child:         displayHeight=430×0.6=258, offset=430×0.001090909-8≈-7.53091
```

현재 아이가 계산대에서 뜨는 원인은 피벗 자체의 누락이 아니라 **이미 하단 정렬된 root에 77.4를 추가한 것**이다. `CustomerWorldQueueView`의 계산대 상대 root Y는 다음과 같다.

```text
rootBottom = -bottomCoverPixels - 3×displayHeight/550 + rise
기존값     = -12 - 3×172/550 + 77.4 = +64.462
수정값     = -12 - 3×172/550 + 0    = -12.938
```

아이 PNG 6종의 alpha>8 하단 투명 여백을 실제 파일에서 측정하면 축소 표시 높이 172 기준 `4.53~11.38`이다.

| 파일 | 원본 하단 투명 여백 | 높이 172에서의 여백 |
|---|---:|---:|
| FemaleChild_01 | 65/1000 | 11.18 |
| FemaleChild_02 | 30/1000 | 5.16 |
| FemaleChild_03 | 40/1000 | 6.88 |
| MaleChild_01 | 68/1254 | 9.33 |
| MaleChild_02 | 33/1254 | 4.53 |
| MaleChild_03 | 83/1254 | 11.38 |

따라서 기존 `rise=77.4`에서는 실제 alpha 하단이 계산대 가림선보다 `68.99~75.84` 기준 픽셀 위에 놓인다. 현재 CanvasScaler는 reference `1280×720`, 출력 목표는 `1600×900`으로 동일 종횡비이므로 화면 배율은 `1.25`다. 실제 화면에서는 약 `86.24~94.80px` 떠 보인다.

`rise=0`으로 바꾸면 alpha 하단은 가림선보다 `1.56~8.41` 기준 픽셀 아래에 놓인다. 1600×900 실제 화면에서는 약 `1.95~10.51px`가 계산대 뒤로 들어간다. idle breath 최대 상승 약 `0.94` 기준 픽셀까지 포함해도 가장 여백이 큰 원화의 하단은 가림선 아래에 남는다. 손/소매 끝이 계산대에 살짝 가려지고 공중에 뜨지 않는 현재 아트에 적합한 값이다.

크기 증가 전 root 하단은 `-12 - 3×172/550 = -12.93818`이다. 크기만 0.6으로 바꾸면 `-12 - 3×258/550 = -13.40727`이 되어 0.46909 내려간다. `risePixels=430×0.001090909=0.46909`를 더하면 최종 하단은 다시 `-12.93818`로 일치한다. 1280×720 기준 높이 증가분 86은 1600×900에서 `86×1.25=107.5px`이며 모두 위쪽으로 추가된다.

이번 보정은 **수치 변경만** 한다. `CustomerPortraitLayout` 계산식, root/body 피벗, `bottomCoverPixels=12`, `heightPixels=430`, 슬롯과 CanvasScaler는 변경하지 않는다. 월드 경로는 `bounds.min.y`를 상쇄해 Sprite 하단을 root에 맞추고 Canvas 경로는 `RectTransform.pivot=(0.5,0)`을 사용하므로 중앙 확대가 아니라 하단 고정 확대가 유지된다.

### 5.4 아이 아트별 몸통 부유 보정

아이 6종을 scale 0.6으로 배치한 상태에서 alpha>8 전체 하단과 손 영향을 줄이기 위한 중앙 몸통 영역(가로 32~68%) 하단을 측정했다. `FemaleChild_01`, `MaleChild_01`, `MaleChild_03`은 몸통 하단이 계산대 가림선보다 각각 약 `3.83`, `4.34`, `4.55` 기준 픽셀 위에 있었다. idle breath 최대 상승 약 `1.41`까지 포함하면 최악 약 `5.96`이다.

공통 sink는 여유를 포함해 `8` 기준 픽셀로 확정한다. 1280×720 reference에서 8이고, 1600×900에서는 Canvas/world 투영 배율 1.25에 따라 `10` 화면 픽셀이다. 개별 이미지별 offset, PNG 재가공, AppearanceIdx 분기는 만들지 않는다. 공용 `CustomerPortraitLayout.DefaultChildCounterSinkPixels`에서 Child에만 적용하여 월드와 Canvas 표시가 동일하게 내려간다.

구현 시 방문별 `Visual`에 생성 시 확정한 layout 결과를 보관한다. 매 프레임 CSV나 catalog를 다시 조회하지 않는다.

- `getVisual(visit, ...)`에서 `visit.Attributes`로 layout을 계산하고 `Visual.DisplayHeight`, `Visual.RisePixels`에 저장한다.
- body scale은 현재 `heightPixels / sprite.bounds.size.y` 대신 `DisplayHeight / sprite.bounds.size.y`를 사용한다.
- 현재 성인 발 기준선 계산은 보존한다. `RisePixels` 적용 코드도 유지하되 기본값이 0이므로 현재 Child에는 추가 오프셋이 없다.
- walk/breath의 Y 진폭과 body 하단 정렬은 `DisplayHeight`를 사용하여 축소된 어린이가 성인 진폭으로 흔들리지 않게 한다.
- 대사는 `DisplayHeight + 4` 기준으로 어린이 머리 위에 배치한다. 성인 높이 `heightPixels`를 쓰면 말풍선이 과도하게 위에 남는다.
- 거래 반응 아이콘의 Y 기준도 `DisplayHeight`를 사용하되 아이콘 자체 크기는 기존 64px 계약을 유지한다.
- normal map의 MaterialPropertyBlock, tint, alpha, sorting, 퇴장 수명은 그대로 유지한다.
- 새 visual을 만든 같은 프레임에 body scale/position을 즉시 초기화한다. 첫 렌더 프레임에 성인 크기로 보였다가 줄어드는 상태를 허용하지 않는다. Astra `Refresh()`의 즉시 보정과 같은 목적이다.

#### 보조 Canvas 경로: `CustomerQueueView`

이 경로가 Prefab에서 활성화될 수 있으므로 같은 계약을 적용하여 두 화면의 Child 크기가 달라지지 않게 한다.

- `getVisual()`에서 Child layout을 저장하고 최초 `rect.sizeDelta`부터 성인 counter 높이의 `0.6`으로 만든다.
- `retarget()`이 받는 target의 성인 기준 크기를 먼저 계산한 뒤 Child이면 Y와 aspect 유지 X에 `0.6`을 적용한다.
- `coveredBottom()` 구조는 유지하고 `adultTargetHeight × 0.001090909`로 높이 증가 전 하단 좌표만 보존한다.
- 이동 중에는 시작 크기/상승량에서 목표 크기/상승량으로 함께 보간하여 순간 점프를 막는다.
- breath와 speech 위치는 현재 보간된 어린이 표시 높이를 사용한다.
- 첫 생성 프레임부터 어린이 크기와 상승량을 적용한다.

현재 공유 MainScene에서 `CustomerWorldQueueView`만 사용된다고 확인되더라도 `CustomerQueueView`를 방치하지 않는다. 호출/Prefab 조사를 통해 완전히 미사용임을 입증하지 못하면 두 경로를 함께 수정하고 같은 순수 계산 테스트를 공유한다.

#### Prefab authoring

- `CustomerWorld.prefab`의 `CustomerWorldQueueView`에 `childPortraitScale=0.6`, `childPortraitRise=0.001090909`를 직렬화한다.
- `CustomerQueueView`가 연결된 Prefab이 있으면 같은 값을 직렬화한다.
- 전체 `heightPixels`, CanvasScaler, 슬롯 Transform/RectTransform을 어린이 때문에 변경하지 않는다.
- 필드에는 Astra와 같은 의미의 Range를 둔다: scale `0.15~1`, rise `0~0.6`.
- Unity Editor의 serialized field 표시와 Prefab override/missing reference만 확인한다. Game View를 실행해 맞추지 않는다.

## 6. 노멀맵 runtime 구조

### 6.1 로드와 조회

`GameUIController`의 화면 수명 preload를 재사용한다.

- `appearanceSprites: Dictionary<uint, Sprite>`는 유지한다.
- 외형 PK 기준 `appearanceNormalTextures: Dictionary<uint, Texture2D>`를 추가한다.
- `loadDisplaySpritesAsync()`에서 `NormalResourceIdx`를 `ResourceDataTable`로 해석하고 `ResourceManager.LoadAssetAsync<Texture2D>`로 로드한다.
- 동일 Resource FK는 타입별 캐시로 재사용한다.
- FK/address/로드 결과가 없으면 flat normal이나 placeholder로 조용히 대체하지 않고 초기화 오류를 낸다.
- `GetCustomerAppearanceNormalTexture(uint appearanceIdx)`를 추가하고 초기화 전/잘못된 PK면 컬러 조회처럼 예외를 낸다.
- handle 소유권과 해제는 현재 `ResourceManager` 계약을 유지한다. view가 Addressables를 직접 load/release하지 않는다.

Canvas `Image` 소비자는 컬러 Sprite만 사용해도 된다. 노멀맵의 필수 소비 대상은 공유 MainScene의 `CustomerWorldQueueView`다.

### 6.2 renderer와 shader 연결

현재 `WorldSprite.mat`은 일반 Sprite shader이므로 `_NormalMap`만 넣어서는 효과가 없다. 기존 `Assets/Shaders/Dystopia/PixelStageLighting.shader`에는 `_NormalMap` per-renderer 입력과 손님 표면 조명이 있으므로 새 shader를 복제하기 전에 재사용한다.

- 손님 body 전용 material을 만들거나 기존 stage-lighting material의 손님 설정을 안전하게 재사용해 `CustomerWorldQueueView`에 연결한다.
- 공용 material의 `_NormalMap`을 방문마다 직접 변경하지 않는다. 각 body `SpriteRenderer`에 `MaterialPropertyBlock`으로 `_NormalMap`을 지정한다.
- 같은 block에 shader가 요구하는 손님 표면값(현재 `_Surface`의 person 범위)과 필요한 조명값을 넣는다.
- 시간대 조명값의 권위는 `WorldSceneView`와 기존 월드 조명 갱신 경로다. 고객 view가 별도 시간 시스템이나 조명 상수를 복제하지 않는다. 기존 API로 전달할 수 없으면 최소 읽기 전용 적용 API를 `WorldSceneView`에 추가한다.
- `getVisual()`에서 컬러 Sprite와 같은 `AppearanceIdx`의 노멀 Texture를 조회해 body 생성 직후 block에 설정한다.
- trade reaction renderer에는 손님 노멀 material/property를 적용하지 않는다. 현재 `spriteMaterial`이 두 용도를 겸하면 body/reaction material 필드를 분리한다.
- 방문 이동, tint, alpha, sorting, breath/stride, 퇴장 로직은 변경하지 않는다.

shader/material/prefab 변경 후 `CustomerWorld.prefab`의 직렬화 참조, missing material, shader property 이름, MaterialPropertyBlock 동작을 Editor에서 확인한다. 노멀 방향이 Sprite UV와 맞지 않으면 shader/텍스처 계약 한 곳에서 고치고 개별 파일 예외를 만들지 않는다.

## 7. 예상 수정 범위

- `Assets/Datas/Customer/CustomerAppearanceData.csv`
- `Assets/Datas/ResourceData.csv`
- `Assets/Scripts/Customer/Data/CustomerAppearanceData.cs`
- `Assets/Scripts/Customer/Data/CustomerAppearanceDataTable.cs`
- `Assets/Scripts/Customer/Data/CustomerCatalog.cs`
- `Assets/Scripts/Customer/CustomerCompositionSelector.cs`
- `Assets/Scripts/Scene/GameUIController.cs`
- `Assets/Scripts/Scene/CustomerWorldQueueView.cs`
- `Assets/Scripts/Scene/CustomerQueueView.cs`
- 어린이 표시 계산을 공유하는 `Assets/Scripts/Scene/CustomerPortraitLayout.cs`와 `.meta`
- 필요 시 `Assets/Scripts/Scene/WorldSceneView.cs`
- 필요할 때만 손님 body 전용 shader/material과 각 `.meta`
- `Assets/Prefabs/World/CustomerWorld.prefab`
- `Assets/AddressableAssetsData/`의 실제 entry/group 설정
- 관련 `Assets/Tests/EditMode/Customer*.cs`

이미 반입된 PNG의 위치를 다시 정하지 말고 현재 폴더를 사용한다. 원본 이동·이름 변경·삭제는 범위가 아니다. `DystopiaSession`, `DystopiaCustomer`, 성별 Sprite 배열, 파일명 직접 로더 같은 별도 시스템을 추가하지 않는다.

## 8. 구현 순서

1. branch/dirty 상태와 기준 Unity Console 오류를 기록한다.
2. 60 컬러 ↔ 60 노멀을 basename으로 1:1 대조하고 누락·중복·Inspector 혼입을 차단한다.
3. ID 권위에서 CustomerAppearance/Resource/Text PK를 확인·예약한다.
4. 컬러와 노멀 import 설정 및 `.meta`/GUID를 검사한다.
5. `ResourceData.csv`와 Addressables에 컬러/노멀 entry를 등록하고 address 중복을 검사한다.
6. DTO/table/catalog에 `normal_resource_idx`, `disposition_type`과 검증을 추가한다.
7. 외형 CSV를 새 60행 roster로 교체하고 분포·FK를 검사한다.
8. Selector를 성향 선확정, 성별 교대, 후보 수 기반 연령 가중치, 3축 외형 필터 순서로 수정한다.
9. 최초 Astra 이식값 `0.4` scale을 1.5배인 `0.6`으로 바꾸고 rise를 하단 보존값 `0.001090909`로 설정한다. 공용 layout 계산과 하단 피벗을 그대로 사용하여 아래쪽 변은 유지하고 위쪽으로만 커지게 한다.
10. `GameUIController`에 노멀 Texture preload/cache/조회 경로를 추가한다.
11. 손님 body 전용 material/shader property block을 연결하고 reaction material을 분리한다.
12. Prefab에 어린이 표시 기본값을 직렬화하고 Unity Editor에서 저장한 뒤 reimport/compile 종료를 기다린다.
13. 관련 EditMode 테스트와 정적 Addressables/직렬화 검증을 실행한다.
14. `git diff --check`, 변경 allowlist, CSV PK/FK, address, GUID를 최종 대조한다.

## 9. 테스트와 검증

### 9.1 필수 EditMode 테스트

- `CustomerCsvTests`: 새 header, 성향 enum, normal FK, 잘못된 성향/연령, 누락 FK, 실제 CSV 60행 분포.
- `CustomerContractTests`: DTO/loader/catalog 공개 전 검증 및 실패 시 이전 공개 데이터 보존.
- `CustomerCompositionSelectorTests`: 3축 일치, 비-Normal Adult 강제, 실패 시 성별 상태 미변경.
- `CustomerCompositionSelectorIntegrationTests`: 모든 필수 조합과 실제 CSV의 성향 mismatch 0건.
- 신규 `CustomerPortraitLayoutTests` 또는 동등한 EditMode 테스트:
  - 기준 높이 430에서 Child는 `displayScale=0.6`, `displayHeight=258`, 최종 수직 offset `≈-7.53091`.
  - scale 0.4/rise 0 시점의 root 하단보다 최종 Child root 하단이 정확히 8 기준 픽셀 아래임.
  - Adult/Elderly는 `displayHeight=430`, `rise=0`.
  - Male/Female flag는 결과에 영향을 주지 않음.
  - `Normal` 성향만으로 Child 판정하지 않음.
  - scale/rise가 NaN, Infinity 또는 Range 밖이면 view 초기화 검증에서 거부됨.
- `CustomerWorldQueueView`의 계산 가능한 부분을 분리해 body scale, speech/reaction 높이, 최초 프레임 초기값이 모두 같은 layout 결과를 사용하는지 검사한다.
- `CustomerQueueView`는 target 크기와 rise를 함께 보간하며 rise가 축소 높이가 아닌 성인 기준 높이에서 계산되는지 검사한다.
- 필요 시 좁은 신규 테스트: 컬러/노멀 FK별 cache 및 조회 실패 계약, AppearanceIdx→normal/property 이름.

확률 테스트는 작은 표본의 퍼센트 허용 오차에만 의존하지 않는다. 고정 난수/통제 후보로 가중 경계를 직접 검사하거나 충분한 표본과 명시적 허용 범위를 쓴다. 핵심은 `Normal` 후보의 12:3:3, 비-Normal의 Adult 100%다.

### 9.2 Editor 및 컴파일 검증만 수행

이 작업에서는 플레이 테스트와 PlayMode 테스트를 피한다. 게임을 재생하지 말고 아래만 수행한다.

- Unity Editor reimport 완료 및 compile error 0.
- 신규 compile warning과 serialization error 확인.
- CSV header/파싱/PK/FK와 실제 60행 분포 검사.
- 120개 이미지 asset과 `.meta` 짝, GUID 중복, import 설정 검사.
- Addressables entry/address/label/group 중복·누락 및 Analyze 또는 동등한 Editor 검사.
- `CustomerWorld.prefab`의 missing script/reference/material 0, body/reaction material 직렬화 확인.
- Child 기본값 `scale=0.6`, `rise=0.001090909`가 관련 Prefab에 저장되었고 Adult/Elderly 경로의 기존 높이값이 변경되지 않았는지 확인.
- 관련 NUnit EditMode 테스트 실행. total=0, skip, 중단은 PASS가 아니다.
- `git diff --check`와 실제 변경 파일 allowlist 확인.

플레이하지 않았으므로 최종 상태는 최대 `STATIC PASS`다. 실제 화면의 노멀 방향, 조명 강도, 실루엣 크기, 알파 가장자리, Child 체감 비율, 이동 중 표시는 `PENDING`으로 명시한다. 이를 확인하려고 PlayMode, 수동 플레이, `InitScene → HubScene → MainScene` 실행을 하지 않는다.

## 10. 완료 보고 형식

- 수정/생성 파일 목록.
- 60행 roster 및 성별·성향·연령 집계.
- 컬러/노멀 Resource PK와 address 검사 결과.
- Addressables 변경과 보호 변경 승인·리뷰 상태.
- Unity reimport, compile error/warning, serialization/missing reference 결과.
- 실행한 EditMode 테스트 이름, total/pass/fail/skip 및 로그 위치.
- 플레이 테스트와 PlayMode 테스트를 의도적으로 하지 않았다는 사실.
- 결과가 최대 `STATIC PASS`이며 시각 검증은 `PENDING`이라는 사실.
- commit/push 여부와 남은 리뷰 또는 승인.

## 11. 금지 사항

- 플레이 테스트 또는 PlayMode 테스트를 수행하지 않는다.
- 새 60종을 기존 45행에 append해 105종 혼합 roster를 만들지 않는다.
- 성향과 외형을 독립 추첨하지 않는다.
- Adult/Child/Elderly를 1:1:1로 유지하지 않는다.
- Child/Elder를 `CustomerDispositionType`에 추가하지 않는다.
- Child 크기를 파일명, 성향 또는 `AppearanceIdx` 범위로 판정하지 않는다. 반드시 `CustomerVisit.Attributes`를 사용한다.
- `childRise`에 `childPortraitScale`을 다시 곱하지 않는다.
- Child를 작게 만들기 위해 공용 `heightPixels`, 슬롯, CanvasScaler 또는 PNG import 크기를 변경하지 않는다.
- 노멀맵을 파일명 검색으로 runtime에서 직접 읽지 않는다.
- view가 Addressables를 직접 load/release하지 않는다.
- 공용 material texture를 방문마다 바꿔 동시에 표시된 손님의 노멀이 덮어써지게 하지 않는다.
- 누락 Sprite/노멀을 placeholder로 숨기지 않는다.
- 무관한 CanvasScaler, 거래, 큐, 애니메이션, 시간 시스템을 리팩터링하지 않는다.
- 승인 없이 기존 이미지, `.meta`, Addressables entry를 삭제하거나 GUID를 재생성하지 않는다.
