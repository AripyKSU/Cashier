# 설비 구매·상품 해금 인계

기준: 2026-09-09, `codex/equipment-upgrades`. 세션·재정·손님·현재 가격표를 연결하며 공유 Scene/prefab은 수정하지 않는다.

## 사용법과 책임

- 초기화한 세션을 사용하는 기존 `GameProgress`에서 `Start()` 이후 `TryPurchaseFacility(12001, out var result)`를 호출한다. 외부 입력은 설비 PK 하나이며 가격·날짜를 받지 않는다.
- `true/Purchased`: CSV 가격 즉시 차감, `PaidAmount`에 지출, `ActivationDay`에 현재 경과일+1. 같은 날은 잠기며 다음 영업일에 활성화된다.
- `false/AlreadyOwned`: 재결제하지 않고 기존 활성일 반환. `false/InsufficientFunds`: 무변경, 활성일 null. 정상 실패 지출은0.
- 0·미등록 ID, 구매 재진입, 날짜 overflow는 예외다. 진행의 Initializing/Failed/Completed 상태는 구매를 거부한다. 그 밖의 상태 구매는 현재 기능 테스트용이며 정식 구매 화면·허용 phase 정책은 후속 결정이다.
- 재정 이벤트 구독자가 예외를 던지면 원래 예외를 전달한다. 이미 차감 완료했다면 보유도 유지한다. 실패를 보고 무조건 재결제하지 말고 `session.FacilityActivationDays`를 조회한다.
- 보유·활성일의 단일 권위는 세션 내부 FacilityService의 사전이다. `IsFacilityActive(uint)`와 읽기 전용 `FacilityActivationDays`만 노출한다. 같은 세션의 표현 객체 교체는 보유를 유지하고 새 세션은 초기화한다. 저장 파일 복원은 미구현이다.

## 데이터와 배포 단위

| 설비 PK / 이름 | 임시 가격 | 해금 상품 PK |
|---|---:|---|
| 12001 식량 보관 선반 | 5000 | 1005,1006 |
| 12002 잠금 약품장 | 7000 | 1009,1013,1014 |
| 12003 공구대 | 12000 | 1015,1016,1017 |
| 12004 전력 통신 장비 | 15000 | 1018,1019 |
| 12005 핵 보호 물품 설비 | 25000 | 1020,1021,1022 |

각 설비는 독립 1회 구매이며 앞 설비 구매를 요구하지 않는다. 가격과 신규 상품 분류는 승인된 테스트 값이다. 신규 의약품은 Medicine, 공구·전력·핵 보호 물품은 임시 DailyNecessities다. 기본 건전지1010과 설비 배터리1019는 서로 다른 상품이다.

- FacilityData: `idx:uint,nameidx:uint,purchase_price:long`. 종류12, PK12001~12005. 11은 ReputationBalance 예약이며 이번 enum/로더에 추가하지 않는다. enum 종료값은 자동 증가한다.
- ProductData에 `required_facility_idx:uint?` 추가. 빈 셀은 기본상품,0은 오류. 기본5개는1001/1004/1007/1010/1011. 기존1002/1003/1008/1012는 삭제하지 않고 비활성화한다. 총22행(기본5+해금13+비활성4).
- TextData는70행으로 이름8056~8070을 추가한다. 실제 가격·전체 행은 [DATA_CATALOG.md](DATA_CATALOG.md) 참조.
- DTO·CSV·DataTableManager·CustomerCatalog를 함께 반영한다. 구형 Product header는 오류다. PK·가격·설비 FK·Text FK가 모두 검증되기 전 공개하지 않는다.
- 승인된 Addressables 연결: 기존 Default Local Group의 `FacilityData` address, 기존 `Datas` label, GUID `965dc884f32f1514c8fa36b64a8c93cb`. 기존 entry/group/schema는 변경하지 않았다.

## 생성·판매·가격표 계약

`CustomerProductAvailability`가 활성 여부·등장일·설비 활성 여부를 함께 판단한다. `DayProgress` 생성기와 `GameUIController`의 가격표 factory는 세션의 `IsFacilityActive`를 전달한다. 독립 호출에서 callback을 생략하면 설비 상품은 닫힌 상태로 처리한다.

방문 생성 시 판매 가능한 **전체 상품 PK**를 복사한다. 최종 제출은 이 범위 안에서 희망 목록과 다른 상품도 가능하다. 잠긴 상품이나 방문 이후 해금 상품은 거부하며 새 방문에서 새 후보를 사용한다. 선호 FK 검증은 전체 카탈로그 기준이므로 잠긴 선호 상품 자체는 오류가 아니다. 가격 이벤트 현재가 계산, 제출 시 가격 스냅샷, 지침 위반 기록·정산 계약은 유지한다.

## 검증과 남은 범위

최신 실행 기록은 [TESTING.md](TESTING.md)를 참조한다. 구매 성공·중복·잔액 부족·재진입·알림 예외·가격 복사·다음날 해금·판매 스냅샷·CSV/FK 실패와 실제 Datas 로딩을 검증한다.

설비 설치 시각화·애니메이션·구매 UI, 묶음/랜덤 판매, 재고, 저장 복원은 이번 범위가 아니다. API 성공은 화면/UX 또는 실제 씬 전환 성공을 의미하지 않는다. 개인 씬과 공유 Scene/prefab은 보존한다.
