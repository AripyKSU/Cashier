# 사운드 에셋 영문명 변경표

2026-09-14 기준으로 사운드 파일명을 영문 PascalCase로 통일했다. 파일과 `.meta`를 함께 이동해 기존 Unity GUID를 보존했으며, 당시 등록된 19개는 `ResourceData.csv` path와 Addressables address도 같은 영문 basename으로 변경했다. 2026-09-15에는 손님 도착 효과음 `CustomerBoxDrop`, 2026-09-17에는 사용자 제공 Hub·엔딩 BGM 3개를 신규 등록했다. 현재 전체 등록 수와 사용 위치는 [SOUND_INTEGRATION.md](SOUND_INTEGRATION.md)를 따른다.

## 변경된 에셋

| 구분 | 기존 파일명 | 변경 파일명 | ResourceData.idx | Addressables address | Addressable |
|---|---|---|---:|---|---|
| BGM | 감독관 브금.mp3 | SupervisorBgm.mp3 | 4257 | SupervisorBgm | 예 |
| BGM | 감독관 브금2.mp3 | SupervisorBgmVariant2.mp3 | - | - | 아니오, 후보 유지 |
| BGM | 감독관 브금3.mp3 | SupervisorBgmVariant3.mp3 | - | - | 아니오, 후보 유지 |
| BGM | 인게임 웅성웅성.mp3 | GameplayAmbience.mp3 | 4258 | GameplayAmbience | 예 |
| BGM | 정산화면.mp3 | SettlementBgm.mp3 | 4259 | SettlementBgm | 예 |
| BGM | 정산화면2.mp3 | SettlementBgmVariant2.mp3 | - | - | 아니오, 후보 유지 |
| BGM | 정산화면3.mp3 | SettlementBgmVariant3.mp3 | - | - | 아니오, 후보 유지 |
| BGM | title.mp3 | TitleBgm.mp3 | 4413 | TitleBgm | 예 |
| BGM | goodending.mp3 | GoodEndingBgm.mp3 | 4414 | GoodEndingBgm | 예 |
| BGM | badending.mp3 | BadEndingBgm.mp3 | 4415 | BadEndingBgm | 예 |
| SFX | 거래 성공시.wav | TransactionSuccess.wav | 4260 | TransactionSuccess | 예 |
| SFX | 거래 실패시.wav | TransactionFail.wav | 4261 | TransactionFail | 예 |
| SFX | 거래 실패시2.wav | TransactionFailVariant2.wav | - | - | 아니오, 후보 유지 |
| SFX | 계산기 열고 닫을 때 소리.wav | CalculatorOpen.wav | 4262 | CalculatorOpen | 예 |
| SFX | 계산기 버튼 누를때.wav | CalculatorButton.wav | 4263 | CalculatorButton | 예 |
| SFX | 물건 집을때.wav | ItemPickup.wav | 4264 | ItemPickup | 예 |
| SFX | 물건 놓을때.wav | ItemPlace.wav | 4265 | ItemPlace | 예 |
| SFX | 물건 제거 소리.wav | ItemRemove.wav | 4266 | ItemRemove | 예 |
| SFX | 박스에서 물건 쏟을때.wav | BoxItemDrop.wav | 4267 | BoxItemDrop | 예 |
| SFX | 청소기 사용중 반복.wav | VacuumLoop.wav | 4268 | VacuumLoop | 예 |
| SFX | 설비 업그레이드 소리.wav | FacilityUpgrade.wav | 4269 | FacilityUpgrade | 예 |
| SFX | 일일 지침 ui 뜰 때.wav | DailyGuideline.wav | 4270 | DailyGuideline | 예 |
| SFX | 하루 시작.wav | DayStart.wav | 4271 | DayStart | 예 |
| SFX | 하루 시간 끝날때.wav | DayEnd.wav | 4272 | DayEnd | 예 |
| SFX | 명성 도장 찍힐 떄 소리.wav | ReputationStamp.wav | 4273 | ReputationStamp | 예 |
| SFX | 가계부 텍스트 적힐때 반복 재생.wav | LedgerWriteLoop.wav | 4274 | LedgerWriteLoop | 예 |
| SFX | 대화 음성.wav | DialogueVoice.wav | 4275 | DialogueVoice | 예 |

## 연결 보존 확인

- 등록 대상 19개의 `.meta` GUID는 파일 이동 전후 동일하다.
- `ResourceData.csv`의 4257~4275 path는 위 변경 파일명의 확장자 제거 값과 일치한다.
- `Default Local Group.asset`의 19개 Addressables entry는 동일 GUID와 새 영문 address를 사용한다.
- 후보 5개는 이름만 변경하고 `ResourceData.csv`와 Addressables에는 계속 등록하지 않는다.
- 실제 `ResourceManager.LoadAssetAsync<AudioClip>` PlayMode 검증에서 등록 대상 19개를 모두 로드한다.
