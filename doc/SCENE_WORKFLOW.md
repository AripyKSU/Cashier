# 개발 씬과 통합 씬

## 실행 경로

`InitScene (manager·데이터 부트스트랩) → LoadingScene → HubScene → LoadingScene → 게임 씬`

Hub는 진입 후 자동으로 게임 씬을 로드한다. 모든 전환은 `GameSceneManager`가 소유한다.
Editor 개인 설정이 없으면 `Assets/Scenes/MainScene.unity`, 설정이 있으면 선택한 개인 씬을 사용한다.
Player 빌드에는 개인 설정 분기가 포함되지 않으며 항상 MainScene으로 이동한다.
MainScene은 통합·실행 검증용 공용 씬이다. 현재는 기존 Hub의 카메라 구성을 복제한 빈 진입 씬이며, 게임 콘텐츠는 기능별 결과물 통합 시 추가한다.

FinanceScene의 경제 런타임 소유권과 테스트 경로는 [FinanceScene 경제 런타임 구조](FINANCE_SCENE_WORKFLOW.md)를 따른다. FinanceScene은 현재 InitScene을 거치지 않고 직접 실행하는 검증 씬이며, 실제 GameSessionManager 통합은 별도 작업 범위다.

## 개인 작업

1. Unity에서 `Assets/Scenes/Local/` 폴더를 만들고 개인 씬을 저장한다. 예: `Assets/Scenes/Local/MyGameplay.unity`.
2. `Cashier > Gameplay Scene Settings`를 열고 `Personal Scene`에 해당 SceneAsset을 지정한다.
3. `InitScene`을 열고 Play한다. 개인 씬도 동일한 manager·CSV 부트스트랩을 거친다.
4. 통합 검증 시 `Use MainScene`을 누르고 InitScene에서 다시 Play한다.

선택한 GUID는 프로젝트 경로별 `EditorPrefs`에 저장된다. 공유 HubScene에는 개인 설정을 직렬화하지 않는다.
씬이 삭제되거나 Local 밖으로 이동하면 Hub에서 오류를 보고한다. 씬을 다시 선택하거나 Main으로 설정해야 한다.
개인 씬은 Editor 전용 API로 로드하므로 Build Settings·Addressables에 등록하지 않는다.

`Assets/Scenes/Local/` 내부 전체와 `Local.meta`는 Git에서 제외한다. 개인 씬은 Git으로 백업되지 않으므로 필요한 백업은 별도로 관리한다.
개발 코드와 공유할 Prefab·데이터는 기존의 추적되는 기능 경로에 작성하고 개인 씬에는 조립·배치만 둔다.
공유 자산에서 개인 씬이나 Local 내부 자산을 참조하지 않는다. 제외 규칙은 `git add -f`를 막지는 않으므로 강제 stage하지 않는다.

## 통합과 검증

- 개인 씬 파일을 Git 병합하는 대신 검증된 코드·Prefab·데이터·배치를 MainScene에 반영한다. 기능 담당자와 통합 작업자를 정해 순서대로 반영한다.
- MainScene과 `.meta`, Addressables의 `MainScene` entry는 함께 관리한다.
- 공용 manager·Addressables 변경은 AGENTS.md의 교차 리뷰 절차를 따른다.
- 최소 실행 확인: Main과 개인 씬 각각 Init에서 출발해 목적 씬 및 manager 유지, Console 오류를 확인한다.
- 개인 씬 누락·허용 경로 밖 설정은 명시적 실패여야 하며 Main으로 자동 우회하면 안 된다.
- 중복 전환 요청은 거부한다. 호출자 취소는 로딩 시작 전 반영하며, Unity Single 씬 로드 시작 후에는 목적지 활성화까지 완료한다.
- Player 빌드 검증은 Editor PlayMode 검증과 별도로 기록한다.
- 목적지 도착 후 `Cashier > Validate Gameplay Entry`로 선택된 경로와 bootstrap manager 유지를 반복 확인할 수 있다. Console 검사와 Player 빌드 검증은 별도다.

## 재현 가능한 확인 절차

1. 개인 설정을 해제하고 Init에서 Play: MainScene에 도착하며 ResourceManager·DataTableManager·GameSceneManager가 유지되어야 한다.
2. Local에 MainScene 복사본을 저장하고 선택한 뒤 Init에서 Play: 개인 씬에 도착해야 한다.
3. 선택한 개인 씬을 Local 밖으로 이동한 뒤 Init에서 Play: Hub에서 명시적 오류를 보고하고 멈춰야 한다. 이후 씬을 원위치하고 재선택한다.
4. `git check-ignore Assets/Scenes/Local/MyGameplay.unity Assets/Scenes/Local/MyGameplay.unity.meta Assets/Scenes/Local.meta`로 제외를 확인한다.

## 2026-09-07 검증 기록

- `PASS`: Unity 6000.3.18f1 컴파일 오류 0. Init → Hub → Main 및 Init → Hub → Local/GameplaySandbox 두 PlayMode 경로에서 목적지와 세 bootstrap manager 유지를 확인했다. 최종 실행 Console 오류·경고 0.
- `PASS`: 누락 GUID와 Local 밖 씬을 설정했을 때 명시적 거부 및 현재 씬 유지 확인. Hub/Main missing script 0, 신규 GUID 중복 없음, 개인 씬·metadata Git 제외 확인.
- 로딩 씬 비활성화 후 파괴된 CanvasGroup에 접근하던 tween은 OnDisable에서 종료하도록 수정했다.
- 로컬 `GameplaySandbox.unity`는 개발 시작용 예제로 유지하며 Git에는 포함하지 않는다. 최종 선택은 Main으로 복원하고 PlayMode를 종료했다.
- Player 빌드·실행은 미검증. 공용 manager와 Addressables 변경의 팀 교차 리뷰 및 commit·push는 별도다.
