# 기능 API 검증

## 전량 제외 대사·거절 사유 분리 (2026-09-22)

- `GameplayBalance` 작업 디렉터리에서 Unity6000.3.18f1 숨김 batchmode로 컴파일·관련 suite를 실행했다. 컴파일 오류0. 신규 TextData **8513~8540(28개)**와 성향 CSV7열을 함께 반영했다.
- EditMode **227/230**, 실패3·skip0. 실패는 `CustomerCsvTests.ProductCsvUsesRebalancedPricesAndCosts`, `DailyProductSelectorUsesRebalancedKindsAndStageGuarantee`(LoadData 후 FK 공개 전 Rows를 읽어0개), `FacilityTests.CsvExposesUpgradeKindsAndStageContracts`(기대500000, 실제280000)다. 해당 상품 로더·상품/설비 CSV·실패 assertion은 이번에 변경하지 않았다. 변경 전 전체 실행 증거는 없으므로 기존 baseline 실행 결과로 표현하지 않는다.
- 전량 제외 관련 실제15성향 행·14프로필 대사 선택/퇴장 보존, null·빈 목록, 가격 거절/판매 성사 사유, 누락 열·빈 후보·0·중복·잘못된 FK 검사는 통과했다. 첫 실행은 생성자 reflection/행 수 fixture 및 신규 테스트 현재가 입력 수정 전224/230이며 최종 결과로 사용하지 않는다.
- PlayMode **5/5**, 실패·skip0: `CalculatorFollowsSaleSortingLifecycle`, `EmptySalePreservesReasonAndCompletesExactlyOnce`, `FacilityAndReputationShareCompletedDayBoundary`, `ProgressPreservesTransactionAndRejectsDuplicateSubmission`, `QueueControllerEmptyCounterAndFinalExitPresentation`. 실제 분류 패널의 전량 제외·키패드 잠금, 전용 대사 ID, 매출0·거절1회·중복 제출 차단·퇴장·다음 방문 및 정산 경계를 검사했다.
- 증거: `Logs/TestResults/no-sale-items-20260922-edit-final/EditMode.xml`·`.log`, `Logs/TestResults/no-sale-items-20260922-play/PlayMode.xml`·`.log`. Unity 재시작으로 Temp의 이전 EditMode 기록이 정리되어 동일 suite를 최종 로그 경로에서 재실행했다. PlayMode 결과는 재시작 전에 Logs로 복사했다. Logs는 Git 제외다. batchmode는 테스트 종료 후 자체 종료했으며 씬·프로젝트 설정·패키지는 변경하지 않았다.
- CSV 정적 비교: 기존 Text499개와 성향15행의 기존 셀 모두 보존, 신규 ID28개 고유·모든 FK 유효, `git diff --check` 통과. 관련 회귀 suite 결과는 실패3건으로 `FAIL`, 요청 변경의 종합 검증은 **PARTIAL**(관련 API 통과, 대사 체감·말풍선 UI 수동 확인 미실행)이다. commit·push는 하지 않았다.


## ART_UPDATE_20260916 최종 기능 검증 (2026-09-16)

- 기준: `codex/store-resource-exchange 01f5707f`, Unity6000.3.18f1/PID9716. 사용자가 마지막 단계로 미룬 1~4단계 검증을 실행했다. 전체 EditMode **319/323**, 전체 PlayMode **64/67**; 각각 skip/미완료0. 관련 EditMode(Facility35·StoreStageData10·WorldScene12·먼지2) **59/59**, 새 착지/먼지 수명 PlayMode1건과 기존 단계 표시/실제 자산 로드4건 모두 통과.
- EditMode 실패4건(이전 통합과 동일): `BusinessClockAndSortingTests.Vacuum_GameUiPrefab_HasAstraVisualAndNozzleBindings`, `VacuumAsset_UsesAstraImportContract`는 옛 자산 경로 기대; `PriceEventTests.ReproducibleSelectionAndInvalidReferences`는 OverflowException 미발생; `InspectorEventTests.ActualCsvHasSevenEventsAndMultilinePages`는 Text8397 폭571.640015>571.
- PlayMode 실패3건(이전 통합과 동일): `GameSessionApiTests.DailyMaintenanceInsufficientBalanceDefersWholePayment`는 예상 경고 문자열 미출력; `InspectorMainClockFollowsDayProgressAndPreviewCannotAdvanceBusiness`는15 vs15.666667; `InspectorWorldEffectsPreserveSuspendedTimeAndFlashLifetime`는39.39991 vs49.4087296. 제품을 테스트 기대에 맞춰 임의 변경하지 않았다. 전체 검증 **FAIL**, 요청 범위 종합 인계 상태는 미완료를 포함한 **PARTIAL**이다.
- 전체 실행 증거: `Temp/TestResults/20260916-190700-cabc07f3d4414cbea371040f984a24f2/EditMode.xml`·`.log`, `Temp/TestResults/art-update-step5-play-20260916-a/PlayMode.xml`·`.log`. 첫 Play 셸 시도는 `no Unity instances running`으로 시작 전 거부됐다. 기존 Editor 연결을 확인하고 CashierTestRun.Start로 실행했으며 중복 job은 만들지 않았다.
- 이후 신규 `StoreStagePresentationTests.PrepareRejectsInvalidFacilityOrderWithoutPublishingPartialState`만 **1/1** 실행: 중복·누락·미등록 설비 순서를 실제 PrepareAsync에서 거부, 이전 준비/표시 유지, 입력 복원 후 정상 준비·적용 확인. `Temp/art-update-step5/facility-order-test.json`. 테스트 추가 후 전체 suite를 반복하지 않았으므로 위67건과 추가1건을 구분한다. 자산 배열 변경은 일시적인 메모리 입력이며 finally 복원·파일 해시 보존을 확인했다.
- 실제 Init→Hub 새 게임→Main→감독관4페이지→영업→Sorting 진입 확인. Main의 표시 API1→2→3→1로 상자/시계 Sprite 교체, 도장 UpdateView로 LedgerStampNeutral 할당 확인. 기존 누락 Sprite2개의 런타임 교체 경로만 확인한 것이며 원본 참조 정리나 전체 수동 UX 통과가 아니다. `Temp/art-update-step5/{main-runtime,inspector-runtime,sorting-runtime}.json`.
- 최종 제품 Console error0·컴파일 실패 없음. Main/Local씬/meta·기존 제품 자산 보존; InitScene clean·Play 종료·시작 씬 override 없음·runInBackground=false 복원. `Temp/art-update-step5/{product-console-errors,preservation,editor-after}.json`. 최종 가독성·조작감·화면비·Player build는 미검증. [범위·병합 보존 사항](work/store-resource-exchange.md#art_update_20260916-5단계-누적-검증인계-2026-09-16).

## total_merge DV3 착지 먼지 후속 통합 (2026-09-16)

- 입력: `total_merge 9b074160` + `DV3 c7728927` (`상자 먼지 정상화`). 고정 접점 기본값 `(640, -578)`과 Inspector 오프셋을 채택하고, 기존 total_merge 폰트는 유지했다. MainScene·Prefab·CSV 변경은 없다.
- 컴파일 오류0. 관련 EditMode **19/21**, 실패2·skip0: `Temp/dv3-dust-edit.xml`. 먼지 재생/중지 및 상자 위치·크기와 무관한 기준점/추가 오프셋/null 입력 검사는 통과했다. 실패는 앞선 통합에서 기록한 청소기 자산 경로 기대2건으로 이번 변경과 무관하다.
- 실제 Init→Hub 새 게임→Main→감독관→PreOpen→Sorting 진행 후 실제 패널의 먼지 API로 입자10개·부모 연결·고정 접점·Stop 후 전체 투명화를 확인했다: `Temp/dv3-dust-smoke.txt`. UI callback/API 기반 검사이며 최종 시각적 착지 위치·사용자 체감·Player build는 미검증이다.
- 초기 검사에서 감독관 표시 중 비활성 OperatingPanel의 먼지 API를 직접 호출해 coroutine 오류1개를 발생시켰다. 정상 영업 단계에서는 재생/중지 검사가 통과했으며 이를 제품 흐름 오류나 Console 무오류로 오인하지 않는다. Play 종료 후 InitScene clean·개인 씬 선택·runInBackground 원래 설정 복원. 전체 상태 **PARTIAL**(기존 테스트 실패2건 유지).

## total_merge FacilityUI·DV3 통합 (2026-09-16)

- 기준 `total_merge 5edaba55`, 입력 `FacilityUI 45ca7765`, `DV3 d97ad4c5`. FacilityUI 인계서 `work/facility-pamphlet-merge-handoff.md`를 실제 diff와 대조했다. FacilityUI 로컬 병합은 `6963b28d`이며 DV3와 후속 테스트 보정을 함께 검증했다. 이번 작업은 원격 push를 포함하지 않는다.
- 신규 팸플릿 UI·가격·무료 단계 확장 계약을 채택하면서 기존 FacilityData의 단계별 외형 FK 3열을 유지했다. 이미지 8개는 GUID를 보존해 `Textures/UI/Dystopia/FacilityUpgrade`로 옮겼다. 기존 MainScene·폰트·개인 IDE 상태를 유지하고, GameUI의 중앙 Window override와 SettlementPanel 변경을 통합했다. Addressables 추가는 없다.
- 통합 컴파일 오류0. 전체 EditMode 최초 **319/324**, 실패5·skip0(`Temp/facility-dv3-edit.json`). 충돌 보정 과정의 invalid ID fixture 순서 문제를 수정한 후 **FacilityTests 35/35**(`Temp/facility-dv3-edit-fixed.json`). 전체를 다시 실행하지 않았으며 남은4건은 기존 청소기 구형 경로2·감독관 Text8397 폭1·가격 이벤트 overflow 기대1이다.
- 전체 PlayMode 최초 **60/66**, 실패6·skip0(`Temp/facility-dv3-play.json`, `.xml`). 설비/시민권 관련 fixture를 보정했다: 현재 시민권 CSV 가격을 차감 기대값으로 사용, 가계부 천 단위 쉼표 반영, 단계 구매 다음 날 감독관 후 PreOpen 검사, 2일차 유지비3000을 충족할 테스트 자금. 제품 구매·경제 로직의 검증을 완화하지 않았다.
- 보정 후 개별 PlayMode **3/3** 통과: `FacilityControllerPurchaseAndModalBoundaries`, `FacilityPurchaseUnlocksOnlyNextDayAcrossProgressInstances`, `CitizenshipNotificationFailureStillFinalizesOnce`. 증거는 `Temp/facility-dv3-modal-final.json`, `-unlock-final.json`, `-citizenship-final.json`. 최초 모달 재검사는 유지비 부족 fixture로0/1(`-modal-fixed.xml`); 이를 보정한 최종 결과와 구분한다. 접두어 필터의0건 실행은 통과 실적에서 제외했다.
- 미해결 PlayMode3건: `DailyMaintenanceInsufficientBalanceDefersWholePayment`의 구형 G 로그 기대, `InspectorMainClockFollowsDayProgressAndPreviewCannotAdvanceBusiness`의15시/15.666667시 차이, `InspectorWorldEffectsPreserveSuspendedTimeAndFlashLifetime`의 시간 차이. 입력 인계서에도 해당 유형의 실패가 있었으며 이번 작업에서 제품 수정이나 전체 재실행은 하지 않았다.
- 실제 Init→Hub 새 게임→Main에서 감독관 다음 버튼→PreOpen→영업/정산 API→팸플릿 버튼→구매 슬롯→무료 단계1→2→3→시민권 페이지를 확인했다. 일반 설비·확장11개 보유, 총차감5,245,000원, SoldOut 표시, 바깥 닫기 후 시민권 페이지 유지, 시민권10,000,000원 차감과 당일 종료 확정 후 BadEndingScene 진입을 확인했다. `Temp/facility-dv3-smoke.txt`.
- DV3 인스턴스1개, 합성 Backquote+U+I 입력으로 메뉴 열림, 공개 API로 자금+100,000·도덕성±10(일일값 불변)·1→2일 날짜 변경을 확인했다. 실제 키보드 체감과 시간 배율 버튼의 사용자 조작은 미검증이다. 일반 빌드 포함 정책은 [DEV3_GUIDE.md](DEV3_GUIDE.md)에 기록했다.
- 제품 Console error0, 신규/기존 자산 전체 GUID 중복0, 설비 FK 유효, 공유 Prefab missing script0. 기존 GameUI 하위 `FrontContainer.Image.m_Sprite`, `ReputationStamp.Image.m_Sprite`의 정적 missing reference2개는 기준5edaba55와 같은 참조다. 실행 시 단계/명성 이미지로 교체되는 경로는 통과했지만 원본 참조 정리는 별도 미완료다.
- MainScene/meta·기존 폰트4개·개인 씬2개/meta2개 hash 보존(`Temp/facility-dv3-before.json`), stash3개 보존. Play 종료 후 InitScene clean·기존 개인 씬 선택·runInBackground=false를 복원했다. 최종 화면비·직접 마우스 UX·Player build는 미검증이다. **PARTIAL**: 요청 기능의 관련 검증과 실제 경로는 통과했으나 위 전체 suite 실패·기존 참조 정리가 남아 있다.

## total_merge Hub·가게 리소스 통합 (2026-09-16)

- 입력: `total_merge 0006cee8` → `hubimage 6e92a4cb` → `codex/store-resource-exchange cb005fbe`. 로컬 병합 커밋은 `d585393b`, `cf15bd2b`이며 후속 호환 보정을 포함해 검증했다. 원격 push는 수행하지 않았다.
- 충돌 해결: 최신 total_merge 폰트 자산·참조를 보존하면서 CustomerWorld의 원근 배율·연령별 오프셋을 통합했다. 개인 IDE workspace 변경은 제외했다. 최신 딸 UI에 맞지 않는 구형 너비 0 override는 제거했다.
- 호환 보정: 손님·딸의 nullable 외형 FK는 빈 값일 때만 선로드를 건너뛰며 잘못 지정한 FK는 실패한다. 설비 단계별 외형 12개를 기존 선로드·캐시에 포함했다. Hub PNG는 GUID를 보존한 단일 2D Sprite로 import하고 Scene의 실제 Sprite fileID를 연결했다. 계약은 [SCENE_WORKFLOW.md](SCENE_WORKFLOW.md)를 따른다.
- Unity 6000.3.18f1 통합 컴파일 오류 0. 전체 EditMode **320/324**, 실패4·skip0: `Temp/hub-store-merge-edit.json`. 전체 통과 상태는 아니다.
  - 청소기 2건: 테스트가 이전 `DystopiaPrototype/TopDownTest/Art/Vacuum.png`, `VacuumWind.mat` 경로를 요구한다. 현재 자산은 `Textures/art/Workbench/Vacuum.png`, `Materials/Checkout/VacuumWind.mat`에 있다. 이번 통합에서는 테스트 계약을 임의 변경하지 않았다.
  - 가격 이벤트 1건: `ReproducibleSelectionAndInvalidReferences`의 0일차 가중치 overflow 기대가 실패했다. 해당 테스트·스케줄·제품 구현은 통합 기준과 동일하다.
  - 감독관 1건: `ActualCsvHasSevenEventsAndMultilinePages`에서 Text8397의 비개행 폭571.640015가571 제한을 초과했다. Text CSV·패널·테스트·유지한 폰트는 통합 기준과 동일하며 기획 문구나 폰트를 변경하지 않았다.
- 관련 PlayMode **6/6**, 실패·skip0: 실제 선로드 정상/빈 FK/잘못된 FK 1건(`Temp/hub-store-merge-preload.json`), 가게 단계 표시4건(`Temp/hub-store-merge-store.xml`), 대기열·연령 오프셋·대사 수명1건(`Temp/hub-store-merge-queue.xml`). 가게·대기열 실행 뒤 CLI 연결이 끊겨 반환한 오류와 달리, 해당 실행 시각의 Unity Test Runner XML은 각각4/4·1/1 완료다. 재실행하지 않았다. 앞선 복합 필터 시도는0건으로 검증 실적에서 제외했다.
- 실제 Init→Hub의 새 게임 버튼→Loading→Main→감독관 다음 버튼→PreOpen→영업 버튼→Sorting을 확인했다. 준비 완료·덮개 유지 후 영업 시 해제, manager/world/queue 각1개, Main missing script0, 제품 Console error0. `Temp/hub-store-merge-smoke.txt`. 버튼 listener 기반 경로 검사이며 전체 마우스 UX·가독성·Player 빌드는 미검증이다.
- 등록 manifest16개와 Resource/Facility FK·자산/meta GUID 일치, Assets 전체 중복 GUID0. MainScene/meta·기존 폰트3개·개인 씬2개/meta2개는 사전 hash와 동일하다. `Temp/hub-store-merge-static.json`. 기존 stash3개는 보존했다.
- Play 종료 후 InitScene clean, 개인 씬 선택·runInBackground=false·시작 씬 override 없음으로 복원했다. 병합 전용 인계서는 최종 승인·정리 전까지 유지한다. 상태는 **PARTIAL**: 요청 기능 경로 검증은 통과했지만 전체 EditMode 실패4건과 최종 사용자 화면 확인이 남아 있다.

## 2026-09-16 가게 리소스·설비 외형 Prefab

- `codex/store-resource-exchange` 구현: 관련 EditMode45/45(StoreStageData10·Facility35), 신규 PlayMode3/3. 실제 Editor Addressables Prefab21·Sprite3 로드, 단계/동일 단계 활성 갱신·상자 상태·종료 소유권을 확인했다.
- 전체 EditMode293/297, 실패4·skip0. 청소기 자산 경로2·가격 이벤트 overflow 기대1·배경 참조1은 이번 범위 밖이며 전체 통과로 간주하지 않는다. 전체 PlayMode·Player 빌드·사용자 UI/UX는 미검증이다.
- 증거: `Temp/TestResults/store-resource-edit-20260916-b/EditMode.xml`, `.log`, `Temp/store-play-tests-a.json`. 처음 Edit283/297에서 실패했던 설비 테스트10건은 옛 가격/행 문자열 의존을 보완했다.
- 자산16개 등록 및 단계 Front/TopView6개 검사 통과. MainScene 직접 변경 없음. 기존 손님/딸 이미지 주소 누락은 전체 새 게임 진입의 별도 선행 작업이다. [계약·전체 검증 기록](work/store-resource-exchange.md#7-검증과-병합-전-확인).

> 2026-09-13 현재 진입 경로: Init 부트 후 Hub 메뉴에서 새 게임을 선택해야 게임 씬으로 이동한다. 엔딩·실패 화면의 새 게임 버튼은 Hub 복귀이며 즉시 재시작하지 않는다. [현재 메뉴·이어하기 사양](CITIZENSHIP_ENDING.md#hub-메뉴와-카메라-2026-09-13)을 따른다. 아래 날짜별 검증의 자동 Hub→Main 경로는 당시 기록이다.

## total_merge 딸 대화 통합 (2026-09-11)

- fetch 후 입력: `total_merge c756bec` + `codex/daughter-dialogue e19bd0f`. 충돌 없이 병합했으며 제품·데이터·프리팹 내용은 딸 대화 브랜치와 동일하다. MainScene은 기존 GameUI prefab의 nested DaughterDialoguePanel/presenter 연결을 상속한다. Scene 파일 수정은 없다.
- 통합 checkout에서 EditMode **230/230**, 실패·skip·미완료0: `Temp/TestResults/20260911-182145-292ce84f05d74e03a07299bbbfc5c5f9/EditMode.xml` 및 `.log`. PlayMode **45/45**, 실패·skip·미완료0: `Temp/TestResults/20260911-182707-b35c83103157424dbe2629239754798d/PlayMode.xml` 및 `.log`.
- 최초 Play 실행은 playModeStartScene=InitScene이 개인 게임으로 라우팅해240초 timeout이었다. `43b6b911-1136-44c1-b38a-86b42587e930` job 취소와 active=false/playing=false 확인 후, 완료 callback 없이 남은 해당 runner pending 상태를 정리했다. 첫 run 폴더의 `PlayMode-Aborted.txt`에 기록했으며 성공으로 집계하지 않는다. 시작 씬 override를 null로 해제한 새 run이 위45/45 결과다. 코드/테스트 변경은 없다.
- 실제 Init→Hub→MainScene→감독관→PreOpen→영업 버튼→현재가 수락 거래→정산: 누적 도덕성0, TextIdx8194(오늘 하루는 어땠어?), Resource4201을 표시했다. 설비 창 열기·닫기 후8194 유지. `Temp/DaughterMerge-Main-Smoke.txt`, `Temp/DaughterMerge-Main-Settlement.png`. Inspector 진행과 거래/마감은 API를 호출한 확인이며 전체 마우스 UX 검증을 의미하지 않는다.
- 최종 compileFailed=false, 제품 Console error0, Main missing script0. 신규 GUID11개 중복 없음, 자산/meta 짝 유효. MainScene/Local씬·meta/기존 폰트2개는 준비 백업5개 hash와 동일하다. 기존 stash2개는 보존했다.
- 최종 MainScene clean open, Play 종료, InitScene 시작 및 Use MainScene, runInBackground=false. 개인 씬 파일을 삭제하거나 변경하지 않았다. 원격 push와 최종 UI/UX·다른 화면비·Player build·저장 복원은 이번 완료 범위 밖이다.

## 딸 대화 시스템 (2026-09-11)

- 기준: total_merge c756bec에서 분기한 `codex/daughter-dialogue`의 미커밋 구현. 최종 EditMode **230/230**, PlayMode **45/45**, 각각 실패·skip·미완료 0. 기존 `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 240`으로 실행했다. 증거: `Temp/TestResults/20260911-175315-d81c54e6473249f8b170c72af1a39e98/EditMode.xml`, `PlayMode.xml` 및 각 `.log`.
- 실제 CSV/FK·6개 도덕성 구간 경계와 소수/무한 범위·후보 선택·날짜별 이미지 전환·모든 후보 대사의 폰트 글리프를 검사했다. PlayMode는 정산 시 누적 도덕성 선택/당일 결과 고정/다음날 수명과 선택 실패 시 경제 정산 중복 방지까지 확인했다.
- 초기 개별 Edit 필터 5/5 이후 Play 필터는 개인 playModeStartScene 설정 때문에 지연·중단되어 통과로 집계하지 않았다. 활성 job 취소 후 기존 runner로 전환했다. `20260911-174531-0f5ca0038442476eb24ed3cf58dc4c9a/EditMode.xml` 229/230은 기존 TextData 행 수 기대182→200 보정 전이다. `20260911-174736-b224acf79ae8411f9f21457cddf2eb71/`은 Edit230/230·Play44/45이며 Mulmaru의 한글 글리프 누락을 발견했다. 기존 Mabinogi 폰트 참조로 교체하고 글리프 검사를 추가했다. test assembly의 Unity.TextMeshPro 참조 누락 컴파일 오류도 최종 실행 전에 해결했다.
- 실제 Init→SpriteWorldSandbox→감독관→영업→수락 거래→정산에서 Morality0/TextIdx8192/Resource4201 표시, 설비 상점 열기·닫기 후 대사 고정을 확인했다. `Temp/Daughter-Smoke.txt`, `Temp/Daughter-Settlement-Final.png`. 최종 suite 후 수정은 딸 패널을 정산 보드 아래로 옮기는 직렬화 배치뿐이며, 실제 화면으로 검증했다.
- 최종 컴파일 실패 없음·제품 Console error0·missing script0. Play 종료, 개인 씬 저장, InitScene 시작/개인 씬 선택, runInBackground=false. MainScene 파일과 기존 사용자 Mulmaru hash를 보존했다. 테스트 생성 TMP fallback glyph만 사전 내용으로 복구했다.
- 기존 개인 씬의 명성·유지비/시설 버튼 겹침은 이번 범위 밖이며 전체 UI/UX·다른 화면비·Player build·저장 복원은 미검증이다. 계약과 병합 연결은 [DAUGHTER_DIALOGUE_SYSTEM.md](DAUGHTER_DIALOGUE_SYSTEM.md)를 따른다.

## DailyInstruction + Sprite world Main 통합 (2026-09-11)

- 입력: total_merge e62fcaf + DailyInstruction 99fc83e + Sprite world b80dfda. 최종 EditMode **225/225**, 실패·skip·미완료 0: `Temp/TestResults/20260911-161803-6305b91b5e4c4dbc92318c87283d07c1/EditMode.xml` 및 `.log`. 최종 PlayMode **43/43**, 실패·skip·미완료 0: `Temp/TestResults/20260911-162058-b5c3dbede73d4f65a1a01dca9b02aeb8/PlayMode.xml` 및 `.log`.
- 최초 Edit 결과는 160940(205/225), 161209(210/225), 161304(168/225)였다. 구형 전체 가격 fixture, Resource FK·기대 Log, CSV 행/문자열 치환, reflection 인자를 현 계약에 맞췄다. 161338 Edit는225/225, Play는37/43이었다. 호환 가격표의 전체 해금 순회와 구매 후 정산 현재 잔액 고정이라는 실제 통합 회귀를 수정했으며, 구형 미납 중단 기대는 유예·다음날 계약으로 교체했다. 161803 Play41/43의 남은2건은 신규 overflow 검사에서 NUnit이 reflection 예외를 unwrap하는 형식 차이였다. 실패 XML은 각 시각 폴더에 보존했다.
- 지침 건수/벌금 누적 overflow를 사전 검증하고 총/일일 도덕성·잔고·거래 개수·판매 수입·지침 누적값 불변을 확인했다. 정산 snapshot은 불변이며 시설 구매 후 표시 현재 잔액만 별도 조회값으로 갱신한다. 제품 guard를 완화하지 않았다.
- 실제 Init→Main: WorldSceneView1·legacy queue0·첫날 Inspector→PreOpen 상품4/지침0→영업→Sorting 버튼/slide 완료(월드 숨김)→수락 거래→다음 방문→정산200→2일차 Inspector를 확인했다. `Temp/TotalMerge-Inspector.png`, `TotalMerge-Day1.png`, `TotalMerge-WorldFront.png`(이름과 달리 **slide 중간**), `TotalMerge-Sorting.png`. pause 시 표현 시간 유지, 실제 slide 완료 가림은 확인했으며 자동 suite의 pause/재활성/퇴장 검증과 구분한다.
- 실제 Debug 버튼으로10일차 지침1,20일차 지침2/서로 다른 대상/당일 상품 FK를 확인했다. 설비 미구매라 상품은4종이며6/8종은 상한이다. 20일차 수락 거래의 복수 위반2건·벌금1000, 테스트용 잔액0에서 유지비2100+벌금1000=미납3100/납부0/유예23일차를 확인했다. 이 잔액 조정은 runtime 검증 입력이며 저장하지 않았다.
- 정산에서 테스트 자금10000을 넣고 실제 시설 버튼으로12001구매: 당일inactive, 정산 현재잔액 문구9000/실제9000, 다음21일차active를 확인했다. 도메인 계산과 실제 UI 호출을 검증한 것이며 전체 마우스 사용감 판정은 아니다.
- 최소 배치 보정: 원본 GuidelineSlot1 x437→137, OpenBusiness/Border 정렬 및 부모의 구형 y=-548 override 제거. 정산의 명성/유지비 겹침과 시설 버튼의 납부·유예 가림을 해소했다. 최종 `Temp/TotalMerge-Day20-Final.png`, `Temp/TotalMerge-Debt-Final-Readable.png`에서 지침2개·납부0·미납3100·상환기한23일차 전체 표시를 확인했다. 이 마지막 변경은 직렬화 배치만이며 승인에 따라 전체 suite는 반복하지 않았다.
- 최종 compileFailed=false·Console 제품 error0·Main missing script/reference0. Play 종료, MainScene clean open, Init 시작/Use Main, runInBackground=false로 인계한다. Local 씬/meta와 사용자 Mulmaru는 준비 백업 hash와 동일하게 보존한다. 테스트의 TMP fallback 생성 데이터만 native 정리했다. Git/index는 설계 담당 소유이며 구현 담당은 조작하지 않았다.
- 최종 전반적 UI 사용감·다른 화면비·Player build·저장 복원은 미확인이다. 이전 절의 유지비 부족 시 중단·Main legacy 표시 설명은 과거 기준이며 이번 신규 미납/World 통합 계약으로 대체된다.

## Sprite world 환경 연출 선택 이관 (2026-09-11)

- EditMode 230/230: `Temp/TestResults/20260911-153412-53aa915127894169826a0797c2364f8e/EditMode.xml` 및 `.log`. PlayMode 41/41: `Temp/TestResults/20260911-152810-cbb07e99ba0d49d9b2f3f629026ab6a2/PlayMode.xml` 및 `.log`. 실패·skip·미완료 0. Play 이후 WorldWhite texture를 Sprite rect와 같은 4×4로 보정했고 Edit를 재실행했다. 이후 제품 코드 변경이나 전체 suite 재실행은 없다.
- 기존 fixture에서 연기 4프레임·총구 수명·pause 재활성·날짜 전환 시 표현 시간 유지·게임 시간 비간섭을 검사했다. 초기 MPB 생성자 오류, shader Color include 누락, 흰 Sprite UV 크기 불일치를 수정했다. 초기 magenta 스크린샷은 성공 증거가 아니다.
- 실제 Local 렌더 증거: `Temp/WorldBirds-On.png`, `WorldBirds-Off.png`, `WorldBirds-Moved-On.png`, `WorldBirds-Moved-Off.png`. pause 상태의 새 renderer On/Off 차이 186픽셀, 0.2초 후 177픽셀, 두 차이 mask의 대칭차 219픽셀로 이동/날개 모양 변화가 렌더됨을 확인했다. 실제 UV는 (0,1),(1,1),(0,0),(1,0), shader error 0. 원본 5마리 수식이며 구조물에 가려 모든 새가 항상 노출되지는 않는다.
- Local runtime API 확인: 표현 시간 10.05004에서 pause 5초·전면 숨김 5초 주입에도 유지, 재표시/resume 0.2초 후 10.25004 및 RenderRoot active=true. UI 활성 상태를 직접 전환한 검사이며 마우스 slide 사용감 검증은 아니다.
- Init→Main 진입·영업 시작: `Temp/WorldEffects-Main-PreOpen.png`, `WorldEffects-Main-Open.png`. 시계와 FrontContainerMale Sprite 참조 유효, missing script 0, compileFailed=false, 제품 Console error 0. 실제 PreOpenPanel은 `Assets/Textures/UI/Dystopia/DailyInstruction.png` 사본을 사용한다. Open 이미지는 손님 fade 도중 표본이다.
- 보호 검사: Main/meta·OperatingPanel·사용자 Mulmaru font·Local/meta 6개 SHA256이 `UserSettings/LocalBackups/AstraEffects-87844af-20260911`과 동일. TMP fallback 생성 데이터 정리 후 Git 내용 diff 0. ProjectSettings·Addressables 변경 없음, stash 2개 보존.
- 최종 상태: Play 종료, SpriteWorldSandbox clean open, missing script/reference 0, compile error 0. 개인 GUID `e051e8369858a0949b19013e7a502dcb`, playModeStartScene=InitScene, runInBackground=false. 최종 배치·가독성·사용감과 Player build는 미확인. 사용자 후속 요청으로 선택 이관·환경 연출의 작업 브랜치 커밋·푸시를 진행하며 실제 결과는 Git 이력과 완료 보고를 따른다.

## Sprite world 표시 분리 (2026-09-11)

### 개인 씬 분리 후 최종 검증

- EditMode **229/229**, 실패·skip·미완료0: `Temp/TestResults/20260911-133815-2a247445b89f47f3978bbb79849981a9/EditMode.xml` 및 `.log`.
- PlayMode **40/40**, 실패·skip·미완료0: `Temp/TestResults/20260911-133839-6abdc9359cbb476795f5b4a18120d03b/PlayMode.xml` 및 `.log`. 공유 legacy prefab의 테스트 인스턴스만 월드 UI로 구성하며 Local 자산에 의존하지 않는다.
- 실제 Init→Main 진입: GameUI 존재, legacy CustomerQueueView 1개·WorldSceneView 0개. Init→Local/SpriteWorldSandbox 진입: GameUI 존재, legacy 0개·CustomerWorldQueueView 1개. 각 경로 Console error0, 컴파일 실패 없음. 이번 후속 실행은 진입 확인이며 전체 거래/다음날 경로 재실행은 아니다.
- Play 종료 후 SpriteWorldSandbox clean으로 열고 개인 GUID를 선택했다. Play 시작 씬은 InitScene, runInBackground=false다. 실제 UI/UX와 Player build는 미검증이다.

### 이전 월드 Main 조립 시점의 검증 (현재 Main 상태 아님)

- 기준 `codex/sprite-world-presentation e62fcaf` + 표시 전환 diff. Unity6000.3.18f1, 기존 Editor PID14048.
- EditMode **228/228**: `Temp/TestResults/20260911-130534-3a729387a96f4fc490763a3fb7026422/EditMode.xml`.
- PlayMode **40/40**: `Temp/TestResults/20260911-130731-f17b9d818dbe493788c0521aa80b7e90/PlayMode.xml`. 각각 log 포함, 실패·skip·미완료0.
- 신규 화면비2종 viewport/색상 합성/실제 prefab 경계4건, 실제 큐 identity·별도 대사 수명·pause·전면숨김·재활성1건과 기존 시계 미리보기 회귀를 포함한다. 초기 viewport fixture의 clamp/float 비교2건과 자동 preview 시작시각1건을 수정 후 재실행했다. 기대값을 낮추거나 제품 가드를 우회하지 않았다.
- 실제 Init→Hub→Main·감독관·PreOpen·전면/대기열·기존 slide/topview·거래·마감/정산·NEXT→2일차 재개를 확인했다. 16:9/16:10의 월드와 UI 정렬, City shader 실제 alpha0/.5/1 및 야간 표시를 확인했다. 상세 `Temp/WorldSmoke-Result.md`, 스크린샷 `Temp/WorldSmoke-*.png`.
- 지연 검사 closure의 파괴된 renderer 접근 오류는 검사 코드 오류로 분리했다. 해당 중간 퇴장 표본을 성공 증거로 쓰지 않는다. 최종 제품 Console error0, compileFailedFalse, MainScene/3prefab missing script/reference0, shader message0.
- 개인 선택/화면비/백그라운드 설정·InitScene을 복원했다. 테스트 생성 TMP fallback atlas는 원상 복원했고 사용자 Mulmaru 변경은 보존했다. 실제 조작감·문구 가독성·Player build는 별도 사용자 확인/미실행이다.

## total_merge 감독관·공용 시계 통합 (2026-09-11)

- 기준: `ba368c8` + `29c1ea5`, 병합 커밋 `cb60967` 이후 공용 영업 시각·시계/배경 참조·리로드 자동 저장 제거를 포함한다. 실제 작업 폴더는 `C:/Users/PC/Projects/Cashier`, 브랜치는 `total_merge`다.
- EditMode **224/224**, 실패·skip·미완료0. `Temp/TestResults/20260911-114517-d7cf83ef8d7f403cb6a8015fa0ee1958/EditMode.xml` 및 `.log`.
- 최초 EditMode는224중222통과·2실패(`20260911-114341-4c25c538ff1948ef9d1e8840f6fb1a6c`). 기존 테스트의 DividerBar.barRect와 CustomerPresenter.dialogueText fixture 참조 누락을 보완했고 기대 동작 검사는 유지했다. 기존 배경 테스트는 EditMode의 명시적 preview 설정과 시계 비간섭을 검사한다.
- 초기 PlayMode 두 실행은 각각39중38통과·1실패였다. `114517-d7cf83ef8d7f403cb6a8015fa0ee1958`는 신규 테스트에 감독관 선행 완료 fixture가 적용된 문제여서 기존 Inspector 테스트 분류에 맞췄다. `114729-a3a01256c8944b1cb7eb2b93dcf093e6`는 영업 전 remainingSeconds=0을 마감시각으로 표시하는 실제 결함이었다. 모델 초기화는 유지하고 InspectorEvent/PreOpen의 표시 비율만 시작값으로 수정했다.
- 최종 PlayMode **39/39**, 실패·skip·미완료0. `Temp/TestResults/20260911-115006-ccc6f23dfc2941c9b49b59774d225b60/PlayMode.xml` 및 `.log`. 실제 GameUI prefab에서 감독관 중 시각 유지, 영업 시작09시·절반15시·끝21시, 일시정지와 배경 미리보기의 영업 비간섭을 검증했다.
- 실제 Init→Hub→Main→첫날 감독관 Next→PreOpen→영업 시작→15시→마감21시→거절 거래 정산→다음날 버튼→2일차 감독관15003을 확인했다. 제품 Console Error0, 컴파일 실패 없음. 시각 중간값은 진행 API와 Pause로 고정했고 버튼 listener를 사용한 최소 실행 검증이다. 화면 증거와 사용자 확인 경계는 [작업 기록](work/inspector-events.md)을 따른다.
- 종료 시 InitScene dirty=false, Play/compile=false, background=false, playModeStartScene=null로 복원했다. 자동 TMP fallback atlas 변화만 제거했으며 기존 사용자 폰트는 보존했다. 최종 UI/UX와 Player build는 미검증이다.

## 2일차 감독관 임시 데이터 (2026-09-11)

- `codex/inspector-events ad72b17` + 데이터·테스트 미커밋 변경: EditMode215/215, PlayMode38/38, 실패·skip·미완료0. 증거·보존 범위는 [작업 상태](work/inspector-events.md#2일차-등장-확인-데이터-추가-2026-09-11)를 따른다.
- 실제 CSV15003/Text8180·8181 로드, 설비 없는 2일차 선정, 첫날·3일차 미등장, 대사·퇴장 후 PreOpen, 중복 완료·재생성 시 재등장 없음, 잔액·명성·도덕성 보존을 확인한다. 이전 2일차 직행 테스트는 감독관 완료 단계를 거쳐 기존 기능을 검증한다. 최종 2일차 화면 사용감은 사용자 확인 대상이다.

## 감독관 시스템 (2026-09-10)

- `codex/inspector-events`에서 EditMode215/215, 최종 PlayMode37/37, 실패·skip·미완료0. XML/log 및 초기 실패·수정 이력은 [감독관 명세 9절](INSPECTOR_SYSTEM_DRAFT.md#9-검증-결과-2026-09-10)에 기록한다.
- 첫날/전날 구매 조건·빈 선정 캐시·중복/전날 콜백·퇴장 완료 이력, 실제 CSV·초기 덮개·오류 안내·UI 재생성/동일 root 재활성, 금액·명성·도덕성 미변경 및 기존 회귀를 검증했다.
- 실제 Init→Main→감독관→PreOpen→영업 시작과 색·알파 중간값을 확인했고 제품 Console Error0. 전용 이미지·최종 UI/UX는 사용자 확인 대상이다. Git 통합은 수행하지 않았다.

## Upgrade 통합 (2026-09-10)

- 기준 `total_merge fcf1518` + `origin/Upgrade 935cf93`. EditMode **209/209**, PlayMode **34/34**, 실패·skip·미완료0.
- XML/log: `Temp/TestResults/20260910-163420-6ec527081ca8438ab48ce405d2e0a856/EditMode.xml`, `Temp/TestResults/20260910-163516-af5b02f004c944fe8370a7ffead169bf/PlayMode.xml` 및 같은 이름의 `.log`.
- CSV14종 로더와 지침 FK 공개 경계, 상품16종/외형45종/실사용Sprite53개, 설비11종·단계 구매 알림 예외의 잔액/보유/단계 일관성, 현재가 카드와 일일 유지비·도덕성 결과 보존을 검증했다. Resource54행은 유지한다.
- 초기 Edit 실패: `163144-4cff8d6fa9704c34af3f6dcaff58178a` 208중5실패(신규 UI fixture 초기화·이미지 기대값), `163317-f88ce4f09b0a426088b78dde4d197db1` 209중4실패(EditMode SendMessage assertion). fixture reflection 초기화와 실제 상품 기대값으로 보정했으며 제품 assertion을 무시하지 않았다.
- 실제 Main: `Temp/UpgradeMain-Smoke.txt`. 기본4카드 Sprite/current가격200, 단계1→3, 구매 당일 효과 잠금/다음날3효과 연결, 일일 유지비200·정산·다음날·대기열1명, 제품 Console Error0을 확인했다. 초기 probe의 잘못된 Closing→BeginSorting 호출은 정상 제출 경로로 보정한 뒤 계속 확인했다.
- 실제 도구 드래그·청소기 흡착 감각·자동소팅9앵커 최종 배치 및 UI/UX는 사용자 수동 확인 대상이다. 지침 거래 적용·벌칙과 유지비 오류 후 복구는 미구현/별도 정책이다. 카드4종 표시 제한을 그대로 둔다.
- 승인된 Addressables 변화는 기존 Default Local Group/Datas의 DailyGuidelineData 1개이며 총71entry(54이미지/14CSV/3scene). 새group/label은 없다. 자동 새로고침 hold 및 테스트 시작 씬 임시 변경은 복원한다.

## total_merge 대기열·이미지·도덕성 통합 (2026-09-10)

- EditMode187/187: `Temp/TestResults/20260910-154916-4bd90ea490c04d7c94ddeeb6e0ff7145/EditMode.xml` 및 `.log`.
- 최종 PlayMode34/34: `Temp/TestResults/20260910-160336-90e8c723e12b4bdda3f4df1363320458/PlayMode.xml` 및 `.log`. 실패·skip·미완료0, 실제54Sprite 로드와 명성/성별교대·도덕성·큐/정산 경계 포함.
- 최초 Play 두 실행(155547-b057d3ebc0bb46eca958db1c5b6ac2f9, 155818-1ae3bda81277406592f0c99612608c73)은 각각34중29 SetUp 실패였다. 병합 후 Editor의 오래된 Addressables 상태에 Morality CSV·54이미지 등록이 없었다. dirty group을 `Temp/MainMerge-StaleGroup.asset.txt`에 보관하고 승인된 HEAD 등록을 복원했다. 첫 ForceUpdate 코드는 namespace 오류로 실행되지 않았으며 이후 정확한 import로 실제 메모리70entries/54images/Morality Datas 라벨을 확인했다. 제품 오류 검증을 완화하지 않았다.
- 등록 복원 후160035-104e7ecdd91d4dd994c02cf9490aa3e4는33/34였다. 기존 성별교대 테스트의 1원 수락 가정만 기존 acceptedOffer helper로 수정했다. 가격민감형은 정가만 수락한다는 제품 규칙과 성별교대 assert는 보존했다. 실패 XML·로그도 Temp에 보존한다.
- 실제 Init→Hub→Main, GameUI PreOpen/UsesCustomerQueue=true 확인. `Temp/MainQueue-Smoke.txt`: 높이430, 거래퇴장 중간색/alpha701표본·대기이탈297표본, pause 시 색/alpha/위치/시계 보존. 마감→Settlement 후 waiting/leaving/visuals0 확인. `Temp/MainQueue-Operating.png` 화면 확인, 실제 실행 Console Error0. 이 API 조작 검사는 전체 마우스 사용감이나 다음날 수동 조작 완료를 의미하지 않는다.
- 최종 Play/compile 종료·오류0·MainScene dirty=False. 개인 씬 선택은 Use Main으로 비우고 이전 GUID는 Editor SessionState `MainMerge.PreviousLocal`에 보관했다. start scene은 InitScene, 임시 runInBackground는false로 복원했다. Local 씬·코드 hash는 사전 백업과 동일하다. Main의 추가/수정 YAML 행만 trailing whitespace를 정리했다.

## 상품·외형 이미지 migration (2026-09-10)

- 컴파일 오류 없음. CustomerCsvTests에 실제 CSV 두 FK·45외형·빈값/누락·표시 선택 검사를, GameSessionApiTests에 실제 Sprite54개 ResourceManager 로드를 추가했다.
- 최초 EditMode 시작은 개인 씬 dirty gate로 거부됐다(0건/XML없음). 저장 요청도 안전 검토에서 차단됐으나 직접 승인 작업에서 백업·저장 완료 후 재개했다.
- 최종 EditMode174/174: `Temp/TestResults/20260910-140531-7c3304297493471bb17cf481bc41c3f6/EditMode.xml`. 최종 PlayMode33/33: `Temp/TestResults/20260910-140803-fee27b1d673648d7819b27c78d3be31f/PlayMode.xml` 및 각 로그. 실패·skip·미완료0. 실제 Sprite54개 로드 포함.
- 초기 EditMode 두 실행의 실패는 테스트 가격 공급/정상 Resource 로그 기대 누락이었다. 이후 Play3실패는 기존1프레임 초기화 가정으로, 실제 GameUI 준비까지20초 제한 대기로 수정했다. 제품 계약을 완화하지 않았다. 실패 증거는 `20260910-140305-085bf35bee744f0886f26acfca201591`, `20260910-140411-bebca359348549598a9b3b21c666e561`, `20260910-140531-7c3304297493471bb17cf481bc41c3f6`에 보존한다.
- 최종 compile 오류 없음, Console Error4건은 기대된 알림1/ResourcePool3 실패 주입. 사용자 씬으로 복귀(dirty=False), Play 종료, 개인 playModeStartScene=InitScene 복원. 실제 화면 배치·마우스 사용감은 별도 사용자 확인 대상이다.
- 연결·migration·UI 수동 확인 범위는 [IMAGE_RESOURCE_INTEGRATION.md](IMAGE_RESOURCE_INTEGRATION.md)를 따른다.

## 일일 도덕성 정산 검증 (2026-09-10)

- EditMode 165/165, PlayMode 32/32, 실패·skip·미완료 0. 증거: `Temp/TestResults/20260910-123818-a7254722d2044b398dedfe5636e87a4c/`의 두 XML 및 로그.
- 실제 CSV 방문의 양수·음수 소수점/0/거절, legacy null, 정산 snapshot·일일값 초기화·다음날·거래 없는 날·총누적 유지, 알림 관찰/예외, 일일 decimal overflow 사전 차단을 검사했다. overflow 사례는 누적 필드만 한계로 설정하고 실제 상품·방문 경로를 사용한다.
- 컴파일 완료 후 기존 runner의 assembly 단위 실행을 한 차례 사용했다. 개인 시작 씬을 테스트 동안 임시 해제하고 종료 후 복원했다. UI 조작은 수행하지 않았다.

## 도덕성 거래 연동 검증 (2026-09-10)

- 사용자 인게임 확인 완료. 도덕성 값 변경 시 현재 값과 변화량을 출력하는 로그를 포함한다. 로그 추가 후 컴파일 오류 없음 확인; 아래 자동 테스트 결과는 로그 추가 전 실행이다.
- 최종 EditMode `20260910-115037-ae34232897084aa784c82babd5be055a`: 165/165, 실패·skip·미완료 0. 추가된 PriceSensitive 실제 제출 경계 검사를 포함한다.
- 최초 전체 실행 `20260910-114237-fe402f4d842447a6879f646c92d87897`은 EditMode 164건 중 기존 기대값 3건이 신규 ID·수락 상한과 달라 실패했고 PlayMode는 시작하지 않았다.
- 수정 후 `20260910-114413-594cfc38ff1841faac99cbaa9db07d87`: EditMode 164/164 통과. PlayMode는 옛 1원 수락 가정 3건이 실패했다.
- 제품 규칙을 유지하고 기존 테스트 입력을 성향별 수락가로 수정했다. PriceSensitive 현재가-1/동일/+1 검사는 1/1 통과했다.
- 최종 PlayMode `20260910-114825-735336072f404f3c8bbb12cf1eda7bcd`: 30/30, 실패·skip·미완료 0. 실제 Datas 로더, decimal 누적, 정상 재정 알림 관찰값과 알림 예외 후 거래·잔고·도덕성 동시 보존을 포함한다.
- 상세 계약은 [MORALITY_INTEGRATION.md](MORALITY_INTEGRATION.md)를 따른다. UI 노출은 구현·검증 범위가 아니다.

## 대기열 진행 연결 검증 (2026-09-09)

- `codex/customer-queue-scene`: EditMode 162/162, PlayMode 28/28, 실패·skip·미완료 0.
- 증거: `Temp/TestResults/20260909-161215-f9d249073a0041899dd3ce93677922f5/`의 XML 및 로그. Temp는 Git 제외다.
- 추가 4건은 FIFO·빈 계산대·pause, 긴 프레임 만료 우선·Closing 마지막 거래, 빈 계산대의 만료 방문 배제, 실제 GameUI 옵션·빈 화면 정리·최종 퇴장 후 정산 표시를 검증한다.
- 테스트 중 개인 playModeStartScene을 임시 해제하고 종료 후 복원한다. 개인 씬의 실제 이동·말풍선·배치는 이번 자동 결과로 검증했다고 간주하지 않으며 사용자 확인이 남는다.

Unity Test Framework 1.6.0의 NUnit/Test Runner를 사용한다. UI/UX 배치·문구·버튼·연출은 사용자 수동 확인이며 API 통과와 분리한다.

## 실행 및 결과

컴파일이 끝난 EditMode에서 Window → General → Test Runner의 Cashier.EditMode.Tests/Cashier.PlayMode.Tests를 실행한다. Save Results로 XML을 저장할 수 있다.

PlayMode 실행 전 `EditorSceneManager.playModeStartScene`에 InitScene 또는 개인 씬이 지정되어 있으면 원래 값을 기록하고 테스트 동안 null로 해제한다. 지정된 시작 씬은 Test Runner의 임시 씬 대신 게임을 실행하여 테스트가 진행되지 않을 수 있다. 실행 종료와 활성 job 정리를 확인한 뒤 원래 값을 복원한다. 기존 runner는 이 개인 설정을 자동 변경하지 않는다.

반복 로컬 검증에서 두 모드의 XML·로그 자동 보관과 0건/실패/skip 종료 판정이 필요하면 유일한 셸 진입점을 사용한다.

```powershell
./Tools/Run-Tests.ps1
./Tools/Run-Tests.ps1 -Mode EditMode
./Tools/Run-Tests.ps1 -Mode PlayMode
```

- 기존 Check-CustomerGenerator/Csv/Queue/PriceEvents/RadioTiming 및 ValidateResourcePool 6개 셸은 대응 suite 전환 후 제거했다. UI 전용 Check-CustomerOutcomes/TradeUI/MainSceneIntegration 3개도 제거했고 UI는 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)·[CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md)의 수동 확인 절차를 따른다. API가 UI 검증을 대체했다는 의미가 아니다. 삭제 전 버전은 Git 이력에서 복구할 수 있다.
- Run-Tests.ps1은 테스트 본문을 포함하지 않는다. CashierTestRun.Start 호출과 완료 XML 검증만 한다. 표준 GUI는 자동 모드 순차 실행·고유 경로 로그 보관·셸 실패 종료를 함께 제공하지 않으므로 이 한 개만 유지한다. 새 runner/framework는 추가하지 않았다.
- 자동 결과는 Temp/TestResults/<실행ID>/EditMode.xml 또는 PlayMode.xml과 .log다. 매번 새 ID를 사용한다. Temp는 Git 제외이며 장기 증거는 별도 보관한다.
- total=0, failed>0, skip/미완료, 비정상 루트 결과, 시간초과, XML 읽기 실패는 성공이 아니다. timeout 후 기존 Unity job 종료를 확인하기 전 재실행하지 않는다.
- 사용자 Play·컴파일·미저장 씬·자체 pending·다른 GUI/API job이 있으면 시작 전에 거부한다. 다른 job 조회가 없거나 실패해도 옵션·SessionState·폴더를 변경하기 전에 거부한다.
- Test Framework 1.6.0의 RunFinished는 씬/옵션 cleanup보다 먼저 온다. 설치된 내부 읽기 전용 TestRunnerApi.IsRunActive 및 runInBackground 복원 확인 후 임시 XML을 최종 경로로 이동한다. 패키지 갱신 시 이 의존을 재검토한다. 최종 이동 실패는 경로·예외를 한 번 기록하고 pending을 해제하며 완료 XML이 없으므로 외부 실행은 실패한다.
- PlayMode fixture는 임시 씬에 소유 manager·provider·locator·객체만 생성/해제한다. 사용자 씬·prefs·세이브는 수정하지 않는다. GUI 직접 실행 시 자동 경로 보관은 적용되지 않는다.

## 현재 진행 통합 검증 (2026-09-09)

기준 checkout: codex/equipment-upgrades, total_merge 9b0a55a. 상세 호출 계약·Closing 정책·설비 후속 경계는 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)를 따른다.

- EditMode 123/123, PlayMode 18/18, Failed=0, skip/미완료=0. 증거: Temp/TestResults/20260909-100839-4f7cdc689523421194b222074527f894/{EditMode,PlayMode}.xml 및 .log.
- 기존 GameSessionApiTests 4건에 실제 GameProgress/DayProgress 경유 9건을 추가해 13건이며 ResourcePoolTests 5건은 유지한다. 기존 132개만 실행해 새 진행 통합을 PASS로 주장하지 않았다.
- 추가 사례: 원본 거래 결과/판매 수량/원가 보존·입력 오류 재제출·중복 제출 거부, 지침 snapshot 보존, 정산 false/overflow 실패 후 판정 보존·진행/재접수 차단(2건), 일반일/상납 성공·부족/중복 완료(2건), pause/60초 방송/손님 snapshot, 긴 프레임과 Closing 마지막 거래, 현재가 가격표/해금일/누락 단가·날짜 불일치.
- 지침 검사는 테스트 생성기로 만든 방문을 진행 경계에 주입하며 실제 지침 공급을 제품에 추가하지 않는다. 긴 프레임 검사는 테스트에서 예약 대기값만 고정한다. 제품 난수·CSV·기획수치는 변경하지 않았다.
- DayProgress는 visit.Result.Value를 그대로 TryApplyTransaction에 전달한다(호출부 정적 확인). 경제 집계는 아직 상세 판매/위반을 저장하지 않고 매출·명성 변화만 사용한다. 결과 보존 검사 통과가 원가 차감·지침 벌칙 구현을 뜻하지 않는다.
- API 실행 후 상태·컴파일·Console 확인은 작업 보고에 남긴다. UI/prefab/Scene/Addressables는 수정하지 않았으며 UI/UX 실제조작은 미실행이다. 최초 Unity heartbeat 지연은 컴파일 증거로 쓰지 않고 연결 복구 후 컴파일·실행했다.

## 명세·구현·등록 검사 대응

기준 기획: [문서 - 퍼즐게임](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit)의 9/8 회의록·경제 시스템 구조 개발 문서(2026-09-08 조회, 수정 시각 07:56:55.006Z UTC). 프로젝트 책임자의 후속 합의와 실제 구현 계약을 우선하며 공식 문서는 변경하지 않았다.

| 등록 suite / 로컬 상세 명세 | 실제 API와 확인 사례 | 대조 결과·경계 |
|---|---|---|
| CustomerContractTests / [CUSTOMER_INTEGRATION](CUSTOMER_INTEGRATION.md) | Generate, BeginOffer, SubmitOffer(long, IReadOnlyList<SaleItem>), Result, Depart: 타입 균등/OR 선호/속성/seed/4판정/정가 범위/최종 수량·단가·원가/지침 AND/오류 원자성 | 현재 3차 계약 일치. 가격 공급 1회와 불변 snapshot 검사. 실제 지침 공급·명성·원가 정산은 미연결. 최종 목록 UI 경로는 존재하되 UX는 수동 확인 |
| CustomerCsvTests / [CSV_RULES](CSV_RULES.md), [DATA_RULES](DATA_RULES.md), CUSTOMER_INTEGRATION | 실제 CsvHelper·전용 LoadData·CustomerCatalog.ValidateAndCommit·enum routing, 정상 1건+오류 56건 | 57건. 추가 종류 생성 없음. PK 중복 사례는 실제 정상 행을 복제해 파싱 오류와 구분. Resource 주소 전체 실자산 검사는 아님 |
| CustomerQueueTests / [CUSTOMER_QUEUE_INTEGRATION](CUSTOMER_QUEUE_INTEGRATION.md) | Start/Advance/TryAdd/TakeNext/Stop/GetSpeech: 5초 입장·10명 정원·FIFO·재촉/이탈 1회·pause/close·긴 프레임 | 시간 계약 일치. 위치/말풍선/3초 결과 후 화면 자동 인계는 수동 UI 확인 |
| PriceEventTests / [PRICE_EVENT_INTEGRATION](PRICE_EVENT_INTEGRATION.md) | IsDue/CreateDay/ApplyRadio/GetRadioDelaySeconds: 실제 CSV/FK 준비, 날짜·4채널 조합·seed·FK/가중치 오류·버림/하한/overflow·불변/중복 효과 | 누락됐던 4조합·seed/FK/가중치 오류 보완. 후보가 있는 라디오는 예약되며 별도 발생 확률 상수 없음. 모든 CSV 음성 조합을 전수 검사한 것은 아님 |
| GameSessionApiTests / 가격 이벤트 명세·9/8 회의록·경제 개발 문서 | 실제 Resource/CSV 초기화→InitializeNewGame→EnsureDailyPrices→BeginTradingDay→AdvanceTradingTime→SubmitOffer→TryApplyTransaction→EndTradingDay→CompleteDay, 종료 정리 | 매출 증가/종료 후 거부/날짜 전이·가격 공급 일치. 중복 초기화·잘못된 시간/조기 날짜 완료 및 위 실제 Progress 통합 사례 추가. 경제 전체 suite로 확대하지 않음 |
| ResourcePoolTests / [RESOURCE_POOL_CONTRACT](RESOURCE_POOL_CONTRACT.md) | LoadAssetAsync/Task/callback, Release/ReleaseAll, SimplePool/Manager 생성·prewarm·대여·반환·Clear: 취소·실패·retry·타입/소유권·cleanup | 기존 계약 일치. 임시 provider로 실제 Addressables 경유. 원격 장애·플랫폼·제품 prefab 검증 아님 |
| CashierTestRunTests (실행 도구 안전성) | Start 재진입 거부, SessionState/옵션/출력 경로 미변경 | 자체 pipeline 및 독립 TestRunnerApi 실행 중 각각 확인. 제품 기능 suite 아님 |

### 미검증·기획 불일치·외부 통합 보류

- 경제 문서의 FinanceService.CanAfford/TrySpend/BalanceChanged, MaintenanceService의 성공/부족/순차 회차/MaintenancePaid, LogService의 순서·전후 금액/Dispose 구독해제는 코드에 있지만 등록된 세션 fixture가 전체 계약을 독립 검증하지 않는다. 현재 매출 AddIncome·Query 잔고·종료 거부·session teardown 증거를 경제 전체 PASS로 확장하지 않는다. 추가 경제 suite는 별도 범위다.
- TryApplyTransaction은 거래 ID 중복 제거 API가 아니다. 현 DayProgress가 방문 상태로 중복 전달을 거부하고 접수 실패 latch를 소유한다. UI 버튼의 실제 조작/오류표시는 수동 검증 대상이다.
- 상품 원가는 SoldItems/CostTotal에 기록만 한다. 일일 원가 차감·명성 계산·지침 벌칙은 미연결이다. 명성0~100 등의 구형 예시를 기대값으로 복원하지 않았다.
- 과거 손님 문서의 공통 행복도·무작위 이탈과 최신 보류/제거 기획이 충돌한다. 현 대기열은 성향별 시간 만료 계약만 검사한다. 현재 유효 성향은 Normal/PriceSensitive/Hasty(표시명 성급함)/Wealthy/Poor이며 다섯 타입의 데이터 행을 검사한다.
- 회의록의 라디오 추후 피처 표기와 이미 승인·구현된 기능을 구분한다. 현재 API 회귀 통과는 최종 기획 활성화 승인이나 UI 완성을 뜻하지 않는다.
- MainScene bootstrap/직렬화·레이아웃·폰트·이미지 실자산·버튼/연출·Player build·저장 복원은 이번 자동 API 범위 밖이다.

## Assembly·병합 의존

- Cashier.Runtime + Cashier.Scene.Editor + Cashier.EditMode.Tests/Cashier.PlayMode.Tests 및 각각의 Unity 생성 meta를 함께 반영한다. global namespace와 기존 script GUID를 유지한다. 새 package/vendor 변경 없음.
- LoadingScene의 fade는 DOTween Modules 확장 대신 DLL To/SetTarget/Kill을 사용해 0.3초 OutQuad 및 OnDisable 해제를 유지한다. 이는 assembly 경계에 필요한 제품 수정이며 실제 시각 결과는 사용자 확인 대상이다.
- CustomerSandbox/CustomerSandboxSetup의 기존 추적 경로 삭제, .gitignore/AGENTS/SCENE_WORKFLOW 변경을 함께 반영한다. 실제 Assets/Scripts/Local 코드·meta는 보존하고 stage하지 않는다. 현 실제 UI는 GameUI.prefab의 GameUIController이며 비활성화된 Dev3 파일은 이번 범위에서 정리하지 않았다.
- 3차 제품 계약은 customer_sys 5ad5fa8까지 이미 push됐고, 이 Test Runner 전환·Local 분리·문서 정리는 그 이후 변경이다. 최신 HEAD/remote 여부는 최종 Git 실행 시 재확인한다.
- 다른 branch에 이식할 때 product cost_price 및 성향 disposition_type/preferred_product_idxs/regular_price_min_rate/regular_price_max_rate를 포함한 현재 CSV와 DTO/loader/catalog/방문·TransactionResult를 부분 복사하지 않는다.
- TMP fallback dirty와 stash, 개인 Local 파일, Temp 검사 증거, 의도치 않은 ProjectSettings 변경은 commit 대상이 아니다. 기본 branch merge는 별도 승인·교차 리뷰 대상이다.

## 손님 속성 세 축 검증 (2026-09-09)

- `CustomerAttributes`는 기존 비트 유지+Adult16/Normal32만 추가했다. 성별2×연령3×특수1=6조합이며 Wealthy/Poor는 성향 enum으로만 사용한다.
- Unity 6000.3.18f1, Cashier PID29172, `Tools/Run-Tests.ps1`: EditMode139/139, PlayMode20/20, 실패0·skip/미완료0.
- 증거: `Temp/TestResults/20260909-124924-72e789a824f34e5d80227f188ac3806b/`의 EditMode.xml/PlayMode.xml 및 각각 .log. Git 제외 임시 증거다.
- CustomerContractTests에서6조합 도달·균등성·외형/성향 독립·seed 재현, 부분조건/완전프로필 분리, 미정의bits·동축중복·축누락 및 실제 방문 생성자 거부, Adult/Normal/Child 단독·세 축 AND 지침을 검사했다. 기존 성향 Wealthy도 Normal 속성과 별개이며 금액 효과가 없음을 확인했다.
- 최초 테스트 작성에서 internal CustomerOrderItem 생성자 접근 컴파일 오류를 정상 생성기 결과 재사용으로 수정한 뒤 위 전체 실행이 통과했다. 최종 compiling=false, scriptCompilationFailed=false, Play 종료/InitScene 복귀 확인.
- Console Error3건은 ResourcePoolTests의 기존 실패 주입(LogAssert.Expect)이며 제품 오류와 구분한다. CSV·Scene/prefab·Addressables·Local·저장 데이터는 수정하지 않았고 UI/UX는 미검증이다. 새 저장 migration과 Git 작업은 수행하지 않았다.

## 설비 구매·다음날 해금 검증 (2026-09-09)

- Unity 6000.3.18f1, Cashier PID16200, `Tools/Run-Tests.ps1`: EditMode137/137, PlayMode20/20, 실패0·skip/미완료0. 컴파일 완료 후 실행했다.
- 증거: `Temp/TestResults/20260909-121421-ae5019f5ba46414c872ce55c0161bdf9/`의 EditMode.xml/PlayMode.xml 및 각각 .log. Temp 파일은 Git에 넣지 않는다.
- 신규 FacilityTests14건과 GameSessionApiTests의 실제 CSV·Addressables 로딩/다음날 해금/새 세션 초기화를 포함한다. 처음 실패한 구형 비활성상품 제출 fixture와 영업 전 null 방문 캡처는 테스트 입력·순서만 고친 뒤 재실행했다. 제품 잠금 규칙은 완화하지 않았다.
- 종료 Console Error3건은 ResourcePoolTests의 명시적 실패 주입(LogAssert.Expect)이다. 신규 제품 오류와 구분한다. 컴파일 오류 없음; 화면 조작·실제 씬 전환·설비 구매 UI는 검증하지 않았다.
- 데이터·사용법·배포 묶음은 [FACILITY_INTEGRATION.md](FACILITY_INTEGRATION.md)를 따른다.

## 설비 상점 UI 검증 (2026-09-09)

- EditMode 141/141: `Temp/TestResults/20260909-133440-6d6d6167362148548c3ca3a76ccee187/EditMode.xml`.
- PlayMode 23/23, 실패·skip·미완료 0: `Temp/TestResults/20260909-134450-0c7ecbb9dff14f0597961e0382dcb326/PlayMode.xml` 및 `.log`.
- 최초 Play 실행은 개인 `playModeStartScene=InitScene`으로 실제 Local 게임 씬이 시작되어 180초 timeout, 결과 XML 미생성으로 BLOCKED였다. 해당 job만 CancelTestRun으로 정리하고 idle 확인 후 개인 시작 씬 설정을 임시 해제해 새 runId로 재검증했다. 종료 후 InitScene 설정을 복원했다. 테스트 실행 시 개인 playModeStartScene을 보존→임시 null→종료 후 복원한다. 공유 runner나 제품 흐름은 변경하지 않는다.
- 상점 스냅샷/FK/날짜 상한, 실제 prefab 버튼 구독 수명, 중복 구매 방지, 잔고 변경, 모달 뒤 입력 차단, 다음날 활성화, 결제 알림 예외 후 소유권 보존과 UI 잠금을 검사했다. 기대된 실패 주입 로그는 제품 오류와 구분한다.
- 실제 Local 부트스트랩의 상점 열기·구매·닫기·다음날 활성화도 확인했다. `Temp/FacilityShop-Open.png`, `Temp/FacilityShop-Purchased.png`는 임시 화면 증거이며 Git 제외다. 최종 사용자 UI/UX 확인은 별도다.

## total_merge 설비·명성·MainScene 통합 검증 (2026-09-09)

- 입력: 설비 `65888e1083d3592788210f295bb0d2bdad4d3814`, 명성 `6976218c38b2678f635248a1a5e7c7880e72615a`; 대상 최초 `9b0a55a071d4172ffbf88ff0d6912702e26e73f7`. 설비 fast-forward 후 명성 merge 이력을 보존하며 원본 branch·Local·stash는 변경하지 않는다.
- Unity 6000.3.18f1/Cashier PID29172, 컴파일 오류0. `Temp/TestResults/20260909-143834-2a5a84abea594862a0c5c63ca57594ae/`: EditMode162/162, PlayMode24/24, 실패0·skip/미완료0. 두 XML과 .log 보존. 테스트 중 개인 playModeStartScene을 null로 하고 종료 후 InitScene을 복원했다.
- 최초 통합 EditMode162중 명성21건은 동일 SetUp FK 오류였다. 명성 fixture가 새 FacilityData CSV를 로드하지 않아 product1005→facility12001 참조 검증에 실패했다. 실제 테이블 로드·주입만 보완하고 제품 검증은 완화하지 않았다. 실패 증거 `20260909-143708-86a644876086423b9036d6a9548169a5`도 보존한다.
- 새 복합 테스트는 할인5건+거절1건의 원본 거래/원가/로그 보존, 설비1회차감, 날짜 완료와 명성1회 적용, 다음날 시설 활성 및 GameProgress 재생성 시 명성·로그 보존을 검사한다. 기존 상납 성공/실패·라디오·시설모달·오류주입 회귀도 통과했다.
- 실제 Init→Hub→Main bootstrap에서 UI ready와 manager 각각1개를 확인했다. 버튼 listener와 Progress API로 할인5건 정산→설비12001 구매→닫기→NEXT를 실행: 잔액95005G, 정산명성+10 피드백, 당일미활성/현재명성0, 다음날 elapsed1/명성10/snapshot10/시설활성을 확인했다. 최종 제품 Console Error0.
- 최초 실제 화면 검사에서 정산 명성 텍스트 참조가 없음을 발견해 기존 GameUI에 TMP1개를 연결했다. 자산 연결 후 동일 실제 정산·구매·다음날 경로를 다시 확인했다. `Temp/IntegratedMain-PreOpen.png`, `Temp/IntegratedMain-Facility.png`, `Temp/IntegratedMain-Settlement-Final.png`는 Git 제외 화면 증거다.
- 명성 이미지·명성에 따른 생성비율·지침 벌칙·원가차감·저장 복원·Player build는 미구현/미검증 범위를 유지한다. 전체 기능이라는 표현은 두 입력 branch에서 구현된 기능의 통합이며 보류 기능 신규 구현을 뜻하지 않는다. 실제 마우스/키보드 사용감은 사용자 확인 대상이다.

## 이전 Test Runner 전환 검증 기록 (2026-09-08)

| suite | Passed | Failed | skip/미완료 |
|---|---:|---:|---:|
| CustomerContractTests | 54 | 0 | 0 |
| CustomerCsvTests | 57 | 0 | 0 |
| CustomerQueueTests | 3 | 0 | 0 |
| PriceEventTests | 8 | 0 | 0 |
| CashierTestRunTests | 1 | 0 | 0 |
| GameSessionApiTests | 4 | 0 | 0 |
| ResourcePoolTests | 5 | 0 | 0 |

- EditMode 123/123, PlayMode 9/9: Temp/TestResults/20260908-171209-4c20b8585d4e41e6b3db55c51b578fa4/ 아래 각 XML 및 .log. 기존 기록117/8 이후 누락 사례와 runner guard를 추가해 재실행했다.
- 독립 TestRunnerApi 필터 실행: CashierTestRunTests.RejectStartWhileRunnerActive 1/1. 자체 pipeline과 별개로 진행 중 API job의 중복 시작 차단 확인(GUI 클릭 자체를 수행한 것은 아님).
- 최종 File.Move 실패 주입: 없는 tmp 경로에서 finishAfterCleanup 두 번 호출, FINALIZE_GUARD_PASS logs=1 pending=empty no_final_xml background_restored. 예상 Error 1건을 별도로 발생시켰고 세션 플래그 복원. update 반복 오류 없음.
- CSV 오류 56건과 Play provider/풀 오류 3건은 LogAssert.Expect로 확인한다. 이 예상 로그 및 위 실패 주입 로그가 Console에 남을 수 있으며 제품 오류와 구분한다.
- 이전 전환 중 NUnit 인자 타입/fixture FK·기대 로그·XML 쓰기 경합/cleanup 순서 문제는 수정 후 위 실행이 통과했다. 초기 실패 XML은 기존 Temp 폴더에 보존했다.

## 손님 구성 선택 통합 검증 (2026-09-09)

- `unity-cli editor refresh --compile --ignore-version-mismatch`로 최신 runtime/test assembly 컴파일 완료를 확인했다.
- EditMode 175/175, PlayMode 25/25, 실패·skip 0. 새 selector 명성 가중치·설비 선호·성별 교대, 타입 전용 명성 매핑과 기존 손님·CSV·시설·진행 회귀를 포함한다. 증거: `Temp/TestResults/20260909-customer-spawn-edit/EditMode.xml`, `Temp/TestResults/20260909-customer-spawn/PlayMode.xml`.
- 실제 성별별 이미지 asset 연결과 UI/UX 표현은 범위 밖이며 수동 확인 대기다. 선호 타입 상품의 명성 정산은 `ReputationDispositionRules`가 선호 필드를 참조하지 않는 것으로 고정한다.
## 시민권·엔딩 검증 참조 (2026-09-14)

현재 `codex/ending-revision`의 EditMode252/252·PlayMode54/54, 실제 Main 구매→시민권 부정 엔딩, 중간 실패·미완료 실행과 복원 상태는 [시민권·엔딩 최종 검증](CITIZENSHIP_ENDING.md#2026-09-14-최종-검증)에 기록한다. 위의 과거 실행 결과와 구분한다.

## total_merge 엔딩·Astra 선택 이관 (2026-09-14)

`b63a8c2 + ending-revision55aac7a + Astra21d6982 선택 이관`의 최종 EditMode258/258·PlayMode53/53 및 실제 Main 정산→시민권 Good 전환을 확인했다. 중간 실행 실패와 정확한 XML/log 경로·보존 상태는 [엔딩 통합 검증](CITIZENSHIP_ENDING.md#2026-09-14-total_merge-통합), 프로토타입 적용 범위·참고 씬의 구형 이미지 누락과 화면 검증 한계는 [Astra 이관 기록](DYSTOPIA_RESOURCE_INTEGRATION.md)을 따른다.
