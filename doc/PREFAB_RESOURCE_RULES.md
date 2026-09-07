# Prefab과 리소스 작업 규격

이 문서는 Prefab, Addressables, `.meta`와 연관 리소스의 생성·수정·검증 기준을 정의한다. 공통 권한과 승인 절차는 [`AGENTS.md`](../AGENTS.md), 역할 명부는 [`TEAM_ROLES.md`](TEAM_ROLES.md)를 따른다.

## 역할 경계

| 변경 | 기본 담당 | 추가 검토 |
|---|---|---|
| 외형, Transform, 기존 component 값과 기존 resource 연결 | 리소스 | 기획 의미나 원본 art가 바뀌면 해당 역할 확인 |
| 신규 script 또는 gameplay 동작 | 프로그래머 | 프로그래머 규칙 적용 |
| 직렬화 field와 공용 component 구조 | 프로그래머 | 프로그래머 `Primary` 검토 |
| Addressables group·address·label | 리소스 | 보호 변경 승인 절차 적용 |
| 기획 수치나 동작 의미 | 기획 | runtime 구조가 바뀌면 프로그래머 확인 |

## Prefab 편집과 규격

- Prefab 생성·수정은 Unity Editor를 기본으로 한다.
- YAML 직접 편집은 변경 대상과 참조 관계가 명확한 제한적 수정에만 허용한다. 이후 Unity reimport, missing script, missing reference와 직렬화 오류를 검사한다.
- 기능별 기존 Prefab을 우선 기준으로 사용한다.
- 파일명과 root GameObject 이름, 저장 경로, Transform·pivot, 필수 component, 자식 구조, layer·tag, 직렬화 참조와 Addressables 등록 필요 여부를 확인한다.
- 기능별 자산은 `Assets/Prefabs/<Feature>/`, `Assets/Anims/<Feature>/`, `Assets/Textures/<Feature>/`처럼 대응하는 하위 폴더에 둔다.

## 파일과 Import 관리

- 대상 리소스의 사용 위치, 규격, 원본·라이선스·출처와 수정 가능 여부를 확인한다.
- 기존 material, sprite, prefab, animation과 audio를 먼저 검색한다.
- 대소문자만 다른 파일이나 같은 목적의 단수·복수 폴더를 만들지 않는다.
- 원본, 작업 파일과 최종 export를 구분하고 임시·도구 출력 파일을 제품 자산 폴더에 남기지 않는다.
- Texture와 Animation은 해상도, frame 수, PPU, pivot, 방향, alpha, framing, slicing과 import 설정을 실제 결과물에서 확인한다.
- 생성 성공 응답이나 로컬 파일 존재만으로 시각 품질 또는 runtime 사용 성공을 주장하지 않는다.
- 사용하지 않는 임시·중복 자산은 식별하되 승인 없이 삭제하지 않는다.

## `.meta`와 GUID

- Unity asset과 `.meta`를 한 쌍으로 취급한다.
- 생성·이동·이름 변경·삭제 시 `.meta`와 모든 소비자 참조를 함께 확인한다.
- 기존 `.meta`를 임의로 재생성하거나 GUID를 교체하지 않는다.
- 병합 전 GUID 중복·변경, missing reference와 본 파일·`.meta`의 1:1 존재를 확인한다.

## Addressables

- 기본 address는 확장자를 제외한 파일명이다.
- 같은 address가 이미 있으면 새 entry를 등록하기 전에 충돌을 해소한다.
- 필요한 경우 기존 `Datas`와 `Prefabs`의 설정을 참고해 group과 label을 생성하거나 적용할 수 있다.
- group·address·label 변경은 보호 변경으로 취급하고 사전 승인과 교차 검토를 받는다.
- 변경 후 중복 address·entry, GUID, dependency와 실제 runtime load를 확인한다.
- address 변경 시 모든 소비자와 migration 영향을 확인한다.

## 연관 파일 동기화

- 기존 manager, pooling과 bundle 경로를 사용하고 개별 객체가 loader API를 우회하지 않게 한다.
- 실제 영향을 받는 CSV, Prefab, Animator, Texture와 Addressables 항목만 하나의 작업 단위로 추적한다.
- 관련 파일을 여러 commit으로 나누더라도 완료 전에는 연결 관계를 함께 검증한다.
- 준비되지 않은 참조를 임의 ID, 임시 문자열 또는 placeholder로 활성화하지 않는다.

## 완료 확인

- Prefab 저장과 Unity reimport가 완료됐는가?
- missing script, missing reference와 직렬화 오류가 없는가?
- asset과 `.meta`가 한 쌍이며 기존 GUID가 보존됐는가?
- address 중복과 dependency 누락이 없는가?
- Texture와 Animation의 규격·방향·alpha·framing·slicing·import 상태를 확인했는가?
- 필요한 최소 실행 경로에서 load와 표시를 확인했는가?

실행 검증을 하지 못했다면 `PASS` 대신 `STATIC PASS`, `PARTIAL` 또는 `BLOCKED`로 보고하고, compile error·실행 오류 또는 필수 데이터·참조 실패가 확인되면 `FAIL`로 보고한다.
