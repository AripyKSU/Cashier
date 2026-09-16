# 개발 씬과 통합 씬

## 엔딩과 새 게임 (2026-09-13)

정상 최종 정산 뒤에는 카메라가 포함된 `GoodEndingScene` 또는 `BadEndingScene`으로 전환한다. 종료 화면의 새 게임 버튼은 Hub 메뉴로 돌아간다. 메뉴의 새 게임을 선택하면 이미 부트스트랩된 manager로 데이터·사운드 준비와 세션 초기화를 수행하고 LoadingScene을 거쳐 게임 씬으로 이동한다. Init을 Addressables에 중복 등록하지 않는다. 이어하기는 유효한 저장 데이터가 있을 때만 표시할 후속 사양이며 현재는 숨긴다. [시민권·엔딩 계약과 검증](CITIZENSHIP_ENDING.md)을 함께 확인한다.

## 실행 경로

`InitScene (manager·데이터 부트스트랩) → LoadingScene → HubScene 메뉴`

`Hub 새 게임 → 기존 manager로 새 세션 준비 → LoadingScene → 게임 씬`

Hub는 새 게임·끝내기 입력을 기다린다. 자동 게임 진입은 하지 않는다. 모든 전환은 `GameSceneManager`가 소유한다.
Editor 개인 설정이 없으면 `Assets/Scenes/MainScene.unity`, 설정이 있으면 선택한 개인 씬을 사용한다.
Player 빌드에는 개인 설정 분기가 포함되지 않으며 항상 MainScene으로 이동한다.
MainScene은 통합·실행 검증용 공용 씬이다. 현재 GameUI.prefab 인스턴스, Camera와 InputSystem EventSystem을 포함한다. 설비·명성 통합 경로와 사용법은 MAINSCENE_INTEGRATION.md를 따른다.

Hub 배경은 `Assets/Textures/UI/Hub/hubscene.png`의 단일 2D Sprite를 직접 참조한다. PNG GUID는 유지하며 TextureImporter와 Scene의 Sprite fileID를 함께 관리한다.

### MainScene 4단계 선로드

MainScene 전환은 목적지 활성화 전에 LoadingScene에서 다음 네 단계를 수행한다.

1. `Loading_00~01`: ResourceManager·DataTableManager 준비와 필수 테이블 확인
2. `Loading_02~04`: 상품 기본/탑뷰와 손님 외형 Sprite 선로드
3. `Loading_05~12`: 손님 Normal Texture와 감독관·딸 Sprite 선로드
4. `Loading_13~18`: 가게 단계 Prefab·시계 Sprite·설비 단계별 외형 Prefab 선로드와 StoreStageVisual/설비 UI 구성 검증, MainScene 로드

손님 `ImageResourceIdx`와 딸 `ResourceIdx`가 비어 있으면 해당 Sprite 선로드를 생략하고 기존 사각형 표시 경로를 사용한다. 지정된 잘못된 FK·주소는 오류로 처리하며 손님 Normal Texture는 필수다. 설비의 단계별 Resource FK 0은 표시 없음이며, 비영점 FK는 기존 ResourceManager 캐시로 로드한다.

MainScene 선로드에서는 이미지를 0.2초 간격으로 재생하고 각 이미지 구간을 한 번 재생한 뒤 해당 구간의 마지막 이미지에서 작업 완료까지 멈춘다. MainScene 이외의 일반 전환은 단계 제어를 사용하지 않고 같은 0.2초 간격으로 `Loading_00~18` 전체 애니메이션을 재생하므로 InitScene 부트 후 HubScene으로 이동할 때도 전체 구간이 표시된다. 공유 자산의 소유권은 ResourceManager에 있고 LoadingScene은 직접 해제하지 않는다. MainScene의 GameUIController와 StoreStagePresentation은 직접 Scene 실행의 안전망을 위해 기존 로드 경로를 유지하며, 정상 전환에서는 준비된 캐시를 사용해 Scene 오브젝트 적용만 수행한다. 선로드 또는 검증 실패 시 MainScene을 활성화하지 않고 LoadingScene의 새 게임 재시도 UI를 표시한다.


## 개인 작업

1. Unity에서 `Assets/Scenes/Local/` 폴더를 만들고 개인 씬을 저장한다. 예: `Assets/Scenes/Local/MyGameplay.unity`.
2. `Cashier > Gameplay Scene Settings`를 열고 `Personal Scene`에 해당 SceneAsset을 지정한다.
3. `InitScene`을 열고 Play한 뒤 Hub의 새 게임을 누른다. 개인 씬도 동일한 manager·CSV 부트스트랩을 거친다.
4. 통합 검증 시 `Use MainScene`을 누르고 InitScene에서 다시 Play한다.

선택한 GUID는 프로젝트 경로별 `EditorPrefs`에 저장된다. 공유 HubScene에는 개인 설정을 직렬화하지 않는다.
씬이 삭제되거나 Local 밖으로 이동하면 새 게임 전환에서 오류를 보고한다. 씬을 다시 선택하거나 Main으로 설정해야 한다.
개인 씬은 Editor 전용 API로 로드하므로 Build Settings·Addressables에 등록하지 않는다.

`Assets/Scenes/Local/` 내부 전체와 `Local.meta`는 Git에서 제외한다. 개인 씬은 Git으로 백업되지 않으므로 필요한 백업은 별도로 관리한다.
개인 씬에서만 사용하는 임시 화면·실험 코드는 `Assets/Scripts/Local/`, 해당 Editor 도구는 `Assets/Scripts/Local/Editor/`에 둔다. 이 폴더 전체와 `Local.meta`도 Git에서 제외하며 로컬 파일과 기존 GUID는 보존한다. 이미 추적되던 파일은 ignore 추가만으로 제외되지 않으므로 원래 추적 경로의 삭제 변경도 함께 반영해야 한다.
제품 기능 코드와 공유할 Prefab·데이터는 기존의 추적되는 기능 경로에 둔다. 개인 씬에서 실행했다는 이유만으로 공유 API·manager·도메인 코드를 제외하지 않는다. 공유 코드·테스트·자산은 개인 코드에 의존하지 않는다.
개인 Editor 코드는 런타임 asmdef에 포함하지 않는다. 상위 runtime asmdef가 있는 경우 개인 `Editor/`에도 Editor 전용 asmdef를 두며 이 설정 역시 개인 폴더와 함께 제외한다.
현재 `CustomerSandbox`와 `CustomerSandboxSetup`은 개인 코드다. 현재 total_merge 기반 공유 UI는 GameUI.prefab의 `GameUIController`다. `Dev3SandboxTester`는 비활성화된 이전 공유 코드이며 이번 진행 통합에서 이동·삭제하지 않는다. 공유 코드 정리는 실제 소비자 확인 후 별도 승인 범위로 진행한다.
공유 자산에서 개인 씬이나 Local 내부 자산을 참조하지 않는다. 제외 규칙은 `git add -f`를 막지는 않으므로 강제 stage하지 않는다.

## 통합과 검증

- 여러 branch의 최종 결과물을 조립할 때 사용자가 참조를 요청하면 [BRANCH_INTEGRATION_RULES.md](BRANCH_INTEGRATION_RULES.md)의 자산 우선순위와 로직 통합 기준을 적용한다.

- 개인 씬 파일을 Git 병합하는 대신 검증된 코드·Prefab·데이터·배치를 MainScene에 반영한다. 기능 담당자와 통합 작업자를 정해 순서대로 반영한다.
- MainScene과 `.meta`, Addressables의 `MainScene` entry는 함께 관리한다.
- 공용 manager·Addressables 변경은 [WORK_RULES.md 12절](WORK_RULES.md#12-팀-분업과-소유권-경계)의 교차 리뷰 절차를 따른다.
- 최소 실행 확인: Main과 개인 씬 각각 Init에서 출발해 목적 씬 및 manager 유지, Console 오류를 확인한다.
- 개인 씬 누락·허용 경로 밖 설정은 명시적 실패여야 하며 Main으로 자동 우회하면 안 된다.
- 중복 전환 요청은 거부한다. 호출자 취소는 로딩 시작 전 반영하며, Unity Single 씬 로드 시작 후에는 목적지 활성화까지 완료한다.
- Player 빌드 검증은 Editor PlayMode 검증과 별도로 기록한다.
- 목적지 도착 후 `Cashier > Validate Gameplay Entry`로 선택된 경로와 bootstrap manager 유지를 반복 확인할 수 있다. Console 검사와 Player 빌드 검증은 별도다.

## 재현 가능한 확인 절차

1. 개인 설정을 해제하고 Init에서 Play 후 Hub의 새 게임 선택: MainScene에 도착하며 ResourceManager·DataTableManager·GameSceneManager가 유지되어야 한다.
2. Local에 MainScene 복사본을 저장하고 선택한 뒤 Init에서 Play하고 Hub의 새 게임 선택: 개인 씬에 도착해야 한다.
3. 선택한 개인 씬을 Local 밖으로 이동한 뒤 Init에서 Play 후 Hub의 새 게임 선택: 잘못된 개인 씬 전환을 명시적으로 거부해야 한다. 이후 씬을 원위치하고 재선택한다.
4. `git check-ignore Assets/Scenes/Local/MyGameplay.unity Assets/Scenes/Local/MyGameplay.unity.meta Assets/Scenes/Local.meta`로 제외를 확인한다.
5. `git check-ignore Assets/Scripts/Local/CustomerSandbox.cs Assets/Scripts/Local/CustomerSandbox.cs.meta Assets/Scripts/Local/Editor/CustomerSandboxSetup.cs Assets/Scripts/Local.meta`로 개인 코드와 metadata 제외를 확인한다. 개인 코드가 없는 새 checkout에서도 공유 코드·테스트가 컴파일되어야 한다.

## 2026-09-07 검증 기록

- `PASS`: Unity 6000.3.18f1 컴파일 오류 0. Init → Hub → Main 및 Init → Hub → Local/GameplaySandbox 두 PlayMode 경로에서 목적지와 세 bootstrap manager 유지를 확인했다. 최종 실행 Console 오류·경고 0.
- `PASS`: 누락 GUID와 Local 밖 씬을 설정했을 때 명시적 거부 및 현재 씬 유지 확인. Hub/Main missing script 0, 신규 GUID 중복 없음, 개인 씬·metadata Git 제외 확인.
- 로딩 씬 비활성화 후 파괴된 CanvasGroup에 접근하던 tween은 OnDisable에서 종료하도록 수정했다.
- 로컬 `GameplaySandbox.unity`는 개발 시작용 예제로 유지하며 Git에는 포함하지 않는다. 최종 선택은 Main으로 복원하고 PlayMode를 종료했다.
- Player 빌드·실행은 미검증. 공용 manager와 Addressables 변경의 팀 교차 리뷰 및 commit·push는 별도다.
