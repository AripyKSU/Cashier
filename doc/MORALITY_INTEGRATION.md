# 도덕성 거래 연동 명세

## 데이터와 판정

- `MoralityData.csv`는 `14001~14036`의36행을 사용하며 `customer_disposition_type`, 수락 여부, 제안가/현재가 배율 구간과 성인·아이/노인 점수를 정의한다. 2026-09-12에 사용자 승인으로 가난8행·부자8행과 기획서의 ID 예약 범위를 확장했다.
- 배율은 `1000=100%`다. `offer_max_rate=0`, `include_max=0` 조합만 명시적 무상한이다. boolean은 `0/1`만 사용한다.
- `MoralityDataTable`은 로드 시 PK·성향 FK·구간 누락·중복·무상한을 한 번 검증한다. 실패하면 LogError와 예외를 전달하고 공개하지 않는다.
- `MoralityCalculator`는 거래 때 검증된 행에서 최초 일치 행만 조회한다. `offeredTotal*1000`과 `referenceTotal*rate`를 decimal로 비교하며 나누기·반올림을 하지 않는다.
- 현재 성향 CSV에서 Normal은130%, Hasty는150%, Poor는115%, Wealthy는180% 이하를 구매한다. PriceSensitive는 정확히100%만 구매한다. 다섯 성향 모두 도덕성 행이 등록되어 있다.
- 가난의 점수는 기획 확정표를 반영했다. 도덕성 중립점은90%이며100% 기준가 판매는 성인−4.5, 아이·노인−6.75다. 거래 성공·기준가 판정과 도덕성0점은 서로 다른 조건이다.
- 부자 점수는 사용자 승인 초안이다.100%는0점,180% 판매와180% 초과 거절은 성인−0.5, 아이·노인−1이다. 전체 경계와 근거는 [반영표·곡선](work/balancing-reference/morality-review-20260912.md)을 따른다.

## 결과와 세션

- `TransactionResult.MoralityDataIdx`와 `MoralityDelta`는 거래 당시의 불변 snapshot이다. null은 미평가, 0은 평가 결과 0점이다.
- 초기 도덕성은 `0m`이며 `GameSessionManager.CurrentMorality`가 날짜·화면 진행 객체와 무관하게 decimal 누적값을 소유한다. clamp와 일일 제한은 없다.
- `DailyAggregationService.DailyMoralityDelta`가 당일 성공·거절 거래의 `MoralityDelta ?? 0m`를 누적한다. `GameSessionManager.DailyMoralityDelta`는 이 값의 전달 getter이며 별도 상태를 저장하지 않는다.
- 정산 결과 `DailyAggregationResult.MoralityDelta`는 확정 snapshot이다. `EndDay` 후 당일 누적은 0으로 초기화되지만 `CurrentMorality`와 이전 정산 결과는 유지된다. 거래 없는 날의 정산 도덕성은 0이다.
- 일일 도덕성 overflow도 세션 총누적·잔고 변경 전에 거부한다. 재정 알림 관찰 시 일일값까지 확정되어 있으며 알림 예외에서도 확정값을 유지한다.

```csharp
decimal today = session.DailyMoralityDelta;
session.EndTradingDay(out DailyAggregationResult settlement);
decimal completedDayDelta = settlement.MoralityDelta;
decimal total = session.CurrentMorality;
```

정산 호출은 기존 진행 소유자만 수행한다. 화면은 기존 `DayProgress.AggregationResult`를 조회할 수 있으며 표시 기능은 이번 범위에 포함하지 않는다.
- `TryApplyTransaction`에서 값이 실제 변경되면 `[Morality] current=현재값, delta=변화량`을 Debug.Log로 출력한다. 개발 확인용 로그이며 도덕성 디버깅이 끝나면 제거 여부를 결정한다.
- 성공과 결제 거절을 모두 계산한다. 동일 방문의 중복 제안은 기존 `CustomerVisit` 상태와 DayProgress 오류 latch가 차단한다.
- 적용 전 일일 매출·명성·잔고·도덕성의 다음 값을 모두 checked 검증한다. 재정 알림 예외가 발생해도 거래 기록·잔고·도덕성은 함께 확정된 상태를 유지하고 오류를 전달한다.
- UI 표시, 명성 계산 변경, 구형 `CashierSession` 연결은 범위 밖이다.

## Addressables와 검증

- 기존 `Default Local Group`, 주소 `MoralityData`, 기존 `Datas` 라벨을 사용한다. 신규 group·label은 만들지 않는다.
- 자동 검증은 실제 CSV36행의 다섯 성향 경계·소수 점수·누락/중복/유한 꼬리 오류, PriceSensitive 현재가 ±1, 가난·부자의 실제 거래 결과와 도덕성 행 연결, 소수 누적과 재정 알림 실패 원자성을 포함한다. 실행 결과는 [작업 기록](work/balancing-preparation.md)을 따른다.

## 과거 기록: DailyInstruction 병합 결정 (2026-09-11)

아래는 병합 당시의 값이다. 현재 구매 상한과 도덕성 행 수는 위의 데이터와 판정 절을 따른다.

CustomerDispositionData를 권위 기준으로 사용하여 Normal 6001·6004~6006은 허용 가격 배율1100(110%), Hasty 6002는1300(130%)을 사용한다. MoralityData의 `is_accepted` 경계도 같은 값으로 맞추며, 기존 점수 구간과 점수 자체는 유지한다. CustomerCompositionSelector의 선택 결과를 Generator가 방문으로 만들 때 세션 MoralityCalculator를 전달하며, 구형 호환 Generate API도 동일하게 전달한다.
