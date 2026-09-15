# 손님 구성 선택·생성 통합

2026-09-09 기준 손님 스폰 규칙의 runtime 경계와 데이터 연결을 기록한다.

## 책임 경계

- `CustomerCompositionSelector`는 손님 구성 snapshot을 선택한다.
  - 하루 시작 명성의 `ReputationBalanceData`로 Normal, PriceSensitive, Wealthy, Hasty(표시명 성급함), Poor를 각각 가중치 선택한다.
  - 구성군 안에서는 타입을 균등 선택하고, 같은 타입의 성향 행을 PK 순서로 균등 선택한다.
  - 선호 타입과 `preferred_product_idxs`를 OR로 합치되, 상품의 날짜·활성·설비 조건을 먼저 적용한다.
  - 영업일마다 새 인스턴스를 만들며 첫 성별은 무작위, 이후 손님은 직전 성별의 반대다.
  - 성별·연령 속성을 정한 뒤 `CustomerAppearanceData`의 두 분류값이 모두 일치하는 후보를 PK 순으로 정렬해 균등 선택한다. 성향과 Normal은 외형 조건이 아니다.
- `CustomerGenerator`는 selector가 만든 `CustomerComposition`을 `CustomerVisit`으로 복사해 전달한다. 가격 callback과 판매 지침 callback의 수명 연결도 이 경계에서 수행한다.
- `DayProgress`는 하루 시작 명성 snapshot, 당일 현재가, `GameSessionManager.IsFacilityActive`를 selector에 전달하고, 매 영업일 selector를 새로 만든다.

2026-09-15 외형 필터 이관: `SelectComposition`, `SelectCompositionUniform` 및 구형 생성 호환 확장의 첫 인수는 외형 ID 목록에서 `IReadOnlyDictionary<uint, CustomerAppearanceData>`로 변경했다. 호출자는 `catalog.Appearances.Rows`를 전달한다. 외형 ID만으로 성별·연령을 추론하거나 무필터 생성으로 우회하지 않는다. 기존 `CustomerGenerator.Generate(CustomerComposition, ...)`는 확정된 구성을 그대로 사용한다.

## 외형 필터 검증 (2026-09-15)

- `codex/customer-talk-ui`에서 컴파일 오류 0, 제품 Console error 0. 관련 EditMode 138/138: Selector 4, SelectorIntegration 9, Composition 4, Contract 57, CSV 64. PlayMode `CustomerGenderAlternatesWithinBusinessDay` 1/1 통과. 실패·skip 0인 최종 결과는 `Temp/AppearanceFilter-*.json`에 보관한다.
- 외형 45행의 기존 PK·이름/이미지 FK를 보존하고 분류표와 전 행을 대조했다. 여섯 조합·12개 테스트 후보 도달, seed 재현성, 실패/무상품 시 성별 상태 보존, 잘못된 분류와 조합 누락 거부, 실제 영업일 방문의 외형 일치를 확인했다.
- 초기 부분 이름 필터 0건은 검증으로 집계하지 않았다. Composition의 고정 외형 ID와 CSV의 과거 상품 가격 문자열 5곳은 테스트 입력만 보정했다. 전체 suite·최종 이미지 분류의 시각적 적합성은 별도 확인 대상이며 씬·프리팹·Addressables는 변경하지 않았다.

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

6004~6006은 Normal 타입의 Tools/ElectricalEquipment/ProtectiveEquipment 선호 행이며, 6008~6009는 PriceSensitive, 6010~6011은 Wealthy, 6012~6015는 Poor의 추가 선호 행이다. Hasty는 의약품 선호만 유지한다. 같은 타입의 여러 행이 타입 출현율을 바꾸지 않도록 selector가 타입을 먼저 선택한다.

## 검증

- EditMode: selector의 명성 구성군 가중치, 같은 타입 행의 정산 규칙 축약, 설비 활성 전후 선호 상품, 기존 손님·CSV·시설 회귀.
- PlayMode: 실제 `GameProgress → DayProgress → CustomerCompositionSelector → CustomerGenerator` 경로와 영업일 수명, 설비 다음날 활성화를 확인한다.
- 기존 이미지 asset, prefab/Addressables와 UI/UX 표현 변경은 이 작업 범위에 포함하지 않는다.
