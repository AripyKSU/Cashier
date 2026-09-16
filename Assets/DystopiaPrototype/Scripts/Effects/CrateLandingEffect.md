# CrateLandingEffect — 상자 착지 연출 사용법

정면 화면에서 상자를 내려놓을 때 쓰는 연출을 독립 컴포넌트로 뗀 것이다. 상자가 위에서 떨어져 착지하고, 밑면을 고정한 채 가로로 늘었다가 세로로 눌리며 복원되고, 좌우로 픽셀 먼지가 잠깐 퍼진다. 원본 구현은 `TopDownTest/Scripts/DystopiaTopDownTest.cs`의 `PlaceContainer()`이며 수치는 동일하다.

## 파일

- `Assets/DystopiaPrototype/Scripts/Effects/CrateLandingEffect.cs` — 컴포넌트 (uGUI 전용, 외부 의존성 없음)
- 먼지 이미지는 없다. 먼지는 uGUI `Image` 단색 사각형(12×6, 색 `(0.34, 0.32, 0.28)`)을 코드가 만든다. 원하면 `dustSprite`에 Sprite를 넣어 바꿀 수 있다.

## 붙이는 법

1. 상자 `Image`가 있는 오브젝트(예: `FrontContainer`)에 `CrateLandingEffect`를 추가한다. `crate`를 비워 두면 같은 오브젝트의 Image를 쓴다.
2. 상자의 RectTransform은 **anchor (0,1), pivot (0,1)** 좌상단 기준이어야 한다. 정면 화면의 모든 소품이 이 규칙이다. 다른 피벗이면 눌림 보정이 어긋난다.
3. 착지 시점에 코드에서 호출한다.

```csharp
var landing = frontContainer.GetComponent<CrateLandingEffect>();
landing.IsPaused = () => session.IsPaused;   // 선택: 일시정지 중 시간 정지
landing.Play();                               // 0.85초 뒤 자동 복원
```

`Stop()`을 부르면 즉시 원래 배치로 되돌린다. 오브젝트가 비활성화되어도 자동 복원된다.

## Inspector 값

| 필드 | 기본값 | 의미 |
|---|---|---|
| `dropHeight` | 76 | 떨어지기 시작하는 높이(캔버스 1280×720 px). 2026-09-16에 38에서 2배로 올렸다 |
| `dropSeconds` | 0.3 | 낙하 시간. `1-(t)^2` 곡선으로 가속 |
| `settleSeconds` | 0.55 | 착지 후 눌림·복원·먼지 시간 |
| `squash` | 0.10 | 착지 순간 가로 +10% / 세로 -10%, `sin(2πt)·e^(-4t)` 감쇠 진동 |
| `dustCount` | 10 | 먼지 조각 수. 짝수 번호는 왼쪽, 홀수 번호는 오른쪽 |
| `dustSize` | (12, 6) | 먼지 조각 크기 |
| `dustColor` | (0.34, 0.32, 0.28) | 먼지 색. 알파는 착지 직후 0.42에서 0으로 |
| `dustSprite` | 없음 | 먼지 Sprite(선택) |

## 동작 규칙 (프로젝트 배치 보호 규칙과 같음)

- 시작할 때 상자의 `anchoredPosition`·`localScale`을 읽어 기준값으로 쓰고, 끝나면 그 값으로 되돌린다. 코드에 고정 좌표가 없다.
- 연출 중 Inspector에서 상자를 옮기거나 크기를 바꾸면 그 프레임에 연출을 중단하고 바뀐 값을 그대로 둔다.
- 먼지는 상자 부모 아래 `LandingDust0..N` 이름으로 만든다. 씬에 같은 이름의 Image가 이미 있으면 재사용한다(정면 씬의 `FrontView/LandingDust0~9`가 그 예). 위치는 2px 격자에 스냅해 도트 화면에서 흔들리지 않는다.
- 시간은 `Time.unscaledDeltaTime`을 쓴다. `IsPaused`가 true를 돌려주는 동안 멈춘다.

## 정면 화면과의 관계

`DystopiaTopDownTest.PlaceContainer()`는 이 컴포넌트를 쓰지 않고 같은 수치를 직접 실행한다(어린이 손님이면 상자를 숨기고 등장 연출로 대체). 다른 씬이나 프로젝트에서 같은 연출이 필요할 때 이 컴포넌트를 쓰면 된다.
