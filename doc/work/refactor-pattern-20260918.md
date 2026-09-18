# 사용자 Pool·DOTween 패턴 적용 검토

- 담당: 성규 담당 기능에 대한 아키텍처·구현 위임 범위. Unity 검증은 아키텍처 담당 단독 수행.
- 기준: `refactor_fix`, 시작 시 `63f42749` 및 사용자 미커밋 변경. 작업 도중 해당 사용자 변경이 `6c7ef198`에 반영되어 이후 기준으로 사용한다.
- 사용자 기준 구현: `CustomerGenerator.resourceIdx/speechIdx` → ResourceData → `WorldVisit`/`WorldQueueSpeech` Prefab → `SimplePoolManager`, `HubScene`의 DOTween 알파 반복.
- 제약: 공용 Prefab ID 상수 유지. CSV 열·ID 배정·공용 Catalog·Addressables·Scene·Prefab 구조의 신규 변경 없음. 자산 로드와 도메인 비동기는 유지.
- 구현 전 상태 보관: `Temp/RefactorPattern/before-working.diff`, `before-staged.diff`. 기존 폰트·설정·사용자 코드 변경은 별도 소유다.

## A. 실제 수정 항목

| 파일 | 기존 문제 | 적용 패턴 / 변경 이유 |
|---|---|---|
| `Assets/Scripts/Scene/CustomerWorldQueueView.cs`, `World/WorldVisit.cs`, `World/WorldQueueSpeech.cs`, `Manager/GameSceneManager.cs` | 사용자 Pool 전환의 대여 실패·반환·수용량 경계와 프레임 직접 연출 | 기존 Prefab/Pool 연결을 완성. 현재1·대기10·불만 이탈10·거래 퇴장1에 맞춰 Pool22, 부분 대여 실패 시 반환, 반환 전 Tween Kill·Sprite/TMP/MPB 초기화. 이동·호흡·반응은 DOTween 진행값을 기존 표현 delta로 샘플링하여 차단과 수동 시간 계약 보존. 바닥·연령 오프셋·대사 수명 유지 |
| `Assets/Scripts/UI/SaleSortingPanel.cs`, `SaleSortingItemView.cs` | 거래마다 동일 상품 Prefab을 생성·파괴, 슬라이드·낙하·정렬·계산기 시간을 각 Coroutine에서 적분 | 기존 로컬 Prefab용 `SimplePool<T>`로 9개 대여·반환. 현재 판매 슬롯9 및 CSV 최대3종×3개 기준. 초과 주문은 부분 대여 전에 거부, 초기화 실패 시 대여분 모두 반환, 재사용 시 입력·회전·속도 초기화. 시각 보간은 DOTween Sequence, Coroutine은 완료 대기·상태 순서만 유지. 중단 시 원래 상자 자세 복구, 재진입 시 이전 완료 알림 방지 |
| `Assets/Scripts/Scene/HubScene.cs` | DOTween과 사용하지 않는 이전 UniTask 루프 병존, Kill한 tween 참조 재사용 | 기존 DOTween 기준을 유지하되 OnEnable에서 재생성, OnDisable에서 Kill/null. unscaled 시간 유지 |
| `Assets/Scripts/Scene/EndingPresenter.cs` | 암전·페이지 fade·표시 Delay를 UniTask 루프에서 직접 적분 | DOTween이 시간·값을 보간하고 기존 UniTask는 완료·취소 순서만 기다림. 로딩/SFX/입력/최종 문구 계약 유지 |
| `Assets/Scripts/UI/Presenters/InspectorPresenter.cs` | UniTask 루프의 색·알파 보간 | 색·CanvasGroup 알파를 Sequence로 결합. scaled 시간·차단·취소 시 완료 미발행 유지 |
| `Assets/Scripts/UI/Presenters/DailySettlementLedgerView.cs` | 두 페이지 타이핑과 간격 Coroutine | Sequence의 문자 수 트윈·간격. 중단·즉시 완료·재진입에서 완료 알림 한 번 |
| `Assets/Scripts/UI/Presenters/DaughterDialoguePresenter.cs` | 타이핑과 고개 끄덕임 Coroutine | 한 DOTween 진행값에 TMP 공개와 기존 목 피벗 수식 적용. 종료 자세·음성 정리 유지 |
| `Assets/Scripts/UI/Presenters/ReputationStampPresenter.cs` | 도장 크기·충격 회전 Coroutine | 기존 충격 곡선·효과음 시점을 DOTween 진행값으로 구동. 취소는 완료 이벤트를 발생시키지 않음 |
| `Assets/Scripts/UI/PortraitIdleBob.cs` | Update에서 직접 유휴 시간 계산 | unscaled 반복 트윈과 정수 픽셀 스냅. 비활성화 시 원위치 복원 |
| `Assets/Scripts/UI/LandingDustEffect.cs` | 입자 10개의 위치·알파를 Coroutine 적분 | 기존 캐시 입자 유지, 하나의 차단 가능한 DOTween으로 확산·fade. 반복 재생 시 이전 트윈 Kill |
| `Assets/Scripts/UI/DividerBarController.cs` | 입력 루프에 밝기 힌트 수식 혼재 | 힌트만 DOTween으로 샘플링. 입력·충돌·속도·기울기 계산은 유지 |
| `Assets/Scripts/UI/VacuumController.cs` | 흡입 표시의 시간 적분과 게임 조작 결합 | 바람 12개의 위상만 DOTween으로 구동. 전달된 delta로 샘플링해 pause와 수동 API 시간 경계 유지 |
| `Assets/Tests/EditMode/{BusinessClockAndSortingTests,WorldSceneTests}.cs`, `Assets/Tests/PlayMode/{GameSessionApiTests,PresentationTweenTests}.cs` | Pool 전환 후 재사용·중단 수명과 사용자 public Visual 변경을 기존 fixture가 반영하지 못함 | 실제 Pool 재대여·고갈/반환, 연출 중단/재시작·차단·완료 알림 검증. 실제 Prefab Pool을 준비하고 public Visual/TMP 경로를 사용. 일일 지침 시 커버 해제, CSV의 서로 다른 SFX 키·Delay, 유지비/벌금 분리라는 현행 동작에 구형 fixture 기대를 일치시킴. 경제·음성 제품 로직은 변경하지 않음 |

DOTween core DLL만 현재 runtime assembly에서 사용 가능하다. `ModulesUI`, `ModulesUnityVersion`, `UNITASK_DOTWEEN_SUPPORT`를 추가하지 않고 `DOTween.To/ToAlpha`, Sequence를 사용한다. 중단은 `Kill(false)`이며 완료를 강제하지 않는다. 절차적 형태 계산은 보간된 진행값을 받아 기존 모양을 유지한다.

## B. 조사했지만 변경하지 않은 항목

| 항목 | 유지 이유 |
|---|---|
| `LandingDustEffect` 10개 / `VacuumController` 12개 Image 생성 | 최초 생성 후 배열에서 재사용한다. 반복 생성·반환 수명이 없어 추가 Pool은 이득이 작다. 시각 연출만 A처럼 변경 |
| `CustomerPresenter` basketItems | 부족한 수만 Prefab 생성하고 기존 목록을 재사용. 이미 재사용 구현이 있어 이중 Pool 불필요 |
| `CustomerPresenter` DialoguePanel/TMP/Canvas, `GameUIController` CanvasGroup, `LoadingScene` CanvasGroup | 정상 Prefab 참조를 우선 사용하고 누락 시 한 번 보완하는 수명 캐시 |
| `StoreStagePresentation` 좌우 매대 확장·설비 외형 | 매대 확장 최대 2개 캐시. 설비는 단계 변경 때만 재조립하며 서로 다른 Prefab이다. 빈번한 반복 수명 아님 |
| `SoundManager` AudioSource | 채널 목록/키별 캐시로 기존 AudioSource 재사용 |
| `InitScene` manager / `Dev3SandboxTester` root | 부트 또는 디버그 수명에 한 번 생성 |
| Sprite/Texture/Material/MaterialPropertyBlock/Mesh/RenderTexture | GameObject 대여와 다른 native 자원 수명. 예: WorldSceneView MPB 캐시·GameUI placeholder 캐시. 이번 작업에서 자원 소유권을 재설계하지 않음 |
| `CustomerQueueView`, `PriceListPanel` | 공유 Scene/Prefab의 script GUID 참조와 실제 제품 호출을 조사했으며 활성 제품 경로가 발견되지 않은 legacy 후보. 활성 World 큐를 우선 변경 |
| `TimeOfDayPixelStage/Source`, `FogMotionController` | 공유 Scene/Prefab 참조가 발견되지 않은 실험/이전 렌더 경로. 별도 camera/mesh 수명까지 바꾸지 않음 |
| `WorldSceneView.AdvanceEffects`, 새·안개 shader, `TimeOfDayUIController` | 공용 표현시간 스냅샷에 의존하는 연기 프레임·경비병 발사 상태·shader 위상과 영업시각→조명 계산. 단순 fade 치환으로 시간을 독립시키면 pause/재활성/API 수동 시간 진행 계약이 바뀜. 현재 샘플링 계약 유지 |
| 밀대·청소기·손 커서의 입력 추종, 상품 물리/분류 판정 | 위치/속도가 조작과 게임 판정에 직접 사용되므로 순수 시각 연출에 해당하지 않음 |
| `LoadingScene` fade / `FacilityPamphletShine` | 이미 DOTween 사용. 로딩 Animator와 준비 단계·최소 표시 대기는 로딩 상태 순서에 연결되어 유지 |
| Addressables / DataTable / GameProgress / SoundManager 비동기 | 로딩·상태·음성 수명 처리. 시각 Tween으로 대체하지 않음 |
| `Assets/DystopiaPrototype`, Editor·테스트·개인 Local 도구 | 운영 코드의 활성 소비자를 기준으로 이관. prototype/test 객체 생성을 제품 Pool 대상으로 확대하지 않음 |

## C. 후속 후보

| 문제 | 장기 권장 방향 | 이번 보류 이유 |
|---|---|---|
| `EndingPresenter.OnDisable`에서 `SimplePoolManager.ClearAll()` 호출 | 화면은 자신의 대여분만 반환하고 전체 Pool 정리는 세션/씬 소유자가 담당 | 사용자가 추가한 전역 정리 의미와 새 게임 로드 순서까지 바뀌는 공용 소유권 문제. 이번에 임의 삭제하지 않음 |
| CustomerVisit가 표시용 Prefab ID를 전달하고 WorldVisit가 큐 Visual DTO를 생성 | 표시 데이터와 도메인 계약의 경계를 별도 검토 | 사용자 대표 코드와 public contract를 우선 보존. 중앙 상수·CSV/Catalog 재설계 금지 범위 |
| WorldSceneView의 shader 시간과 CPU 절차적 연출이 한 API에 결합 | 기존 `AdvanceEffects`의 외부 시간 계약을 유지한 상태에서 순수 표시 경로만 별도 설계 | 일률적인 자동 Tween clock 전환은 공개 시간 진행 계약과 shader 동기화를 바꿈. 별도 범위·검증 필요 |
| 미사용 legacy UI/프로토타입 코드와 정상 Prefab 밖 fallback | 실제 소비자 소유자 확인 후 제거 또는 직렬화 연결 정리 | 이번 요청은 삭제나 자산 구조 전면 교체가 아님 |

## 검증

- 작업 전: 기존 Editor는 InitScene, Play/compile false, dirty false, compileFailed false.
- 수정 중 자동 컴파일에서 DOTween Module 확장 접근 오류를 확인하고 core API로 보정했다. 이를 최종 컴파일 결과로 대체하지 않는다.
- Unity 6000.3.18f1의 기존 Cashier Editor(PID25032)에서 검증. 마지막 코드 변경 후 명시적 컴파일 완료(18:41 KST). DOTween 모듈 접근 오류 및 테스트 Dictionary namespace 오류는 최종 컴파일 전에 보정했다.
- EditMode **33/35**, 실패2·skip0: `BusinessClockAndSortingTests` 21/23, `WorldSceneTests` 12/12. 신규 상품 Pool 재사용·상태 초기화·용량 거부 테스트는 통과. 실패2건은 기존 `doc/TESTING.md`에도 기록된 `Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings`, `VacuumAsset_UsesAstraImportContract`의 구형 자산 경로 기대이며 이번 제품 수정 범위 밖이다.
- Edit 증거: `Temp/RefactorPattern/world-edit.json`, `sorting-edit-summary.json`. 후자는 최초 CLI stderr가 파일에 보관되지 않아 도구 실행 결과의 건수·실패명을 옮긴 요약이며 원본 XML/로그가 아니다.
- PlayMode 단위 연출 `PresentationTweenTests` **5/5**, 기존 `ResourcePoolTests` **5/5**. `presentation-play.json`, `resource-pool-play.json`.
- 실제 연결 API 개별 검증: 대기열 재사용·독립 대사 수명, 계산기 분류 수명, 상자 pause/재시작/취소, 감독관 가림/재생성/퇴장, Main 자산·Pool 사전준비, 엔딩 4종 컷씬·알파·대기·효과음. 각각 1/1: `queue-play.json`, `calculator-play.json`, `container-play.json`, `inspector-flow-fixed-play.json`, `preload-play.json`, `ending-fixed-play.json` (모두 `Temp/RefactorPattern/` 아래).
- 감독관 최초 실패는 PreOpen에도 커버가 남아야 한다는 구형 기대였다. 엔딩 최초 실패는 서로 다른 SFX 키도 같은 AudioSource여야 한다는 구형 기대였다. CSV의 3초 Delay와 4416/4417도 fixture에 반영했다. 최초 실패 결과(`inspector-flow-play.json`, `ending-play.json`)와 수정 후 결과를 구분한다.
- 정산 통합 최초 실행은 CLI 연결이 끊겼지만 동일 run의 결과 파일을 회수했다(`settlement-recovered-play.json`). `총지출` 구형 표시 기대가 실패했으며 현재 유지비/지침 벌금 분리 표시로 fixture만 갱신했다. 실행 전 연결 거부는 테스트 실행 건수에 포함하지 않는다.
- 정산 fixture 갱신 후 **1/1** 통과(`settlement-fixed-play.json`). 관련 PlayMode 최종 고유 테스트는 **17/17**, 실패·skip0. 최초 구형 기대 실패3회와 수정 후 재실행3회를 모두 포함한 실행 횟수는20건(통과17·실패3)이며, 전체 suite 재실행은 하지 않았다.
- 최종 Editor: InitScene clean, Play/compile/compileFailed=false, 시작 씬 override 없음, runInBackground=false. 검사 시 Console error0(`editor-after-tests.txt`, `final-console-errors.json`). 별도 실제 플레이 순회나 전체 제품 Console 무오류 증거로 확대하지 않는다.
- 보호 대상10개 SHA256 보존(`protected-before-tests.json`, `protected-after-tests.json`): Main/Init, GameUI/World Prefab, ResourceData, Addressables 그룹, 사용자 PixelCaps 폰트, URP, ProjectSettings. 테스트가 변경한 ProjectSettings 줄바꿈은 사전 SHA256과 일치하는 내용으로만 복구했다. 테스트 생성 TMP fallback atlas 변경은 별도 보관 후 원래 추적 내용으로 복구했다(`test-generated-fallback.asset`). 사용자 `SimplePool.cs` 공백 변경도 유지했다.
- `git diff --check` 통과. 세부 구현 담당2명의 결과를 아키텍처 담당이 diff·수명 경계·실행 결과로 리뷰하고 개인 지침의 진행 상태를 갱신했다.
- 전체 제품·최종 UI/UX·성능 개선량·Player build 검증으로 확대하지 않는다.
- Git stage/commit/push/merge는 이번 요청으로 수행하지 않는다.
