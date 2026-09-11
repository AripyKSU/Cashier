# 명성 기반 손님 생성 데이터 인계서

## 1. 문서 목적

이 문서는 손님 생성을 담당하는 작업자가 명성 시스템에서 확정한 **하루 시작 명성**을 손님 생성 코드에 연결할 때 참고해야 하는 입력값과 기획 데이터를 정리한다.

명성 시스템은 하루 시작 시점의 명성과 그 명성이 속한 밸런스 구간 데이터를 제공한다. 손님 후보 선택과 실제 생성 방식은 `CustomerGenerator` 담당 범위이므로 이 문서에서는 구현 방법을 규정하지 않는다.

## 2. 전달받아야 하는 값

2026-09-09 total_merge 통합: 현재 명성·적용일 marker·명성 로그의 단일 소유자는 `GameSessionManager`다. `GameProgress.CurrentReputation`과 `ReputationLogService`는 세션에 위임하며 화면 재진입에도 유지한다. `DayStartReputation` snapshot과 기존 계산식은 변경하지 않는다. 생성 가중치는 아래 표와 `CustomerCompositionSelector`에 연결되어 있다.

### 하루 시작 명성

| 항목 | 내용 |
|---|---|
| 값 | `DayProgress.DayStartReputation` |
| 정의 위치 | `Assets/Scripts/Progress/DayProgress.cs` |
| 자료형 | `int` |
| 허용 범위 | `-100` 이상 `100` 이하 |
| 적용 시점 | 하루 시작 시 확정 |
| 수명 | 해당 영업일이 끝날 때까지 고정 |

손님 생성 비율에는 현재 실시간 명성이 아니라 `DayStartReputation`을 사용한다. 하루 중 거래 결과로 계산된 명성 변화는 당일 손님 구성에 반영하지 않고 다음 날 시작부터 반영한다.

### 명성 구간 데이터

`DayStartReputation`에 대응하는 명성 구간은 다음 데이터에서 확인할 수 있다.

| 구분 | 위치 |
|---|---|
| 런타임 원본 CSV | `Assets/Datas/ReputationBalanceData.csv` |
| 행 데이터 형식 | `Assets/Scripts/Finance/Data/ReputationBalanceData.cs` |
| 구간 조회 API | `ReputationBalanceDataTable.TryGetByReputation(int reputation, out ReputationBalanceData data)` |
| 테이블 정의 | `Assets/Scripts/Finance/Data/ReputationBalanceDataTable.cs` |
| 로드된 테이블 접근 | `DataTableManager.Instance.GetDB<ReputationBalanceDataTable>(DataTableType.ReputationBalance)` |

구간의 최소·최대 명성은 모두 포함한다. `-100~100`의 모든 명성은 정확히 하나의 행에 대응한다.

## 3. 손님 구성에 사용하는 CSV 값

| CSV 열 | C# 속성 | 의미 | 단위 |
|---|---|---|---|
| `idx` | `Idx` | 명성 밸런스 행 PK | 정수 ID |
| `min_reputation` | `MinReputation` | 구간 최소 명성 | 명성 |
| `max_reputation` | `MaxReputation` | 구간 최대 명성 | 명성 |
| `normal_weight` | `NormalWeight` | 일반군 생성 비중 | `1000 = 100%` |
| `price_sensitive_weight` | `PriceSensitiveWeight` | 가격 민감 손님 생성 비중 | `1000 = 100%` |
| `wealthy_weight` | `WealthyWeight` | 부자군 생성 비중 | `1000 = 100%` |
| `hasty_weight` | `HastyWeight` | 성급함군 생성 비중 | `1000 = 100%` |
| `poor_weight` | `PoorWeight` | 가난 손님 생성 비중 | `1000 = 100%` |

각 행에서 다섯 구성 가중치의 합은 항상 `1000`이다. `recovery_rate`와 `settlement_*` 열은 명성 정산용 값이며 손님 생성 비율 계산에는 사용하지 않는다.

## 4. 명성 구간별 손님 생성 비율

| 명성 구간 | 일반 | 가격 민감 | Wealthy(부자) | Hasty(성급함) | Poor(가난) | CSV PK |
|---:|---:|---:|---:|---:|---:|---:|
| `-100 ~ -61` | 50.0% | 5.0% | 2.0% | 33.0% | 10.0% | `11001` |
| `-60 ~ -21` | 55.0% | 5.0% | 6.0% | 24.0% | 10.0% | `11002` |
| `-20 ~ 20` | 60.0% | 5.0% | 10.0% | 15.0% | 10.0% | `11003` |
| `21 ~ 60` | 60.0% | 5.0% | 18.0% | 7.0% | 10.0% | `11004` |
| `61 ~ 100` | 55.0% | 5.0% | 25.0% | 5.0% | 10.0% | `11005` |

CSV에는 위 비율이 각각 `500/50/20/330/100`처럼 1000분율 정수로 저장되어 있다.

## 5. 손님 성향과 구성군 대응

손님 성향의 권위 데이터는 `CustomerDispositionData`와 `CustomerDispositionType`이다.

| 명성 구성군 | 포함할 `CustomerDispositionType` | 기획상 의미 |
|---|---|---|
| 일반군 | `Normal` | 명성 시스템에서 가장 높은 기본 등장 비중 |
| 가격 민감군 | `PriceSensitive` | 모든 명성 구간에서 독립적인 고정 5% |
| 부자군 | `Wealthy` | 부자 손님 |
| 성급함군 | `Hasty` | 대기 재촉이 빠른 손님 |
| 가난군 | `Poor` | 명성에 따라 등장률이 변하지 않는 손님 |

관련 정의 위치는 다음과 같다.

| 데이터 | 위치 |
|---|---|
| 손님 성향 타입 | `Assets/Scripts/Commons/CustomerProfileTypes.cs`의 `CustomerDispositionType` |
| 손님 성향 행 | `Assets/Scripts/Customer/Data/CustomerDispositionData.cs` |
| 실제 손님 성향 CSV | `Assets/Datas/Customer/CustomerDispositionData.csv` |
| 검증된 성향 후보 | `DataTableManager.Instance.Customers.Dispositions.Rows.Values` |

## 6. 연결 지점과 책임 경계

하루 시작 명성을 보관하고 외부에 제공하는 위치는 `DayProgress.DayStartReputation`이다. 손님 생성 요청이 만들어지는 현재 호출 지점은 `Assets/Scripts/Progress/DayProgress.cs`의 `createCustomer()`이며, 이곳에서 기존 `CustomerGenerator.Generate(...)`가 호출된다.

현재 상태에서는 `createCustomer()`가 전체 성향 후보와 하루 시작 명성 구간 데이터를 `CustomerCompositionSelector`에 전달한다. 연결에 사용하는 명성 시스템 측 입력은 다음 두 값이다.

- `DayProgress.DayStartReputation`
- 위 값으로 `ReputationBalanceDataTable.TryGetByReputation(...)`를 조회한 `ReputationBalanceData`

책임 범위는 다음과 같다.

| 영역 | 책임 |
|---|---|
| 명성 시스템 | 하루 시작 명성 확정·보관, 명성 구간 CSV 로드와 검증 |
| `DayProgress` | 당일 고정 명성과 손님 생성 요청의 연결 지점 제공 |
| `CustomerCompositionSelector` | 제공받은 명성 구간 비율을 실제 손님 후보 선택에 반영 |

## 7. 참고 사항

- `Wealthy`, `Poor`는 실제 성향 행을 가지며 타입 내부에서 선호 행을 균등 선택한다.
- `PriceSensitive`는 명성 가중치 표에서 독립 구성군으로 등장한다.
- `Child`와 `Elderly`는 `CustomerAttributes`의 연령 속성이다. 명성 구성군과 별개이므로 위 비율이 연령 판정을 대체하지 않는다.
- 가격 민감 성향의 결제 허용 하한은 100%이며, 하한 미달 거절은 판매자 폭리로 명성 분류하지 않고 중립 등급으로 기록한다.
