# 작업 상태: 계산 화면 손맛 사운드

- 사람 담당자: Kido
- 기준 브랜치: `tutorial-direction` 위 `claude/project-thread-cr2mm9`
- 허용 경로: `Assets/Sounds/SFX/` 신규 5개, `ResourceData.csv`, `Default Local Group`, `SoundKeys`, `SoundManager`, `SaleSortingPanel`, `KeypadController`, `CheckoutFeedbackSwitch`, `SoundResourceDataTests`
- 제외 범위: MainScene, 공용 프리팹 기본값. 모든 연출은 기본 꺼짐이며 `CheckoutFeedbackSwitch`가 있는 별도 씬에서만 켜진다.

## 사운드와 출처

원래 후보였던 Splice 샘플은 내려받을 수 없어 Kenney의 CC0 팩으로 대체했다. Kenney Impact Sounds 팩은 kenney.nl이 자동 다운로드를 막아 받지 못했고, OpenGameArt에 Kenney가 올린 아래 두 팩에서 골랐다.

| idx | 이름 | 원본 팩 / 파일 | 가공 |
| --- | --- | --- | --- |
| 4428 | CanLand | RPG Audio (`RPGsounds_Kenney.zip`) / `metalPot2.ogg` | 0.55초 자름, lowpass 3.8kHz, 끝 0.15초 fade |
| 4429 | WaterLand | RPG Audio / `bookPlace2.ogg` | 0.35초, lowpass 2.2kHz, fade |
| 4430 | BatteryLand | RPG Audio / `metalClick.ogg` | 0.4초, lowpass 6kHz, fade |
| 4431 | RegisterClunk | RPG Audio / `metalLatch.ogg` + `doorClose_2.ogg`(70ms 뒤, -5dB) | 0.7초, lowpass, fade |
| 4432 | ReceiptPrint | Interface Sounds (`kenney_interfaceSounds.zip`) / `scroll_002.ogg` | 1.0초, lowpass 3.5kHz, fade |

- 라이선스: CC0 1.0 (http://creativecommons.org/publicdomain/zero/1.0/), 제작 Kenney (www.kenney.nl). 표기는 의무가 아니다.
- 출처 페이지: https://opengameart.org/content/50-rpg-sound-effects, https://opengameart.org/content/interface-sounds
- 형식: 44.1kHz mono 16-bit WAV. importer 설정은 `ItemPlace.wav.meta`와 같다.
- 동전 소리는 기존 TransactionSuccess, 경고음은 기존 DayEnd를 재사용하므로 새 파일이 없다.

## 연결

- 쏟기 착지음(`SaleSortingPanel.spillFeedbackEnabled`가 켜졌을 때만): 상품 1004 통조림 → CanLand, 1001 물 → WaterLand, 1010 건전지 → BatteryLand, 그 외 ItemPlace.
- 가격 확정 시 RegisterClunk: `KeypadController.registerClunkEnabled`(기본 false), `CheckoutFeedbackSwitch.registerClunk`가 켠다.
- ReceiptPrint와 경고음은 등록만 했고, 영수증·도장 연출 작업에서 연결한다.

## 남은 작업

- 실제 플레이에서 음량·체감 확인. 더 맞는 소리가 필요하면 같은 이름으로 WAV만 교체한다(Kenney Impact Sounds 팩의 `impactMetal_light`, `impactPlate` 계열이 후보).
