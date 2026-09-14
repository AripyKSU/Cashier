# 탑다운 청소기 사양

## 목적과 적용 위치

탑다운 결제 분류 화면에서 청소기를 손잡이로 잡아 뒤집고, 작업대 위 상품을 입구로 끌어들였다가 마우스를 놓으면 같은 입구로 다시 배출한다. 한 번에 흡입할 수 있는 상품 수에는 제한이 없다.

런타임 계층은 다음과 같다.

```text
TopDownCheckout
└─ Vacuum
   ├─ Grip
   └─ Nozzle
```

- `Vacuum`은 `DystopiaTopDownTest`의 `vacuum` 필드에 연결한다.
- `Vacuum`의 `SpriteRenderer.sortingOrder`는 `110`이다.
- `Grip`은 클릭 판정과 손 커서 고정점이다.
- `Nozzle`은 흡입과 배출이 모두 일어나는 출입구다.
- 연결 메뉴는 `Dystopia > Top Down Test > Connect Vacuum And Lighting`이다. 기존에 배치가 끝난 청소기는 위치를 다시 계산하지 않고 누락된 연결만 보완한다.

## 이미지와 임포트

| 항목 | 값 |
|---|---|
| 이미지 | `Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png` |
| 원본 크기 | 102 × 128 px, RGBA |
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | 40 |
| Filter Mode | Point |
| Mip Maps | Off |
| Compression | None |

이미지는 프로젝트 사용자가 제공해 기존 프로젝트 자산으로 등록한 파일이다. 별도의 외부 라이선스 정보는 저장소에 기록되어 있지 않다.

## 기준 배치

신규 연결 시 스프라이트의 월드 높이를 `spriteHeight`라고 할 때 다음 기준으로 배치한다.

| 대상 | 위치 |
|---|---|
| `Grip.localPosition` | `(0, spriteHeight × 0.38, 0)` |
| `Nozzle.localPosition` | `(0, -spriteHeight × 0.46, 0)` |
| `Vacuum.position.x` | 탑다운 카메라 하단 중앙의 X |
| `Vacuum.position.y` | 탑다운 카메라 하단 Y `+ 0.97 - spriteHeight × 0.38` |
| `Vacuum.position.z` | `TopDownWorkbench.position.z` |

현재 40 PPU와 128 px 이미지 기준 `spriteHeight`는 3.2 월드 단위이므로 `Grip`은 로컬 Y `1.216`, `Nozzle`은 로컬 Y `-1.472`가 된다. 배치 후에는 씬에서 조절한 Transform을 기준 상태로 사용하며 런타임 연출 종료 시 그 위치와 회전으로 돌아간다.

## 입력과 회전

1. 상품 분류 중이며 일시정지가 아니고 게임 창에 포커스가 있을 때만 입력을 받는다.
2. UI, 상품, 구분봉을 잡고 있지 않은 상태에서 `Grip` 반경 `0.4` 안을 마우스 왼쪽 버튼으로 누르면 청소기를 잡는다.
3. 누른 동안 마우스 이동량을 누적해 손잡이를 따라 움직이고, 화면 하단 바깥쪽으로 화면 높이의 45%까지 이동할 수 있다.
4. 청소기는 180도 뒤집히며 `0.11초` 감쇠값으로 회전 시작과 정지를 부드럽게 연결한다.
5. 버튼을 놓으면 저장된 상품의 배출이 끝날 때까지 뒤집힌 자세를 유지한 뒤 기준 배치로 돌아간다.

## 흡입 법칙

| 항목 | 값 |
|---|---:|
| 흡입 거리 | 1.9 월드 단위 |
| 삼키기 판정 반경 | 0.36 월드 단위 |
| 흡입 가속도 | 52 |
| 삼키기 연출 시간 | 0.16초 |
| 부채꼴 판정 | 진행 방향 내적 `-0.17` 이상 |
| 동시 저장 개수 | 제한 없음 |

- 청소기가 충분히 뒤집힌 상태(`flip > 0.85`)에서만 흡입한다.
- 노즐 방향을 중심으로 약 200도 폭의 넓은 부채꼴을 사용해 노즐 바로 아래쪽의 상품도 잡는다.
- 범위 안 상품에는 질량에 비례한 힘을 노즐 방향으로 가한다.
- 상품이 삼키기 반경에 들어오면 물리를 잠시 끄고 `0.16초` 동안 노즐로 이동시키면서 축소하고 회전시킨다.
- 완전히 들어간 상품은 비활성 상태로 청소기 내부 목록에 보관한다. 상품 분류 수량에서는 아직 제거하지 않는다.
- 버튼을 놓기 전에 완전히 들어가지 못한 상품은 흡입 전 위치, 회전, 크기, 물리와 분류 상태로 복원한다.

## VFX

| 항목 | 값 |
|---|---|
| 재질 | `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat` |
| Shader | `Sprites/Default` |
| 선 개수 | 12 |
| 선당 점 개수 | 6 |
| 폭 | 시작 0.025 → 끝 0.008 |
| 정렬 | 청소기보다 1 높은 순서 (`111`) |

VFX는 런타임에 `SuctionWind0`부터 `SuctionWind11`까지 생성한다. 짧은 곡선들이 서로 다른 위상으로 노즐 쪽을 반복 이동하며, 실제 흡입 중에만 표시된다. 생성된 선 오브젝트는 `HideFlags.DontSave`를 사용해 씬 배치에 저장하지 않는다.

## 배출 법칙

| 항목 | 값 |
|---|---:|
| 기본 속도 | 초당 4.8 월드 단위 |
| 상품 간 간격 | 0.07초 |
| 방향 변화 | 최대 ±7도 |
| 속도 변화 | 기본값의 90~110% |
| 회전 변화 | 최대 ±75도/초 |

- 마우스 버튼을 놓으면 완전히 저장된 상품을 흡입 순서대로 하나씩 배출한다.
- 모든 상품은 청소기 뒤쪽이 아닌 들어갔던 `Nozzle`에서 나온다.
- 각 상품에 방향, 속도와 각속도 차이를 주어 같은 궤적으로 겹치지 않게 한다.
- 화면 전환, 비활성화, 포커스 해제나 분류 종료로 동작이 취소되면 아직 배출하지 않은 상품을 흡입 전 상태로 복원한다.

## 연결 파일

| 역할 | 파일 |
|---|---|
| 이미지 | `Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png` |
| 이미지 임포트 설정 | `Assets/DystopiaPrototype/TopDownTest/Art/Vacuum.png.meta` |
| 바람선 재질 | `Assets/DystopiaPrototype/TopDownTest/Art/VacuumWind.mat` |
| 입력·흡입·보관·배출 | `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaVacuumController.cs` |
| 생성·연결·기본 배치 | `Assets/DystopiaPrototype/TopDownTest/Editor/DystopiaTopDownTestTools.cs` |
| 결제 화면 입력 중재 | `Assets/DystopiaPrototype/TopDownTest/Scripts/DystopiaTopDownTest.cs` |

씬 파일은 청소기 배치의 권위가 아니다. 연결 도구가 신규 객체의 기본 위치와 참조를 만들고, 이후 사용자가 씬에서 조절한 배치를 보존한다.

## 확인 절차

1. 탑다운 결제 화면에서 청소기 이미지와 손잡이 위치를 확인한다.
2. 손잡이를 누른 채 움직였을 때 180도 회전이 자연스럽고 손 커서가 손잡이에 붙는지 확인한다.
3. 노즐 정면과 약간 아래쪽에 여러 상품을 놓고 모두 빠르게 흡입되는지 확인한다.
4. 버튼을 놓았을 때 상품이 같은 노즐에서 `토도도도` 시간차로 나오며 궤적이 조금씩 다른지 확인한다.
5. 흡입 중 화면 전환 또는 포커스 해제 후 상품이 유실되지 않는지 확인한다.
