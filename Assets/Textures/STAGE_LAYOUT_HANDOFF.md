# Stage 2 / Stage 3 배치 인계 — 2026-09-16

## 승인 저장본
- Stage 2: `Assets/DystopiaPrototype/Editor/References/Stage2Reference.unity`.
- 승인 직후 백업: `output/stage2-reference/20260916-073633/Stage2.unity`.
- Stage 3: `Assets/DystopiaPrototype/Editor/References/Stage3Reference.unity`.
- 작업 씬: `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity`, Stage 3로 저장.
- 배치 권위는 위 기준 씬이다. Awake/Start/OnValidate에서 좌표나 크기를 재설정하지 않는다.

## 설비 해금 계약
Stage 1에서 Stage 2로 진입하면 FoodShelf와 MedicineCabinet은 이미 해금된 상태이다. ToolBench와 PowerCommunications는 각 설비 업그레이드 완료 시 표시한다. 표시할 때 저장된 오브젝트를 활성화하며 RectTransform을 다시 계산하지 않는다.

현재 승인 미리보기에는 네 설비가 모두 보인다. `DystopiaSettings.ownedFacilities` 저장값은 0이다. 기존 `DystopiaProduct.IsUnlocked`는 주문 상품을 제한하지만, 설비 구매와 화면 활성화 연동까지 구현된 상태는 아니다. 이번 저장 작업에서 구매 로직을 변경하지 않았다. 이 규칙은 후속 개발 시 지켜야 하는 계약이다.

## Stage 2 시설 Canvas 배치
부모: DystopiaCanvas. anchorMin/Max=(0,1), pivot=(0,1). 좌상단 기준, 음수 Y는 아래쪽. 아래 표는 읽기 편하게 소수 둘째 자리로 표시하며 정확한 값은 기준 씬을 사용한다.

| Object | anchoredPosition | sizeDelta | localScale |
|---|---|---|---|
| FacilityFoodShelf | (53,-548) | (204,175.61) | (1,1,1) |
| FacilityMedicineCabinet | (1029,-559) | (221,157.79) | (1,1,1) |
| FacilityToolBench | (-9,-438) | (354,159.49) | (1,1,1) |
| FacilityPowerCommunications | (985.82,-351) | (204,221.56) | (1.09,1,1) |

## 상판과 연장면
Stage 3 상판 RectTransform은 Stage 2 저장값을 직접 복사했다. anchoredPosition=(113.14,-483), sizeDelta=(1280,394.186859), localScale=(0.82323,0.82323,0.82323), anchors/pivot=(0,1). 실제 표시 크기는 sizeDelta×scale이다.

CounterLeftExtension/CounterRightExtension은 Counter의 자식 RawImage이다. 폭 약 137.43/138.26, 높이 394.19이며 중앙 상판의 가장자리 UV를 반사한다. Stage 3에서는 Stage3CounterTop 텍스처를 사용한다. `RestoreCounterExtensions`는 명시적 단계 적용 시 해당 기준 씬의 UV·그림·배치를 복원한다. 런타임 배치 초기화는 없다.

## Stage 3 프레임 수정
처음 Stage 2 프레임을 공유한 것은 잘못된 적용이었고 수정했다. 현재 Stage 3의 Canopy는 Stage3Shop의 Stage3Ceiling, 양쪽 기둥은 Stage3LeftPillar/Stage3RightPillar Sprite를 사용한다. Stage 3 고유 트러스·기계식 구조를 유지한다. 여섯 설비 위치와 Stage 3 상판 그림은 유지했다. 설비 금속 반응은 상판과 맞추고 접촉 그림자는 (0.5,0,1.15,0.75)로 적용했다.

Stage 2 프레임 안쪽 검은 테두리는 image_gen으로 얇게 편집했다. `Assets/Textures/art/Facility/Frame/Stage2RustedFrame.png`를 교체했으며 1672×941 RGBA와 기존 meta/GUID를 유지한다. 이전 PNG는 output/stage-frame-correction에 보존했다.

## 검증 한계
Unity 컴파일 오류 0, Console 오류 없음 확인. Editor 렌더 확인. Play Mode와 실제 업그레이드 구매는 미검증이다. 최종 Editor 미리보기는 output/facility-preview/front-20260916-080019.png이며 Play Mode 검증은 미실행이다. 이번 작업은 Git commit/push하지 않았다.

## 최신 Stage 3 건메탈 수정 (2026-09-16 08:00)
상판·닫힌/열린 상자·설비 6개, 총 9 PNG를 기존 Assets/Textures/art/Facility 경로에 반영했다. 밝은 넓은 반사광을 줄이고 건메탈 바탕, 패널 이음새, 볼트, 마모 도색을 추가했다. Stage3Shop 원본 기둥/캐노피 구조는 유지했다. 모든 GUID와 Sprite ID를 유지했다. 생성 해상도 차이: CounterTop 1447→1446px 폭(기존 crop 폭만 1px 축소), PowerCommunications 1322×1190→1320×1191(기존 crop 유지). Canvas 크기는 변하지 않는다.

배경 오류: 도시·철조망은 원래 1280폭인데 군중·안개·바리케이드는 축소된 배치로 혼재했다. Stage 2 승인 기준의 배경 RectTransform을 함께 적용했다. FarBackground=(52,-42), size=(1176,661.5), MidBackground=(52,0.26), size=(1176,529.2). 군중과 철조망을 같은 배경 기준으로 복원했다. 단계 적용 코드에도 fog/crowd/barricade/guard/searchlight를 포함했고 저장 경비 위치를 다시 계산하는 호출은 제거했다.

최신 Stage 3 승인 사본: output/stage3-reference/20260916-080019/Stage3.unity. 수정 전 PNG/meta, 프롬프트, 설치 기록은 output/stage3-gunmetal/. Unity 컴파일/Console 오류 0, PNG 9개 GUID 및 Sprite crop 범위 검사 완료. 실제 플레이 검증은 미실행.

## 최종 교정 — 2026-09-16 08:07 (이전 건메탈 결과 대체)
사용자가 생성 결과의 질감·비율·하단 그림자를 거부하여 Stage3 PNG 9개와 meta를 output/stage3-gunmetal/before의 생성 적용 전 파일로 완전히 복원했다. 바이트 일치 확인. 거부된 결과는 output/stage3-correction/before에 보존했다.
FrontContainer 위치 (522,-529)는 유지하고 XY 배율을 같은 값으로 맞췄다. 표시 폭은 승인된 Stage2 상자의 Rect 폭×X 배율을 기준으로 약262.92이다. 크기360×240의 XY 배율은 약0.73033. preserveAspect=true. 원본 상자 종횡비를 세로로 누르지 않는다.
설비6개와 상자의 contactShadow=(0,0,0,0), bottomShade=0, UI Shadow 비활성. PNG에 포함된 원래 그림자를 사용한다. highlightResponse=1로 중복 하이라이트 압축을 제거했다. 시계는 Assets/Textures/Checkout/Shop/Stage3Clock.png의 Sprite로 교체했으며 위치·크기·숫자 Rect는 유지했다.
현재 작업 씬과 Stage3Reference 저장. 컴파일 오류0, Console 오류0, Editor 미리보기 확인. Play Mode 미검증. 이후에는 위의 08:00 건메탈 생성본을 다시 적용하지 않는다.

## 현재 최종 기준 — 2026-09-16 08:10 (08:07 원본 복원 취소)
사용자는 건메탈 스타일을 유지하라고 재확인했다. 상판·상자2종·설비6종의 건메탈 PNG 및 대응 crop을 다시 적용했다. 08:07에 수정한 상자 동일 XY 배율(약0.73033), 위치, 중복 그림자 비활성, Stage3Clock Sprite는 그대로 유지했다. PNG만 복원한 것이며 이전 씬을 덮어쓰지 않았다. 앞 절의 brushed steel 원본 복원 지침은 더 이상 적용하지 않는다. Editor 미리보기: output/facility-preview/front-20260916-081044.png. Play Mode 미검증.

## Stage 3 시계 표시창 교정 — 2026-09-16 08:17
Stage3Clock Sprite(94×37)의 비율에 맞춰 몸체 Rect를 170.5×67.1117로 변경. X=1078과 배율은 유지하고 Y=19→-4로 내려 상단 잘림을 피했다. BusinessClock 숫자 중심=(85.25,-34.22697), 크기=(109.12,29.52915), 가운데 정렬. 폰트·글자색·시간 로직은 유지했다. 명시적 Editor 메뉴로만 적용했으며 런타임 위치 초기화는 없다. 작업 씬 및 Stage3Reference 저장, 적용값 재조회 확인. 자동 RenderTexture 미리보기에는 Canvas 표시 범위가 달라지는 현상이 있어 Game View 최종 시각 검증은 미완료이다.

## 열린 상자 픽셀 표현 — 2026-09-16
Stage3CrateOpen.png만 coarse pixel art로 수정하고 내부 벽·바닥을 거의 검은 저대비로 변경했다. 1448×1086 RGBA 원본 크기, GUID와 Sprite crop은 유지. Unity maxTextureSize=256, FilterMode=Point, mipmap 없음. 씬·배치·애니메이션·닫힌 상자 파일은 변경하지 않았다. 이전 PNG/meta는 output/stage3-open-pixel/before에 보존. 원화 시각 확인 및 Unity Console 오류 없음; 실제 쏟는 애니메이션은 미검증.

## 전체 상태 보존 및 건메탈 시계 — 2026-09-16 08:23
사용자 요청으로 Stage3 전체 현재 상태를 output/stage3-reference/20260916-082109/Stage3.unity에 저장했다. composition.txt와 asset-hashes.json도 기록했다. 시계 건메탈 적용 후 최종 기준은 output/stage3-reference/20260916-082340/Stage3.unity이다.
새 시계: Assets/Textures/art/Facility/Frame/Stage3GunmetalClock.png, GUID 6faf682dc42b41f4e8d93c2a345dc83d. 짙은 청회색 건메탈, Point/256 임포트, 기존94:37 표시 비율 유지. 사용자 현재 시계 위치(1055,6)를 포함해 모든 배치·숫자·조명 설정은 유지했다. 전후 씬 diff는 CounterClock m_Sprite 참조 1개뿐이며 다른 직렬화 값은 동일하다. Unity Console 오류 없음, PlayMode 미검증.
