# 작업 상태: Sound_Upgrade 사운드·설비 단계별 UI 병합 인계

- 마지막 확인: 2026-09-14 (Asia/Seoul)
- 소스 브랜치: `Sound_Upgrade`
- 소스 HEAD: `d00117948f446c3c8633c777452497197b7fa3c7`
- 분기 기준: `total_merge`의 `b63a8c2466b2203e43b59a7fae0ec498ca000411`
- 구현 커밋 순서: `68c154e` 사운드 에셋 → `f25beb1` ResourceData·Addressables·SoundManager → `04d8eab` 재생 시점 → `d001179` 설비 단계별 UI와 후속 UI·음성 정리
- 현재 상태: 위 HEAD 기준 작업 트리는 clean이다. 이 문서 작성 변경은 별도 미커밋 변경으로 남는다.
- 권위 명세: [사운드 통합](../SOUND_INTEGRATION.md), [사운드 파일명 변경표](../SOUND_ASSET_NAME_MIGRATION.md), [설비 통합](../FACILITY_INTEGRATION.md)
- 통합 규칙: [브랜치 최종 통합 규칙](../BRANCH_INTEGRATION_RULES.md), [UI 병합 보존 가이드](../UI병합_보존_가이드.md), [Prefab·리소스 규칙](../PREFAB_RESOURCE_RULES.md)

이 문서는 `Sound_Upgrade` 전체 파일을 대상 브랜치에 덮어쓰라는 지시가 아니다. 공통 조상과 최신 대상 브랜치를 비교해 아래 기능 계약을 의미 단위로 이관하고, 대상 브랜치의 다른 기능·Prefab 연결·CSV 행·Addressables entry를 함께 보존한다.

## 1. 병합 결과에서 반드시 보존할 기능

### 1.1 사운드 데이터·로딩 구조

다음 흐름을 하나의 배포 단위로 유지한다.

```text
SoundKeys의 ResourceData ID 4257~4275
→ ResourceData.csv의 영문 address
→ Default Local Group의 AudioClip entry
→ ResourceManager.LoadAssetAsync<AudioClip>
→ SoundManager의 전체 성공 후 읽기 전용 캐시 공개
→ 화면·진행 Presenter의 BGM/SFX 호출
```

- `SoundManager`는 `InitScene`에서 DataTable 로드 뒤 초기화한다. 부분 로드 결과를 공개하거나 각 Presenter가 Addressables를 직접 조회하게 바꾸지 않는다.
- `SoundLibrary.asset`에는 클립을 직접 넣지 않는다. AudioMixer, SFX source 수, 기본 볼륨만 저장한다.
- `SoundLibrary.asset` GUID `145186bcd1a768442bf197252658f721`, `SoundMixer.mixer` GUID `7e9a382e3bf605e408143c8fd95d394f`를 보존한다. `.meta`를 재생성하지 않는다.
- `ResourceData.csv`의 4257~4275 행과 `SoundKeys.All` 19개는 한 세트다. 일부 ID·클립만 선택 병합하지 않는다.
- Addressables에는 19개 정식 address만 확장자 없는 영문 파일명으로 등록한다. `Datas` label을 붙이지 않는다.
- Variant 5개(`SupervisorBgmVariant2/3`, `SettlementBgmVariant2/3`, `TransactionFailVariant2`)는 에셋과 `.meta`는 보존하되 ResourceData·Addressables에는 등록하지 않는다.
- `Default Local Group.asset`은 보호·공용 파일이다. 파일 전체의 ours/theirs를 선택하지 말고 이 브랜치의 AudioClip 19개 entry를 최신 대상 파일에 합친 뒤 기존 entry를 보존한다.

### 1.2 사운드 재생 시점과 정리

- 감독관, 영업, 정산 BGM의 상태별 전환을 유지한다. 같은 BGM 재요청은 재생 위치를 초기화하지 않는다.
- `DayEnd`는 21:00에 `Closing`으로 전환될 때 한 번 재생한다. 정산 화면 진입에서 중복 재생하지 않는다.
- `SettlementBgm`은 정산 화면에서 시작하고 다음 날 진행 요청 직전에 정지한다.
- 딸 대화 음성은 `PlaySfxForDuration`으로 0.35초만 재생하며, 다음 정산 연출로 넘어갈 때 명시적으로 정지한다.
- 감독관 대화 음성은 `PlaySfxUntilStopped`의 제어 가능한 source를 사용한다. 마지막 대사 확인으로 `AwaitingExit`에 들어갈 때, 패널 비활성화·파괴 때 `StopSfxForDuration`으로 정지한다.
- 청소기·가계부 필기처럼 반복되는 효과음은 시작/정지 쌍을 유지하고 화면 비활성·파괴·취소 경로에서 정리한다.
- 계산기, 상품 집기·배치·제거, 상자 투입, 거래 성공·실패, 설비 구매, 일일 지침, 하루 시작, 명성 도장의 호출 지점을 대상 브랜치의 최신 로직에 맞춰 의미 병합한다.

### 1.3 설비 진행 조건

- 날짜는 설비 구매 잠금 조건이 아니다. 일반 설비 효과가 다음 영업일부터 활성화되는 기존 계약은 유지한다.
- 현재 단계의 `ProductUnlock`과 `Convenience` 설비를 모두 보유해야 하단 진행 항목을 구매할 수 있다.
- 1단계 완료 후 12008(2단계 확장), 2단계 완료 후 12010(3단계 확장), 3단계 완료 후 12012(시민권)를 구매할 수 있다.
- 완료 판정은 ID 하드코딩이 아니라 `FacilityData`의 종류와 `required_store_stage`를 사용한다. 해당 단계 일반 설비가 0개면 완료로 간주하지 않는다.
- 선행 설비 미완료는 `StageLocked`나 잔액 부족과 구분한 `PrerequisiteLocked`로 반환한다. 서비스가 최종 판정 권위이며 UI 비활성화만으로 강제하지 않는다.
- 시민권 12012의 `required_store_stage`는 3이다. 기존 대상 브랜치 값이 1이면 이 브랜치의 3을 채택한다.
- `FacilityDataTable`은 각 단계에 일반 설비가 있고 단계별 진행 항목이 정확히 하나인지 검증한다.

### 1.4 설비 상점 3화면 UI

- `FacilityShopPanel.prefab`은 하나의 전체 목록이 아니라 `Stage1Panel`, `Stage2Panel`, `Stage3Panel` 중 현재 단계 하나만 활성화한다.
- 각 화면은 현재 단계 일반 설비 목록과 하단 진행 항목 하나를 분리해 표시한다.
  - 1단계: 일반 설비 + `2단계 업그레이드`
  - 2단계: 일반 설비 + `3단계 업그레이드`
  - 3단계: 일반 설비 + `시민권 구매`
- `FacilityShopViewData`의 `RegularItems`, nullable `ProgressionItem`, `CompletedRegularCount`, `RequiredRegularCount`를 보존한다. 기존 소비자를 위한 결합 `Items`는 호환 경로일 뿐 새 Presenter의 권위 목록이 아니다.
- 진행 항목은 `완료 수/필요 수`, 선행 잠김, 잔액 부족, 구매 가능, 구매 완료 상태를 구분한다. 텍스트나 버튼 이름을 ID로 역추적하지 않는다.
- 단계 확장 구매 성공 후 상점을 닫지 않고 새 현재 단계 화면으로 즉시 전환한다.
- 상점을 처음 열 때 1·2단계는 현재 단계 완료 안내를, 3단계는 시민권 가격·부족액을 표시한다.
- `FacilityShopPresenter`는 세 단계 panel의 직렬화 참조와 현재 단계 row 재사용을 소유한다. `GameUIController`는 구매 요청 전달·입력 잠금·상태 재조회만 맡고 가격이나 완료 조건을 재판정하지 않는다.
- `FacilityShopPanel.prefab`의 새 계층·RectTransform·버튼·Presenter fileID를 이 브랜치 자산 기준으로 삼되, 대상 브랜치의 다른 승인된 자식과 override는 삭제하지 않는다. Prefab을 unpack하거나 YAML ours/theirs 전체 선택으로 해결하지 않는다.

### 1.5 같은 커밋에 포함된 후속 UI 수정

`d001179`에는 설비 UI 외에 이미 승인된 두 후속 수정이 함께 있다. 전체 브랜치를 병합할 때 누락하지 않는다.

- 딸 대사 TMP를 말풍선 자식으로 두고 `HorizontalLayoutGroup + ContentSizeFitter`로 텍스트 preferred width와 좌우 여백에 맞춰 말풍선 폭을 조절한다.
- 감독관 마지막 대사에서 대화 음성을 명시적으로 정지한다.

## 2. 파일 묶음과 충돌 처리

| 묶음 | 필수 파일 | 병합 기준 |
|---|---|---|
| 사운드 API | `Assets/Scripts/Sound/*`와 `.meta`, `Assets/Resources/SoundLibrary.asset*`, `Assets/Settings/SoundMixer.mixer*` | 클래스·공개 API·GUID를 함께 이관한다. 대상에 별도 SoundManager가 생겼다면 중복 singleton을 만들지 말고 호출자를 한 권위 구현에 맞춘다. |
| 사운드 데이터 | `Assets/Datas/ResourceData.csv`, `Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset` | 4257~4275와 정식 AudioClip 19 entry만 의미 병합하고 대상의 다른 행·entry를 보존한다. 보호 변경 재검토가 필요하다. |
| 오디오 자산 | `Assets/Sounds/BGM/*`, `Assets/Sounds/SFX/*`와 모든 `.meta` | 본 파일과 meta를 한 쌍으로 유지한다. 같은 address·GUID 충돌을 검사한다. 후보 5개는 미등록 상태를 유지한다. |
| 초기화·호출자 | `InitScene.cs`, `GameUIController.cs`, Keypad/정산/딸/감독관/명성/소팅/청소기 Presenter·Controller | 파일 전체 교체 금지. 대상의 최신 상태 전이와 함께 재생·정지 호출을 의미 병합한다. |
| 설비 도메인 | `FacilityData.csv`, `FacilityData.cs`, `FacilityDataTable.cs`, `FacilityPurchaseResult.cs`, `FacilityService.cs` | 시민권 단계3, 데이터 기반 일반 설비 완료, `PrerequisiteLocked`를 한 계약으로 이관한다. |
| 설비 UI | `FacilityUIContracts.cs`, `ProgressViewDataFactory.cs`, `FacilityItemView.cs`, `FacilityShopPresenter.cs`, `GameUIController.cs`, `FacilityShopPanel.prefab` | 3화면 snapshot·표시·구매 후 전환과 Prefab 직렬화 연결을 함께 이관한다. |
| 회귀 검사 | `SoundResourceDataTests.cs*`, `SoundManagerIntegrationTests.cs*`, `FacilityTests.cs`, `GameSessionApiTests.cs`, `CustomerCsvTests.cs` | 대상 테스트의 최신 기대를 유지하면서 신규 사운드·설비 게이트 검사를 합친다. 테스트를 통과시키기 위해 제품 검증을 완화하지 않는다. |
| 영구 명세 | `SOUND_INTEGRATION.md`, `SOUND_ASSET_NAME_MIGRATION.md`, `FACILITY_INTEGRATION.md`, `DATA_CATALOG.md`, `CITIZENSHIP_ENDING.md`, 관련 UI 명세 | 현재 구현 계약을 보존하고 통합 후 실제 SHA·검증 결과만 갱신한다. |

다음은 병합 대상에서 제외한다.

- `.idea/.idea.Cashier/.idea/workspace.xml`: 개인 IDE 상태이므로 제품 병합에서 제외한다.
- `Assets/Scenes/Local/`, 개인 Scene 선택, `UserSettings`, `Temp` 결과물: 공유 병합에 넣지 않는다.
- 후보 오디오 5개의 Addressables/ResourceData 등록: 에셋 보존과 런타임 등록을 혼동하지 않는다.

## 3. 권장 통합 순서

1. 최신 대상 ref를 fetch한 뒤 대상 SHA, `Sound_Upgrade` SHA, 공통 조상을 다시 기록한다. 이 문서의 `total_merge` SHA를 최신 대상이라고 가정하지 않는다.
2. `ResourceData` 4257~4275, `SoundKeys`, 오디오 파일/meta, SoundLibrary/Mixer를 먼저 맞춘다.
3. `Default Local Group.asset`에 19개 정식 entry를 합치고 기존 entry·group schema·label을 보존한다.
4. `SoundManager`와 `InitScene` 초기화 경계를 이관한 뒤 각 UI·진행 호출자를 최신 대상 상태 전이에 맞춰 합친다.
5. 설비 CSV/DTO/table/service의 시민권 단계와 선행 조건을 먼저 이관한다.
6. 설비 ViewData/factory/presenter를 맞춘 뒤 `FacilityShopPanel.prefab`의 3화면 계층과 직렬화 참조를 연결한다.
7. `GameUIController`는 사운드와 설비 양쪽이 수정하므로 마지막에 한 번 의미 병합한다. 대상의 정산·감독관·딸·엔딩·입력 잠금 흐름을 삭제하지 않는다.
8. 테스트와 영구 명세를 실제 통합 결과에 맞춰 갱신하고 아래 검증을 수행한다.

단순 cherry-pick을 사용한다면 의존 순서는 `68c154e` → `f25beb1` → `04d8eab` → `d001179`이다. 그러나 대상 브랜치가 분기 기준 이후 공용 CSV, Addressables, `GameUIController`, 설비 UI를 수정했다면 cherry-pick 성공 여부와 무관하게 위 의미 병합 검사를 수행한다.

## 4. 병합 후 필수 검증

### 정적·데이터

- conflict marker와 중복 `SoundManager`/`SoundKeys`/API가 없다.
- SoundKeys·ResourceData·Addressables·실제 AudioClip GUID가 정식 19개 모두 일치하고 address가 중복되지 않는다.
- 후보 5개는 ResourceData와 Addressables에 없다.
- 모든 신규 asset과 `.meta`가 1:1이며 SoundLibrary/Mixer GUID가 유지된다.
- FacilityData에서 단계별 일반 설비가 존재하고 진행 항목이 단계마다 정확히 하나다. 시민권 요구 단계는 3이다.
- Prefab의 `Stage1Panel`/`Stage2Panel`/`Stage3Panel`, 진행 버튼, Presenter 직렬화 참조에 missing이 없다.
- `git diff --check`와 실제 변경 파일 allowlist 대조를 통과한다.

### Unity·자동 검사

- 정확한 통합 checkout을 Unity 6000.3.18f1로 reimport하고 compile error 0, 제품 Console error 0을 확인한다.
- EditMode에서 `SoundResourceDataTests`, `FacilityTests`, 관련 CSV 검사를 실행한다.
- PlayMode에서 `SoundManagerIntegrationTests`, 설비 구매·3화면 Controller 검사를 실행한다.
- 실행 수, 성공, 실패, skip, 미완료와 XML/log 경로를 [TESTING.md](../TESTING.md)에 기록한다. 실행하지 않은 검사를 PASS로 쓰지 않는다.

### 실제 화면

- InitScene부터 MainScene에 진입해 사운드 manager가 하나만 존재하고 19개 클립 초기화 실패가 없는지 확인한다.
- 감독관 → 영업 전 → 영업 → Closing → 정산 → 다음 날 흐름에서 BGM/SFX 시작·정지 시점과 중복 재생이 없는지 듣는다.
- 감독관 마지막 대사 버튼 뒤 음성이 남지 않고, 딸 대사·도장·다음 날 전환에서 이전 음성이 이어지지 않는지 확인한다.
- 정산에서 설비 상점을 열어 현재 단계 panel만 보이는지, 일반 설비 완료 전 진행 버튼이 잠기는지 확인한다.
- 일반 설비를 모두 구매한 뒤 단계 확장/시민권이 열리고, 확장 성공 즉시 다음 단계 화면으로 전환되는지 확인한다.
- 상점 뒤쪽 입력 차단, 닫기 후 정산 입력 복원, 재진입 listener 중복이 없는지 확인한다.
- 딸 말풍선이 짧고 긴 대사에서 텍스트 길이에 맞춰 변하는지 확인한다.

## 5. 현재 검증·Git 상태와 다음 담당자

- `d001179` 작성 후 Unity AssetDatabase refresh와 compile을 완료했고 당시 Console error 0을 확인했다.
- `git diff --check`는 당시 통과했다.
- 이번 인계 작성에서는 전체 EditMode·PlayMode와 실제 MainScene 사운드/설비 3화면 시나리오를 새로 실행하지 않았다. 따라서 현재 브랜치 구현의 인계 상태는 `STATIC PASS`이며 통합 결과는 별도로 다시 검증해야 한다.
- `Sound_Upgrade`의 위 커밋들은 로컬에 존재한다. 이 문서 작성 시점에 merge·push는 수행하지 않았다.
- 다음 병합 담당자는 최신 대상 브랜치에서 보호 변경인 Addressables와 공용 `GameUIController`를 교차 검토하고, 통합 SHA에 대해 자동·실화면 검증 결과를 기록한다.

