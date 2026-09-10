# 도덕성 거래 연동 명세

## 데이터와 판정

- `MoralityData.csv`는 `14001~14020`을 사용하며 `customer_disposition_type`, 수락 여부, 제안가/현재가 배율 구간과 성인·아이/노인 점수를 정의한다.
- 배율은 `1000=100%`다. `offer_max_rate=0`, `include_max=0` 조합만 명시적 무상한이다. boolean은 `0/1`만 사용한다.
- `MoralityDataTable`은 로드 시 PK·성향 FK·구간 누락·중복·무상한을 한 번 검증한다. 실패하면 LogError와 예외를 전달하고 공개하지 않는다.
- `MoralityCalculator`는 거래 때 검증된 행에서 최초 일치 행만 조회한다. `offeredTotal*1000`과 `referenceTotal*rate`를 decimal로 비교하며 나누기·반올림을 하지 않는다.
- Normal은 130%, Hasty는 150% 이하를 구매한다. PriceSensitive는 정확히 100%만 구매한다. 현재 Wealthy 도덕성 행은 없으므로 미평가(null)이며 신규 행을 추론하지 않는다.

## 결과와 세션

- `TransactionResult.MoralityDataIdx`와 `MoralityDelta`는 거래 당시의 불변 snapshot이다. null은 미평가, 0은 평가 결과 0점이다.
- 초기 도덕성은 `0m`이며 `GameSessionManager.CurrentMorality`가 날짜·화면 진행 객체와 무관하게 decimal 누적값을 소유한다. clamp와 일일 제한은 없다.
- `TryApplyTransaction`에서 값이 실제 변경되면 `[Morality] current=현재값, delta=변화량`을 Debug.Log로 출력한다. 개발 확인용 로그이며 도덕성 디버깅이 끝나면 제거 여부를 결정한다.
- 성공과 결제 거절을 모두 계산한다. 동일 방문의 중복 제안은 기존 `CustomerVisit` 상태와 DayProgress 오류 latch가 차단한다.
- 적용 전 일일 매출·명성·잔고·도덕성의 다음 값을 모두 checked 검증한다. 재정 알림 예외가 발생해도 거래 기록·잔고·도덕성은 함께 확정된 상태를 유지하고 오류를 전달한다.
- UI 표시, 명성 계산 변경, 구형 `CashierSession` 연결은 범위 밖이다.

## Addressables와 검증

- 기존 `Default Local Group`, 주소 `MoralityData`, 기존 `Datas` 라벨을 사용한다. 신규 group·label은 만들지 않는다.
- 자동 검증은 실제 CSV 20행 경계·소수 점수·Wealthy 미평가·누락/중복/유한 꼬리 오류, PriceSensitive 현재가 ±1, 소수 누적과 재정 알림 실패 원자성을 포함한다.
