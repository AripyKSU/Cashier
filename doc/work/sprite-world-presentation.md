# 작업 상태: Sprite world 로컬 개발

## 환경 연출 선택 이관 (2026-09-11, 87844af)

- 작업 기준 `60a729f`, 원본 `87844af6a25a2a2c8f66affa31d1adff36de09f4`. 전체 branch merge가 아니라 prototype 및 직접 의존성 386파일을 선택 반영했다. 원격 output/cache·Packages·ProjectSettings·다른 제품 코드와 삭제는 반영하지 않는다.
- 백업: `UserSettings/LocalBackups/AstraEffects-87844af-20260911`의 기존363파일 해시 일치. 제품·Local·폰트 기준본 포함. 기존8GUID를 유지하고 원본 소비자 참조만 치환했다. 이관 경계/직접 이미지 영향은 [리소스 기록](../DYSTOPIA_RESOURCE_INTEGRATION.md)을 따른다.
- 기존 WorldSceneView가 원본 새5마리 shader, 안개3층, 연기2개/4프레임, 경비병2명과 총구3사각형×2를 갱신한다. 게임 모델/난수/세션과 독립이며 prototype gameplay component는 붙이지 않는다.
- 표현 초는 씬 수명 동안 누적한다. 준비되지 않음·전면 숨김·alpha0·pause에는 누적하지 않으며 날짜만 바뀌었다고0으로 만들지 않는다. 경비병 원본은 바깥을 향할 때만 발사하여 최초 발사까지 약35~47초의 **누적 전면 표시 시간**이 필요하다. 첫30초 영업에 반드시 발사하지 않는다. 재활성 pause에서도 마지막 shader 시간을 복원한다.
- 환경8PNG는 기존 사용용 사본과 원본 픽셀 hash가 같아 재사용했다. `doc/data/DYSTOPIA_RESOURCE_MAP.csv`의 해당8행은 그대로 유효하다. 새 PNG/별도 외부 그림은 없다. WorldWhite.asset은 총구 사각형과 새 shader의 FullRect용4×4흰 Texture/Sprite를 한 native asset에 저장한다.
- 배경0~11은 상대 순서를 유지하며5배 간격으로 재배치했다. Birds22·Smoke23·Fog24/29/44·Guard46/51·Flash47/52로 모두 대기열91~100 아래, 기존 캐노피250/탐조등273~274 아래다. UI/대기열·slide 계약은 유지한다.
- WorldFog 재질은 원본 흐름/seed/opacity를 복사하고 Sprite 경로와 외부 시간만 사용한다. 기존 UI Fog는 _Time 동작을 유지한다. WorldGuard는 원본 픽셀격자20×15·무채색밝기.65만 옮긴다. 전체 PixelStage 조명/메시 캡처는 도입하지 않았다.
- 검증: EditMode 230/230, PlayMode 41/41, 실패·skip·미완료 0. PlayMode 이후 WorldWhite의 texture 크기를 Sprite rect와 같은 4×4로 보정하고 EditMode를 재실행했다. 새 실제 렌더 픽셀 변화·pause/hide/resume·Main 지침/영업 진입·보호 hash와 clean Local 인계를 확인했다. 증거는 [TESTING](../TESTING.md#sprite-world-환경-연출-선택-이관-2026-09-11)에 기록했다. 최종 사용감과 Player build는 미확인이다.
- 사용자 후속 요청으로 이번 선택 이관과 환경 연출을 작업 branch에 commit/push한다. MainScene·공유 UI·폰트·개인 씬·백업은 제외하고 기본 branch 병합은 하지 않는다. 아래 기록은 이전 단계다.

## 이전 단계

- 기준: `codex/sprite-world-presentation`, `total_merge e62fcaf`.
- MainScene·메타·OperatingPanel과 기존 CustomerQueueView·CustomerPresenter·SaleSortingPrefabSetup은 total_merge 원본으로 복원했다. World 표시는 공유 Main에 적용하지 않는다.
- 개인 씬: `Assets/Scenes/Local/SpriteWorldSandbox.unity`, GUID `e051e8369858a0949b19013e7a502dcb`. Git 제외다. GameUI·OperatingPanel 루트만 native unpack했고 다른 중첩 prefab은 유지했다. appearanceImage=null, 기존 성별 TMP 연결을 유지한다.
- 백업: `UserSettings/LocalBackups/SpriteWorld-20260911-133245`. 이전 CustomerQueueSandbox와 작업 중 MainScene 각각 scene/meta를 바이트 대조했다. 이전 개인 씬 pair만 삭제했으며 백업으로 복구할 수 있다. 다른 개인 파일은 보존한다.
- 공유 구현: WorldSceneView, 신규 GUID의 CustomerWorldQueueView, CustomerWorld prefab, 전용 material/shader. 기존 Image renderer는 별도 CustomerQueueView로 유지한다. loader·CSV·시간·거래·방문·handle 소유권은 변경하지 않는다.
- 월드 대기열의 Q-01은 별도 TMP로 3초 불만 표시를 보완했다. legacy Main 경로는 미보완이다.
- 검증: Edit229/229·Play40/40, 실패·skip·미완료0. 실제 Init→Main/Local 양쪽 진입과 Console error0·compileFailed=false. 증거와 이전 상세 smoke의 구분은 [TESTING](../TESTING.md#sprite-world-표시-분리-2026-09-11)을 따른다.
- 향후 통합: [MainScene](../MAINSCENE_INTEGRATION.md#월드-표시-분리-2026-09-11)의 공유 UI 최소 변경을 적용하고 World prefab을 Canvas 밖에 연결한다. 개인 씬을 Git으로 병합하거나 GameUI prefab을 복제하지 않는다. unpack된 개인 UI에는 공유 prefab 변경이 자동 전파되지 않으므로 필요한 변경만 명시적으로 이관한다.
- 인계: Play 종료, SpriteWorldSandbox clean, 해당 개인 GUID 선택, Play 시작은 InitScene. 최종 가독성·배치·사용감과 Player build는 미확인이다.
- 보호: 사용자 Mulmaru 폰트 변경·stash2 유지. Main/Operating·Addressables·ProjectSettings·Local·백업은 커밋에서 제외한다. Git 결과는 완료 보고와 실제 커밋 이력을 따른다.
- 로컬 slide 정렬 보완: OperatingPanel 자식 순서를 AstraFrontView→SaleSortingUI→PriceInput→CalculatorToggle로 두고 DialoguePanel의 기존 Canvas는 overrideSorting=false로 부모 정렬을 따른다. 이전 sibling 역전과 대사 Canvas order30 때문에 Counter·대사가 작업대 위에 남았다. 공용 코드·prefab은 변경하지 않았다.
- 확인: 전면 대사·slide 중간·완료 및 계산기/상품 raycast 확인, Console error0. 중간 표본은 실행 중에만 전환10초로 늦췄고 저장된 사용자값1초는 유지했다. 재진입 Awake/Refresh 후 override=false 유지. 화면 증거는 Git 제외 `Temp/SortingOrder-Front-Final.png`, `SortingOrder-Mid-Fixed.png`, `SortingOrder-Complete.png`. 실제 드래그 사용감은 사용자 확인 대상이며 이번 정렬 수정에 자동 suite는 재실행하지 않았다.
