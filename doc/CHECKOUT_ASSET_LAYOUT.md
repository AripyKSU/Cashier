# Checkout 리소스 위치

2026-09-15 정리. 기존 에셋의 GUID와 import 설정을 유지하여 이동했다.

| 용도 | 경로 |
|---|---|
| 캐릭터·경비 | `Assets/Textures/Checkout/Characters/` |
| 상품 | `Assets/Textures/Checkout/Products/` |
| 가게·단계별 상판과 소품 | `Assets/Textures/Checkout/Shop/` |
| 계산대·도구 | `Assets/Textures/Checkout/Workbench/` |
| 배경·효과 | `Assets/Textures/Checkout/Background/`, `Assets/Textures/Checkout/Effects/` |
| 문서·UI | `Assets/Textures/Checkout/UI/` |
| 재질 | `Assets/Materials/Checkout/` |
| 셰이더 | `Assets/Shaders/Checkout/` |
| 폰트 | `Assets/Fonts/Checkout/` |

## 단계별 시계

- Stage 1: `Assets/Textures/Checkout/Shop/Stage1BasicClock.png`
- Stage 2: `Assets/Textures/Checkout/Shop/Stage2Clock.png`
- Stage 3: `Assets/Textures/Checkout/Shop/Stage3Clock.png` (이전 `시계.png`)

옛 `Stage1Clock.png`, 한글 스탬프 5종 등 사용하지 않는 20개 파일은 제거했다.
`Legacy` 분류는 기존 코드나 프리팹에서 사용하는 호환 에셋이며, 오래된 이름만으로 삭제하지 않는다.

정확한 이동·삭제 목록은 `output/asset-cleanup/moved-and-deleted.csv`에 있다.
백업 경로는 `output/asset-cleanup/manifest.json`의 `backupRoot`에 기록되어 있으며,
에셋 원본·meta, 수정 전 코드 및 열린 씬의 사본을 포함한다.
이번 정리에서는 씬을 저장하거나 다시 열지 않았다.
