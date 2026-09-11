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
