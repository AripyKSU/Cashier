# 작업 계획: 로딩 GIF 기반 1회 재생 애니메이션

문서 목적: 이 문서만 Luna의 새 작업 세션에 제공해도 현재 구현과 프로젝트 규칙을 다시 확인하고, 로딩 화면 교체를 독립적으로 구현할 수 있는 실행 명세를 제공한다.

이 문서는 구현 결과가 아니다. 2026-09-16 현재 저장소와 사용자 제공 원본을 읽기 전용으로 조사해 작성한 계획이다. 작성 시 코드, Scene, Texture, Animation, Addressables는 수정하지 않았으며 Unity 실행·컴파일·테스트도 수행하지 않았다.

## 1. 요청과 확정 사양

### 1.1 목적

현재 `LoadingScene`의 임시 `Loading...`/퍼센트/게이지 UI를 사용자 제공 애니메이션 이미지로 교체한다. 애니메이션은 실제 씬 로딩 진행률과 동기화하지 않는 연출이며, 검은 화면에서 재생한다.

### 1.2 확정된 사용자 동작

- 로딩 화면에 진입할 때마다 애니메이션을 첫 프레임부터 한 번 재생한다.
- 실제 로딩 진행률로 애니메이션 프레임을 제어하지 않는다.
- 목적지 씬의 로드가 빨라도 애니메이션을 끝까지 보여 준 뒤 목적지 씬을 활성화한다.
- 목적지 씬의 로드가 애니메이션보다 오래 걸리면 마지막 프레임에서 정지한 채 기다린다.
- 화면은 원본의 16:9 구도와 검은 배경을 자연스럽게 채워 표시한다.
- 정상 로딩 중 기존 `Loading...`, 퍼센트 텍스트와 임시 게이지는 표시하지 않는다.
- 로딩 실패 안내와 재시도 버튼은 유지한다.
- 실제 구현 작업에서는 **PlayMode 실행, 수동 플레이 테스트, Player 빌드 및 실행을 하지 않는다.** 정적 검사와 Unity 컴파일까지만 수행한다.

### 1.3 사용자 제공 원본

- 현재 로컬 위치: `C:/Users/PC/Downloads/Loading.gif`
- 파일 크기: 165,234 bytes
- 해상도: 768×432, 16:9
- 프레임: 19개
- 프레임 지연: 1~18번 각 100ms, 19번 900ms
- GIF 전체 타임라인: 2.7초
- 런타임 동작: 2.7초 동안 원본 타이밍대로 1회 재생한 뒤 19번 프레임 유지

원본 파일은 사용자가 이 작업의 입력으로 직접 제공했다. 외부 다운로드나 대체 이미지를 사용하지 않는다. 저장소에 원본 GIF까지 보관할지는 별도 지시가 없으므로, 제품 런타임에 필요한 변환 자산만 추가하고 원본 GIF 복사는 기본 범위에서 제외한다. 원본 보관이 필요하면 구현 전에 사용자에게 확인한다.

## 2. 반드시 먼저 읽을 문서

구현자는 작업 시작 시 아래 문서를 직접 다시 읽는다.

1. `doc/WORK_RULES.md`
2. `doc/INDEX.md`
3. `doc/CODING_RULES.md`
4. `doc/PREFAB_RESOURCE_RULES.md`
5. `doc/SCENE_WORKFLOW.md`
6. `doc/work/README.md`
7. 이 문서

코드와 공용 Scene을 함께 바꾸므로 프로그래머 규칙과 Prefab/리소스 규칙을 모두 적용한다. `GameSceneManager`는 공용 manager이므로 기본 branch 통합 전 작업자 외 프로그래머 `Primary` 1명 이상의 교차 검토와 명시적 동의가 필요하다. 문서가 이 검토를 대신하지 않는다.

## 3. 현재 구현 기준

### 3.1 씬 전환 소유권

`Assets/Scripts/Manager/GameSceneManager.cs`가 모든 공용 씬 전환을 소유한다.

```text
최초 실행
InitScene → LoadingScene → HubScene

새 게임
HubScene → LoadingScene → InitScene
         → LoadingScene → MainScene 또는 Editor 개인 씬

엔딩과 메뉴 복귀
현재 씬 → LoadingScene → GoodEndingScene/BadEndingScene/HubScene
```

현재 `TransitionAsync`는 다음 순서다.

1. 중복 요청과 취소를 검사한다.
2. 목적지 씬의 존재/address를 사전 검사한다.
3. Addressables의 `LoadingScene`을 `LoadSceneMode.Single`로 로드한다.
4. 로딩 화면 페이드인을 위해 unscaled time 0.3초를 기다린다.
5. `InitScene`, Editor 개인 씬 또는 Addressable 목적지 씬을 `Single`로 즉시 로드·활성화한다.
6. 성공 또는 실패 후 `isTransitioning`을 해제한다.

중복 전환 거부, 시작 전 cancellation, 목적지 사전 검사, Single 로드 시작 후 완료 보장, 실패 handle 해제 계약을 훼손하지 않는다.

### 3.2 현재 로딩 UI

- Scene: `Assets/Scenes/LoadingScene.unity`
- Component: `Assets/Scripts/Scene/LoadingScene.cs`
- 보조 controller: `Assets/Scripts/Utils/LoadingBarController.cs`
- 계층의 핵심: `Canvas/LoadingBar/Gauge`, `Canvas/LoadingBar/Text (TMP)`, `Canvas/EndingRetry`
- Canvas 기준 해상도: 1920×1080, Scale With Screen Size
- `LoadingScene` root의 `CanvasGroup`을 0→1, 0.3초, unscaled time 성격으로 페이드인한다.
- 실패 시 `progressText`에 오류 문구를 쓰고 `EndingRetry` 버튼을 활성화한다.

`LoadingBarController.SetProgress()`와 `LoadingScene.SetProgress()`를 호출하는 실제 로딩 코드는 2026-09-16 조사 기준 존재하지 않는다. 현재 게이지와 퍼센트는 실제 진행률 계약이 아니다. 다만 구현자는 삭제 직전에 `rg`로 소비자를 다시 확인한다.

### 3.3 보존해야 하는 실패 경로

- 최종 엔딩 전환 실패: 결과를 다시 계산하지 않고 같은 결과로 재시도
- 새 게임 부트 실패: `Init` 전환 재시도
- 엔딩에서 메뉴 복귀 실패: `Hub` 전환 재시도
- 오류 안내 문자열과 재시도 버튼 label 변경 동작 유지
- 실패 상태에서 검은 화면이나 마지막 애니메이션 프레임을 배경으로 오류 UI가 읽혀야 한다.

## 4. 구현 설계

### 4.1 GIF 처리 방식

Unity가 GIF 애니메이션을 uGUI에서 직접 재생한다고 가정하지 않는다. 런타임 GIF decoder, 신규 package, VideoPlayer 변환 또는 외부 plugin을 추가하지 않는다.

권장 방식은 다음과 같다.

1. 원본 GIF의 합성 결과를 기준으로 19개 완전 프레임을 무손실 PNG로 추출한다. GIF의 disposal/delta frame을 부분 이미지로 잘못 추출하지 않는다.
2. `Assets/Textures/UI/Loading/` 아래에 런타임 Sprite 자산을 둔다.
3. `Assets/Anims/Loading/` 아래에 non-loop AnimationClip과 필요한 경우 전용 AnimatorController를 둔다.
4. uGUI `Image.sprite`를 object-reference curve로 교체하는 애니메이션을 사용한다.
5. clip의 Loop Time을 끄고 마지막 프레임이 유지되는지 확인한다.

개별 PNG 19개 또는 하나의 sprite sheet 중 프로젝트에서 Unity Editor로 가장 안정적으로 생성·검증할 수 있는 쪽을 선택할 수 있다. 기본 권장은 import와 프레임 참조가 명시적인 개별 PNG 19개다. 어느 방식을 사용하든 다음을 만족해야 한다.

- 프레임 순서가 파일명 정렬에 의존해 뒤섞이지 않게 `Loading_00`~`Loading_18`처럼 zero-padding한다.
- Sprite import, 2D and UI, 단일 Sprite, 중앙 pivot을 사용한다.
- 전체 프레임이 이미 검은 16:9 화면이므로 alpha 기반 잘라내기나 tight mesh로 framing을 바꾸지 않는다.
- 압축으로 pixel art 경계나 색이 흐려지지 않게 실제 표시를 고려한 import 설정을 사용한다.
- 원본 비율을 유지하고 비균등 stretch를 하지 않는다.
- 생성된 각 asset과 Unity `.meta`를 함께 보존한다.

### 4.2 애니메이션 타이밍

원본 타이밍을 권위값으로 사용한다.

| 구간 | 표시 |
|---|---|
| 0.0초 | 첫 프레임 |
| 0.1~1.8초 | 0.1초 간격으로 다음 프레임 표시 |
| 1.8~2.7초 | 마지막 프레임 유지 |
| 2.7초 이후 | 목적지 로드가 끝날 때까지 마지막 프레임 유지 |

AnimationClip만으로 마지막 0.9초와 이후 hold의 의미가 불명확해지지 않도록, 최소 표시 완료 시점을 `LoadingScene`의 명시적 계약으로 둔다. 애니메이션 sample rate 반올림으로 2.7초보다 짧아지지 않게 실제 clip key와 길이를 Inspector에서 확인한다.

시간은 scaled time에 의존하지 않는다. 이전 씬에서 `Time.timeScale == 0`이어도 로딩 연출과 최소 표시 시간이 진행돼야 한다. Animator를 사용하면 Update Mode를 Unscaled Time으로 설정하거나 동등한 방식을 사용한다.

### 4.3 화면 배치

- 애니메이션 Image를 Canvas 전체 stretch로 배치한다.
- 원본과 화면이 모두 16:9이면 화면 전체를 채운다.
- 16:9가 아닌 화면에서는 원본 비율을 유지한다. 이미지가 찌그러지면 안 된다.
- 기본 정책은 전체 구도를 보존하는 fit이다. 남는 영역은 검은색으로 채운다.
- 별도의 검은 full-screen background Image를 애니메이션 뒤에 둬 letterbox 영역과 로딩 초기 1프레임을 검게 유지한다.
- 캐릭터와 게이지가 포함된 하단 구도를 임의 crop하거나 별도 위치로 재배치하지 않는다.
- 오류 문구와 재시도 버튼은 애니메이션보다 위 sibling order에 둔다.
- Raycast가 필요 없는 배경·애니메이션 Image는 Raycast Target을 끈다.

현재 Camera 배경색에 의존하지 말고 Canvas 안의 명시적 검은 배경으로 결과를 보장한다.

### 4.4 최소 2.7초와 실제 로딩의 병행

목적지 로딩을 2.7초 뒤에 시작하면 매 전환 시간이 `2.7초 + 실제 로딩 시간`이 되어 불필요하게 길어진다. 다음 계약으로 구현한다.

```text
LoadingScene 표시 완료
├─ 최소 표시 타이머 2.7초 시작
└─ 목적지 씬을 activation 보류 상태로 로드 시작

최소 표시 2.7초 완료 AND 목적지 activation 준비 완료
→ 목적지 씬 활성화
```

따라서 체감 전환 시간은 원칙적으로 `max(2.7초, 목적지 준비 시간)`이다. 이 타이머에는 LoadingScene 자체를 불러오는 시간이 아니라 `LoadingScene`이 활성화되어 첫 프레임을 표시하기 시작한 이후 시간이 들어간다.

구현 시 `GameSceneManager`가 기존 세 종류의 목적지 로딩 경로를 모두 activation gate로 처리한다.

- Addressable 공유 씬: activation을 보류해 로드하고, 최소 표시 완료 후 `SceneInstance.ActivateAsync()`로 활성화
- Build Settings의 `InitScene`: `SceneManager.LoadSceneAsync`의 activation을 보류하고 최소 표시 완료 후 허용
- Editor 개인 씬: `EditorSceneManager.LoadSceneAsyncInPlayMode`가 반환한 operation의 activation을 같은 계약으로 보류

각 Unity/Addressables API의 완료 의미가 다르므로 “await가 언제 반환되는가”를 추측하지 않는다. activation 보류 시 0.9 progress에서 operation 완료가 멈추는 API는 완료 task 자체를 기다려 deadlock을 만들면 안 된다. API가 제공하는 준비 상태를 관찰한 뒤 타이머와 함께 activation을 허용하는 구조로 작성한다.

로딩 진행률은 애니메이션에 전달하지 않는다. 기존 `LoadingBarController`를 실제 progress controller로 되살리지 않는다.

### 4.5 `LoadingScene` 책임

`LoadingScene`은 다음 표현 수명만 소유한다.

- 화면 진입 시 애니메이션을 첫 프레임부터 재시작
- 최소 표시 완료를 unscaled time 기준으로 알림
- 마지막 프레임 유지
- 기존 0.3초 CanvasGroup fade-in과 `OnDisable` tween 정리
- 실패 문구와 재시도 버튼 표시
- 비활성화/파괴 시 자기 수명의 대기 작업과 animation 상태 정리

최소 표시 대기 API가 필요하면 `Assets/Scripts/Scene/LoadingScene.cs`에 둔다. 새 singleton이나 전역 상태를 추가하지 않는다. `GameSceneManager`가 Scene에서 현재 `LoadingScene`을 얻고 그 인스턴스의 완료를 기다리게 한다.

최소 표시 시간 `2.7f`는 의미와 단위가 드러나는 상수 또는 직렬화 필드로 표현한다. 원본 애니메이션 길이와 다른 값이 중복 권위가 되지 않도록 한 곳에서 관리하고, 변경 이유를 주석 또는 Inspector label로 남긴다.

### 4.6 진행률 코드 정리

삭제 전 다음을 다시 검색한다.

```powershell
rg -n "LoadingBarController\.Instance|LoadingBarController|SetProgress\(" Assets
```

조사 결과가 현재와 같아 외부 소비자가 없다면 다음을 정리한다.

- `LoadingScene.progressBar`
- 정상 로딩 중 퍼센트를 쓰는 `LoadingScene.progressText` 역할
- `LoadingScene.SetProgress(float)`
- `LoadingBarController` 등록/해제
- `Assets/Scripts/Utils/LoadingBarController.cs`와 `.meta`
- `LoadingScene` 안의 `LoadingBarController` GameObject

단, 실패 안내에는 TMP가 계속 필요하다. `progressText`를 무조건 삭제하지 말고 `errorText`처럼 실패 전용 이름과 책임으로 바꾸거나 별도 실패 UI 참조로 연결한다. 직렬화 필드 변경 후 Scene의 missing reference를 반드시 검사한다.

삭제 대상에 예상하지 못한 소비자가 있으면 해당 삭제만 중단하고 문서와 실제 차이를 보고한다. 삭제 승인 범위를 넓혀 추측하지 않는다.

## 5. 예상 변경 범위

### 5.1 수정 허용 후보

- `Assets/Scripts/Manager/GameSceneManager.cs`
- `Assets/Scripts/Scene/LoadingScene.cs`
- `Assets/Scenes/LoadingScene.unity`
- 위 파일들의 기존 `.meta`는 GUID를 유지

### 5.2 생성 허용 후보

- `Assets/Textures/UI/Loading/Loading_00.png` ~ `Loading_18.png` 및 `.meta`
- `Assets/Anims/Loading/LoadingOnce.anim` 및 `.meta`
- 필요할 때만 `Assets/Anims/Loading/Loading.controller` 및 `.meta`

명칭은 기존 자산 충돌 검색 후 동등한 명확한 이름으로 조정할 수 있다. 새 C# 파일은 기본적으로 만들지 않는다. Scene 수명 표현이 기존 `LoadingScene` 책임 안에 있으므로 우선 그 파일을 확장한다.

### 5.3 조건부 삭제 후보

- `Assets/Scripts/Utils/LoadingBarController.cs` 및 `.meta`
- `LoadingScene`의 기존 임시 게이지/정상 로딩 텍스트 GameObject

외부 소비자 재검색과 Git diff 검토 후에만 삭제한다.

### 5.4 제외 범위

- `Packages/manifest.json`과 신규 package/plugin
- `ProjectSettings/`
- Addressables group, address, label 변경
- 다른 Scene 또는 Prefab의 UI 재설계
- 실제 로딩률 계산과 표시
- CSV/DataTable
- 사운드 추가
- 원본 GIF의 재디자인, 캐릭터/게이지 분해 또는 보간 프레임 생성
- 플레이 테스트를 위한 개인 Scene이나 임시 제품 코드
- commit, push, merge

`LoadingScene` 자체 address는 이미 존재하며 유지한다. 추출 Sprite와 AnimationClip은 Scene의 직접 참조로 묶고, 개별 Addressables entry를 추가하지 않는다.

## 6. 구현 순서

1. 작업 시작 전 branch, `git status --short --branch`, untracked, stash를 확인하고 사용자 변경을 보존한다.
2. 필수 문서를 읽고, 위 허용 후보가 현재 checkout과 일치하는지 확인한다.
3. 현재 코드와 Scene을 다시 조사하고 진행률 API 소비자, `LoadingScene` address, 기존 missing reference 여부를 기록한다.
4. 원본 GIF의 해상도, 19프레임, 지연값을 재확인한다.
5. 완전 합성 PNG 19개를 추출하고 프레임 0, 중간, 마지막 이미지를 육안 또는 이미지 도구로 확인한다.
6. Unity Editor를 사용해 Texture import 설정과 AnimationClip을 생성한다. Scene YAML을 대규모로 직접 작성하지 않는다.
7. `LoadingScene` Canvas에 검은 배경, 전체 화면 애니메이션 Image, 실패 overlay 순으로 계층을 구성한다.
8. non-loop/unscaled 애니메이션과 마지막 프레임 hold를 설정한다.
9. `LoadingScene`에 최소 2.7초 완료 계약과 실패 전용 TMP 참조를 반영한다.
10. `GameSceneManager`의 세 목적지 경로에 activation gate를 적용한다. 기존 사전 검사, cancellation, 중복 거부, 실패 release와 retry 계약을 보존한다.
11. 진행률 코드의 외부 소비자가 없으면 조건부 삭제 범위를 정리한다.
12. Unity가 reimport와 compile을 끝낼 때까지 기다린다.
13. 아래 정적 검증만 수행하고 PlayMode에는 진입하지 않는다.
14. diff와 생성/삭제 asset-meta 짝을 확인하고 결과를 보고한다. stage/commit/push/merge는 하지 않는다.

## 7. 오류와 정리 계약

- 목적지 로딩 실패 시 LoadingScene을 유지하고 기존 상위 retry 경로가 오류 UI를 표시할 수 있어야 한다.
- 실패한 Addressables handle만 release한다. 성공한 씬 handle의 기존 수명 정책을 임의로 바꾸지 않는다.
- activation 보류 중 예외가 발생해도 `isTransitioning`은 `finally`에서 해제돼야 한다.
- 시작 전 전달된 cancellation은 기존처럼 LoadingScene 진입 전에 반영한다.
- Single 목적지 로드를 시작한 뒤에는 호출자의 cancellation으로 중간 중단하지 않는 기존 계약을 유지한다.
- 파괴된 `LoadingScene`, `CanvasGroup`, Animator 또는 UI에 지연 callback이 접근하지 않게 한다.
- 실패 UI로 전환할 때 애니메이션은 마지막 프레임 또는 현재 프레임에서 안전하게 멈춰도 된다. 오류 메시지와 버튼 가독성이 우선이다.
- retry 때 같은 LoadingScene 인스턴스를 재사용한다면 애니메이션 최소 시간 정책을 다시 적용할지 명확히 한다. 기본값은 retry 요청 시 애니메이션을 첫 프레임부터 재시작하고 새 목적지 로드에 다시 2.7초 최소 표시를 적용하는 것이다.

## 8. 검증 계획 — PlayMode 금지

### 8.1 명시적 금지

이 작업에서는 다음을 수행하지 않는다.

- Unity Play 버튼 실행
- PlayMode test
- Init → Hub → Main 등 실제 씬 전환 조작
- 수동 플레이 테스트
- Player build 또는 빌드 실행
- 테스트 목적으로 공유/개인 Scene을 추가 수정

Unity Editor는 asset import, Scene/Animation 편집, 저장, reimport와 compilation 확인에만 사용한다. 현재 Editor에 다른 작업자의 Play 세션이나 저장되지 않은 변경이 있으면 조작하지 말고 중단·보고한다.

### 8.2 허용된 정적 검증

- GIF와 추출 결과: 768×432, 19프레임, 순서와 마지막 프레임 확인
- AnimationClip: Loop Time off, unscaled 재생 설정, key 순서, 2.7초 계약 확인
- Texture: Sprite import, 비율, pivot, framing, 압축 설정 확인
- Scene 저장 후 missing script/missing reference 0 확인
- asset와 `.meta` 1:1, GUID 중복·기존 GUID 변경 없음 확인
- `LoadingScene` 직렬화 필드가 실제 객체에 연결됐는지 확인
- Addressables 설정에 의도하지 않은 diff가 없는지 확인
- Unity reimport 및 compilation 완료, compile error 0 확인
- `rg`로 삭제한 진행률 API의 잔여 참조 0 확인
- `git diff --check`
- 변경 파일이 이 문서의 allowlist와 일치하는지 확인

컴파일 확인은 실행 검증이 아니다. 최종 상태는 최대 `STATIC PASS`로만 보고한다. 화면 비율, 실제 2.7초 체감, 마지막 프레임 hold, 빠른/느린 로딩, 실패와 retry 동작은 모두 `PENDING — 사용자 또는 후속 담당자의 PlayMode 확인 필요`로 남긴다.

## 9. 완료 조건

다음 항목을 모두 만족하면 구현 작업을 정적 완료로 인계할 수 있다.

- 정상 로딩 UI에서 임시 text/퍼센트/gauge가 제거되거나 비표시다.
- 사용자 제공 이미지가 첫 프레임부터 2.7초 동안 한 번 재생되도록 구성됐다.
- 최소 2.7초와 목적지 로드가 병행되며 둘 다 준비된 뒤에만 활성화하는 코드 구조다.
- 느린 로딩에서는 마지막 프레임을 유지하도록 구성됐다.
- 검은 배경과 원본 16:9 비율 보존 정책이 Scene에 반영됐다.
- 기존 세 종류의 실패 문구와 retry 버튼 연결이 보존됐다.
- 외부 package, Addressables 설정, ProjectSettings 변경이 없다.
- Unity compile error가 0이다.
- missing script/reference와 asset-meta 누락이 없다.
- PlayMode/플레이 테스트/빌드를 실행하지 않았다.
- 실행하지 않은 검증을 `PASS`라고 보고하지 않았다.

## 10. 구현 중 차단하고 확인할 조건

다음 상황에서는 임의로 범위를 넓히지 말고 해당 변경을 중단해 사용자에게 보고한다.

- 원본 GIF를 열 수 없거나 768×432/19프레임/2.7초 정보와 다름
- activation 보류 API가 현재 Unity 6000.3.18f1 또는 Addressables 2.9.1에서 기존 실패/release 계약을 안전하게 유지할 수 없음
- Editor 개인 씬 경로만 동일한 activation gate로 처리할 수 없어 별도 정책이 필요함
- 진행률 controller에 조사 시점 이후 신규 소비자가 생김
- LoadingScene 외의 Scene, Addressables 또는 ProjectSettings 변경이 필요함
- 기존 미커밋 변경이 대상 코드/Scene/자산 경로와 겹침
- 컴파일 오류를 해결하려면 허용 범위 밖 파일을 수정해야 함

## 11. 완료 보고 형식

Luna는 작업 후 다음을 간결하게 보고한다.

```md
# 로딩 애니메이션 구현 결과

- 변경/생성/삭제 파일:
- GIF 변환 결과: 해상도 / 프레임 수 / 타이밍 / import 설정
- Scene 연결: 배경 / Image / Animation / 오류 overlay
- 코드 변경: 최소 표시 gate / 세 목적지 경로 / retry·오류 보존
- 진행률 임시 코드 처리:
- 검증: Unity compile / missing reference / meta·GUID / diff check
- 미검증: PlayMode, 실제 화면과 전환, Player build
- 상태: STATIC PASS / PARTIAL / BLOCKED / FAIL
- 보호 변경 리뷰 필요 사항:
- Git: stage / commit / push / merge 각각 수행 여부
```

## 12. Luna 새 세션에 전달할 시작 지시

다음 문장을 이 문서와 함께 전달하면 된다.

```text
doc/work/loading-animation-implementation-plan.md를 단일 작업 명세로 삼아 로딩 애니메이션을 구현해줘.
문서가 요구하는 저장소 규칙과 관련 파일은 네가 직접 다시 읽고 현재 checkout과 대조해.
허용 범위를 넘기거나 차단 조건을 만나면 임의 확장하지 말고 보고해.
Unity PlayMode, 수동 플레이 테스트, PlayMode test, Player build는 실행하지 마.
asset import·Scene/Animation 편집·reimport·compile과 문서에 적힌 정적 검증까지만 수행해.
commit, push, merge는 하지 마.
```
