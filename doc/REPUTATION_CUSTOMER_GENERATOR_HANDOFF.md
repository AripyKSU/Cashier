# 명성 기반 손님 생성 데이터 인계서

## 1. 문서 목적

이 문서는 손님 생성을 담당하는 작업자가 명성 시스템에서 확정한 **하루 시작 명성**을 손님 생성 코드에 연결할 때 참고해야 하는 입력값과 기획 데이터를 정리한다.

명성 시스템은 하루 시작 시점의 명성과 그 명성이 속한 밸런스 구간 데이터를 제공한다. 손님 후보 선택과 실제 생성 방식은 `CustomerGenerator` 담당 범위이므로 이 문서에서는 구현 방법을 규정하지 않는다.

## 2. 전달받아야 하는 값

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
| `wealthy_weight` | `WealthyWeight` | 부자군 생성 비중 | `1000 = 100%` |
| `hasty_weight` | `HastyWeight` | 절박군 생성 비중 | `1000 = 100%` |
| `special_weight` | `SpecialWeight` | 향후 특수 손님 생성 비중 | `1000 = 100%` |

각 행에서 네 구성 가중치의 합은 항상 `1000`이다. `recovery_rate`와 `settlement_*` 열은 명성 정산용 값이며 손님 생성 비율 계산에는 사용하지 않는다.

## 4. 명성 구간별 손님 생성 비율

| 명성 구간 | 일반군 | Wealthy(부자) | Hasty(절박) | 특수군 | CSV PK |
|---:|---:|---:|---:|---:|---:|
| `-100 ~ -61` | 66.7% | 3.3% | 30.0% | 0.0% | `11001` |
| `-60 ~ -21` | 70.7% | 6.5% | 22.8% | 0.0% | `11002` |
| `-20 ~ 20` | 76.1% | 10.9% | 13.0% | 0.0% | `11003` |
| `21 ~ 60` | 73.0% | 18.0% | 9.0% | 0.0% | `11004` |
| `61 ~ 100` | 69.0% | 25.3% | 5.7% | 0.0% | `11005` |

CSV에는 위 비율이 각각 `667/33/300/0`처럼 1000분율 정수로 저장되어 있다.

## 5. 손님 성향과 구성군 대응

손님 성향의 권위 데이터는 `CustomerDispositionData`와 `CustomerDispositionType`이다.

| 명성 구성군 | 포함할 `CustomerDispositionType` | 기획상 의미 |
|---|---|---|
| 일반군 | `Normal`, `PriceSensitive` | 명성 시스템에서는 두 성향을 같은 일반군으로 취급 |
| 부자군 | `Wealthy` | 부자 손님 |
| 절박군 | `Hasty` | 절박 손님 |
| 특수군 | 현재 미정 | 태그·속성 규약 확정 후 연결 |

관련 정의 위치는 다음과 같다.

| 데이터 | 위치 |
|---|---|
| 손님 성향 타입 | `Assets/Scripts/Commons/CustomerProfileTypes.cs`의 `CustomerDispositionType` |
| 손님 성향 행 | `Assets/Scripts/Customer/Data/CustomerDispositionData.cs` |
| 실제 손님 성향 CSV | `Assets/Datas/Customer/CustomerDispositionData.csv` |
| 검증된 성향 후보 | `DataTableManager.Instance.Customers.Dispositions.Rows.Values` |

## 6. 연결 지점과 책임 경계

하루 시작 명성을 보관하고 외부에 제공하는 위치는 `DayProgress.DayStartReputation`이다. 손님 생성 요청이 만들어지는 현재 호출 지점은 `Assets/Scripts/Progress/DayProgress.cs`의 `createCustomer()`이며, 이곳에서 기존 `CustomerGenerator.Generate(...)`가 호출된다.

현재 상태에서는 `createCustomer()`가 기존 전체 성향 후보만 전달하며, 명성 값이나 명성 구간 데이터는 아직 `CustomerGenerator`에 전달하지 않는다. 연결 작업 시 사용해야 할 명성 시스템 측 입력은 다음 두 값이다.

- `DayProgress.DayStartReputation`
- 위 값으로 `ReputationBalanceDataTable.TryGetByReputation(...)`를 조회한 `ReputationBalanceData`

책임 범위는 다음과 같다.

| 영역 | 책임 |
|---|---|
| 명성 시스템 | 하루 시작 명성 확정·보관, 명성 구간 CSV 로드와 검증 |
| `DayProgress` | 당일 고정 명성과 손님 생성 요청의 연결 지점 제공 |
| `CustomerGenerator` | 제공받은 명성 구간 비율을 실제 손님 후보 선택에 반영 |

## 7. 참고 사항

- `Wealthy`는 `CustomerDispositionType`에 이미 정의되어 있지만, 현재 `Assets/Datas/Customer/CustomerDispositionData.csv`에는 `Wealthy` 성향의 실제 행이 없다.
- `PriceSensitive`는 명성 시스템에서 별도 비율을 갖지 않고 일반군 비율에 포함된다.
- `Child`와 `Elderly`는 `CustomerAttributes`의 연령 속성이다. 명성 구성군과 별개이므로 위 비율이 연령 판정을 대체하지 않는다.
- 특수군은 현재 기획과 태그 규약이 확정되지 않았으며 CSV 비율도 전 구간 `0`이다.
- `CustomerGenerator.cs`에는 현재 명성 시스템 관련 변경이 없다.
