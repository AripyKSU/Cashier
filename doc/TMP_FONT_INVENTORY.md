# TMP 한글 폰트 교체 전수조사

기준일: 2026-09-16 (KST)  
상태: 조사 문서만 반영. 현재 폰트와 Unity 에셋은 변경하지 않았다.

## 1. 목적과 조사 범위

향후 TMP 한글 폰트를 `Mulmaru` 계열로 교체할 때 누락되기 쉬운 오브젝트·컴포넌트·코드 경로를 한 곳에 기록한다. 이 문서는 교체를 실행하는 작업 기록이 아니라, 충돌이 정리된 뒤 안전하게 교체하기 위한 현재 기준점이다.

조사 대상은 현재 저장소의 제작용 다음 경로다.

- `Assets/Prefabs/**/*.prefab`
- `Assets/Scenes/**/*.unity`
- 위 에셋을 조립·생성·갱신하는 `Assets/Scripts/**/*.cs`
- `TMP_Text`, `TextMeshProUGUI`, `TextMeshPro`, `TMP_FontAsset` 참조 및 TMP 생성 코드

다음은 현재 제작용 TMP 교체 대상에서 제외했다.

- `Library`, `Temp`, `Logs` 등 Unity 생성물
- `Assets/Scenes/Local`의 개인 작업 씬
- `Assets/_Recovery/0.unity`의 복구용 참조
- `Assets/DystopiaPrototype`의 구형 `UnityEngine.UI.Text`/`Font` 기반 UI. 이 영역은 TMP 한글 폰트 교체 대상은 아니며, 일부 테스트 화면이 이미 `Mulmaru.otf`를 직접 읽는다.

## 2. 조사 요약

- 제작용 Prefab·Scene YAML에서 확인한 TMP 기록은 총 85개다.
  - 직접 수정할 수 있는 원본/씬 직렬화 컴포넌트: 81개
  - `GameUI.prefab` 안의 중첩 Prefab에서 상속된 `stripped` 기록: 4개
- 81개 중 현재 `Mabinogi_Classic_OTF SDF`를 직접 참조하는 컴포넌트는 78개다.
- 나머지 3개는 `LiberationSans SDF`다.
  - `OperatingPanel/AstraFrontView/CounterClock/ClockText`
  - `SettlementPanel/FinalConfirmationPanel/Message`
  - `LoadingScene/Canvas/LoadingBar/Text (TMP)`
- TMP 컴포넌트 외에 폰트를 직렬화하는 현재 제작용 필드가 2개 더 있다.
  - `CustomerWorldQueueView.font`
  - `CustomerPresenter.dialogueFont`
- 대사·데이터 문자열을 런타임에 TMP로 쓰는 생성 경로와, Prefab을 다시 만드는 에디터 자동설정 경로가 별도로 있다. `m_fontAsset`만 일괄 치환하면 다음 생성/초기화에서 기존 폰트가 되살아날 수 있다.

## 3. 현재 폰트와 교체 후보 에셋

| 역할 | 경로 | GUID/현황 | 현재 소비 위치 |
|---|---|---|---|
| 주 UI 폰트 | `Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset` | `c0f9f8080e30dd3499d44ece2a336e14` | 아래 정적 목록의 78개 TMP `m_fontAsset`, `CustomerWorldQueueView.font`, `CustomerPresenter.dialogueFont` |
| TMP 기본 폰트 | `Assets/TextMesh Pro/Resources/TMP Settings.asset` | `m_defaultFontAsset` → LiberationSans GUID `8f586378b4e144a9851e7b34d9b748ee` | 새 TMP가 명시적으로 폰트를 지정하지 않을 때의 기본값. fallback 목록은 비어 있음 |
| Mulmaru 후보 A | `Assets/TextMesh Pro/Fonts/Mulmaru SDF.asset` | `21967ff2222e4b449974e277bfa779ed` | 현재 소비자 없음 |
| Mulmaru 후보 B | `Assets/Fonts/Dystopia/Mulmaru SDF.asset` | `32b42a0afd978fe4b8fd3e9e49d62180` | 현재 소비자 없음 |
| Mulmaru 원본 A | `Assets/Fonts/Checkout/Mulmaru.otf` | `b3f0a6c94e8e4fc3a1f40d741190c0e6` | TMP 소비자 없음 |
| Mulmaru 원본 B | `Assets/Fonts/Dystopia/Mulmaru.otf` | `e0cc59d2cdf8f9848940aaeeb3d443e0` | TMP 소비자 없음 |

두 Mulmaru SDF 파일은 현재 바이트 해시가 같고, 두 OTF 파일도 현재 바이트 해시가 같다. 실제 교체 때는 둘 중 어느 SDF를 공식 기준으로 삼을지 먼저 결정하고, 한 GUID로 통일하는 것이 안전하다. 현재 프로젝트에는 Mulmaru SDF를 참조하는 제작용 소비자가 없다.

## 4. 제작용 정적 TMP 컴포넌트 전수 목록

표의 줄 번호는 현재 YAML에서 해당 TMP 컴포넌트가 시작하는 대략적인 줄이다. `M`은 Mabinogi, `L`은 LiberationSans를 의미한다. `동적/플레이 중 갱신`은 Prefab에 저장된 기본 문구가 한글이라는 뜻이 아니라, 해당 컴포넌트가 런타임에 한글 데이터 또는 메시지를 표시하므로 폰트 교체 검증에 포함해야 한다는 뜻이다.

### 4.1 엔딩·공통 UI

| 에셋 | 오브젝트 경로 | YAML | 폰트·용도 |
|---|---|---:|---|
| `Assets/Prefabs/Ending/EndingPanel.prefab` | `EndingPanel/DialoguePanel/NewGame/Label` | 48 | M · 새 게임 버튼 |
| 〃 | `EndingPanel/DialoguePanel/Dialogue` | 600 | M · 엔딩 대사 |
| 〃 | `EndingPanel/DialoguePanel/Next/Label` | 737 | M · 다음 버튼 |
| 〃 | `EndingPanel/DialoguePanel/Speaker` | 874 | M · 화자 |
| 〃 | `EndingPanel/DialoguePanel/Page` | 1220 | M · 페이지 표시 |
| 〃 | `EndingPanel/Heading` | 1357 | M · 엔딩 제목 |
| `Assets/Prefabs/GameUI/CommonHUD.prefab` | `CommonHUD/Day` | 210 | M · 일차 표시 |
| 〃 | `CommonHUD/Phase` | 347 | M · 단계 표시 |
| 〃 | `CommonHUD/Income` | 724 | M · 일일 수입 |
| 〃 | `CommonHUD/Balance` | 861 | M · 잔액 |
| 〃 | `CommonHUD/Settlement` | 998 | M · 정산 안내 |

### 4.2 딸 대사·설비·실패·검사 UI

| 에셋 | 오브젝트 경로 | YAML | 폰트·용도 |
|---|---|---:|---|
| `Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab` | `DaughterDialoguePanel/SpeechBubble/Dialogue` | 298 | M · 딸 대사, 데이터로 갱신 |
| `Assets/Prefabs/GameUI/ErrorPanel.prefab` | `ErrorPanel/Error` | 48 | M · 오류 문구, 런타임 갱신 |
| `Assets/Prefabs/GameUI/Facility/FacilityItem.prefab` | `FacilityItem/PurchaseButton/Label` | 169 | M · 구매 버튼 |
| 〃 | `FacilityItem/NameText` | 306 | M · 설비명 |
| 〃 | `FacilityItem/ActivationText` | 443 | M · 사용 시점 |
| 〃 | `FacilityItem/UnlockProductsText` | 580 | M · 해금 상품 |
| 〃 | `FacilityItem/PriceText` | 717 | M · 가격 |
| 〃 | `FacilityItem/StatusText` | 854 | M · 구매 가능 상태 |
| `Assets/Prefabs/GameUI/Facility/FacilityShopPanel.prefab` | `FacilityShopPanel/Window/ActivationGuideText` | 129 | M · 활성화 안내 |
| 〃 | `FacilityShopPanel/Window/FeedbackText` | 267 | M · 피드백, 런타임 갱신 |
| 〃 | `FacilityShopPanel/Window/CloseButton/Label` | 404 | M · 닫기 버튼 |
| 〃 | `FacilityShopPanel/Window/BalanceText` | 678 | M · 보유금 |
| 〃 | `FacilityShopPanel/Window/TitleText` | 815 | M · 설비 상점 제목 |
| `Assets/Prefabs/GameUI/FailurePanel.prefab` | `FailurePanel/FailureDescription` | 182 | M · 실패 설명, 런타임 갱신 |
| 〃 | `FailurePanel/FailureTitle` | 399 | M · 실패 제목 |
| 〃 | `FailurePanel/NewGame/Label` | 536 | M · 새 게임 버튼 |
| `Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab` | `InspectorPanel/Dialogue/Text` | 143 | M · 감독관 대사, 런타임 갱신 |
| 〃 | `InspectorPanel/Dialogue/Next/Label` | 280 | M · 다음 버튼 |

### 4.3 GameUI 루트와 영업 패널

| 에셋 | 오브젝트 경로 | YAML | 폰트·용도 |
|---|---|---:|---|
| `Assets/Prefabs/GameUI/GameUI.prefab` | `GameUI/ProgressCanvas/Root/Timer` | 1093 | M · 진행 UI 타이머 |
| 〃 | `GameUI/ProgressCanvas/StartupCover/StartupError` | 1450 | M · 시작 오류, 런타임 갱신 |
| `Assets/Prefabs/GameUI/OperatingPanel.prefab` | `OperatingPanel/PriceInput/Validation` | 83 | M · 가격 입력 검증, 런타임 갱신 |
| 〃 | `OperatingPanel/AstraFrontView/Customer/Appearance/TemporaryGender` | 610 | M · 임시 성별 표시, 런타임 갱신 |
| 〃 | `OperatingPanel/AstraFrontView/CounterClock/ClockText` | 1357 | L · 숫자 시계, 폰트 일관성 확인 대상 |
| 〃 | `OperatingPanel/AstraFrontView/Customer/Dialogue` | 1974 | M · 구형/placeholder 대사. 현재 Presenter는 새 DialoguePanel 대상을 사용 |
| 〃 | `OperatingPanel/PriceInput/TransactionStatus` | 2231 | M · 거래 상태, 런타임 갱신 |
| 〃 | `OperatingPanel/PriceInput/Continue/Label` | 2654 | M · 계속 버튼 |
| 〃 | `OperatingPanel/PriceInput/Price` | 3124 | M · 가격/금액 표시 |
| 〃 | `OperatingPanel/AstraFrontView/DialoguePanel/Dialogue` | 4117 | M · 현재 활성 대화 대상 |

`GameUI.prefab`에는 위 2개 외에 중첩 Prefab에서 들어온 `stripped` TMP 기록 4개가 있다. 대상은 `OperatingPanel/PriceInput/Validation`, `OperatingPanel/PriceInput/TransactionStatus`, `FailurePanel/FailureDescription`, `ErrorPanel/Error`의 상속 인스턴스 기록이다. 이 4개는 `GameUI.prefab`의 stripped YAML을 직접 고치지 말고 각 원본 Prefab을 수정해야 한다.

### 4.4 영업 전 화면

| 에셋 | 오브젝트 경로 | YAML | 폰트·용도 |
|---|---|---:|---|
| `Assets/Prefabs/GameUI/PreOpenPanel.prefab` | `PreOpenPanel/Products/ProductCardSlot6/Price` | 48 | M · 상품 가격 |
| 〃 | `PreOpenPanel/DebugDay30/Label` | 185 | M · 디버그 일차 버튼 |
| 〃 | `PreOpenPanel/DayPaper/InstructionDay` | 398 | M · 일차 안내 |
| 〃 | `PreOpenPanel/MemoryHeading` | 535 | M · 기억 제목 |
| 〃 | `PreOpenPanel/RuleTitle` | 672 | M · 규칙 제목 |
| 〃 | `PreOpenPanel/GuidelineSlot0` | 809 | M · 지침 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot0/Name` | 1059 | M · 상품명 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot0/Price` | 1196 | M · 상품 가격 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot1/Name` | 1446 | M · 상품명 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot1/Price` | 1583 | M · 상품 가격 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot2/Name` | 1833 | M · 상품명 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot2/Price` | 1970 | M · 상품 가격 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot3/Name` | 2220 | M · 상품명 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot3/Price` | 2357 | M · 상품 가격 |
| 〃 | `PreOpenPanel/GuidelineNotice` | 2494 | M · 제한/주의 안내 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot7/Price` | 3063 | M · 상품 가격 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot4/Price` | 3200 | M · 상품 가격 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot5/Price` | 3458 | M · 상품 가격 |
| 〃 | `PreOpenPanel/DebugDay20/Label` | 3595 | M · 디버그 일차 버튼 |
| 〃 | `PreOpenPanel/GuidelineSlot1` | 3875 | M · 지침 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot6/Name` | 4193 | M · 상품명 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot4/Name` | 4330 | M · 상품명 |
| 〃 | `PreOpenPanel/OpenShopBorder/OpenBusiness/Label` | 4776 | M · 영업 시작 버튼 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot5/Name` | 4913 | M · 상품명 |
| 〃 | `PreOpenPanel/Products/ProductCardSlot7/Name` | 5050 | M · 상품명 |
| 〃 | `PreOpenPanel/DebugDay10/Label` | 5187 | M · 디버그 일차 버튼 |

### 4.5 정산·헌사 패널

| 에셋 | 오브젝트 경로 | YAML | 폰트·용도 |
|---|---|---:|---|
| `Assets/Prefabs/GameUI/SettlementPanel.prefab` | `SettlementPanel/DailyLedgerVisual/RightPageText` | 220 | M · 우측 장부, 런타임 갱신 |
| 〃 | `SettlementPanel/FinalConfirmationPanel/FinalConfirmButton/Label` | 478 | M · 최종 확인 버튼 |
| 〃 | `SettlementPanel/FinalConfirmationPanel/Message` | 690 | L · 최종 확인 한글 안내 |
| 〃 | `SettlementPanel/FinalConfirmationPanel/FinalCancelButton/Label` | 829 | M · 취소 버튼 |
| 〃 | `SettlementPanel/DailyLedgerVisual/LeftPageText` | 966 | M · 좌측 장부, 런타임 갱신 |
| 〃 | `SettlementPanel/SettlementNext/Label` | 1224 | M · 다음 단계 버튼 |
| `Assets/Prefabs/GameUI/TributePanel.prefab` | `TributePanel/TributeDescription` | 48 | M · 헌사 설명, 런타임 갱신 |
| 〃 | `TributePanel/TributeTitle` | 306 | M · 헌사 제목 |
| 〃 | `TributePanel/PayTribute/Label` | 521 | M · 결제 버튼 |

### 4.6 씬에 직접 저장된 TMP

| 씬 | 오브젝트 경로 | YAML | 폰트·용도 |
|---|---|---:|---|
| `Assets/Scenes/HubScene.unity` | `HubMenu/Title` | 159 | M · 타이틀 |
| 〃 | `HubMenu/Quit/Label` | 296 | M · 끝내기 버튼 |
| 〃 | `HubMenu/NewGame/Label` | 816 | M · 새 게임 버튼 |
| 〃 | `HubMenu/Status` | 1167 | M · 상태 문구, 런타임 갱신 |
| 〃 | `HubMenu/Subtitle` | 1304 | M · 부제 |
| `Assets/Scenes/LoadingScene.unity` | `Canvas/EndingRetry/Label` | 381 | M · 엔딩 다시 불러오기 버튼 |
| 〃 | `Canvas/LoadingBar/Text (TMP)` | 842 | L · 로딩 문구/진행률, 런타임 갱신 |

`Assets/Scenes/MainScene.unity`는 `GameUI.prefab`와 `CustomerWorld.prefab`의 인스턴스를 조립한다. 따라서 MainScene 인스턴스의 TMP를 개별 수정하지 말고 원본 Prefab을 수정한다. `GoodEndingScene.unity`와 `BadEndingScene.unity`는 같은 `EndingPanel.prefab`을 사용한다.

## 5. Prefab 컴포넌트와 TMP 필드 연결

다음은 폰트 교체 후 화면이 실제로 사용하는 필드를 역추적할 때의 기준표다.

| 에셋/대상 | 컴포넌트 | TMP 필드 | 연결 오브젝트 |
|---|---|---|---|
| `EndingPanel.prefab` | `EndingPresenter` | `heading` | `EndingPanel/Heading` |
| 〃 | 〃 | `speaker` | `EndingPanel/DialoguePanel/Speaker` |
| 〃 | 〃 | `dialogue` | `EndingPanel/DialoguePanel/Dialogue` |
| 〃 | 〃 | `pageIndicator` | `EndingPanel/DialoguePanel/Page` |
| `CommonHUD.prefab` | `GameDayPresenter` | `dayText`, `settlementText`, `phaseText` | `CommonHUD/Day`, `Settlement`, `Phase` |
| 〃 | `EconomyStatusPresenter` | `balanceText`, `dailyIncomeText` | `CommonHUD/Balance`, `Income` |
| `GameUI.prefab` | `ProgressSceneController` 직렬화 이름 / 현재 `GameUIController` | `startupErrorText` | `GameUI/ProgressCanvas/StartupCover/StartupError` |
| 〃 | 〃 | `validationText`, `transactionStatusText`, `failureText`, `errorText` | 각각 중첩 원본 `OperatingPanel`, `FailurePanel`, `ErrorPanel`의 stripped 대상 |
| `OperatingPanel.prefab` | `BusinessClockController` | `clockText` | `AstraFrontView/CounterClock/ClockText` |
| 〃 | `CustomerPresenter` | `temporaryGenderText`, `dialogueText` | `Customer/Appearance/TemporaryGender`, `DialoguePanel/Dialogue` |
| 〃 | `PriceInputPresenter` | `priceDisplayText`, `validationMessageText` | `PriceInput/Price`, `PriceInput/Validation` |
| 〃 | `KeypadController` | `priceDisplayText` | `PriceInput/Price` |
| `SettlementPanel.prefab` | `DailySettlementLedgerView` | `leftPageText`, `rightPageText` | `DailyLedgerVisual/LeftPageText`, `RightPageText` |
| `DaughterDialoguePanel.prefab` | `DaughterDialoguePresenter` | `dialogue` | `SpeechBubble/Dialogue` |
| `FacilityItem.prefab` | `FacilityItemView` | `nameText`, `priceText`, `unlockProductsText`, `activationText`, `statusText` | 같은 이름의 `NameText`, `PriceText`, `UnlockProductsText`, `ActivationText`, `StatusText` |
| `FacilityShopPanel.prefab` | `FacilityShopPresenter` | `titleText`, `balanceText`, `activationGuideText`, `feedbackText` | `Window/TitleText`, `BalanceText`, `ActivationGuideText`, `FeedbackText` |
| `InspectorPanel.prefab` | `InspectorPresenter` | `dialogue` | `Dialogue/Text` |
| `HubScene.unity` | `HubScene` | `statusText` | `HubMenu/Status` |
| `LoadingScene.unity` | `LoadingScene` | `progressText` | `Canvas/LoadingBar/Text (TMP)` |

`EndingPresenter`의 다음 버튼, `LoadingScene`의 재시도 버튼, 정산 패널의 다음 단계 버튼처럼 일부 버튼 라벨은 전용 직렬화 필드 없이 `GetComponentInChildren<TMP_Text>()`로 찾아 런타임에 문구를 쓴다. 버튼의 자식 TMP도 위 정적 목록에 포함했지만, 코드 검색 시 이 패턴을 함께 확인해야 한다.

## 6. 런타임 한글 출력·TMP 생성 경로

| 코드 위치 | 현재 대상/필드 | 동작과 폰트 교체 시 확인점 |
|---|---|---|
| `Assets/Scripts/Scene/GameUIController.cs:52-65` | `startupErrorText`, `validationText`, `transactionStatusText`, `failureText`, `errorText` | 오류·검증·거래·영업권 상실 문구를 런타임에 씀. `priceListText`는 현재 `{fileID: 0}`으로 미연결 |
| `Assets/Scripts/Scene/GameUIController.cs:576,624-649,1329-1336` | 위 GameUI 메시지 | 한글 실패/대기/오류 문구가 코드에 있다. 오브젝트 폰트뿐 아니라 초기화 시점도 검증해야 함 |
| `Assets/Scripts/UI/Presenters/CustomerPresenter.cs:24-37` | `temporaryGenderText`, `dialogueText`, `dialogueFont` | 성별·손님 대사를 출력. `dialogueFont`가 별도 폰트 입력이며 현재 OperatingPanel에서 Mabinogi를 받음 |
| `Assets/Scripts/UI/Presenters/CustomerPresenter.cs:347-381` | `Customer/Appearance/TemporaryGender` | 없으면 TMP를 만들고 대화 TMP의 폰트·머티리얼을 복사함 |
| `Assets/Scripts/UI/Presenters/CustomerPresenter.cs:408-429,485-551` | `AstraFrontView/DialoguePanel/Dialogue` | 말풍선 TMP를 찾거나 만들고 `dialogueFont`를 적용. 에디터 폴백 경로가 Mabinogi 고정 |
| `Assets/Scripts/Scene/CustomerWorldQueueView.cs:16,200-207,232` | `CustomerWorldQueueView.font` → `Queue Speech` `TextMeshPro` | 월드 대기열의 3D TMP를 런타임 생성하고 TextDataTable 한글 대사를 넣음. 현재 `CustomerWorld.prefab:669`에서 Mabinogi를 받음 |
| `Assets/Scripts/Scene/CustomerQueueView.cs:25,176-179,235` | `font` → `Speech` `TextMeshProUGUI` | 구형/코드 전용 대기열 경로. 현재 씬·Prefab 직렬화 소비자는 확인되지 않았으나, 재활성화 시 폰트 지정 필요 |
| `Assets/Scripts/Scene/EndingPresenter.cs:91-130` | 엔딩 제목·화자·대사·페이지·버튼 라벨 | `새 게임`, 엔딩 대사, `마무리`, `다음` 등을 런타임에 갱신 |
| `Assets/Scripts/Scene/HubScene.cs:12,48,72` | `HubMenu/Status` | 허브 상태 문구를 런타임에 갱신 |
| `Assets/Scripts/Scene/LoadingScene.cs:12,46-86` | `Canvas/LoadingBar/Text (TMP)` | 로딩 상태와 퍼센트를 런타임에 갱신. 재시도 버튼 라벨도 자식 TMP를 갱신 |
| `Assets/Scripts/Scene/NewGameButton.cs:36` | 버튼 자식 TMP | `메뉴 복귀 다시 시도`를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/DailySettlementPresenter.cs:129-130` | 정산 다음 단계 버튼 자식 TMP | `최종 확인`/`다음 날`을 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/DailySettlementLedgerView.cs:10-13,68-117` | 좌·우 장부 TMP | 장부를 지우고 런타임 데이터로 다시 채움 |
| `Assets/Scripts/UI/Presenters/DaughterDialoguePresenter.cs:14,54,67,85` | 딸 대사 TMP | TextDataTable 대사를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/FacilityItemView.cs:10-20,41-55` | 설비명·가격·해금·상태 TMP | 설비 데이터와 상태 문구를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/FacilityShopPresenter.cs:11-17,88-92` | 상점 제목·잔액·안내·피드백 TMP | 구매 상태와 한글 안내를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/GameDayPresenter.cs:13-19,28-38` | HUD 일차·단계·정산 TMP | 날짜/영업 단계/정산 상태를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/EconomyStatusPresenter.cs:13-16,26-31` | HUD 잔액·수입 TMP | 경제 상태를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/InspectorPresenter.cs:17,109` | 감독관 대사 TMP | 데이터 대사를 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/PreOpenPanelPresenter.cs:21-46,91-191` | 영업 전 화면 TMP | 일차·규칙·상품명·가격·지침을 런타임에 씀 |
| `Assets/Scripts/UI/Presenters/PriceInputPresenter.cs:15-18,69-76,137` | 가격·검증 TMP | 가격과 한글 검증 문구를 런타임에 씀 |
| `Assets/Scripts/UI/KeypadController.cs:21` | `PriceInput/Price` | 키패드 입력 금액을 같은 TMP에 씀. 통화 문자열은 데이터/포매터 결과까지 확인 |

## 7. 에디터 자동설정·재생성 경로

정적 Prefab을 한 번 바꿔도 아래 코드가 다시 실행되면 폰트가 기존 값으로 복원될 수 있다.

| 코드 위치 | 영향 |
|---|---|
| `Assets/Scripts/Scene/Editor/SaleSortingPrefabSetup.cs:42` | `MabinogiFontPath = "Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset"`를 하드코딩 |
| `Assets/Scripts/Scene/Editor/SaleSortingPrefabSetup.cs:800-884` | `AstraFrontView/DialoguePanel/Dialogue`를 만들거나 찾고 Mabinogi 폰트·머티리얼을 설정 |
| `Assets/Scripts/Scene/Editor/SaleSortingPrefabSetup.cs:916,976-979` | `CustomerPresenter.dialogueFont`에 Mabinogi를 연결하고 다시 설정 |
| `Assets/Scripts/Scene/Editor/SaleSortingPrefabSetup.cs:447` | `ClockText` 생성 경로. 명시적 폰트가 없으면 TMP 기본 폰트/현재 저장값에 의존 |
| `Assets/Scripts/Scene/Editor/SaleSortingPrefabSetup.cs`의 `SaleSortingPanel` 설정 | `sortingStatusText`를 현재 null로 둔다. TMP를 생성·연결하지 않지만 코드에는 한글 상태 메시지가 있음 |
| `Assets/Scripts/Scene/Editor/EndingAssetSetup.cs:19,29,92,151` | 별도 경로에 폰트를 하드코딩하지 않고 `DaughterDialoguePanel`의 TMP 폰트를 읽어 엔딩·허브 생성에 사용 |
| `Assets/Scripts/Scene/Editor/EndingAssetSetup.cs:270-277` | 생성하는 TMP 라벨에 위에서 읽은 폰트를 적용. 원본 딸 대사 폰트가 생성 결과의 기준점 |

실제 교체 시에는 `EndingAssetSetup`의 폰트 취득 기준을 의도적으로 유지할지, 공식 Mulmaru SDF를 명시적으로 참조하도록 바꿀지 결정해야 한다. 현재 조사에서는 코드를 수정하지 않았다.

## 8. 미연결·구형·비활성 경로

| 위치 | 상태 | 교체 시 판단 |
|---|---|---|
| `Assets/Prefabs/GameUI/GameUI.prefab`의 `ProgressSceneController.priceListText` | `{fileID: 0}` | 현재 TMP 오브젝트에 연결되지 않음. 가격 목록 기능을 되살릴 때 별도 연결과 폰트 지정 필요 |
| `Assets/Prefabs/GameUI/GameUI.prefab`의 `SaleSortingPanel.sortingStatusText` | `{fileID: 0}`. `SaleSortingPrefabSetup`도 null로 설정 | 코드에는 `물품을 쏟는 중…`, 분류/판매 상태 한글이 있음. 현재 제작용 TMP 위치로 세지 않았지만 기능 활성화 전 재조사 필요 |
| `Assets/Scripts/Scene/CustomerQueueView.cs` | TMP 생성 코드만 있고 현재 Prefab/Scene 소비자 없음 | 구형 대기열 경로. 코드가 다시 연결되면 `font` 직렬화/할당 확인 |
| `Assets/Scripts/UI/PriceListPanel.cs`, `PriceItemSlot.cs` | 상품명·가격·특수문구 TMP 필드는 있으나 현재 제작용 Prefab/Scene 소비자 없음 | 레거시 후보. 삭제·활성화는 별도 승인/작업으로 분리 |
| `Assets/Scripts/UI/DailyResultPanel.cs` | TMP 필드와 평판 결과 출력은 있으나 현재 제작용 Prefab/Scene 소비자 없음 | 레거시 후보 |
| `Assets/Scripts/UI/DayTimerController.cs` | TMP 필드는 있으나 현재 제작용 소비자 없음. 숫자 시간 출력 중심 | 한글 폰트 교체 핵심 대상은 아니지만 재사용 시 확인 |
| `Assets/Scripts/UI/Dev3SandboxTester.cs` | 관련 TMP 코드가 주석 처리됨 | 라이브 UI 목록에서 제외 |
| `Assets/DystopiaPrototype/**` | TMP가 아닌 `UnityEngine.UI.Text`/`Font` 기반. 일부 코드가 `Mulmaru.otf`를 직접 읽음 | TMP 교체와 별도. 구형 UI 폰트 마이그레이션을 할 때 별도 조사 |

## 9. 나중에 교체할 때의 권장 순서

1. Mulmaru 후보 SDF 두 개 중 공식 기준 에셋과 GUID를 결정한다. 동일 해시라도 프로젝트 기준 경로를 하나로 정한다.
2. 현재 Mabinogi 78개가 있는 원본 Prefab과 씬 직렬화 컴포넌트를 교체한다. MainScene의 중첩 인스턴스와 `stripped` 기록은 직접 편집하지 않는다.
3. `CustomerWorld.prefab`의 `CustomerWorldQueueView.font`를 교체한다.
4. `OperatingPanel.prefab`의 `CustomerPresenter.dialogueFont`를 교체한다.
5. `CustomerPresenter.cs`의 에디터 폴백 경로와 `SaleSortingPrefabSetup.cs`의 Mabinogi 하드코딩 경로를 교체하거나, 공식 폰트 에셋을 주입하는 방식으로 정리한다.
6. `EndingAssetSetup.cs`가 읽는 기준 폰트가 의도한 Mulmaru인지 확인한다. 생성기를 다시 실행할 경우 생성된 엔딩·허브·버튼 라벨을 재확인한다.
7. LiberationSans를 쓰는 `ClockText`, 정산 최종 확인 `Message`, Loading TMP를 한글 폰트 교체 범위에 포함할지 결정하고, 포함한다면 Mulmaru로 통일한다.
8. 다음 화면을 실제 실행 또는 Unity 씬 검증으로 확인한다: MainScene의 영업 전/영업 중/정산, 월드 대기열 3D 대사, HubScene, LoadingScene, GoodEndingScene, BadEndingScene, 설비 상점, 실패·오류·검사 패널.
9. 마지막으로 TMP 폰트 에셋, 폰트 머티리얼, fallback/atlas 설정, 해상도별 줄바꿈·클리핑·버튼 라벨을 확인한다. 특히 한글 글리프가 atlas에 포함되어야 한다.

## 10. 조사 검증 기록

- `Assets/Prefabs`와 `Assets/Scenes`의 YAML을 정적으로 파싱해 TMP 컴포넌트와 오브젝트 경로를 확인했다.
- `TMP_FontAsset` GUID, `m_fontAsset`, `font`, `dialogueFont`, 에디터 폰트 경로를 교차 검색했다.
- 현재 Mulmaru 후보 SDF/OTF는 제작용 소비자 참조가 없음을 확인했다.
- 현재 작업 트리의 기존 변경 파일은 보존했으며, 폰트·씬·Prefab·코드는 변경하지 않았다.
- 검증 상태: **STATIC PASS** — 정적 위치·참조 조사 완료. Unity 재임포트, 컴파일, 플레이 모드 시각 검증은 폰트 교체 작업이 아니므로 실행하지 않았다.

이 문서의 수량은 2026-09-16 현재 작업 트리 기준이다. Prefab 구조나 UI 생성 코드가 바뀌면 교체 전에 이 목록을 다시 정적 검색으로 갱신해야 한다.
