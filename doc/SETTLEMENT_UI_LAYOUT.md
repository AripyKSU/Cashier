# 정산 화면 UI 구성 및 편집 안내

이 문서는 정산 화면에서 플레이어가 Unity Inspector로 직접 위치·크기·폰트·이미지를 조정할 대상을 설명한다. 기능 연결과 상태 흐름은 [`SETTLEMENT_TECHNICAL.md`](SETTLEMENT_TECHNICAL.md)를 참고한다.

## 편집 대상 Prefab

- 전체 UI 조립: `Assets/Prefabs/GameUI/GameUI.prefab`
- 정산 본체: `Assets/Prefabs/GameUI/SettlementPanel.prefab`
- 딸 대사: `Assets/Prefabs/GameUI/Daughter/DaughterDialoguePanel.prefab`
- 정산 이미지: `Assets/Textures/UI/Dystopia/Settlement/`

실제 Scene은 `GameUI.prefab`을 통해 정산 본체와 딸 대사 Prefab을 조립한다. 공용 Scene에 직접 오브젝트를 복제하지 말고 원본 Prefab을 편집한다.

## 실제 조립 계층

```text
SettlementPanel
├─ DailyLedgerVisual
│  ├─ Background
│  ├─ LeftPageText
│  ├─ RightPageText
│  └─ ReputationStamp
├─ FacilityPamphletButton
├─ SettlementNext
└─ DaughterDialoguePanel
   ├─ Portrait
   ├─ SpeechBubble
   └─ Dialogue
```

설비 진입에는 `FacilityPamphletButton`만 사용한다. 이전 임시 `FacilityOpenButton`은 제거됐다.

## 직접 조정할 오브젝트

표의 좌표와 크기는 현재 임시 시작값이다. 최종 배치 기준이 아니므로 플레이 화면에 맞춰 변경할 수 있다.

| 오브젝트 | 현재 위치 | 현재 크기 | 주로 조정할 항목 |
|---|---:|---:|---|
| `DailyLedgerVisual/Background` | Stretch `(0, 0)` | Stretch `(0, 0)` | 가계부 배경 크기, 비율 |
| `LeftPageText` | `(-250, -35)` | `(360, 400)` | 왼쪽 페이지 영역, 폰트 크기, 행간 |
| `RightPageText` | `(250, -35)` | `(360, 400)` | 오른쪽 페이지 영역, 폰트 크기, 행간 |
| `ReputationStamp` | `(300, 80)` | `(125, 125)` | 그림 위 도장 위치와 표시 크기 |
| `FacilityPamphletButton` | `(-390, -120)` | `(140, 100)` | 팜플렛 위치, 크기, 최종 Sprite |
| `SettlementNext` | `(-312, -160)` | `(250, 58)` | 다음 날 버튼 위치, 크기, 라벨 |
| `DaughterDialoguePanel` | `(0, -6)` | `(-80, 110)` | 딸 대사 UI 전체 위치와 영역 |
| `Portrait` | `(0, 0)` | `(88, 110)` | 딸 그림 위치와 크기 |
| `SpeechBubble` | `(45, 0)` | `(-90, 0)` | 말풍선 범위와 딸 그림과의 간격 |
| `Dialogue` | `(50, 0)` | `(-120, -20)` | 대사 여백, 폰트 크기, 줄바꿈 |

현재 가계부와 대사 폰트는 `Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset`이다. 왼쪽·오른쪽 가계부는 24pt, 딸 대사는 20pt에서 시작한다.

## 이미지 리소스 대응

| 표시 대상 | 리소스 |
|---|---|
| 가계부 배경 | `DailyLedger.png` |
| 딸 말풍선 | `LedgerSpeechBubble.png` |
| 최저 명성 도장 | `LedgerStampNotorious.png` |
| 낮은 명성 도장 | `LedgerStampUnpopular.png` |
| 중립 명성 도장 | `LedgerStampNeutral.png` |
| 높은 명성 도장 | `LedgerStampPopular.png` |
| 최고 명성 도장 | `LedgerStampExcellent.png` |

`ReputationStamp`의 Image는 준비 단계에서 선택한 Sprite가 런타임에 들어가므로 Prefab의 Source Image가 비어 있어도 정상이다. `Portrait`는 정산 시작부터 항상 표시하며 `Assets/Textures/UI/Dystopia/Settlement/LedgerDaughter.png`를 Prefab Source Image로 고정한다.

## 팜플렛 아트 교체 방법

`FacilityPamphletButton`은 현재 흰색 `Image + Button` 임시 오브젝트다.

1. 최종 팜플렛 이미지를 정산 Texture 폴더에 Sprite로 import한다.
2. `SettlementPanel.prefab`의 `FacilityPamphletButton`을 선택한다.
3. `Image.Source Image`에 Sprite를 연결한다.
4. `RectTransform`의 위치와 크기를 조정한다.
5. `Button.Target Graphic`이 같은 Image를 계속 가리키는지 확인한다.

오브젝트 또는 Button 컴포넌트를 새로 만들 필요는 없다. 이름을 바꾸거나 `SettlementInteractionView`의 참조를 끊지 않는다.

## 조정해도 되는 연출 수치

| 컴포넌트 | 필드 | 현재값 | 의미 |
|---|---|---:|---|
| `DailySettlementLedgerView` | `charactersPerSecond` | `35` | 가계부 초당 글자 수 |
| `DailySettlementLedgerView` | `pageIntervalSeconds` | `0.25` | 왼쪽 완료 후 오른쪽 시작 간격 |
| `DaughterDialoguePresenter` | `charactersPerSecond` | `24` | 딸 대사 초당 글자 수 |
| `DaughterDialoguePresenter` | `nodDurationSeconds` | `0.8` | 등장 후 고개 움직임 시간 |
| `DaughterDialoguePresenter` | `nodAngleDegrees` | `6` | 고개 회전 각도 |
| `DaughterDialoguePresenter` | `nodDistancePixels` | `5` | 고개 이동 거리 |
| `ReputationStampPresenter` | `durationSeconds` | `0.45` | 도장 전체 연출 시간 |
| `ReputationStampPresenter` | `startScale` | `1.65` | 도장 시작 확대 배율 |
| `ReputationStampPresenter` | `impactRotationDegrees` | `4` | 충격 흔들림 각도 |

타이핑 속도와 연출 수치를 바꾸면 전체 정산 입력 해제 시점도 함께 달라진다.

## 편집 시 유지할 연결

- `DailySettlementLedgerView.leftPageText/rightPageText`
- `DaughterDialoguePresenter.portrait/speechBubble/dialogue`
- `ReputationStampPresenter.stampImage`와 도장 Sprite 5개
- `SettlementInteractionView.facilityPamphletButton/nextDayButton`
- `DailySettlementFlowController`의 정산 Presenter, 딸 Presenter, Interaction View
- 표시 전용 Image와 TMP의 `Raycast Target=false`
- 팜플렛과 다음 날 Image의 `Raycast Target=true`

위 참조를 유지하면 위치·크기·색·폰트·Sprite는 Inspector에서 자유롭게 조정할 수 있다.

## 정리된 이전 UI

가계부 도입 전에 사용하던 항목별 정산 TMP, 명성 문구와 구형 설비 버튼은 제거됐다. 현재 `SettlementPanel`의 직접 표시 자식은 `DailyLedgerVisual`, `FacilityPamphletButton`, `SettlementNext`, 조립된 `DaughterDialoguePanel`뿐이다.
