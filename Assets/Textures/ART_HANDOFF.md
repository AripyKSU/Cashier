# 이미지 개발자 인계 안내

## 전달 범위

이 대화에서 작업한 고객·단계별 상판·설비·상자·프레임·시계 최종 이미지는 모두 `Assets/Textures/` 아래에 있습니다. PNG와 같은 이름의 `.meta`를 함께 전달하세요. `.meta`에는 GUID, Sprite 자르기 영역, PPU와 pivot이 포함됩니다.

| 폴더 | 내용 | PNG 수 |
|---|---|---:|
| `Customer/Dystopia` | 고객 원화 | 60 |
| `Customer/Dystopia/NormalMaps` | 고객 노멀맵 | 60 |
| `Environment/Dystopia` | 단계별 상판·설비·Stage 2 프레임 | 16 |
| `Environment/Dystopia/TopDown` | 탑다운 작업대 | 3 |
| `UI/Dystopia` | 단계별 상자·시계 | 10 |

## 현재 Stage 2에서 사용하는 수정본

- 기둥·천장: `Environment/Dystopia/Stage2RustedFrame.png` (Sprite sheet) 앵글과 조각은 기존 Stage2Shop 기준.
- 시계: `UI/Dystopia/Stage2RustedClock.png` (숫자는 별도 UI).
- 닫힌 상자: `UI/Dystopia/Stage2RustedCrateClosed.png`.
- 열린 상자: `UI/Dystopia/Stage2RustedCrateOpen.png`.
- 상판: `Environment/Dystopia/Stage2CounterTop.png`.
- 기존 Stage2CrateClosed/Open은 보존본이며 이번 수정본은 Rusted 이름을 사용합니다.

## 개발 시 유의 사항

- `Stage3LeftPillar/Stage3RightPillar`는 여러 단계가 재사용하는 씬 오브젝트 이름입니다. 실제 단계는 연결된 Sprite와 저장된 단계 기준 씬으로 구분합니다.
- 2026-09-16 Stage 2 기준과 Stage 3 작업 씬을 저장했습니다. PNG 폴더만 전달하면 현재 씬의 Sprite 연결·위치·그림자 설정까지 전달되지는 않습니다.
- 양옆 상판 확장은 별도 PNG가 아니라 원본 UV를 사용하는 RawImage 두 개입니다.
- 설비 밑면 윤곽은 uGUI Shadow, 접촉 그림자는 `Assets/DystopiaPrototype/Scripts/DystopiaPixelStage.cs`와 `Assets/Shaders/Checkout/PixelStageLighting.shader`가 담당합니다.
- 고객 클래스·성별·Canvas 크기·어린이 배율 설명: `doc/CUSTOMER_ARTWORK_GUIDE.md`.
- Stage 1 기준은 보존했습니다. Stage 2/3의 최신 저장 정보는 STAGE_LAYOUT_HANDOFF.md를 확인하세요.
- 원본은 사용자가 제공한 프로젝트 이미지이며, 수정본 일부는 image_gen으로 제작했습니다. 별도 라이선스를 새로 부여하지 않습니다.
- output 및 생성 도구 폴더의 미리보기·폐기 시안·백업은 최종 런타임 이미지가 아닙니다.

## 검증

- 전체 목록: `ART_IMAGE_INVENTORY.md`.
- 이 목록의 PNG 149개와 `.meta` 짝은 이관 검증에서 확인합니다.
- 이번 폴더 정리에서는 이미지 내용, GUID, 씬과 배치를 변경하지 않았습니다.

## 2026-09-16 용도별 경로 이관

기존 `Assets/Textures/art/` 아래 149개 PNG와 `.meta`를 고객·환경·UI 용도별 폴더로 이동했다. 파일명과 GUID는 유지했다. 원본→목적지, SHA-256과 GUID의 권위 목록은 [`doc/data/ART_RESOURCE_MOVE_MAP.json`](../../doc/data/ART_RESOURCE_MOVE_MAP.json)이다. 아래 과거 작업 기록의 `art/...` 표기는 당시 경로다.

이번 완료 범위는 파일 이관과 경로 정리다. 자산·`.meta` 쌍 및 GUID 보존 검증과 Editor 도구 경로 갱신은 포함하지만, 게임 데이터·Addressables·Prefab·Scene·실제 화면 연결은 수행하지 않았으며 후속 작업에서 별도로 검증한다.

## 2026-09-16 녹 표현 수정

Stage2RustedFrame/Clock 및 CrateClosed/Open의 잔녹과 X 모양 녹을 줄이고 이음새·모서리에서 퍼지는 큰 녹으로 변경했습니다. 당시 파일 경로와 GUID를 유지했으며, 현재 경로는 위 용도별 이관 절을 따릅니다. 열린 상자는 생성 해상도가 1px 달라 Sprite 사각형 크기는 유지하고 시작 Y만 34→33px로 보정했습니다. image_gen 사용, 요청 프롬프트와 원본 백업은 `output/stage2-seam-rust/`에 있습니다.

