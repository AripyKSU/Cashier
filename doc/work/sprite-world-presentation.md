# 작업 상태: Sprite world 로컬 개발

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
