# DailyInstruction · Sprite world 병합 준비

## 승인 후 통합 실행 (2026-09-11)

- 준비 이후 사용자가 실제 병합·MainScene 조립을 승인했다. 설계 담당이 두 입력을 no-commit 병합하고 Git/index를 전담하며 구현 담당은 파일·Unity 편집과 검증을 수행한다. 아래 준비 기록은 당시 상태다.
- MainScene에 기존 CustomerWorld prefab을 Canvas 밖에 연결했다. OperatingPanel의 중복 배경/외형 Image와 시간대 UI 컴포넌트, Main의 legacy Image queue/anchor만 제거하고 성별 TMP를 기존 생성 경로로 직렬화했다. GameUI nested Front→Sorting 순서와 Dialogue overrideSorting=false를 반영했다. DailyInstruction의 PreOpen/Settlement child 계층·연결은 유지하되, 실제 Main 검증에서 발견한 지침 위치·버튼 정렬과 부모 override의 가림만 보정했다.
- 통합 결함 보완: DailyAggregationService.ValidateTransaction에서 지침 건수/벌금 overflow를 도덕성·금액·목록 변경 전에 검사한다. CreatePriceListText는 해금 전체가 아니라 당일 가격 snapshot의 PK만 순회한다.
- 정산 결과의 BalanceAfterSettlement는 불변이다. 설비 구매 후 현재 보유금 표시는 별도 현재 잔액을 factory에 전달해 갱신하며 정산 수입·지출·미납·유예 계산은 변경하지 않는다.
- 구형 테스트 fixture를 새 당일 snapshot·Resource FK·최신 CSV 행과 reflection 생성자 인자에 맞췄다. 미납 시 정산 중단 기대는 전액 미납/유예 후 다음날 진행 계약으로 교체했다. 제품 guard나 실제 잔액 갱신 기대는 완화하지 않았다.
- 최종 Edit225/225·Play43/43, 실패·skip·미완료0. 실제 Main의 감독관·일일 지침·거래/벌금·미납·다음날 설비 해금을 확인했다. PreOpen 지침2번 anchor/버튼 override와 정산 명성·시설 버튼의 상세 필드 가림을 최소 직렬화 수정했다. 마지막 배치 변경은 전체 suite 재실행 없이 Main20일차·미납 화면과 참조를 확인했다. XML·화면·초기 실패는 [TESTING](../TESTING.md#dailyinstruction--sprite-world-main-통합-2026-09-11)에 기록한다.
- Play 종료·Main clean·Init 시작·Use Main으로 인계했다. 개인 씬/meta와 사용자 Mulmaru는 준비 백업과 hash 일치 상태로 보존한다. 전체 UX·다른 화면비·Player build는 미확인이다. 구현 담당의 Git/index 조작은 없으며 최종 Git 결과는 설계 담당이 기록한다.

설계 담당은 최종 API·금액 사전 검증·정산 표시 diff와 XML(225/225, 43/43), Main의 지침/정산 화면을 검토했다. Local/meta·사용자 폰트 hash, 보호 설정 불변과 Main clean/compileFailed=false를 다시 확인했다. 두 입력을 부모로 유지하는 total_merge 병합 커밋을 확정하며 푸시는 수행하지 않는다. 실제 커밋 SHA는 Git 이력과 완료 보고를 따른다.

## 준비 당시 기록

- 요청 범위: 현재 작업 폴더에서 `total_merge`로 이동하고 두 브랜치의 병합을 준비한다. 실제 병합·MainScene 조립·커밋·푸시는 이번 준비 단계에서 수행하지 않는다.
- 확인일: 2026-09-11. 사용자의 작업을 위임받은 설계 담당이 준비·검토했다. 다음 구현 담당은 실행 요청 후 지정한다.
- `git fetch origin` 후 고정한 입력:

| 역할 | ref | SHA |
|---|---|---|
| 대상 | total_merge / origin/total_merge | e62fcaf1e1f54364ed406995a2cee4c6fb47ad5f |
| 일일 지침·정산 | origin/DailyInstruction | 99fc83ef36572040d6bc997ee3221705df607d09 |
| 월드 표시·환경 연출 | origin/codex/sprite-world-presentation | b80dfda4c6c14617d077527faa24269348aeb24e |

## 준비 결과

- 현재 checkout은 `total_merge`, 원격 대상과 동일하다. 두 입력 모두 대상 e62fcaf를 포함하며 입력 간 공통 조상도 e62fcaf다.
- `git merge-tree --write-tree --name-only` 계산은 exit 0, 텍스트 충돌 0. 계산 tree는 `47db10f96f5c6939e2725578af66be57386c55fc`다. 객체 계산만 했으며 index·작업 파일에 병합하지 않았다. 컴파일·런타임 성공을 뜻하지 않는다.
- 대상 이후 변경은 DailyInstruction 59파일, Sprite world 98파일이다. 공통 수정은 GameUIController.cs, CUSTOMER_QUEUE_INTEGRATION.md, IMAGE_RESOURCE_INTEGRATION.md, TESTING.md 4개다.
- 양쪽 모두 MainScene 자체의 변경은 없다. Sprite world의 로컬 씬 조립은 Git에 없으므로 코드 병합만으로 MainScene이 월드 표시로 전환되지 않는다.
- Packages·ProjectSettings·Addressables 변경은 양쪽 입력에서 발견되지 않았다. DailyGuidelineData의 기존 주소·라벨은 실제 로드 검증 대상으로 유지한다.

## 병합 참고와 권장 순서

1. 실제 실행 직전 다시 fetch하고 입력 SHA 변경 여부를 확인한다.
2. DailyInstruction의 일일 상품 추첨·지침 생성·손님 구성·가격/명성·거래 위반·미납 정산 계약과 CSV를 함께 반영한다. DailyInstruction의 PreOpenPanel·SettlementPanel 계층/연결을 유지하고 기존 GameUI 부모의 감독관·설비·대기열 연결을 보존한다.
3. Sprite world의 코드·prefab·재질·shader·리소스 이관을 반영한다. GameUIController 자동 병합 결과에서 표시 관찰 API와 새로운 정산/영업 시작 경로가 함께 유지되는지 확인한다.
4. MainScene에 Canvas 밖 CustomerWorld를 연결하고 기존 Image 대기열과 중복 렌더를 제거한다. Counter·대사·성별 텍스트·감독관·초기 가림은 보존한다. SaleSortingUI가 Counter와 대사보다 위에 표시되도록 기존 로컬 검증 사항을 적용한다. 로컬 씬 파일 자체를 병합하지 않는다.
5. DailyInstruction에 포함된 `.idea/.idea.Cashier/.idea/workspace.xml`과 생성된 TMP fallback atlas는 기능 통합에서 제외하는 안이다. 원본 branch를 수정하지 않고 실제 통합 시 명시적으로 처리한다. 기존 사용자 Mulmaru 변경도 커밋 대상에서 제외한다.
6. `Tools/Run-Tests.ps1`로 EditMode/PlayMode 전체 API 회귀를 실행한다. 새 일일 상품 snapshot·정산 DTO 때문에 기존 fixture가 맞지 않으면 제품 계약을 완화하지 않고 fixture를 대조한다.
7. Init→Main에서 감독관→지침→영업→거래/벌금→정산/미납→다음날, 10/20일 지침, 설비 다음날 해금과 월드 입장/이탈·pause·slide·가림을 검증한다. 최종 사용감·Player build는 별도다.

기준 명세는 소스 ref에서 확인했다. 아래 소스 전용 파일은 현재 total_merge checkout에는 아직 없을 수 있다.

- `origin/DailyInstruction:doc/DAILY_GUIDELINE_TOTAL_MERGE_HANDOFF.md`: 작성 기준 380a71e 이후 최신 99fc83e까지의 실제 diff를 함께 검토해야 한다. 과거 증거는 지침 테스트 4/4이며 전체 통합 검증이 아니다.
- `origin/codex/sprite-world-presentation:doc/work/sprite-world-presentation.md`, `doc/MAINSCENE_INTEGRATION.md`, `doc/DYSTOPIA_RESOURCE_INTEGRATION.md`: 월드 조립·UI 보존·선택 이관 경계.
- Sprite world의 기존 증거는 EditMode 230/230·PlayMode 41/41이다. 준비 단계에서는 재실행하지 않았으며 새 통합 결과의 통과 근거로 대체하지 않는다.

## 로컬 보존·다음 시작점

- Unity는 Play/compile=false, 저장된 InitScene, Prefab stage 없음인 상태에서 전환했다. 준비 중 씬 저장·조립은 하지 않는다.
- `UserSettings/LocalBackups/MergePreparation-20260911-155907`에 SpriteWorldSandbox 씬/meta와 사용자 Mulmaru font를 해시 대조 백업했다. 기존 stash 2개를 유지했다.
- 개인 씬/meta는 Git 제외로 파일을 유지한다. total_merge에는 아직 월드 코드·prefab이 없으므로 병합 전 개인 씬을 열어 저장하지 않는다.
- 실제 내용이 있는 기존 미커밋 변경은 Mulmaru font다. Main/meta·CustomerPresenter·SaleSortingPrefabSetup·TMP fallback은 status에 M으로 보이지만 확인 시 내용 diff는 0이었다. 상태 표시만 보고 복원·stage하지 않는다.
- 후속 실행 범위는 두 branch의 통합과 MainScene 조립이다. 이 문서는 준비 결과이며 실행/푸시 완료 기록이 아니다.
