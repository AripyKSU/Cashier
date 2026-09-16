# 작업 상태: 설비 업그레이드 팸플릿 UI 병합 인계

- 사람 담당자 / 위임받은 역할·범위: 김승욱(`kimsu00215@naver.com`) / 설비 UI·설비 가격 데이터·구매 로직·관련 프리팹과 테스트. 역할 명부상 프로그래머 Primary, 기획·리소스 Shared다. 이 기록은 권한 확대나 자기 승인을 의미하지 않는다.
- 사용 도구 / 보조 세션 링크(선택): Codex Desktop과 Unity Editor 연결 도구. 도구 링크 없이 아래 commit과 저장소 파일만으로 재현할 수 있다.
- 마지막 확인 시각(시간대 포함): 2026-09-16 17:16:55 +09:00
- 기준 저장소·브랜치·commit: `Cashier`, `FacilityUI`, `a91a19900fc1f26f4f8f0b04b76477dc24e36176`. 공통 조상 및 병합 대상 후보 기준은 `total_merge`의 `0006cee8`이다.
- 허용 경로 / 제외 범위: 이 commit은 설비 팸플릿 UI, `FacilityData` 가격, 무료 단계 상승 처리, 정산 가계부 TMP 자동 크기 조정과 공유 `GameUI`/`MainScene` 직렬화 변경을 포함한다. Package, ProjectSettings, Addressables, 개인 `Assets/Scenes/Local`, 저장 시스템은 변경하지 않았다.
- 보존할 미커밋·untracked 변경 / stash: 확인 시 작업 트리는 clean이다. stash도 없다. 구현 중 기존 미커밋 `FacilityShopPanel.prefab`은 `Temp/LocalBackups/FacilityPamphlet-20260916/`에 임시 백업했으나 Temp 자료는 다른 checkout에 없을 수 있으며 병합 입력으로 사용하지 않는다.
- 참조할 기능 명세·선행 산출물: [`FACILITY_INTEGRATION.md`](../FACILITY_INTEGRATION.md), [`DATA_CATALOG.md`](../DATA_CATALOG.md), [`CITIZENSHIP_ENDING.md`](../CITIZENSHIP_ENDING.md), [`PREFAB_RESOURCE_RULES.md`](../PREFAB_RESOURCE_RULES.md), [`BRANCH_INTEGRATION_RULES.md`](../BRANCH_INTEGRATION_RULES.md).

## 완료한 내용

### 설비 팸플릿 UI

- 기존 동적 `FacilityItemView`/ScrollRect 행 화면을 고정 좌표 팸플릿 화면으로 교체했다. `FacilityShopPanel.prefab`은 `Stage1Panel`, `Stage2Panel`, `Stage3Panel`, `CitizenshipPanel` 중 하나를 표시한다.
- 1~3단계는 각각 가격 Text·구매 Button·SoldOut Image를 가진 `FacilityPamphletSlotView` 3개, 시민권은 같은 구조의 슬롯 1개를 가진다. 각 슬롯과 SoldOut의 RectTransform은 Inspector에서 사람이 직접 이동할 수 있다.
- 현재 단계 일반 설비를 모두 구매하기 전에는 `PurchasedAllToUnlock`, 완료 후 1·2단계는 `UpgradeToNextFacility`, 3단계는 `UpgradeToCivilization` Sprite를 표시한다.
- 1·2단계 진행 버튼은 기존 단계 상승 설비 PK를 구매 요청한다. 3단계 진행 버튼은 시민권을 구매하지 않고 시민권 패널만 연다. 한 번 열린 시민권 페이지는 같은 Presenter 수명에서 팸플릿을 닫고 다시 열어도 유지된다.
- 기존 X 닫기 버튼 대신 전체 화면 `outsideCloseButton`이 `OnCloseRequested`를 보낸다. 팸플릿 Window가 내부 raycast를 가로채 구매 클릭과 바깥 닫기 클릭을 분리한다. 구매 중 닫기 잠금과 `GameUIController.closeFacilityShop()`의 잔액 구독·정산 입력 복원은 유지한다.
- `GameUI.prefab`의 nested `FacilityShopPanel` Window override는 중앙 anchor, `430 × 645`로 맞췄다. `FacilityShopPanel.prefab` GUID `51fe6c289b4d87748a3ffce59cdbeac2`와 Presenter source fileID 연결을 유지한다.

### 데이터와 구매 계약

- 단계 상승 `12008`, `12010`의 가격을 0G로 변경했다. `FacilityData.Validate()`는 `StoreStage`에만 0원을 허용하고 상품·편의·시민권의 0원 가격은 계속 거부한다.
- `FacilityService`는 0원 단계 상승일 때 `FinanceService.TrySpend`를 호출하지 않지만 보유 등록, 현재 단계 변경, `PurchaseCompleted` 발행과 예외 후 확정 상태 보존은 기존 흐름대로 처리한다. 일반 설비와 시민권 결제 경로는 바뀌지 않는다.
- 기존 단계 상승 가격을 현재 단계 일반 설비에 분배했고 모든 가격을 1,000G 단위로 정리했다.

| 단계 | 일반 설비 가격 | 단계 합계 | 진행 가격 |
|---|---:|---:|---:|
| 1 | 12001=257,000G / 12002=467,000G / 12007=84,000G | 808,000G | 12008=0G |
| 2 | 12003=707,000G / 12004=897,000G / 12009=488,000G | 2,092,000G | 12010=0G |
| 3 | 12005=350,000G / 12006=1,980,000G / 12011=15,000G | 2,345,000G | 시민권 12012=10,000,000G |

### 리소스와 정산 UI

- `Assets/Textures/Checkout/UI/FacilityUpgrade/`에 팸플릿 4장과 상태/진행 이미지 4장을 `.meta`와 함께 추가했다. 2026-09-16 검사에서 신규 GUID를 포함한 `Assets/**/*.meta` 중복 그룹은 0개였다.
- `SettlementPanel.prefab`의 가계부 Text 2개는 fontSize 26, auto sizing 활성, min 16/max 26으로 변경됐다.
- `Mulmaru SDF.asset` 재질의 `_ScaleRatioA`, `_ScaleRatioC` 값도 commit에 포함된다. 폰트 전역 소비자에 영향을 줄 수 있으므로 정산 자동 크기 변경과 별도로 검토한다.

## 병합 단위와 권장 순서

1. CSV·DTO·서비스·테스트를 한 계약 단위로 이관한다: `FacilityData.csv`, `FacilityData.cs`, `FacilityService.cs`, `FacilityTests.cs`, `GameSessionApiTests.cs`. CSV만 또는 서비스만 선택 이관하지 않는다.
2. 신규 이미지와 모든 `.meta`, `FacilityPamphletSlotView.cs/.meta`, `FacilityShopPresenter.cs`, `FacilityShopPanel.prefab`을 함께 이관한다. 이미지 GUID나 기존 패널 GUID를 재생성하지 않는다.
3. 대상 branch의 `GameUI.prefab` 계층과 다른 기능 연결을 기준으로 nested `FacilityShopPanel`의 Window anchor/size override 및 Presenter 참조만 의미 단위로 합친다. YAML의 ours/theirs 전체 선택을 사용하지 않는다.
4. `SettlementPanel.prefab` 자동 크기 변경과 `Mulmaru SDF.asset` 재질 값은 설비 UI와 독립된 두 번째 변경으로 검토·이관한다.
5. `MainScene.unity`는 아래 미확정 사항을 먼저 대조한 뒤 필요한 override만 Unity Editor에서 반영한다. 파일 전체 선택 이관이나 prefab override 일괄 적용은 권장하지 않는다.
6. 계약 설명인 `FACILITY_INTEGRATION.md`, `DATA_CATALOG.md`를 실제 CSV·코드와 함께 반영한다.

## 충돌·보존 주의사항

- `FacilityShopPresenter`의 직렬화 필드가 동적 행/ScrollRect 구조에서 고정 슬롯 배열과 시민권 페이지 구조로 바뀌었다. 대상 branch의 구형 `FacilityShopPanel.prefab`을 유지하면서 스크립트만 이관하면 필수 참조가 비어 실행 시 실패한다.
- `GameUI.prefab`은 다른 UI 기능들이 함께 의존하는 공유 프리팹이다. 대상 branch의 최신 계층·직렬화 연결을 보존하고, 이 branch에서는 설비 Window의 anchor와 size 변경만 의도된 차이로 본다.
- `FacilityItemView.cs`와 `FacilityItem.prefab`은 삭제하지 않았다. 새 팸플릿 Presenter가 더 이상 사용하지 않을 뿐 다른 branch 소비자를 조사하기 전 제거하지 않는다.
- `MainScene.unity` diff에는 설비 외에도 여러 GameUI 활성 상태, TextStyleHashCode, font size/좌표, feedbackText null override, removed GameObject 1개가 포함된다. 이 값들은 commit 메시지만으로 모두 의도됐다고 확정할 수 없다. 대상 MainScene의 최신 prefab instance override와 대조하고 설비·정산에 필요한 항목만 채택한다.
- `Mulmaru SDF.asset` 변경은 같은 폰트를 쓰는 전체 UI에 영향을 줄 수 있는 공용 리소스 변경이다. 기본 branch 반영 전 리소스 담당 및 다른 프로그래머 Primary의 교차 검토가 필요하다.
- `FacilityData` 가격과 무료 단계 확장은 기획 수치 및 공용 구매 계약 변경이다. 기본 branch 반영 전 기획/프로그래머 교차 검토가 필요하다.

## 검증

- 상태: `PARTIAL`.
- Unity import·컴파일: 최종 `unity-cli editor refresh --compile --ignore-version-mismatch`에서 compilation complete를 확인했다.
- EditMode 전체 실행: 309개 중 301 성공, 8 실패, skip/미완료 0. 증거는 로컬 `Temp/TestResults/20260916-163852-0817dbaccd774236bd2c93b98d581d3f/EditMode.xml`이다. 설비의 무료 단계 상승 기대 잔액 1건은 이후 854→864로 수정했으나 전체 suite는 재실행하지 않았다. 나머지 당시 실패는 청소기 자산 2건, 손님 invalid submission 2건, 감독관 Text 폭 1건, 가격 이벤트 overflow 1건, WorldScene effect 1건이었다.
- PlayMode 전체 실행: 59개 중 52 성공, 7 실패, skip/미완료 0. 증거는 로컬 `Temp/TestResults/20260916-164026-00c3486331dc464c923c6b87a8b9e6d2/PlayMode.xml`이다. 새 Presenter의 반복 열기·구매 PK 전달·SoldOut·시민권 페이지 재개방 유지 검사는 통과했다. 실패는 실제 Sprite 개수, 시민권 기존 잔액 기대값, 정산 warning/금액 문자열, 감독관 시작 상태, 시계·이펙트 시간 기대값이었다.
- 정적 검사: 신규 이미지 asset/meta 짝과 GUID 중복 0을 확인했다. commit의 변경 파일은 32개다.
- 미검증: 전체 suite 최종 재실행, Init→Hub→Main 실제 사용자 클릭 경로, 화면비별 팸플릿 배치, 내부/외부 클릭 체감, 단계 1→2→3→시민권 엔딩 전체 수동 흐름, Player build, `MainScene`의 광범위 override 의도 확인.
- 다른 작업자 접근: `Temp/TestResults`와 `Temp/LocalBackups`는 로컬 전용일 수 있다. 재현은 대상 통합 checkout에서 `Tools/Run-Tests.ps1 -Mode Both -TimeoutSeconds 300`을 사용하되 Unity CLI/connector 버전이 다르면 먼저 같은 버전으로 맞춘다. 버전 불일치를 무시한 실행은 임시 진단으로만 사용한다.

## 남은 작업과 다음 담당자

- 다음 담당자: `total_merge` 통합 담당자와 김승욱 외 프로그래머 Primary 1명, 기획/리소스 검토자.
- 바로 할 행동: `a91a1990`과 통합 대상 최신 SHA를 고정하고, 대상 branch의 `GameUI.prefab`, `SettlementPanel.prefab`, `MainScene.unity`, `Mulmaru SDF.asset` 동시 변경을 먼저 비교한다.
- `MainScene`의 설비 외 override와 removed GameObject가 의도된 것인지 김승욱 또는 프로젝트 책임자 김기도가 확인해야 한다. 확인 전에는 해당 Scene 전체를 병합 완료로 판단하지 않는다.
- 최종 통합 후 실제 단계별 가격·구매·SoldOut·하단 이미지·재개방 시민권 페이지·바깥 클릭 닫기·시민권 즉시 엔딩을 MainScene에서 확인한다.
- 전체 EditMode/PlayMode를 재실행하고 기존 기준선 실패와 신규 회귀를 분리한다. 설비 관련 신규 실패가 있으면 `PASS`로 보고하지 않는다.
- 동일 파일·공용 계약 편집 담당 / Unity Editor 조작 담당: 통합 중 `FacilityData`, `FacilityShopPresenter`, `FacilityShopPanel`, `GameUI`, `SettlementPanel`, `MainScene`, `Mulmaru SDF` 편집과 Unity 실행은 한 명의 통합 담당자가 순차 수행한다.
- 장애·미확정·필요 승인: `MainScene` 광범위 override 의도, 공용 폰트 재질 영향, 가격 기획 승인, 전체 suite 기존 실패 기준선. 기본 branch 통합 전 다른 프로그래머 Primary 및 관련 기획/리소스 검토를 기록한다.

## Git

- stage: 수행됨.
- commit: `a91a19900fc1f26f4f8f0b04b76477dc24e36176`으로 수행됨.
- push: 확인되지 않음.
- merge: 수행하지 않음. `total_merge`는 아직 `0006cee8`이며 이 문서 작성 시점의 현재 branch는 `FacilityUI`다.
