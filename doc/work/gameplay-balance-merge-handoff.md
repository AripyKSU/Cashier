# GameplayBalance 브랜치 병합 인계

작성 기준: 2026-09-23 09:34 KST. `total_merge`에서 갈라진 커밋 4건과 현재 미커밋 변경을 함께 다룬다. 이 문서는 병합용 설명이며 실제 Git merge·commit·push를 수행한 기록은 아니다.

## 1. 기준과 상태

| 항목 | 내용 |
|---|---|
| 작업 브랜치 | `GameplayBalance` |
| 분기점 | `63f42749dd54580a9b58d9ad9daa7501c36865eb` (`total_merge`) |
| 현재 HEAD | `ea0dd31e6d14839712517ad37cc0353d8ee8eabf` |
| 로컬 원격 추적 | `origin/GameplayBalance` = `d198931482bf79e1711b844253693d9fb9902485`; 로컬 HEAD가 1커밋 앞섬. fetch하지 않아 실제 원격 최신 상태는 미확인 |
| 변경 범위 | 분기점 대비 56개 기존 파일; 그중 21개에 미커밋 변경. 이 문서 1개는 신규 |
| 인계 상태 | PARTIAL: 관련 기능 검증 완료, 기존 회귀 실패 3건과 실제 화면·20일 밸런스 검수 남음 |
| 담당 기록 | 기존 커밋 작성자의 Git email은 TEAM_ROLES의 김승욱 조회 키에 대응. Git identity는 승인 증거가 아님 |

**HEAD만 병합하면 전량 제외 전용 대사 작업이 빠진다.** 아래 미커밋 파일을 커밋에 포함해 실제 전달 SHA를 정한 뒤 병합한다. 작성 전 staged·untracked·stash는 없었고 이 문서 외 작업 파일은 보존했다.

| 순서 | 커밋/상태 | 범위 |
|---|---|---|
| 1 | `7084aa17` | 20일차 진행·경제 리밸런싱, 가격 이벤트 운영 중단, 데이터/런타임/UI·문서 |
| 2 | `f0f2055a` | 설비창 가격 텍스트 위치·크기와 자동 크기 조절 |
| 3 | `d1989314` | 부자·가난 손님의 구매 종류/최소 수량 조정 |
| 4 | `ea0dd31e` | 표시 1~6일차 상품 1종당 최대 2개 제한 |
| 5 | 미커밋 | 전량 제외 거절 사유, 성향 CSV 7열, 전용 Text 28개, 관련 테스트·명세 |

## 2. 20일 진행과 밸런스

- `GameSessionManager.FinalDay=20`, 하루 영업 120초. `elapsedDays=0`은 표시 1일차, `19`는 표시 20일차다. 20일차 정산 뒤 21일차를 만들지 않는다. 시민권 미보유 시 Bad 엔딩이며 유지비 미납과 시민권 즉시 엔딩 우선순위는 기존 계약을 따른다.
- 일일 상품 종류는 표시 1~6일차 4종, 7~12일차 6종, 13~20일차 8종. 기본/1/2/3단계 가중치는 `100/250/450/700`. 최고 활성 단계에서 각각 1/2/2종을 먼저 선택한 뒤 나머지를 비복원 추첨한다.
- 일일 지침 수는 표시 1~2일차 0개, 3~12일차 1개, 13~20일차 2개. 운영 규칙 CSV는 기존 2행을 사용한다.
- 감독관 날짜 이벤트 15001~15005는 1·3·7·13·19일차로 조정. 시설 조건 이벤트 15006/15007은 12008/12010 보유 조건 유지. 영업 전 디버그 이동 버튼도 7·13·19일차로 조정했고 `DebugJumpToDay`는 1~20만 허용한다.
- 운영 `PriceEventData.csv`의 9001~9004와 `PriceEventScheduleData.csv`의 10001~10002 행을 제거해 양쪽 CSV를 헤더만 남겼다. `DataTableManager`는 행이 있으면 기존 첫 idx, 행이 없는 두 이벤트 테이블만 정확한 헤더로 분류한다. 두 DataTable의 0행 허용과 함께 가져와야 한다. 스케줄러·가격 계산 API는 남아 있으나 운영 가격은 기본 판매가와 같다.
- 뉴스 Text 8042~8049는 제거. 새 의미로 재사용하지 않는다.

### 상품 가격·원가

| 상품 ID | 상품 | 판매가 G | 원가 G | 해금 설비 |
| --- | --- | --- | --- | --- |
| 1001 | 물 | 1,000 | 500 | - |
| 1004 | 통조림 | 2,500 | 1,250 | - |
| 1005 | 군용식량 | 5,000 | 2,500 | 12001 |
| 1006 | 영양바 | 6,000 | 3,000 | 12001 |
| 1007 | 붕대 | 3,000 | 1,500 | - |
| 1010 | 건전지 | 2,000 | 1,000 | - |
| 1013 | 약통 | 8,000 | 4,000 | 12002 |
| 1014 | 응급 주사 | 10,000 | 5,000 | 12002 |
| 1015 | 손전등 | 15,000 | 7,500 | 12003 |
| 1016 | 접이식 삽 | 20,000 | 10,000 | 12003 |
| 1018 | 무전기 | 30,000 | 15,000 | 12004 |
| 1019 | 배터리 | 25,000 | 12,500 | 12004 |
| 1020 | 방독면 | 50,000 | 25,000 | 12005 |
| 1021 | 방호복 | 60,000 | 30,000 | 12005 |
| 1022 | 방사능 측정기 | 80,000 | 40,000 | 12006 |
| 1023 | 열화상 카메라 | 100,000 | 50,000 | 12006 |

상품 16종 모두 `is_available=1`, `available_day=0`이다. 실제 등장에는 설비 활성 조건과 당일 추첨이 적용된다. 상품 PK·이미지·설비 FK는 재배정하지 않았다.

### 설비 가격

| 설비 ID | 설비 | 가격 G | 요구 단계 | 목표 단계 |
| --- | --- | --- | --- | --- |
| 12001 | 식량 보관 선반 | 80,000 | 1 | 0 |
| 12002 | 약품 보관장 | 100,000 | 1 | 0 |
| 12003 | 공구대 | 350,000 | 2 | 0 |
| 12004 | 전력·통신 장비 | 500,000 | 2 | 0 |
| 12005 | 핵보호 물품 설비 | 750,000 | 3 | 0 |
| 12006 | 정밀 전자장비 보관장 | 1,000,000 | 3 | 0 |
| 12007 | 막대 | 100,000 | 1 | 0 |
| 12008 | 2단계 확장 | 0 | 1 | 2 |
| 12009 | 소팅 | 300,000 | 2 | 0 |
| 12010 | 3단계 확장 | 0 | 2 | 3 |
| 12011 | 청소기 | 600,000 | 3 | 0 |
| 12012 | 시민권 · 나와 딸 | 3,000,000 | 3 | 0 |

실제 시민권 가격은 **3,000,000G**다. 과거 실행 계획의 제안 5,000,000G를 현재 CSV에 적용하지 않는다. 12008/12010 가게 확장은 0G이며 목표 단계 2/3이다.

### 유지비

| 표시 일차 | 유지비 G |
| --- | --- |
| 1 | 5,000 |
| 2 | 5,000 |
| 3 | 10,000 |
| 4 | 10,000 |
| 5 | 15,000 |
| 6 | 15,000 |
| 7 | 25,000 |
| 8 | 25,000 |
| 9 | 35,000 |
| 10 | 35,000 |
| 11 | 50,000 |
| 12 | 50,000 |
| 13 | 75,000 |
| 14 | 75,000 |
| 15 | 100,000 |
| 16 | 100,000 |
| 17 | 150,000 |
| 18 | 150,000 |
| 19 | 200,000 |
| 20 | 250,000 |

총 **1,380,000G**. 1~20일차만 남기고 21~31일차 행은 제거했다. 기존 ID 3001~3020을 유지하며 제거한 ID를 재사용하지 않는다.

### 손님 구매량

| 대상 | CSV 행 | 이전 → 현재 |
|---|---|---|
| 부자 | 6007, 6010, 6011 | 구매 종류 1~3 → 3~3, 종류별 수량 1~3 → 2~3 |
| 가난 | 6012~6015 | 구매 종류 1~3 → 1~1, 종류별 수량 1~3 → 2~3 |
| 모든 성향의 표시 1~6일차 | `CustomerCompositionSelector` | 종류별 최대 수량은 CSV와 2 중 작은 값; 7일차부터 원래 CSV 상한 |

초반 제한은 방문 생성 시의 경과일로 판정한다. 유효 최소 수량도 상한 이내로 맞추며 현재 CSV의 최소 수량은 2 이하다. 종류 수·선호 상품·가격 판정에는 초반 제한을 적용하지 않는다.

## 3. 전량 제외 거절 사유와 대사

```text
SaleSortingPanel.AllItemsDiscarded
→ GameUIController가 가격 0, 빈 SaleItem 목록 제출
→ DayProgress / CustomerVisit.SubmitOffer
→ PaymentRefused + RejectionReason.NoSaleItems
→ FeedbackTextIdx가 생성 시 선택한 NoSaleItemsTextIdx 반환
→ ProgressViewDataFactory가 TextData 조회
→ 결과 확인·퇴장·다음 손님
```

전량 제외 UI 이벤트는 기존 경로를 재사용한다. 이번 미커밋 변경의 핵심은 거절 사유·대사 판정이다.

| 상황 | Outcome | RejectionReason | 대사 |
|---|---|---|---|
| 미제출 또는 판매 성사 | None 또는 기존 판매 결과 | None=0 | 입장 또는 기존 판매 대사 |
| 상품이 있는 가격 거절 | PaymentRefused | PriceRejected=1 | 기존 RejectTextIdx |
| null/빈 최종 판매 목록 | PaymentRefused | NoSaleItems=2 | 신규 NoSaleItemsTextIdx |

- `CustomerRejectionReason`은 `CustomerVisit.cs`, 확정 사유의 저장 권위는 불변 `TransactionResult.RejectionReason`이다. 방문 속성은 결과를 읽으므로 퇴장 후에도 보존된다.
- `CustomerComposition` 주 생성자·호환 overload 및 `CustomerVisit` 생성자에 `rejectTextIdx` 다음 전량 제외 Text ID를 추가했다. 외부 소비자/fixture의 호출 인자도 함께 이전해야 한다. `TransactionResult` 상세 생성자는 결과·거절 사유의 일관성을 검사한다.
- 전량 제외 거래의 매출·원가 0, 거절 횟수 1회, 기존 명성 큰 폭리 등급, 도덕성 미평가, 거절 이모지는 그대로다. 정산과 명성에서 독립 결과를 새로 만들지는 않았다.
- 대사 후보는 손님 생성 시 한 번 선택해 고정한다.

### 성향 CSV 7열

기존 열 끝에 다음 순서로 추가했다. 전부 `UIntArrayConverter`가 읽는 `_` 구분 `IReadOnlyList<uint>`이며 `TextData.idx` FK다.

| CSV 열 | C# 속성 | 필수값 |
| --- | --- | --- |
| no_sale_items_text_idxs | NoSaleItemsTextIdxs | 전 성향 |
| male_no_sale_items_text_idxs | MaleNoSaleItemsTextIdxs | 전 성향 |
| female_no_sale_items_text_idxs | FemaleNoSaleItemsTextIdxs | 전 성향 |
| male_child_no_sale_items_text_idxs | MaleChildNoSaleItemsTextIdxs | 일반 성향 |
| female_child_no_sale_items_text_idxs | FemaleChildNoSaleItemsTextIdxs | 일반 성향 |
| male_elderly_no_sale_items_text_idxs | MaleElderlyNoSaleItemsTextIdxs | 일반 성향 |
| female_elderly_no_sale_items_text_idxs | FemaleElderlyNoSaleItemsTextIdxs | 일반 성향 |

다른 성향은 연령 열을 비워 같은 성별 기본 후보를 쓴다. 선택 우선순위는 연령·성별 → 같은 성별 기본 → 공용이다. 공용 후보에는 해당 성향 성인 남녀 4개를 넣었다. 누락 열·필수 빈값·0·배열 중복·없는 Text FK는 로더/카탈로그에서 거부한다. 성향 CSV·DTO·FK 검사·selector·composition·generator·visit·transaction·두 CSV·테스트를 함께 병합한다.

## 4. TextData ID 및 문구

분기점 대비 뉴스 8행 삭제, 기존 5개 문구 수정, 신규 대사 **8513~8540 총 28개** 추가. 전량 제외 작업 전의 기존 Text 499개는 보존했다. 아래는 신규 문구 전체다.

| Text ID | 문구 |
| --- | --- |
| 8513 | 이런. |
| 8514 | 아, 그렇군요. |
| 8515 | 아쉽네요. |
| 8516 | 네, 알겠어요. |
| 8517 | 어? 이건 안 돼요? |
| 8518 | 에이… |
| 8519 | 다음엔 살 수 있죠? |
| 8520 | 엄마한테 뭐라고 하지… |
| 8521 | 허허, 헛걸음했구먼. |
| 8522 | 그러면 할 수 없지. |
| 8523 | 아이고, 어쩌나. |
| 8524 | 알았어요. 수고해요. |
| 8525 | 잠깐만요. 꼭 필요합니다. |
| 8526 | 이러면 곤란한데… |
| 8527 | 어쩌지… |
| 8528 | 정말 방법이 없나요? |
| 8529 | 사정은 알겠습니다. |
| 8530 | 그럼 거래는 여기까지죠. |
| 8531 | 미리 말씀해 주시지. |
| 8532 | 다른 데 알아볼게요. |
| 8533 | 다른 곳으로 가죠. |
| 8534 | 허, 별일이군. |
| 8535 | 어머, 그래요? |
| 8536 | 더 볼 일은 없겠네요. |
| 8537 | 저… 이것도 안 됩니까? |
| 8538 | 하아… 알겠습니다. |
| 8539 | 네… 어쩔 수 없죠. |
| 8540 | 부탁드려도 안 될까요? |

| 기존 ID | 변경 |
|---|---|
| 8391, 8392, 8393 | 감독관 제목 10/20/30일차 → 7/13/19일차 |
| 8405 | 감독관 대사의 열흘째 → 일곱째 날 |
| 8410 | 폐쇄 시점을 이달 말 → 20일로 명시 |
| 8042~8049 | 가격 이벤트 뉴스 Text 제거 |

신규 Text는 기존 Text8 종류 ID 안에 있다. 현재 CSV·문서·로컬 Git 이력에서 ID 충돌이 없었으며 실제 원격 ref를 최신화한 뒤 다시 검사한다.

## 5. UI·자산·도구 파일

- `FacilityShopPanel.prefab`: 가격/설명 글씨 위치·크기 조정, 한 텍스트의 자동 크기 활성화와 최소 크기 18→10. 기존 계층·fileID·Inspector 연결을 유지하며 화면 가독성은 수동 확인한다.
- `PreOpenPanel.prefab`: 디버그 버튼 문구를 7·13·19일차로 변경. `GameUIController` 동작과 일치시킨다. `PreOpenPanelPresenter`의 `DebugDay10Button/20Button/30Button` 속성·직렬화 이름은 호환 때문에 남아 있으므로 이름만 보고 옛 날짜로 복원하지 않는다.
- `EndingAssetSetup`의 생성용 부제를 '시민권을 향한 20일'로 변경했다.
- `PixelCaps! SDF.asset`와 `LiberationSans SDF - Fallback.asset`는 중간 커밋에 변경이 있었으나 분기점 대비 최종 순변경은 없다. 커밋별 이관 시 중간 캐시 diff를 최종 변경으로 오인하지 않는다.
- `.idea/.idea.Cashier/.idea/workspace.xml`은 커밋에 포함된 개인 IDE 상태 1건이다. 기능 의존이 없으므로 통합 담당자가 제외 여부를 검토한다.
- Package, ProjectSettings, Addressables 설정, `.meta`, 공유 Scene은 분기점 대비 최종 변경이 없다.

## 6. 병합 단위와 충돌 주의

1. 20일 진행: GameSessionManager·GameProgress·DayProgress·엔딩 결과, 감독관/유지비 CSV·Text, 영업 전 UI·Prefab. 0기준 경과일과 표시 일차를 맞춘다.
2. 상품/지침: DailyProductSelector·DailyGuidelineGenerator, 상품/설비 CSV와 테스트. 최고 단계 보장 상품을 나머지 후보에서 빼 중복을 막는 로직을 유지한다.
3. 이벤트 0행: 이벤트/스케줄 CSV, 두 DataTable, DataTableManager, 뉴스 Text 제거, 이벤트 테스트. header-only 라우팅을 함께 가져온다.
4. 구매량: 성향 CSV의 부자·가난 값과 초반 상한, selector의 신규 대사 추첨을 한 파일에서 합친다.
5. 전량 제외: CustomerRejectionReason·TransactionResult·방문·구성/생성기·성향 7열·Text8513~8540·테스트를 묶는다. 성향 CSV 충돌 시 구매량 값과 신규 열을 모두 보존한다.
6. 화면: 설비창/영업 전 Prefab, 디버그 버튼 코드, 기능 명세·검증 기록과 이 인계서를 반영한다.

특히 `TextData.csv`, `CustomerDispositionData.csv`, `CustomerCompositionSelector.cs`, `GameSessionManager`, `DataTableManager`, `GameSessionApiTests.cs`는 다른 브랜치와 의미 충돌을 확인한다. 일괄 ours/theirs 선택으로 해결하지 않는다. 공용 manager·API의 기본 브랜치 반영 전 교차 검토는 [공통 규칙](../WORK_RULES.md)을 따른다. 이 문서가 승인·리뷰 완료를 대신하지 않는다.

## 7. 검증과 남은 일

| 검증 | 기록 |
|---|---|
| 현재 작업 트리 Unity 컴파일 | 오류 0 |
| 관련 EditMode 8개 suite | 227/230 통과, 실패 3, skip 0 |
| 관련 PlayMode | 5/5 통과, 실패·skip 0 |
| CSV 정적 비교 | 전량 제외 전 Text 499개·성향 15행 기존 셀 보존; 신규 28 ID 고유·FK 유효 |
| 실제 말풍선 문구·설비창 가독성 | 사용자 수동 확인 대기 |
| 전체 20일 경제 플레이 및 Player build | 이번 검증에서 미실행 |

EditMode 실패 3건은 `CustomerCsvTests.ProductCsvUsesRebalancedPricesAndCosts`(예상 16, 실제 0), `DailyProductSelectorUsesRebalancedKindsAndStageGuarantee`(첫날 예상 4, 실제 0), `FacilityTests.CsvExposesUpgradeKindsAndStageContracts`(기대 500000, 실제 280000)다. 앞의 둘은 FK 검증·공개 전 상품 Rows를 읽는 테스트 계약을 점검하고, 마지막은 현재 CSV 값과 낡은 기대값을 대조한다. 해당 로더·상품/설비 CSV·실패 assertion은 전량 제외 작업에서 변경하지 않았다. 실패를 숨기거나 수치를 임의로 되돌리지 않는다. 관련 회귀 suite 상태는 FAIL, 브랜치 인계 종합 상태는 PARTIAL이다.

직접 검사 XML/로그는 이 PC의 Git 제외 경로 `Logs/TestResults/no-sale-items-20260922-edit-final/`과 `Logs/TestResults/no-sale-items-20260922-play/`에 있다. 다른 담당자는 결과 요약과 해당 checkout에서 다시 실행한다. [검증 기록](../TESTING.md)의 과거 dotnet build 성공·PriceEventTests 8/8·가격표 진행 1/1은 이전 시점의 증거이며 이번 전체 테스트 결과로 재해석하지 않는다. 예전 문서의 InspectorEventTests Text 폭 실패는 이번 230개 suite에서 실행하지 않았다.

병합 후에는 정확한 결과 checkout에서 Unity import/컴파일, CSV PK/FK·ID 충돌, 이벤트 0행 로딩, 관련 테스트를 확인한다. 이어 표시 6→7일차 수량 해제, 상품 6→7·12→13일차, 지침 2→3·12→13일차, 19일차 감독관, 20일차 최종 정산과 시민권 3,000,000G 구매/엔딩을 점검한다. 전량 제외·일부 제외 후 판매·가격 거절 각각의 대사와 1회 집계, Init→Hub→Main 흐름, 설비창 가격 가독성도 실제 화면에서 확인한다.

## 8. 관련 문서

- [20일 적용 결과](20-day-rebalancing-result.md): 기존 리밸런싱 실제 값·당시 검증. 이 인계서는 이후 구매량·초반 제한·전량 제외까지 포함한다.
- [20일 실행 계획](20-day-rebalancing-execution-plan.md): 설계 근거. 제안 가격이 최종 CSV와 다를 수 있다.
- [손님 계약](../CUSTOMER_INTEGRATION.md), [가격 이벤트](../PRICE_EVENT_INTEGRATION.md), [엔딩](../CITIZENSHIP_ENDING.md), [테스트 기록](../TESTING.md): 영구 API·데이터 계약과 증거.
- [포트폴리오 브리프](portfolio-customer-csv-brief.md): 커밋에 들어간 설명용 자료. 런타임 의존은 없다.

## 9. 분기점 대비 파일 목록

이 인계서 작성 직전 `git diff 63f42749 --name-only`와 `git diff HEAD --name-only`를 대조했다. `HEAD+미커밋`은 해당 파일에 아직 커밋되지 않은 변경이 있다는 뜻이다.

| 파일 | 상태 |
| --- | --- |
| `.idea/.idea.Cashier/.idea/workspace.xml` | 커밋됨 |
| `Assets/Datas/Customer/CustomerDispositionData.csv` | HEAD+미커밋 |
| `Assets/Datas/Customer/ProductData.csv` | 커밋됨 |
| `Assets/Datas/FacilityData.csv` | 커밋됨 |
| `Assets/Datas/InspectorEventData.csv` | 커밋됨 |
| `Assets/Datas/MaintenanceBalanceData.csv` | 커밋됨 |
| `Assets/Datas/PriceEventData.csv` | 커밋됨 |
| `Assets/Datas/PriceEventScheduleData.csv` | 커밋됨 |
| `Assets/Datas/TextData.csv` | HEAD+미커밋 |
| `Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab` | 커밋됨 |
| `Assets/Prefabs/GameUI/PreOpenPanel.prefab` | 커밋됨 |
| `Assets/Scripts/Commons/Data/PriceEventData.cs` | 커밋됨 |
| `Assets/Scripts/Commons/Data/PriceEventDataTable.cs` | 커밋됨 |
| `Assets/Scripts/Commons/Data/PriceEventScheduleDataTable.cs` | 커밋됨 |
| `Assets/Scripts/Commons/GameEndingResult.cs` | 커밋됨 |
| `Assets/Scripts/Commons/SaleRestriction.cs` | 커밋됨 |
| `Assets/Scripts/Customer/CustomerComposition.cs` | HEAD+미커밋 |
| `Assets/Scripts/Customer/CustomerCompositionSelector.cs` | HEAD+미커밋 |
| `Assets/Scripts/Customer/CustomerGenerator.cs` | HEAD+미커밋 |
| `Assets/Scripts/Customer/CustomerProductAvailability.cs` | 커밋됨 |
| `Assets/Scripts/Customer/CustomerVisit.cs` | HEAD+미커밋 |
| `Assets/Scripts/Customer/Data/CustomerCatalog.cs` | HEAD+미커밋 |
| `Assets/Scripts/Customer/Data/CustomerDispositionData.cs` | HEAD+미커밋 |
| `Assets/Scripts/Finance/TransactionResult.cs` | HEAD+미커밋 |
| `Assets/Scripts/Manager/DataTableManager.cs` | 커밋됨 |
| `Assets/Scripts/Manager/GameSessionManager.cs` | 커밋됨 |
| `Assets/Scripts/Progress/DayProgress.cs` | 커밋됨 |
| `Assets/Scripts/Progress/GameProgress.cs` | 커밋됨 |
| `Assets/Scripts/Scene/Editor/EndingAssetSetup.cs` | 커밋됨 |
| `Assets/Scripts/Scene/GameUIController.cs` | 커밋됨 |
| `Assets/Scripts/UI/Presenters/PreOpenPanelPresenter.cs` | 커밋됨 |
| `Assets/Scripts/UI/PriceItemSlot.cs` | 커밋됨 |
| `Assets/Scripts/UI/PriceListPanel.cs` | 커밋됨 |
| `Assets/Tests/EditMode/CustomerCompositionSelectorIntegrationTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/CustomerCompositionTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/CustomerContractTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/CustomerCsvTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/DailyGuidelineTests.cs` | 커밋됨 |
| `Assets/Tests/EditMode/FacilityTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/InspectorEventTests.cs` | 커밋됨 |
| `Assets/Tests/EditMode/MoralityTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/PriceEventTests.cs` | 커밋됨 |
| `Assets/Tests/EditMode/ReputationDispositionRulesTests.cs` | HEAD+미커밋 |
| `Assets/Tests/EditMode/ReputationSystemTests.cs` | HEAD+미커밋 |
| `Assets/Tests/PlayMode/GameSessionApiTests.cs` | HEAD+미커밋 |
| `doc/CITIZENSHIP_ENDING.md` | 커밋됨 |
| `doc/CUSTOMER_INTEGRATION.md` | HEAD+미커밋 |
| `doc/DATA_CATALOG.md` | HEAD+미커밋 |
| `doc/FACILITY_INTEGRATION.md` | 커밋됨 |
| `doc/INSPECTOR_SYSTEM_DRAFT.md` | 커밋됨 |
| `doc/MAINSCENE_INTEGRATION.md` | 커밋됨 |
| `doc/PRICE_EVENT_INTEGRATION.md` | 커밋됨 |
| `doc/TESTING.md` | HEAD+미커밋 |
| `doc/work/20-day-rebalancing-execution-plan.md` | 커밋됨 |
| `doc/work/20-day-rebalancing-result.md` | 커밋됨 |
| `doc/work/portfolio-customer-csv-brief.md` | 커밋됨 |

병합 전달 전 현재 미커밋 파일과 이 인계서를 포함한 실제 commit SHA를 정하고, 최신 원격 ref 기준으로 CSV ID·GUID·Addressables·공용 계약 충돌을 다시 확인한다. 기존 실패 3건과 UI·20일 경제 검증 결과를 갱신한 뒤 필요한 교차 리뷰를 기록한다. 이 문서 작성 시 stage·commit·push·merge는 수행하지 않았다.
