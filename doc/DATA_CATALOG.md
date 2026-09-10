# 기획용 데이터 카탈로그

> 2026-09-10 이미지 스키마·표·관련 CSV 원문을 갱신했다. 다른 기능은 9/9 관측 범위를 유지한다. 이미지 연결·검증 경계는 [IMAGE_RESOURCE_INTEGRATION.md](IMAGE_RESOURCE_INTEGRATION.md)를 따른다.

목차: [기준](#1-기준과-읽는-방법) · [로딩·형식](#2-원본에서-화면까지) · [CSV 스키마](#3-csv-스키마와-현재-값) · [현재 수치·계산](#4-현재-테스트-데이터의-기획-의미) · [Enum](#5-enum-값-전체) · [런타임 계약](#6-런타임-데이터결과-계약) · [UI 데이터](#7-ui용-데이터와-표시-한계) · [구형·저장 모델](#8-남아-있는-구형-모델과-저장-데이터) · [명칭 비교](#9-이름이-같거나-비슷한-값의-구분) · [검증 범위](#10-확인-범위와-후속-판단) · [전체 CSV 원문](#부록-a-전체-csv-원문-스냅샷)

## 1. 기준과 읽는 방법

- 조사 기준: 2026-09-09, `total_merge` 설비65888e1·명성6976218 통합. Git 배포 여부는 커밋·푸시 결과로 별도 확인한다.
- 목적: 기획자가 현재 수치와 데이터 구조를 검토할 수 있도록 실제 저장소를 설명한다. 신규 기능 기획이나 ID 예약표가 아니다. ID 배정·예약 권위와 변경 절차는 [CSV_RULES.md](CSV_RULES.md), 데이터 작업은 [DATA_RULES.md](DATA_RULES.md)를 따른다. 아래 숫자 ID는 현재 코드·파일의 **관측 스냅샷**이며 새 번호를 배정하지 않는다.
- 범위: `Assets/Datas/`의 CSV 12종, 컬럼 총 76개(테이블별 중복 컬럼 포함), 데이터 282행. 관련 DTO·DataTable·enum·생성/판정/경제 소비자, 공유 UI의 입력·결과·직렬화 조작값, 남아 있는 구형 데이터와 저장 모델을 포함한다.
- 제외: vendor/Plugins, Unity·패키지·렌더러 기술 설정 전체, 테스트 fixture 데이터, Git 제외 Local 실험 값. UI 모든 색상·폰트·좌표를 나열하는 아트 규격은 아니며 거래 조작과 시간에 영향을 주는 값은 포함한다.
- **확인**: 실제 CSV·코드·prefab에서 확인한 내용. **해석**: 코드 계산으로부터 도출한 의미·예시. **미확인**: 실제 에셋 로드·화면 조작 등 이번 문서 조사에서 실행하지 않은 내용.
- **현재 연결**은 GameUI.prefab → GameUIController → GameProgress/DayProgress → GameSessionManager 경로에 호출이 있다는 뜻이다. 이번 문서 작업의 런타임 PASS를 뜻하지 않는다. **독립 API**는 구현이 있지만 현재 UI 경로에서 호출하지 않는 기능, **구형/미연결**은 남은 모델을 의미한다.

## 2. 원본에서 화면까지

1. [DataTableManager](../Assets/Scripts/Manager/DataTableManager.cs)가 `Datas` 라벨의 TextAsset을 가져온다. 후보가 없을 때만 기존 Resources/Editor fallback을 검사한다.
2. 첫 데이터 행의 `idx / 1000`으로 로더를 선택한다. 파일명은 오류 문맥이며 종류 판정 키가 아니다. 각 테이블이 header·타입·중복·범위를 검사한다.
3. [CustomerCatalog](../Assets/Scripts/Customer/Data/CustomerCatalog.cs)가 상품·성향·외형·분류·Text FK를 검사한다. 가격 이벤트 FK는 DataTableManager가 검사한다. 소비자는 전체 로딩 완료를 기다린다.
4. [GameSessionManager](../Assets/Scripts/Manager/GameSessionManager.cs)가 경제 상태·0부터 시작하는 경과일·당일 현재가를 소유한다. [GameProgress](../Assets/Scripts/Progress/GameProgress.cs)와 [DayProgress](../Assets/Scripts/Progress/DayProgress.cs)는 이를 통해 진행한다.
5. [CustomerCompositionSelector](../Assets/Scripts/Customer/CustomerCompositionSelector.cs)가 명성·성별 교대·설비 상태로 손님 구성 snapshot을 선택하고, [CustomerGenerator](../Assets/Scripts/Customer/CustomerGenerator.cs)가 이를 [CustomerVisit](../Assets/Scripts/Customer/CustomerVisit.cs)으로 변환한다. `CustomerVisit`은 최종 판매 목록과 제출 당시 현재가로 판정한다.
6. [GameUIController](../Assets/Scripts/Scene/GameUIController.cs)와 [ProgressViewDataFactory](../Assets/Scripts/UI/ProgressViewDataFactory.cs)가 표시용 값을 전달한다. 표시 문자열은 데이터의 내부 식별자가 아니다.

### 공통 형식·제약

- CSV는 쉼표 구분이며 [Util.GetCsvConfiguration](../Assets/Scripts/Utils/Util.cs)은 InvariantCulture와 header 소문자 비교를 사용한다. ResourceDataTable은 별도 InvariantCulture 기본 설정이다. 실제 header 이름·순서를 그대로 유지한다.
- 아래 스키마 표는 **CSV 순서**다. C# 선언 순서는 달라도 `[Name]` mapping을 따른다.
- `uint`: 0~4,294,967,295, `int`: -2,147,483,648~2,147,483,647, `long`: 부호 있는 64비트. 각 컬럼의 더 좁은 업무 제한이 우선한다.
- 필수 숫자 셀은 빈값을 허용하지 않는다. DTO initializer가 있더라도 누락 header·빈 셀의 CSV 기본값 정책으로 해석하지 않는다. nullable 필드와 명시적 배열 converter만 아래 예외를 따른다.
- `UIntArrayConverter`: 빈 셀 → 빈 배열, 원소 구분은 `_`; 0·중복·FK 제약은 각 DTO/catalog에서 검사한다. 상품 분류 converter는 숫자만 받으며 None/미정의 분류를 거부한다.
- `ZeroOneBooleanConverter`: 정확히 `0` 또는 `1`만 허용한다. 문자열 true/false는 쓰지 않는다.
- PK는 원칙상 해당 종류의 내부 ID 1~999이며 중복 금지. 경제 두 로더는 현재 `idx / 1000` 종류와 중복만 검사하고 내부번호0을 별도로 거부하지 않는 **검증 차이**가 있다. 작성 규칙이 2000/3000을 허용한다는 뜻은 아니다.
- FK는 대상 행 존재가 필요하다. Resource 행 존재와 Addressables 실제 에셋 존재·타입 성공은 별도다.
- 오류 로그·예외 및 실행 검증 기준은 상위 규칙을 따른다. 이번 문서는 기존 로더를 수정하거나 모든 실패 사례를 새로 실행하지 않았다.

## 3. CSV 스키마와 현재 값

현재 값 칸은 실제 고유값을 요약한다. Text·Resource처럼 긴 원문은 부록의 전체 행이 권위 스냅샷이다. `빈 셀`은 문자열 "빈 셀"을 저장하라는 뜻이 아니다.

| CSV | 행 수 | 컬럼 수 | 연결 상태 요약 |
|---|---:|---:|---|
| [EconomyBalanceData](../Assets/Datas/EconomyBalanceData.csv) | 1 | 3 | 현재 데이터 경로 연결 |
| [MaintenanceBalanceData](../Assets/Datas/MaintenanceBalanceData.csv) | 12 | 3 | 현재 데이터 경로 연결 |
| [PriceEventData](../Assets/Datas/PriceEventData.csv) | 4 | 7 | 현재 데이터 경로 연결 |
| [PriceEventScheduleData](../Assets/Datas/PriceEventScheduleData.csv) | 5 | 7 | 현재 데이터 경로 연결 |
| [ResourceData](../Assets/Datas/ResourceData.csv) | 54 | 2 | 미사용72행 제거, 이미지54행 유지 |
| [TextData](../Assets/Datas/TextData.csv) | 115 | 2 | 현재 데이터 경로 연결 |
| [CustomerAppearanceData](../Assets/Datas/Customer/CustomerAppearanceData.csv) | 45 | 3 | 현재 데이터 경로 연결 |
| [CustomerDispositionData](../Assets/Datas/Customer/CustomerDispositionData.csv) | 7 | 21 | 구매 연결 / queue 독립 API |
| [ProductCategoryData](../Assets/Datas/Customer/ProductCategoryData.csv) | 7 | 3 | 현재 데이터 경로 연결 |
| [ProductData](../Assets/Datas/Customer/ProductData.csv) | 22 | 10 | 현재 데이터 경로 연결 |
| [FacilityData](../Assets/Datas/FacilityData.csv) | 5 | 3 | 세션 구매·다음날 해금·정산 상점 UI 연결 |
| [ReputationBalanceData](../Assets/Datas/ReputationBalanceData.csv) | 5 | 12 | 거래 명성 계산·정산 피드백 연결, 생성 가중치는 미연결 |

### ReputationBalanceData

종류11, PK11001~11005. 모든 열은 필수 숫자다. `idx:uint`, 나머지는 `int`: `min_reputation,max_reputation`(-100~100 구간), `normal_weight,wealthy_weight,hasty_weight,special_weight`(각 비음수·합1000), `recovery_rate`(1000 이상), `settlement_min_score,settlement_max_score`(0~100 구간), `settlement_delta`(-15~10). 다섯 행이 명성·정산 점수 전체 범위를 각각 중복·공백 없이 덮는다. 상세 현재 값과 생성 연결 보류는 [명성 인계서](REPUTATION_CUSTOMER_GENERATOR_HANDOFF.md)를 따른다.

현재 명성·반영일 marker·거래/정산 로그는 세션 소유이며 화면 재생성으로 초기화하지 않는다. DayProgress의 시작 snapshot으로 명성을 계산하고 날짜 완료 직후 한 번 적용한다. 실제 손님 생성에는 아직 해당 가중치를 적용하지 않는다.

### FacilityData

`idx:uint`는 종류12의 12001~12005, `nameidx:uint`는 TextData FK(8056~8060), `purchase_price:long`은 양수 통화다. header·중복·대역·가격·Text FK 검사 후 공개한다. 통합 로더에는 ReputationBalance11과 Facility12가 모두 등록된다.

독립 구매이며 선행 설비가 없다. 보유·활성일은 세션 소유, 지불 즉시 차감하고 현재 경과일+1에 해금한다. 상세 API와 임시 수치는 [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md)를 따른다.

근거: [CSV](../Assets/Datas/FacilityData.csv), [DTO](../Assets/Scripts/Commons/Data/FacilityData.cs), [DataTable](../Assets/Scripts/Commons/Data/FacilityDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 설비 PK | 필수; 종류12·내부번호1~999·고유 | 상품 RequiredFacilityIdx 참조 대상 | 12001~12005 |
| 2. `nameidx` | NameIdx · uint | 설비 표시 이름 | 필수; 0·빈값·미존재 거부 | TextData.idx | 8056~8060 |
| 3. `purchase_price` | PurchasePrice · long | 1회 구매 가격 G | 필수; 양수 | 재정 차감 | 5000,7000,12000,15000,25000 |

### EconomyBalanceData

현재 연결. 정확히 1행. EconomySettings → EconomyRuntime/FinanceService의 시작 보유금, GameProgress의 상납일 계산에 사용한다.

근거: [CSV](../Assets/Datas/EconomyBalanceData.csv), [DTO](../Assets/Scripts/Finance/Data/EconomyBalanceData.cs), [DataTable](../Assets/Scripts/Finance/Data/EconomyBalanceDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 경제 설정 PK | 필수·0 금지; 종류/중복 검사, 내부번호0 검사 차이는 2절 | 없음 | 002001 |
| 2. `initialBalance` | InitialBalance · long | 새 게임 시작 보유금, 정수 G | 필수; 0 이상 | 없음 | 100000 |
| 3. `maintenanceCycleDays` | MaintenanceCycleDays · int | 상납 간격, 게임 일수 | 필수; 1 이상 | 없음 | 7 |

### MaintenanceBalanceData

현재 연결. 최소1행이며 paymentRound는1부터 연속이다. EconomySettings → MaintenanceService. 현12회차 이후는 마지막 금액 반복이 아니라 범위 오류다. 7일 주기이므로 표시7·14·…·84일차에 해당한다.

근거: [CSV](../Assets/Datas/MaintenanceBalanceData.csv), [DTO](../Assets/Scripts/Finance/Data/MaintenanceBalanceData.cs), [DataTable](../Assets/Scripts/Finance/Data/MaintenanceBalanceDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 상납 설정 PK | 필수·0 금지; 종류/중복 검사, 내부번호0 검사 차이는 2절 | 없음 | 003001, 003002, 003003, 003004, 003005, 003006, 003007, 003008, 003009, 003010, 003011, 003012 |
| 2. `paymentRound` | PaymentRound · int | 납부 회차, 1부터 | 필수; 양수·고유, 전체 1부터 연속 | 없음 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 |
| 3. `maintenanceAmount` | MaintenanceAmount · long | 해당 회차 상납액, 정수 G | 필수; 양수 | 없음 | 100000, 110000, 120000, 150000, 200000, 280000, 400000, 560000, 780000, 1100000, 1540000, 2150000 |

### PriceEventData

현재 연결. DataTableManager FK 검사 → PriceEventScheduler → GameSessionManager의 현재가. Text 제목/설명은 뉴스 전달용이다. 같은 사건이 두 채널에 잡히면 효과는 한 번만 적용한다.

근거: [CSV](../Assets/Datas/PriceEventData.csv), [DTO](../Assets/Scripts/Commons/Data/PriceEventData.cs), [DataTable](../Assets/Scripts/Commons/Data/PriceEventDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 가격 효과 사건 PK | 필수; 종류 대역·고유 | 없음 | 9001, 9002, 9003, 9004 |
| 2. `nameidx` | NameIdx · uint | 뉴스 제목 | 필수; 0 금지 | TextData.idx | 8042, 8044, 8046, 8048 |
| 3. `descriptionidx` | DescriptionIdx · uint | 뉴스 설명 | 필수; 0 금지 | TextData.idx | 8043, 8045, 8047, 8049 |
| 4. `product_idxs` | ProductIdxs · uint[] | 효과 대상 개별 상품 | 빈 배열 허용; 0·중복 금지; 효과가 있으면 두 대상 목록 중 하나 이상 필요 | ProductData.idx | 빈 셀, 1001 |
| 5. `product_types` | ProductTypes · ProductType[] | 효과 대상 분류; 개별 대상과 합집합 | 빈 배열 허용; None·중복·미정의 금지 | ProductType | 2, 빈 셀, 3 |
| 6. `change_type` | ChangeTypeValue · uint → ChangeType | 0 무효과 / 1 비율 / 2 정액 | 필수; 0~2, End 금지 | PriceChangeType | 1, 2, 0 |
| 7. `change_value` | ChangeValue · int | 부호로 상승/하락; Rate는1000=100%, Amount는G | 필수; Rate ≥ -1000, Amount는 int 범위. None이면0 및 대상 두 목록 모두 빈값 | 없음 | -200, 30, 0, 300 |

### PriceEventScheduleData

현재 연결. 채널별 날짜 후보에서 가중치로 최대1개를 고른다. 후보 없음과 무효과 사건 선택은 다르다. 라디오는 후보가 있으면 예약하되 실제 방송은 영업시간 경과 조건을 따른다.

근거: [CSV](../Assets/Datas/PriceEventScheduleData.csv), [DTO](../Assets/Scripts/Commons/Data/PriceEventScheduleData.cs), [DataTable](../Assets/Scripts/Commons/Data/PriceEventScheduleDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 선정 후보 스케줄 PK | 필수; 종류 대역·고유 | 없음 | 10001, 10002, 10003, 10004, 10005 |
| 2. `event_idx` | EventIdx · uint | 실행할 사건 | 필수; 0 금지 | PriceEventData.idx | 9001, 9003, 9002, 9004 |
| 3. `channel` | ChannelValue · uint → Channel | 신문1 / 라디오2 | 필수; None/End/미정의 금지 | PriceEventChannel | 1, 2 |
| 4. `start_day` | StartDay · uint | 최초 후보 경과일, 시작일0 | 필수; 0 허용 | ElapsedDays와 비교 | 0, 3 |
| 5. `end_day` | EndDay · uint? | 후보 종료 경과일(포함) | 빈 셀=null 무기한; 숫자0은0일 종료; StartDay 이상 | 없음 | 빈 셀 |
| 6. `repeat_days` | RepeatDays · uint | 신문 반복 간격, 게임 일수 | 필수; 신문0=시작일1회, 양수=간격; 라디오는0만 | 없음 | 2, 4, 0 |
| 7. `selection_weight` | SelectionWeight · uint | 같은 채널·날짜 후보 사이 상대 가중치 | 필수; 양수. 선택 시 후보 총합 int.MaxValue 이하; 합1000 요구 없음 | 없음 | 1 |

### ResourceData

현재 리소스54행(4201~4254). 2026-09-10 코드·CSV·직렬화 자산의 FK·주소 소비 조사 후 미사용72행을 제거했다. 삭제 ID는 4001~4099의 기존65행 및 4101·4104·4106·4107·4109·4110·4199이며 재사용하지 않는다. 원본 자산·meta·Addressables는 보존한다. 삭제 등록은 기준 d7fa49d에서 복구할 수 있다. 상품7종의 기본·탑뷰 FK는 유지하며15종은 두 칸이 빈값이다.

근거: [CSV](../Assets/Datas/ResourceData.csv), [DTO](../Assets/Scripts/Commons/Data/ResourceData.cs), [DataTable](../Assets/Scripts/Commons/Data/ResourceDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 리소스 PK | 필수; 종류 대역·내부번호1~999·고유 | 이미지 등의 FK 대상 | 54개: 부록 |
| 2. `path` | Path · string | Addressables key (디스크 상대경로와 다를 수 있음) | 필수; null/빈값/공백만 금지; 문자열 명시 허용; Path 중복 별도 검사는 없음 | ResourceManager/Addressables | 전체54키: 부록 |

### TextData

현재 연결. 게임 전체 텍스트 원본이며 손님 전용 Text가 아니다. NameIdx·뉴스·대사 FK로 조회한다. 70개 실제 문자열은 부록에 전부 보존한다. 모든 UI 문구가 이미 TextData로 이전된 상태는 아니다.

근거: [CSV](../Assets/Datas/TextData.csv), [DTO](../Assets/Scripts/Commons/Data/TextData.cs), [DataTable](../Assets/Scripts/Commons/Data/TextDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 표시 문구 PK | 필수; 종류 대역·고유 | 다른 CSV의 Text FK 대상 | 115개: 부록 |
| 2. `text` | Text · string | 실제 이름·대사·뉴스 문구 | 필수; null/빈값/공백만 금지; 문자열 명시 허용 | 표시 소비자 | 전체115문구: 부록 |

### CustomerAppearanceData

현재 연결. 생성기는 외형 PK를 방문마다 선정한다. GameUIController가 필수 Resource FK를 로드하고 ProgressViewDataFactory는 Sprite를 전달한다. NameIdx는 검증되지만 현재 손님 화면은 외형 이름을 표시하지 않는다. 성별·연령·성향과 외형 선정은 독립이다.

근거: [CSV](../Assets/Datas/Customer/CustomerAppearanceData.csv), [DTO](../Assets/Scripts/Customer/Data/CustomerAppearanceData.cs), [DataTable](../Assets/Scripts/Customer/Data/CustomerAppearanceDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 외형 PK; 영구 손님 ID 아님 | 필수; 종류 대역·고유 | 없음 | 5001~5045 |
| 2. `nameidx` | NameIdx · uint | 외형 표시 이름 | 필수; 0·빈값 금지 | TextData.idx | 8001~8004,8075~8115 |
| 3. `image_resource_idx` | ImageResourceIdx · uint | 외형 Sprite | 필수; 0·빈값·Resource 대역·존재 검사 | ResourceData.idx → path → Sprite | 4201~4245 |

### CustomerDispositionData

구매·가격·대사·타입은 현재 연결. queue_*는 로딩/FK 검증되지만 CustomerQueue는 현재 GameUIController/DayProgress에 연결되지 않은 독립 API다. 이름은 검증되며 내부 성향 이름·허용액이 현재 UI에 자동 노출되는 것은 아니다.

근거: [CSV](../Assets/Datas/Customer/CustomerDispositionData.csv), [DTO](../Assets/Scripts/Customer/Data/CustomerDispositionData.cs), [DataTable](../Assets/Scripts/Customer/Data/CustomerDispositionDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 성향 설정 PK; 성향 타입과 별개 | 필수; 종류 대역·고유 | 없음 | 6001, 6002, 6003 |
| 2. `nameidx` | NameIdx · uint | 성향 표시 이름 | 필수; 0·빈값 금지 | TextData.idx | 8005, 8006, 8007 |
| 3. `preferred_product_types` | PreferredProductTypes · IReadOnlyList<ProductType> | 주로 고를 상품 분류 목록 | 빈 배열 허용; 숫자_배열, 0·중복·미정의 금지 | ProductType 및 Category 표시 행 | 1_2, 3, 4 |
| 4. `preferred_selection_chance` | PreferredSelectionChance · int | 선호군 선택 확률, 1000=100% | 필수; 0~1000. 양쪽 후보가 남을 때 적용 | 없음 | 900 |
| 5. `min_product_kinds` | MinProductKinds · int | 희망 목록 상품 종류 수 최소 | 필수; 1 이상, 최대 이하; 후보 수 부족 시 축소 | 없음 | 1 |
| 6. `max_product_kinds` | MaxProductKinds · int | 희망 목록 상품 종류 수 최대(포함) | 필수; 최소 이상, int.MaxValue 미만 | 없음 | 3 |
| 7. `min_quantity` | MinQuantity · int | 상품별 수량 최소 | 필수; 1 이상, 최대 이하 | 없음 | 1 |
| 8. `max_quantity` | MaxQuantity · int | 상품별 수량 최대(포함) | 필수; 최소 이상, int.MaxValue 미만 | 없음 | 3 |
| 9. `price_tolerance` | PriceTolerance · int | 현재가 합계에 대한 결제 허용 배율, 1000=100% | 필수; 양수, 1000 초과 허용; 확률 아님 | 없음 | 1300, 1500, 1000, 1400 |
| 10. `entry_text_idxs` | EntryTextIdxs · IReadOnlyList<uint> | 입장 대사 후보 | 필수 비어 있지 않은 _배열; 0·중복 금지 | TextData.idx | 8024_8025, 8030_8031, 8036_8037 |
| 11. `regular_sale_text_idxs` | RegularSaleTextIdxs · IReadOnlyList<uint> | 기준가 판매 대사 후보 | 필수 비어 있지 않은 _배열; 0·중복 금지 | TextData.idx | 8026_8027, 8032_8033, 8038_8039 |
| 12. `discount_sale_text_idxs` | DiscountSaleTextIdxs · IReadOnlyList<uint> | 저가 판매 대사 후보 | 필수 비어 있지 않은 _배열; 0·중복 금지 | TextData.idx | 8026_8027, 8032_8033, 8038_8039 |
| 13. `exploitative_sale_text_idxs` | ExploitativeSaleTextIdxs · IReadOnlyList<uint> | 착취 판매 대사 후보 | 필수 비어 있지 않은 _배열; 0·중복 금지 | TextData.idx | 8026_8027, 8032_8033, 8038_8039 |
| 14. `reject_text_idxs` | RejectTextIdxs · IReadOnlyList<uint> | 결제 거부 대사 후보 | 필수 비어 있지 않은 _배열; 0·중복 금지 | TextData.idx | 8028_8029, 8034_8035, 8040_8041 |
| 15. `queue_patience_seconds` | QueuePatienceSeconds · uint | 줄 합류부터 이탈까지 초 | 필수; 현재 재촉 잔여6초보다 커야 함 | 독립 CustomerQueue API | 12, 9, 18 |
| 16. `queue_warning_textidx` | QueueWarningTextIdx · uint | 재촉 대사 1회 | 필수; 0 금지 | TextData.idx | 8050, 8052, 8054 |
| 17. `queue_leave_textidx` | QueueLeaveTextIdx · uint | 이탈 불만 대사 1회 | 필수; 0 금지 | TextData.idx | 8051, 8053, 8055 |
| 18. `disposition_type` | DispositionTypeValue · uint → DispositionType | 성향 타입 선택 키 | 필수; 실제 enum 1~4, None/End/미정의 금지 | CustomerDispositionType | 1, 2, 3 |
| 19. `preferred_product_idxs` | PreferredProductIdxs · IReadOnlyList<uint> | 개별 선호 상품; 분류 선호와 OR | 빈 배열 허용; 0·중복 금지, 각 행 존재 | ProductData.idx | 빈 셀 |
| 20. `regular_price_min_rate` | RegularPriceMinRate · int | 기준가 판매 인정 하한 배율, 1000=100% | 필수; 0 < 값 ≤ 1000 | 없음 | 1000 |
| 21. `regular_price_max_rate` | RegularPriceMaxRate · int | 기준가 판매 인정 상한 배율, 1000=100% | 필수; 1000 이상; 결제 거부 상한과 독립 | 없음 | 1000 |

### ProductCategoryData

현재 연결. 분류 enum의 표시 이름 계약이며 전체4분류가 각1행 필요하다. 현재 가격표 본문은 상품 이름을 표시하고 분류 이름을 직접 표시하지 않는다. 분류 라벨을 조회할 수 있는 데이터 준비와 실제 화면 출력을 구분한다.

근거: [CSV](../Assets/Datas/Customer/ProductCategoryData.csv), [DTO](../Assets/Scripts/Commons/Data/ProductCategoryData.cs), [DataTable](../Assets/Scripts/Commons/Data/ProductCategoryDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 분류 표시 행 PK; 분류 숫자와 별개 | 필수; 종류 대역·고유 | 없음 | 7001, 7002, 7003, 7004 |
| 2. `nameidx` | NameIdx · uint | 분류 표시 이름 | 필수; 0·빈값 금지 | TextData.idx | 8008, 8009, 8010, 8011 |
| 3. `product_type` | ProductType · ProductType(uint) | 표시 이름을 연결할 enum 값 | 필수; 1~4 각 1행, 전체 분류 누락 금지 | ProductType | 1, 2, 3, 4 |

### ProductData

현재 연결. CustomerCatalog → 생성 후보/최종 거래/현재가/상품 UI. IsAvailable && AvailableDay <= ElapsedDays && (필요 설비 없음 또는 활성)인 상품이 생성·가격표 후보다. 현재 기본5·설비 해금13·비활성4행이며 모두0일 등장이다. 상품7종은 기본·탑뷰 FK가 연결됐고15종은 이미지 두 칸이 빈값이다. 원가는 결과에 기록만 되며 자동 지출이 아니다.

근거: [CSV](../Assets/Datas/Customer/ProductData.csv), [DTO](../Assets/Scripts/Commons/Data/ProductData.cs), [DataTable](../Assets/Scripts/Commons/Data/ProductDataTable.cs).

| 순서·컬럼 | C# 구성원·타입 | 의미·단위 | 빈값·0·검증 | FK/참조 | 현재 값 |
|---|---|---|---|---|---|
| 1. `idx` | Idx · uint | 상품 PK | 필수; 종류 대역·내부번호1~999·고유 | 없음 | 1001~1022 |
| 2. `nameidx` | NameIdx · uint | 상품 표시 이름 | 필수; 0·빈값 금지 | TextData.idx | 8012~8023, 8061~8070 |
| 3. `product_type` | ProductType · ProductType(uint) | 구매 선호와 이벤트·지침에 쓰는 분류 | 필수; 정의된 1~4, 숫자 converter | ProductCategoryData.product_type (enum 대응) | 1, 2, 3, 4 |
| 4. `is_available` | IsAvailable · bool | 판매 후보 활성 여부 | 필수; 정확히 0/1 | 없음 | 0, 1 |
| 5. `base_price` | BasePrice · uint | 상품 1개의 고정 기본가격, G | 필수; 양수 | 없음 | 100~5000, 전체 행은 부록 |
| 6. `available_day` | AvailableDay · uint | 등장 경과일, 시작일0 | 필수; 0 허용 | GameSessionManager.ElapsedDays와 비교 | 0 |
| 7. `image_resource_idx` | ImageResourceIdx · uint? | 기본 UI·계산대 이미지 | 두 이미지 열이 함께 빈값이면 흰색; 0·한쪽 누락·잘못된 FK 거부 | ResourceData.idx → Path → Sprite | 7종 FK/15종 빈값 |
| 8. `cost_price` | CostPrice · uint | 상품 1개의 원가, G | 필수; 양수, BasePrice 이하라는 제한 없음 | 없음 | 50~2500, 전체 행은 부록 |

| 9. `required_facility_idx` | RequiredFacilityIdx · uint? | 해금에 필요한 설비 | 빈 셀=null 기본상품; 0·대역 오류·미존재 거부 | FacilityData.idx | 빈 셀,12001~12005 |
| 10. `top_view_image_resource_idx` | TopViewImageResourceIdx · uint? | 탑뷰 상품 이미지 | 기본 이미지와 동일한 필수·FK 검사; 탑뷰 원본 없으면 기본 FK 명시 복제 | ResourceData.idx → Path → Sprite | 별도 탑뷰3종/기본복제4종/빈값15종 |

## 4. 현재 테스트 데이터의 기획 의미

### 상품·외형·성향

상품 종류 숫자는 PK가 아니다. 예를 들어 물의 상품 PK는1001, 분류 Water는1, 분류 표시 행은7001, 상품 이름 Text는8012다.

| 상품PK·이름 | 분류(enum) | 기본가격 G | 원가 G | 판매 조건 (등장일0 / 이미지 빈 셀) |
|---|---|---:|---:|---|
| 1001 물 | Water=1 | 100 | 50 | 기본 |
| 1002 정수 캔 | Water=1 | 150 | 75 | 비활성 |
| 1003 휴대용 필터 | Water=1 | 500 | 250 | 비활성 |
| 1004 통조림 | Food=2 | 250 | 125 | 기본 |
| 1005 분말 수프 | Food=2 | 150 | 75 | 설비 12001 |
| 1006 영양바 | Food=2 | 200 | 100 | 설비 12001 |
| 1007 붕대 | Medicine=3 | 300 | 150 | 기본 |
| 1008 소독제 | Medicine=3 | 350 | 175 | 비활성 |
| 1009 해열제 | Medicine=3 | 400 | 200 | 설비 12002 |
| 1010 건전지 | DailyNecessities=4 | 200 | 100 | 기본 |
| 1011 성냥 | DailyNecessities=4 | 100 | 50 | 기본 |
| 1012 방한포 | DailyNecessities=4 | 600 | 300 | 비활성 |
| 1013 연고 | Medicine=3 | 500 | 250 | 설비 12002 |
| 1014 응급 주사 | Medicine=3 | 900 | 450 | 설비 12002 |
| 1015 손전등 | Tools=5 | 800 | 400 | 설비 12003 |
| 1016 접이식 삽 | Tools=5 | 1000 | 500 | 설비 12003 |
| 1017 쇠지렛대 | Tools=5 | 1200 | 600 | 설비 12003 |
| 1018 무전기 | ElectricalEquipment=6 | 2000 | 1000 | 설비 12004 |
| 1019 배터리 | ElectricalEquipment=6 | 1200 | 600 | 설비 12004 |
| 1020 방독면 | ProtectiveEquipment=7 | 2500 | 1250 | 설비 12005 |
| 1021 방호복 | ProtectiveEquipment=7 | 4000 | 2000 | 설비 12005 |
| 1022 방사능 측정기 | ProtectiveEquipment=7 | 5000 | 2500 | 설비 12005 |

모든 상품의 현재 원가는 기본가격의50%로 입력되어 있다. 이는 **현 행 값의 관계**이지 `CostPrice <= BasePrice` 검증이나 자동 원가 계산식이 아니다. 가격 이벤트는 CostPrice를 바꾸지 않는다.

외형은5001~5045의 Female18/Male27 Sprite다. 표시 리소스는 가격·인내도·속성 보정이 아니며 기존 RGBA 열은 제거됐다.

| 성향 PK·표시명 | 타입 | 주 선호 | 선호군 선택 | 결제 허용 배율 | 기준가 인정 | 줄 합류 후 재촉 / 이탈 |
|---|---|---|---:|---:|---:|---|
| 6001 평범 | Normal=1 | Water·Food | 900=90% | 1300=130% | 1000~1000 | 6초 / 12초 |
| 6002 급함 | Hasty=2 | Medicine | 900=90% | 1500=150% | 1000~1000 | 3초 / 9초 |
| 6003 가격 민감 | PriceSensitive=3 | DailyNecessities | 900=90% | 1000=100% | 1000~1000 | 12초 / 18초 |
| 6004 공구 선호 | Normal=1 | Tools | 900=90% | 1300=130% | 1000~1000 | 6초 / 12초 |
| 6005 전기장비 선호 | Normal=1 | ElectricalEquipment | 900=90% | 1300=130% | 1000~1000 | 6초 / 12초 |
| 6006 보호장비 선호 | Normal=1 | ProtectiveEquipment | 900=90% | 1300=130% | 1000~1000 | 6초 / 12초 |
| 6007 부유한 손님 | Wealthy=4 | ElectricalEquipment·ProtectiveEquipment | 900=90% | 1400=140% | 1000~1000 | 9초 / 15초 |

- 희망 목록은 각 성향 모두1~3종류, 종류당1~3개이며 동일 상품은 한 항목으로 표현한다. 후보가 부족하면 종류 수를 줄인다.
- 선호 분류와 개별 상품은 OR다. 현재 개별 선호 상품 목록은 모두 비어 있다. 양쪽 후보군이 남아 있을 때만90%가 적용되고 한쪽 소진 시 남은 쪽에서 고르므로 최종 장바구니의 정확히90%가 선호 상품이라는 뜻은 아니다.
- 명성 출현은 하루 시작 명성의 `ReputationBalanceData` 가중치로 일반군(Normal·PriceSensitive), Wealthy, Hasty 구성군을 먼저 선택한 뒤 구성군 내부 타입과 같은 타입의 성향 행을 균등 선택한다. 현재 특수군 가중치는 0이며 매핑이 확정되기 전에는 양수 값을 거부한다. 명성 정산은 같은 타입의 여러 행 중 선호·대사와 무관하게 가격 규칙만 대표값으로 사용하며, 같은 타입 행의 가격 규칙이 다르면 데이터 오류로 거부한다. 외형은 전달된 후보에서 균등, 대사는 선택된 행의 후보 목록에서 균등 선택한다.
- 6004~6006은 기존 Normal 타입의 전문 선호 행이고 6007은 Wealthy 실제 행이다. 따라서 선호 행 증가는 명성 일반군의 타입 비율을 바꾸지 않는다.
- 손님 선호는 `ProductType`와 개별 `preferred_product_idxs`의 OR이며 날짜·활성·설비 필터 이후의 상품 후보에만 적용한다. 설비 상품은 Tools/ElectricalEquipment/ProtectiveEquipment로 분류되어 해당 타입 선호 손님이 설비 활성 뒤에만 해당 상품을 고를 수 있다.
- 성별2종과 연령3종은 독립 균등이며 외형·성향과 별개다. 성인은Adult=16, 특수 속성은 현재Normal=32 하나뿐이며 전체2×3×1=6조합이다. Normal만을 위해 난수를 추가 소비하지 않는다.
- 현재 같은 성향의 기준가·저가·착취 대사 목록이 동일하다. 판정이 달라도 문구가 같을 수 있다. 결제 거부는 별도 목록이다.
- 재촉 시점은 `queue_patience_seconds - 6`이다. 모든 성향이3초에 재촉하는 것이 아니라 가장 급한 성향이3초이며 다른 성향은 비례 조정된 값이다. 대기열은 독립 API이므로 현재 UI에서 위 시간이 흐른다는 보장은 없다.

### 가격 사건·스케줄

| 사건 PK·제목 | 대상 | 효과 | 현재 스케줄 |
|---|---|---|---|
| 9001 식료품 공급 확대 | Food=2 전체 | Rate -200 = -20% | 신문10001: 경과0일부터2일 간격, 끝없음 |
| 9002 생수 운송 지연 | 물1001만 | Amount +30G | 라디오10003: 경과0일부터 매일 후보, weight1 |
| 9003 지역 소식 | 없음 | None=0, 변화0 | 신문10002: 경과3일부터4일 간격 / 라디오10005 매일 후보 |
| 9004 의약품 수요 증가 | Medicine=3 전체 | Rate +300 = +30% | 라디오10004: 경과0일부터 매일 후보, weight1 |

- 신문·라디오를 독립 선정한다. 같은 이벤트를 가리키는 스케줄 여러 행이 후보에 있으면 그 사건의 상대 가중치가 커진다.
- 현재 라디오 후보3개의 가중치가 모두1이므로 각1/3 선택이다. 별도50%/100% 발생 상수는 없다. 무효과 사건9003 선택도 방송 예약이며, 후보 부재와 다르다.
- 신문 효과는 당일 준비 시 적용하고, 라디오는 영업 시작 후 무작위 대기 시간이 지나면 새 현재가로 교체한다. 현재 지연은 `(float)(Random.NextDouble() * 60)`초다. 의도는0~60초 미만이며 float 변환 경계에서는60이 될 수 있다.
- 현 GameUIController는 현재가 가격표를 사용하지만 신문 사건의 NameIdx/DescriptionIdx를 읽어 화면에 전달하는 호출은 없다. 신문 가격 효과 연결과 신문 기사 UI 연결을 구분한다. 라디오 제목·설명은 GameSessionManager의 방송 로그에서 사용한다.
- 현재 영업 기본값30초이므로 방송 후보가 있어도 방송 시각 전에 영업시간이 끝날 수 있다. pause와 Closing에서는 방송 시계를 늘리지 않는다. 방송 로그와 신문 화면 연결의 상세 상태는 [PRICE_EVENT_INTEGRATION](PRICE_EVENT_INTEGRATION.md)을 함께 확인한다.
- 효과는 해당 날짜 기본가격에서 다시 계산한다. 대상 상품 목록과 분류 목록은 합집합이며 같은 사건·상품에 두 번 적용하지 않는다.

계산식(현재 코드의 해석):

`현재 단가 = max(1, floor(BasePrice × (1000 + 적용 Rate 합) / 1000) + 적용 Amount 합)`

동일 사건의 두 채널 중복을 제거한 뒤 계산한다. 비율을 연속 곱하지 않는다. 결과는 uint 범위를 넘으면 예외다. 물1001은 기본100, 사건9002 방송 후130. Food 통조림1004는 기본250, 사건9001 적용 시200이다.

### 거래·정산 해석

`ReferenceTotal = Σ(최종 수량 × 제출 당시 현재 단가)`

`AllowedTotal = floor(ReferenceTotal × PriceTolerance / 1000)`

1. 제안 총액이 AllowedTotal 초과이면 결제 거부를 먼저 판정한다.
2. 그 외 제안액×1000이 ReferenceTotal×RegularPriceMinRate 미만이면 저가 판매.
3. ReferenceTotal×RegularPriceMaxRate 초과이면 착취 판매.
4. 두 경계 포함 범위는 RegularSale이다. 실제 `OutcomeLabel`은 “기준가 판매”다.

기준가 인정 범위를 넓혀도 허용 상한을 자동으로 늘리지 않는다. 현 PriceSensitive의1000 상한에서는 현재가 초과 거래가 착취 수락 대신 결제 거부가 된다. 총액 판정이며 상품별 제안 가격을 따로 입력하지 않는다.

최종 SaleItem은 최초 희망 목록과 달라도 API상 허용한다. 동일 상품 수량은 합산하며 양수·상품 존재·단가·원가를 검증한다. 생성 시 활성·등장일·설비 조건을 만족한 전체 후보 PK를 방문에 복사한다. 최종 제출은 이 스냅샷과 catalog·현재가를 검사하며, 희망 목록의 부분집합으로 제한하지 않는다. 방문 이후 해금된 상품은 새 방문부터 가능하다.

수락 결과는 offeredTotal이 SaleIncome이 된다. CostTotal은 판매한 상품 원가 합계를 **기록만** 하며 현재 보유금 차감이나 일일 지출에 반영하지 않는다. 지침 위반 기록 역시 결제를 취소하거나 명성·벌칙을 자동 계산하지 않는다. 거래 판정 완료와 재정 반영 완료는 별개이고, DayProgress는 재정 접수 실패 후 진행을 차단한다.

## 5. Enum 값 전체

아래는 데이터 및 관련 진행/표현 enum의 실제 값이다. `_End`는 자동 증가한 관측값이며 유효 데이터가 아니다. 종료 표식이 없는 enum에 임의로 새 항목을 추가하지 않았다.

| enum·근거 | 실제 이름=숫자 | 사용·제약 |
|---|---|---|
| [DataTableType : uint](../Assets/Scripts/Commons/Commons.cs) | None=0, Product=1, EconomyBalance=2, MaintenanceBalance=3, Resource=4, CustomerAppearance=5, CustomerDisposition=6, ProductCategory=7, Text=8, PriceEvent=9, PriceEventSchedule=10, ReputationBalance=11, Facility=12, DataTableType_End=13(자동) | idx/1000 로더 routing 관측값. None/End 로더 없음; 예약 권위 아님 |
| [ProductType : uint](../Assets/Scripts/Commons/Data/ProductType.cs) | None=0, Water=1, Food=2, Medicine=3, DailyNecessities=4, Tools=5, ElectricalEquipment=6, ProtectiveEquipment=7 | 상품·선호·이벤트·지침. None 거부, End 없음 |
| [CustomerDispositionType : int](../Assets/Scripts/Commons/CustomerProfileTypes.cs) | None=0, Normal=1, Hasty=2, PriceSensitive=3, Wealthy=4, CustomerDispositionType_End=5(자동) | CSV 원시 uint를 enum으로 해석,1~4만 허용 |
| [CustomerAttributes : int, Flags](../Assets/Scripts/Commons/CustomerProfileTypes.cs) | None=0, Male=1, Female=2, Child=4, Elderly=8, Adult=16, Normal=32 | bit OR. 성별·연령·특수 각각 최대1개. 실제 방문은 세 축 모두필수 |
| [PriceChangeType : int](../Assets/Scripts/Commons/Data/PriceEventData.cs) | None=0, Rate=1, Amount=2, PriceChangeType_End=3(자동) | CSV0~2만; None은 무효과 데이터로 유효 |
| [PriceEventChannel : int](../Assets/Scripts/Commons/Data/PriceEventScheduleData.cs) | None=0, Newspaper=1, Radio=2, PriceEventChannel_End=3(자동) | CSV1·2만 |
| [CustomerState : int](../Assets/Scripts/Customer/CustomerVisit.cs) | Entering=0, AwaitingOffer=1, Accepted=2, Rejected=3, Departed=4, Queued=5, Abandoned=6 | 방문 수명. Departed와 대기 만료 Abandoned 구분 |
| [CustomerTradeOutcome : int](../Assets/Scripts/Customer/CustomerVisit.cs) | None=0, RegularSale=1, DiscountSale=2, ExploitativeSale=3, PaymentRefused=4 | 가격 판정. 퇴장 후에도 유지 |
| [FinanceChangeReason : int](../Assets/Scripts/Finance/FinanceChangeReason.cs) | None=0, Sale=1, Maintenance=2, FacilityPurchase=3 | 재정 변경 사유. 현재 거래/상납이 각각1/2 |
| [FacilityPurchaseStatus](../Assets/Scripts/Facility/FacilityPurchaseResult.cs) | None=0, Purchased=1, AlreadyOwned=2, InsufficientFunds=3, FacilityPurchaseStatus_End=4(자동) | 정상 구매 결과; 입력·재진입·알림 오류는 예외 |
| [FacilityDisplayState](../Assets/Scripts/UI/Contracts/FacilityUIContracts.cs) | Available=0, InsufficientFunds=1, Pending=2, Active=3, FacilityDisplayState_End=4(자동) | UI 표시 상태, 구매 요청 결과와 별개 |
| [GameProgressState : int](../Assets/Scripts/Progress/GameProgressState.cs) | Initializing=0, DayInProgress=1, Maintenance=2, Failed=3, Completed=4 | 전체 진행. Completed 값 존재가 최종 목표 기능 구현을 뜻하지 않음 |
| [DayProgressState : int](../Assets/Scripts/Progress/DayProgressState.cs) | Initializing=0, PreOpen=1, Operating=2, Sorting=3, TransactionResult=4, Closing=5, Settlement=6, Completed=7 | 하루 진행. Closing은 마지막 거래 마감 |
| [GameDayPhase : int](../Assets/Scripts/UI/Contracts/UIContracts.cs) | PreOpen=0, PriceGuide=1, Operating=2, TradingResult=3, Closing=4, DailySettlement=5, Tribute=6 | UI 표시용. PriceGuide는 레거시 호환 |
| [SaleSortingItemView.SortingState : int](../Assets/Scripts/UI/SaleSortingItemView.cs) | Working=0, ForSale=1, Excluded=2 | 작업대 상품1개 분류 |
| [SaleSortingPanel.ViewState : int (private)](../Assets/Scripts/UI/SaleSortingPanel.cs) | Hidden=0, FrontWaiting=1, Transition=2, Pouring=3, Sorting=4, Locked=5 | 패널 표현 수명, CSV 저장값 아님 |
| [GameSceneManager.SceneName : int](../Assets/Scripts/Manager/GameSceneManager.cs) | Init=0, Hub=1, Main=2 | 공유 씬 routing, 개인 씬은 enum 없음 |
| [CashierPhase : int](../Assets/Scripts/UI/CashierSession.cs) | PriceGuide=0, Trading=1, Result=2, Settlement=3, Tribute=4, Goal=5, Failed=6 | 구형 CashierSession만 사용, 현재 진행 상태와 숫자 호환 아님 |

CustomerAttributes의 생성 가능한6조합(모두일반): 성인 남49(1|16|32), 성인 여50(2|16|32), 남아37(1|4|32), 여아38(2|4|32), 노인 남41(1|8|32), 노인 여42(2|8|32). `ValidateAttributes`는 None·부분조건도 허용하지만 `ValidateCompleteAttributes`와 실제 방문은 세 축 각각1개를 요구한다. SaleRestriction.RequiredAttributes에는 None0을 거부하며 `(방문 속성 & 필요 속성) == 필요 속성`의 AND로 검사한다. Adult16/Normal32 단독 지침도 가능하다.

기존 비트0/1/2/4/8은 유지했다. 성인이 연령 비트0인 이전 생성값은 더 이상 완전 프로필이 아니다. 현재 속성 저장·복원 경로는 없으므로 migration을 만들지 않았다. 특수속성 Wealthy/Poor는 없으며 기존 `CustomerDispositionType.Wealthy=4`와 속성은 별개다. 이 변경은 CSV·허용가격·선호 정책을 바꾸지 않는다.

## 6. 런타임 데이터·결과 계약

CSV 원본이 아니라 실행 중 생성·계산되는 값이다. 현재 구현 경로를 기준으로 적었다. DTO의 public setter가 있는 것과 소비자가 임의 변경해도 된다는 것은 다르다.

### 방문·판매·지침

근거: [CustomerVisit/CustomerOrderItem](../Assets/Scripts/Customer/CustomerVisit.cs), [SaleItem/SoldItem](../Assets/Scripts/Commons/SaleItem.cs), [SaleRestriction/Violation](../Assets/Scripts/Commons/SaleRestriction.cs), [TransactionResult](../Assets/Scripts/Finance/TransactionResult.cs).

| 계약 | 모든 데이터 구성원과 의미 | 현재 연결/초기·빈값 |
|---|---|---|
| CustomerVisit | AppearanceIdx:uint 외형PK, DispositionIdx:uint 설정PK, DispositionType:enum 타입, Attributes:flags 독립 속성, Items:IReadOnlyList<CustomerOrderItem> 희망 목록 | 생성 시 복사, 현재 연결. 같은 조합 재등장이 동일 인물 의미 아님 |
| CustomerVisit 가격 | PriceTolerance:int, RegularPriceMinRate:int, RegularPriceMaxRate:int (모두1000기준), BaseTotal:long? 최종 현재가 합계 별칭, AllowedTotal:long? 허용 상한, OfferedTotal:long? 제안 총액 | 배율은 생성 시 고정; 합계는 제출 전 null |
| CustomerVisit 결과·표현 | Result:TransactionResult? 미판정 null, State:CustomerState 기본 Entering, Outcome:CustomerTradeOutcome 기본 None, WasAccepted:bool? 미판정 null, OutcomeLabel:string 결과명, EntryTextIdx:uint 입장대사, FeedbackTextIdx:uint 현 결과 대사 | 대사는 생성 시 각 결과별 후보 선택. 퇴장 후 판정 유지 |
| CustomerOrderItem | ProductIdx:uint 상품PK, Quantity:int 희망수량, UnitPrice:uint 생성 시 현재가 | 현재 UI 장바구니 표시. 제출 단가와 시점 다름 |
| SaleItem | ProductId:uint 상품PK, Quantity:int 최종 판매수량 | UI→SubmitOffer 입력; default 값도 제출 경계에서 거부 |
| SoldItem | ProductId:uint 상품PK, Quantity:int 중복 합산 수량, UnitPrice:uint 제출 당시 단가, UnitCostPrice:uint 원가 | 양수 검증·불변 snapshot. 단가×수량은 long 합산 |
| SaleRestriction | RequiredAttributes:CustomerAttributes 모두 필요한 속성, ProductType:ProductType 제한 분류 | 독립 API. 현재 DayProgress는 공급자 미전달(null); 정식 지침 ID/CSV/유효일 없음 |
| SaleRestrictionViolation | Restriction:SaleRestriction 조건 복사, ProductId:uint 위반 판매 상품, Quantity:int 합산 수량 | 조건-상품별 기록. 실제 지침 공급 미연결; 중복 조건 입력 거부 |
| TransactionResult | Outcome:enum, OfferedTotal:long?, ReferenceTotal:long?, SoldItems:IReadOnlyList<SoldItem>, CostTotal:long, WereRestrictionsEvaluated:bool, RestrictionViolations:IReadOnlyList<SaleRestrictionViolation>, SaleIncome:long, ReputationDelta:int | 현재 DayProgress가 원본 Result 그대로 재정에 전달. 거부 시 Offered/Reference 보존, SoldItems 빈 목록·CostTotal0·SaleIncome0·평가false. 현재 방문 결과 ReputationDelta0 |
| TransactionResult 호환 생성자 | (saleIncome:long, reputationDelta:int) | 상세 없는 재정 단독 경로. Outcome None, 두 합계 null, 비용0·빈 목록·미평가. 새로운 방문 결과를 만드는 대신 쓰지 않음 |

`WereRestrictionsEvaluated=false`와 “검사했으나 위반 없음”은 다르다. 공급자가 존재하고 빈 규칙을 반환하면 수락 시 평가true/위반0이 가능하다. null 공급자는 미연결, 공급자가 null 목록을 반환하면 오류다.

### 경제·날짜·이벤트·큐

| 계약·근거 | 데이터·단위·소유권 | 현재 상태/제약 |
|---|---|---|
| [EconomySettings](../Assets/Scripts/Finance/EconomySettings.cs) | InitialBalance:long, MaintenanceCycleDays:int, MaintenanceAmounts:IReadOnlyList<long> 회차순 | CSV 두 테이블을 검증·복사한 설정. 현재100000/7/12회차 |
| [FinanceChangeResult](../Assets/Scripts/Finance/FinanceChangeResult.cs) | PreviousBalance:long 이전잔액, BalanceDelta:long 부호있는증감, CurrentBalance:long 이후잔액, Reason:FinanceChangeReason | FinanceService 결과; 날짜/상품 내역 아님 |
| [EconomyLogEntry](../Assets/Scripts/Finance/EconomyLogEntry.cs) | Sequence:long 기록순번, PreviousBalance:long, BalanceDelta:long, CurrentBalance:long, Reason:FinanceChangeReason | LogService 결과. 순번은 CSV PK나 날짜 아님 |
| [DailyAggregationResult](../Assets/Scripts/Finance/DailyAggregationResult.cs) | SaleIncome:long 당일 매출, ReputationDelta:int 거래 변화합, Transactions:IReadOnlyList<TransactionResult> | 수락·거절의 원본 snapshot을 EndDay에서 복사·확정. 일일 명성 최종값은 DailyReputationCalculationResult.FinalDelta |
| [MaintenancePaymentResult](../Assets/Scripts/Finance/MaintenancePaymentResult.cs) | PaymentRound:int 회차, RequiredAmount:long 필요금액, IsPaid:bool 성공, PreviousBalance:long, CurrentBalance:long | 부족 시 잔액·LastPaidRound 유지. 연속 다음회차만 납부 |
| [EconomyRuntime](../Assets/Scripts/Finance/EconomyRuntime.cs) | Settings, FinanceService, DailyAggregationService, MaintenanceService, LogService, QueryService | 서비스 묶음이며 추가 밸런스 원본 아님 |
| [EconomyQueryService](../Assets/Scripts/Finance/EconomyQueryService.cs) | CurrentBalance:long, DailySaleIncome:long, IsDayOpen:bool, MaintenanceCycleDays:int | 소유 서비스의 읽기 결과. 새로운 보유금 저장소 아님 |
| [MaintenanceService](../Assets/Scripts/Finance/MaintenanceService.cs) | LastPaidRound:int 초기0 | 복원 인수0~목록길이; 실제 저장 연결과 별도 |
| [GameSessionManager](../Assets/Scripts/Manager/GameSessionManager.cs) | ElapsedDays:uint 시작0, Economy:EconomyRuntime, DailyPrices:DailyPriceState, IsInitialized:bool 초기false | 날짜·현재가 권위. DailyPrices는 준비 전null, Economy는 초기화 전 접근 오류. CompleteDay가 종료일을 한 번 진행 |
| [DailyPriceState](../Assets/Scripts/Events/DailyPriceState.cs) | ElapsedDays:uint, NewspaperEventIdx:uint? 신문사건, RadioEventIdx:uint? 예정사건, IsRadioBroadcast:bool, Prices:IReadOnlyDictionary<uint,uint> 상품PK→현재가 | 후보 없음 null. 라디오 선정과 방송완료 구분. 새 snapshot 교체 |
| [GameProgress](../Assets/Scripts/Progress/GameProgress.cs) | State:GameProgressState, CurrentDay:int, CurrentDayProgress:DayProgress, IsMaintenanceDay:bool, DaysUntilMaintenance:int, CurrentMaintenanceRound:int | CurrentDay는 시작 전0, 이후ElapsedDays+1. 별도 날짜 증가값 아님 |
| [DayProgress](../Assets/Scripts/Progress/DayProgress.cs) | Day:int, State:DayProgressState, CurrentVisit:CustomerVisit, RemainingSeconds:float, BusinessDurationSeconds:float, IsPaused:bool, IsBusinessTimeExpired:bool, CanSubmitOffer:bool, SuccessfulSales:int, RefusedCustomers:int, AggregationResult:DailyAggregationResult? | 현재 순차 손님 진행. 초 단위; 집계 확정 전null. 결과 재정 실패 시 진행차단 |
| [CustomerQueue](../Assets/Scripts/Customer/CustomerQueue.cs) | Waiting/Leaving:IReadOnlyList<Entry>, 내부 now/nextArrival:double 초, running:bool | 독립 API. 계산 중 손님은 Waiting에서 제외 |
| CustomerQueue.Entry | Visit:CustomerVisit; 내부 Deadline:double 만료시각, WarningTextIdx/LeaveTextIdx:uint, Warned:bool, SpeechIdx:uint, SpeechUntil:double | 합류 시 값 복사. GetSpeech의0은 표시대사 없음. 이탈 후3초 표시 기록과 논리 대기열 분리 |

설비 구매는 `GameProgress.TryPurchaseFacility(uint, out FacilityPurchaseResult)`로 요청한다. 결과는 `Status`, `FacilityIdx`, `PaidAmount`, `ActivationDay:uint?`. 세션은 `FacilityActivationDays:IReadOnlyDictionary<uint,uint>`와 `IsFacilityActive(uint)`를 공개하며 서비스 인스턴스를 노출하지 않는다. 가격표 factory의 설비 조회 callback 미지정 시 해금 상품은 제외된다.

## 7. UI용 데이터와 표시 한계

설비 상점은 `FacilityShopViewData(CurrentBalance:long, Items:IReadOnlyList<FacilityItemViewData>)`를 사용한다. 행은 FacilityIdx:uint, DisplayName:string, PurchasePrice:long, UnlockProducts:string, State:FacilityDisplayState, ActivationDisplayDay:ulong을 복사한다. 실제 Facility/Text/Product FK와 세션 보유·잔액에서 생성하며 표시 DAY는 활성 경과일+1이다. 정산 중에만 열고 구매 요청에는 PK만 전달한다. [설비 UI 인계](FACILITY_INTEGRATION.md) 참조.

근거: [UIContracts](../Assets/Scripts/UI/Contracts/UIContracts.cs), [ProgressViewDataFactory](../Assets/Scripts/UI/ProgressViewDataFactory.cs), [GameUIController](../Assets/Scripts/Scene/GameUIController.cs). ViewData는 표현용 snapshot이며 CSV·경제 상태를 대체하지 않는다. 문자열을 UI에 전달하는 것은 CSV 문자열 키 허용을 확대하지 않는다.

| 구조체 | 전체 필드 | 현재 소비·의미 |
|---|---|---|
| EconomyStatusViewData | CurrentBalance:long, DailySaleIncome:long | EconomyStatusPresenter, 현재 보유금과 당일 매출 |
| GameDayViewData | CurrentDay:int, DaysUntilSettlement:int, IsSettlementDay:bool, Phase:GameDayPhase | GameDayPresenter. Settlement 명칭이지만 다음 **상납일**/상납일 여부로 공급 |
| BusinessTimerViewData | RemainingSeconds:float, NormalizedTime:float, IsPaused:bool, CanPause:bool, CanResume:bool | BusinessTimerPresenter. NormalizedTime은 잔여/전체시간의0~1 |
| ItemPriceViewData | ItemId:uint, DisplayName:string, Price:long, SpecialNote:string, Icon:Sprite, IsAvailable:bool | 계약만 존재, 현재 생성·소비 호출 없음. 현재 가격표는 CreatePriceListText 문자열 |
| CustomerBasketItemViewData | ItemId:uint, DisplayName:string, Quantity:int, Icon:Sprite, UnitPrice:int, TopViewIcon:Sprite | CustomerPresenter/CustomerBasketItemPresenter/SaleSortingPanel. 생성 시 희망 단가. 원본uint가 int.MaxValue보다 크면 factory가 표시값을 int.MaxValue로 제한함 |
| CustomerViewData | HasCustomer:bool, AppearanceColor:Color, AppearanceSprite:Sprite, DialogueText:string, Basket:IReadOnlyList<CustomerBasketItemViewData> | 현재 연결. Empty는false/clear/null/빈문자열/빈목록. 현 factory는 필수 AppearanceSprite를 전달하고 흰색 tint를 사용 |
| PriceInputViewData | InputAmount:long?, CanConfirm:bool, IsInputEnabled:bool, ValidationMessage:string | PriceInputPresenter. 빈 입력은null. 양수 총액 제출과 분류완료 조건은 진행/UI에서 검증 |
| TransactionViewData | WasAccepted:bool, OfferedPrice:long, FeedbackMessage:string | 계약만 존재, 현재 생성·소비 호출 없음. 상세4판정·판매목록을 담는 원본 TransactionResult와 다름 |
| DailySettlementViewData | Day:int, SaleIncome:long, Expenses:long, NetProfit:long, CurrentBalance:long, ReputationDelta:int, SuccessfulSales:int, RefusedCustomers:int, DepartedCustomers:int | DailySettlementPresenter 현재 연결. Expenses=0, NetProfit=SaleIncome, ReputationDelta=일일 FinalDelta, DepartedCustomers=0. 명성은 정성적 피드백 표시 |

DailySettlementPresenter는 전달된 NetProfit을 표시한다. 별도 [DailyResultPanel](../Assets/Scripts/UI/DailyResultPanel.cs)은 SaleIncome-Expenses로 다시 계산하는 구현이지만 현 controller가 직접 호출하는 경로는 아니다. 지출0 표시는 원가가 없다는 뜻도, 상납이 무료라는 뜻도 아니다. 상납은 별도 진행 단계다.

### 구형 가격표 패널의 별도 표시 데이터

[PriceListPanel.ItemPriceInfo](../Assets/Scripts/UI/PriceListPanel.cs)는 위 ItemPriceViewData와 별개인 중첩 DTO다. 전체 필드는 ItemName:string 표시명, Price:long 표시가격, SpecialNote:string 선택 문구(기본null), Icon:Sprite 선택 이미지(기본null)이며 상품 PK 필드가 없다. SetPriceList가 [PriceItemSlot.SetData](../Assets/Scripts/UI/PriceItemSlot.cs)에 전달한다. 현재 GameUIController의 문자열 가격표와는 미연결이다.

PriceListPanel.autoPopulateSampleData는 SerializeField bool, 코드 기본true다. Start 시 true이고 생성 슬롯이0개면 PopulateSampleData를 호출한다. 별도 샘플6행은 아래와 같으며 실제 ProductData가 아니다. 20% OFF/1+1 EVENT는 표시 문구일 뿐 할인 계산 구현이 아니다. 모두 Icon=null이다.

| ItemName | Price (G) | SpecialNote |
|---|---:|---|
| Fresh Apple | 1500 | 20% OFF |
| Organic Milk | 2200 | 1+1 EVENT |
| Sweet Banana | 3500 | null |
| Tuna Riceball | 1300 | null |
| Lunch Box | 5500 | HOT |
| Canned Coffee | 1000 | null |

이 샘플의 이름·가격을 CSV 상품의 이름·기본가격이나 당일 현재가로 합치지 않는다. 패널에는 슬롯 참조가 없으면 warning 후 표시를 중단하는 경로가 있으며 이 문서에서는 별도 샘플 패널 실행을 확인하지 않았다.

현재 거래 상태 문구는 ACCEPTED/REJECTED이며 Outcome enum의4단계가 실제 모든 UI에 구별 출력되는 것은 아니다. 내부 Result의 상세 판정 존재와 화면 문구를 분리한다.

### 공유 UI·코드에 있는 조정값

[GameUI.prefab](../Assets/Prefabs/GameUI/GameUI.prefab), [OperatingPanel.prefab](../Assets/Prefabs/GameUI/OperatingPanel.prefab), [SaleSortingPanel](../Assets/Scripts/UI/SaleSortingPanel.cs), [SaleSortingItemView](../Assets/Scripts/UI/SaleSortingItemView.cs), [KeypadController](../Assets/Scripts/UI/KeypadController.cs), [CustomerPresenter](../Assets/Scripts/UI/Presenters/CustomerPresenter.cs)에서 확인했다. Inspector Min/Range는 편집 힌트이며 모든 런타임 입력의 검증 보장은 아니다.

| 위치·원래 이름 | 타입 | 코드 기본 / 공유 prefab 값 | 의미·범위 |
|---|---|---|---|
| DayProgress.DefaultBusinessDurationSeconds | const float | 30초 / 현재 생성자 기본 사용 | 영업 제한시간. 생성자는 유한 양수 요구. 결과확인 중도 시간 진행, pause/Closing은 정지 |
| CustomerQueue.Capacity | const int | 10 / prefab 아님 | 대기 정원, 계산중 제외. 현재 UI 미연결 |
| CustomerQueue.ArrivalSeconds | const double | 5초 | 자동 입장 간격, 첫 자동 입장5초후 |
| CustomerQueue.SpeechSeconds | const double | 3초 | 재촉·이탈 대사 표시시간, 거래결과 확인시간 아님 |
| CustomerQueue.updateWaiting의 잔여 기준 | 숫자 literal | 6초 | 인내시간에서6을 뺀 시점에 재촉; CSV ValidateQueueSettings와 함께 묶인 계약 |
| PriceEventScheduler.GetRadioDelaySeconds | float 반환 | NextDouble×60초 | 사건 선정 확률과 다른 방송 시각 범위 |
| SaleSortingPanel.itemSizePixels | float | 72 / 72 | 상품 UI 한변; Min24 |
| cursorRadiusPixels | float | 30 / 30 | 커서 영향 반경; Min0 |
| cursorImpulse | float | 0.065 / 0.065 | 커서 속도에서 전달하는 충격 계수; Min0 |
| maximumSpeedPixels | float | 230 / 230 | UI 로컬 픽셀/초 속도 상한; Min0 |
| frictionPerSecond | float | 6.5 / 6.5 | 초당 감속 계수; Min0 |
| itemRestitution | float | 0.1 / 0.1 | 충돌 반발 계수; Range0~1 |
| transitionSeconds | float | **0.25 / 1** | 화면 전환 초. 코드 기본값보다 실제 직렬화값 우선 |
| customerArrivalSeconds | float | 0.8 / 0.8 | 박스 도착 연출 시작 전 대기 초; Queue 입장 간격 아님 |
| pourSeconds | float | 0.65 / 0.65 | 상품 쏟기 연출 초 |
| SaleSortingPanel.playContainerArrival.ArrivalSeconds | const float | 0.85초 | 박스 낙하 연출; 추가 수직거리180px, cubic ease |
| SaleSortingPanel 물리 시간 | float | delta 상한0.05초, MinimumDeltaSeconds0.0001초 | UI 시뮬레이션 안정화. Time.unscaledDeltaTime 사용 |
| SaleSortingPanel 초기 속도 | 코드식 | Random.insideUnitCircle×14 | 쏟기 후 초기 UI 속도 크기 |
| SaleSortingItemView.CollisionRadiusScale | const float | 0.18 | 최소 rect변 길이에 곱해 충돌 반경 계산 |
| CustomerPresenter.basketItemSpacingPixels | float | 8 / OperatingPanel 8 | 장바구니 상품 간 간격, 화면 배치 |
| CustomerPresenter.RandomPlacementAttempts | const int | 32 | 겹침 없는 무작위 배치 시도 상한 |
| KeypadController.maxDigits | int | 6 / OperatingPanel 6 | 현재 입력 최대6자리(양수 최대999999). long 거래API 상한과 다름 |
| DayTimerController.defaultBusinessTimeSeconds | float | 60 | 독립 구형 Timer component 기본. 현 진행은 DayProgress30초이며 이 값으로 덮지 않음 |
| DayTimerController.autoStartOnAwake | bool | false | 이름과 달리 Start에서 자동 시작 여부 확인. 현재 진행 타이머가 아님 |
| CommonConstants.ParryWindowDuration | const float | 0.15초 | 자체 코드 소비 미발견. 남은 공용 상수, Cashier 타이밍 근거 아님 |

그 밖의 prefab 색상·폰트·RectTransform·Sprite 참조는 표현 리소스다. 상품 가격이나 거래 규칙을 이미지 프레임·오브젝트 이름으로 판정하지 않는다. 개인 씬 override는 이 표에 포함하지 않았으므로 공유 prefab 값과 개인 씬의 실행값이 다를 수 있다.

## 8. 남아 있는 구형 모델과 저장 데이터

다음은 삭제 제안이나 새 기준이 아니라 **존재하지만 현재 진행 경로에 연결되지 않은 값**이다. 근거: [CashierSession.cs](../Assets/Scripts/UI/CashierSession.cs), [CashierSaveData.cs](../Assets/Scripts/UI/CashierSaveData.cs). 공유 자체 코드 검색에서 CashierSession 생성 호출·SaveManager 소비 연결은 확인되지 않았으며, Dev3SandboxTester는 주석 처리된 과거 코드다. 형식이 Serializable이라는 사실만으로 현재 세션 저장·복원이 구현된 것으로 보지 않는다.

### CashierSettings 전체 입력

| 원래 필드 | 타입·현재 코드 기본값 | 구형 의미 |
|---|---|---|
| products | CashierProduct[] 빈 배열 | 명시적으로 공급하는 구형 상품 목록; 현 ProductData 대체 아님 |
| citizenshipPrice | int 300000 | 시민권 목표 구매액 |
| firstTribute / secondTribute / laterTribute | int 50000 / 80000 / 110000 | 7일까지/14일까지/이후 상납액 |
| baseVisitors / minVisitors / maxVisitors | int 8 / 6 / 12 | 평판 보정 후 일일 방문수 범위 |
| gaugeStart / greenThreshold | float 70 / 70 | 대기 게이지 시작과 보상 기준 |
| gaugeDecay / gaugeRecovery / departureRecovery | float 2 / 12 / 30 | 초당감소 / 거래후회복 / 이탈후리셋 |
| departureCount / departureReputation / greenReputation | int 2 / -5 / 1 | 게이지 소진 이탈수·평판감소·초록게이지 판매 평판 |
| toleranceReputation / budgetReputation / refusalMorality | int -2 / -1 / -1 | 허용가격초과·예산초과·거절의 변화 |
| discountMorality / generousMorality / markupMorality | int 2 / 4 / -2 | 할인/80%이하 할인/할증의 도덕성 변화 |
| poorChance / poorBudgetRatio | float 0.18 / 0.85 | 가난한 손님 확률(0~1) / 예산비율 |
| normalBudgetMinPercent / normalBudgetMaxPercent | int 110 / 150 | 일반 손님 예산비율 **100=100%** |
| resultSeconds | float 0.85초 | 구형 결과 자동 표시시간, 현 수동 결과확인과 다름 |

구형의 추가 하드코딩: Day1·Cash0·Reputation50·Morality50에서 시작, 평판/도덕성0~100 clamp, `(Reputation-50)/10` 방문수 보정,7일 상납, 희망 종류1~최대3/수량1~2, 외형0~3, 일반 tolerancePercent110~139, 가난한 손님 tolerancePercent100, 입력문자 최대7자리. 위 수치에는 현 CSV와 동일한 검증/migration이 없다. 새로운 밸런싱 근거로 그대로 복사하지 않는다.

### 구형 DTO·세션 결과 전체

| 모델 | 원래 구성원·타입 | 의미·주의 |
|---|---|---|
| CashierProduct | idx:uint, id:string, name:string, price:int, firstDay:int, resourceIdx:uint, resourcePath:string, sprite:Sprite | 구형 상품. id 기본빈문자열이면 이름 공백제거로 생성. price/firstDay 생성자인수, resource 기본0/빈값/null. 현 nameidx/nullable이미지FK와 호환 아님 |
| CashierBasketLine | product:CashierProduct, quantity:int | 구형 장바구니 행 |
| CashierCustomer | basket:List<CashierBasketLine>, total:int, budget:int, tolerancePercent:int, appearance:int, isPoor:bool | 구형 예산 기반 고객. 초기목록 빈값, 그 외 기본0/false. 성향PK·속성 flags 없음 |
| CashierSession 진행 | Phase:CashierPhase, Customer:CashierCustomer, Day:int, IsPaused:bool, Revision:int | 구형 상태·표현 갱신번호. 현 GameProgress와 별도 |
| CashierSession 재정·평가 | Cash:int, Reputation:int, Morality:int, Revenue:int, ReputationChange:int, MoralityChange:int | 구형 정수 잔액·당일 매출·평가 및 하루 시작 대비차 |
| CashierSession 방문 | Visitors:int, Remaining:int, Sold:int, Refused:int, Departed:int, Gauge:float | 일일 방문수·남은수·거래/이탈수·0~100게이지 |
| CashierSession 피드백 | LastAccepted:bool, Feedback:string, Reason:string | 구형 수락 및 하드코딩 설명 |
| CashierSession 목표 | TributeAmount:int, DaysUntilTribute:int, CanBuy:bool | 위 구형 설정·7일조건·시민권 구매조건에서 파생 |

### 저장 모델 전체

| 모델·필드 | 타입·기본 | 의미·실제 한계 |
|---|---|---|
| GameSaveData.currentDay | int 1 | 구형 표시일 |
| cash | int 0 | 구형 잔액. 현 InitialBalance100000 및 Finance long과 다름 |
| reputation / morality | int 50 / 50 | 구형 평가값; 현 방문 결과의 ReputationDelta0과 별개 |
| buildingTier | int 1 | 저장 슬롯만 존재, 설비 업그레이드 기능 완성을 뜻하지 않음 |
| bgmVolume / sfxVolume | float 0.8 / 1.0 | 음량 저장값; 저장 모델 자체의 범위 검증 없음 |
| ledgerHistory | List<LedgerRecord> 빈 목록 | 장부 기록 목록 |
| LedgerRecord.day | int 0 | 일자 |
| maintenanceFee | int 0 | 상납 지출 |
| upgradeExpense | int 0 | 업그레이드 지출 |
| revenue | int 0 | 매출 |
| netCash | int 0 | 기록한 최종 현금값; constructor는 계산하지 않고 전달값 보관 |
| reputationChange | int 0 | 평판 변화 |
| netReputation | int 0 | 기록한 최종 평판값 |

CashierSaveManager의 키는 `Cashier_LocalGameSave_v1`이며 JsonUtility/PlayerPrefs로 저장한다. Save(null)은 무시, 저장 없음·JSON null·예외는 새 기본 데이터로 돌아간다. 스키마 버전 필드·현 GameSession 복원 경로·범위 검증은 없다. 키의v1을 완성된 migration으로 간주하지 않는다. 이번 조사는 사용자 PlayerPrefs 저장 내용을 읽거나 변경하지 않았다.

## 9. 이름이 같거나 비슷한 값의 구분

| 원래 명칭들 | 관계/다른 의미 |
|---|---|
| ProductData.Idx / CustomerOrderItem.ProductIdx / SaleItem.ProductId / SoldItem.ProductId / SaleRestrictionViolation.ProductId / UI.ItemId / SaleSortingItemView.ProductId | 현재 경로에서는 같은 상품 PK 의미. 이름은 원문 유지. SaleSortingItemView.UnitIndex는 상품 개체 순번으로 PK가 아님 |
| ProductCategoryData.Idx / ProductType / product_type | 표시 행PK / 고정enum 값. 7001과1을 바꿔 쓰지 않음 |
| CustomerDispositionData.Idx / DispositionIdx / disposition_type / DispositionTypeValue / DispositionType | 개별 설정PK / 방문의설정PK / CSV타입열 / 원시uint / enum 해석 |
| ProductData.BasePrice / DailyPriceState.Prices / CustomerOrderItem.UnitPrice / SoldItem.UnitPrice | 고정 기본단가 / 이벤트 현재단가 / 생성시 희망 단가 / 제출시 확정 단가 |
| CustomerVisit.BaseTotal / TransactionResult.ReferenceTotal | **동일 의미 별칭**: 최종 제출 목록 현재가 합계. 이름 Base가 고정 기본가격을 뜻하지 않음 |
| OfferedTotal / TransactionViewData.OfferedPrice / PriceInputViewData.InputAmount | 총액 제출 / 미연결 UI 총액계약 / 아직 입력중 null가능 값 |
| CostPrice / UnitCostPrice / CostTotal / Expenses / NetProfit | 원본 단위원가 / 확정 단위원가 / 확정 원가합계 / 현재 UI0 / 현재 UI매출과동일. 서로 치환 불가 |
| PriceTolerance / tolerancePercent / normalBudgetMinPercent | 현재1000분모 배율 / 구형100분모 허용률 / 구형100분모 예산률 |
| PreferredSelectionChance / SelectionWeight / poorChance | 현재0~1000 확률 / 후보 간 상대가중치 / 구형0~1 확률 |
| ElapsedDays / available_day / start_day / end_day / GameProgress.CurrentDay / DayProgress.Day / firstDay | 앞의4개0시작 경과일 / 현재표시1시작 / 하루표시1시작 / 구형1시작 해금 |
| maintenanceCycleDays / paymentRound / MaintenanceAmounts / firstTribute | 간격일수 / 회차 / 회차별목록 / 구형첫상납액. 첫회차100000과50000은 충돌한 별도모델 |
| QueuePatienceSeconds / Gauge / customerArrivalSeconds / ArrivalSeconds / SpeechSeconds / resultSeconds | 줄이탈초 / 구형대기게이지 / UI도착대기 / Queue입장5초 또는 UI낙하0.85초(다른소유자) / 대사3초 / 구형결과0.85초 |
| State / Outcome / GameDayPhase / CashierPhase | 방문수명 / 가격판정 / UI일간표현 / 구형진행. 숫자값을 서로 캐스팅하지 않음 |
| NameIdx / Text / DisplayName / CashierProduct.name / ResourceData.Path | Text FK / 문자열원본 / 표시용문자열 / 구형직접이름 / 리소스키. 이름·리소스키는 상품ID 아님 |
| ItemPriceViewData.DisplayName / PriceListPanel.ItemPriceInfo.ItemName / PriceItemSlot.SetData(itemName) | 표시 이름이라는 목적은 같지만 별도 계약. ItemPriceInfo에는 ItemId가 없고 현재 CSV FK 연결 없이 샘플 문자열을 담음 |
| ReputationDelta / Reputation / morality / netReputation | 현재결과 변화량 / 구형현재평판 / 구형도덕성 / 저장장부값. 같은 평판시스템 구현으로 합치지 않음 |

## 10. 확인 범위와 후속 판단

- 문서 조사로 확인: 파일 목록, 모든 header·현재 행, DTO mapping, 주요 검증·FK 경계, 직접 소비 경로, enum 값, 공유 prefab 조작값과 코드 기본값 차이.
- 설비 변경의 최신 검증은 [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md)를 참조한다. UI 수동 조작·Addressables 전 자산 전수 검증은 별도이며 API 테스트 성공으로 대체하지 않는다.
- 미연결/검토 포인트: 현 UI의 대기열, 정식 지침 공급·벌칙·명성·원가 지출, 구형 SaveData와 현재세션 복원, Wealthy 성향 행, Product 이미지 FK 전부빈값, Resource 옛경로의 실자산 유효성.
- 유지보수: 밸런싱 변경 시 실제 CSV/DTO/소비자와 함께 이 관측 문서를 갱신한다. 본문 요약보다 실제 코드·승인 규격을 우선하며 새 ID나 기획 결정을 이 문서만 보고 배정하지 않는다.
- 부록은 모든 원본 행·컬럼 순서·셀 문자열을 그대로 보존한다. Markdown의 개행 표시는 LF로 통일하며 원본 BOM/CRLF 여부를 재현하는 바이너리 백업은 아니다. SHA-256은 조사 당시 CSV 파일 바이트 기준이다.

## 부록 A. 전체 CSV 원문 스냅샷


### Assets/Datas/EconomyBalanceData.csv

데이터 1행, 3컬럼. SHA-256: `34BACCEDAAF3C697B2152A79E6D49028875CBC730195734EFD8F77A856E4A31E`.

```csv
idx,initialBalance,maintenanceCycleDays
002001,100000,7
```

### Assets/Datas/MaintenanceBalanceData.csv

데이터 12행, 3컬럼. SHA-256: `2420113BAB89A6C2A513CA684B8D8D70B7252DBD39EC7758B8B3044C06B57E0F`.

```csv
idx,paymentRound,maintenanceAmount
003001,1,100000
003002,2,110000
003003,3,120000
003004,4,150000
003005,5,200000
003006,6,280000
003007,7,400000
003008,8,560000
003009,9,780000
003010,10,1100000
003011,11,1540000
003012,12,2150000
```

### Assets/Datas/PriceEventData.csv

데이터 4행, 7컬럼. SHA-256: `6C9F9B13B9AF40F11B08ADD41D4114AFB838380885986F193800EF64785FBA4A`.

```csv
idx,nameidx,descriptionidx,product_idxs,product_types,change_type,change_value
9001,8042,8043,,2,1,-200
9002,8044,8045,1001,,2,30
9003,8046,8047,,,0,0
9004,8048,8049,,3,1,300
```

### Assets/Datas/PriceEventScheduleData.csv

데이터 5행, 7컬럼. SHA-256: `65176B3D51529D0C9B9B4D56B0ECEC70FBB054C97B425821AC78B4EC6206EB78`.

```csv
idx,event_idx,channel,start_day,end_day,repeat_days,selection_weight
10001,9001,1,0,,2,1
10002,9003,1,3,,4,1
10003,9002,2,0,,0,1
10004,9004,2,0,,0,1
10005,9003,2,0,,0,1
```

### Assets/Datas/ResourceData.csv

데이터 54행, 2컬럼. SHA-256: `536F735A418DE9007E9E579C62CB3E145EEC03D813F345D572767F6775039D85`.

```csv
idx,path
4201,FemaleCustomer_01
4202,FemaleCustomer_02
4203,FemaleCustomer_03
4204,FemaleCustomer_04
4205,FemaleCustomer_05
4206,FemaleCustomer_06
4207,FemaleCustomer_08
4208,FemaleCustomer_09
4209,FemaleCustomer_10
4210,FemaleCustomer_11
4211,FemaleCustomer_12
4212,FemaleCustomer_14
4213,FemaleCustomer_15
4214,FemaleCustomer_16
4215,FemaleCustomer_17
4216,FemaleCustomer_18
4217,FemaleCustomer_19
4218,FemaleCustomer_20
4219,MaleCustomer_01
4220,MaleCustomer_02
4221,MaleCustomer_03
4222,MaleCustomer_04
4223,MaleCustomer_05
4224,MaleCustomer_06
4225,MaleCustomer_07
4226,MaleCustomer_08
4227,MaleCustomer_09
4228,MaleCustomer_10
4229,MaleCustomer_12
4230,MaleCustomer_13
4231,MaleCustomer_14
4232,MaleCustomer_15
4233,MaleCustomer_16
4234,MaleCustomer_17
4235,MaleCustomer_18
4236,MaleCustomer_19
4237,MaleCustomer_21
4238,MaleCustomer_22
4239,MaleCustomer_23
4240,MaleCustomer_24
4241,MaleCustomer_25
4242,MaleCustomer_26
4243,MaleCustomer_28
4244,MaleCustomer_29
4245,MaleCustomer0
4246,Bandage
4247,Battery
4248,Can
4249,Crackers
4250,Painkiller
4251,TopDownCan
4252,TopDownCrackers
4253,TopDownWater
4254,Water
```

### Assets/Datas/TextData.csv

데이터 115행, 2컬럼. SHA-256: `CAF19EF00ECBA92E8E0044AAA2DA7A328D41AB5D4A8FFC9F1FA15041D9054CBD`.

```csv
idx,text
8001,여성 외형 01
8002,여성 외형 02
8003,여성 외형 03
8004,여성 외형 04
8005,평범
8006,급함
8007,가격 민감
8008,식수
8009,식량
8010,의료
8011,생활
8012,물
8013,정수 캔
8014,휴대용 필터
8015,통조림
8016,분말 수프
8017,영양바
8018,붕대
8019,소독제
8020,해열제
8021,건전지
8022,성냥
8023,방한포
8024,필요한 것만 몇 개 사려고요.
8025,평소 사던 물건들이에요. 계산 부탁해요.
8026,네 그 정도면 괜찮네요.
8027,좋아요. 이 가격에 살게요.
8028,생각보다 비싸네요. 오늘은 안 살게요.
8029,그 가격이면 조금 더 알아봐야겠어요.
8030,시간이 없어서요. 빨리 계산 부탁해요.
8031,급히 가져가야 해요. 바로 계산할 수 있죠?
8032,좋아요. 바로 가져갈게요.
8033,알겠어요. 서둘러 주세요.
8034,급해도 그 가격은 못 내겠네요.
8035,아무리 급해도 너무 비싸요. 안 살게요.
8036,다른 곳 가격도 알아보고 왔어요.
8037,가격을 꼼꼼히 비교하는 편이에요.
8038,가격이 맞네요. 구매할게요.
8039,이 정도면 납득할 수 있어요.
8040,그 가격이면 다른 데서 사겠어요.
8041,알아본 가격보다 비싸네요. 안 살게요.
8042,식료품 공급 확대
8043,오늘은 식료품 기본가격에서 20% 인하합니다.
8044,생수 운송 지연
8045,오늘은 물의 기본가격에 30이 추가됩니다.
8046,지역 소식
8047,오늘 지역 소식은 상품 가격에 영향을 주지 않습니다.
8048,의약품 수요 증가
8049,오늘은 의약품 기본가격에서 30% 인상합니다.
8050,얼마나 더 기다려야 하나요?
8051,너무 오래 걸리네요. 다음에 오겠습니다.
8052,급한 일이 있어요. 서둘러 주세요!
8053,더는 못 기다리겠어요!
8054,가격을 확인하려는데 오래 걸리네요.
8055,이렇게 기다릴 바엔 다른 가게로 가겠어요.
8056,식량 보관 선반
8057,잠금 약품장
8058,공구대
8059,전력 통신 장비
8060,핵 보호 물품 설비
8061,연고
8062,응급 주사
8063,손전등
8064,접이식 삽
8065,쇠지렛대
8066,무전기
8067,배터리
8068,방독면
8069,방호복
8070,방사능 측정기
8071,공구 선호
8072,전기장비 선호
8073,보호장비 선호
8074,부유한 손님
8112,여성 외형 05
8113,여성 외형 06
8114,여성 외형 08
8115,여성 외형 09
8075,여성 외형 10
8076,여성 외형 11
8077,여성 외형 12
8078,여성 외형 14
8079,여성 외형 15
8080,여성 외형 16
8081,여성 외형 17
8082,여성 외형 18
8083,여성 외형 19
8084,여성 외형 20
8085,남성 외형 01
8086,남성 외형 02
8087,남성 외형 03
8088,남성 외형 04
8089,남성 외형 05
8090,남성 외형 06
8091,남성 외형 07
8092,남성 외형 08
8093,남성 외형 09
8094,남성 외형 10
8095,남성 외형 12
8096,남성 외형 13
8097,남성 외형 14
8098,남성 외형 15
8099,남성 외형 16
8100,남성 외형 17
8101,남성 외형 18
8102,남성 외형 19
8103,남성 외형 21
8104,남성 외형 22
8105,남성 외형 23
8106,남성 외형 24
8107,남성 외형 25
8108,남성 외형 26
8109,남성 외형 28
8110,남성 외형 29
8111,남성 외형 0
```

### Assets/Datas/Customer/CustomerAppearanceData.csv

데이터 45행, 3컬럼. SHA-256: `93C299ECCE11D71C3D8D40908964706F0C055B5C28989648CA1050C8ACEDCEBF`.

```csv
idx,nameidx,image_resource_idx
5001,8001,4201
5002,8002,4202
5003,8003,4203
5004,8004,4204
5005,8112,4205
5006,8113,4206
5007,8114,4207
5008,8115,4208
5009,8075,4209
5010,8076,4210
5011,8077,4211
5012,8078,4212
5013,8079,4213
5014,8080,4214
5015,8081,4215
5016,8082,4216
5017,8083,4217
5018,8084,4218
5019,8085,4219
5020,8086,4220
5021,8087,4221
5022,8088,4222
5023,8089,4223
5024,8090,4224
5025,8091,4225
5026,8092,4226
5027,8093,4227
5028,8094,4228
5029,8095,4229
5030,8096,4230
5031,8097,4231
5032,8098,4232
5033,8099,4233
5034,8100,4234
5035,8101,4235
5036,8102,4236
5037,8103,4237
5038,8104,4238
5039,8105,4239
5040,8106,4240
5041,8107,4241
5042,8108,4242
5043,8109,4243
5044,8110,4244
5045,8111,4245
```

### Assets/Datas/Customer/CustomerDispositionData.csv

데이터 7행, 21컬럼. SHA-256: `A80DFD600AD0077C3E32F916E615427C741775EEC4B5B1D51040812B8A348388`.

```csv
idx,nameidx,preferred_product_types,preferred_selection_chance,min_product_kinds,max_product_kinds,min_quantity,max_quantity,price_tolerance,entry_text_idxs,regular_sale_text_idxs,discount_sale_text_idxs,exploitative_sale_text_idxs,reject_text_idxs,queue_patience_seconds,queue_warning_textidx,queue_leave_textidx,disposition_type,preferred_product_idxs,regular_price_min_rate,regular_price_max_rate
6001,8005,1_2,900,1,3,1,3,1300,8024_8025,8026_8027,8026_8027,8026_8027,8028_8029,12,8050,8051,1,,1000,1000
6002,8006,3,900,1,3,1,3,1500,8030_8031,8032_8033,8032_8033,8032_8033,8034_8035,9,8052,8053,2,,1000,1000
6003,8007,4,900,1,3,1,3,1000,8036_8037,8038_8039,8038_8039,8038_8039,8040_8041,18,8054,8055,3,,1000,1000
6004,8071,5,900,1,3,1,3,1300,8024_8025,8026_8027,8026_8027,8026_8027,8028_8029,12,8050,8051,1,,1000,1000
6005,8072,6,900,1,3,1,3,1300,8024_8025,8026_8027,8026_8027,8026_8027,8028_8029,12,8050,8051,1,,1000,1000
6006,8073,7,900,1,3,1,3,1300,8024_8025,8026_8027,8026_8027,8026_8027,8028_8029,12,8050,8051,1,,1000,1000
6007,8074,6_7,900,1,3,1,3,1400,8024_8025,8026_8027,8026_8027,8026_8027,8028_8029,15,8050,8051,4,,1000,1000
```

### Assets/Datas/Customer/ProductCategoryData.csv

데이터 7행, 3컬럼. SHA-256: `A002313CF3D5B596DB4286DE6B47E152DF65D0C2ABE239C25A36FB3B135FB756`.

```csv
idx,nameidx,product_type
7001,8008,1
7002,8009,2
7003,8010,3
7004,8011,4
7005,8058,5
7006,8059,6
7007,8060,7
```

### Assets/Datas/Customer/ProductData.csv

데이터 22행, 10컬럼. SHA-256: `FD1D4585418E949B67FD8A85E6BDB6130B35FB381DD237C933A3ADFD823CBA5C`.

```csv
idx,nameidx,product_type,is_available,base_price,available_day,image_resource_idx,cost_price,required_facility_idx,top_view_image_resource_idx
1001,8012,1,1,100,0,4254,50,,4253
1002,8013,1,0,150,0,,75,,
1003,8014,1,0,500,0,,250,,
1004,8015,2,1,250,0,4248,125,,4251
1005,8016,2,1,150,0,,75,12001,
1006,8017,2,1,200,0,4249,100,12001,4252
1007,8018,3,1,300,0,4246,150,,4246
1008,8019,3,0,350,0,,175,,
1009,8020,3,1,400,0,4250,200,12002,4250
1010,8021,4,1,200,0,4247,100,,4247
1011,8022,4,1,100,0,,50,,
1012,8023,4,0,600,0,,300,,
1013,8061,3,1,500,0,,250,12002,
1014,8062,3,1,900,0,,450,12002,
1015,8063,5,1,800,0,,400,12003,
1016,8064,5,1,1000,0,,500,12003,
1017,8065,5,1,1200,0,,600,12003,
1018,8066,6,1,2000,0,,1000,12004,
1019,8067,6,1,1200,0,4247,600,12004,4247
1020,8068,7,1,2500,0,,1250,12005,
1021,8069,7,1,4000,0,,2000,12005,
1022,8070,7,1,5000,0,,2500,12005,
```

### Assets/Datas/FacilityData.csv

데이터 5행, 3컬럼. SHA-256: `B94956FCADA9643FDD13A56C7FB1CB017956BC0CC2ECAC3E6753D3E72529BF80`.

```csv
idx,nameidx,purchase_price
12001,8056,5000
12002,8057,7000
12003,8058,12000
12004,8059,15000
12005,8060,25000
```
