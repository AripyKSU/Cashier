# 손님·상품 시스템 안내

현재 공개 API·CSV 의미·거래 판정의 단일 상세 명세는 [CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md)다. 이 문서는 진입 안내와 과거 검증 기록만 유지한다.

아래의 "현재"·"미연결"·화면 담당 클래스 표현은 작성 당시 요약으로 보존한다. 실제 작업은 해당 상세 명세와 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md), 현재 checkout을 대조해 판단하고, 과거 요약을 근거로 구현을 되돌리지 않는다.

## 현재 데이터와 실행 흐름

- 외형·성향·상품·상품 분류는 각각 전용 DataTable에서 파싱하고 CustomerCatalog의 FK 검증 후 공개한다. 제거된 CustomerCsvTable<T>를 복원하지 않는다.
- 표시 이름은 nameidx → 공용 TextData.Text, 상품 종류는 product_type enum, 선호는 preferred_product_types OR preferred_product_idxs다. 구형 category_idx/preferred_category_ids 열은 사용하지 않는다.
- 실제 헤더는 Assets/Datas/Customer/*.csv와 DTO의 Name 매핑을 함께 확인한다. 확률·배율은 1000=100%, 필수 열 누락을 DTO 기본값으로 보완하지 않는다. PK 배정은 [CSV_RULES.md](CSV_RULES.md), 로딩 검증은 [DATA_RULES.md](DATA_RULES.md)를 따른다.
- 방문 생성 시 타입·속성·최초 희망 목록을 고정한다. 가격 제안 시 최종 SaleItem 목록과 최신 현재가를 확정해 TransactionResult를 만든다. 상품 원가는 결과에 기록하지만 실제 차감·일일 원가 집계는 미연결이다.
- 대기열 시간·FIFO는 [CUSTOMER_QUEUE_INTEGRATION.md](CUSTOMER_QUEUE_INTEGRATION.md), 일간 가격·라디오는 [PRICE_EVENT_INTEGRATION.md](PRICE_EVENT_INTEGRATION.md)를 따른다.

## 실행과 검증

- 공유 통합 화면은 MainScene의 Dev3SandboxTester다. [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)의 사용법으로 사용자 UI 검증을 수행한다.
- 개인 씬 및 CustomerSandbox/CustomerSandboxSetup은 Assets/Scenes/Local 및 Assets/Scripts/Local에만 존재할 수 있고 Git 제외다. 다른 checkout에서 설치 메뉴·개인 코드의 존재를 가정하지 않는다.
- API 자동 검증은 [TESTING.md](TESTING.md)의 표준 Test Runner 또는 Tools/Run-Tests.ps1을 사용한다. 기존 개별 Check 스크립트는 제거했다. 자동 API 통과는 MainScene UI 검수 통과가 아니다.

## 과거 구현 검증 기록

아래는 당시 사실을 보존한 기록이다. 구형 ID·행 수·미구현 범위·Git 상태는 현재 계약이나 실행 지시가 아니며 최신 증거는 TESTING.md를 따른다.

### 확률 1000 기준 전환 검증

- 컴파일 완료, compileFailed=False. 생성기 검사 통과: 900 설정의 선호 선택 8,953/10,000, 0·1000 경계 및 -1·1001 거부.
- 실제 CSV의 세 성향 값 900 확인, CSV 실패 검사 19/19 통과 (예상 LogError 19건). 구형 확률 header와 범위 밖 값도 거부한다.
- GameplaySandbox 직접 Play에서 방문 생성과 UI의 `90%` 표시 확인, 제품 Console 오류·경고 0건. Init 경로의 첫 자동 확인은 씬 로드 전에 실행되어 검사 자체가 실패했고, 개인 씬 직접 Play에서 재확인했다.
- Play 중지 후 기존 InitScene과 runInBackground=false로 복구했다. 이번 전환은 아직 commit·push하지 않았다.

### ID·nameidx 이관 검증

- `PASS`: Unity compileFailed=False, InitScene → HubScene → GameplaySandbox 진입 및 버튼 100회 생성 확인.
- 과거 상품·Resource·Text ID 이관 당시의 enum·로더 분류를 확인했다. 이 항목은 과거 이관 기록이며 현재 상품 카탈로그와 행 구성은 [DATA_CATALOG.md](DATA_CATALOG.md)를 따른다.
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
