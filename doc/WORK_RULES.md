# 프로젝트 공통 작업 규칙

이 문서는 이 저장소에서 코드·데이터·테스트·빌드 설정 또는 이미지·애니메이션·오디오·프리팹·번들 리소스를 다루는 사람과 AI 작업자에게 적용한다.

모든 작업에서 먼저 읽는다. [CODING_RULES.md](CODING_RULES.md)는 같은 프로젝트 규칙의 프로그래머·기술 부분이며 코드 수정 시 함께 읽는다. 필요한 기능 문서는 [INDEX.md](INDEX.md)에서 선택한다. 원문 대응을 위해 기존 AGENTS.md의 절 번호를 유지한다.

파일·폴더·자산 생성/이동 또는 기술 설정 변경에는 CODING_RULES.md의 [7절](CODING_RULES.md#7-현재-프로젝트-기술-기준)과 [8절](CODING_RULES.md#8-프로젝트-구조와-신규-파일-위치)을, CSV enum·식별자 변경에는 관련 [9절](CODING_RULES.md#9-cashier-코딩-컨벤션과-아키텍처-경계)을 함께 확인한다.

## 1. 우선순위

1. 사용자 또는 책임자의 최신 명시적 지시
2. 이 문서와 경로별 작업자 규칙
3. 승인된 기획·아키텍처·아트·데이터·스타일 명세
4. 개인 관행

규칙이 충돌하거나 선택에 따라 결과가 크게 달라지면 임의로 결정하지 말고 차이와 대안을 보고한다.

## 2. 공통 작업 절차

- 시작 전에 목표, 허용·금지 범위, 완료 조건과 필요한 선행 자료를 확인한다.
- 현재 브랜치, 작업 디렉터리, dirty·untracked 파일과 stash를 확인하고 무관한 변경을 보존한다.
- 기존 코드, 자산, 공용 시스템, 표준 기능과 설치된 의존성을 먼저 찾는다.
- 같은 효과라면 수정 파일과 구현이 가장 적은 해법을 선택한다.
- 요구하지 않은 리팩터링, 추상화, 설정, 의존성, 임시 시스템을 추가하지 않는다.
- 문서나 테스트가 제품 사양과 다르면 제품을 억지로 맞추지 말고 차이를 보고한다.
- 정적 검사, import·컴파일 성공, Editor 표시, 자동 테스트, 실제 런타임 검증을 구분해 보고한다.
- 임시 코드, placeholder, debug 설정은 영향과 제거 조건을 기록한다.

### 프로젝트 기준 문서

- 사용자가 "문서"에서 파일을 읽거나 찾으라고 요청하면 [문서 - 퍼즐게임](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.0)의 하위 문서에서 요청에 해당하는 문서를 찾아 확인한다.
- 사용자가 개발 체크리스트를 언급하거나 개발 작업의 범위를 확인·추가하라고 요청하면 [개발 체크리스트](https://docs.google.com/spreadsheets/d/1PAsKzteDN3awiqwOH331VnG9f0Ljog7MJ6owkTJPNhE/edit?gid=0#gid=0)를 기준으로 작업한다.
- 목적 중심의 자연어 요청을 실행 프롬프트로 구체화할 때는 [`doc/AGENT_REQUEST_GUIDE.md`](AGENT_REQUEST_GUIDE.md)를 따른다.
- Prefab, Addressables, `.meta`와 연관 리소스 작업은 [`doc/PREFAB_RESOURCE_RULES.md`](PREFAB_RESOURCE_RULES.md)를 따른다.
- CSV와 DataTable 작업은 [`doc/DATA_RULES.md`](DATA_RULES.md)를 따른다.
- CSV 종류 ID와 문자열 허용 경계는 [`doc/CSV_RULES.md`](CSV_RULES.md)를 따른다. 표시 이름은 `nameidx`로 연결한다.

### 작업 요청과 지정

작업 요청에는 다음 항목을 가능한 범위에서 명시한다.

- 작업 목적과 대상 기능
- 담당자 또는 AI 작업자가 권한을 대행하는 참여자
- 적용 역할과 권한 수준
- 허용된 파일·폴더와 생성·수정·이동·삭제 범위
- 보호 변경 포함 여부와 필요한 검토자
- 완료 조건과 컴파일·실행 검증 방법

Git name과 email은 명부 조회 키일 뿐 권한 위임이나 승인 증거가 아니다. 필수 정보가 빠졌더라도 안전하게 분리 가능한 일반 작업은 진행하고, 권한이 불명확한 보호 변경만 `BLOCKED`로 분리한다.

## 4. 리소스 역할 규칙

리소스 역할은 승인된 기획과 기술 명세를 프로젝트 자산으로 제작·가공·연결해 인계한다. 프로그래머 역할이 소유한 게임 로직과 판정·타이밍은 임의로 변경하거나 이미지 frame에 고정하지 않는다.

### 공통 원칙

- 작업 전에 대상 리소스, 사용 위치, 허용 범위, 완료 조건과 필요한 선행 자료를 확인한다.
- 기존 자산과 공용 시스템을 먼저 검색하고 명세를 충족하는 가장 작은 변경을 선택한다.
- 원본, 라이선스, 출처와 수정 가능 범위를 확인한다.
- 기존 폴더와 이름 규칙을 따르고 원본, 작업 파일과 최종 export를 구분한다.
- 준비되지 않은 참조를 임의 ID, 임시 문자열 또는 placeholder로 활성화하지 않는다.
- 사용하지 않는 임시·중복 자산도 승인 없이 삭제하지 않는다.

### 기술 규격

- CSV와 DataTable 작업은 [`doc/DATA_RULES.md`](DATA_RULES.md)를 따른다.
- Prefab, Addressables, `.meta`, Texture와 Animation 작업은 [`doc/PREFAB_RESOURCE_RULES.md`](PREFAB_RESOURCE_RULES.md)를 따른다.

### 인계

다음을 보고한다.

- 제작·수정한 파일과 경로
- 원본과 라이선스 정보
- prefab·animator·data·address 연결 관계
- PK·FK·GUID·중복 검사 결과
- 컴파일·Console·runtime 검증 상태
- placeholder, 미완성, 사용자 확인 대기 항목과 downstream의 다음 단계

## 5. Git과 안전

- 승인된 경로만 `git add -- <paths>`로 stage하고 `git add .` 같은 광범위 stage를 하지 않는다.
- commit 전 `git diff --cached --check`, name-status, stat와 staged count를 확인한다.
- 생성 자산은 본 파일과 metadata·mapping 파일의 짝을 확인한다.
- checkpoint는 로컬 commit이다. push·pull·merge·브랜치 정리는 별도 승인 후 수행한다.
- `reset --hard`, `clean`, 강제 push, 임의 stash와 광범위 포맷팅을 승인 없이 수행하지 않는다.
- 프로세스·시스템 종료, 삭제, 배포, 외부 메시지·업로드처럼 복구가 어렵거나 외부에 영향을 주는 작업은 명시적 권한을 받는다.
- 재귀 삭제·이동 전 절대 경로와 작업 영역 포함 여부를 확인한다.
- 비밀값을 명령 출력, 로그, 문서와 commit에 노출하지 않는다.

## 6. 완료 확인

- 요청된 기능·데이터·리소스가 실제로 반영됐는가?
- 허용 범위 밖 파일이나 무관한 설정을 수정하지 않았는가?
- 기존 공용 구현과 자산을 불필요하게 중복하지 않았는가?
- 데이터 권위, 참조, 오류·취소·정리 경로가 안전한가?
- 임시 코드·자산·debug 설정의 제거 또는 유지 결정이 기록됐는가?
- 검증하지 않은 항목을 PASS로 표현하지 않았는가?
- Git 변경, commit, push, merge 여부와 남은 위험·후속 승인을 구분했는가?

## 10. 검증 절차와 실행 시점

### A. 작업 전

- 현재 branch, dirty·untracked·stash와 작업 허용 파일을 확인한다.
- 관련 호출자, prefab, Scene, data, Addressables 소비자를 확인한다.
- 기준 Console 오류가 있으면 작업 전 오류와 작업 후 오류를 구분할 수 있게 기록한다.

### B. 파일 수정 직후

- C# 수정: Unity reimport와 compilation 종료까지 기다린 뒤 compile error 0을 확인한다.
- prefab·Scene·asset 수정: 저장 후 missing script, missing reference, GUID·`.meta` 짝을 확인한다.
- CSV·데이터 수정: header, PK, FK, parser·loader routing과 Addressables 연결을 확인한다.
- package·render 설정 수정: 설정 파일 diff와 영향을 받는 platform·Scene을 확인한다.

### C. 컴파일과 최소 실행 검증

- API·CSV/parser·생성·큐·거래·금액·지침·이벤트 계약은 설치된 Unity Test Framework의 NUnit EditMode로 검증한다. 실제 Unity 수명·비동기·ResourceManager·pool API는 필요한 경우에만 PlayMode로 검증한다.
- Unity reimport와 compilation 종료 후 compile error 0을 확인한다.
- 관련 기존 테스트와 변경 경계를 검증하고 실행 개수·실패·skip·미완료를 보고한다. total=0, skip 또는 미완료를 PASS로 판단하지 않는다. 실행과 XML·로그 보관은 [`doc/TESTING.md`](TESTING.md)를 따른다.
- UI 버튼·문구·배치·사용감은 사용자 수동 확인 대상이다. API 테스트 성공을 화면·UX 성공으로 확대하지 않고 사용자 확인 전까지 미확인으로 보고한다. 자동 검사를 위해 개인 씬이나 공유 Scene을 변경하지 않는다.
- 검증 환경이 없다는 이유로 신규 framework나 package를 추가하지 않는다. 승인된 테스트 assembly와 기존 설치 의존성을 재사용한다.

### D. Console과 런타임

- compile 이후 Console의 compile error와 제품 runtime error를 별도로 확인한다.
- warning을 무조건 삭제하지 말고 신규 warning인지와 제품 영향 여부를 분류한다.
- 사용자 입력, UI, Scene 전환, 물리, animation처럼 체감 동작은 실제 PlayMode 또는 사용자 확인 전까지 `PENDING`으로 남긴다.
- 정적 검사나 prefab 존재만으로 runtime 성공을 주장하지 않는다.

### E. 완료 전

- 변경 파일 allowlist와 실제 diff가 일치하는지 확인한다.
- 임시 route, debug flag, placeholder, test fixture를 제거하거나 유지 결정을 기록한다.
- `git diff --check`를 통과시키고 commit·push·merge 여부를 각각 구분해 보고한다.

검증 상태는 다음 다섯 가지로만 표현한다.

- `PASS`: compile error가 없고 요청된 최소 실행 검증까지 완료
- `STATIC PASS`: 구조·참조·컴파일은 통과했으나 실행 검증 미실행
- `PARTIAL`: 유효한 결과와 미완료 항목이 함께 존재
- `BLOCKED`: 필수 환경·자료·권한 부족으로 안전하게 진행 불가
- `FAIL`: compile error, 실행 오류 또는 필수 데이터·참조 실패가 확인됨

## 11. 보류 항목

다음 항목은 선행 기준이 확정될 때까지 현재 규칙으로 강제하지 않는다. 조건이 충족되면 승인된 별도 작업으로 이 문서에 반영한다.

1. **세부 시스템·경로 승인권과 문서 리뷰 책임자**: 기능별 소유 범위가 확정된 뒤 생성·수정·이동·삭제·승인 권한과 문서 승인·갱신 책임자를 지정한다.
2. **브랜치·빌드·배포 전략**: CI와 release 흐름이 확정된 뒤 branch, merge, build, release 절차를 정의한다.
3. **자동 테스트 정량 기준**: 자체 test assembly와 CI 기준선이 마련된 뒤 coverage 또는 필수 scenario 기준을 정한다.

## 12. 팀 분업과 소유권 경계

작업자는 프로그래머, 기획, 아트, 리소스 역할을 하나 이상 가질 수 있다. 역할은 누적 적용하며, 실제 수정 권한은 역할 이름만으로 판단하지 않고 현재 작업의 허용 경로와 작업 종류를 함께 확인한다.

| 역할 | 기본 소유 범위 | 다른 역할과 협의가 필요한 경계 |
|---|---|---|
| 프로그래머 | gameplay 코드, manager, 공용 API, async·수명과 기술 설정 | 기획 수치, 원본 art, resource import·연결 변경 |
| 기획 | 규칙, 밸런스, 데이터 의미와 acceptance | loader·runtime 구조, 원본 art와 import pipeline 변경 |
| 아트 | 원본 image, animation, audio와 시각 기준 | gameplay 로직, 데이터 ID, prefab·Addressables 연결 변경 |
| 리소스 | asset 가공, import, prefab·data·Addressables 연결 | gameplay 판정·타이밍, 기획 의미와 원본 art 변경 |

역할 수준은 다음과 같이 해석한다.

- `Primary`: 해당 역할 규칙을 주 책임으로 적용하고 관련 변경의 기본 검토 책임을 가진다.
- `Shared`: 해당 역할의 공용 규칙을 함께 소유하고 현재 작업에서 허용된 범위 안에서 수행·검토한다.
- 역할 수준은 package, ProjectSettings, 공용 manager, 원본 삭제, Git 통합 같은 보호 작업의 포괄 승인을 의미하지 않는다.

현재 프로그래머 `Primary`는 단일 대표를 두지 않는다.

- 역할별 승인 범위는 권위 규칙으로 유지하되, 다른 작업과 분리된 작업 branch에서는 사전 허가보다 기본 branch 통합 전 검토 게이트로 적용한다.
- 프로그래머 `Primary`는 작업 branch에서 공용 manager, 공용 API와 아키텍처 변경을 구현·검증하고 local commit할 수 있다.
- 해당 변경을 기본 branch에 반영하기 전 작업자 외 프로그래머 `Primary` 1명 이상의 코드 리뷰를 받고, 리뷰에 참여한 프로그래머끼리 변경 반영에 명시적으로 동의해야 한다.
- 명시적 승인과 동의는 PR 댓글 또는 현재 작업 명세에 기록한다. 구두 합의나 Git 작성자 정보만으로 승인된 것으로 판단하지 않는다.
- 작업자와 승인권자가 같은 보호 변경은 자기 승인만으로 완료하지 않는다. 관련 역할의 다른 `Primary` 1명이 교차 검토하고, 다른 `Primary`가 없으면 해당 역할의 `Shared` 1명이 검토한다. 검토자를 지정할 수 없으면 프로젝트 책임자가 판단한다.
- 프로젝트 책임자가 직접 수행한 보호 변경도 가능한 관련 역할 `Primary`의 교차 검토를 받는다.
- 리뷰 참여자 간 동의가 결렬되면 이견과 영향을 기록하고 프로젝트 책임자가 최종 반영 여부를 결정한다. 단, 필수 검증 실패나 데이터 손실·보안 위험은 책임자 판단만으로 면제할 수 없다.
- 배정된 기능과 허용 경로 안의 일반 변경은 담당자 1인이 작업·commit하고 통합할 수 있다.
- package, ProjectSettings, Addressables group·address, 원본 자산 삭제처럼 영향이 크거나 복구 비용이 높은 변경은 branch 분리 여부와 관계없이 작업 전에 승인을 받는다.
- 별도 작업 branch를 사용하지 않고 기본 branch에서 직접 작업하면 공용·보호 변경은 commit 전에 같은 리뷰와 동의 절차를 적용한다.
- PM, QA와 문서 작업자는 현재 독립 역할로 지정하지 않는다. 요구사항 승인, 검증과 문서 갱신은 작업별로 명시된 담당자가 수행한다.

실제 참여자와 복수 역할의 단일 권위는 [`doc/TEAM_ROLES.md`](TEAM_ROLES.md)다. Git name과 email은 명부 조회 키일 뿐 권한 인증 수단이 아니다. 명부와 현재 identity가 일치하지 않거나 경로·작업 종류가 명시되지 않았으면 권한을 추측하지 않는다.

### 작업 배정 규칙

- 새 파일을 만들기 전에 해당 경로와 시스템의 담당자를 확인한다.
- 담당자가 불명확하면 사용자 또는 프로젝트 책임자가 소유자를 지정하기 전까지 공용 manager, package, render 설정을 수정하지 않는다.
- 다른 담당자의 파일이 필요하면 직접 넓혀 수정하지 않고 변경 목적, 필요한 API, 허용 파일을 인계한다.
- 공용 파일은 소비자와 영향 범위를 조사하고 해당 역할의 `Primary` 또는 프로젝트 책임자 승인을 받는다.
- 역할별 선행 결과가 필요한 작업은 의존 순서대로 진행한다.
- 병렬 작업은 수정 파일이 겹치지 않고 독립적으로 검증 가능한 경우에만 허용한다.
- 병합 직전에 최신 기본 branch를 기준으로 CSV ID, Addressables address, GUID와 공용 직렬화 파일의 중복·충돌을 다시 확인한다. branch 분리만으로 승인이나 충돌 검사를 생략하지 않는다.
- 완료 보고에는 변경 파일, 검증 결과, 소유 경계 밖에서 보류한 항목, 다음 담당자를 명시한다.

### 병합 인계와 문서 정리

- 작업 브랜치에서 커밋·푸시하기 전에 AI 에이전트는 병합에 필요한 변경 사항이 기록되어 있는지 확인한다.
- 기록이 없으면 작업자에게 작성을 요청한다. AI가 변경 내용을 파악한 경우 권장 초안을 먼저 제시하고, 미확정 사항만 작업자에게 확인한다.
- 기록 위치는 커밋 메시지 본문, PR 설명 또는 저장소 내 병합 인계 문서로 한다. 간단한 변경에는 별도 문서를 만들지 않는다.
- 기록에는 변경 목적, 영향 범위, API·CSV·직렬화·리소스 참조 변경, 선행 작업과 적용 순서, 검증 결과, 미완료 사항을 필요한 만큼 포함한다.
- total_merge 통합 전 원격 브랜치를 최신화하고, 각 작업의 병합 기록을 실제 diff와 대조한다. 충돌 해결과 통합 검증 시 해당 기록을 참고한다.
- 기록 누락만으로 안전한 작업 전체를 중단하지 않는다. 결과에 영향을 주는 미확정 계약이나 충돌은 담당자에게 확인한다.
- 통합 검증과 최종 승인이 완료되면, 병합 전용 임시 인계 문서 중 필요한 내용이 영구 명세·PR 설명에 반영된 문서만 삭제한다.
- API·데이터·아키텍처·사용법 명세와 검증 기록은 삭제하지 않고 최신화한다. 다른 미완료 작업이 참조하는 문서는 유지한다.
- 문서 정리는 최종 커밋·푸시 전에 수행한다. 이미 푸시한 경우에는 이력을 재작성하지 않고 별도 정리 커밋으로 반영한다.
