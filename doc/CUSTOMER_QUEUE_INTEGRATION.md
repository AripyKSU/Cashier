# 손님 대기열 구현·병합 명세

## 범위와 규칙

- 대기 정원·표시 최대 10명. 계산 중인 손님은 별도다. FIFO 순서는 화면의 #번호로 표시한다.
- OPEN STORE에서 최초 손님을 바로 계산대에 배치하고 이후 5초마다 줄에 합류한다. 꽉 찬 시점의 입장은 건너뛰며 재시도 물량을 쌓지 않는다.
- 합류 시 외형·성향·최초 희망 목록과 표시 단가를 고정한다. 실제 거래 기준액·허용액·원가는 SubmitOffer 시점의 최종 목록·최신 현재가로 확정한다. 라디오 이후에도 최초 표시 단가는 보존하지만 판정은 최신 가격을 사용한다.
- 성향별 한도에 도달하면 즉시 논리적 이탈. 계산 중인 손님에게 대기 한도는 적용하지 않는다.
- 남은 시간 6초 이하일 때 재촉 1회, 만료 시 불만 1회. 말풍선은 각각 3초 표시한다. 긴 프레임에서 재촉·만료를 함께 넘으면 불만만 표시한다.
- 만료를 처리한 뒤 계산대로 인계한다. 거래 결과는 3초 보여준 뒤 자동으로 다음 손님을 인계한다. NEXT CUSTOMER로 먼저 인계할 수도 있지만 빈 줄에서 새 손님을 생성하지 않는다.
- 게임 일시정지 시 입장·대기·대사 표시·결과 표시 시간이 멈춘다. 영업 종료 시 줄과 남은 말풍선은 불만 없이 제거한다. 이탈 페널티·일수별 인원 증가는 범위 밖이다.

## CSV migration

기준 커밋 `6884abf`의 성향 CSV 마지막에 아래 필수 컬럼을 추가한다. 기존 열·행·ID·GUID를 보존한다. 구형 CSV는 header 누락으로 실패하며 기본값으로 우회하지 않는다. 롤백은 DTO·CSV·로더·catalog·소비자를 함께 되돌린다. 현재 세이브 연결은 없다.

| 컬럼 | 타입/검증 | 의미 |
|---|---|---|
| queue_patience_seconds | uint, 6 초과 | 줄 합류부터 만료까지 초 |
| queue_warning_textidx | uint, 필수 TextData FK | 재촉 대사 |
| queue_leave_textidx | uint, 필수 TextData FK | 이탈 불만 |

테스트 배정: 평범(6001) 12초·8050/8051, 급함(6002) 9초·8052/8053, 가격 민감(6003) 18초·8054/8055. TextData의 기존 마지막 8049 뒤에 6개 문구를 추가했다. 새 CSV 종류·Addressables 등록은 없다. 병합 직전 다른 branch와 신규 Text PK 충돌을 다시 검사한다.

`CustomerDispositionDataTable`은 구매 및 대기 설정을 검증하고 `CustomerCatalog`는 두 신규 Text FK를 검사한 후 공개한다. 오류 시 기존 LogError·예외 경로를 따른다. 독립 구매 생성 테스트는 대기 설정을 요구하지 않지만 CSV 로드와 줄 합류에서는 필수다.

## 소유권·API

- `CustomerQueue`: 화면 수명 대기열. 생성 콜백과 성향 사전을 생성자에 전달한다. 콜백은 그 시점의 `EnsureDailyPrices().Prices`를 사용한다.
- `Start` / `Stop`: 영업 시작·정리. 중복 Start는 거부한다.
- `Advance(deltaSeconds, paused)`: 활성 영업 화면 한 곳에서 프레임당 한 번 호출한다. 만료·재촉·5초 입장을 처리한다. 인계 전에 호출해야 한다.
- `TryAdd`: 정원 내에서 새 방문 등록. 초기 계산대 손님을 위한 1회 호출 외에는 자동 입장을 사용한다.
- `TakeNext`: FIFO 인계. 빈 줄이면 null. 반환된 방문에 `BeginOffer`를 호출하고 기존 거래 시스템을 사용한다.
- `Waiting` / `Leaving` / `GetSpeech`: 읽기 전용 대기·이탈 표시 데이터. 이탈 기록은 논리적 정원에 포함하지 않는다.
- `CustomerState` 기존 숫자는 보존하고 `Queued=5`, `Abandoned=6`을 뒤에 추가한다. 만료 이탈은 거래 결과 None이며 수입을 발생시키지 않는다.
- `Dev3SandboxTester.Queue`: 통합 화면 소유 대기열 조회. Update가 시계를 진행하며 고정 10개 색상 사각형을 재사용한다. 테스트 배치는 계산대 왼쪽 2열이며 최종 공간상 줄 배치는 아트/UI 통합 시 교체할 수 있다.
- 화면 파괴와 영업 종료 시 Stop. 영업 중 씬 재진입·세이브 복원은 기존 제한을 유지한다. 구형 CustomerSandbox는 Git 제외 Local 개인 코드이며 공유 설치에 필요하지 않다.

## 사용법·검증

1. InitScene부터 개인 GameplaySandbox로 진입한다. START SESSION → 여정 시작 → 영업 시작 → OPEN STORE.
2. 계산 중인 손님을 그대로 두면 5초 간격으로 대기열이 증가한다. 최대 10명을 넘지 않는다.
3. 급함 성향은 합류 3초 후 재촉, 9초 후 이탈한다. 평범은 6/12초, 가격 민감은 12/18초다. 입장 간격 5초·대사/결과 표시 3초·라디오 시간은 유지한다.
4. 거래를 판정하면 3초 후 다음 대기 손님이 계산대로 이동한다. 종료하려면 결과 표시 중 END DAY를 누른다.
5. 자동 API 검사는 [TESTING.md](TESTING.md)의 CustomerQueueTests/CustomerCsvTests를 사용한다. 기존 셸은 제거했다. 정확한 최신 건수·증거는 해당 문서를 따른다.
6. 위 화면·버튼·말풍선 사용법은 사용자 수동 검증이며 자동 API 통과와 별개다.

이전 개인 씬 구현 검증 기록(현재 Test Runner 실행과 별개): PASS. 순수 로직·CSV·기존 CustomerGenerator 회귀 통과. Unity 컴파일 통과. GameplaySandbox에서 10명 색상·PK 표시, 재촉·이탈 표시, 3초 결과 이후 자동 FIFO 인계, 기존 거래 수락/거부·중복 수입 방지·정산·다음 날 전환 통과. Console error 0. 시간 경계 검사는 Advance로 시간을 주입했고 자동 인계는 실제 Update 경과로 확인했다. 최종 아트·공간상 줄 배치는 미작업이다.

## 병합 주의

- 성향 CSV와 DTO·로더·catalog·TextData를 같은 변경으로 병합한다. 기존 header를 유지한 채 3개 열을 끝에 추가한다.
- MainScene/prefab을 새로 저장할 필요 없이 기존 Dev3SandboxTester 연결을 사용한다. 개인 씬은 Git 제외 유지.
- 공용 가격 이벤트·Finance API를 복제하지 않는다. 최초 희망 표시와 제출 시점 확정 단가를 구분하고 기존 거래의 한 번 입금 계약을 유지한다.
- UI branch의 NEXT CUSTOMER·Update·영업 시작/종료 변경과 의미 충돌을 검토한다. 초기 영업 안내·대기 손님의 희망 목록 표시 단가와 제출 시 확정 거래 단가는 라디오 발생 이후 서로 다를 수 있다.
- 신규 스크립트의 Unity 생성 .meta를 함께 병합한다. 기존 TMP fallback font 변경은 이 기능과 무관하다.


현재 성향 CSV에는 이후 추가된 disposition_type/preferred_product_idxs/regular_price_min_rate/regular_price_max_rate도 존재한다. 위 3열 migration만 복사해 최신 후속 열을 잃지 않는다. 실제 헤더와 [CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md)를 함께 반영한다. Test Runner assembly/meta 및 Local 제외 의존은 [TESTING.md](TESTING.md)를 따른다.
