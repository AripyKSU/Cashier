# 2026-09-14 프로토타입 변경·추가사항

## 범위와 실행 환경

- 대상: `Assets/DystopiaPrototype/`, 브랜치 `astra-prototype`.
- Unity 6000.3.18f1 / URP 2D. 기존 Packages·ProjectSettings 변경 없음.
- 이번 문서는 이전 커밋 이후 누적된 이번 작업분을 설명한다. 에셋 추가, 화면 표현 변경, 상품 카탈로그 변경을 함께 포함한다.
- 일반 실행 씬: `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity`.
- 작업 중 열린 씬은 자동 저장·재로드하지 않았다. **일반 씬 파일 자체는 이번 변경에 포함되지 않는다.** 아래 메뉴로 설정을 적용해야 한다.

## 추가: 생존 아이템 16종

PNG와 Unity 메타데이터 위치: `Assets/DystopiaPrototype/Art/Products/`.

| 상품 | 파일명 | 기본 가격 | 필요 설비 | 최소 단계 |
|---|---|---:|---|---:|
| 생수 | DrinkingWater.png | 1,000 | 없음 | 0 |
| 통조림 | CannedFood.png | 2,500 | 없음 | 0 |
| 붕대 | MedicalBandage.png | 3,000 | 없음 | 0 |
| 건전지 | DryBattery.png | 3,500 | 없음 | 0 |
| 군용식량 | MilitaryRation.png | 미설정 | 식량 보관 선반 | 1 |
| 영양바 | NutritionBar.png | 미설정 | 식량 보관 선반 | 1 |
| 약통 | Medicine.png | 미설정 | 약품 보관장 | 1 |
| 응급 주사 | EmergencyInjection.png | 미설정 | 약품 보관장 | 1 |
| 손전등 | Flashlight.png | 미설정 | 공구대 | 2 |
| 접이식 삽 | FoldingShovel.png | 미설정 | 공구대 | 2 |
| 무전기 | Radio.png | 미설정 | 전력·통신 장비 | 2 |
| 배터리 | PowerBattery.png | 미설정 | 전력·통신 장비 | 2 |
| 방독면 | GasMask.png | 미설정 | 핵보호 물품 설비 | 3 |
| 방호복 | ProtectiveSuit.png | 미설정 | 핵보호 물품 설비 | 3 |
| 방사능 측정기 | RadiationDetector.png | 미설정 | 정밀 전자장비 보관장 | 3 |
| 열화상 카메라 | ThermalCamera.png | 미설정 | 정밀 전자장비 보관장 | 3 |

`DystopiaSession.cs`에 고정 `DystopiaProductId`, 설비 비트 플래그 `DystopiaFacility`, `shopStage`, `ownedFacilities`를 추가했다. 상품 배열 인덱스 대신 ID로 일일 규칙을 판정한다. 단계와 설비 보유 조건을 모두 만족하며 가격이 양수이고 Sprite가 있는 상품만 영업일의 판매 목록에 포함한다. 가격 0은 무료 판매가 아니라 **미설정/주문 제외**다. 중복 ID·누락 ID·판매 가능 상품 없음은 오류로 알린다. 기존 건빵/즉석밥 규칙은 영양바/군용식량 ID에 대응한다.

### 기존 씬에서 등록

1. Play를 종료한 상태에서 작업 씬을 연다.
2. `Dystopia > Register 16 Survival Products`를 실행한다. 기존 상품의 가격 등 보존 가능한 값은 연결하면서 카탈로그를 등록한다. 기존 10종 배열을 그대로 실행하면 ID 검증에 실패할 수 있다.
3. 필요한 신규 상품 가격과 `ownedFacilities`를 설정한다. Stage 적용 메뉴만 누른다고 설비를 구입한 것으로 처리되지는 않는다.
4. `Validate Survival Product Colliders`, `Validate Survival Product Unlocks`는 확인용 Editor 메뉴다.

## 추가·정리: Stage 1 / 2 / 3 외형과 적용 기준

현재 권위 기준은 `Assets/DystopiaPrototype/Editor/References/Stage1Reference.unity`, `Stage2Reference.unity`, `Stage3Reference.unity`다. **직접 플레이할 씬이 아니라 적용 메뉴가 읽는 기준 사본**이다. 각 `.meta`를 함께 보존해야 한다.

| 단계 | 외형 | 상자·시계 | 배경 |
|---|---|---|---|
| 1 | 낡은 목재 가판·천막 | 작은 목재 상자, 낮은 오른쪽 시계 | 원래 100% 크기 |
| 2 | 철제 프레임·상판 | Stage2Container, Stage2Clock | 92% 축소 배치 |
| 3 | 보강 철제 프레임·전등·하단 설비 | 새 Stage3Container, 기존 시계 외형 | 철제 가판에 맞춘 92% 배치 |

`Dystopia > Apply Stage 1 Shop`, `Apply Stage 2 Shop`, `Apply Stage 3 Shop`은 `DystopiaTools.ApplyApprovedStageReference`로 연결했다. 이전 메뉴의 고정값 때문에 다른 단계의 크기·숫자 위치가 남던 문제를 수정했다. 프레임, 상자, 시계 몸체/숫자, 배경, 전등 관련 설정을 참조에서 읽고 현재 대상에 적용한다. 단계 변경은 **이 메뉴를 명시적으로 실행할 때만** 일어난다. Play/OnEnable에서 배치를 자동 초기화하지 않는다.

- Stage 3에 남아 하단을 덮던 `Stage2Cabinet` 표시를 끈다. 별도 하부장 표시 상태도 단계 적용 시 확인한다.
- Stage 3 배경은 Stage 2 기준의 6개 배경 Transform을 사용한다. 따라서 현재 구현에서 Stage 2 배경 기준을 수정하면 Stage 3에도 영향이 있다.
- 적용 전후 사본은 로컬 `output/shop-stage-switch/`에 생성한다. 이 임시 백업 폴더는 커밋 대상이 아니다.
- `Save Current Stage 3 Reference`는 현재 씬 사본과 메뉴용 기준 파일을 함께 갱신한다.
- **현재 Save Current Stage 1/2 Reference 메뉴는 output 사본만 저장한다.** 새 배치를 확정할 때는 Editor/References의 해당 기준 파일까지 별도 갱신해야 한다. 기록만 남기고 메뉴 연결을 빠뜨리지 않도록 주의한다.
- 과거 실험용 `Tune`, `Fit`, `Apply ... Parts`, `Restore ...` 메뉴도 남아 있다. 일반 단계 전환에는 위 세 Apply 메뉴만 사용한다. 실험 메뉴는 배치·임포트 설정을 바꿀 수 있다.

### 최신 Stage 3 상자·시계

- 원본: 사용자가 제공한 `stage3-steel-container.png`. 프로젝트 파일은 `Art/Stage3Container.png`.
- 상자: `FrontContainer`, 위치 `(522,-436)`, Rect 크기 `(360,240)`, Scale `(.66766,.5954192,.66766)`.
- 시계: `CounterClock`, 위치 `(1009,-578)`, Rect 크기 `(170.5,120)`, Scale `(1.34844,1.34844,1.1237)`.
- 두 Image tint는 `(.62,.66,.70,1)`. 동일 배율이어도 원본 색이 달라 보이는 차이는 남을 수 있다.
- 상자는 Point 필터, 512 임포트, Sprite 영역 `(179,121,1178,777)`. 원본 PNG는 변형하지 않고 Sprite 영역으로 여백을 제외한다.
- 이전 상자용 노멀 연결 제거, rim/specular 0, bottomShade .35. 원본의 밝은 부분만 셰이더에서 억제한다.
- 밑면에 짧고 진한 검정 하드 엣지 접촉 그림자를 추가했다. UV를 큰 블록으로 묶던 실험은 흰 픽셀 문제로 제거했다. 흐린 스팟 그림자를 추가한 상태가 아니다.
- `SetStage3Container`가 재적용 때 이 외형 설정을 적용한다. 배치는 기준 씬이 소유한다.

## 변경: 배경과 경비병

- `FARBACKGROUND`, `Dawn`, `Evening`, `SunsetClouded`: 남산타워를 왼쪽 가시 영역으로 옮기고 확대한 버전. 시간대별 배경도 변경했다.
- `BoothBarricade`: 벽 틈이 보이던 영역 보완.
- `MidBackground`: 경비병을 별도 Sprite로 표현할 수 있도록 배경 수정.
- `RearWatchGuard0/1`, `ExtractedWatchTowers` 추가. 별도 경비병 연결과 앞 경비탑 표시 조정용 메뉴 제공.
- `Animate Extracted Rear Guards`, `Inspect Rear Guard Links`: 연결/점검용. 이번 인계에서는 실시간 움직임을 새로 재생 검증하지 않았다.
- `Align Smoke To Inset Background`: 왼쪽 연기 하단을 타워 옆 건물 좌표에 맞추는 수동 메뉴. 전체 시간대 연기 위치는 재확인 대상이다.
- 시간대 아트 일부는 이미지 생성/수정을 거쳤다. 원본과 픽셀 단위 동일성을 보장하지 않는다.

## 변경: UI와 손님 연출

- `DailyInstruction.png`, `InstructionStartStamp.png`를 제공 이미지로 교체.
- 날짜 칸, 제목·지침·가격표·안내문 위치 조정. 영업시작은 이미지에 포함된 글자를 사용해 중복 텍스트를 제거하고 하단 중앙 크기 조정.
- 가계부 벽의 딸 그림(`ledgerDrawing`)을 코드에서 끄던 처리를 제거.
- 대기 손님 전진에 개별 위상 좌우 흔들림·상하 움직임·작은 비율 변화를 추가. 도착 시 기존 배치로 합류.
- 거래 후 손님은 오른쪽으로 이동하며 작아지고 검게 변하면서 페이드아웃. 다음 손님 갱신 전에 상태를 복원해 사라진 손님이 다시 보이는 프레임 방지.

## 변경: 탑다운 아이템 조작

- 상품 ID와 직접 Sprite 연결 사용. 오래된 배열 순서 기반 프리팹을 신규 카탈로그에 잘못 대응하지 않도록 분리.
- 신규 생성 아이템 긴 변 크기 180 → 126 기준으로 축소. Sprite 기반 PolygonCollider2D 사용.
- 이동·회전 감쇠 강화, 회전 속도 제한 축소. 아이템 충돌 시 별도 회전/힘을 더하던 처리를 제거하고 물리 계산에 맡김.
- 잡은 손 이미지는 이동 방향이나 물체 회전 때문에 옆으로 돌아가지 않도록 원본 방향 유지.

## 주요 코드 위치

| 파일 | 책임 |
|---|---|
| Editor/DystopiaTools.cs | 단계 적용·저장, 상품 등록·검증, 에셋 슬라이싱/연결 |
| Scripts/DystopiaSession.cs | 상품 ID, 가격/설비/단계 조건, 주문 목록, 일일 규칙 |
| Scripts/DystopiaScreen.cs | 일일지침, 딸 그림, 손님 진입/퇴장 |
| Scripts/DystopiaPixelStage.cs | 렌더 레이어, 상자 전용 셰이더 분기 연결 |
| Art/TimeOfDay/PixelStageLighting.shader | 상자 하이라이트 억제, 단단한 접촉 그림자 |
| TopDownTest/Scripts/DystopiaTopDownTest.cs | 상품 Sprite/충돌체 생성, 조작 물리, 손 표시 |
| TopDownTest/Scripts/DystopiaTopDownItem.cs | 충돌 처리 |

## 검증과 남은 항목

- 빌드: `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity quiet -clp:ErrorsOnly` 수행. 실제 커밋 전 결과는 커밋 인계 메시지 참조.
- Stage 적용, 시계/상자 Sprite·색상·위치의 MCP 읽기 및 저장 사본 확인을 수행했다. 모든 단계의 왕복 전환을 한 번에 Play 검증한 것은 아니다.
- 이번 인계 전체 상태는 **PARTIAL**: 파일/메뉴 구현과 개별 적용 확인은 있으나 신규 체크아웃에서 아래 실행 검증이 필요하다.
  1. 상품 등록 후 4종 기본 상품으로 시작, 가격 0 상품 제외 확인.
  2. 단계와 설비 각각 부족한 경우 주문 제외, 모두 충족 시 다음 영업일 반영 확인.
  3. Stage 1 → 2 → 3 → 1 순환 시 상자·시계·숫자·하부장·배경 확인.
  4. 낮/저녁 배경의 경비병·연기 연결, 손님 전진·퇴장 반복, 일일지침 해상도별 표시 확인.
- 고양이 꼬리 애니메이션은 미구현. 배경에 합쳐진 원화라 분리 소재가 필요하다.
- 아빠가 단비를 업고 가판을 멘 로딩 애니메이션은 부위 원화 부족으로 미구현. 원본 GIF를 부위 애니메이션으로 완성했다는 뜻이 아니다.
- 원본 자산은 사용자 제공 자료 및 이 작업의 생성/수정 이미지다. 별도 외부 배포 라이선스 증빙은 이번 작업에서 추가 검증하지 않았다.
- 포커스·마우스·키보드 제어 없이 파일과 Unity MCP로만 인계 작업 수행. 일반 씬/Play 상태 변경 없음.
