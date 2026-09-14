# 작업 계획: 사운드 클립의 ResourceData·Addressables 통합

## 1. 이 문서의 용도

이 문서는 새로운 작업 세션의 구현자에게 그대로 전달할 수 있는 실행 계획서다. 구현자는 이 문서만 전달받더라도 저장소 규칙을 다시 확인하고, 현재 사운드 호출부를 보존하면서 오디오 클립 로딩을 프로젝트 공용 리소스 흐름으로 통합해야 한다.

이 문서는 구현 승인이나 보호 변경 승인을 대신하지 않는다. 특히 신규 `ResourceData.idx` 배정과 Addressables group·address 변경은 아래 승인 게이트를 통과한 뒤 수행한다.

## 2. 작업 목표

현재 `SoundManager`가 `SoundLibrary.asset`에 직접 직렬화된 `AudioClip`을 조회하는 구조를 다음 구조로 변경한다.

```text
SoundKeys의 uint ResourceData ID
→ ResourceDataTable에서 Addressables address 조회
→ ResourceManager.LoadAssetAsync<AudioClip>
→ SoundManager가 부팅 중 클립을 캐시
→ 기존 PlayBgm / PlaySfx / PlayLoopSfx 호출
```

완료 후 게임플레이·UI 호출자는 파일명, Addressables address 또는 `AudioClip`을 직접 알지 않는다. 호출자는 `SoundKeys`의 숫자 ID만 전달하고, 경로 문자열은 `ResourceData.csv`만 소유한다.

## 3. 작업 시작 전 필수 절차

1. 저장소 루트의 `AGENTS.md`를 확인한다.
2. 다음 문서를 전부 읽고 적용한다.
   - `doc/WORK_RULES.md`
   - `doc/INDEX.md`
   - `doc/CODING_RULES.md`
   - `doc/DATA_RULES.md`
   - `doc/CSV_RULES.md`
   - `doc/PREFAB_RESOURCE_RULES.md`
   - `doc/RESOURCE_POOL_CONTRACT.md`
   - `doc/SOUND_INTEGRATION.md`
   - `doc/work/README.md`
3. 현재 branch, commit, dirty·untracked 파일과 stash를 확인한다.
4. 이미 존재하는 미커밋 변경은 사용자 작업으로 간주하고 보존한다. 특히 다음 변경은 작업 시작 시 다시 확인하며, 승인 없이 되돌리거나 덮어쓰지 않는다.
   - 사운드 호출이 추가된 `GameUIController`, `KeypadController`, 각 Presenter, `SaleSortingPanel`, `VacuumController`
   - `InitScene`의 `SoundManager` 생성 코드
   - `SoundManager`의 반복 SFX 구현
   - `Assets/Resources/SoundLibrary.asset`과 `Assets/Settings/SoundMixer.mixer`
   - `doc/SOUND_INTEGRATION.md`
5. 같은 Unity Editor를 다른 작업자가 조작 중인지 확인한다. 동일 파일 편집과 Unity compile/test 실행 담당자는 한 번에 한 명이어야 한다.

## 4. 현재 구조와 변경 이유

현재 구조는 다음과 같다.

```text
SoundKeys 문자열
→ SoundManager.PlayBgm/PlaySfx/PlayLoopSfx
→ SoundLibrary.TryGetClip
→ SoundLibrary.asset의 직접 AudioClip GUID 참조
```

`SoundManager`는 `Resources.Load<SoundLibrary>("SoundLibrary")`로 설정과 클립 목록을 읽는다. 이 구조는 다음 프로젝트 공용 계약을 우회한다.

- 도메인과 소비자는 `ResourceData.idx`를 참조한다.
- 실제 address 문자열은 `ResourceData.path`에서만 해석한다.
- 자산은 기존 `ResourceManager`를 통해 Addressables로 로드한다.
- 공유 자산의 로드, 실패, 타입 충돌과 해제는 `ResourceManager`가 소유한다.

이번 작업은 **오디오 클립 로딩만** 위 공용 경로로 통합한다. `AudioMixer`, 기본 볼륨과 SFX source 개수 같은 사운드 시스템 설정은 `SoundLibrary`에 남긴다.

## 5. 확정 범위

### 5.1 포함

- 사용 중인 BGM 3개와 SFX 16개를 `ResourceData.csv`에서 조회하도록 변경
- 해당 19개 오디오 파일을 Addressables에 등록
- `SoundKeys`의 값을 문자열에서 `uint ResourceData.idx`로 변경
- `SoundManager`에 명시적인 비동기 초기화 API와 클립 캐시 추가
- `InitScene`이 데이터 로드 뒤 사운드 초기화를 await하도록 부팅 순서 변경
- `SoundLibrary`에서 직접 `AudioClip` 목록 제거
- 현재 BGM, 일회성 SFX, 반복 SFX, 볼륨 동작 유지
- 관련 데이터·초기화·실제 로드 테스트 추가 또는 기존 테스트 확장
- `doc/SOUND_INTEGRATION.md`를 최종 계약에 맞게 갱신

### 5.2 제외

- 오디오 원본 삭제, 이동, 이름 변경 또는 재인코딩
- 미사용 후보 음원 등록
- `AudioMixer` group, exposed parameter 또는 믹싱 정책 변경
- 볼륨 저장 기능 신규 구현
- 새로운 CSV 종류(`SoundData.csv`) 도입
- 런타임 스트리밍, LRU, 장면별 unload 또는 메모리 최적화 정책 추가
- package, ProjectSettings, 씬 또는 prefab의 요구하지 않은 변경
- 기존 사운드 재생 타이밍이나 게임 판정 변경

## 6. 미확정 사항과 승인 게이트

### 6.1 ResourceData ID

현재 저장소의 `ResourceData.csv`는 4256까지 사용 중이지만, 4257 이후를 임의로 신규 배정하지 않는다.

구현 전에 Google Docs의 `CSV 종류 ID` 권위 문서와 현재 예약 현황을 확인하고, 사용 중인 19개 오디오에 대한 연속 또는 명시적 ID 목록을 승인받는다. 접근할 수 없거나 승인 여부를 확인할 수 없으면 다음 작업만 수행하고 CSV·코드 상수에는 숫자를 쓰지 않는다.

- 코드 변경 초안 작성
- 필요한 매핑 목록과 Addressables 변경 목록 작성
- 테스트 설계
- 승인 필요 사항 보고

승인된 ID가 없는데 임시 ID, 0, 기존 리소스 ID 또는 문자열 키 fallback을 넣어 실행 가능하게 만들지 않는다.

### 6.2 Addressables

`Assets/AddressableAssetsData` 변경은 보호 변경이다. 작업 전에 해당 변경 권한을 확인하고, 기본 branch 통합 전 리소스 역할 검토와 프로그래머 교차 검토를 기록한다.

기본 address는 프로젝트 규칙에 따라 확장자를 제외한 실제 파일명으로 사용한다. 기존 address와 충돌하거나 별도 영문 address가 필요하다면 임의 변경하지 말고 이유와 모든 소비자 영향을 보고해 승인을 받는다.

### 6.3 공용 Manager/API

`SoundManager` 초기화 계약과 `InitScene` 부팅 순서 변경은 공용 manager/API 변경이다. 작업 branch에서 구현·검증할 수 있지만 기본 branch 통합 전에 작업자 외 프로그래머 `Primary` 1명 이상의 검토와 명시적 동의가 필요하다.

## 7. 사운드 리소스 매핑

아래 논리 이름과 파일의 연결은 현재 `SoundLibrary.asset` 및 `doc/SOUND_INTEGRATION.md`를 기준으로 확정한다. `ResourceData.idx` 열은 승인된 숫자로만 채운다.

| SoundKeys 이름 | ResourceData.idx | Addressables address 기본값 | 실제 파일 |
|---|---:|---|---|
| `SupervisorBgm` | 승인 필요 | `SupervisorBgm` | `Assets/Sounds/BGM/SupervisorBgm.mp3` |
| `GameplayAmbience` | 승인 필요 | `GameplayAmbience` | `Assets/Sounds/BGM/GameplayAmbience.mp3` |
| `SettlementBgm` | 승인 필요 | `SettlementBgm` | `Assets/Sounds/BGM/SettlementBgm.mp3` |
| `TransactionSuccess` | 승인 필요 | `TransactionSuccess` | `Assets/Sounds/SFX/TransactionSuccess.wav` |
| `TransactionFail` | 승인 필요 | `TransactionFail` | `Assets/Sounds/SFX/TransactionFail.wav` |
| `CalculatorOpen` | 승인 필요 | `CalculatorOpen` | `Assets/Sounds/SFX/CalculatorOpen.wav` |
| `CalculatorButton` | 승인 필요 | `CalculatorButton` | `Assets/Sounds/SFX/CalculatorButton.wav` |
| `ItemPickup` | 승인 필요 | `ItemPickup` | `Assets/Sounds/SFX/ItemPickup.wav` |
| `ItemPlace` | 승인 필요 | `ItemPlace` | `Assets/Sounds/SFX/ItemPlace.wav` |
| `ItemRemove` | 승인 필요 | `ItemRemove` | `Assets/Sounds/SFX/ItemRemove.wav` |
| `BoxItemDrop` | 승인 필요 | `BoxItemDrop` | `Assets/Sounds/SFX/BoxItemDrop.wav` |
| `Vacuum` | 승인 필요 | `VacuumLoop` | `Assets/Sounds/SFX/VacuumLoop.wav` |
| `FacilityUpgrade` | 승인 필요 | `FacilityUpgrade` | `Assets/Sounds/SFX/FacilityUpgrade.wav` |
| `DailyGuideline` | 승인 필요 | `DailyGuideline` | `Assets/Sounds/SFX/DailyGuideline.wav` |
| `DayStart` | 승인 필요 | `DayStart` | `Assets/Sounds/SFX/DayStart.wav` |
| `DayEnd` | 승인 필요 | `DayEnd` | `Assets/Sounds/SFX/DayEnd.wav` |
| `ReputationStamp` | 승인 필요 | `ReputationStamp` | `Assets/Sounds/SFX/ReputationStamp.wav` |
| `LedgerWrite` | 승인 필요 | `LedgerWriteLoop` | `Assets/Sounds/SFX/LedgerWriteLoop.wav` |
| `DialogueVoice` | 승인 필요 | `DialogueVoice` | `Assets/Sounds/SFX/DialogueVoice.wav` |

다음 후보 파일은 이번 작업에서 `ResourceData`나 Addressables에 등록하지 않는다.

- `SupervisorBgmVariant2.mp3`
- `SupervisorBgmVariant3.mp3`
- `SettlementBgmVariant2.mp3`
- `SettlementBgmVariant3.mp3`
- `TransactionFailVariant2.wav`

## 8. 상세 구현 계약

### 8.1 `SoundKeys.cs`

- 기존 public 상수 이름은 유지한다. 호출부의 의미와 변경 범위를 보존하기 위해 클래스를 rename하지 않는다.
- 각 상수 타입을 `string`에서 승인된 `uint ResourceData.idx`로 변경한다.
- 초기화 대상 전체를 한 곳에서 열거할 수 있는 읽기 전용 목록을 제공한다.
- 목록에는 정확히 위 19개 ID가 한 번씩만 들어가야 한다.
- 호출부나 `SoundManager` 내부에 address 문자열 또는 파일명을 중복 선언하지 않는다.

의도한 API 형태:

```csharp
public static class SoundKeys
{
    public const uint SupervisorBgm = /* 승인된 ResourceData ID */;
    // 나머지 상수

    public static IReadOnlyList<uint> All { get; }
}
```

실제 구현은 프로젝트 코드 스타일을 따르되, 외부에서 목록 내용을 변경할 수 없어야 한다.

### 8.2 `SoundLibrary.cs`와 `SoundLibrary.asset`

- `SoundEntry`, `entries`, `TryGetClip`과 직접 `AudioClip` 참조를 제거한다.
- 다음 설정은 유지한다.
  - `AudioMixer`
  - `SfxSourceCount`
  - `DefaultMasterVolume`
  - `DefaultBgmVolume`
  - `DefaultSfxVolume`
- 이번 작업에서 asset과 class를 `SoundSettings`로 rename하지 않는다. 파일 rename·GUID 변경과 호출 범위 확대를 피한다.
- `SoundLibrary.asset`에는 오디오 클립 GUID가 남지 않아야 한다.
- `Resources.Load<SoundLibrary>`는 설정 로딩에만 사용한다. 오디오 클립은 절대 `Resources.Load`로 fallback하지 않는다.

### 8.3 `SoundManager.cs`

다음 상태를 명시적으로 소유한다.

- 준비 완료된 `IReadOnlyDictionary<uint, AudioClip>` 또는 외부에서 수정할 수 없는 동등한 캐시
- 진행 중 초기화 작업
- 초기화 완료 여부
- BGM source, 일반 SFX source pool, 반복 SFX source
- 현재 BGM clip 또는 현재 BGM resource ID

공개 초기화 API의 의도한 계약:

```csharp
public UniTask InitializeAsync(
    DataTableManager dataTables,
    CancellationToken cancellationToken = default);
```

구현 세부 조건:

1. `OnSingletonAwake`에서는 `SoundLibrary` 설정 로드, mixer group 검증과 `AudioSource` 생성만 한다.
2. `InitializeAsync`는 `ResourceManager.Instance`, 전달된 `DataTableManager`, `ResourceDataTable`과 `SoundKeys.All`을 검증한다.
3. 각 ID에 대해 `ResourceDataTable.TryGetResource` 또는 `GetResourcePath`로 address를 얻고, 빈 경로나 누락 ID를 오류로 처리한다.
4. 각 address를 `ResourceManager.Instance.LoadAssetAsync<AudioClip>(address, cancellationToken)`으로 로드한다.
5. null clip, 중복 ID, 중복 또는 빈 address, 잘못된 asset type을 성공으로 처리하지 않는다.
6. 로딩 중에는 지역 dictionary를 사용하고 19개가 모두 성공한 뒤에만 실제 캐시와 `isInitialized`를 한 번에 교체한다. 부분 성공 캐시는 재생에 공개하지 않는다.
7. 같은 시점의 중복 초기화 호출은 하나의 내부 작업을 공유한다.
8. 이미 성공한 뒤의 반복 호출은 멱등하게 즉시 완료한다.
9. 실패나 취소는 호출자에게 전달한다. 실패를 로그만 남기고 성공으로 바꾸지 않는다.
10. 실패한 초기화는 상태를 준비 완료로 만들지 않으며, 원인이 해결된 뒤 재시도할 수 있어야 한다.
11. 호출자의 cancellation은 그 호출자의 대기만 취소하고, 공유 로드 소유권은 기존 `ResourceManager` 계약을 따른다. 구현 시 manager 수명 token과 호출자 token의 의미를 구분한다.
12. `SoundManager`는 공유 `AudioClip`을 `Destroy`, `Addressables.Release` 또는 `ResourceManager.ReleaseAll`하지 않는다. 종료 시 자신의 dictionary와 `AudioSource` 참조만 정리한다.

재생 API 계약:

```csharp
public void PlayBgm(uint resourceIdx);
public void PlaySfx(uint resourceIdx, float volumeScale = 1f);
public void PlayLoopSfx(uint resourceIdx, float volumeScale = 1f);
public void StopLoopSfx(uint resourceIdx);
```

- 기존 재생 방식과 볼륨 clamp는 유지한다.
- 같은 BGM clip이 재생 중이면 재시작하지 않는다.
- 같은 loop SFX ID가 재생 중이면 위치를 유지하고 볼륨만 갱신한다.
- 반복 SFX dictionary key도 `uint`로 변경한다.
- 초기화 전 호출이나 등록되지 않은 ID는 예외로 게임을 중단시키지 않되, 문제를 추적할 수 있는 경고를 남긴다. 같은 원인으로 매 프레임 로그가 폭증하지 않도록 기존 호출 빈도를 확인하고 필요한 최소한의 중복 억제만 적용한다.
- address 문자열 기반 public overload나 `SoundLibrary` fallback을 추가하지 않는다.

### 8.4 `InitScene.cs`

부팅 순서를 다음과 같이 고정한다.

```text
필수 manager 존재 확인·SoundManager 생성
→ ResourceManager.InitAsync
→ DataTableManager.EnsureDataLoadedAsync
→ SoundManager.InitializeAsync
→ GameSessionManager 새 게임 초기화
→ 다음 Scene 전환
```

구현 조건:

- `DataTableManager.EnsureDataLoadedAsync` 성공 전에는 사운드 초기화를 시작하지 않는다.
- `SoundManager.InitializeAsync`를 `InitScene`의 destroy cancellation token과 함께 await한다.
- 사운드 필수 리소스가 하나라도 실패하면 부팅 완료나 Scene 전환을 수행하지 않는다.
- 오류를 임의로 삼키거나 사운드 없는 정상 부팅으로 바꾸지 않는다.
- `async void Start`의 기존 오류 관찰 방식이 프로젝트 전반과 충돌하는지 확인하되, 이번 작업과 무관한 부팅 시스템 리팩터링은 하지 않는다.

### 8.5 기존 호출자

상수 타입 변경 후 다음 호출자들이 컴파일되고 기존 타이밍을 유지하는지 확인한다.

- `Assets/Scripts/Scene/GameUIController.cs`
- `Assets/Scripts/UI/KeypadController.cs`
- `Assets/Scripts/UI/SaleSortingPanel.cs`
- `Assets/Scripts/UI/VacuumController.cs`
- `Assets/Scripts/UI/Presenters/DailySettlementLedgerView.cs`
- `Assets/Scripts/UI/Presenters/DaughterDialoguePresenter.cs`
- `Assets/Scripts/UI/Presenters/InspectorPresenter.cs`
- `Assets/Scripts/UI/Presenters/ReputationStampPresenter.cs`

호출 시점이나 분기 조건은 바꾸지 않는다. 컴파일을 위해 필요한 타입 반영 외에 이 파일들을 리팩터링하지 않는다.

### 8.6 `ResourceData.csv`

- 승인된 19개 PK와 위 표의 address를 새 행으로 추가한다.
- 기존 `idx,path` header, 순서, encoding과 기존 56개 행을 그대로 보존한다.
- 기존 PK, path 또는 행을 재번호화하지 않는다.
- PK는 Resource 대역이며 고유해야 한다.
- 모든 path는 비어 있지 않고 Addressables address와 정확히 일치해야 한다.
- 후보 음원은 추가하지 않는다.

### 8.7 Addressables

- 위 19개 실제 오디오 asset과 각 `.meta`의 기존 GUID를 보존한다.
- 승인된 기존 group에 entry를 추가한다. 임의로 새 group·schema·label을 만들지 않는다.
- address는 기본적으로 확장자를 제외한 파일명으로 설정하고 `ResourceData.path`와 정확히 맞춘다.
- address 중복, GUID 중복, missing entry와 잘못된 asset type을 확인한다.
- `ResourceManager`의 `TargetLabel = "Datas"`를 오디오 때문에 변경하지 않는다. 오디오는 명시적 address 로드로 가져온다.
- 부팅 시 오디오 번들 전체 사전 다운로드 정책이 별도로 필요해지면 이번 작업을 확대하지 말고 후속 안건으로 보고한다.

## 9. 예상 변경 파일

필수 후보:

- `Assets/Scripts/Sound/SoundKeys.cs`
- `Assets/Scripts/Sound/SoundLibrary.cs`
- `Assets/Scripts/Sound/SoundManager.cs`
- `Assets/Scripts/Scene/InitScene.cs`
- `Assets/Resources/SoundLibrary.asset`
- `Assets/Datas/ResourceData.csv`
- `Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset` 또는 승인된 실제 group 파일
- `doc/SOUND_INTEGRATION.md`
- 관련 EditMode 또는 PlayMode 테스트 파일

조건부 후보:

- 기존 호출자 파일: 상수 타입 변경만으로 컴파일되면 수정하지 않는다.
- `.meta`: 신규 파일을 만들 때만 Unity가 생성한 짝을 포함한다. 기존 asset `.meta`는 재생성하지 않는다.

금지:

- 무관한 scene/prefab/package/ProjectSettings 수정
- `Assets/Plugins` 수정
- 기존 오디오 원본 및 `.meta` 삭제·이동
- 광범위 포맷팅 또는 관련 없는 코드 정리

## 10. 구현 순서

1. 현재 dirty 파일과 기준 diff를 기록한다.
2. 승인된 ResourceData ID 19개와 Addressables 변경 권한을 확인한다.
3. 실제 오디오 파일·`.meta`·현재 `SoundLibrary.asset` GUID 매핑을 다시 대조한다.
4. `SoundKeys`를 승인된 `uint` ID 계약으로 변경한다.
5. `SoundLibrary`를 설정 전용으로 축소한다.
6. `SoundManager.InitializeAsync`와 원자적 캐시 공개를 구현한다.
7. `InitScene` 부팅 순서에 사운드 초기화 await를 연결한다.
8. `ResourceData.csv`에 승인된 행을 추가한다.
9. Unity Editor를 통해 오디오 Addressables entry를 등록하고 GUID·address를 확인한다.
10. `SoundLibrary.asset`에서 모든 직접 clip 참조가 제거됐는지 확인한다.
11. 컴파일 오류를 해결하되 호출 타이밍과 무관한 코드는 변경하지 않는다.
12. 아래 자동·런타임 검증을 실행한다.
13. `doc/SOUND_INTEGRATION.md`를 실제 최종 구조와 검증 결과에 맞춰 갱신한다.
14. diff allowlist, `git diff --check`, 변경 파일과 보호 변경 검토 상태를 확인한다.

## 11. 검증 계획

### 11.1 정적·데이터 검증

- `ResourceData.csv` header가 정확히 `idx,path`인지 확인
- 기존 행 56개가 변경되지 않고 승인된 19개만 증가했는지 확인
- 전체 PK 고유성, Resource 대역과 path 필수값 확인
- `SoundKeys.All`의 개수가 19이고 중복이 없는지 확인
- 모든 `SoundKeys` ID가 실제 `ResourceDataTable` 행으로 해석되는지 확인
- 모든 path가 Addressables entry 하나와 정확히 대응하는지 확인
- 모든 entry의 asset type이 `AudioClip`인지 확인
- `SoundLibrary.asset`에 `AudioClip` GUID나 `entries` 직렬화 데이터가 남지 않았는지 확인
- 후보 음원이 Addressables/ResourceData에 추가되지 않았는지 확인

### 11.2 자동 테스트

기존 테스트 assembly와 패턴을 우선 사용하고 새 package/framework를 추가하지 않는다.

최소 테스트 시나리오:

1. 실제 `ResourceData.csv`를 파싱했을 때 모든 사운드 ID가 존재하고 path가 비어 있지 않다.
2. 19개 사운드 ID가 모두 고유하다.
3. 실제 `ResourceManager`를 통해 19개 address를 `AudioClip`으로 로드할 수 있다.
4. 누락 ResourceData ID가 초기화를 실패시키고 부분 캐시를 공개하지 않는다.
5. 잘못된/누락 Addressables address가 초기화를 실패시키고 준비 완료 상태가 되지 않는다.
6. 취소된 초기화가 준비 완료로 바뀌지 않는다.
7. 성공 후 반복 초기화가 중복 로드나 상태 초기화를 만들지 않는다.
8. 초기화 성공 후 BGM, one-shot SFX와 loop SFX API가 null reference 없이 동작한다.
9. 같은 BGM과 같은 loop SFX의 중복 호출이 재생 위치를 불필요하게 초기화하지 않는다.
10. loop SFX 정지가 해당 source의 clip을 정리한다.

Addressables와 Unity 수명이 필요한 검증은 PlayMode 테스트로 둔다. 순수 매핑·중복·CSV 계약은 가능한 범위에서 EditMode로 검증한다.

### 11.3 Unity 검증

1. Unity reimport와 compilation 종료 대기
2. compile error 0 확인
3. 관련 EditMode/PlayMode 테스트 실행 수, 성공, 실패, skip 확인
4. InitScene부터 시작해 부팅 완료 후 다음 Scene으로 전환되는지 확인
5. Console에서 신규 error와 예상하지 않은 warning이 없는지 확인
6. 다음 대표 경로를 실제 재생으로 확인
   - 영업 시작 BGM과 `DayStart`
   - 계산기 버튼 SFX
   - 거래 성공 또는 실패 SFX
   - 청소기 loop 시작·정지
   - 정산 BGM, 장부 loop 시작·정지
7. 실제 음량, loop 경계, 첫 재생 지연과 체감 타이밍은 사용자 수동 확인 항목으로 분리 보고

테스트 0건, skip, 컴파일만 성공한 상태를 `PASS`로 보고하지 않는다.

## 12. 완료 조건

다음을 모두 만족해야 구현 완료다.

- 사용 중인 19개 사운드 클립이 승인된 `ResourceData.idx`를 가진다.
- 각 `ResourceData.path`가 유효한 Addressables `AudioClip` entry를 가리킨다.
- 호출부는 문자열 key/address/file name을 사용하지 않고 `uint` ID만 전달한다.
- `SoundLibrary`는 클립을 직접 참조하지 않고 설정만 보관한다.
- `SoundManager`가 데이터 로드 이후 19개 클립을 `ResourceManager`로 로드하고 전체 성공 뒤에만 준비 완료가 된다.
- 초기화 실패 시 Scene 전환으로 진행하지 않는다.
- 기존 BGM·SFX·loop·볼륨 동작이 유지된다.
- compile error 0과 최소 실행 검증을 완료한다.
- Addressables/공용 manager 변경의 필수 교차 검토 상태를 기록한다.
- 관련 문서와 실제 구현이 일치한다.
- 기존 미커밋 작업과 범위 밖 파일을 훼손하지 않는다.

## 13. 완료 보고 형식

구현자는 최종 보고에 다음을 포함한다.

```md
- 기준 branch/commit과 보존한 기존 dirty 변경:
- 실제 수정·생성 파일:
- 승인받아 사용한 ResourceData ID 목록:
- ResourceData ID → address → asset 경로 매핑:
- SoundManager 초기화·실패·재시도 계약의 실제 구현:
- Addressables group/address/label 변경:
- 테스트: 종류별 실행 수 / 성공 / 실패 / skip:
- Unity compile·Console·PlayMode 결과:
- 사용자 수동 확인이 남은 항목:
- 보호 변경 검토자와 승인 기록 위치:
- Git stage·commit·push·merge 각각의 실제 수행 여부:
- 남은 위험 또는 후속 작업:
```

## 14. 새 작업 세션에 전달할 실행 지시

새 세션에는 이 문서와 함께 다음 한 문장만 전달하면 된다.

> `doc/work/sound-resource-data-migration-plan.md`를 작업 명세로 사용해 사운드 클립의 ResourceData·Addressables 통합을 구현하고 검증해줘. 문서의 승인 게이트와 기존 dirty 변경 보존 조건을 지키고, 승인되지 않은 ID나 Addressables 변경이 필요하면 그 부분만 중단해 보고해줘.
