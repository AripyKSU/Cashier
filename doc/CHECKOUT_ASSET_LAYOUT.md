# Checkout 리소스 위치

## store-resource-exchange 통합 경로 보정 (2026-09-16)

`astra-prototype a43c1ae`의 아래 재편을 병합하되 이미 게임에 이관한 동일 GUID 자산은 기존 경로를 유지한다. 원본 `STAGE1_HANDOFF.md`의 아트 경로는 프로토타입 브랜치 기준이다.

- 손님 외형·노멀: `Assets/Textures/Customer/Dystopia/`와 `NormalMaps/`.
- 단계별 신규 상판·설비: `Assets/Textures/Environment/Dystopia/`; 작업대는 그 아래 `TopDown/`.
- 단계별 신규 상자·프레임·시계: `Assets/Textures/UI/Dystopia/`. 특히 Stage2RustedClock/Stage3GunmetalClock은 이 경로를 유지한다.
- 기존 Checkout 자산100장의 재편은 아래 art 경로를 사용한다. 기존 게임용 사본과 프로토타입 자산의 파일명이 같더라도 서로 다른 GUID를 합치지 않는다.

실제 참조는 GUID와 ResourceData/Prefab 연결을 기준으로 확인한다. 검증과 알려진 프로토타입 참조 누락은 [병합 기록](work/store-resource-exchange.md#astra-prototype-병합-2026-09-16)을 따른다.

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

## 2026-09-16 재편: `Assets/Textures/Checkout/` → `Assets/Textures/art/`

옛 `Checkout` 폴더의 PNG 100장을 `.meta`와 함께 종류별 폴더로 옮겼다(GUID 보존, 씬·프리팹 참조 변경 없음). 코드의 경로 문자열도 함께 교체했다. 위 표의 `Checkout/...` 경로는 아래로 읽는다. 상세는 `Assets/Textures/art/STAGE1_HANDOFF.md` 3장과 `output/art-move/moved.csv`.

| 옛 경로 | 새 경로 |
|---|---|
| `Checkout/Background/*` , `Checkout/Shop/BoothBarricade.png` | `art/Background/` |
| `Checkout/Effects/{Dawn,Evening,SunsetClouded,CityLights,Searchlight,CounterLight}` | `art/Effects/TimeOfDay/` |
| `Checkout/Effects/ChimneySmoke0~3` | `art/Effects/Smoke/` |
| `Checkout/Effects/Fog{Back,Mid,Front}` | `art/Effects/Fog/` |
| `Checkout/Characters/Crowd*` | `art/Characters/Crowd/` |
| `Checkout/Characters/{WatchGuard,RearWatchGuard0,1}` | `art/Characters/Guard/` |
| `Checkout/Characters/{Inspector,InspectorHead}` | `art/Characters/Inspector/` |
| `Checkout/Characters/MaleCustomer0` | `art/Customer/Legacy/` |
| `Checkout/Products/*` (+`Legacy/`) | `art/Products/` (+`Legacy/`) |
| `Checkout/Shop/{BoothCanopy,Stage2Shop,Stage2ShopNormal,Stage3Shop,Stage3ShopNormal}` | `art/Facility/Frame/` |
| `Checkout/Shop/Stage{1,2,3}*Clock*`, `Checkout/Workbench/CounterClock`, `art/Facility/Frame/Stage2RustedClock`, `Stage3GunmetalClock` | `art/Facility/Clock/` |
| `Checkout/Shop/{BoothCounter,Counter,Stage1WoodCounter,Stage3Counter,Stage3CounterNormal}` | `art/Facility/CounterTop/` |
| `Checkout/Shop/Stage{1,2,3}*Container*`, `Checkout/Workbench/FrontContainer{Male,Female,Normal}` | `art/Facility/Crate/` |
| `Checkout/Workbench/TopDownWorkbench(+Normal)` | `art/Facility/Workbench/` |
| `Checkout/Workbench/{Calculator,CalculatorToggle,DividerBar,Vacuum,TopDown*}` | `art/Workbench/` |
| `Checkout/UI/*` | `art/UI/` |
