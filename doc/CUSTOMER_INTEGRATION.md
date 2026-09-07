# 손님·상품 시스템 MainScene 인계

기준: `customer_sys`의 `214df99` 구현. 이 문서는 기존 공개 API와 통합 시 필요한 작업을 설명한다. MainScene 연결 자체가 완료됐다는 의미는 아니다. 이전 `CUSTOMER_SYSTEM.md`의 구형 스키마·미구현 설명 대신 이 문서와 실제 코드를 확인한다.

## 1. 통합 범위와 책임

- 재사용 대상: `Assets/Scripts/Customer/`의 생성기, 방문·거래 판정, CSV DTO·catalog와 기존 DataTableManager/ResourceManager.
- 구현 완료 범위: 방문마다 외형·성향 조합, 구매 목록 생성, 등장 일수 필터, 총액 제안 1회, 수락·거절 판정, 입장·결과 대사 선택.
- 통합 담당자 작업: MainScene의 화면·입력·입퇴장 연출 연결, 게임 날짜 공급, 거래 결과의 다른 시스템 전달.
- 미구현: 자금 증감, 재고 차감·예약, 매출 기록, 저장·복구, 손님 이동·대기열, 재방문 인물 관리. `Accepted`는 가격 수락이지 결제·재고 반영 완료가 아니다.
- `CustomerSandbox`는 개인 씬의 테스트 화면이다. 정식 공용 prefab이나 MainScene 설치기가 아니다. `CustomerSandboxSetup`은 Local 씬에서만 사용한다.
- 개인 씬 파일을 병합하지 않는다. 공유할 코드·데이터와 승인된 prefab·배치만 통합한다. 씬 규칙은 [SCENE_WORKFLOW.md](SCENE_WORKFLOW.md), 보호 변경 리뷰는 [AGENTS.md 12절](../AGENTS.md)을 따른다.

## 2. 초기화와 공개 API

정상 진입은 InitScene → LoadingScene → HubScene → LoadingScene → MainScene이다. MainScene에서 bootstrap manager를 새로 만들거나 별도 CSV 로더를 추가하지 않는다.

| API | 호출 계약 |
|---|---|
| `DataTableManager.Instance.EnsureDataLoadedAsync()` | CSV 파싱·참조 검증 완료까지 대기. 실패는 예외로 전달되며 생성·입력을 활성화하지 않는다. 씬 수명 token은 `AttachExternalCancellation(token)`으로 연결한다. |
| `DataTableManager.Instance.Customers` | 대기 성공 후 사용하는 manager 소유 `CustomerCatalog`. |
| `catalog.Appearances/Dispositions/Categories/Products/Texts.Rows` | uint PK로 조회하는 읽기 전용 사전. DTO 자체는 불변 객체가 아니므로 소비자가 수정하지 않는다. |
| `new CustomerGenerator(System.Random random)` | 난수원을 주입하고 방문 간 재사용한다. |
| `Generate(IReadOnlyList<uint> appearanceIds, IReadOnlyList<CustomerDispositionData> dispositions, IReadOnlyDictionary<uint, ProductData> products, uint elapsedDays = 0)` | 검증된 catalog 후보를 전달한다. 시작일은 0. 판매 가능 상품이 없으면 null, 잘못된 후보·설정은 예외. 외형·성향 후보는 균등 선정한다. |
| `CustomerVisit.BeginOffer()` | `Entering`에서만 `AwaitingOffer`로 전환. 입장 표시·연출이 준비된 시점에 한 번 호출한다. |
| `CustomerVisit.SubmitOffer(long total)` | 전체 목록에 대한 양의 정수 총액. `AwaitingOffer`에서 한 번만 판정하고 bool 수락 여부를 반환한다. 0·음수는 예외이며 기회를 소모하지 않는다. 재제안·잘못된 상태는 예외. |
| `CustomerVisit.Depart()` | `Accepted` 또는 `Rejected`에서만 `Departed`로 전환. 결과 확인·후속 처리 후 호출한다. 실제 GameObject 이동·파괴는 하지 않는다. |

`ValidateAndCommit`, CSV `LoadData`·`Release`는 loader/manager 소유 작업이다. MainScene 소비자가 직접 호출하지 않는다. 현재 런타임 hot reload는 제공하지 않는다.

### 방문에서 읽는 값

| 값 | 용도 |
|---|---|
| `AppearanceIdx`, `DispositionIdx` | 외형·성향 데이터 조회. 같은 조합이 다시 나와도 같은 인물을 뜻하지 않는다. |
| `Items` | 읽기 전용 구매 목록. 각 `CustomerOrderItem`의 `ProductIdx`, `Quantity`, `UnitPrice` 사용. 동일 상품은 한 줄에 수량으로 표현한다. |
| `BaseTotal`, `PriceTolerance`, `AllowedTotal` | 방문 생성 시 고정된 정가 합계·허용 배율·수락 상한. 상한을 UI에 자동 노출하지 않는다. |
| `State`, `OfferedTotal`, `WasAccepted` | 방문 상태, 제안 총액, 수락 여부. 판정 전 마지막 두 값은 null. 퇴장 후에도 결과 유지. |
| `EntryTextIdx`, `FeedbackTextIdx` | `catalog.Texts.Rows[idx].Text`로 표시. 제안 전 Feedback은 입장 대사, 제안 후 수락·거절 대사. |

방문과 주문 항목 생성자는 internal이다. 통합 코드는 생성기를 사용하고 상태를 직접 덮어쓰지 않는다. 상태는 `Entering → AwaitingOffer → Accepted 또는 Rejected → Departed` 순서다.

## 3. 최소 호출 순서

아래는 호출 순서 예시이며 완성된 MonoBehaviour가 아니다. `token`은 화면 파괴에 연결된 CancellationToken, `elapsedDays`는 날짜 소유자가 제공하는 uint 값이다. 생성기와 현재 방문은 통합 component가 소유한다. 현재 방문의 판정·퇴장 전에 다음 방문으로 덮어쓰지 않는다.

```csharp
// using System.Linq; using Cysharp.Threading.Tasks;
await DataTableManager.Instance.EnsureDataLoadedAsync()
    .AttachExternalCancellation(token);
token.ThrowIfCancellationRequested();
var catalog = DataTableManager.Instance.Customers;
var generator = new CustomerGenerator(new System.Random()); // 초기화 시 한 번

// 손님 입장 요청 시
var visit = generator.Generate(
    catalog.Appearances.Rows.Keys.OrderBy(x => x).ToArray(),
    catalog.Dispositions.Rows.Values.OrderBy(x => x.Idx).ToArray(),
    catalog.Products.Rows, elapsedDays);
if (visit == null) return; // 정상적인 판매 후보 부재: 대기/안내 처리
string entryText = catalog.Texts.Rows[visit.EntryTextIdx].Text;
// 외형·상품·entryText 표시 후
visit.BeginOffer();

// 이후 사용자 입력 callback에서, 양의 정수 검증과 상태 확인 후
bool accepted = visit.SubmitOffer(total);
string feedback = catalog.Texts.Rows[visit.FeedbackTextIdx].Text;
// feedback 표시 및 승인된 후속 처리 완료 후
visit.Depart();
```

- `total`은 별도 입력 callback이 전달하는 long이다. 위 코드를 한 프레임에 모두 실행하지 않는다.
- 입력 검사는 기존 `CustomerSandbox.SubmitPrice()` 참고: `long.TryParse` + `NumberStyles.None` + `InvariantCulture`, 값 > 0. 공백·소수·부호·구분자·overflow는 거부하고 재입력을 허용한다.
- 수락 판정은 `total <= floor(BaseTotal × PriceTolerance / 1000)`이다. 금액·수량은 방문 snapshot을 사용하며 현재 CSV 단가로 다시 계산하지 않는다. 금액 범위 초과는 실패로 처리한다.
- 확률과 배율은 1000=100%. 현재 테스트 선호 확률 900, 허용 배율 1100/1300/1000이다. 단위 규칙은 [CSV_RULES.md](CSV_RULES.md)를 따른다.
- 결과 event나 결제 API는 아직 없다. 통합 호출자가 반환값·방문 상태를 읽는다. 나중에 자금·재고를 연결할 때 중복 반영 방지와 실패 처리 책임을 해당 시스템 담당자와 먼저 정한다.

## 4. 데이터와 표시 연결

| 대상 | 현재 연결 |
|---|---|
| 외형 | `Appearances.Rows[visit.AppearanceIdx]`의 RGBA. 현재는 사각형+색상, 외형 파츠·정식 sprite 계약 없음. |
| 상품 이름 | `Products.Rows[item.ProductIdx].NameIdx → Texts.Rows[idx].Text` |
| 상품 종류 | `ProductData.ProductType` enum. `Categories.Rows.Values`에서 동일 ProductType 행을 찾아 `NameIdx → Text`로 UI 표시. enum 이름을 표시명이나 내부 문자열 키로 쓰지 않는다. |
| 상품 이미지 | `ImageResourceIdx`가 null이면 흰색 기본 사각형+상품 이름. 값이 있으면 `GetDB<ResourceDataTable>(DataTableType.Resource).GetResourcePath(idx)` → `ResourceManager.LoadAssetAsync<Sprite>(path)`. |
| 대사 | 성향의 entry/accept/reject TextIdx 배열에서 방문 생성 시 각 1개 추첨. 실제 문장은 TextData에만 저장. |
| 등장 조건 | `IsAvailable && AvailableDay <= elapsedDays`. 실제 재고 보유량 필터는 아직 없음. |

이미지 FK의 0은 빈값이 아니다. CSV 빈 셀만 null이며, 잘못된 FK·Sprite 로드 실패를 기본 이미지로 숨기지 않는다. 비동기 로딩에는 씬 수명 취소를 붙이고 성공 후 표시한다. ResourceManager 소유 공유 자산을 화면 종료 시 임의 Destroy/전체 Release하지 않는다. 화면이 직접 만든 사각형 Sprite와 임시 글꼴만 화면이 정리한다.

필수 데이터는 `Assets/Datas/Customer/`의 CustomerAppearanceData, CustomerDispositionData, ProductCategoryData, ProductData와 `Assets/Datas/TextData.csv`, `ResourceData.csv`다. 기존 Addressables의 확장자 없는 address와 `Datas` label을 유지한다.

현재 header는 실제 CSV를 기준으로 한다. 특히 구버전 `preferred_category_ids`/`category_idx` 대신 `preferred_product_types`/`product_type`을 사용한다. ProductType은 None=0(사용 금지), Water=1, Food=2, Medicine=3, DailyNecessities=4다.

Resource PK는 현재 4000+n이며 이전 3000+n 참조는 통합 전에 점검한다. 기존 ResourceData.path와 GUID는 유지했다. PlayerData는 미사용으로 제거했다. 이 설명은 신규 ID 배정 권한이 아니며 충돌 검사는 [DATA_RULES.md](DATA_RULES.md)와 [CSV_RULES.md](CSV_RULES.md)를 따른다.

## 5. 테스트 UI와 정식 통합의 경계

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

기존 확인 도구: `Tools/Check-CustomerGenerator.ps1`, `Tools/Check-CustomerCsv.ps1` (프로젝트를 연 Unity와 unity-cli 필요), `Tools/Check-CustomerTradeUI.ps1` (설정된 Sandbox Play 필요). 마지막 도구는 MainScene acceptance를 대신하지 않는다. CSV 음성 검사는 의도된 LogError를 발생시키므로 제품 오류와 구분한다.

이 인계 문서 작성에서는 코드·Scene을 변경하거나 컴파일·Play 검증을 새로 실행하지 않았다. MainScene 통합 완료 판정은 위 항목을 실제 통합 후 확인하고 AGENTS.md의 검증 상태로 보고한다.
