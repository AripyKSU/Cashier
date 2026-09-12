# 딸 대화 시스템

상태: 구조·CSV 등록·API 구현 완료. 2026-09-12에 기존6단계의 점수 경계·대사를 플레이 검증용 초안으로 보완했다. 최종 UI/UX는 사용자 확인 대상.

## 현재 데이터 초안 (2026-09-12)

사용자가 기존6단계를 우선 사용하도록 승인하여 점수 경계를 −120 / −40 / 0 / 40 / 120으로 조정했다.6행·18개 Text ID를 유지하며15문구를 보완했다. 부정3은−120 미만, 부정2는−120 이상−40 미만, 부정1은−40 이상0 미만, 긍정1은0 이상40 미만, 긍정2는40 이상120 미만, 긍정3은120 이상이다. 하한 포함·상한 제외와0점의 긍정1 선택 계약은 유지한다.

기존±10·20은 큰 점수 거래 몇 건으로 강한 반응에 도달하므로 경계를 넓혔다. 명성0 구성·연령 각1/3·하루8회 제시의5,000회 비교에서 가장 강한 반응의 최초 도달 중앙값은 정가 부정3 약25일, 할인 혼합 긍정3 약16일,120% 제시 부정3 약6일이다. 도달 경로만의 중앙값이며 실제 플레이 예측이 아니다. [정책 정의·점수표·곡선·민감도](work/balancing-reference/daughter-dialogue-20260912/draft.md)를 함께 본다.

0점에서는 일상 인사, 단계가 커지면 불안·거리감 또는 안도·신뢰를 표현한다. 게임의 거래를 직접 목격했거나 치료 상태가 바뀌었다고 단정하지 않는다. 도덕성 누적·판정 코드, 날짜별 이미지, 일일 선택·재표시 계약은 변경하지 않았다. 다른 기획 탭의 음·양 각5단계 방향은 이번6단계 초안으로 최종 확정하거나 대체하지 않는다. 실행 검증은 [작업 기록](work/balancing-preparation.md)의 순차 작업4를 따른다.

기획 원문: [딸 대화 시스템](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.qsw5ep3wnjrl).
기준 checkout: total_merge c756bec에서 생성한 codex/daughter-dialogue. 사용자 담당 기능이며 아키텍처 담당이 계약·리뷰, 전담 하위 에이전트 `/root/daughter_dialogue_impl`이 구현을 맡았다. 최종 테스트 복구·표시 폰트 보정·검증은 아키텍처 담당이 이어받았다. 사용자는 별도 작업 브랜치에서 진행하는 조건으로 아래 임시 데이터·공용 ID 예약·Addressables CSV 2개 등록을 승인했다.

## 승인된 동작

- 누적 `GameSessionManager.CurrentMorality`를 정산 시점에 읽는다. 일일 변화량은 선택 기준으로 사용하지 않는다.
- 음수 3구간, 0 이상 3구간. 0점은 긍정 1단계다.
- 구간별 후보 중 동일 확률로 대사 한 문장을 하루 한 번 선택한다. 서로 다른 날의 같은 대사 반복은 허용한다.
- 정산 재표시와 설비 구매·닫기에 따른 화면 갱신에서는 재선택하지 않는다.
- 딸 이미지는 날짜 구간에 따라 선택한다. 정식 이미지가 없으므로 기존 등록 이미지 하나를 임시로 재사용한다.
- 마지막 이미지 구간은 이후 날짜에도 유지한다. 이미지·텍스트는 각각 ResourceData·TextData FK로 연결한다.
- UI 표시만 수행한다. 도덕성·명성·치료비·병세·엔딩 결과는 변경하지 않는다.

## 데이터 계약

### DaughterDialogueData

header: `idx,morality_min,morality_max,text_idxs`

| 컬럼 | 타입 | 계약 |
|---|---|---|
| idx | uint | 필수 PK, 예약된 종류 ID 16 사용 |
| morality_min | decimal? | 포함 하한. 최하위 행만 빈 셀로 무하한 허용 |
| morality_max | decimal? | 미포함 상한. 최상위 행만 빈 셀로 무상한 허용 |
| text_idxs | uint[] | 필수, 원소 1개 이상, 중복 금지, TextData FK, 기존 `_` 배열 converter 사용 |

조건은 `min <= morality < max`이며 없는 한쪽 경계만 비교에서 생략한다. 빈 셀을 0으로 해석하지 않는다.
데이터 로드 시 PK·FK·빈 배열·역전·중복·누락 구간·양 끝 무한 구간·0점 분리 경계를 검증한다. 전체 검증 후 공개하고 실패 시 LogError와 기존 오류 경로를 사용한다. CsvHelper를 수정하거나 우회하지 않는다.

### DaughterAppearanceData

header: `idx,start_day,resource_idx`

| 컬럼 | 타입 | 계약 |
|---|---|---|
| idx | uint | 필수 PK, 예약된 종류 ID 17 사용 |
| start_day | uint | 양수, 중복 금지, 첫 구간은 1일, 사용자가 보는 일차 기준 |
| resource_idx | uint | 필수 ResourceData FK, 기존 등록 Sprite 참조 |

현재 일차 이하인 start_day 중 가장 큰 행을 선택한다. 마지막 구간은 무기한 유지한다.
최초 행이 1일부터 시작하고 값이 양수·고유하면 모든 이후 날짜가 포함된다.

## 선택과 상태 소유권

1. 기존 `DayProgress.tryBeginSettlement`에서 마지막 거래 종료와 경제 정산을 확정한다.
2. 정산 단계 진입 이벤트를 알리기 전에 누적 도덕성과 해당 일차로 `DaughterDialogueService`를 호출한다.
3. 선택 결과는 DayProgress가 날짜 수명 동안 한 번 보관한다. UI가 상태 소유자나 난수 호출자가 되지 않는다.
4. 결과는 조회 전용 값으로 일차·평가 도덕성·선택 행 ID·TextData ID·ResourceData ID를 보관한다. 누적 도덕성의 별도 가변 복제본을 만들지 않는다.
5. `ProgressViewDataFactory`는 이미 선택된 결과를 표시 데이터로 변환한다. `DaughterDialoguePresenter`가 텍스트와 이미지를 표시한다.

기존 DailySettlementResult의 금액 계약과 SettlementStarted 이벤트 인자는 유지한다. 새 독립 manager, 대화 트리, 선택지, 반복 방지 이력, 저장 시스템은 추가하지 않는다.
정산 이후 표현 오류로 경제 정산을 재실행하지 않는다. 필수 데이터 오류는 기존 오류 경로로 보고하고 성공처럼 진행하지 않는다.
리소스 로딩은 기존 ResourceManager를 재사용하고 화면 종료·파괴 시 기존 취소/해제 규칙을 따른다. DayProgress의 정산 준비 실패는 settlementError로 보관하여 같은 경제 정산을 다시 시도하지 않는다. 상태 변경/이벤트 알림은 준비가 성공한 뒤 발생하며, 표시 관찰자의 예외와 준비 실패를 혼동하지 않는다.

## 구현 API와 병합 연결

- `DataTableManager`: 신규 두 테이블을 등록하고 Text/Resource FK를 검사한 후 기존 테이블들과 함께 공개한다. 필수 테이블 누락을 허용하지 않는다.
- `GameSessionManager`: 초기화 시 검증된 행으로 DaughterDialogueService를 생성한다. 내부 `SelectDaughterDialogue(uint day)`는 당시 CurrentMorality를 전달하며, 초기화 실패·세션 파괴 시 서비스를 해제한다.
- `DaughterDialogueService.Select(uint day, decimal morality)`: 정렬된 구간에서 대사와 이미지를 선택하여 `DaughterDialogueResult`를 반환한다. 결과의 `Day`, `Morality`, `DialogueIdx`, `TextIdx`, `ResourceIdx`는 읽기 전용이다.
- `DayProgress.DaughterDialogueResult`: 정산 전 null, 정산 성공 시 날짜 수명 동안 유지되는 결과다. 기존 재정 결과와 SettlementStarted 인자는 유지했다.
- `ProgressViewDataFactory.CreateDaughterDialogueViewData`: 확정된 Text/Resource FK를 문자열과 Sprite로 변환한다. 새 난수를 사용하지 않는다.
- `GameUIController`: 기존 로딩 수명에서 Sprite를 미리 읽고 정산 재표시 때 같은 딸 결과를 전달한다. 필수 daughterDialoguePresenter 참조가 있어야 한다.
- 독립 자산: `Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab`. GameUI의 정산 패널 아래 nested prefab으로 연결했다. Image/TMP의 raycastTarget은 false다.
- 대사 폰트는 기존 `Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset`을 참조한다. Mulmaru에는 임시 대사 일부 한글이 없어 대체했으며 기존 폰트 자산 자체는 수정하지 않는다. 모든 후보 문자열의 글리프 존재를 EditMode에서 검사한다. 이를 위해 기존 EditMode test assembly에 설치된 Unity.TextMeshPro 참조만 추가했다.
- 병합 시 두 CSV와 meta, DataTableType/loader, TextData 18행, 두 Addressables entry, 코드·테스트 및 GameUI의 딸 presenter/nested prefab 연결을 함께 반영한다. MainScene 파일 변경은 없다. MainScene이 GameUI를 참조하면 이 prefab 변경의 영향을 받으므로 최종 통합 시 해당 인스턴스 참조를 다시 검사한다.
- 개인 씬 `Assets/Scenes/Local/SpriteWorldSandbox.unity`에 연결을 준비했다. 개인 설치 도구 `Assets/Scripts/Local/Editor/DaughterDialogueSetup.cs`와 씬은 Git 제외이며 제품 코드가 참조하지 않는다.

## 과거 기록: 최초 승인된 테스트용 데이터

실제 밸런스와 대사는 원문에 없다. 다음은 최초 구현 당시 기능 검증용 초안이며 현재 값은 위2026-09-12 절을 따른다. 출시 기획값으로 간주하지 않는다.

| 구간 | 범위 | 임시 대사 후보 3개 |
|---|---|---|
| 부정 3 | 점수 < -20 | 아빠가 조금 낯설게 느껴져. / 오늘은 혼자 있고 싶어. / 예전처럼 웃어주면 안 돼? |
| 부정 2 | -20 <= 점수 < -10 | 아빠, 무슨 일 있었어? / 요즘은 아빠 표정이 무서워. / 나한테는 솔직하게 말해줘. |
| 부정 1 | -10 <= 점수 < 0 | 오늘 많이 힘들었어? / 아빠가 걱정돼. / 잠깐만 내 옆에 있어줘. |
| 긍정 1 | 0 <= 점수 < 10 | 아빠, 오늘도 다녀왔네. / 같이 조금만 이야기하자. / 오늘 하루는 어땠어? |
| 긍정 2 | 10 <= 점수 < 20 | 아빠랑 이야기하면 마음이 편해. / 오늘은 좋은 꿈을 꿀 것 같아. / 아빠가 웃어서 나도 좋아. |
| 긍정 3 | 20 <= 점수 | 나는 아빠가 자랑스러워. / 내일도 아빠랑 함께하고 싶어. / 아빠 덕분에 오늘은 덜 무서웠어. |

- 이미지 시작일: 1·11·21일. 세 행 모두 기존 ResourceData 4201 (`FemaleCustomer_01`)을 임시 참조한다. 새 이미지·주소 등록 없이 정식 이미지 교체 전까지 유지한다.
- 신규 종류 ID: DaughterDialogue=16, DaughterAppearance=17. 2026-09-11 공용 권위 문서에서 미배정을 재확인하고 두 행을 예약·읽기 검증했다. 다른 탭 본문은 보존했다.
- 행 ID: 16001~16006, 17001~17003. 임시 대사 TextData ID: 8183~8200. 반영 전 실제 데이터 충돌을 확인했다.
- 승인된 Addressables 범위: 기존 Default Local Group에 주소 DaughterDialogueData·DaughterAppearanceData, 기존 Datas 라벨로 CSV 2개만 추가한다. 신규 group·label·이미지는 추가하지 않는다.
- 규칙 근거: WORK_RULES.md 12절의 Addressables 사전 승인, CSV_RULES.md 3절의 외부 권위 목록 예약과 AGENT_REQUEST_GUIDE.md의 미확정 기획 수치 규칙.

## 구현 범위와 검증

- 구현은 별도 기능 branch에서 진행하고 MainScene은 통합 결과를 보존한다. 기존 로컬 씬을 사용해 연결을 확인한다. 다른 폰트 dirty 변경을 보존한다.
- DTO·DataTable은 Commons/Data, 선택 서비스는 기존 Progress 기능 경로, 데이터는 Assets/Datas, 표시는 기존 UI 경로와 분리된 딸 표시 prefab을 사용한다.
- 실제 연결 단계에서 기존 정산 prefab의 최소 연결 지점을 확인하고 개인 씬에서 검증한다. 화면 배치를 이유로 기존 정산 정보를 삭제하지 않는다.
- EditMode: 실제 CSV 파싱/FK, 모든 경계 바로 전·일치·바로 후, 소수 점수, 무한 꼬리, 0점, 후보별 선택, 날짜 구간, 중복·누락·빈 후보 거부.
- 진행 API: 정산 당시 누적값 사용, 일일 누적 초기화 영향 없음, 같은 날 재조회·설비 갱신에 결과 유지, 다음 날 새 선택. 기존 재무·명성 정산 결과 보존.
- 기존 Unity Test Runner 사용. 비동기 리소스 수명은 필요한 PlayMode 검사로 검증한다. UI/UX는 사용자 확인 대상으로 남긴다.
- 초기 신규 EditMode 필터 5/5 통과 후 PlayMode 필터가 개인 playModeStartScene 설정으로 지연되었다. 해당 실행을 실패로 처리하고 활성 job을 취소한 뒤 IsRunActive=false를 확인했다. 최종 검증은 기존 CashierTestRun의 XML·로그 경로를 사용한다.
- 첫 전체 EditMode 229/230 실패는 TextData 구형 행 수182 기대를 승인된200으로 수정했다. 이후 230/230 통과, PlayMode 44/45 실패는 Mulmaru 글리프 누락을 위 기존 폰트 참조 변경으로 해결했다. 글리프 API 검증 추가에 필요한 test assembly 참조 누락도 수정했고 compileFailed=false 확인 후 재실행했다. 상세 증거는 TESTING.md에 기록한다.
- 검증 기준은 c756bec 위의 기능 변경 전체다. 사용자 요청에 따라 작업 브랜치 커밋·푸시 대상으로 정리했으며 통합 병합은 별도 단계다. 실제 커밋·푸시 상태는 Git 이력을 따른다. 기존 사용자 폰트 dirty 변경은 작업 결과에서 제외한다.

### 최종 검증 결과 (2026-09-11)

- 기존 `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 240` 실행: EditMode **230/230**, PlayMode **45/45** 통과. 각각 실패·skip·미완료 0. 증거: `Temp/TestResults/20260911-175315-d81c54e6473249f8b170c72af1a39e98/`의 XML·로그.
- 실제 Init→개인 씬→감독관→영업→수락 거래→정산에서 누적 도덕성 0, TextIdx 8192, ResourceIdx 4201 표시 확인. 설비 상점 열기·닫기 뒤 같은 대사 유지. `Temp/Daughter-Smoke.txt`, `Temp/Daughter-Settlement-Final.png`.
- API suite 이후에는 표시 위치만 보정했다. 딸 패널은 정산 보드 아래, 가로 stretch·높이110·y=-6, 이미지88×110으로 배치했다. 보정 후 실제 표시를 확인했으며 제품 로직은 변경하지 않았다.
- 컴파일 실패 없음, 제품 Console error 0, 로컬 씬 missing script 0. Play 종료 후 SpriteWorldSandbox를 저장하고 InitScene 시작/개인 씬 선택을 유지했다. MainScene 파일과 기존 사용자 Mulmaru 변경은 보존했다.
- 개인 씬에 남은 기존 명성·유지비/설비 버튼 배치 겹침은 이번 딸 패널 변경 범위 밖이다. 최종 가독성·사용감·다른 화면비, Player build와 저장 복원은 미검증이다.

### total_merge 반영 (2026-09-11)

- 원격 최신화 후 c756bec에 e19bd0f를 이력 보존 병합했다. 기획·API·CSV·Addressables 및 프리팹에 추가 변경 없이 문서만 통합 결과로 보완했다.
- MainScene의 기존 GameUI 인스턴스가 딸 패널과 presenter를 상속하므로 Scene 파일 편집이 필요하지 않았다. 실제 Main의 정산·설비 창 재표시에서 대사와 이미지 연결을 확인했다.
- 통합 EditMode230/230·PlayMode45/45 통과. 실행·중단 기록과 증거 경로는 TESTING.md의 total_merge 딸 대화 절을 따른다. 로컬 병합 커밋의 실제 식별자는 Git 이력을 따른다. 원격 push는 별도다.
