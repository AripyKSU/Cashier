# 담당 기능 명세·구현 대조

기준: 2026-09-11, `codex/inspector-events 50155f1` (`6115ec0` 감독관 구현 + `d8ea9ef` 공통 문서). 범위는 사용자 담당인 손님·거래, 설비, 도덕성, 대기열, 감독관이다. 관련 저장소 명세·코드·CSV·직렬화 연결·기존 테스트를 정적으로 대조했다. 최신 사용자 확정 사항과 이후 승인된 Upgrade 통합 계약을 함께 적용했다.

검증 상태는 **PARTIAL**이다. 구현 대응은 확인했으나 아래 표현 불일치와 검증 공백이 남아 있다. 이번 실행 테스트 0건이며 Unity 컴파일·제품 Console·UI/UX를 새로 검증하지 않았다. 테스트 파일의 존재는 현재 실행 성공 증거가 아니다. 기능 수정은 수행하지 않고 문서 차이만 정정했다.

## 요구사항 대응

| 기능 | 확인한 계약과 구현 근거 | 기존 검증과 한계 |
|---|---|---|
| 손님 조립 | [CustomerCompositionSelector](../Assets/Scripts/Customer/CustomerCompositionSelector.cs)의 SelectComposition은 성향·속성·외형을 별도로 선택한다. 속성은 남/여 × 성인/아이/노인 × 일반이다. 성향 Wealthy와 속성은 별개이며 특수 속성을 임의 추가하지 않았다. | [CustomerContractTests](../Assets/Tests/EditMode/CustomerContractTests.cs)의 TypeThenRowSelectionAndIndependentAttributes는 호환 생성 경로 24조합을 확인한다. 실제 명성 선택 경로의 독립성 직접 회귀는 V-02 참조. |
| 상품 선택·거래 | 날짜·활성·설비 조건으로 후보를 걸러 선호 상품 ID OR 분류를 적용한다. [CustomerVisit.SubmitOffer](../Assets/Scripts/Customer/CustomerVisit.cs)는 최종 SaleItem을 합산하고 제출 시 현재가·원가·지침 위반·도덕성을 snapshot으로 확정한다. 거부는 수입·판매 목록 없이 결과를 남기고 DayProgress가 원본 결과를 한 번 적용한다. | CustomerContractTests, [CustomerCompositionSelectorIntegrationTests](../Assets/Tests/EditMode/CustomerCompositionSelectorIntegrationTests.cs), 관련 거래·정산 테스트가 존재한다. 지침 위반 기록 계약과 다른 담당의 실제 일일 지침 생성·UI 정책 전체 검증은 구분한다. |
| 설비 | [FacilityService](../Assets/Scripts/Facility/FacilityService.cs)는 가격 복사·구매 중복/재진입 거부·금액 차감·실패 복원을 소유한다. 상품 해금·편의 기능은 다음날, 가게 단계는 즉시 적용한다. [가용 상품 필터](../Assets/Scripts/Customer/CustomerProductAvailability.cs)와 정산 상점으로 연결된다. | [FacilityTests](../Assets/Tests/EditMode/FacilityTests.cs)에 잔액 부족, 단계 잠금, 다음날 활성, 재진입, 지불 후 알림 예외, 가용 상품 snapshot 검사가 있다. 최초 독립 설비안 이후 승인된 Upgrade 통합의 단계 조건을 현재 계약으로 사용한다. |
| 도덕성 | [MoralityCalculator](../Assets/Scripts/Morality/MoralityCalculator.cs)는 성향·수락 여부·현재가 대비 제안액 구간·성인/아이·노인 점수로 평가한다. 미등록 부자는 null(평가 없음)이다. 세션의 CurrentMorality, 일일 집계의 DailyMoralityDelta와 정산 MoralityDelta가 별도 수명을 유지한다. | [MoralityTests](../Assets/Tests/EditMode/MoralityTests.cs) 및 거래·일일 집계 테스트가 존재한다. 검토 범위에서 확정된 계산·소유권 불일치는 발견하지 않았다. 노인 직접 회귀는 V-01 참조. |
| 대기열 | [CustomerQueue](../Assets/Scripts/Customer/CustomerQueue.cs)의 정원 10명·5초 입장·FIFO·대기 만료·일시정지를 DayProgress가 구동한다. [CustomerQueueView](../Assets/Scripts/Scene/CustomerQueueView.cs)는 동일 크기, 계산대 하단·호흡 보정, 대기→계산대 이동과 좌/우 검정·알파 퇴장을 구현한다. MainScene에 공유 렌더러와 useCustomerQueue=1이 연결된다. | [CustomerQueueTests](../Assets/Tests/EditMode/CustomerQueueTests.cs)는 경고·만료·3초 불만 수명, 정원/FIFO, 긴 프레임 경계를 다룬다. 실제 표시 수명은 Q-01에서 다르다. |
| 감독관 | [감독관 명세](INSPECTOR_SYSTEM_DRAFT.md)의 첫날 20줄·최초 가게 3단계 구매 다음날 29줄, 대사 전용 서비스·독립 prefab·초기 준비 가림을 대조한다. 당일 선정 목록과 완료 이력은 세션 서비스가 보존한다. | 기존 EditMode/PlayMode와 실화면 증거는 명세 9절의 과거 기록이다. 현재 대조 결과와 미해결 항목은 아래에 기록한다. |

## 미해결 항목

### Q-01 · P2 · 불만 대사의 3초 표시와 퇴장 페이드 충돌

- 계약: [대기열 명세](CUSTOMER_QUEUE_INTEGRATION.md)의 만료 불만은 3초 표시한다. CustomerQueue.cs:124도 SpeechUntil을 프레임 종료 + 3초로 설정한다.
- 감사 당시 구현: 퇴장 방문의 부모 CanvasGroup이 0.45초에 alpha0이 되어 자식 Speech도 함께 사라졌다. 당시 실화면 재현은 하지 않았다.
- 2026-09-11 `codex/sprite-world-presentation` 보완: SpriteRenderer 외형과 월드 TMP 대사를 별도 객체로 분리했다. 불만은 이탈 당시 위치에 남아 모델의3초 수명을 사용하고, 이미지 검정/alpha만0.45초로 전환한다. pause·전면숨김·비활성/날짜교체 정리도 같은 수명 경계에 적용한다.
- `InspectorWorldQueuePreservesIdentityAndIndependentSpeechLifetime` 실행에서 외형 alpha0 이후 대사 유지, pause·숨김·재활성·만료 정리를 통과했다. 최종 실행 묶음은 [작업 기록](work/sprite-world-presentation.md)에 기록한다. 문구 가독성과 위치의 최종 UX는 사용자 확인 대기이며 자동 검사를 화면 승인으로 확대하지 않는다.
- 상세 계약: [월드 표시 조립](CUSTOMER_QUEUE_INTEGRATION.md#월드-표시-조립-2026-09-11). 보완 구현은 개인 씬의 CustomerWorldQueueView다. MainScene은 기존 CustomerQueueView로 복원했으므로 Q-01은 공유 Main 경로에서 여전히 미해결이다.

### V-01 · P3 · 노인 도덕성 점수 직접 회귀 미등록

MoralityTests.cs:15-34는 Adult와 Child로 구간 점수를 검사하지만 Elderly 입력을 직접 검사하지 않는다. 구현은 Adult 비트가 없는 유효 프로필에 ChildElderlyMoralityPoint를 선택하므로 현재 계산 결함으로 판단하지 않는다. 기존 테스트에 동일 경계의 Elderly 입력 한 건을 추가하면 누락을 막을 수 있다.

### V-02 · P3 · 실제 손님 선택 경로의 조합 독립성 검증 보강

CustomerContractTests.cs:104의 24조합 검사는 호환 CustomerGenerator.Generate 경로다. 실제 명성 기반 SelectComposition의 통합 테스트는 타입 가중치·선호·설비 활성에 집중한다. 외형과 속성의 독립성을 해당 경로에서도 검사하면 이후 성별 이미지 연결 수정이 속성 생성에 섞이는 회귀를 잡을 수 있다. 현재 구현 불일치로 판단하지 않는다.

### D-01 · P3 · 설비 API의 오래된 XML 설명

FacilityService.cs:98은 고단계도 선행 구매를 요구하지 않는다고 설명하지만 :116 이후는 RequiredStoreStage와 다음 단계 순서를 검사한다. 구현과 현재 설비 명세는 일치한다. 다음 코드 수정 시 XML 설명을 현재 단계 조건·즉시/다음날 적용 계약에 맞춰 정정한다. 이번 문서 감사에서는 C#을 수정하지 않았다.

### V-03 · P3 · 감독관 경계의 직접 회귀 보강

- 일반 설비: GameSessionManager.EnsureInspectorDay는 단계 설비와 일반 설비의 활성일 의미를 구분한다. 기존 PlayMode의 InspectorPurchaseDayThreeAppearsDayFourOnce는 StoreStage 구매를 검사하지만 일반 설비의 실제 구매→전날 보유 스냅샷 경로는 직접 검사하지 않는다. 일반 설비 조건 fixture 한 건으로 구매 당일 미선정·익일 선정을 검증할 수 있다.
- 거래 잠금: 기존 PlayMode는 감독관 중 OpenBusiness·설비 구매 거부를 검사하지만 BeginCustomerSorting/SubmitOffer 거부를 직접 검사하지 않는다. 상태 가드는 존재하며 결함으로 판단하지 않는다.
- 동일 우선순위: InspectorEventService 생성자의 priority→idx 정렬은 존재하지만 InspectorEventTests의 복수 이벤트 검사는 서로 다른 priority를 사용한다. 동일 priority·역순 입력 두 행으로 tie-break를 고정할 수 있다.

### V-04 · 검증 보류 · 초기 화면의 느린 로딩·Scene 전환

[GameSessionApiTests](../Assets/Tests/PlayMode/GameSessionApiTests.cs):740-823은 최초 덮개 상태·준비 후 해제·재생성·퇴장 중 파괴·참조 실패의 오류 표시를 검사한다. 의도적인 리소스 지연 중 매 프레임 노출 검사와 GameUI 생성 이전의 전체 Scene 전환 가림은 이 테스트만으로 입증되지 않는다. 감독관 명세의 사용자 화면 확인 범위에 유지하며 이번 정적 감사로 완료 처리하지 않는다.

## 감독관 대조 근거

- InspectorEventService.cs:93-115는 같은 날 BeginDay를 재호출해도 빈 목록을 포함한 최초 선정 결과를 유지한다. 생성자 정렬, 날짜·설비·최소 단계 AND 조건, OncePerSession/OncePerDay 이력과 날짜/이벤트/줄 번호 중복 방지 경계를 확인했다.
- GameSessionManager.cs:47-63은 단계 설비의 activationDay < ElapsedDays, 일반 설비의 activationDay <= ElapsedDays로 전날 구매 상태를 구성한다. DayProgress.cs:243-285의 전진·퇴장·PreOpen 전환과 영업 잠금, 세션의 설비 구매 잠금을 확인했다.
- [InspectorPresenter](../Assets/Scripts/UI/Presenters/InspectorPresenter.cs)는 별도 초상 틴트와 대사 CanvasGroup을 사용한다. 활성 수명·취소·OnEnable 재개·입장 전과 퇴장 중 입력 차단을 확인했다. GameUIController는 로드·바인딩·첫 레이아웃 이후 presentationReady를 설정하고 실패 시 오류 덮개를 유지한다.
- GameUI의 InspectorPanel nested GUID는 실제 독립 prefab meta와 일치한다. StartupCover는 직렬화 상태부터 활성·검정 alpha1·전체 stretch·입력 차단으로 연결된다. UI 직렬화 확인을 실제 가시성 검증으로 확대하지 않는다.
- 검토한 감독관 계약에서 확정 구현 불일치는 발견하지 않았다. 기존 테스트·현재 코드 대응과 위 검증 공백을 함께 기록하며 새 실행 PASS를 주장하지 않는다.

## 이번에 정정한 문서

- 대기열 명세의 MainScene 미연결·Local 렌더러 전용 설명을 현재 공유 연결로 갱신하고 Dev3 버튼/자동 인계 설명은 과거 기록으로 구분했다.
- 설비 명세에 감독관 진행 중 API 구매 거부를 추가했다.
- 데이터 카탈로그에 감독관 CSV·enum·Text 추가분과 현재 설비 표시 enum을 반영했다. 카탈로그의 과거 스냅샷과 현재 값을 혼동하는 연결 설명도 조사 범위에서 정정했다.
- 공통 색인에 감독관 명세와 이 감사·작업 상태를 연결했다. 감독관 기능 명세에는 현재 통합 기준과 과거 검증의 구분을 추가했다.

## 후속 작업 순서와 제외 범위

1. Q-01의 브랜치 구현·검증 기록을 통합 시 확인하고 최종 대사 위치/가독성은 사용자 확인한다. API 테스트만으로 UI 완료를 선언하지 않는다.
2. 관련 코드 수정 시 V-01~V-03과 D-01을 함께 보완하고 해당 Unity Test Runner 검사를 실행한다. V-04는 실제 화면·로딩 검증으로 확인한다. 단순 문서 수정 때문에 전체 테스트를 반복하지 않는다.
3. 감독관을 total_merge에 반영할 때 최신 원격 기준과 데이터·GUID·Addressables 차이를 재검토하고 필요한 통합 검증을 수행한다.

CSV DTO의 public setter를 관례적으로 읽기 전용으로 사용하는 기존 합의는 유지했다. CsvHelper 우회, 신규 MVP·이벤트 엔진·저장 시스템, 다른 담당의 경제·명성 계산·도덕성 서사 확장은 이번 감사의 수정 범위가 아니다. 전용 감독관 이미지와 최종 UI/UX는 미완료가 명시된 후속 항목이며 누락 구현으로 계산하지 않는다.
