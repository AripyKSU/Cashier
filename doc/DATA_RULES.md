# CSV와 DataTable 작업 규격

이 문서는 CSV 데이터의 생성·수정, 검증과 runtime 연결 기준을 정의한다. 공통 권한과 승인 절차는 [`AGENTS.md`](../AGENTS.md), 역할 명부는 [`TEAM_ROLES.md`](TEAM_ROLES.md)를 따른다.

## 기존 구현 우선

- 설치된 CsvHelper와 기존 `DataTableManager`, `ResourceData.csv` 및 관련 DataTable 구현을 우선 참고한다.
- 기존 parser, loader와 manager 경로를 재사용하고 같은 목적의 데이터 framework를 새로 만들지 않는다.
- 기존 CSV의 encoding, delimiter, header 이름과 column 순서를 유지한다.
- quote, newline과 특수문자는 CsvHelper를 통해 처리하고 기존 loader의 Culture와 변환 규칙을 따른다.

## Schema와 오류 처리

- CSV별 필수 header, 자료형, 필수값, 기본값과 허용값을 확인한다.
- 필수 header·값 누락이나 자료형 변환 실패 시 `Debug.LogError`에 파일, 행, column과 원인을 출력하고 예외를 발생시킨다.
- 오류가 있는 데이터는 정상 반영하거나 임의 기본값으로 대체하지 않는다.
- 전체 데이터 검증이 끝난 뒤 실제 runtime 상태를 교체한다.

## PK와 ID

- 테이블별 승인된 ID 범위 또는 예약표를 사용한다.
- 신규 ID를 할당하기 전에 기존 값과 현재 예약을 확인한다.
- 승인된 범위가 없으면 ID를 임의로 할당하지 않고 `BLOCKED`로 보고한다.
- 병합 직전에 최신 기본 branch를 기준으로 중복을 다시 확인한다.

## FK

- FK가 참조하는 table, column과 대상 asset 또는 address를 확인한다.
- 참조 실패 시 `Debug.LogError`에 원본 table·행·FK 값과 대상 table을 기록하고 예외를 발생시킨다.
- FK 검증에 실패한 행은 활성 데이터로 등록하지 않는다.

## 신규 CSV 연결

신규 CSV는 다음 경로를 모두 확인한다.

1. CSV 파일과 `.meta`
2. DTO와 column mapping
3. `IDataLoad` 또는 DataTable 구현
4. `DataTableManager` 등록과 loader routing
5. PK·FK와 필수값 검증
6. Prefab, resource 또는 Addressables 연결
7. Unity reimport와 compile error 0 확인
8. 데이터를 실제로 읽는 최소 실행 경로 검증

파일만 생성하고 loader에 연결되지 않은 상태는 완료로 판단하지 않는다.

## 병렬 작업과 충돌

- 동시에 수정하는 작업은 ID 범위와 address를 먼저 예약한다.
- 공용 CSV와 Addressables 설정은 branch가 달라도 의미 충돌이 발생할 수 있다고 간주한다.
- 병합 직전에 최신 기본 branch 기준으로 ID, FK, address, GUID와 loader routing을 다시 검증한다.

## 완료 확인

- header, 자료형, 필수값과 허용값이 기존 schema에 맞는가?
- PK가 고유하고 승인 범위 안에 있는가?
- 모든 FK와 resource·address 참조가 유효한가?
- CsvHelper와 기존 loader가 오류 없이 읽는가?
- Unity compile과 최소 실행 검증을 완료했는가?

실행 검증을 하지 못했다면 `PASS` 대신 `STATIC PASS`, `PARTIAL` 또는 `BLOCKED`로 보고하고, compile error·실행 오류 또는 필수 데이터·참조 실패가 확인되면 `FAIL`로 보고한다.
