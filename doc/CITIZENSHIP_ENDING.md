# 시민권·엔딩 구현 및 플레이 테스트 안내

2026-09-13. 기준 `total_merge 480af457efc440839ed101fb753c624c943fce14`, 작업 브랜치 `codex/citizenship-ending`. 사용자가 설계 초안의 구현을 승인하고 손님 외형 분류보다 시민권·엔딩을 먼저 진행하도록 선택했다. 이 문서가 현재 기능 계약이다. 결정 근거와 구현 전 제안은 [설계 기록](work/citizenship-ending.md)에 보존한다.

## 구현 현황 (2026-09-13)

- 기존 설비 상점에 시민권 `Facility 12012`를 추가했다. 가격은 **1,000,000G**, 요구 가게 단계1, 정산 중 1회 구매, 결제 즉시 주인공과 딸의 자격을 보유한다. 일반 설비의 익일 활성 규칙은 유지한다. 기존 상점 목록에 Viewport/RectMask2D를 연결해 스크롤한 행이 제목·가격 안내를 가리지 않게 했다.
- **31일차 정산 중에도 구매 가능**하다. 그날은 다음날 버튼 대신 ‘최종 확인’을 제공하고 미보유자는 구매 마감 확인창에서 정산으로 돌아갈 수 있다. 실제 구매 가능 여부는 세션의 정산 상태·미납 판정·소유·잔액으로 검사한다.
- 미납 유예 만료가 실제 `Failed` 상태와 기존 실패 패널로 이어진다. 1~30일에는 시민권과 무관하게 실패한다. 31일 정산 진입 전에 보유했으면 기본 정책에서 실패만 면제하며 비용과 미납 기록을 보존한다. 그 정산에서 새로 산 시민권은 사전 보유로 소급되지 않는다.
- 최종 확인에서 소유하면 Good, 미소유하면 Bad 결과를 고정한다. 마지막 명성은 한 번 적용하며 32일을 생성하지 않는다. 고정된 결과에는 종료일·시민권·잔액·명성·도덕성이 포함된다.
- `GoodEndingScene` / `BadEndingScene`은 공용 `EndingPanel`로 각4페이지를 표시한다. 대사는 페이지당3줄, 다음 버튼 또는 Enter로 진행하며 연속 입력을 제한한다. 현재 모든 페이지는 기존 `MidBackground.png`를 임시로 참조하고 화면에도 임시 이미지임을 표시한다.
- 마지막에는 결과 요약과 **새 게임**을 표시한다. 종료 화면의 새 게임 버튼은 **HubScene 메뉴로 복귀**한다. 실패 패널의 같은 버튼도 Hub로 돌아간다. Hub의 **새 게임**을 선택해야 Init의 기존 부트·데이터 준비가 실행되고 시민권·미납 기록·종료 결과를 초기화한 뒤 게임에 진입한다. Init은 Build Settings 시작 씬이며 Addressables에 중복 등록하지 않는다.
- 씬 로딩 실패 시 보존된 결과로 재시도하고, 페이지 로딩 실패 시 해당 화면에서 재시도한다. 저장파일 복원은 이번 범위에 포함되지 않는다.
- 감독관 Text8152의 50억 안내는 상점에서 실제 금액·마감일을 확인하도록 수정했다. 가격은 시설 CSV가 소유한다.

### Hub 메뉴와 카메라 (2026-09-13)

- 최초 실행: Init → Loading → Hub 메뉴. Hub는 자동으로 게임을 시작하지 않는다.
- 종료 화면의 새 게임: Ending/Failure → Loading → Hub. 메뉴에 돌아오기만 해서는 종료 결과나 현재 세션을 초기화하지 않는다.
- 메뉴의 새 게임: Hub → Loading → Init(기존 정책으로 새 세션 초기화) → Loading → Main/선택된 개인 씬. 이 새 게임 요청은 한 번만 소비되며 다음 메뉴 방문은 자동 진행하지 않는다.
- 메뉴의 끝내기: Player에서는 게임 애플리케이션 종료, Editor에서는 Play 종료. 컴퓨터 종료는 수행하지 않는다.
- GoodEndingScene·BadEndingScene에 활성 Main Camera와 AudioListener를 추가했다. 기존 Screen Space Overlay UI와 페이지 구성은 유지한다. Hub의 기존 카메라는 유지한다.

**이어하기는 문서상 후속 사양이다.** 현재 통합 게임에는 저장·불러오기 기능이 없으므로 메뉴에 이어하기를 표시하지 않는다. 저장 기능을 구현할 때 **불러올 수 있는 유효한 저장 데이터가 있는 경우에만** 이어하기 버튼을 표시하고, 저장 데이터가 없거나 유효하지 않으면 숨긴다. 현재 메모리의 세션·엔딩 결과를 저장 데이터로 취급하지 않으며, 이번 작업에서는 저장 파일 검사·생성·복원·삭제 기능을 추가하지 않는다.

### 데이터·구현 위치

| 항목 | 현재 연결 |
|---|---|
| 시민권 | `Assets/Datas/FacilityData.csv` 12012, `FacilityUpgradeKind.Citizenship=4` |
| 엔딩 페이지 | `Assets/Datas/EndingPageData.csv`, 종류18, PK18001~18008, Good=2 / Bad=3 |
| 페이지 컬럼 | `idx,ending_kind,page_order,text_idx,speaker_nameidx,background_resource_idx` |
| 문구 | `TextData` 8231~8234 이름·화자, 8240~8247 대사; 전체242행 |
| 배경 | `ResourceData` 4255 → `MidBackground`; 감독관 전용4256 추가 후 전체56행 |
| 로더 검증 | 양쪽 엔딩 필수, 페이지1부터 연속·중복 금지, Text/Resource FK 검증 후 공개 |
| 보유·정산 기록 | `GameSessionManager`, `FacilityService` |
| 종료 판정·최종 명성 | `GameProgress`, `GameEndingResult` |
| 화면 | `DailySettlementPresenter`, `GameUIController`, `EndingPresenter`, `NewGameButton` |
| 생성 자산 | `Assets/Prefabs/Ending/EndingPanel.prefab`, 두 엔딩 씬; 기존 정산·실패·로딩 UI 확장 |

종류18과 PK18001~18008은 권위 Google 기획서의 CSV 종류 탭(`t.ccpln6m1g4kv`)에 등록한 후 사용했다. 신규 CSV는 기존 Addressables `Datas` 라벨로 로딩하며 두 엔딩 씬과 임시 배경도 기존 그룹에 등록했다.

### 정책 변경과 사람 검토

정책은 `InitScene` 컴포넌트의 `Exempt Final Day Preowned Citizenship` 체크 값 하나로 새 세션 시작에 고정된다. Inspector에서 변경한 뒤 새 게임을 시작한다. 기본 `true`는 최종일 사전 보유 예외, `false`는 모든 날짜의 미납 우선이다. 양쪽 경계 사례를 자동 검사한다.

사람 플레이 테스트에서는 다음을 확인한다.

1. 시민권의 가격·부족액·마감일을 이해하고 일반 설비 투자와 비교할 수 있는가.
2. 31일 정산의 ‘최종 확인’과 미구매 경고에서 상점으로 돌아가는 흐름이 명확한가.
3. 최종일 사전 보유자의 미납 종료 면제가 납득되는가. 실제 미납금 자체는 면제되지 않는 안내가 전달되는가.
4. 두 엔딩의 3줄 대사, 글자 크기·줄바꿈·페이지 입력·감정 흐름이 적절한가. 이미지는 임시다.
5. 조기 구매 후 운영비 부족과 마지막 날 구매 전략을 비교해 가격100만G 및 예외 정책을 유지할지 결정한다.

31일 일반 설비 구매 비활성화, 영구 저장, 최종 엔딩 아트, 도덕성별 대사 변형은 후속 검토 항목이다. 손님 외형45종은 후속 사용자 요청으로 [표시 이름을 정리](CUSTOMER_APPEARANCE_CLASSIFICATION.md)했다. 성별·연령은 이름 정리 참고에만 사용하며 외형 데이터나 생성 조건으로 고정하지 않는다. 기존 상품·손님 밸런싱 값은 시민권 구현에서 변경하지 않았다.

### 검증 기록

#### 최종 통합 전 확인 (2026-09-13)

- 최종 코드의 EditMode **249/249 통과**, 실패·skip·미완료0: `Temp/TestResults/citizenship-pre-merge-01/EditMode.xml`. PlayMode는 아래 Hub 메뉴·엔딩 카메라 검증의 **53/53 통과** 결과를 재사용한다.
- Unity는 컴파일 오류 없이 Play 종료·InitScene 상태로 복원됐다. 패키지 설치, 개인 프로젝트 설정과 동적 폰트 캐시 변경은 기능 커밋에서 제외하고 로컬에 보존한다.
- 통합 대상은 `codex/citizenship-ending` → `total_merge`이며 최종 PR은 `total_merge` → `master`다. PR에는 기존 밸런싱·감독관·딸 대화·일일 지침 통합도 포함된다. 사람의 UX 검토, Player 빌드와 저장 기능은 이번 검증 범위에 포함하지 않는다.

#### Hub 메뉴·엔딩 카메라 검증 (2026-09-13)

- 최종 PlayMode **53/53 통과**, 실패·skip·미완료0: `Temp/TestResults/hub-menu-ending-cameras-02/PlayMode.xml`. 초기 `hub-menu-ending-cameras-01`은52/53으로 기존 거래 테스트가 무작위 당일 진열에 없는 상품1001을 조회해 실패했다. 테스트 상품을 실제 당일 가격 목록에서 선택하도록 수정했으며 거래·중복 제출 기대값은 유지했다.
- 실제 Init→Hub 메뉴 대기, Hub 새 게임 중복 입력 차단→Main 1일차, 굿·배드 각각4페이지→종료 화면 새 게임→Hub 복귀를 확인했다. Hub 방문 시 기존 종료 결과를 보존하고, 이후 새 게임을 선택하면 시민권·미납·결과가 초기화되며 세션 manager는1개다.
- 두 엔딩에서 활성 카메라1개와 실제 화면 출력을 확인했고 굿 엔딩의 AudioListener도1개임을 확인했다. Hub의 끝내기 버튼으로 Editor Play가 종료되는 것을 확인했다. Player 실행 파일의 종료·다른 화면비·저장/이어하기는 검증하지 않았다.
- 화면 증거: `Temp/hub-menu.png`, `Temp/good-ending-camera.png`, `Temp/bad-ending-camera.png`. 버튼 리스너와 제품 진행 API를 이용한 검증이며 사람의 전체 마우스·키보드 UX 검토를 대신하지 않는다.
- 실제 실행 Console Error0, 최종 compileFailed=false, 테스트 종료 후 InitScene/Play 종료 상태다. 세 씬의 카메라·fileID·Hub 필드 연결을 검사했고 InitScene·MainScene 자산은 변경하지 않았다. `git diff --check` 통과. 이번 변경에서 커밋·푸시는 수행하지 않았다.

#### 임시 테스트 자금 버튼 (2026-09-13)

- InitScene부터 Play한 뒤 Hub의 새 게임을 누르면 게임 화면 왼쪽 위에 `TEST +100,000 G`가 표시된다. 클릭할 때마다10만G를 추가하며, 31일 정산과 설비 상점에서도 사용할 수 있다. 지급 후 설비 상점에서 시민권을 직접 구매하고 최종 확인을 진행한다.
- 일일 지침 UI의 기존 10·20일차 이동 버튼 옆에 **30일차 이동**을 추가했다. 영업 전 상태에서 경과일29/표시일30으로 이동하며, 기존 날짜 점프 규칙을 사용한다. 일반 배포 빌드에서는 세 날짜 버튼 모두 숨긴다.
- `GameUIController`의 `UNITY_EDITOR || DEVELOPMENT_BUILD` 블록에만 존재하는 임시 버튼이다. 일반 배포 빌드에는 포함하지 않으며 테스트 종료 시 해당 자금 버튼 블록을 제거한다. 기존 날짜 점프 버튼과 별개다.
- 기존 FinanceService에 `FinanceChangeReason.None`으로 잔액만 추가한다. 일일 매출·거래 횟수·명성·도덕성은 변경하지 않으며, 이미 확정된 미납·정산·엔딩 결과를 다시 계산하지 않는다. 종료 결과 확정 후에는 지급할 수 없다.
- 이전 100만G 버튼 검증: 실제 Init→Main에서 지급 핸들러2회 호출로 정확히200만G 증가와 매출·명성·도덕성 불변을 확인했다. 31일 정산 및 열린 상점의 표시 잔액 즉시 갱신, 시민권100만G 구매도 확인했다. 컴파일 성공·제품 Console Error0. 버튼 화면은 확인했으며 테스트 호출은 핸들러와 기존 구매 경로를 사용했다. 이번 버튼 변경에서 자동 전체 테스트·배포 빌드는 실행하지 않았다.

- 30일차·10만G 변경 검증: 실제 Main의 `30일차 이동` 버튼 리스너로 30일차 PreOpen/경과일29를 확인했다. 자금 핸들러1회 호출은 잔액만100,000G 증가했고 매출·명성·도덕성은 유지됐다. 화면에서 날짜 버튼3개와 자금 버튼 표시를 확인했고 Console Error0이다. 증거: `Temp/day30-test-controls.png`. 자동 전체 테스트는 재실행하지 않았다.

#### 기존 시민권·엔딩 전체 검증

- Unity 컴파일 통과.
- EditMode `citizenship-ending-edit-04`: **249/249 통과**, `Temp/TestResults/citizenship-ending-edit-04/EditMode.xml`.
- PlayMode `citizenship-ending-play-03`: **53/53 통과**, `Temp/TestResults/citizenship-ending-play-03/PlayMode.xml`. 부트 복귀·스크롤 클리핑·실패 안내·엔딩 전환 재시도를 포함한 최종 코드로 실행했다. 실패·skip·미완료0이다.
- 실제 Unity 실행: Init→Main, 31일 정산 구매 버튼 중복 입력에도 100만G만 차감(1,997,800→997,800G), GoodEndingScene 로드, 미구매 최종 확인 취소→상점 복귀→확인→BadEndingScene 로드, 각4페이지 진행을 확인했다. 엔딩→새 게임 및 30일 미납 게임오버→새 게임에서 Main 1일차·시민권 미보유·종료 결과 없음·세션1개·Console Error0을 확인했다.
- 화면 증거: `Temp/citizenship-good.png`, `citizenship-bad.png`, `citizenship-confirm.png`, `citizenship-shop-fixed.png`, `citizenship-gameover-fixed.png`. 최종 실패 화면에서 제목·3줄 문구·새 게임 버튼이 잘리지 않는 것을 확인했다. 자동 API/버튼 리스너와 화면 검증이며 실제31일 정상 플레이 또는 사람의 UX 승인을 대신하지 않는다.
- 가격의 도달 난이도, 조기 구매 후 운영 지속성, 미납 예외 정책 유지 여부, 최종 아트와 대사 감정선은 사람 플레이 테스트가 남아 있다. 배포 빌드와 로딩 장애 주입 테스트는 미실행이다. 재시도 안내는 기존 LoadingScene을 로드할 수 있어야 한다.
- 신규 자산·meta 짝, 신규 GUID 중복과 `git diff --check`를 확인했다. 검증 중 생성된 기본 폰트 fallback 캐시 변경은 원복했다. Unity는 Play 종료, 임시 runInBackground 설정 복원 상태다.
- 기존 설치 패키지·프로젝트 설정 변경을 보존한다. Unity가 추가한 EditorBuildSettings의 App UI config 등록도 이번 기능 변경과 별개로 남아 있다. 이 작업에서 커밋·푸시·병합은 수행하지 않았다.
