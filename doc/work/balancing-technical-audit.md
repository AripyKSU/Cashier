# 밸런싱 데이터 입력 기술 감사

## 후속 반영·검증 (2026-09-11)

기준 `c1d6422`에서 성향 CSV의 구매 상한 12행과 도덕성 CSV의 수락 플래그 4행을 함께 변경했다. 기존 성향·도덕성 테스트 2개 파일을 갱신했고 Unity EditMode 236/236, 실패·skip 0을 확인했다. 증거는 `Temp/TestResults/Balancing-20260911/EditMode.xml` 및 `.log`다. PlayMode와 인게임 화면은 미검증이다. 세부 범위와 남은 데이터는 [입력 준비 문서](balancing-preparation.md)의 1차 반영 결과를 따른다.

감사 정정: 변경 전 기존 20개 도덕성 행은 **당시 CSV 구매 상한**과 일치했다. 확정 기획의 새 상한을 적용하려면 14006·14007·14015·14016의 수락 플래그를 함께 바꿔야 한다는 의미다. 가난·부자의 미등록 도덕성은 `null`로 유지되며 이 상태를 가난 확정표 구현 완료로 보지 않는다. 아래 본문은 첫 반영 전 정적 감사 기록이다.

작업 자료: [입력 준비 검토](balancing-reference/INPUT_READINESS.md), [기획서 참고 탭](balancing-reference/SOURCE_INDEX.md).

- 작성 범위: 기획 데이터 입력 준비를 위한 읽기 전용 정적 조사
- 사람 담당 / 위임 범위: 강성규(`tjdrb70@gmail.com`)의 프로그래머 기술 검토 보조. 원본·승인권 변경 없음
- 권위 기준: `C:/projects/Cashier`, `total_merge`, `c1d64229316c9f891c36a8311c7a0b3ab73f5039`
- 감사 worktree HEAD: `accfa16fcb636f34d38a86050d64a73a04387d6b` (권위 기준과 다름). 아래 파일·값·코드 위치는 모두 원본 경로를 읽기 전용으로 확인했다.
- 허용 변경: 이 문서만 작성. Assets/Packages/ProjectSettings/원본 Google 문서·시트 및 원본 checkout은 변경하지 않았다.
- 검증 상태: `PARTIAL` — CSV·코드·기존 테스트를 정적으로 대조했으며 Unity reimport, compile, Console, EditMode/PlayMode, 실제 화면은 이번 작업에서 실행하지 않았다.

## 1. 결론

1. 17개 CSV가 실제로 존재하고 모두 `DataTableManager`에 등록돼 있다. 과거 15종 카탈로그 수치는 현재 기준으로 쓰면 안 된다.
2. 확정 거래 조건을 담을 스키마와 비교식은 이미 충분하다. `CustomerDispositionData.csv`의 값만 바꾸면 `Normal=1300`, `Poor=1150`, `Hasty(기획명 절박)=1500`, `Wealthy=1800`, `PriceSensitive=1000 + minimum_price_tolerance=1000`을 표현할 수 있다. `CustomerVisit.SubmitOffer`는 상한을 `floor(B*tolerance/1000)`으로 계산하고 가격민감은 별도로 `P == B`를 검사한다.
3. 현재 성향 값은 확정안과 불일치한다. 타입별 행은 Normal `1100`, Hasty `1300`, PriceSensitive `1000/최소1000`, Wealthy `1400`, Poor `1050`이다. 표시명만으로 값을 고치지 말고 `disposition_type`별 전 행에 같은 확정값을 적용해야 한다.
4. 도덕성은 `decimal`로 CSV→계산→일일/누적 상태→딸 대화까지 보존되므로 `0.25`, `0.5`, `2.25`, `6.75` 같은 소수를 스키마 변경 없이 지원한다. 그러나 현재 20행은 Normal 8 + Hasty 9 + PriceSensitive 3으로 예약을 모두 사용했고, Normal/Hasty의 `is_accepted` 구간은 현재 또는 확정 거래 상한과도 맞지 않는다. 확정 Poor 표까지 담으려면 행/ID 예약 확대가 필요하며 이는 단순 값 치환이 아니다.
5. 딸 대화 6행은 코드가 행 수를 고정하지 않아 구간을 더 잘게 나누는 행 추가가 가능하다. 다만 ID 권위 문서는 `16001~16006`만 예약한다. 이미지 3행은 모두 같은 `ResourceData 4201`을 참조하므로 날짜 구간은 나뉘어도 시각 변화는 없다. 새 이미지에는 ResourceData·실제 Sprite·주소 연결 및 새 ID 승인이 필요하다.
6. `MaintenanceBalanceData.csv`는 1~30일 30행뿐이다. 로더와 서비스는 연속된 임의 길이를 지원하므로 승인된 `30031/day=31` 한 행을 더하면 31일 조회 자체는 가능하다. 현재 상태에서 31일 납부 요청은 범위 오류다.
7. 감독관 CSV에는 실제 `15003` 행이 있지만 CSV 종류 ID 권위 스냅샷은 `15001~15002`만 예약한다. 구현 결함으로 단정할 사안이 아니라 ID 권위와 현재 데이터의 승인 기록을 조정자가 확인해야 하는 충돌이다.
8. `DayProgress.DefaultBusinessDurationSeconds=30f`, `CustomerQueue.Capacity=10`, 입장 `5초`, 말풍선 `3초`, `PriceEventScheduler` 라디오 지연 `0~60초 미만`은 CSV가 아니다. 기획 수치 입력만으로 조정할 수 없고, 30초는 코드 주석상 MVP 기본값이지 확정 밸런스가 아니다.

## 2. 17개 CSV 실측

행 수는 header를 제외한 물리 데이터 행 수다. 모든 경로는 `C:/projects/Cashier/Assets/Datas/` 기준이다.

| CSV | 행 | 실제 header |
|---|---:|---|
| `DailyGuidelineData.csv` | 2 | `idx,rule_type,allowed_quantity,penalty_amount` |
| `DaughterAppearanceData.csv` | 3 | `idx,start_day,resource_idx` |
| `DaughterDialogueData.csv` | 6 | `idx,morality_min,morality_max,text_idxs` |
| `EconomyBalanceData.csv` | 1 | `idx,initialBalance` |
| `FacilityData.csv` | 11 | `idx,nameidx,purchase_price,upgrade_kind,required_store_stage,effect_type,target_store_stage` |
| `InspectorEventData.csv` | 3 | `idx,nameidx,day,required_facility_idx,min_store_stage,priority,repeat_mode,dialogue_text_idxs,portrait_resource_idx` |
| `MaintenanceBalanceData.csv` | 30 | `idx,day,maintenanceAmount` |
| `MoralityData.csv` | 20 | `idx,customer_disposition_type,is_accepted,offer_min_rate,offer_max_rate,include_min,include_max,adult_morality_point,child_elderly_morality_point` |
| `PriceEventData.csv` | 4 | `idx,nameidx,descriptionidx,product_idxs,product_types,change_type,change_value` |
| `PriceEventScheduleData.csv` | 5 | `idx,event_idx,channel,start_day,end_day,repeat_days,selection_weight` |
| `ReputationBalanceData.csv` | 5 | `idx,min_reputation,max_reputation,normal_weight,price_sensitive_weight,wealthy_weight,hasty_weight,poor_weight,recovery_rate,settlement_min_score,settlement_max_score,settlement_delta` |
| `ResourceData.csv` | 54 | `idx,path` |
| `TextData.csv` | 200 | `idx,text` |
| `Customer/CustomerAppearanceData.csv` | 45 | `idx,nameidx,image_resource_idx` |
| `Customer/CustomerDispositionData.csv` | 15 | `idx,nameidx,preferred_product_types,preferred_selection_chance,min_product_kinds,max_product_kinds,min_quantity,max_quantity,price_tolerance,minimum_price_tolerance,entry_text_idxs,regular_sale_text_idxs,discount_sale_text_idxs,exploitative_sale_text_idxs,reject_text_idxs,queue_patience_seconds,queue_warning_textidx,queue_leave_textidx,disposition_type,preferred_product_idxs,regular_price_min_rate,regular_price_max_rate` |
| `Customer/ProductCategoryData.csv` | 7 | `idx,nameidx,product_type` |
| `Customer/ProductData.csv` | 16 | `idx,nameidx,product_type,is_available,base_price,available_day,image_resource_idx,cost_price,required_facility_idx,top_view_image_resource_idx` |

## 3. 공통 로딩·공개 경계

`DataTableManager`는 Addressables의 `Datas` label에서 CSV를 읽고 첫 데이터 행의 `idx`로 `DataTableType`을 결정한다. 17종 로더를 모두 등록한 뒤 다음 순서로 교차 FK를 확인하고, 성공한 테이블만 `Commit`한다.

- 손님 묶음: `CustomerCatalog.ValidateAndCommit`이 Appearance/Disposition/ProductCategory/Product 및 Text/Resource/Facility를 연결한다.
- 가격 사건: manager의 `validatePriceEvents`가 PriceEvent의 Text/Product와 Schedule의 event FK를 검사한다.
- 도덕성: `MoralityDataTable.Validate`가 데이터에 등장한 성향 타입이 CustomerDisposition에 존재하고 수락/거절별 구간이 유일한지 검사한다.
- 감독관: Text/Resource/Facility FK를 검사한다.
- 딸: Dialogue→Text, Appearance→Resource FK를 검사한다.

오류가 하나라도 나면 `isLoaded`를 성공으로 공개하지 않는다. 다만 이번 감사에서는 실제 Addressables label·asset 로드나 commit 성공을 실행하지 않았다.

## 4. P1 데이터 흐름과 계약

### 4.1 상품·분류·손님·거래·대기열

| CSV/값 | DTO·로더 | 소비자 | 타입·단위·nullable·범위 | FK |
|---|---|---|---|---|
| Product | `ProductData` → `ProductDataTable` → `CustomerCatalog` | `CustomerProductAvailability`, `CustomerCompositionSelector`, `CustomerVisit`, 가격 사건, 설비 해금 | PK/FK `uint`; `is_available` 0/1; 가격/원가 양수; `available_day` 경과일 기준 | `nameidx→Text`; `product_type→ProductCategory.product_type`; `image_resource_idx`·`top_view_image_resource_idx→Resource`는 스키마 허용 빈값; `required_facility_idx→Facility` 허용 빈값 |
| ProductCategory | `ProductCategoryData` → table → catalog | selector, 가격 사건의 분류 대상 | `product_type` 숫자 enum, 중복 분류 금지 | `nameidx→Text`; 각 Product의 type이 존재해야 함 |
| CustomerAppearance | `CustomerAppearanceData` → table → catalog | selector → `CustomerComposition` → UI/prefab | PK `uint`; 이미지 필수 | `nameidx→Text`, `image_resource_idx→Resource` |
| CustomerDisposition | `CustomerDispositionData` → table → catalog | selector → composition → generator → `CustomerVisit.SubmitOffer`; queue | 확률 `preferred_selection_chance` 0~1000; 수량/종류 양수 범위; 가격 배율 1000=100%; `minimum_price_tolerance` 0~1000; 대기 `queue_patience_seconds` 양수 초; enum은 `Normal=1,Hasty=2,PriceSensitive=3,Wealthy=4,Poor=5` | `nameidx`, 5종 대사 배열, queue warning/leave → Text; preferred product types → ProductCategory; preferred product idxs → Product |
| 거래 결과 | CSV 없음 | `CustomerVisit` → `TransactionResult` → `DayProgress` → `GameSessionManager`/`DailyAggregationService` | 제시액 `long>0`; 기준합계/허용합계 `long`; overflow checked; 결과 enum은 Discount/Regular/Exploitative/PaymentRefused | 최종 `SaleItem.productIdx→Product`; 제출 시 현재가 사전의 같은 PK |
| Queue | disposition의 인내/대사만 CSV | `CustomerQueue` → `DayProgress` | Capacity 10, Arrival 5초, Speech 3초는 코드 상수; 인내는 행별 초 | queue 대사 → Text |

성향 매핑의 주의점:

- 기획의 절박은 현재 enum `Hasty=2`에 대응한다. CSV 6002가 Hasty이고 의료(type 3)를 선호한다. enum rename은 저장/CSV/API migration이므로 이번 값 입력에 섞지 않는다.
- 15행은 타입당 하나가 아니다: Normal 4행(6001, 6004~6006), Hasty 1행(6002), PriceSensitive 3행(6003,6008,6009), Wealthy 3행(6007,6010,6011), Poor 4행(6012~6015). 확정 상한은 타입별 모든 행에 일관되게 입력해야 한다.
- 확정 수락식은 현 코드로 표현 가능하다. 일반 4타입은 `minimum_price_tolerance=0`; PriceSensitive만 `price_tolerance=1000`, `minimum_price_tolerance=1000`이며 코드가 `P != B`를 별도 거절한다. `regular_price_min_rate=max_rate=1000`은 수락 상한과 별개로 정가 결과를 정확히 100%에만 배정한다.
- 현재 코드는 비율을 반올림하지 않고 교차곱으로 비교한다. 일반 상한은 정수 허용액으로 내림한다. 따라서 B=1000 경계 예시는 1300/1301, 1150/1151, 1500/1501, 1800/1801로 직접 검사할 수 있다.

### 4.2 가격 이벤트

`PriceEventData.csv` → `PriceEventData`/table → manager FK 검증 → `PriceEventScheduler.CreateDay/ApplyRadio` → `DailyPriceState.Prices` → 손님 생성 및 제출 시 가격 조회.

- `nameidx`, `descriptionidx→Text`; `product_idxs→Product`; `product_types`는 숫자 `ProductType` enum이다. 두 대상 배열은 각각 중복 불가이며 효과가 있으면 대상이 하나 이상 필요하다.
- `change_type`: 0 None, 1 Rate, 2 Amount. Rate의 `change_value`는 1000=100%인 부호 있는 `int`이며 -1000 미만은 거부한다. Amount도 부호 있는 정수 금액이다.
- 여러 효과는 기본가에서 rate/amount를 합산하고 `floor(base*(1000+rate)/1000)+amount`, 최소 1로 계산한다. 매번 기본가에서 재계산하므로 누적 적용하지 않는다.
- Schedule의 `event_idx→PriceEvent`; `channel` enum(신문/라디오); `end_day` nullable; `repeat_days` 0은 지정일 1회이며 라디오는 0만 허용; `selection_weight`는 양수 상대 가중치이고 합계 1000 확률이 아니다.
- 라디오 후보 선택과 효과 적용은 구현돼 있으나 방송 대기 `0~60초 미만`은 `GetRadioDelaySeconds` 코드 상수 계산이며 CSV 입력 대상이 아니다.

### 4.3 설비

`FacilityData.csv` → `FacilityData`/table → `FacilityService` → `GameSessionManager`/`GameProgress`/상점 UI; Product의 `required_facility_idx`가 상품 해금 소비자다.

- `idx uint`, `nameidx→Text`, `purchase_price long>0`.
- `upgrade_kind` 숫자 enum: ProductUnlock/Convenience/StoreStage. 종류별로 불필요한 `effect_type`/`target_store_stage`는 0이어야 한다.
- `required_store_stage`는 1~3; StoreStage는 현재 단계의 다음 단계만 구매 가능. Convenience 효과와 stage target은 테이블 내 중복을 막는다.
- ProductUnlock 설비는 Product 쪽 역참조가 실제 하나 이상 있어야 하며, Product가 StoreStage/Convenience 설비를 해금 FK로 쓰면 실패한다.
- 구매 가격·조건·연결 상품은 기존 컬럼 값 변경/행 추가로 조정 가능하다. 새 `upgrade_kind`, 새 편의 효과 동작, 실제 가판 외형 변화, 신규 상품 이미지/Resource 연결은 코드·리소스 작업이다.

### 4.4 거래 도덕성

`MoralityData.csv` → `MoralityData`/table → `MoralityCalculator` → `CustomerVisit.Result.MoralityDelta` → `DailyAggregationService(decimal)` → `GameSessionManager.currentMorality(decimal)` → 딸 대화/엔딩 소비자 후보.

- 가격 구간 `offer_min_rate/offer_max_rate`는 `uint`, 1000=100%. `offer_max_rate=0`만 무상한이며 이때 `include_max=false`여야 한다. 같은 min/max는 양끝 포함만 허용한다.
- 점수는 `decimal`; 누적도 `decimal`; 계산은 `offered*1000`과 `reference*boundary` 교차곱이므로 소수 가격비율 경계도 표시 반올림 없이 분류한다.
- `is_accepted`는 0/1이며 거래 결과에서 확정된 수락 여부와 함께 lookup한다. 동일 성향/수락 여부/비율이 정확히 한 행에만 맞아야 한다.
- 현재 데이터 문제: Normal의 1100~1300 행(14006~14007)이 거절로 기록돼 있어 확정 Normal 1300 수락과 충돌한다. Hasty의 1300~1500 행(14015~14016)이 거절로 기록돼 있어 확정 절박 1500 수락과 충돌한다. Poor/Wealthy 규칙 행은 없다.
- 확정표를 문자 그대로 담는 데 필요한 행은 Normal 8 + PriceSensitive 3 + Poor 8 + Hasty/절박 9 = 28행이다(Wealthy 별도 도덕성 표는 제공 스냅샷에 없음). 현재 예약 `14001~14020`만으로 부족하다. 새 ID 범위를 먼저 승인·기록한 뒤 행 추가와 관련 고정-count 테스트를 함께 바꿔야 한다.

### 4.5 감독관

`InspectorEventData.csv` → `InspectorEventData`/table → `InspectorEventService` → `GameSessionManager.EnsureInspectorDay` → `DayProgress.InspectorEvent` → Presenter.

- `day`, `required_facility_idx`, `min_store_stage`는 nullable이며 0은 허용하지 않는다. `min_store_stage`는 1~3.
- `priority int`; `repeat_mode` 숫자 enum 1 OncePerSession / 2 OncePerDay; `dialogue_text_idxs`는 순서 있는 Text FK 배열이며 중복은 허용; portrait는 필수 Resource FK.
- FK: `nameidx`와 모든 dialogue→Text, portrait→Resource, optional required facility→Facility.
- 실제 15003은 day=2, Text 8180/8181, Resource 4201을 사용하며 파서·서비스는 정상 대역의 추가 행을 수용한다. 다만 권위 ID 문서가 15001~15002만 예약하므로 데이터 수정 전에 승인 근거를 확인한다.

### 4.6 딸 대화·이미지

`DaughterDialogueData.csv`/`DaughterAppearanceData.csv` → 각 DTO/table → `DaughterDialogueService.Select(day, morality)` → 정산 Presenter.

- Dialogue PK `uint`; `morality_min/max decimal?`; 하한 포함/상한 미포함. 첫 하한과 마지막 상한은 null이어야 하고 모든 구간이 빈틈·겹침 없이 맞닿아야 한다. 0은 양수 구간의 포함 하한이어야 한다. `text_idxs`는 중복 없는 필수 Text FK 배열.
- 현재 6행은 `(-∞,-20),[-20,-10),[-10,0),[0,10),[10,20),[20,+∞)`이고 각 3개 대사를 가진다. 코드는 6행을 고정하지 않으므로 확정 경계가 나오면 행 추가/경계 변경이 가능하지만 새 Dialogue ID와 Text ID가 필요하다.
- Appearance는 `start_day uint>0` 고유, 첫 행은 1일이어야 한다. 서비스는 `start_day<=day` 중 마지막 행을 선택하므로 31일도 마지막 이미지로 조회 가능하다. 현재 1/11/21일 세 행이 모두 Resource 4201이라 실제 그림은 같다.
- 제공 딸 문서는 음·양 각 3단계(현재 6행)와 단계별 약 3대사를 말하지만, 최신 거래·도덕성 문서는 긍정·부정 각 5단계 방향과 경계 미정을 말한다. 행 확장은 기술적으로 가능하되 기획 충돌이 해소되기 전 값/ID를 발급하면 안 된다.

### 4.7 연결 데이터와 비CSV 값

- Maintenance: `day int>0`, `maintenanceAmount long>0`, 1부터 중간 누락 없이 연속. 서비스 배열 길이가 유효 마지막 날이므로 31일은 행 추가 전 지원되지 않는다.
- ReputationBalance: 성향 타입별 상대 가중치가 별도 열이다. 성향 행 수가 등장 확률이 아니며 Hasty 열이 기획 절박에 대응한다. 정확한 새 구성값은 본 감사에서 확정하지 않는다.
- DailyGuideline: 2행뿐이며 enum rule/허용 수량/벌금의 현재 구현값이다. 가격·상품 확정 후 생성되고 거래 수락 시 위반을 기록하지만, 기획의 모든 지침 콘텐츠·벌금/미납 흐름이 완료됐다는 뜻은 아니다.
- EconomyBalance: 시작 잔액 1행. 설비·유지비 회수 가능성과 함께 조정하되 다른 담당 경제 설계를 재정의하지 않는다.
- 비CSV: 영업 30초, queue 10명/5초/3초, 라디오 60초 범위. 실제 하루 길이를 데이터로 운영하려면 별도 설정 계약이 필요하다. 단 한 값만 조정할 계획이면 새 CSV보다 기존 생성 지점에서 명시적으로 전달하는 작은 코드 변경이 우선이다.

## 5. 입력만 가능한 항목 / 별도 구현이 필요한 항목

### 기존 값 수정 또는 승인된 행 추가로 가능

- 15개 성향 행의 타입별 `price_tolerance`와 `minimum_price_tolerance`, 정가 범위, 선호/주문량/인내/대사 FK 값.
- Product의 가격·원가·해금일·설비 연결, ProductCategory 값, Appearance/Text/Resource의 기존 승인 범위 내 콘텐츠 연결.
- PriceEvent 효과·대상과 Schedule 날짜·채널·상대 가중치.
- Facility 가격·단계·기존 enum 효과와 Product 역참조.
- 승인된 30031 ID가 있으면 Maintenance 31일 행 추가.
- 승인된 ID/Text/Resource가 있으면 Inspector·Daughter 행 추가. 로더는 현재 행 수를 고정하지 않는다.

### 코드·스키마·리소스 또는 선행 승인 필요

- 도덕성 확정표 28행 이상 수용: Morality ID 예약 확대가 선행. CSV 행과 고정 20행 기대 테스트 수정 필요. enum/스키마 변경은 불필요하지만 ID 권위 변경은 필요하다.
- `Hasty`를 코드상 `Desperate`로 rename: 데이터/저장/API migration이므로 값 입력과 분리. 지금은 매핑 문서화가 최소 해법이다.
- 딸 긍정·부정 각 5단계가 최종 확정되면 6행 예약 확대 및 Text 콘텐츠 추가. 이미지 변화에는 새 Sprite, ResourceData 행, Addressables/GUID 연결과 리소스 승인 필요.
- 감독관 15003의 사후 승인 또는 ID 조정. 현재 행을 임의 삭제/재번호화하지 않는다.
- 31일 전체 게임 흐름·최종 정산/시민권 순서 및 매일 상납·3일 유예는 Maintenance 31번째 값만으로 구현되지 않는다.
- 영업시간/queue/radio를 데이터 조정값으로 만들려면 소비자 계약 변경이 필요하다. 확정 수치가 하나면 새 범용 설정 시스템은 만들지 않는다.
- 새 Facility convenience 동작이나 실제 외형 변화, 신규 상품 이미지와 Addressables는 코드/Prefab/리소스 작업이다.

## 6. 작은 구현 인계 항목

직접 구현하거나 새 작업을 만들지 않는다. 아래 항목은 영향과 파일 후보, 완료 검증을 갖춘 최소 단위다.

1. **성향 확정 상한 데이터 반영**
   - 영향: 거래 수락/거절, 도덕성 `is_accepted` 일치성, 명성별 생성 가능 타입.
   - 허용 파일 후보: `Assets/Datas/Customer/CustomerDispositionData.csv`, 필요 시 기대값만 `Assets/Tests/EditMode/CustomerCsvTests.cs`, `CustomerContractTests.cs`.
   - 검증: 타입별 모든 행의 상한 일치; B=1000에서 1300/1301, 1150/1151, 1500/1501, 1800/1801; PriceSensitive 999/1000/1001; 정가/저가/착취 분류.

2. **도덕성 표·ID 확장**
   - 영향: 거래별 점수, 누적 decimal, 딸/엔딩 분기. 기존 14001~14020 참조 안정성.
   - 선행: CSV 종류 ID 원본에서 추가 Morality 개별 ID 예약 및 Poor/Wealthy 점수 정책 확인.
   - 허용 파일 후보: `Assets/Datas/MoralityData.csv`, `Assets/Tests/EditMode/MoralityTests.cs`; 코드 변경은 최신 로더가 실제 확정표를 거부할 때만.
   - 검증: 모든 성향의 수락/거절 구간 무공백·무겹침; 확정 경계; `-0.25/-2.25/-6.75` 누적 보존; 거래당 1회; 미등록 성향 정책.

3. **31일 유지비 행과 최종일 흐름 분리**
   - 영향: MaintenanceService day 31 조회/납부. 게임 종료·유예는 별도 기능.
   - 허용 파일 후보: 값 입력은 `Assets/Datas/MaintenanceBalanceData.csv`; 기대값은 기존 finance/EditMode 테스트.
   - 검증: 1~31 연속, 31일 금액 조회/1회 납부, 30→31 순서, 32 거부. 최종일 게임 흐름은 별도 acceptance로 남김.

4. **감독관 15003 예약 정합화**
   - 영향: ID 권위/병합 충돌, 기존 day 2 이벤트 참조.
   - 허용 파일 후보: 원본 CSV 종류 ID 문서 또는 승인에 따른 Inspector CSV/관련 Text·테스트. 이 감사 범위에서는 모두 수정 금지.
   - 검증: 승인 기록, PK 중복/대역, Text/Resource FK, day 2 1회 실행. 기존 ID를 무근거 재번호화하지 않음.

5. **딸 단계·이미지 콘텐츠 확장**
   - 영향: 정산 표현과 Resource/Addressables.
   - 선행: 6단계와 10단계 방향 중 최종 단계 수·경계, 0점 반응, 새 Dialogue/Resource ID.
   - 허용 파일 후보: 두 Daughter CSV, TextData/ResourceData, Sprite와 `.meta`, 관련 Addressables(보호 승인 후), `DaughterDialogueTests.cs`.
   - 검증: decimal 경계 양쪽과 0, 전 구간 연속, 각 Text FK, 1/11/21/31 이미지, 서로 다른 실제 Sprite 필요 시 resource가 실제로 달라지는지.

6. **비CSV 시간값 결정**
   - 영향: 하루 처리량·수익, queue 이탈, 라디오가 30초 기본 영업 안에 늦게/전혀 방송되는 체감.
   - 허용 파일 후보: `DayProgress` 생성 지점/설정 전달 경계, `CustomerQueue.cs`, `PriceEventScheduler.cs`, 관련 기존 테스트. 새 CSV는 반복 조정 요구가 확정될 때만 검토.
   - 검증: pause 제외 경과시간, 긴 프레임, 30초보다 긴 radio delay 처리 정책, queue 정원/입장/인내, 마감 당시 현재 거래 완료.

## 7. 기존 테스트와 실행하지 않은 최소 시나리오

기존 경로:

- `Assets/Tests/EditMode/CustomerCsvTests.cs`
- `Assets/Tests/EditMode/CustomerContractTests.cs`
- `Assets/Tests/EditMode/CustomerQueueTests.cs`
- `Assets/Tests/EditMode/CustomerCompositionSelectorTests.cs` 및 Integration/Composition 계열
- `Assets/Tests/EditMode/PriceEventTests.cs`
- `Assets/Tests/EditMode/FacilityTests.cs`
- `Assets/Tests/EditMode/MoralityTests.cs`
- `Assets/Tests/EditMode/InspectorEventTests.cs`
- `Assets/Tests/EditMode/DaughterDialogueTests.cs`
- 경제/진행 관련 EditMode 및 실제 수명 경계의 PlayMode suite

값 입력 후 최소 경계 묶음:

- 거래: 타입별 확정 상한 바로 아래/정확 경계/1원 초과, PriceSensitive 999/1000/1001, 1원 이상 저가 수락, 정가 범위 정확히 1000.
- 도덕성: 모든 표의 포함/제외 경계, 거절 우선, 아이/노인 소수점, 여러 거래 누적, 딸 0 및 각 구간 경계.
- FK: Product↔Facility, 성향 선호 type/product, 모든 대사→Text, 모든 이미지/portrait→Resource, Schedule→Event, Event→Product/Text.
- 기간: PriceEvent 31일 후보, Daughter image 31일 fallback, Maintenance 31일, 최종일 완료 순서.
- 비CSV: 30초 기본 영업에서 라디오 0/경계 직전/영업보다 늦은 예약, queue 장프레임·pause·마감.

이번에는 어떤 테스트도 실행하지 않았다. 과거 문서의 통과 기록은 과거 기준 증거일 뿐 `c1d642…` 및 새 밸런스 값의 실행 PASS로 재사용하지 않는다.

## 8. 조정자 확인이 필요한 결정

- `Hasty`를 절박의 현 기술 매핑으로 유지할지, 별도 rename migration을 승인할지.
- Morality 개별 ID를 최소 28행 이상으로 확대할지와 Wealthy 도덕성 점수표의 존재/미평가 정책.
- 딸 대사는 현재 6단계 확정인지, 최신 방향인 음·양 각 5단계로 확장할지; 0점과 실제 경계.
- Inspector 15003을 기존 승인 누락으로 보완할지, 다른 예약 ID로 이관할지.
- 31일 유지비 금액과 최종일에 실제 납부를 수행하는 순서.
- 실제 하루 길이 및 라디오/queue 시간값을 고정 코드 인자로 둘지 데이터화할지.

## 9. Git·변경 범위

- 수정 파일: `doc/work/balancing-technical-audit.md`만 생성.
- stage/commit/push/merge: 모두 수행하지 않음.
- Unity/패키지/원본 checkout/Google 원본: 조작하지 않음.
