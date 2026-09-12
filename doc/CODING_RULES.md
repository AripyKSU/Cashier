# 프로그래머·기술 작업 규칙

코드 수정 시 [WORK_RULES.md](WORK_RULES.md)와 함께 읽는다. 기존 AGENTS.md의 3·7·8·9절을 원문 번호 그대로 옮겼다. 경로 표기는 저장소 루트 기준이며, 승인·보호 변경·검증은 [WORK_RULES.md 12절](WORK_RULES.md#12-팀-분업과-소유권-경계)과 [10절](WORK_RULES.md#10-검증-절차와-실행-시점)을 따른다.

## 3. 프로그래머 규칙

### 구현과 구조

- 요구사항과 모든 관련 호출·데이터 흐름을 추적한 뒤 공통 원인을 가장 낮은 계층에서 한 번 수정한다.
- 동작을 바꾸지 않는 정리는 기능 변경과 분리한다.
- 하나의 구현만 있는 interface·factory, 한 번만 쓰는 wrapper, 미래만을 위한 확장점은 만들지 않는다.
- 함수는 한 가지 결과를 만들고 이름과 다른 부수 효과를 숨기지 않는다.

### 상태, 데이터와 오류

- 상태 소유자가 변경과 불변 조건을 함께 책임지고, 하나의 권위값만 저장한다.
- 전역·정적 가변 상태, singleton과 cache는 기존 아키텍처가 요구할 때만 사용하며 소유자와 초기화·정리 시점을 명시한다.
- 중복 호출과 재진입은 멱등성을 보장하거나 명확히 거부한다.
- 외부 입력은 경계에서 검증하고 내부에는 검증된 값을 전달한다.
- 식별자와 routing은 프로젝트의 단일 권위 타입·범위를 따르며 문자열, 파일명, 표시명, UI 문구를 내부 키로 쓰지 않는다.
- 신규 데이터는 PK·FK·범위·필수값·parser·loader·버전·직렬화 호환성을 확인한다.
- 파싱 결과는 완전히 검증한 뒤 실제 상태를 교체하고 형식 변경에는 migration 방식을 명시한다.
- null의 의미를 계약에 명시하고 예외를 삼키거나 성공값으로 바꾸지 않는다.
- 처리할 수 없는 오류는 문맥을 보존해 전달하며 실패 중에도 파일, lock, 구독과 임시 상태를 정리한다.
- 부분 갱신이 불일치를 만들면 검증 후 일괄 교체하거나 rollback 가능한 순서를 사용한다.

### 비동기와 성능

- fire-and-forget은 실패를 관찰할 책임자가 있을 때만 사용한다.
- cancellation을 전달하고 정상 취소와 오류를 구분하며 객체 제거 후 callback·coroutine·task의 상태 변경을 막는다.
- 공유 상태의 경쟁 조건과 실행 순서 의존성을 제거하고 무한 retry를 만들지 않는다.
- 측정 전 복잡한 최적화나 cache를 추가하지 않는다.
- 반복 경로의 불필요한 할당·전체 검색·문자열 조합·중복 계산을 피한다.
- 시간 기반 로직은 프레임 수가 아닌 경과 시간을 사용하고 낮은 프레임률과 다중 이벤트에서도 상태 전이를 보존한다.
- 물리·충돌·이동은 프로젝트의 고정 시간 루프와 단일 권위 시스템을 따른다.

### 의존성과 아키텍처

- 새 패키지는 표준 기능과 기존 의존성으로 해결할 수 없을 때만 추가한다.
- 하위 계층이 상위 UI나 구체 저장소를 직접 참조하거나 순환 참조를 만들지 않게 한다.
- 전역 상태·상태 소유권, 공용 manager·API, 데이터 로딩·전달 흐름, namespace 기본 정책, ScriptableObject 데이터 경로, 공용 직렬화 구조 또는 여러 기능이 의존하는 계약을 변경하면 아키텍처 변경으로 판단한다.

## 7. 현재 프로젝트 기술 기준

아래 값은 2026-09-04 기준 실제 프로젝트 파일에서 확인한 현재 상태다. Unity·패키지·권위 폴더 구조·코드 컨벤션을 변경하면 같은 작업에서 이 문서와 근거를 함께 갱신한다.

- Unity: `6000.3.18f1`
  - 근거: `ProjectSettings/ProjectVersion.txt`
- Render Pipeline: Universal Render Pipeline `17.3.0`, 2D Renderer
  - Pipeline asset: `Assets/Settings/UniversalRP.asset`
  - Renderer data: `Assets/Settings/Renderer2D.asset`
- Input: Input System `1.19.0`
- UI: uGUI `2.0.0`, TextMesh Pro 사용
  - `Assets/TextMesh Pro/` 리소스가 존재하고 자체 코드에서 `TextMeshProUGUI`를 사용한다.
- 비동기: Addressables `2.9.1`, 프로젝트 내 UniTask plugin 사용
- Animation 보조: 프로젝트 내 DOTween plugin 사용
- Test Framework: Unity Test Framework `1.6.0`
- 2D 도구: 2D Animation, Aseprite Importer, PSD Importer, SpriteShape, Tilemap
- Visual Scripting `1.9.11`, Timeline `1.8.12`가 설치되어 있다.

`Packages/manifest.json`에 없는 패키지나 신규 외부 plugin을 코드에서 가정하지 않는다. 패키지 추가·제거·버전 변경은 프로그래머 `Primary` 또는 프로젝트 책임자의 승인과 migration 영향 검토 후 수행한다.

## 8. 프로젝트 구조와 신규 파일 위치

### 현재 권위 폴더

| 경로 | 소유 책임 | 신규 파일 기준 |
|---|---|---|
| `Assets/Scripts/Commons/` | 공용 enum, interface, DTO, 상수 | 둘 이상의 시스템이 공유하고 안정된 계약만 배치 |
| `Assets/Scripts/Manager/` | 전역 수명과 공용 서비스 | 기존 manager 책임을 확장할 때만 배치 |
| `Assets/Scripts/Scene/` | Scene 진입·전환·표현 | 특정 Scene의 수명에 종속된 component 배치 |
| `Assets/Scripts/Customer/` | 손님 생성·구매 목록과 관련 데이터 검증 | 손님 기능 코드. `Editor/`의 설치 도구는 개인 씬만 변경 |
| `Assets/Scripts/Events/` | 일간 가격 이벤트 선정·현재가 계산 | 상태 수명과 날짜 권위는 기존 GameSessionManager에 유지. CSV DTO·DataTable은 Commons/Data에 배치 |
| `Assets/Scripts/Utils/` | 상태를 소유하지 않는 범용 도구 | 특정 도메인 규칙을 넣지 않음 |
| `Assets/Datas/` | 런타임 데이터 원본 | 기존 식별자·loader·Addressables 규칙 준수 |
| `Assets/Prefabs/` | prefab과 직렬화 연결 | 기능별 하위 폴더를 사용하고 공용 prefab은 실제 공유 시에만 분리 |
| `Assets/Anims/` | animation clip과 controller | 기능별 하위 폴더를 사용하고 gameplay 판정·타이밍은 코드 권위를 따름 |
| `Assets/Textures/` | texture와 sprite | 기능별 하위 폴더를 사용하고 import·slicing·PPU·pivot 설정을 함께 관리 |
| `Assets/Sounds/` | audio 원본과 runtime asset | 기능별 하위 폴더를 사용하고 원본·압축·사용 위치를 함께 관리 |
| `Assets/Resources/` | 기존 Resources fallback 자산 | 신규 기본 경로로 사용하지 않고 기존 fallback과의 호환에만 사용 |
| `Assets/AddressableAssetsData/` | Addressables 설정과 group | 작업별 승인권을 받은 리소스 담당자 또는 프로젝트 책임자 승인 없이 직접 편집 금지 |
| `Assets/Scenes/` | Unity Scene | 현재 작업의 지정 담당자 또는 프로젝트 책임자 승인 후 생성·이동 |
| `Assets/Scenes/Local/` | 개인 개발 씬 (Git 제외) | Editor 전용. 공유 자산에서 참조하거나 Build Settings·Addressables에 등록하지 않음 |
| `Assets/Scripts/Local/` | 개인 씬 전용 실험·임시 화면 코드 (Git 제외) | Editor 도구는 하위 `Editor/`에 분리. 공유 코드·테스트·자산에서 참조 금지. 제품 기능·manager·공유 API는 기존 추적 경로에 유지 |
| `Assets/Settings/` | URP와 renderer 설정 | 프로그래머 `Primary` 또는 프로젝트 책임자 승인 필요 |
| `Assets/Plugins/` | 외부·vendor 코드 | 직접 수정 금지. wrapper 또는 상위 코드에서 대응 |
| `Assets/TextMesh Pro/` | TMP 기본 리소스 | 프로젝트 UI 정책 변경이 아니면 수정 금지 |

공용 CSV DTO·DataTable과 상품 분류 변환기는 `Assets/Scripts/Commons/Data/`, 손님 전용 데이터와 catalog는 `Assets/Scripts/Customer/Data/`, 경제 CSV DTO·DataTable은 `Assets/Scripts/Finance/Data/`에 둔다. Data와 DataTable은 같은 폴더에 배치하고 `DataTableManager`는 `Manager/`에 유지한다.

### 신규 C# 파일 결정 순서

1. 기존 클래스의 책임에 포함되면 새 파일을 만들지 않고 기존 파일을 수정한다.
2. 공용 데이터 계약이면 `Commons`, manager면 `Manager`, Scene 수명 component면 `Scene`, 무상태 범용 도구면 `Utils`를 사용한다.
3. 어느 폴더에도 맞지 않는 새 시스템은 임의로 기존 폴더에 넣지 않는다. 시스템 담당자와 `Assets/Scripts/<Feature>/` 신설 여부를 먼저 결정한다.
4. Editor 전용 코드는 runtime 폴더에 두지 않고 승인된 `Assets/Editor/` 또는 `<Feature>/Editor/`에 둔다.
5. 테스트를 새로 도입할 때는 runtime 코드와 분리해 `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`를 사용하고 필요한 `.asmdef`를 함께 검토한다.
6. 새 asset과 script에는 Unity가 생성한 `.meta`를 포함하고 파일 이동으로 GUID가 바뀌지 않게 한다.

자체 runtime 경계는 `Assets/Scripts/Cashier.Runtime.asmdef`, 공유 Editor 경계는 `Scene/Editor/Cashier.Scene.Editor.asmdef`다. API 테스트는 `Assets/Tests/EditMode/`와 `Assets/Tests/PlayMode/`의 별도 테스트 assembly에 둔다. 개인 코드의 `Local/Editor/` 경계는 로컬 소유이며 공유 테스트가 참조하지 않는다. 새 assembly·의존성 확대는 기존 승인 절차를 따른다.

자산 하위 폴더는 작업 기능을 기준으로 생성한다. 예를 들어 같은 기능은 `Datas/<Feature>/`, `Prefabs/<Feature>/`, `Anims/<Feature>/`, `Textures/<Feature>/`처럼 이름을 맞춘다. `Common` 또는 `Shared`는 둘 이상의 기능이 실제로 사용하는 자산에만 사용한다.

## 9. Cashier 코딩 컨벤션과 아키텍처 경계

### C# 코드 컨벤션 기준

- 기본 코드 컨벤션은 Microsoft Learn의 [Common C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)를 따른다.
- 이 절에 정의된 프로젝트 규칙은 Microsoft C# 컨벤션보다 우선한다.
- 프로젝트 규칙에 없는 항목만 Microsoft C# 컨벤션을 기본값으로 적용한다.

### Namespace

- 현재 `Assets/Scripts/` 자체 코드는 global namespace를 사용한다.
- 새 파일 하나에만 임의 namespace를 도입해 기존 코드와 혼합하지 않는다.
- namespace 도입이 필요하면 프로그래머 `Primary`가 `Cashier.<Feature>` 형식과 assembly 경계를 먼저 확정하고, 관련 시스템 단위 migration으로 진행한다. 이는 아키텍처 변경이므로 기본 branch 반영 전에 [WORK_RULES.md 12절](WORK_RULES.md#12-팀-분업과-소유권-경계)의 코드 리뷰·명시적 동의 절차를 적용한다.
- `Assets/Plugins/`의 외부 namespace 규칙은 프로젝트 규칙으로 복사하지 않는다.

### Naming

- type, enum, interface, public property, public/protected method: `PascalCase`
- interface: `I` 접두사 사용 (`IDataLoad`)
- private method: `camelCase`
- private·protected field와 local variable: `camelCase`, `_` 접두사 없음
- `const`: `PascalCase`
- 비동기 method: 가능한 경우 `Async` 접미사
- boolean: `is`, `has`, `can`, `should`처럼 참의 의미가 드러나는 이름
- 단위가 중요한 값은 이름에 단위를 포함한다 (`delaySeconds`, `sizePixels`).
- 매직 넘버는 의미가 반복되거나 조정 대상일 때만 `PascalCase` 상수로 승격한다.
- 기존 파일의 일관된 스타일이 다르면 기능 변경과 무관한 전체 rename을 하지 않는다.

### Enum 종료 표식

- enum에 종료 표식이 필요하면 마지막에 `<EnumType>_End` 형식으로 선언한다.
- `_End` 또는 `_end` 종료 항목에는 `= 숫자`를 명시하지 않고 C# 자동 증가값을 사용한다.
- 종료 표식은 유효한 데이터 종류·상태가 아니다. CSV ID 배정, 저장값, loader 등록에 사용하지 않는다.
- 종료 표식도 `Enum.IsDefined`에는 포함되므로 해당 검사만으로 데이터 유효성을 판단하지 않는다. 종료 표식 제외와 실제 등록·허용값을 함께 확인한다.
- 실제 데이터 항목의 승인된 숫자값은 유지한다. 종료 표식 규칙을 이유로 기존 ID를 재번호화하지 않는다.

### 구성원 배치

1. `const`
2. `static`, `static readonly`
3. `[SerializeField] private` 필드
4. 일반 인스턴스 필드
5. property와 indexer
6. event와 delegate
7. 생성자
8. Unity 생명주기 함수
9. public API
10. protected 확장 지점
11. private helper
12. event·충돌 callback
13. 중첩 type

- 같은 종류에서는 `public → protected → internal → private` 순서를 기본으로 하되 호출 흐름과 관련 동작을 가까이 두는 것을 우선한다.
- 필드는 기본적으로 `private`로 두고 필요한 경우 property나 명시적 method로 공개한다.
- 접근 한정자를 생략하지 않고 변경되지 않는 필드는 `readonly`, 컴파일 상수는 `const`로 선언한다.
- Unity 생명주기 함수는 `Awake → OnEnable → Start → Update → FixedUpdate → LateUpdate → OnDisable → OnDestroy` 순서로 둔다.
- overload와 같은 기능의 method는 붙여 둔다.
- 사용하지 않는 변수, method, `using`, 주석 처리된 코드와 의미 없는 주석은 제거한다.
- 기존 파일에서는 현재 스타일을 우선하고 기능과 무관한 전체 재정렬은 하지 않는다.

### Event와 callback

- 현재 자체 코드에는 method parameter와 pool hook에 `System.Action` callback을 전달하는 방식이 확인된다.
- event·delegate를 구현할 때 비동기 완료·취소·수명 처리가 더 명확해지면 UniTask를 우선한다.
- 다중 구독, 연속 상태 변화, 조합·필터링이 필요한 흐름은 UniRx가 승인·설치되어 있고 코드와 수명 관리가 더 간결하며 할당 비용이 합리적일 때 우선 적용한다.
- 단순한 동기 단일 callback은 기존 `Action`, 단순한 지속 구독은 C# `event`를 사용한다. UniTask·UniRx로 불필요하게 감싸지 않는다.
- UniRx는 현재 설치가 확인되지 않았으므로 승인 없이 의존성을 추가하지 않는다.
- Inspector 연결이 실제 요구되는 경우에만 `UnityEvent`를 사용한다.
- 전역 event bus나 새 messaging framework는 승인 없이 추가하지 않는다.
- event handler에서 예외를 숨기지 않고, 객체 파괴 시 event·UniRx 구독을 해제하며 UniTask에는 수명에 연결된 cancellation을 전달한다.

### 주석과 문서

- 새로 작성하거나 수정하는 class, struct, interface와 enum에는 역할과 책임을 설명하는 XML 문서 주석(`/// <summary>`)을 작성한다.
- 새로 작성하거나 수정하는 method와 property에는 목적과 역할을 설명하는 XML 문서 주석을 작성하고, method에는 해당하는 `/// <param>`, `/// <returns>`, `/// <exception>`을 함께 작성한다.
- 중요한 필드에는 용도, 생명주기, 단위, 허용 범위 또는 Unity Inspector 연결 의도가 드러나는 한 줄 주석을 작성한다. public API와 직렬화 필드는 XML 문서 주석을 우선한다.
- 중요한 함수 내부의 주요 기능, 핵심 분기, 상태 전이, 알고리즘, Unity 생명주기 의존성, 부작용과 비직관적인 처리에는 이유를 설명하는 한 줄 주석을 작성한다.
- 코드를 그대로 읽어주는 주석은 피한다.
- 기존 코드를 수정할 때는 변경 범위에 포함된 class, method와 필드의 누락된 문서화도 보완한다.
- TODO에는 적용 조건과 제거 기준을 적는다.
- public API와 데이터 형식 변경 시 관련 명세와 예제를 함께 갱신한다.

### ScriptableObject

- 현재 자체 gameplay 코드에서 ScriptableObject 기반 도메인 데이터 사용은 확인되지 않았다.
- 현재 데이터 경로는 CSV DTO → `IDataLoad`/DataTable → manager와 Addressables를 사용한다.
- 신규 데이터를 편의상 ScriptableObject로 우회하지 않는다.
- 공유 authoring asset, Inspector 편집, 빌드 시 불변 설정이라는 명확한 이점이 있을 때만 프로그래머 `Primary`와 현재 작업의 기획·리소스 담당자가 사용 여부와 migration을 승인한다. ScriptableObject 도입은 아키텍처·데이터 경로 변경으로 보고 기본 branch 반영 전에 [WORK_RULES.md 12절](WORK_RULES.md#12-팀-분업과-소유권-경계)의 코드 리뷰·명시적 동의 절차를 적용한다.

### 공용 시스템

- 비동기에는 프로젝트의 UniTask 패턴과 cancellation을 재사용한다.
- Addressables 로딩은 기존 `ResourceManager`와 `DataTableManager` 경로를 우선하고 소비자가 임의로 중복 구현하지 않는다.
- pooling은 기존 `SimplePool`과 `SimplePoolManager`의 소유·반환 규칙을 따른다.
- Scene 전환은 기존 `GameSceneManager` 책임을 우회하지 않는다.
- 개인 씬 선택과 MainScene 통합은 [`doc/SCENE_WORKFLOW.md`](SCENE_WORKFLOW.md)를 따른다.
- singleton은 기존 전역 수명 manager에만 제한하고 기능 component에 새로 확산하지 않는다.
