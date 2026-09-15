# 손님 대기열 구현·병합 명세

## 현재 구현 상태 (2026-09-15)

- `codex/customer-talk-ui`: 거래 완료·대기 이탈의 오른쪽 출구 고정과 대기 불만 대사 추종을 구현했다. 씬·프리팹·CSV 변경 없이 기존 연결을 사용하며 아직 통합 브랜치에 병합하지 않았다.
- Unity 컴파일 오류 0, 제품 Console error 0. 기존 `InspectorWorldQueuePreservesIdentityAndIndependentSpeechLifetime` PlayMode 1/1 통과(실패·skip 0): 오른쪽 목표·이동·대사 오프셋, 이미지 fade·3초 대사 정리·숨김·재활성·거래 이모지를 확인했다. 증거: `Temp/CustomerTalkFocusedTestResult.json`. 전체 suite·legacy UI 실행·최종 화면 가독성은 이번 검증 범위 밖이다.

GameUIController의 `useCustomerQueue` 옵션으로 DayProgress 대기열을 연결한다. GameUI prefab 기본값은 false지만 공유 MainScene은 override로 true를 지정하고 공유 CustomerWorld.prefab의 CustomerWorldQueueView를 연결한다. 개인 씬이나 Local 코드 없이 공유 씬에서 대기열을 표시한다. 현재 진행/시간/정산은 [MAINSCENE_INTEGRATION.md](MAINSCENE_INTEGRATION.md)를 따른다. 아래 과거 Dev3 설치·검증 기록은 보존하되 현재 공유 씬 사용법으로 해석하지 않는다.

### 현재 GameUI 연결 계약

- DayProgress가 시계를 소유한다. `UsesCustomerQueue`, `WaitingCustomers`, `LeavingCustomers`, `DepartedCustomers`, `GetQueueSpeech`로 조회하며 가변 큐를 외부에 공개하지 않는다.
- 최초 손님은 즉시 생성한다. 이후 5초 입장과 FIFO를 사용하고 빈 계산대는 다음 입장을 기다린다. 긴 프레임은 프레임 종료까지 만료를 먼저 처리하고 생존한 방문만 인계한다.
- `CustomerDeparted`는 거래 완료 방문을 알린다. 대기 이탈은 거래·명성 페널티를 추가하지 않고 이탈 수에만 반영한다. Closing은 대기열을 정리하고 마지막 거래는 유지한다.
- 공유 MainScene은 `CustomerWorld.prefab`의 `CustomerWorldQueueView`를 사용한다. Canvas 밖 방문 객체별 SpriteRenderer를 입구→대기 위치→계산대→화면 오른쪽 출구로 이동하며, 성별 라벨·거래 대사는 기존 UI에 유지한다. 이동은 모델을 변경하지 않는다.
- 마지막 거래의 정산 모델은 즉시 확정하지만 `queueExitSeconds`(기본 0.45초) 동안 정산 화면을 지연한다. 공유 렌더러 퇴장도 같은 값을 사용한다. 일시정지는 큐와 UI 연출을 함께 정지한다.
- 아래 Dev3 버튼·3초 자동 결과 인계 설명은 이전 화면의 계약이다. 현재 GameUI의 거래 결과 완료 경로와 혼동하지 않는다. UI/UX는 사용자 확인 대상이다.

- 모든 손님은 같은 크기를 사용하며 원근 배율은 적용하지 않는다. 계산대 하단 가림선에 호흡 최대 상승량을 보정한다. 퇴장 이미지는 검정 틴트와 alpha 페이드를 적용한다.
- 월드 손님의 이동은 0.65초 동안 개별 위상의 좌우 흔들림·상하 발걸음·미세 squash를 적용하되 양 끝 오프셋은 0이다. 거래 완료와 대기 이탈은 모두 오른쪽 출구와 같은 보행·검정 fade 시간을 사용한다.
- 현재 방문의 확정 `CustomerTradeOutcome`은 방문당 한 번 4개 직렬화 Sprite(Satisfied, Delighted, Reluctant, Refused)로 표시한다. 1초 pop·상승·fade는 pause와 전면 숨김에서 멈추고 퇴장·날짜 교체·비활성화 때 정리한다.
- CustomerWorldQueueView의 월드 TMP는 불만의 모델 수명 3초를 보존하도록 외형과 별도 객체지만, 매 프레임 퇴장 중인 외형의 현재 위치를 따라간다. 외형의 0.45초 퇴장 alpha는 적용하지 않으며 표현 차단·전면 숨김·날짜 교체·비활성 정리 계약을 유지한다. 최종 가독성은 사용자 확인 대상이다.

### 월드 표시 조립 (2026-09-11)

- 공유 MainScene과 개인 SpriteWorldSandbox의 독립 `Assets/Prefabs/World/CustomerWorld.prefab` 인스턴스에 WorldSceneView와 CustomerWorldQueueView를 함께 연결한다. Canvas 부모 아래에 두지 않는다.
- RenderRoot는 전면 UI의 화면 사각형만 카메라 viewport에 대응시킨다. 자식은 좌상단 기준 일반 Transform 좌표이며 UI Image를 실시간 복제하지 않는다. CounterAnchor·Entrance·RightExit·Slot01~10을 Scene/Prefab에서 편집한다. 기존 LeftExit 직렬화 참조는 prefab 호환용으로만 보존하며 필수 연결이 아니다.
- 동일 Visit 객체를 표시 키로 쓰므로 같은 외형 PK의 손님도 별개이며 대기→현재 전환에는 같은 SpriteRenderer를 재사용한다. 현재·대기 높이430px, 이동0.65초, 하단12px+최대 bob 보정을 유지한다.
- 정렬은 배경0~11→대기100~91→현재200→거래 이모지220→캐노피250→탐조등273~274→대사300이다. 매대/매대 조명은 Canvas UI가 월드를 가린다.
- GameUIController의 기존 외형 preload와 `GetCustomerAppearanceSprite`를 사용한다. queue는 Sprite handle을 로드·해제하지 않는다. 시간대 인물 tint와 퇴장 검정/alpha는 `ComposeColor` 한 곳에서 합성한다.
- 개인 씬은 GameUI·OperatingPanel 루트만 native unpack하여 UI 이관 상태를 보존했다. 향후 공유 통합은 [MainScene 조립 지침](MAINSCENE_INTEGRATION.md)을 따른다. 구형 Sale Sorting 설치기는 원본이며 월드 prefab 존재만으로 차단하지 않는다. 월드 개인 씬에 구형 UI 전체 재설치를 실행하지 않는다.

## 2026-09-14 프로토타입 보행·이모지 반영

- `DystopiaScreen.AdvanceQueueVisual`의 보행 위상·좌우/상하 흔들림·미세 squash를 현재 월드 이동에 이식했다. 원본의 원근 축소·자동 거래 완료는 현재 계약에 적용하지 않는다.
- 이모지는 새 가격/도덕성 판정 없이 기존 확정 `CustomerTradeOutcome`을 사용한다. `GetReactionIndex`는 `None`/그 외 값=-1, `RegularSale`=0(Satisfied), `DiscountSale`=1(Delighted), `ExploitativeSale`=2(Reluctant), `PaymentRefused`=3(Refused)를 반환한다.
- `tradeReactionSprites`는 위 순서의 필수 참조 4개다. 새 시트 사본과 GUID·영역·연결은 [리소스 이관 기록](DYSTOPIA_RESOURCE_INTEGRATION.md#2026-09-14-손님-보행거래-표정-이관)을 따른다. 외형 객체를 소유하는 기존 Visual이 64px 이모지의 생성·정리를 함께 맡는다.
- MainScene 파일을 저장하거나 교체하지 않고 이미 참조 중인 `CustomerWorld.prefab`을 갱신했다. 프로토타입 원본·CSV·Addressables·가격 판정·퇴장 입력은 변경하지 않았다.
- 실제 Init→Hub 새 게임→Main의 할인 거래에서 `Delighted`가 머리 옆에 표시됨을 확인했다. pause 중 경과값 유지 후 resume하면 비활성화됐다(관찰 종료 경과 1.306035초: 낮은 프레임 속도로 1초 경계를 넘은 프레임에서 종료). `Temp/Queue-Reaction-Main.png`, `Temp/Queue-Reaction-Main.txt`. API로 진행한 화면 확인이며 최종 사용감·다른 화면비·Player build 검증은 아니다.


### 검증 결과

- EditMode **259/259**, 실패·skip·미완료 0: `Temp/TestResults/20260914-155015-709493e032ca4255b8af796b08d514d6/EditMode.xml` 및 `.log`.
- 최종 PlayMode **54/54**, 실패·skip·미완료 0: `Temp/TestResults/20260914-160818-09c6891cafaa450e9f94a0c3762cf3db/PlayMode.xml` 및 `.log`. 네 결과/None 매핑, 방문 identity, 불만 수명, 실제 Sprite 참조, pause·숨김 중 타이머 유지, 재표시 후 종료·중복 방지, 거래 완료 시 제거를 포함한다. 이전 계산기 이동·비활성화 회귀도 함께 통과했다.
- 중간 Play 실행 `155015-709493e032ca4255b8af796b08d514d6`, `160119-346ace35730647ea9383cb59aeac5a77`은 각각53/54였다. 이모지 종료 검사에서 입장/쏟기 완료 전에 직접 거래를 제출한 fixture의 남은 코루틴이 전면을 숨겼다(`elapsed=.732869, opacity=0, paused=false`). 실제 Sorting 완료 대기와 종료 deadline으로 검사를 보정했다. 제품의 숨김 정지·1초 연출 규칙을 완화하지 않았다.
- Unity 컴파일 오류 없음, `CustomerWorld.prefab` missing script0·표정4개 참조 유효, MainScene의 prefab dependency 확인. 원본과 사본 PNG 해시 일치·신규 GUID 중복 없음·`git diff --check` 통과. Console의 실패 주입 로그는 기존 테스트의 예상 로그이며 새 제품 오류로 집계하지 않는다.
- 종료 상태: InitScene clean, Play 종료, compileFailed=false, runInBackground=false, pending test 없음. MainScene·프로토타입 원본·Addressables·CSV·ProjectSettings 내용 변경 없음. 기존 계산기 변경을 보존하며 이번 작업에서 commit/push는 하지 않았다.

## 범위와 규칙

- 대기 정원·표시 최대 10명. 계산 중인 손님은 별도다. 이전 Dev3 화면의 #번호는 현재 공유 렌더러의 필수 계약이 아니다.
- OPEN STORE에서 최초 손님을 바로 계산대에 배치하고 이후 5초마다 줄에 합류한다. 꽉 찬 시점의 입장은 건너뛰며 재시도 물량을 쌓지 않는다.
- 합류 시 외형·성향·최초 희망 목록과 표시 단가를 고정한다. 실제 거래 기준액·허용액·원가는 SubmitOffer 시점의 최종 목록·최신 현재가로 확정한다. 라디오 이후에도 최초 표시 단가는 보존하지만 판정은 최신 가격을 사용한다.
- 성향별 한도에 도달하면 즉시 논리적 이탈. 계산 중인 손님에게 대기 한도는 적용하지 않는다.
- 남은 시간 6초 이하일 때 재촉 1회, 만료 시 불만 1회. 말풍선은 각각 3초 표시한다. 긴 프레임에서 재촉·만료를 함께 넘으면 불만만 표시한다.
- 만료를 처리한 뒤 계산대로 인계한다. 현재 GameUI는 거래 결과 완료 입력에서 DayProgress.CompleteTransactionResult를 호출한다. 아래 3초 자동 인계·NEXT CUSTOMER 설명은 이전 Dev3 화면에만 해당한다. 빈 줄에서는 새 손님을 즉시 생성하지 않는다.
- 게임 일시정지 시 입장·대기·대사 표시·결과 표시 시간이 멈춘다. 영업 종료 시 줄과 남은 말풍선은 불만 없이 제거한다. 이탈 페널티·일수별 인원 증가는 범위 밖이다.

## CSV migration

기준 커밋 `6884abf`의 성향 CSV 마지막에 아래 필수 컬럼을 추가한다. 기존 열·행·ID·GUID를 보존한다. 구형 CSV는 header 누락으로 실패하며 기본값으로 우회하지 않는다. 롤백은 DTO·CSV·로더·catalog·소비자를 함께 되돌린다. 현재 세이브 연결은 없다.

| 컬럼 | 타입/검증 | 의미 |
|---|---|---|
| queue_patience_seconds | uint, 6 초과 | 줄 합류부터 만료까지 초 |
| queue_warning_textidx | uint, 필수 TextData FK | 재촉 대사 |
| queue_leave_textidx | uint, 필수 TextData FK | 이탈 불만 |

테스트 배정: 평범(6001) 12초·8050/8051, 성급함(6002) 9초·8052/8053, 가격 민감(6003) 18초·8054/8055. TextData의 기존 마지막 8049 뒤에 6개 문구를 추가했다. 새 CSV 종류·Addressables 등록은 없다. 병합 직전 다른 branch와 신규 Text PK 충돌을 다시 검사한다.

`CustomerDispositionDataTable`은 구매 및 대기 설정을 검증하고 `CustomerCatalog`는 두 신규 Text FK를 검사한 후 공개한다. 오류 시 기존 LogError·예외 경로를 따른다. 독립 구매 생성 테스트는 대기 설정을 요구하지 않지만 CSV 로드와 줄 합류에서는 필수다.

## 소유권·API

- `CustomerQueue`: 화면 수명 대기열. 생성 콜백과 성향 사전을 생성자에 전달한다. 콜백은 그 시점의 `EnsureDailyPrices().Prices`를 사용한다.
- `Start` / `Stop`: 영업 시작·정리. 중복 Start는 거부한다.
- `Advance(deltaSeconds, paused)`: 활성 영업 화면 한 곳에서 프레임당 한 번 호출한다. 만료·재촉·5초 입장을 처리한다. 인계 전에 호출해야 한다.
- `TryAdd`: 정원 내에서 새 방문 등록. 초기 계산대 손님을 위한 1회 호출 외에는 자동 입장을 사용한다.
- `TakeNext`: FIFO 인계. 빈 줄이면 null. 반환된 방문에 `BeginOffer`를 호출하고 기존 거래 시스템을 사용한다.
- `Waiting` / `Leaving` / `GetSpeech`: 읽기 전용 대기·이탈 표시 데이터. 이탈 기록은 논리적 정원에 포함하지 않는다.
- `CustomerState` 기존 숫자는 보존하고 `Queued=5`, `Abandoned=6`을 뒤에 추가한다. 만료 이탈은 거래 결과 None이며 수입을 발생시키지 않는다.
- `Dev3SandboxTester.Queue`: 통합 화면 소유 대기열 조회. Update가 시계를 진행하며 고정 10개 색상 사각형을 재사용한다. 테스트 배치는 계산대 왼쪽 2열이며 최종 공간상 줄 배치는 아트/UI 통합 시 교체할 수 있다.
- 화면 파괴와 영업 종료 시 Stop. 영업 중 씬 재진입·세이브 복원은 기존 제한을 유지한다. 구형 CustomerSandbox는 Git 제외 Local 개인 코드이며 공유 설치에 필요하지 않다.

## 이전 Dev3 사용법·검증 기록

1. InitScene부터 개인 GameplaySandbox로 진입한다. START SESSION → 여정 시작 → 영업 시작 → OPEN STORE.
2. 계산 중인 손님을 그대로 두면 5초 간격으로 대기열이 증가한다. 최대 10명을 넘지 않는다.
3. 성급함 성향은 합류 3초 후 재촉, 9초 후 이탈한다. 평범은 6/12초, 가격 민감은 12/18초다. 입장 간격 5초·대사/결과 표시 3초·라디오 시간은 유지한다.
4. 거래를 판정하면 3초 후 다음 대기 손님이 계산대로 이동한다. 종료하려면 결과 표시 중 END DAY를 누른다.
5. 자동 API 검사는 [TESTING.md](TESTING.md)의 CustomerQueueTests/CustomerCsvTests를 사용한다. 기존 셸은 제거했다. 정확한 최신 건수·증거는 해당 문서를 따른다.
6. 위 화면·버튼·말풍선 사용법은 사용자 수동 검증이며 자동 API 통과와 별개다.

이전 개인 씬 구현 검증 기록(현재 Test Runner 실행과 별개): PASS. 순수 로직·CSV·기존 CustomerGenerator 회귀 통과. Unity 컴파일 통과. GameplaySandbox에서 10명 색상·PK 표시, 재촉·이탈 표시, 3초 결과 이후 자동 FIFO 인계, 기존 거래 수락/거부·중복 수입 방지·정산·다음 날 전환 통과. Console error 0. 시간 경계 검사는 Advance로 시간을 주입했고 자동 인계는 실제 Update 경과로 확인했다. 최종 아트·공간상 줄 배치는 미작업이다.

## 초기 대기열 병합 기록 (현재 통합 시 실제 diff 재확인)

- 성향 CSV와 DTO·로더·catalog·TextData를 같은 변경으로 병합한다. 기존 header를 유지한 채 3개 열을 끝에 추가한다.
- MainScene/prefab을 새로 저장할 필요 없이 기존 Dev3SandboxTester 연결을 사용한다. 개인 씬은 Git 제외 유지.
- 공용 가격 이벤트·Finance API를 복제하지 않는다. 최초 희망 표시와 제출 시점 확정 단가를 구분하고 기존 거래의 한 번 입금 계약을 유지한다.
- UI branch의 NEXT CUSTOMER·Update·영업 시작/종료 변경과 의미 충돌을 검토한다. 초기 영업 안내·대기 손님의 희망 목록 표시 단가와 제출 시 확정 거래 단가는 라디오 발생 이후 서로 다를 수 있다.
- 신규 스크립트의 Unity 생성 .meta를 함께 병합한다. 기존 TMP fallback font 변경은 이 기능과 무관하다.


현재 성향 CSV에는 이후 추가된 disposition_type/preferred_product_idxs/regular_price_min_rate/regular_price_max_rate도 존재한다. 위 3열 migration만 복사해 최신 후속 열을 잃지 않는다. 실제 헤더와 [CUSTOMER_INTEGRATION.md](CUSTOMER_INTEGRATION.md)를 함께 반영한다. Test Runner assembly/meta 및 Local 제외 의존은 [TESTING.md](TESTING.md)를 따른다.
