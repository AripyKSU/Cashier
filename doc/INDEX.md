# Cashier 문서 색인

## 읽는 순서

1. 모든 작업은 [WORK_RULES.md](WORK_RULES.md)에서 공통 승인·보존·검증 규칙을 확인한다.
2. 코드 수정이면 [CODING_RULES.md](CODING_RULES.md)를 함께 읽는다.
   파일·폴더·자산 생성/이동 또는 기술 설정 변경도 해당 문서의 [7절](CODING_RULES.md#7-현재-프로젝트-기술-기준)·[8절](CODING_RULES.md#8-프로젝트-구조와-신규-파일-위치)을 확인한다. CSV enum·식별자 변경에는 관련 [9절](CODING_RULES.md#9-cashier-코딩-컨벤션과-아키텍처-경계)도 적용한다.
3. 아래 표에서 이번 작업과 연결되는 문서만 읽는다. 조사 중 영향 범위가 늘어나면 해당 명세를 추가로 확인한다.
4. 담당·승인권 확인에는 [TEAM_ROLES.md](TEAM_ROLES.md), 담당 전환·중단에는 [작업 상태 인계](work/README.md)를 사용한다.

이 색인은 전체 문서를 매번 읽으라는 지시가 아니다. 기능 계약의 본문은 해당 명세 한 곳에서 갱신하고 입구·색인에 복제하지 않는다. 날짜·브랜치·검증 실행이 명시된 과거 기록은 현재 완료 증거와 구분한다. 문서와 현재 구현이 충돌하면 차이와 영향을 보고하며 이번 문서 개편을 이유로 기능 계약을 추측해 바꾸지 않는다.

## 전체 문서와 읽기 조건

기준: 2026-09-11, `total_merge ba368c8` + `inspector-events 29c1ea5` 통합 작업. 기존 문서32개와 최신 UI 병합 보존 가이드1개를 연결한다. 다른 브랜치 전용 문서를 현재 파일처럼 연결하지 않는다.

| 문서 | 읽는 조건 | 내용 |
|---|---|---|
| [WORK_RULES.md](WORK_RULES.md) | 모든 작업에서 필수 | 공통 절차·승인·보호·Git·검증·역할 경계 |
| [CODING_RULES.md](CODING_RULES.md) | 코드 수정에서 필수. 자산·경로·설정 변경은 7·8절, CSV enum·식별자 변경은 관련 9절 | 프로그래머 규칙·기술 기준·폴더·C# 규격 |
| [INDEX.md](INDEX.md) | 작업 시작·범위 변경 | 현재 체크아웃의 문서 색인과 이동 대응 |
| [INSPECTOR_SYSTEM_DRAFT.md](INSPECTOR_SYSTEM_DRAFT.md) | 영업 전 감독관 이벤트 | 구현된 데이터·진행·UI 계약과 과거 검증 |
| [DAUGHTER_DIALOGUE_SYSTEM.md](DAUGHTER_DIALOGUE_SYSTEM.md) | 정산 화면의 딸 대사·날짜별 이미지 | 구현 계약·상태 소유권, 임시 데이터·병합 연결과 검증 결과 |
| [FEATURE_CONTRACT_AUDIT.md](FEATURE_CONTRACT_AUDIT.md) | 담당 기능의 명세·구현 차이 확인 | 손님·설비·도덕성·대기열·감독관 대조와 미해결 항목 |
| [work/inspector-events.md](work/inspector-events.md) | 감독관 작업 재개·통합 | 현재 브랜치·완료·후속 작업 |
| [work/sprite-world-presentation.md](work/sprite-world-presentation.md) | 월드 배경·손님 로컬 개발 | Main 원본 복원·개인 씬 분리·향후 통합 경계 |
| [work/README.md](work/README.md) | 담당 전환·중단 | 도구와 무관한 작업 상태·인계 기준 |
| [AGENT_REQUEST_GUIDE.md](AGENT_REQUEST_GUIDE.md) | 목적만 제시된 자연어 요청 구체화 | 검토 요약→승인→실행 명세 |
| [TEAM_ROLES.md](TEAM_ROLES.md) | 담당·권한·검토자 확인 | 실제 참여자와 복수 역할 명부 |
| [CSV_RULES.md](CSV_RULES.md) | CSV 생성·수정·ID 배정 | 종류 ID 권위 목록·컬럼·문자열·확률 규칙 |
| [DATA_RULES.md](DATA_RULES.md) | CSV·DataTable 작업 | 파싱·검증·PK/FK·신규 CSV 연결 |
| [DATA_CATALOG.md](DATA_CATALOG.md) | 데이터 기획·기존 테이블 조사 | 기획용 데이터 카탈로그 |
| [PREFAB_RESOURCE_RULES.md](PREFAB_RESOURCE_RULES.md) | 리소스·Prefab·Addressables 작업 | 제작·연결·metadata·검증 |
| [SCRIPT_GUIDE.md](SCRIPT_GUIDE.md) | 코드/API 위치 조사 | 스크립트·API 안내 |
| [SCENE_WORKFLOW.md](SCENE_WORKFLOW.md) | 개인 씬·공유 씬·진입 전환 | 개발 씬과 통합 씬 |
| [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md) | MainScene 조립·세션 흐름 | 진행·세션 API 통합 |
| [UI병합_보존_가이드.md](UI병합_보존_가이드.md) | 최신 UI 브랜치와 통합 | 배경·말풍선·착지/쏟기·직렬화 보존 |
| [BRANCH_INTEGRATION_RULES.md](BRANCH_INTEGRATION_RULES.md) | 명시적으로 승인된 브랜치 통합 | Unity 자산 우선순위·병합·검증 |
| [TESTING.md](TESTING.md) | 검증 계획·실행·결과 해석 | 실행 절차와 과거 XML·로그 증거 |
| [CUSTOMER_SYSTEM.md](CUSTOMER_SYSTEM.md) | 손님 기능 전체 탐색 | 손님·상품 시스템 안내 |
| [CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md) | 손님·상품 기능 연결 | MainScene 인계와 사용 계약 |
| [CUSTOMER_SPAWN_INTEGRATION.md](CUSTOMER_SPAWN_INTEGRATION.md) | 손님 구성 선택·생성 | 생성 통합 계약 |
| [CUSTOMER_QUEUE_INTEGRATION.md](CUSTOMER_QUEUE_INTEGRATION.md) | 대기열·이탈·표현 | 대기열 구현·병합 명세 |
| [REPUTATION_CUSTOMER_GENERATOR_HANDOFF.md](REPUTATION_CUSTOMER_GENERATOR_HANDOFF.md) | 명성 기반 손님 생성 | 데이터 인계와 조건 |
| [MORALITY_INTEGRATION.md](MORALITY_INTEGRATION.md) | 거래 도덕성·정산 | 도덕성 거래 연동 |
| [PRICE_EVENT_INTEGRATION.md](PRICE_EVENT_INTEGRATION.md) | 신문·라디오·현재가 이벤트 | 가격 변동 이벤트 계약 |
| [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md) | 설비 구매·해금·가게 단계 | 설비·상품 해금 인계 |
| [FINANCE_SCENE_WORKFLOW.md](FINANCE_SCENE_WORKFLOW.md) | 경제 런타임·FinanceScene | 경제 구성과 작업 흐름 |
| [RESOURCE_POOL_CONTRACT.md](RESOURCE_POOL_CONTRACT.md) | 리소스 비동기 로드·풀 수명 | ResourceManager·Pool 계약 |
| [SALE_ITEM_LAYOUT_RULES.md](SALE_ITEM_LAYOUT_RULES.md) | 판매 상품 위치·레이아웃 | 판매 상품 배치 규칙 |
| [IMAGE_RESOURCE_INTEGRATION.md](IMAGE_RESOURCE_INTEGRATION.md) | 상품·손님 Sprite 연결 | 이미지 FK·migration·연결 |
| [DYSTOPIA_RESOURCE_INTEGRATION.md](DYSTOPIA_RESOURCE_INTEGRATION.md) | Dystopia 원본·선택 이관 | 리소스 이관 기록과 범위 |
| [DEV3_GUIDE.md](DEV3_GUIDE.md) | 기존 UI·입력 구현 경위 조사 | 개발자 3 UI 가이드·로드맵 |

### 데이터 자료 목록

다음 CSV는 문서의 등록·감사·이관 자료이며 런타임 테이블로 새로 등록하지 않는다. 관련 리소스 조사·이관 시 해당 명세와 함께 확인한다.

| 자료 | 읽는 조건 |
|---|---|
| [data/IMAGE_RESOURCE_REGISTRATION.csv](data/IMAGE_RESOURCE_REGISTRATION.csv) | 상품·손님 이미지 등록 내역 대조 |
| [data/DYSTOPIA_SOURCE_AUDIT.csv](data/DYSTOPIA_SOURCE_AUDIT.csv) | Dystopia 원본 감사 내역 확인 |
| [data/DYSTOPIA_RESOURCE_MAP.csv](data/DYSTOPIA_RESOURCE_MAP.csv) | Dystopia 원본과 이관 리소스 대응 확인 |

## 원문 이동 대응

기존 `AGENTS.md`의 절 번호·항목 순서를 보존했다. 절 번호가 연속하지 않는 것은 이동 대조를 위한 의도적인 구성이다. 변경은 문서 위치에 따른 링크 경로와 코드 규칙의 승인 절 상호 참조 정정에 한정한다.

| 기존 AGENTS.md 절 | 현재 단일 본문 |
|---|---|
| 1. 우선순위 | [WORK_RULES.md 1절](WORK_RULES.md#1-우선순위) |
| 2. 공통 작업 절차 | [WORK_RULES.md 2절](WORK_RULES.md#2-공통-작업-절차) |
| 3. 프로그래머 규칙 | [CODING_RULES.md 3절](CODING_RULES.md#3-프로그래머-규칙) |
| 4. 리소스 역할 규칙 | [WORK_RULES.md 4절](WORK_RULES.md#4-리소스-역할-규칙) |
| 5. Git과 안전 | [WORK_RULES.md 5절](WORK_RULES.md#5-git과-안전) |
| 6. 완료 확인 | [WORK_RULES.md 6절](WORK_RULES.md#6-완료-확인) |
| 7. 현재 프로젝트 기술 기준 | [CODING_RULES.md 7절](CODING_RULES.md#7-현재-프로젝트-기술-기준) |
| 8. 프로젝트 구조와 신규 파일 위치 | [CODING_RULES.md 8절](CODING_RULES.md#8-프로젝트-구조와-신규-파일-위치) |
| 9. Cashier 코딩 컨벤션과 아키텍처 경계 | [CODING_RULES.md 9절](CODING_RULES.md#9-cashier-코딩-컨벤션과-아키텍처-경계) |
| 10. 검증 절차와 실행 시점 | [WORK_RULES.md 10절](WORK_RULES.md#10-검증-절차와-실행-시점) |
| 11. 보류 항목 | [WORK_RULES.md 11절](WORK_RULES.md#11-보류-항목) |
| 12. 팀 분업과 소유권 경계 | [WORK_RULES.md 12절](WORK_RULES.md#12-팀-분업과-소유권-경계) |

기존 적용 대상 설명은 WORK_RULES.md 서두에 유지했다. 프로젝트 규칙의 우선순위·보호 변경·승인·5단계 검증 상태를 입구 요약으로 대체하지 않는다.

## 도구별 입구

Codex 입구 형식은 [공식 AGENTS.md 안내](https://learn.chatgpt.com/docs/agent-configuration/agents-md)를 참고한다.

| 도구 | 입구 | 공통 본문 연결 |
|---|---|---|
| Codex | [AGENTS.md](../AGENTS.md) | 작업 전 WORK_RULES 파일 읽기, INDEX로 조건별 문서 선택 |
| Claude Code | [CLAUDE.md](../CLAUDE.md) | 동일한 파일 읽기 지침 |
| Antigravity | [.agents/rules/project.md](../.agents/rules/project.md) | `trigger: always_on`, 규칙 파일 기준 `../../doc/` 상대 링크 |

Claude의 [공식 메모리 문서](https://code.claude.com/docs/en/memory)는 파일 import를 지원한다. Antigravity의 [공식 Rules/Workflows 문서](https://antigravity.google/docs/rules-workflows)는 workspace 규칙·Always On·상대 파일 참조와 규칙 파일 크기 제한을 설명한다. 이번 구성은 상세 본문 전체를 입구에 복제하거나 import 확장하지 않고, 작업 전에 공통 파일을 읽으라는 동일한 지침을 사용한다.

설계 담당은 설치된 Antigravity 확장의 rule parser에서 `always_on` 기본값과 Always On 옵션을 확인했다. 이는 설정 형식 확인이며, 세 클라이언트의 **새 대화에서 자동으로 입구를 읽고 본문까지 적용하는 동작은 아직 검증하지 않았다**. 도구가 입구를 자동으로 읽지 않았다면 사용자가 해당 입구 파일을 명시적으로 지정한다. 특정 도구의 세션 ID·모델 배분·개인 지침은 공통 규칙에 넣지 않는다.

## 문서 이동 검증 기록 (d8ea9ef 작성 시점)

- 2026-09-11 기준 원문은 `d13d2e25a46c0030bdfea9ae58cd00315ff3790b:AGENTS.md`다. 위 대응표로 12개 절을 대조했으며, 링크 경로와 두 곳의 12절 참조 표기를 정규화한 후 12/12절이 일치한다. 비제목·비공백 본문 266줄은 누락 없이 보존했다.
- 기존 doc Markdown 25개, 신규 공통 Markdown 4개와 doc/data CSV 3개를 모두 이 색인에서 연결했다. 자료 자체의 값이나 기능 계약은 수정하지 않았다.
- 수정 파일: `AGENTS.md`, `doc/AGENT_REQUEST_GUIDE.md`, `doc/BRANCH_INTEGRATION_RULES.md`, `doc/CSV_RULES.md`, `doc/CUSTOMER_INTEGRATION.md`, `doc/DATA_RULES.md`, `doc/FACILITY_INTEGRATION.md`, `doc/PREFAB_RESOURCE_RULES.md`, `doc/RESOURCE_POOL_CONTRACT.md`, `doc/SCENE_WORKFLOW.md`, `doc/TEAM_ROLES.md`, `doc/SCRIPT_GUIDE.md`, `doc/CUSTOMER_SYSTEM.md`.
- 신규 파일: `CLAUDE.md`, `.agents/rules/project.md`, `doc/WORK_RULES.md`, `doc/CODING_RULES.md`, `doc/INDEX.md`, `doc/work/README.md`.
- 재현: `git show d13d2e25a46c0030bdfea9ae58cd00315ff3790b:AGENTS.md`와 두 규칙 문서를 절 단위로 비교한다. Markdown 상대 링크의 파일 존재·제목 앵커와 `git diff --check`도 확인한다. 이번 로컬 링크 검사에서 기존·신규 깨진 참조는 없었다. 검사 코드는 Temp에만 두었으며 영구 검사 도구를 추가하지 않았다.
- 문서만 변경했다. Unity 조작·테스트와 Git stage·commit·push·merge는 수행하지 않았고 기존 stash를 보존했다. 도구 자동 적용 검증 한계는 위 절과 같다.
