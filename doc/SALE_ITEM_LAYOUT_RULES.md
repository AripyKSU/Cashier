# 판매 상품 배치 규칙

## 목적

판매 구역에 들어온 상품의 자동 소팅 위치와 상품 생성 계약을 정의한다.

## 상품 생성 계약

- 한 손님 방문에는 최대 3종류의 상품만 생성한다.
- 한 상품 종류의 최대 수량은 3개다.
- 자동 소팅은 3종류×3개, 총 9개 앵커를 전제로 한다.
- 이 제한을 변경하려면 손님 상품 생성 규칙, 판매 UI, 앵커 구성, 테스트와 이 문서를 함께 변경한다.
- 계약을 위반한 입력은 상품을 조용히 겹치게 하거나 삭제하지 않고 개발 오류로 감지한다.

## 앵커 구성

`GameUI.prefab`의 `ForSaleZone` 아래에 다음 9개 `RectTransform`을 둔다.

```text
위쪽:   SaleAnchor_Row1_Slot1, SaleAnchor_Row1_Slot2, SaleAnchor_Row1_Slot3
가운데: SaleAnchor_Row2_Slot1, SaleAnchor_Row2_Slot2, SaleAnchor_Row2_Slot3
아래쪽: SaleAnchor_Row3_Slot1, SaleAnchor_Row3_Slot2, SaleAnchor_Row3_Slot3
```

`SaleSortingPanel.saleAnchors` 배열은 위쪽 행부터, 각 행은 왼쪽부터 참조한다. 앵커의 `RectTransform.anchoredPosition`은 프리팹 Inspector에서 조정할 수 있으며, 소팅 시 해당 위치를 읽어 사용한다. 기본 위치는 판매 구역 중앙 기준으로 다음과 같다.

| 행 | 위치 Y | 슬롯 X |
|---:|---:|---:|
| 1 | 60 | -78, 0, 78 |
| 2 | 0 | -78, 0, 78 |
| 3 | -60 | -78, 0, 78 |

## 소팅 순서

- 판매 구역에 있고 `State == ForSale`이며 `ManipulationState == Idle`인 상품만 소팅한다.
- 상품 종류의 행은 `ProductId` 오름차순이다.
- 같은 상품의 좌우 순서는 `UnitIndex` 오름차순이다.
- 플레이어 드래그, 막대 이동, 청소기 부착 중인 상품은 자동 소팅하지 않는다.
- 논리적인 판매 목록은 위치가 아니라 상품의 `ForSale` 상태로 집계한다.
- 상품 간 물리 충돌은 사용하지 않는다.

## 계산기 표시와 입력

- 계산기는 기본적으로 닫혀 있으며 별도 토글 버튼을 두지 않는다.
- 계산기 위치는 `OperatingPanel/PriceInput`에서 조정한다. 1280×720 기준 중앙 앵커 위치 `(30, -160)`, 크기 `360×362`로 가운데 물품 구역의 오른쪽 아래에 배치하며 오른쪽 분류 구역을 가리지 않는다. 프리팹 설정 도구도 같은 값을 사용한다.
- 실제 상품이 하나 이상 있고 모든 상품이 판매 또는 제외 상태로 분류된 경우에만 자동으로 연다.
- 상품을 다시 집거나 막대·청소기·자동 소팅으로 이동하는 동안에는 닫고, 모든 조작과 분류가 끝나면 다시 연다.
- 전부 제외한 경우도 분류 완료이므로 계산기는 열지만, 기존 거래 경계가 판매 수량 0인 제출을 거부한다.
- 새 손님, 쏟기, 결과 표시, 선택 잠금과 화면 정리 상태에서는 계산기와 키패드·Enter 입력을 닫는다.

### 계산기 변경 검증 (2026-09-14)

- `codex/customer-trade-presentation`, 기준 `4642f3b` 이후 미커밋 변경. `GameUI.prefab`의 토글 객체와 `SaleSortingPanel.ToggleCalculator`·토글 직렬화 필드를 제거했다. 기존 `IsCalculatorOpen`·표시 변경 이벤트와 결제 경로를 사용하며 MainScene 파일은 변경하지 않았다.
- Unity 컴파일 성공. EditMode **258/258**, PlayMode **54/54**, 실패·skip·미완료 0. 신규 `CalculatorFollowsSaleSortingLifecycle`은 실제 GameUI에서 초기/미분류 닫힘, 분류 완료 열림, 재분류 시 입력 금액 초기화, 전부 제외 시 제출 거부, 잠금/정리를 검증한다.
- 증거: `Temp/TestResults/20260914-145616-5a5602ae99d44868bb998d9297f6756a/EditMode.xml`, `Temp/TestResults/20260914-145636-cb8363dd6c29487cb6a52c5ec79e622e/PlayMode.xml` 및 각 `.log`.
- 초기 신규 테스트의 참조 누락 2건은 수정 후 위 검증을 실행했다. GameUI 프리팹의 missing script 0, 토글 객체 0을 확인했다. 최종 배치·조작감은 사용자 확인 대상이다.
