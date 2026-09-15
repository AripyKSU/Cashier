# 작업 계획: Astra 청소기 아트·흡입·배출 연출을 제품 UI에 통합

## 1. 문서의 용도

이 문서는 새 구현 세션에 그대로 전달할 수 있는 실행 계획서다. 구현자는 별도의 대화 맥락 없이 이 문서와 저장소만으로 `vacuum` 브랜치의 임시 UI 청소기를 `origin/astra-prototype`의 최신 청소기 사양에 맞게 수정한다.

이 작업은 프로토타입 코드를 제품 코드에 그대로 복사하는 작업이 아니다. 프로토타입은 `SpriteRenderer`, 월드 좌표와 `Rigidbody2D`를 사용하지만 제품 분류 화면은 `Canvas`, `RectTransform`, `SaleSortingItemView.Position`과 자체 `Velocity`를 사용한다. 아트·타이밍·상태 전이·연출 의도는 원본을 따르고, 실제 구현은 제품 UI 구조에 맞게 작성한다.

이 문서는 작업 승인이나 보호 변경 승인을 대신하지 않는다. 기존 dirty 변경, 담당 범위와 Prefab·리소스 변경 권한은 작업 시작 시 다시 확인한다.

## 2. 작업 목표

현재 제품의 임시 청소기를 다음 사용자 사양으로 교체한다.

1. Astra 청소기 아트 리소스를 제품 `GameUI.prefab`에 연결한다.
2. 청소기를 잡아 작동시키면 부드럽게 180도 회전한다.
3. 회전 후 위쪽을 향하는 실제 입구를 기준으로 상품을 판정한다.
4. 작동 중 흡입 바람 연출을 표시한다.
5. 상품은 청소기에 붙어 보이지 않고, 입구로 이동하며 축소·회전한 뒤 보이지 않게 된다.
6. 삼킨 상품은 거래 수량에서 제거하지 않고 청소기 내부 상태로 보관한다.
7. 드래그 버튼을 놓으면 상품이 같은 입구에서 짧은 간격과 약간씩 다른 궤적으로 쏟아져 나온다.
8. 화면 종료·비활성화·시설 효과 해제 같은 강제 취소에서는 상품 유실 없이 원래 상태를 복원한다.
9. 기존 청소기 반복음, 구분봉, 직접 드래그, 자동 정렬과 입력 우선권을 보존한다.

의도한 정상 상태 전이는 다음과 같다.

```text
Idle
  → TurningAndDragging
  → Suction
  → Spitting
  → Returning
  → Idle
```

강제 취소는 어느 상태에서든 다음 경로를 사용한다.

```text
TurningAndDragging / Suction / Spitting
  → CancelAndRestoreAll
  → Idle
```

## 3. 기준 저장소와 원본

계획 작성 시 확인한 기준은 다음과 같다.

- 대상 checkout: `vacuum`
- 대상 commit: `ff05c1fc5d97e754e5f9df956b8e03b8fe2cf174`
- 로컬 `astra-prototype`: `87844af6a25a2a2c8f66affa31d1adff36de09f4`
- 최신 원본: `origin/astra-prototype`
- 최신 청소기 commit: `e1bd9edb4b9039a0544af81f20a47816e2fa900a`
- 최신 commit 제목: `feat: refine top-down vacuum interaction`
- 공통 merge-base: `ee8a4e1cff4a4c507b072582c8b2ad41c564e842`

구현 시 `origin/astra-prototype`가 더 진행되었더라도 자동으로 새 HEAD 전체를 기준으로 넓히지 않는다. 우선 `e1bd9ed`의 아래 여섯 파일을 원본 근거로 사용하고, 이후 변경이 이번 청소기 사양에 필요한지 별도로 판단한다.

| 역할 | 원본 경로 |
|---|---|
| 청소기 이미지 | `Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png` |
| 이미지 Import 설정 | `Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png.meta` |
| 흡입 바람 재질 | `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat` |
| 프로토타입 동작 참고 | `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaVacuumController.cs` |
| 프로토타입 연결·배치 참고 | `Assets/DystopiaPrototype/TopDownTest/Editor/DystopiaTopDownTestTools.cs` |
| 원본 기능 명세 | `doc/TOPDOWN_VACUUM.md` |

원본 commit 확인 예시:

```powershell
git show e1bd9ed:doc/TOPDOWN_VACUUM.md
git show e1bd9ed:Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaVacuumController.cs
git show --stat e1bd9ed
```

브랜치 전체 merge, cherry-pick 또는 폴더 전체 복사는 금지한다. `astra-prototype`에는 현재 제품 브랜치에 없는 파일과 오래된 시스템이 다수 있으므로 청소기 범위만 의미 병합한다.

## 4. 작업 시작 전 필수 절차

1. 저장소 루트 `AGENTS.md`를 읽는다.
2. 다음 문서를 전부 읽고 적용한다.
   - `doc/WORK_RULES.md`
   - `doc/INDEX.md`
   - `doc/CODING_RULES.md`
   - `doc/PREFAB_RESOURCE_RULES.md`
   - `doc/SALE_ITEM_LAYOUT_RULES.md`
   - `doc/FACILITY_INTEGRATION.md`
   - `doc/DYSTOPIA_RESOURCE_INTEGRATION.md`
   - `doc/work/README.md`
3. 현재 branch, commit, dirty·untracked 파일과 stash를 확인한다.
4. 기존 변경은 사용자 작업으로 간주하고 보존한다. 승인 없이 stash, reset, checkout 복원, 광범위 포맷팅을 하지 않는다.
5. 같은 Unity Editor를 다른 작업자가 조작 중인지 확인한다. 동일 Prefab 편집과 Unity import·compile 담당자는 한 번에 한 명만 둔다.
6. 아래 기준 파일의 현재 내용을 다시 읽고 호출 관계를 확인한다.
   - `Assets/Scripts/UI/VacuumController.cs`
   - `Assets/Scripts/UI/SaleSortingPanel.cs`
   - `Assets/Scripts/UI/SaleSortingItemView.cs`
   - `Assets/Scripts/UI/DividerBarController.cs`
   - `Assets/Scripts/UI/SaleSortingHandCursor.cs`
   - `Assets/Prefabs/GameUI/GameUI.prefab`
7. 제품 코드가 `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaVacuumController.cs`를 참조하지 않는지 확인한다. 이번 작업에서도 참조를 새로 만들지 않는다.

## 5. 현재 제품 구현과 변경 이유

현재 `VacuumController`의 계약은 다음과 같다.

- `Vacuum`은 `RectTransform`과 `Image`로 구성된 Canvas UI다.
- `GameUI.prefab`의 `Vacuum`은 `48 × 480` 크기의 임시 막대이고 `Image.sprite`가 비어 있다.
- 자식 `SuctionArea`는 본체 아래 `y = -250`에 고정되어 있다.
- 본체 전체 사각형을 클릭하면 즉시 드래그를 시작한다.
- `SuctionArea` 안의 상품은 `attachedItems`에 들어간다.
- 등록된 상품은 `VacuumAttached` 상태로 청소기 아래에 줄지어 보인다.
- 버튼을 놓으면 `ReleaseAttachedItems()`가 모든 상품을 한 번에 반환한다.
- `SaleSortingPanel.releaseVacuumItems()`가 반환 상품을 즉시 분류한다.

이 구조는 다음 요구사항과 충돌한다.

- 180도 회전 후에는 입구의 실제 위치와 방향이 바뀌므로 고정된 아래쪽 판정이 틀린다.
- 상품이 붙어 보이기 때문에 내부로 빨려 들어간 느낌이 없다.
- 비활성 내부 보관 상태가 없어서 순차 배출할 수 없다.
- `IsHolding`이 false가 되는 프레임에 Panel이 즉시 전부 해제·분류하므로 배출 애니메이션이 들어갈 시간이 없다.
- 프로토타입의 `Rigidbody2D.AddForce`를 UI 상품에 직접 적용할 수 없다.

## 6. 확정 범위

### 6.1 포함

- 기존 `Vacuum.png`를 실제 제품 UI에 연결
- 최신 원본 사양에 맞는 Texture Import 설정 반영
- `VacuumWind.mat`과 `.meta` 이식
- 제품 UI 청소기의 180도 회전과 기준 위치 복귀
- 회전된 `Nozzle` 위치·방향 기반 흡입 판정
- UI 좌표계에서의 상품 흡입 이동
- 상품별 축소·회전·숨김 연출
- 복수 상품 내부 보관과 순차 배출
- 정상 배출과 강제 취소 복원의 분리
- 기존 청소기 loop SFX 시작·정지 보존
- Panel의 입력 중재와 자동 정렬 제외 상태 수정
- 필요한 EditMode 테스트 또는 Editor 검증 코드
- 구현 후 관련 영구 문서 또는 본 문서의 실제 결과 갱신

### 6.2 제외 및 금지

- `astra-prototype` 전체 merge 또는 cherry-pick
- 프로토타입 Scene, `DystopiaTopDownTest`, `DystopiaVacuumController`를 제품 UI에 연결
- `Rigidbody2D`를 제품 UI 상품에 새로 추가
- Addressables group·address·label 변경
- Package, ProjectSettings, 공용 Render 설정 변경
- 상품 분류 규칙, 거래 수량, 시설 가격·해금 조건 변경
- 무관한 UI·사운드·자동 정렬 리팩터링
- 원본 이미지 삭제·이동·GUID 재생성
- PlayMode 테스트 실행
- Unity Editor에서 Play 버튼을 누르는 실플레이 검증
- Player build 실행

## 7. 원본에서 유지할 사양과 UI 환산 규칙

### 7.1 그대로 유지할 시간·연출 사양

| 항목 | 기준값 |
|---|---:|
| 회전 목표 | 기준 회전 + 180도 |
| 회전 감쇠 시간 | 0.11초 |
| 흡입 시작 회전 진행도 | `flip > 0.85` |
| 삼키기 연출 시간 | 0.16초 |
| 배출 간격 | 0.07초 |
| 배출 방향 변화 | 최대 약 ±7도 |
| 배출 속도 변화 | 기준값의 90~110% |
| VFX 선/스트릭 수 | 12개 |
| 원본 동시 저장 개수 | 제한 없음 |

프레임 수가 아니라 `Time.unscaledDeltaTime` 누적으로 처리한다. 분류 UI의 기존 일시정지 계약을 유지한다.

### 7.2 그대로 복사하지 않을 월드 단위 값

프로토타입의 `1.9` 월드 흡입 거리, `0.36` 월드 캡처 반경, 가속도 `52`, 배출 속도 `4.8`은 Canvas pixel 좌표에 직접 넣지 않는다. 제품 `itemRoot` 로컬 좌표로 환산한 직렬화 필드를 사용한다.

초기 Editor 기본값 후보는 다음과 같다.

| UI 직렬화 필드 | 초기 후보 | 의미 |
|---|---:|---|
| `suctionRadiusPixels` | 150 | 노즐 중심에서 흡입을 시작하는 거리 |
| `captureRadiusPixels` | 28 | 삼키기 연출로 전환하는 거리 |
| `suctionAccelerationPixels` | 1800 | 초당 속도 변화량 |
| `spitSpeedPixels` | 420 | 배출 초기 속도 |
| `spitOffsetPixels` | 12 | 노즐과 배출 시작점의 최소 간격 |
| `returnSmoothSeconds` | 0.11 | 기준 위치·회전 복귀 감쇠 |

이 값은 구현자가 코드를 완성하기 위한 시작값이지 플레이 검증으로 확정된 밸런스가 아니다. 이번 작업에서는 PlayMode를 실행하지 않으므로 최종 보고에 `PENDING: 사용자 실플레이 튜닝 필요`라고 명시한다.

## 8. 리소스 구현 계약

### 8.1 `Vacuum.png`

현재 브랜치의 본 파일 blob과 최신 원본 blob은 계획 작성 시 동일했다.

```text
34a82c010574909c5c8d71cbaee6e7ef8a8fa798
```

따라서 PNG 본 파일을 덮어쓰거나 새로 생성하지 않는다. 기존 경로와 GUID를 유지한다.

```text
Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png
GUID: 8c5509423fa570745bc62cafae857f42
```

최신 원본 사양에 맞춰 `.meta`의 Import 설정만 Unity Editor에서 반영한다.

| 설정 | 값 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | 40 |
| Filter Mode | Point |
| Generate Mip Maps | Off |
| Compression | None |
| Alpha Is Transparency | On |

현재 `.meta`의 오래된 Multiple Sprite 정보는 제거되더라도 GUID는 바꾸지 않는다. Prefab에는 Single Sprite의 실제 local file ID를 Unity Editor를 통해 연결한다.

### 8.2 `VacuumWind.mat`

`e1bd9ed`에서 다음 파일과 `.meta`를 함께 가져온다.

- `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat`
- `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat.meta`

원본은 `Sprites/Default` 계열 재질이다. 제품 UI에서 `Image.material`로 사용할 때 Canvas 표시가 가능한지 Editor Inspector와 Prefab 참조로 확인한다. Shader 또는 material이 UI에서 부적절하다는 정적 문제가 확인되면 다음 우선순위를 따른다.

1. 기본 UI Graphic material로 동일한 색·알파 연출을 구현한다.
2. 그래도 별도 재질이 필요하면 기존 프로젝트 Shader/Material을 검색한다.
3. 신규 Shader는 이번 범위에서 만들지 않고 사유를 보고한다.

`VacuumWind.mat`을 실제로 사용하지 않게 결정했다면 미사용 파일을 관성적으로 추가하지 않는다. 추가한 뒤 사용하지 않게 된 파일을 삭제할 때는 정확한 대상과 `.meta` 짝을 확인하고 기존 파일이 아니었는지 검증한다.

### 8.3 `GameUI.prefab`

Prefab은 Unity Editor로 수정한다. 최소 권장 계층은 다음과 같다.

```text
Vacuum
├─ Visual
├─ GripArea
├─ Nozzle
└─ SuctionVfxRoot
```

기존 root `Vacuum`의 file ID와 `VacuumController` 참조는 가능한 한 보존한다. 기존 `SuctionArea`는 역할을 바꾸어 `Nozzle`로 rename하거나, rename의 직렬화 위험이 더 크면 이름을 유지하되 코드와 Inspector Tooltip에서 “실제 입구 Transform”임을 명시한다.

Prefab 연결 조건:

- `Visual.Image.sprite`에 실제 `Vacuum.png` Sprite 연결
- `Image.preserveAspect = true`
- 임시 단색 tint 제거 또는 `Color.white`
- 클릭 판정용 `GripArea`는 손잡이 영역에 배치
- `Nozzle`은 대기 자세에서 이미지의 아래쪽 입구에 배치
- `SuctionVfxRoot`는 Nozzle을 기준으로 배치하고 상품보다 앞에 보이되 다른 UI 패널을 침범하지 않음
- 장식용 Image는 불필요한 raycast를 받지 않도록 `raycastTarget = false`
- 입력 판정은 root 전체가 아니라 `GripArea`를 우선 사용
- 180도 회전 시 자식 `Nozzle`도 함께 회전해 실제 판정점이 위쪽으로 이동해야 함

원본 이미지의 유효 Sprite 영역은 약 `74 × 127px`이며 전체 PNG는 `102 × 128px`이다. UI 크기는 비율을 유지하고 기존 작업대에서 손잡이와 입구가 식별 가능한 크기로 설정한다. PlayMode 튜닝 없이 임의로 화면 전체를 가리는 크기로 키우지 않는다.

## 9. `VacuumController` 상세 구현 계약

수정 대상:

```text
Assets/Scripts/UI/VacuumController.cs
```

### 9.1 책임

`VacuumController`가 다음을 단일 소유한다.

- 청소기 입력 시작과 유지
- 기준 위치·회전 저장
- 180도 회전과 복귀
- 노즐 기반 흡입 후보 판정
- 상품별 흡입 애니메이션 상태
- 완전히 삼킨 상품의 내부 보관
- 순차 배출
- 정상 배출과 강제 취소 복원
- 흡입 VFX와 loop SFX 수명

상품의 최종 판매/제외 분류 규칙은 계속 `SaleSortingPanel`이 소유한다. `VacuumController`가 판매 수량을 직접 변경하거나 상품을 `Excluded`로 확정하지 않는다.

### 9.2 직렬화 참조와 설정

기존 필드는 호환 가능한 범위에서 유지하고 다음 역할을 명확히 한다.

```csharp
[SerializeField] private RectTransform vacuumRect;
[SerializeField] private Image vacuumImage;
[SerializeField] private RectTransform gripArea;
[SerializeField] private RectTransform nozzle;
[SerializeField] private RectTransform suctionVfxRoot;
```

필요한 조정값은 Inspector에서 확인 가능하게 직렬화한다.

```csharp
turnSmoothSeconds
returnSmoothSeconds
suctionRadiusPixels
captureRadiusPixels
suctionAccelerationPixels
swallowSeconds
spitSpeedPixels
spitIntervalSeconds
spitAngleDegrees
spitSpeedVariation
```

기존 `liftOffsetPixels`, `attachedSpacingPixels`, `positionFollowSpeed`는 새 동작에서 사용하지 않으면 직렬화 migration 영향을 확인한 뒤 제거한다. 사용하지 않는 필드를 warning 억제로 남기지 않는다.

### 9.3 공개 상태 계약

`IsHolding` 하나에 배출 상태를 섞지 않는다.

최소 공개 계약:

```csharp
public bool IsHolding { get; }
public bool IsSpitting { get; }
public bool IsBusy { get; }
public int StoredItemCount { get; }
```

- `IsHolding`: 실제 포인터 버튼을 누르고 청소기를 조작 중
- `IsSpitting`: 버튼은 놓였지만 내부 상품을 배출 중
- `IsBusy`: 회전·흡입·배출·복귀 중 다른 도구 입력을 막아야 하는 상태
- `StoredItemCount`: 흡입 애니메이션 중이거나 내부에 삼켜진 상품 수

기존 외부 사용처를 확인해 API 변경 범위를 최소화한다. `AttachedItemCount`가 테스트나 외부에서 필요하면 의미를 `StoredItemCount`로 migration하거나 호환 property를 두되, “붙어 보이는 상품”이라는 오래된 의미는 주석에 남기지 않는다.

### 9.4 초기화와 기준 상태

`Initialize(workArea, itemRoot)`에서 다음을 수행한다.

1. 모든 필수 참조를 해석한다.
2. Prefab에 저장된 대기 `anchoredPosition`, `localRotation`, `localScale`을 기준 상태로 캡처한다.
3. 진행 중 상태가 있다면 먼저 안전하게 복원한다.
4. VFX pool을 한 번만 준비한다.
5. 청소기를 기준 위치로 둔다.

현재 `ResetToStart()`처럼 `new Vector2(startOffsetX, 0)`로 매번 덮는 방식보다 Prefab 저작 위치를 기준값으로 사용하는 것을 우선한다. 기존 `startOffsetX`가 다른 코드나 Prefab migration에 필요하면 최초 배치 fallback으로만 사용한다.

중복 `Initialize`와 `ResetToStart` 호출은 멱등해야 한다. 내부 상품을 조용히 버리면 안 된다.

### 9.5 입력과 회전

기존 Input System/fallback 경계를 유지한다. 새 입력 framework를 추가하지 않는다.

잡기 시작 조건:

- 작업이 허용됨
- 새 상호작용이 허용됨
- 포인터가 이번 프레임 눌림
- `GripArea` 안에 포인터가 있음
- 청소기가 `Idle` 또는 잡을 수 있는 대기 상태

잡은 시점에:

- 기준 클릭 오프셋을 저장
- `PlayLoopSfx(SoundKeys.Vacuum)`을 한 번 호출
- 회전 목표를 180도로 설정

회전은 `Mathf.SmoothDamp` 또는 동등한 경과 시간 기반 감쇠를 사용한다. 목표 진행도는 0~1이고 실제 각도는 `180 * SmoothStep(0, 1, flip)`으로 계산할 수 있다. `flip > 0.85` 전에는 상품을 수집하지 않는다.

드래그 중 root가 클릭 지점을 따라가되, 회전하면서 손잡이가 포인터에서 크게 이탈하지 않도록 다음 중 현재 UI에 더 안정적인 방법을 택한다.

1. `GripArea`의 실제 월드 위치와 포인터 차이를 root 위치에 보정하거나
2. 기준 회전 전 drag offset을 계산하고 회전된 local grip offset을 반영한다.

root 중심만 포인터에 붙이는 구현은 손잡이를 잡는 연출을 깨므로 피한다.

### 9.6 회전된 노즐 판정

상품 판정은 `vacuumRect.anchoredPosition`의 위·아래에 상수를 더하지 않는다.

1. `nozzle.position`을 `itemRoot.InverseTransformPoint`로 변환해 노즐 로컬 좌표를 구한다.
2. 청소기의 회전된 입구 방향도 `itemRoot` 로컬 방향으로 변환한다.
3. 각 상품의 `Position`과 노즐 위치 사이 거리 및 내적을 계산한다.
4. `suctionRadiusPixels`와 부채꼴 조건을 모두 통과한 상품만 흡입한다.

대기 자세에서 Nozzle은 이미지 아래쪽에 있고, 180도 회전 후에는 화면상 위쪽에 있어야 한다. 흡입 방향은 회전된 Nozzle에서 작업대 상품 쪽으로 향해야 한다. 부호를 프로토타입의 `-transform.up`에서 기계적으로 복사하지 말고 UI Transform의 실제 방향으로 확인한다.

### 9.7 흡입 후보 제외 조건

다음 상품은 흡입하지 않는다.

- null 또는 비활성 상품
- `State == Excluded`
- 플레이어가 직접 드래그 중
- 구분봉 이동 중
- 자동 정렬 중
- 이미 청소기 내부 상태에 등록됨
- 현재 Panel 목록에 없는 상품

흡입 대상으로 등록하는 즉시 `Manipulation = VacuumAttached`로 바꿔 다른 시스템이 동시에 위치를 변경하지 못하게 한다. enum 이름은 직렬화 값과 외부 참조 때문에 이번 작업에서 rename하지 않는다. XML 주석만 “청소기 흡입/내부 보관이 위치와 표시를 소유하는 상태”로 수정한다.

### 9.8 상품별 저장 상태

현재 `List<SaleSortingItemView> attachedItems` 대신 상품별 상태를 보관한다. 중첩 private class 또는 struct를 사용할 수 있다.

최소 저장값:

```text
item
originalPosition
originalLocalRotation
originalLocalScale
originalVelocity
originalSortingState
originalManipulationState
elapsedSeconds
hasSwallowed
```

하나의 상품은 목록에 한 번만 들어간다. `Contains` 선형 검색이 반복 병목이 될 정도의 수량은 예상되지 않지만, 목록과 별도 HashSet을 함께 둘 경우 두 자료구조를 모든 성공·취소 경로에서 원자적으로 정리한다. 미래 최적화를 위한 불필요한 복잡성은 추가하지 않는다.

### 9.9 빨려 들어가는 연출

상품이 캡처 반경 안에 들어오면 다음을 수행한다.

1. 상품의 원래 상태를 저장한다.
2. `Velocity = Vector2.zero`로 중복 이동을 멈춘다.
3. `swallowSeconds`, 기본 0.16초 동안 진행도를 누적한다.
4. 위치를 시작 위치에서 현재 Nozzle 위치로 보간한다.
5. 크기를 원래 값에서 거의 0으로 줄인다.
6. 약 120~180도 범위에서 회전시킨다.
7. 완료 시 `gameObject.SetActive(false)`로 숨긴다.
8. `hasSwallowed = true`로 표시하되 `SaleSortingPanel.items`와 거래 수량에서는 제거하지 않는다.

노즐이 드래그로 움직이므로 종료점은 매 프레임 현재 `Nozzle` 위치를 사용한다. 시작점은 캡처 순간 값을 보존한다.

완전히 들어가기 전에 정상 버튼 해제가 발생한 상품은 배출하지 않고 흡입 직전 전체 상태로 복원한다. 이는 원본 `e1bd9ed`의 계약이다.

### 9.10 흡입 VFX

프로토타입 `LineRenderer`를 Screen Space Canvas에 그대로 생성하지 않는다. 제품에서는 `SuctionVfxRoot` 아래에 UI `Image` 스트릭 12개를 한 번 준비해 재사용한다.

각 스트릭은 다음 속성을 가진다.

- Nozzle 바깥에서 Nozzle 방향으로 반복 이동
- index별 위상 차이
- 측면 위치와 길이에 작은 차이
- 입구에 가까워질수록 알파 또는 크기 변화
- 실제 흡입 가능 상태(`IsHolding && flip > 0.85`)에서만 활성
- 배출, 복귀, 강제 취소, 비활성화에서 즉시 숨김
- `raycastTarget = false`

런타임에 매 프레임 GameObject를 생성·파괴하지 않는다. Prefab에 12개를 저작하거나 최초 초기화 때 한 번 생성한 뒤 재사용한다. 런타임 생성 시 `HideFlags.DontSave`를 제품 Prefab 객체에 무분별하게 사용하지 말고, 비활성화·파괴 수명을 명확히 한다.

### 9.11 버튼 해제와 순차 배출

포인터 버튼이 놓이면 즉시 `IsHolding = false`로 전환하고 loop SFX와 흡입 VFX를 정지한다. 이후:

1. `hasSwallowed == false`인 상품을 원래 상태로 복원하고 내부 목록에서 제거한다.
2. 완전히 삼킨 상품이 없으면 `Returning`으로 전환한다.
3. 완전히 삼킨 상품이 있으면 `Spitting`으로 전환한다.
4. 배출이 끝날 때까지 청소기는 180도 자세와 현재 위치를 유지한다.
5. 저장 순서대로 첫 상품을 즉시, 이후 상품을 `spitIntervalSeconds` 간격으로 배출한다.
6. 마지막 상품 배출 후 `Returning`으로 전환한다.

배출 시 상품 상태:

```text
position = Nozzle 위치 + 배출 방향 * spitOffsetPixels
localScale = 원래 크기
localRotation = 원래 회전 + 작은 편차
State = Working
Manipulation = Idle
Velocity = 배출 방향 * 변형된 spitSpeedPixels
gameObject.activeSelf = true
```

배출 방향은 같은 Nozzle을 기준으로 한다. 상품마다 결정적인 sequence 기반 변화를 사용해 테스트 재현성을 유지한다. `UnityEngine.Random` 전역 상태를 변경하지 않는다. 원본처럼 `Mathf.Sin(sequence * 상수)`와 `Mathf.Repeat` 조합을 사용할 수 있다.

배출 위치는 상품 중심이 `itemRoot` 경계를 심하게 벗어나지 않도록 최소 clamp한다. 단, 모든 상품을 정확히 같은 점으로 clamp해 겹치게 만들지 않는다.

### 9.12 정상 완료와 강제 취소 API 분리

기존 `ReleaseAttachedItems()` 하나로 정상 배출과 강제 정리를 처리하지 않는다.

권장 API 의미:

```csharp
public void BeginRelease();
public void CancelAndRestoreItems();
public IReadOnlyList<SaleSortingItemView> DrainSpatItems();
public void ResetToStart();
```

이름은 현재 스타일에 맞게 조정할 수 있으나 계약은 분리한다.

- `BeginRelease`: 정상 포인터 해제. 순차 배출 시작.
- `CancelAndRestoreItems`: 화면 종료·시설 비활성화·일시정지 등. 미배출 상품 전체 원상복구.
- `DrainSpatItems`: 이번 프레임 실제 배출된 상품을 Panel에 한 번만 전달. 내부 목록을 외부가 수정할 수 없게 함.
- `ResetToStart`: 먼저 안전한 취소 복원을 수행한 뒤 기준 Transform과 효과를 초기화.

이미 배출되어 Panel 소유로 돌아간 상품은 이후 취소에서 원래 흡입 위치로 되돌리지 않는다. 아직 내부에 남은 상품만 복원한다.

### 9.13 `OnDisable`과 수명 종료

`OnDisable`에서 반드시:

- loop SFX 정지
- VFX 숨김
- 아직 내부에 남은 모든 상품 복원
- 임시 상태와 배출 큐 정리
- 기준 위치·회전·스케일 복원

을 수행한다. `gameObject.SetActive(false)`된 상품이 남은 채 청소기만 비활성화되면 안 된다.

`OnDestroy`가 필요하다면 같은 정리 로직을 멱등하게 호출한다. 동일 상품을 두 번 배출하거나 두 번 복원하지 않아야 한다.

## 10. `SaleSortingPanel` 상세 구현 계약

수정 대상:

```text
Assets/Scripts/UI/SaleSortingPanel.cs
```

### 10.1 즉시 일괄 해제 제거

현재 다음 흐름을 제거한다.

```text
wasVacuumHolding && !isVacuumHolding
→ releaseVacuumItems()
→ ReleaseAttachedItems()
→ 모든 상품 즉시 classifyItem
```

버튼 해제는 `VacuumController`가 감지해 `Spitting`을 시작하거나, Panel이 한 번만 `BeginRelease()`를 호출하는 구조로 변경한다. 어느 쪽이든 두 클래스가 동시에 배출을 시작하지 않게 단일 소유자를 정한다.

### 10.2 입력 중재

`vacuum.IsBusy` 동안 다음을 보장한다.

- 새 직접 상품 드래그 시작 금지
- 구분봉 새 드래그 시작 금지
- 자동 정렬 정지
- 청소기 중복 잡기 금지

배출 완료 후 `IsBusy == false`가 되면 기존 입력을 다시 허용한다.

`IsHolding`과 `IsBusy`를 구분한다. 배출 중에는 손 버튼이 눌리지 않았으므로 손 커서를 계속 “잡은 손”으로 보여줄 필요가 없다. 기존 커서 정책에 맞춰 배출 시작 시 숨기는 것을 기본으로 한다.

### 10.3 배출 상품 인수

매 Update에서 `DrainSpatItems()` 또는 동등 API로 실제 배출된 상품만 받는다.

각 상품에 대해:

- Panel 목록 소속인지 확인
- `Manipulation == Idle`인지 확인 또는 설정
- 기존 상품 이동 시뮬레이션 대상에 복귀
- 적절한 1회성 배출/놓기 SFX가 필요하면 기존 `ItemPlace`를 한 번 재생

배출 즉시 `classifyItem`으로 최종 확정하지 않는 것을 기본으로 한다. 상품은 `State = Working`으로 나오고 기존 작업대 위치·속도 갱신과 분류 판정 흐름을 거친다. 현재 Panel 구조상 명시적 분류 호출이 반드시 필요하다면 상품이 배출되어 위치가 적용된 뒤 한 번만 호출하며, 숨겨진 내부 상태에서는 절대 호출하지 않는다.

여러 상품 배출이 끝난 뒤 자동 정렬 요청이 필요하면 마지막 배출 완료 시 한 번만 요청한다. 상품마다 `requestAutoSort()`를 호출해 중복 예약하지 않는다.

### 10.4 강제 취소 호출부

다음 기존 경로에서 정상 배출이 아니라 `CancelAndRestoreItems()`를 호출한다.

- `state != ViewState.Sorting`
- presentation pause 진입
- `SetVacuumAvailable(false)`
- Panel 초기화/리셋
- 화면 숨김 또는 종료
- `OnDestroy`

취소 복원 후 상태·통계·상품 수량이 변하지 않아야 한다. 취소 때문에 `ItemPlace` 또는 `ItemRemove` SFX를 재생하지 않는다.

### 10.5 기존 기능 보존

- 시설 효과가 활성화된 경우에만 청소기 표시·조작 가능
- 청소기 조작이 계산기 UI 입력을 뚫지 않음
- 청소기 상품은 `DividerBarController`와 자동 정렬에서 제외
- 직접 드래그·구분봉·청소기 중 한 주체만 입력 소유
- `VacuumLoop`는 시작/종료 쌍을 유지
- 상품의 `ForSale`/`Excluded` 최종 수량 계산은 Panel 권위 유지

## 11. `SaleSortingItemView` 조건부 변경

조건부 수정 대상:

```text
Assets/Scripts/UI/SaleSortingItemView.cs
```

가능하면 기존 public/internal API를 사용한다.

- `Position`
- `Velocity`
- `State`
- `Manipulation`
- `transform.localScale`
- `transform.localRotation`
- `gameObject.SetActive`

다음 경우에만 작은 API를 추가한다.

- 원래 회전·크기·표시 상태를 안전하게 캡처/복원할 수 없음
- 비활성화 후 필요한 UI reference가 소실됨
- Panel과 Controller가 같은 private 상태를 중복 변경해야 함

지속적인 각속도 시스템은 이번 작업에 추가하지 않는다. 배출 시 작은 초기 회전 편차만 적용하고 기존 UI 이동을 재사용한다.

`ManipulationState.VacuumAttached`의 enum 값과 이름은 보존하고 XML 주석을 새 의미에 맞게 갱신한다.

## 12. VFX 구현 선택 기준

구현자는 다음 순서로 가장 작은 해법을 선택한다.

1. Prefab에 12개 비활성 `Image`를 배치하고 Controller가 위치·회전·알파를 갱신
2. Prefab 비대화가 명확히 불리하면 Initialize에서 12개를 한 번 생성해 재사용
3. 기존 프로젝트에 검증된 UI line component가 이미 있으면 새 의존성 없이 재사용 가능 여부 검토

금지:

- 매 프레임 GameObject 생성/삭제
- 새 package 설치
- 제품 Canvas에 월드 `LineRenderer`를 검증 없이 혼합
- VFX가 raycast를 가로채는 설정
- VFX 타이밍에 상품 판정 권위를 묶는 구현

VFX가 꺼져도 gameplay 상태 전이는 정상 동작해야 한다.

## 13. 예상 변경 파일

### 13.1 필수 후보

- `Assets/Scripts/UI/VacuumController.cs`
- `Assets/Scripts/UI/SaleSortingPanel.cs`
- `Assets/Prefabs/GameUI/GameUI.prefab`
- `Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png.meta`
- `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat`
- `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat.meta`
- 관련 EditMode 테스트 파일 또는 Editor 검증 파일
- 구현 결과를 기록할 관련 문서

### 13.2 조건부 후보

- `Assets/Scripts/UI/SaleSortingItemView.cs`
- 신규 VFX용 Prefab 또는 Sprite가 실제로 필요한 경우의 기능별 자산과 `.meta`

### 13.3 수정 금지 기본값

- `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaVacuumController.cs`
- `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs`
- 프로토타입 Scene
- `Assets/Scenes/MainScene.unity`
- `Assets/AddressableAssetsData/**`
- `Packages/**`
- `ProjectSettings/**`
- `Assets/Plugins/**`

제품 `GameUI.prefab` 참조만으로 MainScene 변경이 불필요한지 먼저 확인한다. Scene override가 실제 청소기 값을 덮고 있을 때만 차이와 필요 범위를 보고하고 승인 없이 Scene을 수정하지 않는다.

## 14. 구현 순서

1. 필수 규칙과 관련 기능 문서를 읽는다.
2. branch·commit·dirty·stash를 기록하고 변경 allowlist를 정한다.
3. `e1bd9ed`의 원본 사양·코드·리소스만 다시 확인한다.
4. 현재 제품 Controller, Panel, ItemView, DividerBar와 Prefab 참조를 추적한다.
5. `Vacuum.png` 본 파일 동일성과 GUID를 확인한다.
6. Unity Editor에서 최신 Import 설정을 적용하되 GUID를 보존한다.
7. `VacuumWind.mat` 사용 여부를 결정하고 사용할 경우 `.meta`와 함께 이식한다.
8. `GameUI.prefab`에 Visual, GripArea, Nozzle, VFX root를 구성하고 직렬화 참조를 연결한다.
9. `VacuumController`에 기준 상태 캡처와 명시적인 상태 전이를 구현한다.
10. 회전된 Nozzle 기반 UI 흡입 판정을 구현한다.
11. 상품별 흡입·축소·회전·숨김·복원 상태를 구현한다.
12. 순차 배출과 배출 완료 후 복귀를 구현한다.
13. 정상 해제와 강제 취소 API를 분리한다.
14. `SaleSortingPanel`에서 즉시 일괄 해제·분류 흐름을 제거하고 `IsBusy` 입력 중재를 연결한다.
15. VFX pool과 loop SFX 수명을 모든 상태 전이에 연결한다.
16. 순수 상태·수학·복원 계약을 EditMode에서 검증할 수 있도록 필요한 테스트 가능 경계를 최소로 마련한다.
17. Unity import와 compilation을 실행하고 오류를 수정한다.
18. Prefab·Inspector·asset 참조를 Editor에서 정적으로 검증한다.
19. PlayMode, 실플레이와 Player build는 실행하지 않는다.
20. 변경 파일 allowlist, asset/meta 짝, GUID, `git diff --check`를 확인한다.
21. 완료 보고에 실행하지 않은 Play 검증과 남은 사용자 튜닝을 명시한다.

## 15. 검증 제한: 반드시 준수

### 15.1 허용되는 검증

- Unity asset import/reimport
- Unity script compilation
- Console compile error 확인
- EditMode 테스트
- Editor script를 이용한 Prefab/SerializedProperty/asset 참조 검사
- Prefab Mode 또는 Inspector에서의 정적 연결 확인
- Texture Import 설정 확인
- missing script·missing reference 검사
- C# 정적 분석 및 `dotnet build`가 저장소의 승인된 검증 흐름인 경우 해당 build
- Git diff, `.meta` 짝, GUID 중복, YAML 참조 검사

### 15.2 금지되는 검증

다음은 이번 작업에서 실행하지 않는다.

- Unity Editor Play 버튼 실행
- PlayMode 테스트
- 실제 마우스 드래그 플레이 검증
- runtime reflection smoke scene 실행
- 자동으로 Play Mode에 진입하는 Editor 메뉴 실행
- Player build 및 실행
- 별도 실행 파일을 통한 런타임 검증

`origin/astra-prototype`의 `DystopiaTopDownTestTools`에 Play 검증 메뉴나 자동 Play 진입 코드가 있더라도 실행하지 않는다. 해당 도구를 제품 검증용으로 복사하지 않는다.

이번 검증 결과는 `PASS`로 과장하지 않는다. 컴파일과 Editor 정적 검증이 모두 성공하면 프로젝트 규칙에 따라 `STATIC PASS`로 보고하고, 회전 감각·흡입 반경·배출 궤적·VFX 품질은 `PENDING`으로 남긴다.

## 16. EditMode 및 Editor 검증 계획

새 package/framework를 추가하지 않고 기존 Unity Test Framework와 Editor API를 사용한다. `VacuumController`를 테스트하기 위해 private 구현을 광범위하게 public으로 열지 않는다. 순수 계산 helper를 `internal`로 분리할 경우 기존 test assembly의 접근 방식과 asmdef 경계를 먼저 확인한다.

### 16.1 리소스·Prefab 검사

최소 검사 항목:

1. `Vacuum.png`와 `.meta`가 모두 존재한다.
2. GUID가 `8c5509423fa570745bc62cafae857f42`로 유지된다.
3. Texture Type이 Sprite이고 Sprite Mode가 Single이다.
4. PPU 40, Point Filter, mipmap off, compression none이다.
5. `GameUI.prefab`의 Vacuum Image가 null Sprite가 아니다.
6. VacuumController의 root, Image, GripArea, Nozzle, VFX root 참조가 모두 유효하다.
7. 장식 Image/VFX의 raycastTarget이 false다.
8. Prefab에 missing MonoBehaviour가 없다.
9. 추가 asset과 `.meta`가 1:1이다.
10. 전체 Assets GUID 중복이 없다.

### 16.2 순수 상태 계약 검사

Play Mode 없이 가능한 범위에서 다음을 검증한다.

1. 대기 상태에서 `IsHolding`, `IsSpitting`, `IsBusy`가 false다.
2. 상태 전이 수학에서 180도 목표가 정확하다.
3. `flip <= 0.85`에서는 흡입 가능 판정이 false다.
4. 회전된 Nozzle 위치와 방향을 기준으로 위쪽 상품은 포함되고 반대쪽 상품은 제외된다.
5. 같은 상품이 내부 저장 목록에 중복 등록되지 않는다.
6. 삼키기 진행도 0에서 원래 위치·크기, 1에서 Nozzle 위치·최소 크기가 계산된다.
7. 배출 sequence별 각도와 속도가 허용 범위 안이고 결정적이다.
8. 배출 순서가 저장 순서를 보존한다.
9. 정상 배출은 상품을 `Working/Idle`로 돌리고 거래 목록에서 제거하지 않는다.
10. 강제 취소는 위치·회전·크기·Velocity·SortingState·ManipulationState와 active 상태를 복원한다.
11. 일부 상품 배출 후 취소하면 이미 배출된 상품은 되돌리지 않고 남은 상품만 복원한다.
12. 중복 Reset/Cancel 호출이 상품을 중복 배출하거나 유실시키지 않는다.

MonoBehaviour의 `Update`, Input System, 시간 흐름을 실제로 구동해야만 하는 테스트는 이번 범위에서 PlayMode로 만들지 않는다. 대신 상태 계산을 작은 순수 helper로 검증하거나 Editor 정적 검사로 남기고, 검증 한계를 보고한다.

### 16.3 컴파일·Console 검사

1. Unity가 asset import와 script compilation을 끝낼 때까지 기다린다.
2. compile error가 0인지 확인한다.
3. 신규 compile warning을 분류한다.
4. 기존 warning을 이번 변경이 만든 것으로 과장하지 않는다.
5. Console에 missing script, missing reference, serialization type mismatch가 없는지 확인한다.
6. 실행한 EditMode 테스트의 total/pass/fail/skip을 기록한다. total 0 또는 skip을 성공으로 간주하지 않는다.

## 17. 정적 완료 조건

다음을 모두 만족하면 이번 구현을 `STATIC PASS`로 인계할 수 있다.

- 실제 Astra 청소기 Sprite가 `GameUI.prefab`에 연결되어 있다.
- Texture Import 설정이 최신 원본 사양과 일치하고 기존 GUID가 유지된다.
- 청소기 root 아래 Grip과 Nozzle 참조가 명확하다.
- 회전과 판정이 고정된 아래쪽 offset이 아니라 실제 Nozzle Transform을 사용한다.
- Controller에 Idle/흡입/배출/복귀 또는 동등한 명시적 상태 구분이 있다.
- 상품이 청소기에 붙어 보이는 기존 배치 로직이 제거되었다.
- 상품별 흡입·숨김·보관·순차 배출·취소 복원 상태가 구현되었다.
- Panel이 버튼 해제 순간 모든 상품을 즉시 일괄 분류하지 않는다.
- `IsBusy` 동안 직접 드래그·구분봉·자동 정렬 충돌이 방지된다.
- loop SFX와 VFX가 정상 해제·취소·비활성화에서 정리된다.
- Prefab missing reference/script가 없다.
- Unity compile error 0이다.
- 관련 EditMode/Editor 검사가 성공하고 실행 수·실패·skip이 기록되어 있다.
- asset/meta 짝과 GUID 중복 검사가 통과한다.
- `git diff --check`가 통과한다.
- PlayMode·실플레이·Player build를 실행하지 않았다고 명시한다.
- 실플레이 튜닝과 체감 품질이 `PENDING`으로 보고되어 있다.

## 18. 알려진 위험과 판단 기준

### 18.1 Canvas 회전 중심

이미지 pivot과 root pivot이 손잡이 위치에 맞지 않으면 180도 회전 중 손이 미끄러져 보일 수 있다. Sprite Import pivot을 바꾸어 다른 소비자에게 영향을 주기보다 Prefab의 `Visual` 자식 offset과 `GripArea` 위치로 보정한다.

### 18.2 비활성 상품과 Panel 반복문

삼킨 상품은 `items` 목록에는 남고 GameObject만 비활성화된다. Panel의 충돌·분류·자동 정렬 반복문이 `activeInHierarchy`를 확인하는지 모두 추적한다. 확인하지 않는 경로가 있으면 `VacuumAttached` 검사도 함께 보강한다.

### 18.3 배출과 자동 정렬 경쟁

마지막 배출 전 자동 정렬이 시작되면 내부 상품과 방금 나온 상품의 위치 소유권이 충돌한다. `IsBusy`가 false가 된 뒤 한 번만 자동 정렬을 요청한다.

### 18.4 정상 해제와 강제 취소 혼동

포인터 버튼 해제는 배출이고, 화면 종료·일시정지·시설 비활성화는 원상복구다. 하나의 Release 함수가 두 의미를 추측하도록 만들지 않는다.

### 18.5 Editor 검증 한계

이번 작업은 PlayMode가 금지되어 있어 다음을 확인할 수 없다.

- 실제 드래그 손맛
- 0.11초 회전 체감
- 흡입 반경과 속도
- VFX의 실제 Canvas 합성 품질
- 여러 상품이 쏟아질 때 겹침 정도
- 저프레임에서의 시간 전이
- 사운드의 실제 시작·정지 체감

이 항목은 구현 실패가 아니라 명시적으로 남겨야 하는 후속 사용자 검증이다.

## 19. 완료 보고 형식

구현자는 최종 보고에 다음을 포함한다.

```md
- 기준 branch/commit:
- 보존한 기존 dirty·untracked 변경과 stash:
- 실제 수정·생성 파일:
- 원본 e1bd9ed에서 직접 이식한 asset/meta:
- Vacuum.png GUID와 최종 Import 설정:
- 최종 Prefab 계층과 직렬화 참조:
- 구현한 청소기 상태 전이:
- 흡입 판정의 좌표계와 Nozzle 방향 계산:
- 상품 저장·숨김·배출·취소 복원 계약:
- SaleSortingPanel 입력·분류 흐름 변경:
- VFX 구현 방식과 material 사용 여부:
- Unity compile 결과와 신규 Console 오류/경고:
- EditMode 테스트 total/pass/fail/skip:
- Editor Prefab·asset·GUID 검사 결과:
- PlayMode/실플레이/Player build 미실행 확인:
- PENDING인 사용자 체감·튜닝 항목:
- 보호 변경 검토·승인 상태:
- git diff --check 결과:
- Git stage·commit·push·merge 각각의 실제 수행 여부:
```

검증 상태는 PlayMode를 실행하지 않았으므로 최대 `STATIC PASS`다. 컴파일 또는 필수 Editor 참조 검사가 실패하면 `FAIL`, 필수 권한이나 자료가 없어 구현이 불가능하면 프로젝트 규칙에 맞춰 `BLOCKED` 또는 `PARTIAL`로 보고한다.

## 20. Luna 새 세션에 전달할 실행 지시
새 세션에는 이 문서의 구현 범위와 검증 금지 조건을 그대로 전달한다.

> PlayMode·실플레이·Player build 없이 Unity 컴파일, EditMode 검사와 Editor 정적 검증만 수행한다.


## 21. 구현 결과 기록

- 구현 기준은 현재 `vacuum` 브랜치의 `ff05c1f`이며, 이 문서는 작업 중 생성된 계획 문서로 그대로 보존한다.
- 사용자 확인에 따라 제품 UI에는 물리 충돌·속도 적분을 추가하지 않았다. 정상 배출 시 `Position`, `Velocity`, `State = Working`, `Manipulation = Idle`을 설정하고, 이후 이동은 기존 UI 흐름이 소유한다.
- `VacuumController`를 `GripArea` 입력, 회전된 `Nozzle` 좌표·방향 판정, 상품별 흡입/삼키기/복원 상태, 순차 배출, 복귀, VFX pool 상태로 확장했다.
- `SaleSortingPanel`은 정상 배출과 강제 취소를 분리하고, 배출 중 다른 조작·자동 정렬·확정을 막으며, 배출 상품을 기존 거래 목록에서 제거하지 않는다.
- `GameUI.prefab`에는 Astra `Visual`, `GripArea`, `Nozzle`, `SuctionVfxRoot`를 연결했고, `Vacuum.png` import 계약과 `VacuumWind.mat` 원본 재질을 반영했다.
- EditMode 검사 코드는 추가했고 런타임/테스트 어셈블리 컴파일은 오류 없이 통과했다. 런타임 프로젝트에는 기존 `System.Net.Http` 참조 충돌 경고 2개가 남는다.
- Unity Test Runner의 실제 EditMode 실행은 기존 Unity Editor 인스턴스가 프로젝트를 사용 중인 상태에서 배치 실행이 코드 1로 종료되어 결과 XML을 만들지 못했다. 다른 Unity 프로세스를 종료하지 않았다.
- 에셋·Prefab·GUID·물리 적분 금지에 대한 파일 정적 검사는 모두 통과했고, `git diff --check`도 실제 trailing whitespace 없이 통과했다.
- 요청대로 Unity Play/PlayMode, 실플레이, Player build는 실행하지 않았다. 회전 감각, 흡입 반경, 배출 궤적, VFX 합성 품질의 런타임 튜닝은 `PENDING`이다.
- 후속 UI 튜닝으로 Vacuum root scale을 1.9배, HandCursorImage를 160×160, 흡입 UI 보간 가속도 참고값을 2800으로 조정했다. 청소기 경계 제한은 확대된 실제 scale을 반영한다.
- 추가 요구 반영으로 배출 상품은 인수 즉시 판매/제거 영역을 판정하고, 잡기 판정은 `GripArea`에서 Vacuum root 전체로 확장했으며, 본체의 절반이 작업대 밖으로 나갈 수 있도록 경계를 완화했다.
- 최종 검증 상태는 Unity Test Runner 결과 XML이 없어 `PARTIAL`로 보고한다. 컴파일 및 파일 정적 검사는 통과했지만 실제 Editor asset import/Prefab EditMode 실행은 후속으로 남긴다.
- stash는 비어 있었고 stage·commit·push·merge는 수행하지 않았다. 작업 중 IDE가 남긴 `.idea` workspace의 newline-only 변경은 별도 커밋 없이 그대로 표시된다.

새 세션에는 이 문서와 함께 다음 문장을 전달한다.

> `doc/work/astra-vacuum-ui-integration-plan.md`를 단일 구현 계획으로 사용해 현재 브랜치의 임시 UI 청소기를 Astra 사양으로 구현해줘. 문서에 지정된 저장소 규칙과 변경 범위, 정상 배출/강제 취소 계약을 지켜줘. Unity PlayMode 테스트, Editor Play 실행, 실플레이, Player build는 절대 실행하지 말고 Unity 컴파일, EditMode 테스트와 Editor 정적 검증만 수행해줘. 검증 결과는 최대 STATIC PASS로 보고하고 플레이 감각과 수치 튜닝은 PENDING으로 남겨줘.
