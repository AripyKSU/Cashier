# 감독관 구현과 담당 기능 대조 상태

- 기준: 2026-09-11, `codex/inspector-events`, 구현 `6115ec0`, 공통 문서 통합 `50155f1`.
- 담당: 사용자 담당 범위를 위임받은 설계 담당이 Git·문서·최종 리뷰를 수행했다. 감독관 기존 프로그래머의 감사는 별도 사용자 작업 전환으로 미수행되어, 설계 담당과 손님·도덕성 조사 담당이 감독관 대조까지 완료했다.
- 기능 계약: [감독관 명세](../INSPECTOR_SYSTEM_DRAFT.md). 전체 대조: [담당 기능 감사](../FEATURE_CONTRACT_AUDIT.md).

## 완료한 작업

- 공통 지침을 `doc/`로 모으고 Codex·Claude Code·Antigravity 입구를 연결했다. `d8ea9ef`를 `total_merge`에 커밋·푸시했다.
- 원격 최신화를 확인한 후 `origin/total_merge`를 감독관 브랜치로 병합했다(`50155f1`, 이후 별도 사용자 요청을 받은 기존 담당이 원격 반영). 기능 구현 `6115ec0`을 유지하며 공통 문서 구조를 가져왔다.
- 감독관은 첫날 소개와 가게 3단계 구매 다음날의 목표 공개 대화, 독립 InspectorPanel prefab, 검정·알파 입퇴장과 초기 준비 덮개를 구현했다. 실제 징수·명성·도덕성 변경은 하지 않는다.
- 손님·설비·도덕성·대기열·감독관의 명세, 호출 경로와 기존 테스트를 대조하고 확인된 문서 차이를 갱신했다.

## 남은 작업과 검증 경계

- 2026-09-11 사용자 확인: "감독관의 대화 다이얼로그는 확인했어." 대화 다이얼로그는 사용자 확인 완료로 기록한다. 확인한 이벤트·날짜의 상세 범위는 지정되지 않았으므로 모든 등장 조건이나 전체 UI/UX 승인으로 확대하지 않는다.
- 2026-09-11 추가 사용자 확인: "fade, 화면 가림 확인". 감독관 입퇴장 페이드와 초기 화면 가림도 사용자 확인 완료다.
- 감사의 미해결 항목을 기준으로 수정 범위를 정한다. 이번 작업에서 제품 코드·CSV·prefab·Scene을 수정하거나 새 테스트를 실행하지 않았다.
- 감독관 기존 실행 결과는 기능 명세 9절의 2026-09-10 기록이다. 이번 통합은 문서 변경이며 그 기록을 현재 브랜치의 새 실행 결과로 취급하지 않는다.
- 대화 다이얼로그·입퇴장 페이드·초기 화면 가림의 사용자 확인을 완료했다. 전용 감독관 이미지, 영업 중 개입, 저장·복원과 실제 상납 기능은 구현 범위 밖이다.
- `total_merge`와 `master`로 감독관 기능을 역병합하지 않았다. 이후 통합 시 [병합 규칙](../BRANCH_INTEGRATION_RULES.md)과 실제 diff를 다시 확인한다.
- Git 제외 개인 씬·코드와 감사 중 새로 발생한 `Assets/Fonts/Dystopia/Mulmaru SDF.asset` 변경을 보존했다. 시작 시 stash는 3개, 마지막 조회는 2개였으며 이 감사에서는 stash 적용·삭제를 수행하지 않았다. Unity Editor는 이번 감사에서 조작하지 않았다. 문서 전용 검증은 상대 링크·파일 대응·`git diff --check`이며 최종 Git 상태는 이 기록을 포함한 커밋 이력으로 확인한다.

이 파일은 미해결 감사 항목을 포함하는 작업 상태다. 완료 후에도 영구 기능 계약과 검증 기록은 유지하고, 작업 상태 기록 정리는 공통 규칙을 따른다.

## 2일차 등장 확인 데이터 추가 (2026-09-11)

- 사용자 요청: 2일차 등장 항목과 등장 확인용 임시 대사. 계약은 [감독관 명세 10절](../INSPECTOR_SYSTEM_DRAFT.md#10-2일차-등장-확인-데이터-2026-09-11)을 따른다.
- 작업 단위: InspectorEventData15003과 Text8180/8181, 실제 CSV·진행 테스트 기대값, 관련 명세. 기존 첫날/최종 확장 이벤트와 제품 진행 로직은 유지한다.
- 확인 방법: 데이터를 다시 읽는 새 세션에서 1일차 정산 완료 → 2일차 영업 전 진입. 임시 한 줄 대사를 확인하고 퇴장 후 기존 영업 전 화면으로 진행한다.
- 기존 사용자 확인은 앞선 두 이벤트 구현에 대한 기록이다. 새 2일차 항목의 화면 확인은 별도다.
- 완료: InspectorEventData15003과 Text8180/8181 추가, 기존 감독관2행·Text179행 값 보존. 실제 CSV 테스트의 3행/2일차 조건과 기존 Text 행 수 기대값을 갱신했다. 기존 진행 테스트에는 2일차 감독관 완료 선행 단계를 적용하고 실제 1→2→3일차·반복 없음·무설비·금전/명성/도덕성 미변경 회귀를 추가했다.
- 검증: 기존 Unity6000.3.18f1 Cashier Editor에서 컴파일 오류0 확인 후 EditMode215/215, PlayMode38/38 통과. 실패·skip·미완료0. 기준 `ad72b17` + 이번 데이터·테스트 미커밋 변경. XML/log는 `Temp/TestResults/20260911-102132-fd222054806e47daa5fc2cfa6f1a3079/`의 EditMode·PlayMode 파일이다. Temp는 Git 제외이므로 다른 작업자는 같은 테스트를 재실행한다.
- 설계 검토: 실제 diff, 기존 CSV 값 보존, 추가 PK/FK·스키마, 데이터 카탈로그 원문·해시, Markdown 참조를 확인했다. 제품 코드·Addressables·Scene·prefab 변경은 이번 작업에 없다. 새 2일차 대사의 UI/UX는 사용자 확인 대상이다. 커밋·푸시는 수행하지 않았다.
- 종료 확인: 테스트 각 모드1회. 최종 컴파일 실패 없음, InitScene 미저장 변경 없음·Play 종료. Console6건은 기대 실패 주입 로그로 분류했으며 별도 실화면 실행은 하지 않았다. 테스트가 자동 생성한 TMP fallback 변경1개를 복원했고, 기존 Mulmaru·GameUI/OperatingPanel/PreOpenPanel 변경은 SHA-256 전후 일치로 보존을 확인했다.

## total_merge 실제 병합과 시간 계약 정리 (2026-09-11)

- 사용자 승인: 최신 total_merge에서 감독관 병합 및 공용 영업 시각 상수·시계/배경 연결 제안까지 실행.
- 고정 기준: 대상 `ba368c80c8208ac7501d9d8820063e29203b8d5a`, 입력 `29c1ea58dd719f5a4fce8760d57c87c8d8e220d9`, 공통 조상 `d8ea9ef8a6dc5c32c87f0882f73c104c81e593b4`.
- 사용자의 후속 지시에 따라 **기존 `C:/Users/PC/Projects/Cashier` 폴더를 total_merge로 전환**했다. 별도 worktree의 병합 변경39파일을 해시 검증 백업 후 기존 폴더에 이관했고, 별도 worktree는 detached 상태로 남겼다. 준비용 codex/inspector-total-merge 브랜치는 사용하지 않는다.
- 전환 전 미커밋5파일은 `UserSettings/LocalBackups/InspectorMerge-20260911/`에 원본5개·SHA-256·original.patch를 보존했다(Temp 작업 백업에서 해시 검증 복사, Git 제외). 폰트 수정은 working tree에도 복원했다. 옛 GameUI/OperatingPanel 직렬화 수정본은 최신 UI를 덮어쓰지 않고 백업에 유지한다. PreOpenPanel은 줄 끝 공백 외 의미 차이가 없음을 확인했다.
- 이관 후 다른 작업 경로에서 생성된 병합 커밋 `cb609677175071e3ccb6df4092f5dfaaea3cc3a2`를 확인했다. 부모는 위 두 입력이며 이력을 변경하지 않고 공통 시계와 UI 연결의 미완료 변경·검증을 이어간다.
- 구현 담당: 기존 프로그래머가 현재 폴더의 C#·자산·테스트·Unity 검증을 맡고 설계 담당이 문서·diff·계약 검토와 Git을 맡는다. 기존 Unity Editor PID14048를 사용하며 전환 전 InitScene 미저장 변경 없음·Play 종료·PrefabStage 없음 확인. 전환 중 임시 import/reload 잠금은 해제했다. 개인 씬은 변경하지 않는다.
- 적용 계약은 [MainScene 통합](../MAINSCENE_INTEGRATION.md#감독관공용-영업-시각-통합-2026-09-11). 최신 UI를 기준으로 충돌 두 파일을 해결하고 BusinessHours의09~21시를 공유한다. 실제 영업 기본30초·감독관 조건/데이터는 유지한다.
- 보호 변경: 신규 package·ProjectSettings·Addressables 등록·원본 아트·폰트 변경은 하지 않는다. 기존 승인 InspectorEventData address/Datas entry 병합분만 포함한다.
- 시작 정적 검사: 감독관 신규meta9개와 최신 대상의 GUID 충돌0. 통합 checkout 전체 meta GUID 중복0, Mabinogi 폰트/InspectorPanel GUID 존재. 감독관3행·Text181행 PK고유. 이 결과는 실행 검증을 대신하지 않는다.
- 완료 상태: 현재 폴더의 total_merge에서 충돌 해결·공용 시계 연결·통합 검증 완료. 앞 절의 source 결과와 구분해 최종 EditMode224/224, PlayMode39/39(실패·skip·미완료0)를 확인했다. XML/log 경로와 초기 실패 이력은 [TESTING.md](../TESTING.md#total_merge-감독관공용-시계-통합-2026-09-11)에 기록한다. 원격 push는 이 작업에서 수행하지 않는다.

- 통합 중 확인된 결함: `[InitializeOnLoad] AutoSaleSortingPrefabUpdater`가 리로드 직후 두 prefab을 저장하면서 GameUI의 keypad 참조14개 등이 유실되었다. 저장 전 파일은 Git/Temp에 보존돼 있으며 자동 updater만 제거하고 최신 참조를 복구한다. 수동 메뉴는 유지하고 회복 후의 컴파일·참조·실행 결과를 별도로 검증한다.

- TimeOfDay의 기존 런타임 생성 레이어와 참조를 OperatingPanel에 직렬화한다. 기존 `AutoResolveReferences` 배치·리소스를 사용하며 새 아트나 새 연출 규칙은 추가하지 않는다. Editor 전용 리소스 탐색을 Player 런타임에 의존하지 않도록 기존 설정을 자산에 보관한다.

- 실제 MainScene 검증: Init→Hub→Main/manager1개/UI ready→Inspector15001 Next→PreOpen540→OpenBusiness 버튼/30초·540→15초 경과/900·배경15시→Closing1260·배경21시→거절거래 정산/잔액99800→다음날 버튼/day2 Inspector15003·시계540. 제품 Console Error0, 컴파일 실패 없음.
- 화면 증거: `Temp/InspectorMerge-{Day1,PreOpen,Open0900,Half1500,End2100,Settlement,Day2}.png`. 버튼 listener와 진행 API 기반 smoke이며 09/15시 캡처는 입장 직후 Pause하여 손님·상자 fade가 아직 진행하지 않은 표본이다. 이후 Resume/Closing 캡처에는 손님·상자가 표시된다. 수동 마우스 조작·드래그·최종 배치/연출과 Player build는 미검증이다.
- 최종 보호/리뷰: Unity InitScene dirty=false, Play/compile=false, background=false, 시작 씬 override 없음·개인 선택 복원. 테스트가 만든 TMP fallback atlas 5줄 변경만 제거했고 사용자 Mulmaru 해시는 전환 전과 일치한다. 최신 keypad15/15·front source5638334816923447982·button2288817115886397551·시계/배경 연결, missing script/reference0, meta GUID중복0, CSV source대비 차이0을 확인했다. 신규 Addressables 등록·패키지·ProjectSettings 변경은 추가하지 않았다.
- 승인 범위 구현은 병합 커밋 이후 별도 로컬 후속 커밋으로 보관한다. 이력 재작성·원격 push는 하지 않으며 사용자 원본 백업은 Git 제외 상태로 유지한다.
