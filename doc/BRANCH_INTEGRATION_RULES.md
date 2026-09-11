# Unity 브랜치 최종 통합 규칙

## 적용과 우선순위

사용자가 이 문서를 참조해 병합을 요청하면 지정한 브랜치와 범위에 적용한다. 문서 참조만으로 Git 변경·push를 수행하지 않는다. 최신 사용자 지시와 [WORK_RULES.md](WORK_RULES.md)가 우선하며, 이 규칙은 보호 변경 승인·교차 리뷰·검증을 면제하지 않는다.

핵심 기준은 **화면·자산은 요청 순서 우선, 로직은 기능별 최신 유효 구현 우선**이다. 이는 통합 결과의 기준이지 Git 명령 실행 순서나 파일 전체 덮어쓰기 규칙이 아니다.

## 요청 시 지정할 정보

- 입력 브랜치 또는 ref를 우선순위 순서로 나열한다. 첫 번째가 자산 기준이다. 채팅에서 우연히 먼저 언급된 이름으로 순서를 추측하지 않는다.
- 결과를 반영할 대상 브랜치와 기준 Scene 경로를 지정한다. 브랜치 병합과 MainScene 콘텐츠 조립 중 요청 범위를 명시한다.
- 사용할 최신 기준을 지정한다: fetch 후 원격 tip 또는 특정 commit. 미지정 시 변경 전에 제안하고 확인한다.
- 포함 기능·제외 파일·담당자·검증 범위, commit·push·기본 branch 반영 허용 여부를 지정한다.
- 결과에 영향을 주는 필수 정보가 없으면 읽기 전용 조사까지만 하고 확인한다. 일반적인 병합 요청이 강제 push·삭제·stash를 허용하지는 않는다.

## 대상별 선택 기준

| 대상 | 통합 규칙 |
|---|---|
| uGUI, Prefab, Scene | 첫 입력 브랜치의 계층·배치·기존 직렬화 연결을 기준으로 보존한다. 같은 자산이 첫 브랜치에 없으면 해당 자산이 존재하는 입력 중 가장 앞선 브랜치를 기준으로 한다. |
| API, 클래스, 시스템 | 공통 조상과 변경 이력·호출자를 비교해 최신 유효 기능을 통합한다. commit 날짜만으로 승자를 정하지 않는다. 분기된 구현은 어느 쪽이 더 최신인지 단정하지 않는다. |
| CSV, enum, DTO, parser, loader | 일관된 데이터 계약 단위로 통합한다. 파일별 최신 버전 조합 금지. PK·FK·종류 ID·소비자와 migration을 함께 확인한다. |
| `.meta`, GUID, fileID | 기준 자산의 식별자와 참조를 보존한다. 같은 경로의 다른 GUID 또는 다른 경로의 같은 GUID는 충돌로 조사한다. 임의 재생성·치환 금지. |
| Package, ProjectSettings, Addressables | 최신 파일 자동 선택 금지. 영향·호환성·사전 승인 범위를 별도로 검토한다. |

### 자산 기준의 의미

- 이후 브랜치의 필요한 버튼·component·자산은 기준 계층에 추가·연결한다. 첫 브랜치 우선이라는 이유로 다른 승인 기능을 누락하지 않는다.
- 삭제·이름 변경도 의도된 변경인지 확인한다. 오래된 자산이나 폐기된 기능을 무조건 복원하지 않는다.
- Prefab 원본, variant, nested prefab, Scene instance override를 구분한다. override를 원본에 무조건 적용하거나 prefab을 임의 unpack하지 않는다.
- UI component의 script 구현은 로직 기준으로 검토하되 RectTransform, Canvas 설정, Inspector 참조, UnityEvent 연결은 기준 자산과 함께 검증한다.
- 자산의 기획 수치·동작 설정이 최신 로직과 충돌하면 배치 우선 규칙만으로 선택하지 않는다. 차이와 대안을 보고한다.
- YAML의 `ours`/`theirs` 일괄 선택을 의미하지 않는다. Git 충돌이 없어도 의미·참조 충돌을 검사한다.

### 코드와 직렬화의 호환

- field 이름·타입, component 교체, script GUID, UnityEvent method·인자 변경을 추적한다. 컴파일 성공만으로 Inspector 연결 보존을 인정하지 않는다.
- 최신 API에 맞춰 실제 호출자와 직렬화 데이터를 함께 이관한다. 단순 rename 호환과 타입·의미 변경 migration을 구분한다.
- 같은 기능의 manager·singleton·event 구독·상태 소유자가 중복되지 않게 한다. 임시 wrapper나 병렬 구버전 구현으로 충돌을 숨기지 않는다.
- 양립할 수 없는 기획·기능은 임의 선택하지 않는다. 영향 파일, 두 동작의 차이, 권장 선택을 보고하고 해당 통합을 보류한다.

## 실행 절차

1. 현재 checkout, dirty·untracked·stash와 Unity의 미저장 Scene·Prefab 상태를 확인한다. 사용자 작업을 덮어쓰거나 임의 stash하지 않는다. 필요하면 별도 통합 worktree를 제안한다.
2. 승인된 경우 fetch하고 입력 ref·대상 ref를 commit SHA로 고정해 기록한다. 실행 중 새 원격 commit이 생겨도 조용히 기준을 바꾸지 않는다.
3. 공통 조상 대비 변경과 기능별 의존성을 조사한다. 자산 기준 branch, 채택할 로직, 데이터 이관, 충돌·보호 변경을 간결한 통합 계획으로 제시한다. 미승인 선택만 확인받는다.
4. 의존 순서대로 데이터 계약·코드 호환성을 맞추고 기준 자산에 기능을 연결한다. 자산 우선순위가 편집 순서를 강제하지는 않는다. Scene·Prefab 편집은 Unity Editor를 기본으로 한다.
5. [SCENE_WORKFLOW.md](SCENE_WORKFLOW.md)에 따라 MainScene에 검증된 공유 결과물을 조립한다. `Assets/Scenes/Local/`과 개인 설정은 Git·Build Settings·Addressables에 포함하지 않는다.
6. 아래 검증 후 승인된 경로만 stage한다. commit·push는 요청된 범위에서만 수행한다. 검증 실패를 숨기는 완료·배포는 금지하며 미완성 checkpoint는 명시적 요청과 상태 표시가 필요하다.

원본 branch 수정·삭제, reset/clean, 강제 push는 수행하지 않는다. Git merge, 선택적 이관 등 방식에 따라 원본 이력이 보존되는지 설명한다. squash·rebase처럼 이력을 바꾸는 방식을 임의 선택하지 않는다.

## 최소 검증과 보고

- 정적 검사: conflict marker, 중복 type/API, CSV header·PK·FK·routing, asset/meta 짝, GUID·address 중복과 변경 범위.
- Unity import·컴파일: 정확한 통합 checkout을 연 Editor에서 compile error 0, 직렬화 오류·missing script·missing reference 확인.
- uGUI: 기준 배치·anchor·scale·정렬, Canvas·EventSystem·raycast, 버튼 및 입력 callback, prefab override 보존 확인.
- 실행: InitScene부터 HubScene을 거쳐 MainScene 진입, manager 중복 없음, 요청 기능들의 연계·실제 입력·결과 처리·재진입 확인. 개인 씬 검사만으로 대체하지 않는다.
- Console: 기존 오류·신규 제품 오류·의도된 실패 검사를 분리한다. Player build는 요청 시 별도 검증하며 Editor 성공으로 대체하지 않는다.
- 기존 [DATA_RULES.md](DATA_RULES.md), [CSV_RULES.md](CSV_RULES.md), [PREFAB_RESOURCE_RULES.md](PREFAB_RESOURCE_RULES.md)의 상세 검사를 재사용한다. 새 test framework를 임의 도입하지 않는다.
- 보고: 입력/대상 SHA, 자산 기준, 기능별 채택·제외·이관 내역, 변경 파일, 미해결 충돌, 검증 근거와 한계, commit·push 여부. 상태는 [WORK_RULES.md 10절](WORK_RULES.md#10-검증-절차와-실행-시점)의 PASS / STATIC PASS / PARTIAL / BLOCKED / FAIL을 따른다.

## 요청 템플릿

```text
doc/BRANCH_INTEGRATION_RULES.md를 기준으로 통합해줘.

입력 ref (자산 우선순위순): <첫 번째>, <두 번째>, <세 번째>
최신 기준: origin fetch 후 각 원격 ref의 tip
대상 branch: <기존 branch 또는 생성할 branch>
기준 Scene: Assets/Scenes/MainScene.unity
범위: <기능 목록>의 Git 통합 및 MainScene 조립
제외: <제외 기능·파일>
담당자 / 보호 변경 승인 범위: <지정>
검증: 컴파일, 참조 검사, Init부터 MainScene 및 <기능 시나리오>
Git 완료 범위: <로컬 작업만 / commit까지 / commit·push까지>

필수 선택이 충돌하면 해당 부분만 보류하고 차이와 권장안을 알려줘.
```

이 문서는 실행 기준만 정의한다. 특정 브랜치의 통합·검증이 완료됐다는 증거는 아니다.
