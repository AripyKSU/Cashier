# 일반 손님 생성과 로컬 테스트

- 범위: `Assets/Scripts/Customer/`의 생성 로직, 실제 CSV·로더, 개인 씬의 사각형 외형·PK·구매 목록·생성 버튼. 가격 판정·대사·재방문은 제외한다.
- `CustomerDispositionData`: 성향별 구매 설정. 기본 종류 1~3종, 수량 1~3개, 선호군 선택 90%.
- `CustomerGenerator.Generate`: 검증된 외형 ID 후보, 성향 후보, 판매 가능 상품 ID → 상품군 ID 사전을 받는다. 외형과 성향 후보는 현재 균등 선정한다.
- `CustomerVisit`: 외형 ID·성향 ID와 읽기 전용 구매 목록. `CustomerOrderItem`은 상품 ID와 수량을 보관한다.
- 구매 종류 수는 가능한 수로 상한을 제한한 범위에서 균등 선정한다. 수량도 범위 내 균등 선정한다.
- 선호 90%는 양쪽 후보가 남은 개별 상품 선택에 적용한다. 해당 군 안의 상품은 동일 확률이며 이미 고른 상품은 제외한다. 한쪽 소진 시 다른 쪽을 사용한다.
- 판매 가능한 상품이 없으면 null이다. 후보 누락, 0·중복 ID, 잘못된 설정은 예외로 구분한다.
- 모든 외형·성향 후보를 검사한 뒤 난수를 사용한다. 방문 결과는 입력 설정·사전을 나중에 바꿔도 변경되지 않는다.
- 상품 사전의 삽입 순서는 결과에 영향을 주지 않는다. 재현에는 같은 seed와 동일 순서의 외형·성향 후보를 제공한다.
- 상품 재고 수량 제한·예약은 미구현이며 현재 입력은 판매 가능 여부만 표현한다.

## GameplaySandbox 사용법

1. `Assets/Scenes/Local/GameplaySandbox.unity`를 열고 Play한다. 직접 Play도 기존 ResourceManager·DataTableManager를 통해 초기화한다.
2. 로딩이 끝나면 **무작위 손님 생성**을 누른다. 같은 UI 객체의 외형 색상, 외형 PK + 성향 PK, 성향·선호군, 상품명·수량을 교체한다.
3. 조합은 매번 독립 추첨이므로 직전과 같은 외형·성향이 나올 수 있다. 하단 생성 횟수로 버튼 동작을 확인한다.
4. CSV 값을 수정한 뒤에는 Play를 중지하고 다시 시작한다. 실행 중 데이터 hot reload는 제공하지 않는다.
5. InitScene 경로도 사용할 수 있다. `Cashier > Gameplay Scene Settings`에서 개인 씬을 선택하고 InitScene부터 Play한다.

다른 PC에서는 개인 씬이 Git에 없으므로 `Assets/Scenes/Local/`에 씬을 저장한 뒤 `Cashier > Setup Customer Sandbox`를 실행하고 저장한다. 설치 도구는 기존 객체를 삭제하지 않으며, 이미 화면이 있으면 중복 생성하지 않는다. 개인 씬은 Build Settings·Addressables에 등록하지 않는다.

사각형은 공용 white texture로 만든 임시 Sprite를 uGUI Image에 표시한다. 정식 아트 자산이 아니며 화면 파괴 시 Sprite를 해제한다. 한국어 표시에는 로컬 OS 글꼴(Windows: Malgun Gothic)을 사용한다. 폰트 파일을 복사하거나 TMP 기본 리소스를 변경하지 않으며, 다른 OS에서는 해당 한국어 글꼴의 가용성을 확인해야 한다.

## CSV 스키마와 종류 ID 확인

종류 ID의 배정·예약은 [`CSV_RULES.md` 3절](CSV_RULES.md)이 연결하는 Google Docs의 **CSV 종류 ID**를 단일 권위로 사용한다. 이 문서는 배정 목록을 복제하지 않는다. 실제 CSV·enum과 권위 목록이 다르면 이를 보고하고, 새 배정이나 추가 이관을 임의로 수행하지 않는다. 아래는 현재 구현의 컬럼 명세다.

| CSV (`Assets/Datas/Customer/`) | 필수 열 (순서 유지) |
|---|---|
| CustomerAppearanceData.csv | idx, nameidx, color_r, color_g, color_b, color_a |
| CustomerDispositionData.csv | idx, nameidx, preferred_category_ids, preferred_selection_percent, min_product_kinds, max_product_kinds, min_quantity, max_quantity |
| ProductCategoryData.csv | idx, nameidx |
| ProductData.csv | idx, nameidx, category_idx, is_available |
| ../TextData.csv | idx, text |

- UTF-8, 쉼표 구분, idx 첫 열. 배열은 기존 UIntArrayConverter의 `_` 구분을 사용한다. 판매 여부는 기존 converter의 0/1 규칙이다.
- color_r/g/b는 0~255, color_a는 1~255인 byte 값이다. 기존 외형 색상은 숫자 채널로 동일하게 보존한다.
- 성향 선호군은 테스트값: 평범=식수·식량, 급함=의료, 가격 민감=생활. 확정된 성향 효과나 가격 허용치를 뜻하지 않는다.
- `preferred_category_ids`와 `category_idx`는 ProductCategoryData.idx를 참조한다. 모든 `nameidx`는 TextData.idx를 참조한다. 실제 이름은 TextData.text에서만 관리하며 문자열 이름을 내부 식별자로 사용하지 않는다.
- `is_available=0`인 상품은 생성 후보에서 제외한다. 전부 0이면 손님을 만들지 않고 화면에 이유를 표시한다.
- 손님 관련 4개 CSV와 TextData.csv는 기존 Default Local Group, `Datas` label에 등록하며 address는 확장자 없는 파일명이다. 테스트 도구나 개인 씬은 Addressables에 등록하지 않는다.

### 타입·범위·빈값

- `idx`, `nameidx`, `category_idx`: `uint`, 0 금지. idx는 테이블 안에서 고유하며 FK는 대상 행이 존재해야 한다.
- `preferred_category_ids`: `IReadOnlyList<uint>`로 읽는 `_` 구분 숫자 배열. 빈 셀은 선호군 없음이며 0·중복 원소는 금지한다. 각 원소는 ProductCategoryData.idx를 참조한다.
- `preferred_selection_percent`: `int`, 0~100. CSV에서는 필수이며 DTO 초기값 90을 누락·빈 셀의 대체값으로 사용하지 않는다.
- 확률의 신규 공통 기준은 [CSV_RULES.md 6절](CSV_RULES.md)의 `1000 = 100%`다. 현재 손님 구현의 `preferred_selection_percent`는 기존 0~100 계약이며 이번 문서 변경만으로 재해석하지 않는다. 후속 승인된 migration에서 `preferred_selection_permille`, 기본값 900, 범위 0~1000과 난수 분모 1000으로 CSV·DTO·생성기·검사를 함께 전환한다. 현재 컬럼에 900을 입력하면 검증 실패다.
- `min_product_kinds`/`max_product_kinds`, `min_quantity`/`max_quantity`: `int`, 1 이상·min≤max·max<int.MaxValue. CSV 누락·빈 셀은 거부한다. 생성 시 종류 수만 판매 가능 종류 수로 제한한다.
- `color_r/g/b/a`: `byte`, 위 색상 범위 적용. `is_available`: 기존 ZeroOneBooleanConverter로 읽는 `bool`, CSV에서는 0/1만 허용한다.
- `text`: `string`, 실제 표시 문구이므로 허용한다. 빈값·공백만 있는 값은 금지하며 원래 문구를 보존한다.
- 그 외 컬럼의 빈값·누락은 허용하지 않는다. 기존 CsvHelper Name mapping과 UIntArrayConverter를 사용하고, 명시적 예외 외 문자열 컬럼을 추가하지 않는다.

### 이전 작업의 이관 기록 (권위 배정 목록 아님)

기존 Resource의 `1000+n`을 `3000+n`, 테스트 상품의 `8000+n`을 `1000+n`으로 이동했고, Text enum은 2에서 8로 이동했다. 사용되지 않던 Text DTO의 `kr`는 실제 문자열 컬럼 `text`로 정리했다. ResourceData.path, 기존 asset GUID와 address는 보존했다.

이 기록은 당시 변경의 복구·검증 근거이지 새 ID 배정 기준이 아니다. 이전 ID가 저장된 외부 데이터·세이브는 테이블 종류를 아는 이관이 필요하며, 숫자만 보고 자동 치환하거나 예전 ID를 별칭으로 허용하지 않는다. 외부 세이브 이관기는 추가하지 않았다.

## 로딩·오류 계약

기존 `DataTableManager`가 `idx / 1000`으로 로더를 선택한다. 손님 데이터와 TextData는 다섯 `CustomerCsvTable<T>`를 사용한다. CsvHelper로 header·형식·PK·행 범위를 검증하고 대기 데이터에 보관한 뒤, `CustomerCatalog.ValidateAndCommit`에서 상품군·nameidx FK를 모두 검사하여 함께 공개한다. 로딩 완료 전 공개 행 수는 0이다. Text 로더는 `DataTableType.Text`에도 등록되어 있다.

외부 데이터 오류는 파일·행/PK·열·원인을 LogError에 기록하고 예외를 발생시킨다. manager는 실패를 `EnsureDataLoadedAsync`의 대기자에게 전달하며 성공 상태로 바꾸지 않는다. 테스트 화면은 생성 버튼을 비활성화한다. 잘못된 데이터로 임시 손님을 만들지 않는다. ResourceData도 이관된 003 대역·PK 중복·필수 path를 확인한 뒤 캐시를 교체한다. 기존 path가 실제 자산으로 해석되는지 전수 검증한 것은 아니다.

공용 변경: DataTableType 대역 추가와 DataTableManager의 로딩 완료·오류 전파 및 FK 검증. 기본 branch 통합 전 프로젝트 규칙에 따른 교차 리뷰가 필요하다. 사용자 요청에 따라 customer_sys에 commit·push하며 기본 branch 통합은 포함하지 않는다.

## 미확정 사항

가격 허용 수식·대사 조건·외형 파츠 구조는 별도 확정 후 추가한다. 현재 Visit의 성향 ID만으로 향후 가변 가격 설정을 실시간 조회하는 방식은 사용하지 않고, 가격 계약 확정 시 방문별 적용값을 고정해야 한다.

## 최소 실행 검사

Unity Editor가 이 프로젝트를 열고 컴파일을 끝낸 후 PowerShell에서 실행한다.

```powershell
./Tools/Check-CustomerGenerator.ps1
./Tools/Check-CustomerCsv.ps1
```

생성기 검사용 ID는 메모리 안에서만 사용한다. CSV 검사는 실제 원본을 읽고 메모리 사본만 변형하며, 정상 동작에서 예상 LogError 16건을 출력한다. 이 오류들은 제품 실행 오류와 구분한다. 검사는 Test Runner·test assembly를 추가하지 않는다.

### ID·nameidx 이관 검증

- `PASS`: Unity compileFailed=False, InitScene → HubScene → GameplaySandbox 진입 및 버튼 100회 생성 확인.
- 상품 1001~1012, Resource 3001~3099의 사용 중인 65행, Text 8001~8023의 enum·로더 분류 확인. Resource는 기존 PK에 2000만 더했고 모든 path 값과 행 순서는 동일하다.
- 외형·성향·상품군·상품·Text 4/3/4/12/23행 로딩. nameidx 조회 결과와 이전 RGBA 색상의 화면 표시 확인.
- CSV 오류 16종 차단 및 실패 데이터 비공개 검사 통과. 기존 생성기 검사도 통과.
- 정상 Play 후 제품 Console 오류·경고 0건. 검증 중만 사용한 runInBackground 옵션은 false로 복구했고 Play를 중지했다.
- 외부 세이브의 구 ID 이관 및 기존 Resource path 전체의 자산 존재 검사는 수행하지 않았다. 밸런스는 종류 ID만 예약하고 CSV·게임 로직을 추가하지 않았다.

### 이전 구현 검증 기록 (ID·nameidx 이관 전)

- 생성 로직·CSV 검사: Unity import·컴파일 후 수행.
- 1,000회 생성의 종류·수량 범위 및 상품 중복 검사 통과.
- 선호 선택 8,953/10,000회. 0%·100% 경계, 후보 소진·부족·빈 목록, 결과 보존, 동일 seed 재현, 잘못된 입력 거부 통과.
- 실제 CSV 4/3/4/12행 로딩, 잘못된 header·중복 PK·대역·상품 FK·성향 FK·boolean·수량·색상 8종 차단. 실패 데이터 비공개 및 기존 공개 데이터 보존 확인.
- GameplaySandbox 직접 Play에서 버튼 100회 교체, raycast·pointer click 처리, 사각형·PK·한국어 구매 목록 화면 확인.
- InitScene → HubScene → GameplaySandbox 진입과 생성 성공. 판매 후보 0개 처리 후 복구, 추가 100회 교체 중 외형 4종·성향 3종 및 UI 객체 수 유지 확인.
- 정상 실행 후 제품 Console 오류·경고 0건. 화면은 테스트를 위해 개인 씬에만 저장했으며 Git 제외를 유지한다.
- 가격 판정, 재고 예약, 정식 아트·대사·재방문과 Player build는 범위 밖이다.
