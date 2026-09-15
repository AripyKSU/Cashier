# uGUI 해상도·RectTransform 규격화 준비

- 상태: 조사·계획 초안. 실제 UI/코드/ProjectSettings 변경 전.
- 담당: 성규 / 구조·영향 분석은 설계 담당, 확정 후 제한된 구현은 프로그래머에 배정.
- 기준: 2026-09-15 fetch한 `origin/total_merge 3fbd28f`.
- 작업 브랜치: `codex/ugui-layout-standard`.
- 목표: 기준 해상도에서 배치 의미가 명확하고 다른 작업자가 같은 방식으로 수정·병합할 수 있는 uGUI 작성 규칙을 정한다.
- 이번 범위: 브랜치 생성·게시와 계획 준비. 기존 dirty TMP fallback 폰트·개인 씬·stash는 보존한다.

## 현재 파일에서 확인한 기준

| 대상 | 기준 해상도 | 현재 설정 / 근거 |
|---|---|---|
| GameUI | 1280×720 | `Assets/Prefabs/GameUI/GameUI.prefab`: Scale With Screen Size, Match Width Or Height 0.5 |
| Hub | 1600×900 | `Assets/Scenes/HubScene.unity`: 같은 모드, match 0.5 |
| 엔딩 | 1600×900 | `Assets/Prefabs/Ending/EndingPanel.prefab`: 같은 모드, match 0.5 |
| 로딩 | 1920×1080 | `Assets/Scenes/LoadingScene.unity`: 같은 모드, match 0 |
| 데스크톱 Player 기본 창 | 1920×1080 | `ProjectSettings/ProjectSettings.asset`: native resolution 사용, resizableWindow=0 |
| Web 기본 크기 | 960×600 | 같은 설정 파일. 실제 배포 페이지/브라우저 표시 크기는 미확인 |

이 표는 저장된 값의 정적 조사다. Scene instance override, 중첩 Canvas, 실행 중 변경까지 모두 검증한 목록은 아니다. Dystopia 템플릿에도 1280×720 및 다른 Screen Match 모드가 있지만 본편 사용 여부부터 확인한다.

실행 창 크기와 Canvas의 논리 좌표 기준은 별개다. 1920×1080 창이라는 이유만으로 모든 RectTransform을 해당 크기로 바꾸지 않는다. 기존 계산기 명세도 1280×720 좌표를 사용한다.

## 먼저 결정할 내용

1. 공통 UI 작성 기준: 기존 GameUI의 1280×720을 유지할지, 프로젝트 기본 창과 맞춰 1920×1080으로 이관할지 결정한다. 현재는 어느 쪽도 확정하지 않았다.
2. 지원 화면비: 16:9 고정 구성인지, 16:10·초광폭·창 크기 변경도 지원할지 정한다. 다른 화면비에서 여백/잘림/확장 중 어떤 동작을 요구하는지 명시한다. Web 960×600도 함께 확인한다.
3. 범위: 본편 GameUI부터 시범 적용하고 Hub·Loading·Ending으로 확대하는 안을 검토한다. 이번 준비만으로 전체 화면 변경을 승인한 것으로 해석하지 않는다.

## 규칙 초안

- 최상위 CanvasScaler의 referenceResolution·screenMatchMode·match 값을 공통 규격에 명시한다. 중첩 Canvas는 별도 좌표/정렬 책임이 있는지 확인하고 중복 스케일링을 피한다.
- 전체 화면 가림/배경 패널은 부모를 채우는 stretch와 명시적 여백을 사용한다. 모서리 고정 UI는 해당 앵커와 pivot, 고정 크기·여백을 사용한다. 중앙 패널은 중앙 기준 위치·크기를 사용한다. 모든 객체에 같은 preset을 일괄 적용하지 않는다.
- 고정 앵커의 크기/위치와 stretch의 여백을 구분해 기록한다. Inspector 값은 부모의 논리 좌표로 해석하며 해상도 변경을 개별 자식 scale 조정으로 숨기지 않는다.
- 정적 배치에서는 scale 1을 기본 검토안으로 삼되, 기존 가게 아트 배율·청소기 1.9배·입장/퇴장/흡입 애니메이션·월드 투영은 예외로 분리한다. 연출이 소유하는 Transform 값을 정적 규칙이 덮어쓰지 않는다.
- 기준 해상도를 변경하면 위치·여백·고정 크기·폰트·입력 임계값의 단위를 구분한다. 1280→1920의 1.5배를 모든 수치에 일괄 곱하지 않는다. 앵커 비율·회전·시간·게임 판정 수치는 별개다.
- LayoutGroup/ContentSizeFitter/AspectRatioFitter가 값을 제어하는 객체와 코드가 RectTransform을 갱신하는 객체를 따로 표시한다. 하나의 속성에 여러 작성 주체가 경쟁하지 않도록 한다.
- 확정 규칙은 추후 공용 문서 한 곳에 두고 기존 규칙·기능 명세는 링크로 연결한다. 조사 단계에서 새 레이아웃 프레임워크나 자동 전체 수정 도구를 만들지 않는다.

## 영향 조사 대상

- `SaleSortingPanel`: 정면↔탑뷰 이동, 계산기 화면 밖 출입, 작업대/판매/제외 구역, 정렬 앵커와 포인터 좌표 변환.
- `VacuumController`, 분류 막대, `SaleSortingItemView`, 손 커서: 본체 배율·노즐·흡입/클릭 범위와 좌표 단위.
- `StoreStageVisual.CopyRect`와 단계 프리팹: 단계마다 적용하는 이미지 배치·시계 숫자 앵커. 공용 규칙과 런타임 덮어쓰기 관계.
- `WorldSceneView`: UI 사각형을 기준으로 월드 SpriteRenderer를 투영하는 연결. uGUI 수정으로 배경·손님 정렬이 어긋나는지 확인.
- 지침/가격표/감독관/정산/딸 대화/설비 상점/엔딩: 실제 부모 계층, Canvas 정렬, 글자 영역, 입력 차단, Prefab instance override.

## 진행 순서와 완료 기준

1. 읽기 전용 현황표: Canvas별 실제 사용처, RectTransform 소유자, 중첩 Canvas, 비정상 배율 후보와 예외를 기록한다. 저장된 값과 런타임 값을 구분한다.
2. 기준 합의: 해상도·화면비 정책과 UI 역할별 preset/허용 예외를 확정하고 수정할 파일 allowlist를 정한다.
3. 개인 씬 시범: 기존 공유 결과를 로컬 씬에서 검증한다. MainScene은 작업 중 편집하지 않고 최종 통합 결과만 반영한다. GUID·UnityEvent·직렬화 참조·사용자 미저장 변경을 보존한다.
4. 단계 적용: 연관된 한 화면/입력 흐름씩 변경하고 해당 명세를 갱신한다. 공유 프리팹 변경은 MainScene에도 상속되므로 최종 합의 전 total_merge에 병합하지 않는다.
5. 검증: 관련 API/좌표 변환/수명 검증은 기존 Unity Test Runner를 사용한다. 실제 배치·가독성·마우스 조작감은 사용자가 확인한다. 기준 해상도와 합의된 다른 해상도의 정면/탑뷰·단계1~3·모달·계산기·청소기를 확인한다.

검증 상태: 정적 초기 조사만 완료. Unity 조작·Play·테스트·UI 구현은 이번 준비에서 수행하지 않았다. 작업량은 1차 현황표와 화면비 정책 확정 후 산정한다.

참고: [공통 규칙](../WORK_RULES.md), [리소스 규격](../PREFAB_RESOURCE_RULES.md), [씬 작업](../SCENE_WORKFLOW.md), [판매 화면 배치](../SALE_ITEM_LAYOUT_RULES.md), [현재 가게 외형](sales-window-lighting.md).
