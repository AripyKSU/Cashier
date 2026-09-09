# 손님·상품 시스템 MainScene 인계

기준: `customer_sys`의 `5ad5fa8`까지 반영된 3차 구현과 이후 Test Runner 전환. 이 문서는 손님 공개 API·데이터의 상세 권위다. 최신 자동 검증 및 병합 의존은 [TESTING.md](TESTING.md)를 따른다. 아래 단계별 검증 기록은 당시 결과이며 삭제된 Check 스크립트의 현재 실행 안내가 아니다.

## 1. 통합 범위와 책임

### 2026-09-09 실제 진행 연결

현재 total_merge 기반 UI는 GameUIController → GameProgress(session, ...) → DayProgress(day, session, ...)다. 선택 UI의 SaleItem 목록을 제출하고 visit.Result.Value를 재생성 없이 한 번 정산 전달한다. 입력 오류는 재제출 가능하지만 판정 후 접수 실패는 원본 Result를 유지하고 진행을 중단한다. 날짜·라디오·가격표·상납 흐름과 기존 정책 차이는 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)의 현재 계약을 따른다. 아래 3차/2차 검증 기록과 Dev3 설명은 당시 맥락이다.

### 정가 인정 범위·판매 지침 구조 (3차)

- 성향 CSV 마지막에 필수 int `regular_price_min_rate`, `regular_price_max_rate`를 추가한다. DTO `RegularPriceMinRate`/`RegularPriceMaxRate` 기본값과 기존 세 행은 모두 1000/1000이다. 단위는 1000=100%이며 `0 < min <= 1000 <= max`를 검증한다. 기존 셀·가격 허용도는 보존하고 빈 셀·구형 header는 거부한다. CSV와 소비 코드를 함께 배포·복구한다.
- 두 배율은 방문 생성 시 getter-only 값 복사다. 기존 `PriceTolerance > 0` 계약은 그대로이며 `max <= PriceTolerance`는 강제하지 않는다. 결제 거부 판정이 항상 우선하고, 수락 범위 안에서만 정가 인정 범위가 의미 있다.
- 수락 시 `(decimal)offeredTotal * 1000`과 `(decimal)ReferenceTotal * min/max`를 비교한다. 하한 미만은 저가, 상한 초과는 착취, 양끝 포함 안쪽은 정가 판매다. 하한·상한 금액을 먼저 floor하지 않는다. 예: 기준액 101, 배율 950~1050은 95 저가·96~106 정가·107 착취(각각 결제 허용 범위 내일 때). 1000/1000은 기존 4판정을 유지한다.
- `SaleRestriction(RequiredAttributes, ProductType)`은 필요 속성을 **모두** 가진 손님에 대한 해당 분류 판매 제한이다. None·미정의·배타 속성, None·미정의 상품 분류 및 같은 속성+분류의 중복 규칙을 거부한다. 정식 지침 ID·CSV·기호품 분류·실제 규칙은 아직 없다.
- `Generator.Generate` 마지막 선택 인자 `Func<IReadOnlyList<SaleRestriction>> getSaleRestrictions = null`을 통해 제출 시 유효 규칙을 공급할 수 있다. 생성 시에는 호출하지 않는다. 현재 UI/manager는 공급하지 않아 **미연결** 상태다. 향후 원본·효력 수명은 공급자가 책임진다.
- 가격 수락 경로에서 공급자를 제출당 한 번 호출하고 목록 복사·전체 검증 후 최종 합산 판매 목록과 방문 `Attributes`를 검사한다. 최초 희망 목록이나 외형을 사용하지 않는다. 규칙-상품당 `SaleRestrictionViolation(Restriction, ProductId, Quantity)` 한 건을 기록하며 Quantity는 최종 합산 수량이다. 위반은 결제를 막거나 매출·명성·벌금을 변경하지 않는다.
- `TransactionResult.WereRestrictionsEvaluated`와 `RestrictionViolations`로 평가 여부·불변 기록을 조회한다. 공급자 null/거부/legacy/default는 false+빈 목록, 공급된 빈 목록은 true+빈 목록이다. 공급자의 null 반환·예외·잘못된 규칙/상품 분류는 오류다. 지침 결과 및 원가 합산까지 모두 성공한 뒤에만 방문 상태를 확정하므로 실패 후 정상 재제출이 가능하다. 결과 이후 원본 규칙목록·상품분류 변경은 과거 기록에 영향이 없다.
- 검사: 기존 생성기 검사에 정가 범위 양끝·작은/큰 총액·snapshot·tolerance<1000 거부 우선, 지침 AND·불일치·다상품·합산수량·중복/default/오류 공급자·1회 조회·거부 미조회·미연결/빈목록·불변·원가 overflow 포함 실패 원자성/재시도를 추가했다. CSV 검사는 56개 오류를 의도적으로 발생시킨다. 실제 지침 미연결이므로 지침 검증은 기존 분류를 사용한 메모리 입력 검사이며 제품 규칙 활성화 증거가 아니다.
- 3차 검증(2026-09-08): compilation 완료·failed=False, Check-CustomerGenerator/CustomerCsv(56/56, 의도된 LogError 56건)/CustomerQueue 통과. InitScene → GameplaySandbox initialized=True 확인 후 Check-CustomerOutcomes의 실제 UI 4판정·대사·매출·중복방지·퇴장 통과, Console 오류 0건. MainScene 자산·구형 CustomerSandbox 화면·실제 지침 공급은 미검증/미연결이며 가격 이벤트·라디오 검사는 이번에 재실행하지 않았다. Play 종료·runInBackground 원복 완료. 원본 CSV는 각 행 끝에 승인된 두 컬럼만 추가됐음을 HEAD 문자열 비교로 확인했다.

### 성향 타입·개별 상품 선호·방문 속성 (2차)

- `CustomerDispositionType`은 None=0(사용 금지), Normal=1, Hasty=2, PriceSensitive=3, Wealthy=4다. 마지막 `CustomerDispositionType_End`는 자동 증가 종료 표식이며 데이터로 사용하지 않는다.
- 기존 성향 CSV 끝에 `disposition_type`, `preferred_product_idxs`를 순서대로 추가했다. 기존 6001/6002/6003은 각각 1/2/3으로 매핑하며 다른 셀·PK·대사·가격·대기 수치는 보존한다. Wealthy는 타입만 정의하며 실제 행이 없어 등장하지 않는다.
- `disposition_type`은 필수 uint 숫자로 읽고 검증한 enum을 `DispositionType`으로 제공한다. 빈값·문자열·None·종료값·미정의 값은 거부한다. `preferred_product_idxs`는 필수 header, 빈 셀은 선호 없음이며 기존 `UIntArrayConverter`의 `_` 구분 uint 배열을 사용한다. 0·중복·null과 ProductData.idx FK 누락은 거부한다. 기존 세 행은 빈 셀이다.
- 추첨은 실제 후보 타입을 정렬해 균등 선택한 뒤 그 타입의 설정을 Idx 순으로 정렬해 균등 선택한다. 따라서 타입별 행 개수는 타입 출현율을 바꾸지 않는다. 같은 seed·외형 후보 순서·후보 집합에서 재현되며 데이터·추첨 방식 변경 전 버전과의 난수열 호환은 보장하지 않는다.
- 선호 풀은 품목 `preferred_product_types` **OR** 개별 `preferred_product_idxs`다. 둘 다 해당해도 상품은 한 번만 포함된다. 비활성·미등장 상품 제외, 기존 0~1000 선호 확률과 한쪽 풀 소진 시 fallback은 유지한다.
- `CustomerAttributes`는 Male=1/Female=2 중 하나와 성인(연령 비트 없음)/Child=4/Elderly=8 중 하나를 각각 균등 추첨한 6조합이다. 외형·성향과 독립이다. None=0은 표현만 허용하고 생성하지 않는다. 미정의 비트, 남녀 동시 또는 아이·노인 동시는 거부한다.
- 방문의 getter-only `DispositionType`·`Attributes`는 생성 시 값 복사이며 원본 DTO 변경에 영향받지 않는다. 타입·속성으로 가격 허용도·대기 시간을 자동 보정하지 않는다. 기존 UI는 PK 표시를 유지하며 새 정보는 공개 API로 조회한다.
- 배포 시 CSV와 DTO·생성기를 함께 반영한다. 구형 header는 오류로 차단한다. 런타임 저장 형식은 추가하지 않았다. 복구 시 이 스키마 변경과 소비자를 함께 되돌리고 기존 자산 GUID를 유지한다.
- 검사: `Tools/Check-CustomerGenerator.ps1`은 타입별 3:1 설정 후보의 타입 균등성·행 균등성, 같은 외형의 6속성, OR 선호·중복·미등장 제외·스냅샷과 기존 최종 거래를 검사한다. `Tools/Check-CustomerCsv.ps1`은 새 header/enum/FK를 포함해 48개 오류를 거부한다(의도된 LogError 48건).
- 2차 검증(2026-09-08): compile failed=False, 생성기 선호 9014/10000·Normal 5936/12000, CSV 48/48, Queue·PriceEvents 통과. InitScene → GameplaySandbox의 initialized=True 확인 후 CustomerOutcomes(실제 UI 4판정·대사·매출·중복 방지) 및 별도 새 Play 세션의 RadioTiming 통과, 각 Console 오류 0건. 초기 연결 heartbeat 지연으로 준비 조회가 늦었지만 복구 후 새 세션에서 검사했다. MainScene 자산 및 구형 CustomerSandbox UI는 이번에 실행하지 않았다. Play 종료와 runInBackground 원래 값 복원을 확인했다.

### 제출 시 거래 확정 계약

- `SaleItem(ProductId, Quantity)`는 최종 입력, `SoldItem(ProductId, Quantity, UnitPrice, UnitCostPrice)`는 확정 내역이다. 중복 수량은 checked 합산한다. `Items`는 최초 희망 목록을 유지한다.
- `CustomerVisit.Result`는 제출 전 null이며 판정 완료 시 불변 `TransactionResult`를 보관한다. Outcome, OfferedTotal, ReferenceTotal, SaleIncome, SoldItems, CostTotal을 제공한다. 수락 매출은 제시액, 원가는 판매 항목에서 합산한다. 거부는 매출·원가 0과 빈 판매 목록이며 제시액·기준액은 보존한다.
- `ProductData.cost_price`는 필수 양수 uint다. 기존 Product CSV 끝에 추가하며 테스트값은 `max(1, floor(base_price / 2))`다. 일반 규칙으로 원가<기본가를 강제하지 않는다. 정식 원가 승인 시 테스트값을 교체한다. CSV·DTO·loader를 함께 배포하며 구형 header는 실패한다.
- 기존 `TransactionResult(long, int)`는 재정 단독 검사 호환으로만 유지한다. 상세 미확정은 Outcome=None, OfferedTotal/ReferenceTotal=null, SoldItems 비움으로 표현한다. 새 UI 흐름은 방문이 만든 Result를 전달하며 재생성하지 않는다.
- 판정 완료와 재정 반영 완료는 별개다. 일일집계 접수 실패 시 오류 중단하고 자동 재시도하지 않는다. 명성 변화는 0, 원가 실제 차감·일일 원가 집계는 미연결이다.
- 현재 GameUIController는 SaleSortingPanel에서 선택한 최종 목록을 사용한다. 이전 Dev3의 최초 Items 변환은 구형 화면 계약이며 새 진행 경로와 구분한다.
- 검증: Check-CustomerGenerator의 최종 목록 교체 4판정·최신 가격·외부 변경 불변·수량/원가 합계·실패 원자성, Check-CustomerCsv 38/38 오류 거부, Check-CustomerQueue/Check-PriceEvents 회귀 통과. GameplaySandbox에서 Check-CustomerOutcomes·Check-RadioTiming·Check-MainSceneIntegration 실행 통과. 마지막 검사는 의도된 재정 접수 실패 LogError 1건을 발생시키며 판정 보존·미입금·재시도 차단을 확인한다. 구형 CustomerSandbox UI 검사 스크립트는 호출자만 이행했고 해당 화면 실행은 미검증이다.

대기열·5초 입장·성향별 재촉/이탈·FIFO 인계의 최신 계약과 추가 성향 컬럼은 [CUSTOMER_QUEUE_INTEGRATION.md](CUSTOMER_QUEUE_INTEGRATION.md)를 따른다. 줄 합류 시 최초 희망 목록의 표시 단가만 고정한다. 최종 거래 단가·기준액·허용액·원가는 SubmitOffer 시점에 확정한다.

#### GameplaySandbox 검증 재현 범위

Check-CustomerOutcomes와 Check-RadioTiming은 스크립트 그대로 실행했다. Check-MainSceneIntegration은 실제 실행 씬이 GameplaySandbox였으므로 파일을 변경하지 않고 실행 메모리에서 씬 이름 조건만 대체했다. 나머지 검사는 그대로 실행했으며 MainScene 자산 자체를 실행한 증거는 아니다.

당시에는 기존 검사 내용을 메모리에서만 변경해 GameplaySandbox 조건으로 실행했다. 해당 셸 파일은 Test Runner 전환 후 제거했으므로 현재 재현 명령으로 사용하지 않는다.

실행 출력: `MAIN_INTEGRATION_PASS: button input, accepted/rejected, one income, departure, settlement, next day, failed settlement stops without retry (expected LogError=1)`. Console에는 의도된 접수 실패 `InvalidOperationException: 영업 종료로 거래 수입 반영이 거부되었습니다.` 1건이 있었다. 출력은 작업 실행 기록에 있으며 별도 로그 파일은 저장하지 않았다. Check-RadioTiming은 최초 초기화 전 실행이 실패했고 GameplaySandbox의 initialized=True 확인 후 재실행한 성공 결과다.

### 거래 결과 4단계

- `CustomerVisit.Outcome`은 `None / RegularSale / DiscountSale / ExploitativeSale / PaymentRefused`이며 퇴장 후에도 보존한다. `WasAccepted`는 결과에서 파생된다.
- 양의 제안 총액이 허용 총액을 초과하면 결제 거부, 그 외에는 위 3차 정가 인정 범위로 판정한다. 초기 1000/1000 데이터에서는 기준 총액보다 1만 높아도 착취 판매다. RegularSale enum 값은 유지한다.
- 성사된 세 유형만 제안 총액을 Finance에 한 번 반영한다. 시스템 반영 실패는 손님의 결제 거부와 별개다. 명성 변화는 기존 0을 유지한다.
- 성향 CSV의 `accept_text_idxs`를 `regular_sale_text_idxs`, `discount_sale_text_idxs`, `exploitative_sale_text_idxs`로 교체했다. 모두 필수 uint 배열이며 TextData FK를 검증한다. 구형 CSV는 새 loader에서 거부한다.
- 초기 migration은 각 성향의 기존 수락 대사 ID를 세 컬럼에 동일하게 복사했다. 신규 TextData ID·문구는 추가하지 않았다. 저가·착취 전용 문구가 승인되면 해당 컬럼만 교체한다.
- 테스트 결과명은 `OutcomeLabel`로 표시한다. 정식 UI 현지화 시 결과명도 TextData로 이관한다.

- 재사용 대상: `Assets/Scripts/Customer/`의 생성기·방문·거래 판정, `Customer/Data/`의 손님 DTO·DataTable·catalog, `Commons/Data/`의 공용 상품·텍스트·리소스 DTO·DataTable과 기존 DataTableManager/ResourceManager. 경제 CSV DTO·DataTable은 `Finance/Data/`에 둔다. 세 하위 경로 모두 `Assets/Scripts/` 기준이다.
- 구현 완료 범위: 방문마다 외형·성향 조합, 구매 목록 생성, 등장 일수 필터, 총액 제안 1회, 수락·거절 판정, 입장·결과 대사 선택.
- 통합 담당자 작업: MainScene의 화면·입력·입퇴장 연출 연결, 게임 날짜 공급, 거래 결과의 다른 시스템 전달.
- 현재 연결: GameUIController의 선택 목록을 DayProgress가 판정·정산한다. CustomerQueue는 현 진행에 미연결이며 Dev3 연결은 과거 경로다. 미연결: 원가 차감·일일 원가 집계·명성 계산·지침 공급. 재고 예약·저장 복구·재방문 인물·이동 연출은 미구현이다. `Accepted`는 가격 수락이지 후속 반영 완료가 아니다.
- `CustomerSandbox`와 `CustomerSandboxSetup`은 `Assets/Scripts/Local/`의 개인 코드이며 Git 제외다. 다른 checkout이나 공유 assembly에서 존재를 가정하지 않는다. 현재 실제 UI는 GameUIController이며 비활성화된 Dev3 파일을 이 작업에서 이동/삭제하지 않는다.
- 개인 씬 파일을 병합하지 않는다. 공유할 코드·데이터와 승인된 prefab·배치만 통합한다. 씬 규칙은 [SCENE_WORKFLOW.md](SCENE_WORKFLOW.md), 보호 변경 리뷰는 [AGENTS.md 12절](../AGENTS.md)을 따른다.

## 2. 초기화와 공개 API

정상 진입은 InitScene → LoadingScene → HubScene → LoadingScene → MainScene이다. MainScene에서 bootstrap manager를 새로 만들거나 별도 CSV 로더를 추가하지 않는다.

| API | 호출 계약 |
|---|---|
| `DataTableManager.Instance.EnsureDataLoadedAsync()` | CSV 파싱·참조 검증 완료까지 대기. 실패는 예외로 전달되며 생성·입력을 활성화하지 않는다. 씬 수명 token은 `AttachExternalCancellation(token)`으로 연결한다. |
| `DataTableManager.Instance.Customers` | 대기 성공 후 사용하는 manager 소유 `CustomerCatalog`. |
| `catalog.Appearances/Dispositions/Categories/Products.Rows` | uint PK로 조회하는 읽기 전용 사전. DTO 자체는 불변 객체가 아니므로 소비자가 수정하지 않는다. |
| `new CustomerGenerator(System.Random random)` | 난수원을 주입하고 방문 간 재사용한다. |
| `Generate(appearanceIds, dispositions, products, elapsedDays = 0, getCurrentPrices = null, getSaleRestrictions = null)` | 런타임 현재가 공급은 `() => GameSessionManager.Instance.EnsureDailyPrices().Prices`이며 null은 거부한다. 지침 공급자만 optional/null 허용하며 의미는 위 3차 계약을 따른다. 시작일은 0. 판매 가능 상품이 없으면 null, 잘못된 후보·설정은 예외. 외형·타입·타입 내 설정은 각각 균등 선정한다. |
| `CustomerVisit.BeginOffer()` | `Entering`에서만 `AwaitingOffer`로 전환. 입장 표시·연출이 준비된 시점에 한 번 호출한다. |
| `CustomerVisit.SubmitOffer(long offeredTotal, IReadOnlyList<SaleItem> saleItems)` | 최종 목록과 양의 정수 총액. 최초 희망 목록과 달라도 허용한다. `AwaitingOffer`에서 한 번만 판정하고 bool 수락 여부를 반환한다. 0·음수는 예외이며 기회를 소모하지 않는다. 재제안·잘못된 상태는 예외. |
| `CustomerVisit.Depart()` | `Accepted` 또는 `Rejected`에서만 `Departed`로 전환. 결과 확인·후속 처리 후 호출한다. 실제 GameObject 이동·파괴는 하지 않는다. |

`ValidateAndCommit`, CSV `LoadData`·`Release`는 loader/manager 소유 작업이다. MainScene 소비자가 직접 호출하지 않는다. 현재 런타임 hot reload는 제공하지 않는다.

### 전용 DataTable 구조

`CustomerCsvTable<T>`는 제거하고 `CustomerAppearanceDataTable`, `CustomerDispositionDataTable`, `ProductCategoryDataTable`, `ProductDataTable`, `TextDataTable`이 각각 `IDataLoad`를 직접 구현한다. Finance·Resource처럼 테이블 안에서 파싱·행 검증·사전 조회를 처리하며 `TryGetData(uint idx, out DTO data)`로 조회할 수 있다. `Rows` 조회 API는 유지한다. 공용 텍스트는 `DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text)`로 조회하며 CustomerCatalog에 Texts 속성을 두지 않는다.

DataTableManager가 각 DataTable을 `new`로 직접 생성·등록한다. CustomerCatalog는 등록된 외형·성향·상품 분류·상품 테이블을 생성자로 전달받아 참조한다. 게임 전체 TextData는 manager 소유이며 FK 검증 시 `ValidateAndCommit(texts, resources)`에 전달한다. 별도 인스턴스를 중복 생성하지 않는다. Customer 데이터는 상호 FK가 있으므로 `LoadData` 성공만으로 공개하지 않고 catalog 전체 검증 후 공개한다. 이 점은 독립적인 Finance·Resource 테이블과 의도적으로 다르다. CSV header·PK·FK·GUID는 변경하지 않았다.

### 방문에서 읽는 값

| 값 | 용도 |
|---|---|
| `AppearanceIdx`, `DispositionIdx` | 외형·성향 데이터 조회. 같은 조합이 다시 나와도 같은 인물을 뜻하지 않는다. |
| `Items` | 읽기 전용 구매 목록. 각 `CustomerOrderItem`의 `ProductIdx`, `Quantity`, `UnitPrice` 사용. 동일 상품은 한 줄에 수량으로 표현한다. |
| `BaseTotal`, `PriceTolerance`, `AllowedTotal` | BaseTotal·AllowedTotal은 제출 전 null, 제출 시 최종 목록의 최신 단가로 확정한다. PriceTolerance는 성향의 기존 배율이다. 상한을 UI에 자동 노출하지 않는다. |
| `State`, `OfferedTotal`, `WasAccepted` | 방문 상태, 제안 총액, 수락 여부. 판정 전 마지막 두 값은 null. 퇴장 후에도 결과 유지. |
| `EntryTextIdx`, `FeedbackTextIdx` | `texts.Rows[idx].Text`로 표시. 제안 전 Feedback은 입장 대사, 제안 후 수락·거절 대사. |

방문과 주문 항목 생성자는 internal이다. 통합 코드는 생성기를 사용하고 상태를 직접 덮어쓰지 않는다. 상태는 `Entering → AwaitingOffer → Accepted 또는 Rejected → Departed` 순서다.

## 3. 최소 호출 순서

아래는 호출 순서 예시이며 완성된 MonoBehaviour가 아니다. `token`은 화면 파괴에 연결된 CancellationToken, `elapsedDays`는 날짜 소유자가 제공하는 uint 값이다. 생성기와 현재 방문은 통합 component가 소유한다. 현재 방문의 판정·퇴장 전에 다음 방문으로 덮어쓰지 않는다.

```csharp
// using System.Linq; using Cysharp.Threading.Tasks;
await DataTableManager.Instance.EnsureDataLoadedAsync()
    .AttachExternalCancellation(token);
token.ThrowIfCancellationRequested();
var catalog = DataTableManager.Instance.Customers;
var texts = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
var generator = new CustomerGenerator(new System.Random()); // 초기화 시 한 번

// 손님 입장 요청 시
var dailyPrices = GameSessionManager.Instance.EnsureDailyPrices();
var visit = generator.Generate(
    catalog.Appearances.Rows.Keys.OrderBy(x => x).ToArray(),
    catalog.Dispositions.Rows.Values.OrderBy(x => x.Idx).ToArray(),
    catalog.Products.Rows, dailyPrices.ElapsedDays, () => GameSessionManager.Instance.EnsureDailyPrices().Prices);
if (visit == null) return; // 정상적인 판매 후보 부재: 대기/안내 처리
string entryText = texts.Rows[visit.EntryTextIdx].Text;
// 외형·상품·entryText 표시 후
visit.BeginOffer();

// 이후 사용자 입력 callback에서, 양의 정수 검증과 상태 확인 후
var saleItems = visit.Items.Select(x => new SaleItem(x.ProductIdx, x.Quantity)).ToArray(); // 실제 선택 UI 미구현 호환
bool accepted = visit.SubmitOffer(total, saleItems);
// 수락 시 기존 일일집계에 visit.Result.Value를 한 번 전달한다.
string feedback = texts.Rows[visit.FeedbackTextIdx].Text;
// feedback 표시 및 승인된 후속 처리 완료 후
visit.Depart();
```

- `total`은 별도 입력 callback이 전달하는 long이다. 위 코드를 한 프레임에 모두 실행하지 않는다.
- 입력 규격: `long.TryParse` + `NumberStyles.None` + `InvariantCulture`, 값 > 0. 공백·소수·부호·구분자·overflow는 거부하고 재입력을 허용한다.
- 수락 판정은 `total <= floor(BaseTotal × PriceTolerance / 1000)`이다. 최종 목록과 제출 순간 현재가·원가를 복사한다. 이후 외부 목록·가격 변경은 확정 결과에 영향을 주지 않는다. 금액 범위 초과는 실패로 처리한다.
- 확률과 배율은 1000=100%. 현재 테스트 선호 확률 900, 허용 배율 1100/1300/1000이다. 단위 규칙은 [CSV_RULES.md](CSV_RULES.md)를 따른다.
- 방문 결과 event는 없다. 통합 호출자가 Result를 읽어 영업 중인 `Economy.DailyAggregationService.TryApplyTransaction`에 한 번 전달한다. 집계 API 자체에 거래 ID 기반 중복 방지는 없으므로 호출자 소유의 한 번 반영 상태가 필요하다. false/예외는 반영 실패이며 재제안·자동 재입금으로 우회하지 않는다.

## 4. 데이터와 표시 연결

| 대상 | 현재 연결 |
|---|---|
| 외형 | `Appearances.Rows[visit.AppearanceIdx]`의 RGBA. 현재는 사각형+색상, 외형 파츠·정식 sprite 계약 없음. |
| 상품 이름 | `Products.Rows[item.ProductIdx].NameIdx → Texts.Rows[idx].Text` |
| 상품 종류 | `ProductData.ProductType` enum. `Categories.Rows.Values`에서 동일 ProductType 행을 찾아 `NameIdx → Text`로 UI 표시. enum 이름을 표시명이나 내부 문자열 키로 쓰지 않는다. |
| 상품 이미지 | `ImageResourceIdx`가 null이면 흰색 기본 사각형+상품 이름. 값이 있으면 `GetDB<ResourceDataTable>(DataTableType.Resource).GetResourcePath(idx)` → `ResourceManager.LoadAssetAsync<Sprite>(path)`. |
| 대사 | 성향의 entry/regular_sale/discount_sale/exploitative_sale/reject TextIdx 배열에서 방문 생성 시 각 1개 추첨. 실제 문장은 TextData에만 저장. |
| 등장 조건 | `IsAvailable && AvailableDay <= elapsedDays`. 실제 재고 보유량 필터는 아직 없음. |

이미지 FK의 0은 빈값이 아니다. CSV 빈 셀만 null이며, 잘못된 FK·Sprite 로드 실패를 기본 이미지로 숨기지 않는다. 비동기 로딩에는 씬 수명 취소를 붙이고 성공 후 표시한다. ResourceManager 소유 공유 자산을 화면 종료 시 임의 Destroy/전체 Release하지 않는다. 화면이 직접 만든 사각형 Sprite와 임시 글꼴만 화면이 정리한다.

필수 데이터는 `Assets/Datas/Customer/`의 CustomerAppearanceData, CustomerDispositionData, ProductCategoryData, ProductData와 `Assets/Datas/TextData.csv`, `ResourceData.csv`다. 기존 Addressables의 확장자 없는 address와 `Datas` label을 유지한다.

현재 header는 실제 CSV를 기준으로 한다. 특히 구버전 `preferred_category_ids`/`category_idx` 대신 `preferred_product_types`/`product_type`을 사용한다. ProductType은 None=0(사용 금지), Water=1, Food=2, Medicine=3, DailyNecessities=4다.

Resource PK는 현재 4000+n이며 이전 3000+n 참조는 통합 전에 점검한다. 기존 ResourceData.path와 GUID는 유지했다. PlayerData는 미사용으로 제거했다. 이 설명은 신규 ID 배정 권한이 아니며 충돌 검사는 [DATA_RULES.md](DATA_RULES.md)와 [CSV_RULES.md](CSV_RULES.md)를 따른다.

## 5. 테스트 UI와 정식 통합의 경계

아래는 Git 제외 개인 코드가 로컬에 있는 경우만 적용한다. 공유 설치 요구사항이 아니다.

`CustomerSandbox` 공개 API는 `CurrentVisit`, `GenerateCustomer()`, `SubmitPrice()`다. `SubmitPrice()`는 Inspector에 연결된 InputField를 읽는 테스트 callback이지 총액 인자를 받는 게임 서비스가 아니다. 상태 갱신 후 UI를 함께 바꾸므로 외부에서 `CurrentVisit`만 변경하면 화면과 어긋날 수 있다. 정식 UI는 생성기·방문 API를 직접 연결한다.

테스트용 Inspector 연결: appearanceImage, identityText, orderText, statusText, generateButton, offerInput, offerButton, dialogText, productRoot 및 elapsedDays. 설치 도구는 개인 씬에서 이 배치를 생성·갱신한다. 정식 UI prefab은 별도 승인 범위에서 제작·연결해야 하며 Local 씬·자산 참조를 포함하지 않는다. 테스트 OS 글꼴·PK 표시·버튼 생성 방식은 정식 UI 사양이 아니다.

## 6. 통합 담당자 완료 체크

1. 최신 기본 branch와 작업 branch의 CSV PK, enum, 공용 manager, Addressables, GUID 충돌 확인. 기존 수정 전체를 부분 복사하지 말고 코드·CSV·loader를 함께 통합한다.
2. 통합 Scene/Prefab 편집 담당자, 날짜·자금·재고 소유자와 이번 허용 범위 지정. 미구현 후속 처리는 완료로 보고하지 않는다.
3. MainScene에 UI와 수명 소유 component 연결. 생성 전 데이터·필요 이미지 준비 확인. 중복 manager·버튼 구독·재제안 방지.
4. Init부터 MainScene 진입, 판매 후보 없음, 등장 날짜 경계, 상품 이름·수량·이미지 fallback, 입장 대사 확인.
5. 허용 총액과 같은 값 수락 / 1 큰 값 거절, 잘못된 입력 후 재입력, 두 번째 제안 차단, 결과 대사·퇴장·다음 방문 확인.
6. 씬 이탈 중 로딩 취소, 재진입·반복 방문에서 오류·객체·구독 누적 확인. 정식 이미지가 추가됐다면 빈값 테스트와 별도로 실제 Sprite 로드를 검증한다.
7. Unity 컴파일 오류와 제품 Console 오류를 분리해 보고. Player 빌드는 Editor 실행과 별도 검증. 보호 변경은 기본 branch 반영 전 교차 리뷰·명시적 동의 기록.

현재 자동 API 검사는 [TESTING.md](TESTING.md)의 Test Runner를 사용한다. 이전 개별 셸은 제거했으며 UI 동작은 위 수동 완료 체크로 확인한다. CSV 음성 검사의 기대 LogError를 제품 오류와 구분한다.

초기 인계 문서 작성 당시에는 컴파일·Play 검증을 수행하지 않았다. 이후 거래 확정 구현의 검증 결과와 실제 실행 범위는 1절에 기록했다. Test Runner 전환 후 실행 증거는 TESTING.md에 분리했다. MainScene 자산 자체의 통합 완료 판정은 실제 통합 후 별도로 확인한다.
