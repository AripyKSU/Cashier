# 16종 상품 연결

원본: 사용자가 제공한 N:/개인/정총무/*.png. 프로젝트 PNG는 원본과 SHA256 동일. 별도 라이선스 문서는 제공되지 않음.

|단계|설비|상품 / 이미지 파일|
|---|---|---|
|기본|없음|생수 / DrinkingWater, 통조림 / CannedFood, 붕대 / MedicalBandage, 건전지 / DryBattery|
|1|식량 보관 선반|군용식량 / MilitaryRation, 영양바 / NutritionBar|
|1|약품 보관장|약통 / Medicine, 응급 주사 / EmergencyInjection|
|2|공구대|손전등 / Flashlight, 접이식 삽 / FoldingShovel|
|2|전력·통신 장비|무전기 / Radio, 배터리 / PowerBattery|
|3|핵보호 물품 설비|방독면 / GasMask, 방호복 / ProtectiveSuit|
|3|정밀 전자장비 보관장|방사능 측정기 / RadiationDetector, 열화상 카메라 / ThermalCamera|

이미지 경로: Assets/DystopiaPrototype/Art/Products/*.png. 원본을 잘라 저장하지 않고 Sprite 영역만 투명 여백에 맞춰 설정. Point, 무압축, mipmap 없음.

DystopiaScreen > Settings > Shop Stage (0~3)와 Owned Facilities에서 해금 조건을 설정. 두 조건을 모두 충족해야 함. Products의 Price는 양수로 입력. 기본 네 상품의 기존 가격은 보존했고 신규 12종은 가격 미설정(0)이므로 주문에서 제외됨. 가격이나 설비 구매 비용은 임의로 정하지 않음. 설비 구매 UI는 이번 작업에 포함되지 않음.

해금된 상품 목록은 다음 영업일 시작에 확정되어 기존 장바구니의 상품 인덱스가 중간에 바뀌지 않음. Apply Stage 1 / 3 Shop은 가게 단계 조건을 각각 1 / 3으로 갱신하지만 보유 설비를 자동 지급하지 않음.

등록 메뉴: Dystopia/Register 16 Survival Products. 기존 이름 또는 ID가 일치하는 상품 가격은 보존하며 카탈로그와 이미지 연결만 교체. 자동 실행하지 않음.
검증 메뉴: Dystopia/Validate Survival Product Unlocks. 원래 씬과 가격을 변경하지 않고 복사본으로 256가지 단계·설비 조합 및 실제 세션 주문을 검사함. 결과는 validation.txt에 기록.

일일지침의 건빵·즉석밥 참조는 영양바·군용식량으로 교체. 잠긴 해당 상품의 지침은 제한 없음으로 표시. 기존 네 품목 프리팹의 배열 순서에 의존하지 않고 카탈로그 Sprite로 물품을 생성.

현재 열린 씬에 등록 완료. 전후 검증 복사본: output/survival-products/20260913-164755/. 상품 설정 두 곳 외 배치 변경 없음. 사용자가 작업 중인 원본 씬은 자동 저장·재로드하지 않았으므로 Unity에서 저장해야 디스크 씬에 유지됨.
