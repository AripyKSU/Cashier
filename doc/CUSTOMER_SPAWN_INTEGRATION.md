# 손님 구성 선택·생성 통합

2026-09-09 기준 손님 스폰 규칙의 runtime 경계와 데이터 연결을 기록한다.

## 책임 경계

- `CustomerCompositionSelector`는 손님 구성 snapshot을 선택한다.
  - 하루 시작 명성의 `ReputationBalanceData`로 일반군(Normal·PriceSensitive), Wealthy, Hasty를 가중치 선택한다.
  - 구성군 안에서는 타입을 균등 선택하고, 같은 타입의 성향 행을 PK 순서로 균등 선택한다.
  - 선호 타입과 `preferred_product_idxs`를 OR로 합치되, 상품의 날짜·활성·설비 조건을 먼저 적용한다.
  - 영업일마다 새 인스턴스를 만들며 첫 성별은 무작위, 이후 손님은 직전 성별의 반대다.
  - 외형 ID는 현재 전체 후보에서만 선택한다. 성별별 실제 이미지 묶음 연결은 별도 리소스 작업으로 남겨 두며 이 클래스에서 외형 ID로 성별을 추론하지 않는다.
- `CustomerGenerator`는 selector가 만든 `CustomerComposition`을 `CustomerVisit`으로 복사해 전달한다. 가격 callback과 판매 지침 callback의 수명 연결도 이 경계에서 수행한다.
- `DayProgress`는 하루 시작 명성 snapshot, 당일 현재가, `GameSessionManager.IsFacilityActive`를 selector에 전달하고, 매 영업일 selector를 새로 만든다.

## 상품 분류와 설비

`ProductType`에는 `Tools=5`, `ElectricalEquipment=6`, `ProtectiveEquipment=7`을 추가했다. ProductData의 설비 상품은 다음과 같이 분류된다.

- 설비 12003: 상품 1015~1016, `Tools`
- 설비 12004: 상품 1018~1019, `ElectricalEquipment`
- 설비 12005: 상품 1020~1021, `ProtectiveEquipment`
- 설비 12006: 상품 1022~1023, `ProtectiveEquipment`

상품 후보를 구성할 때 `CustomerProductAvailability`가 `IsAvailable`, `AvailableDay`, `RequiredFacilityIdx`를 모두 통과시킨 뒤에 선호 타입을 평가한다. 따라서 선호 타입이 잠금 상품만 가리켜도 잠금 전에는 기본 후보에서만 구성된다.

## 명성 정산 타입 매핑

`ReputationDispositionRules.BuildByType`는 `CustomerDispositionData`를 타입별 대표 행으로 축약한다. `PreferredProductTypes`, 개별 선호 ID, 대사와 같은 구성 필드는 무시하며 `PriceTolerance`, `RegularPriceMinRate`, `RegularPriceMaxRate`만 비교한다. 같은 타입 행의 가격 규칙이 다르면 정산이 모호하므로 `InvalidDataException`으로 로드를 중단한다.

## 데이터 행

6004~6006은 기존 Normal 타입의 Tools/ElectricalEquipment/ProtectiveEquipment 선호 행이다. 6007은 Wealthy 타입의 실제 행이다. 같은 타입의 여러 행이 명성 일반군의 타입 출현율을 바꾸지 않도록 selector가 타입을 먼저 선택한다.

## 검증

- EditMode: selector의 명성 구성군 가중치, 같은 타입 행의 정산 규칙 축약, 설비 활성 전후 선호 상품, 기존 손님·CSV·시설 회귀.
- PlayMode: 실제 `GameProgress → DayProgress → CustomerCompositionSelector → CustomerGenerator` 경로와 영업일 수명, 설비 다음날 활성화를 확인한다.
- 실제 성별별 이미지 asset 연결, prefab/Addressables 연결, UI/UX 표현은 이 작업 범위에 포함하지 않는다.
