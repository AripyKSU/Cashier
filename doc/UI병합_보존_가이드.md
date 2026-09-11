# UI 병합 보존 가이드

이 문서는 손님 전면 화면(AstraFrontView), 대화창, 상자 착지 연출, 시계 배치, **시간 경과에 따른 배경 변화(TimeOfDay)**, 그리고 **작업대 상자 물품 쏟기 연출(Item Pouring Animation) 및 아이템 뷰** 기능을 다른 브랜치와 병합할 때 **반드시 유지되어야 하는 자산 계층, 직렬화 수치, 로직 규격 및 검증 체크리스트**를 정의합니다.

기준 문서: [`doc/BRANCH_INTEGRATION_RULES.md`](BRANCH_INTEGRATION_RULES.md), [`AGENTS.md`](../AGENTS.md)

2026-09-11 감독관 통합: 편집기 리로드에서 `SetupDialoguePrefab()`을 자동 실행하는 updater는 제거한다. 브랜치 전환 시 구형 import 상태가 prefab 참조를 덮어쓰는 문제를 방지하며, 필요한 설치는 기존 수동 메뉴로 실행한다. 영업 시각의 최신 계약은 아래 B절을 따른다.

---

## 1. 핵심 보존 항목 요약

2026-09-11 Sprite world 후속 계약: 배경/캐노피/손님의 CustomerWorld 전환은 개인 SpriteWorldSandbox에서만 적용한다. MainScene·OperatingPanel은 total_merge 원본으로 복원하여 기존 UI controller를 유지한다. WorldSceneView는 TimeOfDayUIController의 공통 정적 곡선을 사용한다. UI CityLights 원본은 보존하고 별도 `Assets/Shaders/WorldCityLights.shader`·material이 URP Sprite 색/alpha 경로로 같은 chroma mask를 적용한다. Counter·CounterLight는 UI 예외다. 향후 공유 UI에 적용할 변경과 보존 대상은 [MainScene](MAINSCENE_INTEGRATION.md#월드-표시-분리-2026-09-11)을 따른다.

월드 단축키는 `enableDebugKeys`를 켜면 [ / ] / 역슬래시 / T로 사용한다. `debugOverrideTime`·`debugHour`·`autoAdvanceClockForTesting`·45초 전체 주기를 유지하며 실제 표시 시계는 바꾸지 않는다. 고급 PixelStage relighting은 도입하지 않는다.

| 대상 | 핵심 유지 내용 | 관련 파일 |
|---|---|---|
| **배경 시간대 전환 (TimeOfDay)** | 09:00~21:00 시간 경과에 따른 배경 페이드(아침/주간/석양/야간), 도시 불빛·가판대 조명·탐조등 점등, 환경광/인물 틴트 제어 | `TimeOfDayUIController.cs`<br>`BusinessClockController.cs`<br>`Assets/DystopiaPrototype/Art/TimeOfDay/*` |
| **대화창 & 폰트** | `DialoguePanel` 최상단 계층 순서 보존, 마비노기 SDF 폰트 직렬화 연결, 손님 대사별 자동 표시/숨김 | `OperatingPanel.prefab`<br>`CustomerPresenter.cs`<br>`DialogueFrame.png` |
| **상자 착지 흙먼지** | 상자 밑면 매대 접점(`Y ≈ -578`) 기반 정확한 착지 위치 계산, 10개 픽셀 먼지의 좌우 자연스러운 비산 궤적 | `LandingDustEffect.cs`<br>`SaleSortingPanel.cs` |
| **상자 & 시계 정렬** | 1280 해상도 기준 정중앙선(**Center X = 640**) 일치, 상자 매대 착지(`Y = -350`), 시계 매대 전면부 중앙 배치(`Y = -605`) | `OperatingPanel.prefab`<br>`SaleSortingPrefabSetup.cs` |
| **상자 물품 쏟기 & 아이템 뷰** | 상자 틸트 회전(-90° -> -100°)과 함께 물품이 우르르 쏟아져 나오는 애니메이션, 빈 상자 퇴장(Fade), 아이템 뷰 드래그 및 영역 판정 피드백 | `SaleSortingPanel.cs`<br>`SaleSortingItemView.cs`<br>`TopDownContainerTilted.png`<br>`TopDownContainerEmpty.png` |
| **단위 테스트** | TimeOfDay 시간 반영/블렌딩, 착지 접점 계산, 말풍선 제어, 아이템 뷰 드래그/상태 전환 검증 테스트 보존 | `BusinessClockAndSortingTests.cs` |

---

## 2. 세부 구현 사양 및 직렬화 유지 가이드

### A. 작업대 상자 물품 쏟기 연출 (Item Pouring) & 아이템 뷰

1. **상자 쏟기 연출 흐름 (`SaleSortingPanel.playEntryFlow`)**:
   * 전면 화면에서 작업대로 전환 후, 기울어진 상자(`PouringContainer`)가 활성화됩니다.
   * `pourSeconds`(기본 0.6초) 동안 상자가 `-90°`에서 `-100°`로 추가 틸트되면서 대기 중이던 상품들이 상자 입구(`getPourStartPosition`)에서 작업대 위 목표 지점(`getInitialSpreadPosition`)으로 우르르 쏟아져 나오는 보간 애니메이션이 실행됩니다:
     ```csharp
     // 상자 틸트 회전
     this.containerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-90f, -100f, t));
     
     // 물품들이 상자 입구에서 작업대로 쏟아져 나옴
     for (int i = 0; i < this.items.Count; i++)
     {
         SaleSortingItemView item = this.items[i];
         Vector2 target = this.getInitialSpreadPosition(i, this.items.Count);
         item.Position = Vector2.Lerp(this.getPourStartPosition(i), target, t);
     }
     ```
   * 쏟기가 완료되면 `emptyContainerSprite`(`TopDownContainerEmpty.png`)로 교체된 뒤, 상자가 좌측 상단으로 이동하며 투명하게 페이드아웃 퇴장합니다.
   * 쏟아진 물품들에는 자연스러운 분산 초속도(`Velocity = Random.insideUnitCircle * 20f`)가 부여됩니다.
2. **필수 스프라이트 연결**:
   * `tiltedContainerSprite`: `Assets/DystopiaPrototype/TopDownTest/Art/TopDownContainerTilted.png`
   * `emptyContainerSprite`: `Assets/DystopiaPrototype/TopDownTest/Art/TopDownContainerEmpty.png`
3. **아이템 뷰 동작 (`SaleSortingItemView.cs`)**:
   * 생성된 아이템의 `Image.raycastTarget = true`를 보장하여 드래그 앤 드롭 조작이 즉시 가능해야 합니다.
   * 드래그 중 및 판매 영역(`SaleZone`) / 제외 영역(`ExcludedZone`) 배치 시의 상태 전이와 하이라이트 색상 피드백을 유지합니다.

---

### B. 배경 시간대 전환 시스템 (TimeOfDay & BusinessClock)

1. **시간 권위 (2026-09-11 감독관 통합 계약)**:
   * 실제 진행·일시정지·마감은 `DayProgress`가 소유합니다. `BusinessHours`의 09:00~21:00을 진행 비율에 맞춰 표시하며 실제 영업 기본30초는 유지합니다.
   * `GameUIController`가 `BusinessClockController.DisplayTime(minutes)`으로 표시를 동기화합니다. 통합 플레이의 시계는 독립적으로 시간을 진행하거나 마감 이벤트를 발생시키지 않습니다.
   * 배경 시작·끝도 공용 상수를 사용합니다. 낮·석양·야간의 중간 전환값과 기존 아트 레이어는 유지합니다. 상세 계약은 [MainScene 통합](MAINSCENE_INTEGRATION.md#감독관공용-영업-시각-통합-2026-09-11)을 따릅니다.
2. **배경 아트 레이어 계층 순서 (`AstraFrontView`)**:
   * 스카이라인(아파트 및 남산타워)이 가려지지 않도록 잘못된 이전 안개 레이어(`FogBack`, `FogMid`, `FogFront`)는 비활성화 유지합니다.
   * 배경 레이어 순서:
     1. `FarBackground` (최하단 베이스 원경)
     2. `DawnBackground` (`Dawn.png`: 09:00~12:00 아침 햇살)
     3. `SunsetBackground` (`Sunset.png`: 15:00~18:00 석양/노을)
     4. `EveningBackground` (`Evening.png`: 18:00~21:00 야간)
     5. `CityLights` (`CityLights.png` + `CityLights.mat`: 야간 원경 도시 건물 창문 불빛 점등)
     6. `MidBackground` (중경 폐허 건물)
     * 추가 조명:
       * `LeftBeam`, `RightBeam` (`Searchlight.png`: 좌우 감시탑 탐조등, 야간 점등)
       * `CounterLight` (`CounterLight.png`: 매대 조명, 야간 점등)
3. **시간대별 페이드 및 틴트 로직 (`TimeOfDayUIController.cs`)**:
   * **09:00 ~ 12:00 (아침 -> 주간)**: Dawn 레이어 페이드아웃, 따뜻한 아침 틴트에서 맑은 주간 톤으로 전환.
   * **12:00 ~ 15:00 (주간)**: 기본 대낮 상태 (Tint: White).
   * **15:00 ~ 18:00 (주간 -> 석양)**: Sunset 레이어 페이드인, 주황/붉은빛 노을 틴트 적용.
   * **18:00 ~ 21:00 (석양 -> 야간/마감)**: Evening 레이어 페이드인, 야간 틴트(어두운 청회색) 적용, CityLights·CounterLight·좌우 탐조등 점등.
4. **배경 미리보기 및 단축키 기능**:
   * 아래 조작은 배경 표현만 바꾸며 실제 영업 진행·표시 시계를 변경하지 않습니다. 통합 prefab의 debug/자동 진행은 기본 비활성화합니다.
   * `[`: 1시간 뒤로 이동
   * `]`: 1시간 앞으로 이동
   * `\` 또는 `T`: 주요 페이즈(아침 09:00 -> 주간 12:00 -> 석양 16:30 -> 저녁 19:30 -> 마감 21:00) 즉시 순환
   * `autoAdvanceClockForTesting`: 09:00~21:00 자동 시간 경과 테스트 옵션

---

### C. 대화창 (DialoguePanel) 및 말풍선 UI

1. **렌더링 순서 (Hierarchy Order)**:
   * `AstraFrontView` 내에서 `Customer`(손님), `Counter`(매대), `FrontContainer`(상자)보다 **앞쪽(상위 레이어)**에 렌더링되어야 합니다.
   * `OperatingPanel.prefab` 상에서 `AstraFrontView`의 맨 마지막 자식(`SetAsLastSibling()`)으로 유지하며, Canvas 컴포넌트의 `SortingOrder = 30`을 보존합니다.
2. **에셋 및 폰트 연결**:
   * **배경 프레임**: `Assets/DystopiaPrototype/Art/DialogueFrame.png` (9-슬라이스 적용, Sliced Image, `PixelsPerUnitMultiplier = 4`).
   * **폰트 에셋**: `Assets/TextMesh Pro/Fonts/Mabinogi_Classic_OTF SDF.asset` (`GUID: c0f9f8080e30dd3499d44ece2a336e14`).
   * **공유 머티리얼**: `Mabinogi_Classic_OTF SDF Material` (`fileID: 8083455743747494227`).
3. **로직 (`CustomerPresenter.cs`)**:
   * 손님 대사(`viewData.Dialogue`)가 있을 때만 말풍선이 활성화되고, 손님이 없거나 대사가 비어있으면 자동으로 비활성화되어야 합니다.
   * 프리팹에 사전 구성된 `speechBubbleRoot`와 `dialogueText` 직렬화 참조를 최우선 사용합니다.

---

### D. 상자 착지 흙먼지 이펙트 (LandingDustEffect)

1. **접점 계산 방식 (`CalculateContactPoint`)**:
   * 기존의 임의 오프셋(`box.anchoredPosition.y - 151f`, `0.42f`)은 상자 중간 공중에서 먼지가 터지므로 **절대 복원하지 않습니다**.
   * 상자 스프라이트(`FrontContainerMale.png`, 1254×1254, 하단 접점 비율 95.06%)의 실제 렌더링 영역을 기반으로 계산식을 유지합니다:
     ```csharp
     float boxLeftX = box.anchoredPosition.x - box.pivot.x * box.sizeDelta.x;
     float boxTopY = box.anchoredPosition.y + (1f - box.pivot.y) * box.sizeDelta.y;
     float centerX = boxLeftX + box.sizeDelta.x * 0.5f;

     float renderedHeight = Mathf.Min(box.sizeDelta.x, box.sizeDelta.y);
     float visualBottomY = boxTopY - (renderedHeight * 0.9506f);
     return new Vector2(centerX, visualBottomY) + this.dustOffset;
     ```
2. **먼지 입자 비산 궤적 (`animateDust`)**:
   * 상자 하단 테두리 폭(~228px, 반폭 ~114px) 아래에서 10개의 픽셀 먼지가 좌우 대칭으로 솟구치도록 시작점과 호(arc)를 설정합니다:
     * 좌측 파티클: 시작 X = `-30px ~ -94px`, 이동 X = `-35px ~ -108px`, 호 높이 = `8px ~ 18px`.
     * 우측 파티클: 시작 X = `+30px ~ +94px`, 이동 X = `+35px ~ +108px`, 호 높이 = `8px ~ 18px`.
3. **인스펙터 조정 필드**:
   * 미세 조정용 직렬화 필드 `[SerializeField] private Vector2 dustOffset`을 유지합니다.

---

### E. 상자 및 시계 매대 중앙 정렬 (Center Alignment)

해상도 1280×720 기준으로 매대 및 화면의 정중앙선(**X = 640**)에 정렬된 직렬화 좌표입니다.

```yaml
# Assets/Prefabs/GameUI/OperatingPanel.prefab

# 1. 상자 (FrontContainer)
FrontContainer:
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 460, y: -350}  # Width 360 -> Center X = 460 + 180 = 640
  m_SizeDelta: {x: 360, y: 240}
  m_Pivot: {x: 0, y: 1}

# 2. 시계 (CounterClock)
CounterClock:
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 550, y: -605}  # Width 180 -> Center X = 550 + 90 = 640
  m_SizeDelta: {x: 180, y: 90}
  m_Pivot: {x: 0, y: 1}

# 3. 시계 텍스트 (ClockText)
ClockText:
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: -2}      # CounterClock 내부 중앙
  m_SizeDelta: {x: 110, y: 28}
  m_Pivot: {x: 0.5, y: 0.5}
```

* **위치 관계**:
  * 상자는 매대 상판 위(`Y = -350`, 바닥 접점 `Y ≈ -578`)에 착지합니다.
  * 시계는 매대 전면부(`Y = -605`)에 부착되어, 상자가 매대에 놓여도 시계가 가려지지 않고 항상 보입니다.
* `SaleSortingPrefabSetup.cs`의 `setupFrontView()` 내 배치 코드도 동일 수치(`FrontContainer: (460f, 350f)`, `CounterClock: (550f, 605f)`)로 유지합니다.

---

## 3. 병합 시 파일별 충돌 해결 지침

| 파일 경로 | 병합 주의사항 |
|---|---|
| `Assets/Scripts/UI/SaleSortingPanel.cs` | **[CRITICAL]** 상자 쏟기 연출(`playEntryFlow`), 틸트 및 빈 상자 퇴장 로직, 착지 시점(`elapsed >= 0.3f`) dust 호출 로직 유지. |
| `Assets/Scripts/UI/SaleSortingItemView.cs` | 드래그 앤 드롭 필수 플래그(`raycastTarget = true`) 및 판정 영역 시각 피드백 유지. |
| `Assets/Scripts/UI/TimeOfDayUIController.cs` | **[신규/유지]** 시간대별(아침/낮/노을/밤) 레이어 페이드 및 조명 점등 로직, 단축키 제어 필수 보존. |
| `Assets/Scripts/UI/BusinessClockController.cs` | DayProgress 비율을 BusinessHours의09:00~21:00으로 표시. 자체 마감 이벤트로 진행을 변경하지 않음. |
| `Assets/DystopiaPrototype/Art/TimeOfDay/*` | Dawn, Sunset, Evening, CityLights, CounterLight, Searchlight 스프라이트 및 머티리얼·셰이더 보존. |
| `Assets/Prefabs/GameUI/OperatingPanel.prefab` | **[CRITICAL]** 다른 브랜치의 구버전 프리팹(대화창 누락, 상자 Y=-405, 시계 X=1080)으로 덮어쓰지 말 것. 본 브랜치의 `DialoguePanel` 계층, RectTransform 수치, `PouringContainer` 직렬화 참조를 채택해야 함. |
| `Assets/Prefabs/GameUI/GameUI.prefab` | `OperatingPanel` 인스턴스의 override에서 본 브랜치의 계층과 위치가 덮어써지지 않도록 확인. |
| `Assets/Scripts/UI/LandingDustEffect.cs` | `CalculateContactPoint` 함수, `dustOffset` 필드 및 개선된 파티클 비산 궤적을 온전히 유지. |
| `Assets/Scripts/UI/Presenters/CustomerPresenter.cs` | `dialogueFont` 프로퍼티, 대사 유무에 따른 말풍선 자동 On/Off 및 예외 안전 처리 유지. |
| `Assets/Scripts/Scene/Editor/SaleSortingPrefabSetup.cs` | `setupDialoguePanel()` 메서드 및 전면 화면 중앙 정렬 좌표(550, 605 / 460, 350) 유지. |
| `Assets/Tests/EditMode/BusinessClockAndSortingTests.cs` | TimeOfDay 연동 테스트, 아이템 뷰 드래그 테스트, 착지 접점 계산 단위 테스트 유지. |

---

## 4. 병합 후 검증 체크리스트

1. **컴파일 검증**:
   * `dotnet build Cashier.Runtime.csproj` -> 빌드 성공 (경고 0, 오류 0).
   * `dotnet build Cashier.Scene.Editor.csproj` -> 빌드 성공.
   * `dotnet build Cashier.EditMode.Tests.csproj` -> 빌드 성공.
2. **단위 테스트 실행**:
   * Unity Test Runner (EditMode)에서 `BusinessClockAndSortingTests` 전체 PASS 확인.
     * `SaleSortingItemView_InitializesRaycastTarget_AndSupportsDrag` PASS
     * `TimeOfDayUIController_EditorPreview_DoesNotChangeBusinessClock` PASS
     * `TimeOfDayUIController_BlendsDayAndNight_AccordingToBusinessClock` PASS
     * `LandingDustEffect_CalculatesContactPointAtBottomOfBox` PASS
3. **Unity Editor 씬/프리팹 검증**:
   * `OperatingPanel.prefab`을 열었을 때 Missing Script나 Missing Reference가 없는지 확인.
   * `AstraFrontView` 내 `DialoguePanel`이 최하단(화면 최앞단)에 존재하고, TextMeshProUGUI에 마비노기 폰트가 정상 할당되어 있는지 확인.
   * `FrontContainer`(Pos X: 460, Pos Y: -350)와 `CounterClock`(Pos X: 550, Pos Y: -605)이 중앙선(X=640)에 일렬 정렬되어 있는지 확인.
4. **런타임 동작 검증**:
   * 플레이 모드 진입 시 손님이 오면 대사 말풍선이 손님/매대 앞쪽에 마비노기 폰트로 선명하게 표시되는지 확인.
   * 손님이 상자를 내려놓는 순간(0.3초) 매대 상판 접점 바로 아래에서 흙먼지 10조각이 좌우로 자연스럽게 피어오르는지 확인 (단축키 `L`로도 즉시 테스트 가능).
   * 상자를 클릭해 작업대로 전환될 때, **상자가 틸트되며 물품들이 작업대 위로 우르르 쏟아져 나오고, 상자가 빈 상자로 교체되어 화면 밖으로 퇴장**하는지 확인.
   * 작업대 위에 쏟아진 아이템들이 드래그되어 판매 영역 및 제외 영역에 정상 분류되는지 확인.
   * 단축키 `[` / `]`를 누르거나 시간이 흐름에 따라 **배경이 아침(09시) -> 대낮(12시) -> 석양(16시) -> 야간(19시 이후 도시 불빛 및 탐조등 점등)**으로 부드럽게 전환되는지 확인.
