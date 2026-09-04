# 프로젝트 참여자 역할 명부

이 문서는 프로젝트 참여자의 복수 역할과 적용할 역할 규칙을 식별하는 단일 권위다. 역할별 기본 책임과 권한 판정 방식은 [`AGENTS_GUIDE.md`](AGENTS_GUIDE.md)를 따른다.

프로젝트 책임자 및 PM은 김기도(`kidokim.game@gmail.com`)다.

## 역할 수준

- `Primary`: 해당 역할 규칙의 주 책임과 기본 검토 책임
- `Shared`: 해당 역할의 공용 규칙을 공동 소유하며 작업별 허용 범위에서 수행·검토
- `—`: 현재 부여되지 않은 역할

## 참여자

| 이름 | Git email | 프로그래머 | 기획 | 아트 | 리소스 |
|---|---|---|---|---|---|
| 강성규 | `tjdrb70@gmail.com` | Primary | Shared | — | Shared |
| 김승욱 | `kimsu00215@naver.com` | Primary | Shared | — | Shared |
| 이규영 | `rbdud1216@gmail.com` | Primary | Shared | — | Shared |
| 김기도 | `kidokim.game@gmail.com` | Shared | Primary | Primary | Primary |

## 권한 판정

- 역할은 누적 적용한다. 여러 역할의 규칙이 충돌하면 현재 작업의 명시적 지시와 허용 범위를 우선하고, 결과를 바꾸는 충돌은 사용자 또는 프로젝트 책임자에게 보고한다.
- Git email은 참여자를 찾는 조회 키로만 사용하며 신원이나 승인 권한을 증명하지 않는다.
- `Primary`와 `Shared`는 저장소 전체 수정 권한이 아니다. 실제 작업에서는 대상 기능, 경로와 생성·수정·이동·삭제·검토·승인 중 허용 작업을 확인한다.
- 별도 작업 branch는 구현과 local commit을 격리할 뿐 역할 권한을 확대하지 않는다. 일반 변경은 담당자가 통합할 수 있고, 공용·보호 변경은 [`AGENTS_GUIDE.md`](AGENTS_GUIDE.md)의 기본 branch 통합 전 검토 게이트를 따른다.
- package, ProjectSettings, 공용 manager, 원본 삭제, Git 통합처럼 보호된 변경은 [`AGENTS_GUIDE.md`](AGENTS_GUIDE.md)의 별도 승인 규칙을 따른다.
- 작업별 지시는 이 명부의 범위를 좁힐 수 있다. 역할이나 보호 작업 권한을 넓히려면 사용자의 명시적 승인이 필요하다.

## 교차 검토

- 보호 변경을 수행한 작업자는 해당 변경을 단독 승인할 수 없다.
- 프로그래밍 보호 변경은 작업자 외 프로그래머 `Primary` 1명 이상이 검토한다.
- prefab, data와 resource 보호 변경은 작업자 외 관련 역할 `Primary`가 검토한다. 다른 `Primary`가 없으면 해당 역할의 `Shared` 1명이 검토한다.
- 승인 증거는 PR 댓글 또는 현재 작업 명세에 기록한다.
- 검토자를 지정할 수 없거나 동의가 결렬되면 이견과 영향을 기록하고 프로젝트 책임자가 최종 판단한다.

## 미확정 항목

- 기능·시스템별 담당자
- 경로별 생성·수정·이동·삭제 권한
- 공용 파일과 보호 설정의 승인자
- 문서 검토·갱신 책임자
