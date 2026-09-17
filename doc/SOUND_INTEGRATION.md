# Cashier 사운드 통합 명세

2026-09-15 손님 도착 연출 사운드를 추가했다. 기존 사운드 ID `4257~4275`와 주소는 유지하고 `CustomerBoxDrop`을 `4295`에 추가하여 ResourceData는 총 102행이다. 충돌한 상품 Sprite는 `4276~4291`을 사용하고, 매장 2단계 World 리소스는 `4297`로 이동했다. [통합 계약](MAINSCENE_INTEGRATION.md#거래-화면상품-16종-통합-2026-09-14)을 따른다.

기준: `SoundKeys`, `ResourceData.csv`, Addressables `Default Local Group`, `SoundManager`와 `Assets/Sounds` 에셋.
영문 파일명·address 변경 내역은 [SOUND_ASSET_NAME_MIGRATION.md](SOUND_ASSET_NAME_MIGRATION.md)에 기록한다.

## 로딩 구조

    SoundKeys의 uint ResourceData ID
    → ResourceDataTable에서 영문 path 조회
    → ResourceManager.LoadAssetAsync<AudioClip>
    → SoundManager가 전체 성공 후 읽기 전용 캐시 공개
    → 기존 PlayBgm / PlaySfx / PlayLoopSfx 호출

- SoundLibrary는 AudioMixer, SFX source 개수와 기본 볼륨만 보관한다.
- 오디오 클립은 SoundLibrary.asset에 직접 참조하지 않는다.
- SoundManager.InitializeAsync는 InitScene에서 CSV 로드 완료 후 await한다.
- 초기화 실패·취소 시 부분 캐시를 공개하지 않으며, 실패 후 재시도할 수 있다.
- 동시에 들어온 초기화 요청은 하나의 내부 작업을 공유한다.
- 공유 AudioClip의 로드·해제 소유권은 ResourceManager가 가진다.

## ID와 address 매핑

| SoundKey | ResourceData.idx | Addressables address | Asset |
|---|---:|---|---|
| SupervisorBgm | 4257 | SupervisorBgm | Assets/Sounds/BGM/SupervisorBgm.mp3 |
| GameplayAmbience | 4258 | GameplayAmbience | Assets/Sounds/BGM/GameplayAmbience.mp3 |
| SettlementBgm | 4259 | SettlementBgm | Assets/Sounds/BGM/SettlementBgm.mp3 |
| TransactionSuccess | 4260 | TransactionSuccess | Assets/Sounds/SFX/TransactionSuccess.wav |
| TransactionFail | 4261 | TransactionFail | Assets/Sounds/SFX/TransactionFail.wav |
| CalculatorOpen | 4262 | CalculatorOpen | Assets/Sounds/SFX/CalculatorOpen.wav |
| CalculatorButton | 4263 | CalculatorButton | Assets/Sounds/SFX/CalculatorButton.wav |
| ItemPickup | 4264 | ItemPickup | Assets/Sounds/SFX/ItemPickup.wav |
| ItemPlace | 4265 | ItemPlace | Assets/Sounds/SFX/ItemPlace.wav |
| ItemRemove | 4266 | ItemRemove | Assets/Sounds/SFX/ItemRemove.wav |
| BoxItemDrop | 4267 | BoxItemDrop | Assets/Sounds/SFX/BoxItemDrop.wav |
| CustomerBoxDrop | 4295 | CustomerBoxDrop | Assets/Sounds/SFX/CustomerBoxDrop.wav |
| Vacuum | 4268 | VacuumLoop | Assets/Sounds/SFX/VacuumLoop.wav |
| FacilityUpgrade | 4269 | FacilityUpgrade | Assets/Sounds/SFX/FacilityUpgrade.wav |
| DailyGuideline | 4270 | DailyGuideline | Assets/Sounds/SFX/DailyGuideline.wav |
| DayStart | 4271 | DayStart | Assets/Sounds/SFX/DayStart.wav |
| DayEnd | 4272 | DayEnd | Assets/Sounds/SFX/DayEnd.wav |
| ReputationStamp | 4273 | ReputationStamp | Assets/Sounds/SFX/ReputationStamp.wav |
| LedgerWrite | 4274 | LedgerWriteLoop | Assets/Sounds/SFX/LedgerWriteLoop.wav |
| DialogueVoice | 4275 | DialogueVoice | Assets/Sounds/SFX/DialogueVoice.wav |

모든 대상 address는 확장자를 제외한 영문 파일명이며, 기존 `Default Local Group`에 AudioClip GUID를 보존한 채 등록한다. `Datas` label은 사용하지 않는다.

## 재생 호출

- BGM: `PlayBgm(uint resourceIdx, float volumeScale = 1f)`, 같은 클립이 재생 중이면 위치를 유지하고 0~2 배율을 적용한다.
- 일반 SFX: `PlaySfx(uint resourceIdx, float volumeScale = 1f)`.
- 짧은 전용 SFX: `PlaySfxForDuration(uint resourceIdx, float durationSeconds, float volumeScale = 1f)`와 `StopSfxForDuration(uint resourceIdx)`를 사용한다. 딸 대화 음성은 0.35초로 제한하고 도장 연출 시작 전에 정지한다.
- 반복 SFX: `PlayLoopSfx(uint resourceIdx, float volumeScale = 1f)` 후 `StopLoopSfx(uint resourceIdx)`.
- 재생 전 초기화가 끝나지 않았거나 ID가 캐시에 없으면 예외 대신 중복 억제 경고를 남긴다.
- Master/BGM/SFX mixer routing과 볼륨 clamp는 기존 계약을 유지한다.

재생 시점 규칙:

- `DayEnd`는 `DayProgressState.Closing`으로 전환되는 21:00 경계에서 재생하며, 정산 화면 진입 처리에서는 재생하지 않는다.
- `SettlementBgm`은 정산 화면에서 시작하고 다음 날 버튼 요청 직전에 정지한다.

## 후보 에셋

다음 파일은 영문으로 이름만 통일했으며, 이번 통합에서 ResourceData와 Addressables에는 등록하지 않는다.

- `SupervisorBgmVariant2.mp3`
- `SupervisorBgmVariant3.mp3`
- `SettlementBgmVariant2.mp3`
- `SettlementBgmVariant3.mp3`
- `TransactionFailVariant2.wav`

## 검증

- 정적: 20개 ResourceData path, Addressables address, 실제 파일 GUID의 일치 여부와 후보 미등록 여부.
- EditMode: SoundKeys 20개 고유성, Resource 대역, 실제 CSV 20개 path 매핑.
- PlayMode: 20개 실제 AudioClip 로드, 동시·반복 초기화, 호출자 취소, 누락/잘못된 address 실패·재시도, BGM/SFX/loop 재생·정지.
