# Scene에서 배치 편집

대상 Scene: `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity`

Play를 종료한 상태에서 `FrontCounter` 또는 `TopDownCheckout`을 선택하면 Inspector에 **정면 배치 표시 / 탑다운 배치 표시 / 지침서 배치 표시 / 가계부 배치 표시** 버튼이 있습니다. 표시를 고른 뒤 Hierarchy의 실제 오브젝트를 선택하고 Scene 뷰 또는 Inspector로 편집하세요. 편집 후 Scene을 저장합니다.

| 대상 | Hierarchy 경로 | 편집 항목 |
|---|---|---|
| 정면 배경·안개·가판·손님 | FrontCounter / DystopiaCanvas / 해당 이미지 | Rect Transform 위치·크기, Image |
| 정면 상자 | TopDownCheckout / TopDownTestCanvas / FrontView / FrontContainer | Rect Transform, Image |
| 쏟는 상자 | TopDownCheckout / TopDownTestCanvas / WorkViewUI / PouringContainer | Rect Transform 위치·크기·회전, Image |
| 계산기 | TopDownCheckout / TopDownTestCanvas / WorkViewUI / RegisterMotion / Register | Rect Transform 위치, 전체 확대는 Scale, Image |
| 계산기 토글 | TopDownCheckout / TopDownTestCanvas / WorkViewUI / CalculatorToggle | Rect Transform, Image |
| 시계 | TopDownCheckout / TopDownTestCanvas / CounterClock | Rect Transform, Image; BusinessClock 자식에서 글꼴·색상 |
| 지침서·가계부 | FrontCounter / DystopiaCanvas / DailyInstruction 또는 DailyLedger | Sheet / Book과 자식 텍스트 |
| 탑다운 작업대 | TopDownCheckout / TopDownWorkbench | Transform, Sprite Renderer |
| 판매·제외 판정 | TopDownCheckout / SaleArea, ExcludedArea | Transform과 Box Collider 2D의 Offset / Size |
| 물품 이동 한계 | TopDownCheckout / MovementArea | Transform과 Box Collider 2D의 Offset / Size |
| 물리 벽 | TopDownCheckout / Boundary_Top, Bottom, Left, Right | Transform, Box Collider 2D |
| 탑다운 카메라 | TopDownCheckout / TopDownCamera | Camera의 Size와 Transform |

물품은 `Assets/DystopiaPrototype/Prefabs/Product0.prefab`(생수), `Product1.prefab`, `Product2.prefab`, `Product3.prefab`을 사용합니다. 각 Prefab의 Sprite Renderer, Transform Scale, Box Collider 2D, Rigidbody 2D 감쇠를 편집합니다. 쏟는 동안에는 잠시 감쇠를 낮췄다가 Prefab 감쇠로 복원합니다. 커서 힘·속도 상한·쏟기 시간은 TopDownCheckout 컴포넌트에서 조절합니다.

`RegisterMotion`은 열고 닫는 애니메이션용 부모이므로 계산기 배치는 그 안의 **Register**에서 수정합니다. 손님은 거래 데이터에 따라 이미지가 바뀌므로 외형 목록은 FrontCounter 컴포넌트의 남녀 Sprite 배열에서 바꿉니다. 손님 자리와 크기는 Customer / WaitingLeft / WaitingRear에서 조절합니다.

실행 시 배치 객체를 다시 생성하지 않고 저장된 오브젝트를 연결합니다. 물품 수, 거래 글자, 손님 외형, 화면 표시 여부와 상대적인 애니메이션은 코드가 제어합니다. 정면과 계산기·문서·상자는 uGUI Canvas, 탑다운 작업대와 물품은 SpriteRenderer이며 물품은 Rigidbody2D를 사용합니다. 배치 오브젝트는 카메라의 자식이 아닙니다.

기본 배치는 16:9입니다. 작업대 크기나 카메라 범위를 바꿀 때에는 판매·제외 영역, MovementArea와 물리 벽도 함께 맞춥니다. Play 중 변경한 값은 Unity 기본 동작대로 Play 종료 시 되돌아갑니다.

## 시간대 배경과 조명

TopDownCheckout의 DystopiaDayNight에서 Preview를 켜고 Preview Hour(9~21)를 조절하면 편집 중에도 확인할 수 있습니다. Play에서는 기존 BusinessMinute가 시간 권위입니다.

- 09~12: Dawn에서 기존 낮 배경으로 서서히 전환.
- 12~18: 기존 낮 화면 유지.
- 18~21: Evening으로 전환하며 도시 불빛·탐조등·가판 조명 증가.
- FrontCounter/DystopiaCanvas의 DawnBackground, EveningBackground, CityLights, LeftSearchlight, RightSearchlight, CounterLamplight는 저장된 Image 오브젝트입니다. 위치·크기는 RectTransform, 밝기와 탐조등 각도는 DystopiaDayNight에서 조절합니다.
- 생성 아트: Art/TimeOfDay/Dawn.png, Evening.png, CityLights.png, Searchlight.png, CounterLight.png. 내장 image_gen으로 생성했으며 기존 배경을 참조한 아침·저녁 변형과 별도 조명 이미지입니다. 원본 배경은 보존했습니다.
- CityLights에는 생성된 회색 배경이 포함되어 전용 CityLights.mat / CityLights.shader가 무채색을 제거합니다. 일반 UI Material로 바꾸면 배경이 보입니다.
- 모든 새 Texture는 Point, mipmap 없음, 비압축으로 import했습니다.
- 검증: Unity compile error 0, Console error 0, 새 shader message 0, 연결 누락 0. 9/12/18/21시 레이어 수치 및 별도 미리보기 렌더 확인. 실제 PlayMode 체감 확인은 사용자 확인 대기입니다.

## 시간대별 픽셀 조명 (2026-09-08)

TopDownCheckout의 **Dystopia Pixel Stage**가 정면 배경·NPC·소품 47개를 480×270 RenderTexture로 렌더하고 Point 확대합니다. 대사·시계 숫자·계산기·문서 UI는 기존 Canvas에서 선명하게 표시합니다. 탑다운 상품 물리와 계산 입력은 변경하지 않았습니다.

기존 Image/RectTransform은 편집 및 애니메이션의 원본으로 유지합니다. DystopiaPixelSource가 최종 메시(그림자 포함)를 임시 월드 MeshRenderer에 전달합니다. 새로운 월드 카메라와 출력 Canvas는 런타임 리소스이며 Scene에 중복 저장하지 않습니다. Pixel Stage를 끄면 원본 UI 표시를 복원합니다.

- **Width**: 픽셀 출력 너비. 기본 480, 높이 270. 320이면 더 굵은 픽셀.
- **Lamp Position**: 1280×720 화면 좌상단 기준 전등 X/Y.
- **Lamp Height / Radius**: 가상 광원의 깊이 및 영향 반경.
- **Lamp Intensity / Color**: 야간 가판 전등 강도와 색.
- **Rim Intensity**: 아침·석양 외곽 역광.
- **Lighting Steps**: 표면 명암 단계. 색상 채널을 독립적으로 양자화하지 않아 색 띠를 방지합니다.
- **Layers**: 원본 그래픽, 표면 분류, 전등 반응. 순서는 그리기 순서입니다.
- **Preview In Editor**: 편집 중 새 렌더 사용 여부. 시간은 Dystopia Day Night의 Preview / Preview Hour로 확인합니다.

조명 전환은 기존 DayNight의 아침·석양·밤 혼합값을 사용합니다. 별도 게임 시간은 만들지 않았습니다. 표면은 인물의 큰 볼륨과 금속 면을 근사하는 전용 셰이더입니다. Unity Light2D 컴포넌트나 새 노멀맵 아트를 사용한 구현은 아니며, 전용 노멀맵이 없는 얼굴의 정밀 굴곡과 물체 간 투사 그림자에는 한계가 있습니다. 기존 접촉 그림자를 유지합니다.

새 파일: Scripts/DystopiaPixelStage.cs, Scripts/DystopiaPixelSource.cs, Art/TimeOfDay/PixelStageLighting.shader 및 Unity 생성 meta. DystopiaDayNight.cs와 현재 Scene 연결을 수정했습니다. 원본 이미지, Packages, ProjectSettings, 기존 Prefab 원본은 변경하지 않았습니다.

검증: STATIC PASS. 9/12/16.5/18/21시 렌더와 UI 합성 확인, 연결 누락 0, 캡처된 Text 0, 비활성화 후 잔여 캡처 0, shader 메시지 0, compile/Console error 0. 실제 PlayMode 입력·전환·호흡 체감 및 성능 측정은 미실행입니다.

### 안개와 구름빛 보정

안개 OriginalEffect의 _Color에 기존 머티리얼 색 × 시간대 안개색을 전달합니다. Pixel Stage의 Dawn Fog / Sunset Fog / Night Fog로 조절하며 알파·흐름은 보존합니다. Sky 레이어는 밝은 구름 영역에만 추가 빛을 적용하고 밤에는 끕니다. Sky Glow Intensity로 강도를 조절합니다. 구름빛 중심은 아침 화면 오른쪽에서 석양 중앙 쪽으로 완만하게 변하며 인물 역광도 동일 중심에서 계산합니다. 화면상의 방향이며 게임 세계의 동서 방위는 정의하지 않았습니다.

SunsetClouded.png는 기존 Sunset.png를 내장 image_gen으로 편집한 결과입니다. 프롬프트 핵심은 도시 구도·픽셀·안개 보존, 둥근 태양 제거, 그 자리의 구름 사이 황금빛 유지입니다. 기존 Sunset.png는 보존하고 SunsetBackground의 Sprite만 새 이미지로 연결했습니다.

앞 Customer의 RectTransform X를 부모 Canvas 너비의 절반으로 저장했습니다. 대기 손님은 유지합니다. 검증: 9/12/18/21시 안개색 전달과 렌더 확인, 새 shader 메시지 0. 실제 플레이 확인은 미실행입니다.

### 앞 손님 노멀맵·실내 반사광 시험 (2026-09-08)

TopDownCheckout → Dystopia Pixel Stage에서 **Relighting Trial**을 끄면 이번 노멀맵과 실내 반사광만 해제되어 이전 표현으로 돌아갑니다. 원본 이미지와 기존 Lamp Response는 보존했습니다.

- Use Customer Normal Map: 노멀맵만 켜고 끄는 비교 스위치. Normal Strength 기본 0.65.
- Customer 레이어의 Normal Sprite가 MaleCustomer_18일 때만 MaleCustomer_18_Normal을 사용합니다. 다른 손님은 기존 근사 표면 조명을 유지합니다.
- Room Light Strength 기본 1.2. Counter, Canopy(앞 기둥·천막), FrontContainer, CounterClock에 넓은 반사광을 더합니다. 소품별 노멀맵을 만든 것은 아닙니다. Layers의 Room Response로 개별 반응을 조절합니다.
- DayNight Preview / Preview Hour 21로 야간 비교가 가능합니다. 저장 상태는 Preview 꺼짐이며 플레이의 게임 시간을 변경하지 않았습니다.

자산: Art/Customers/MaleCustomer_18_Normal.png와 Unity 생성 meta. 프로젝트 기존 MaleCustomer_18.png를 참조하여 내장 image_gen으로 RGB tangent-space 노멀맵을 생성했습니다. 얼굴·어깨·코트·가방 끈의 추정 굴곡이며 실제 3D 모델에서 베이크한 정밀 데이터는 아닙니다. 원본 이미지의 별도 라이선스는 새로 확인하지 않았으며 외부 다운로드 자산은 없습니다. 생성 결과 1142×1378을 원본 206×248의 UV에 대응하여 사용하므로 정밀 윤곽 정합에는 한계가 있습니다. Linear RGB, Default Texture, Point, Clamp, 비압축, mipmap 없음으로 읽습니다(플랫폼별 Normal Map 압축 디코딩을 사용하지 않음).

검증: STATIC PASS. Unity compile error 0, Console error 0, shader message 0. 야간 켜기/끄기 렌더 비교, 일치 Sprite 강도 0.65 / 다른 Sprite 0 / 시험 해제 0 확인, Scene 저장 완료. 실제 PlayMode 손님 전환·호흡 중 체감은 미실행입니다. Commit/push 없음.

### 저녁 집중광 (2026-09-08)

Dystopia Pixel Stage의 Evening Spotlight로 새 집중광과 공기 중 빛줄기를 함께 끕니다. Spot Origin / Target은 1280×720 좌상단 기준 위치, Spot Intensity는 강도(기본 4.5), Spot Half Angle은 원뿔 반각, Spot Haze는 공기 중 빛의 불투명도입니다. 기존 밤 혼합값에 따라 18~21시 증가합니다. 인물 및 Room Response가 지정된 실내 소품에 같은 원뿔 범위로 광원을 계산하고, 빛줄기는 첫 인물 뒤에 렌더합니다. 원본 아트를 수정하지 않는 절차적 셰이더 표현입니다. 실제 Light2D, 광원 기구 아트, 물체 간 cast shadow는 포함하지 않습니다. Relighting Trial은 이전 노멀맵·반사광 시험용이고, 이번 집중광 복원 스위치는 Evening Spotlight입니다.

검증 상태: PARTIAL. 코드와 셰이더 연결을 정적으로 확인했으나 Unity HTTP 연결 거부로 reimport·컴파일·실행·이미지 검증은 미완료입니다. 실행 후 확인할 항목은 야간 밝기, 콘의 위치, 모든 손님 및 바구니의 수광, 비활성화 시 기존 표현 복원입니다. Commit/push 없음.

### 방향광 대비 보정

노멀맵의 세부 굴곡에 몸통의 큰 곡률을 유지하고, 아침·석양·밤의 인물 주변광은 40%, 정면 전등은 18%까지 낮춰 주광 반대편의 명암을 남깁니다. 실내 소품에도 완화된 비율로 적용합니다. Room 반사광도 같은 시간 가중치로 줄이며 낮에는 기존 값을 유지합니다. 이 대비 보정은 Relighting Trial 해제 시 꺼집니다. 스포트라이트 시작점은 화면 밖 오른쪽 위 (1050,-60), 목표 (600,600), Haze 0.025로 저장해 머리 뒤에서 시작하는 삼각형을 줄입니다. 원본 노멀맵은 변경하지 않았습니다.

검증 PARTIAL: diff 공백 검사 통과. Unity 연결 무응답으로 이번 변경의 컴파일 및 실제 명암 결과 확인은 미완료입니다. 저장 Scene의 새 위치는 Scene 재로드 시 적용되며, 실행 중 Inspector 값은 현재 메모리에 남을 수 있습니다.

### 감시탑 역광과 가판 투영 그림자

Tower Backlight(기본 3.2)는 양쪽 감시탑 위치에서 들어오는 손님 외곽광입니다. 석양부터 켜지고 밤에 강해지며, 앞면 광량은 낮춰 실루엣 대비를 유지합니다. 0이면 이 역광 보정을 해제합니다. 탐조등은 왼쪽 -17도, 오른쪽 192도, 흔들림 5도로 손님 뒤쪽을 향하도록 저장했습니다.

Customer Shadow Opacity(기본 .65)는 현재 앞 손님 이미지의 알파를 상판에 투영한 그림자입니다. Shadow Table Y(기본 520~625)는 1280×720 좌상단 좌표의 상판 뒤/앞 경계입니다. 두 감시탑의 반대 방향으로 그림자를 벌리고 상판 밖에는 적용하지 않습니다. 손님은 상반신 이미지이므로 전신의 실제 그림자가 아니며, 3D shadow map이나 물체 사이의 정확한 차폐를 계산하지 않는 2D 근사입니다. 원본 이미지는 변경하지 않았습니다.

검증 PARTIAL: 코드·Scene 수치 및 diff 검사만 확인. Unity 연결 무응답으로 컴파일과 실제 PlayMode 그림자 정합·밝기 확인은 미완료입니다.

### 인물의 픽셀 원화 유지 보정

사용자 피드백에 따라 인물의 감시탑 알파 외곽선과 태양 외곽선 가산을 제거했습니다. 몸통의 가상 곡률을 낮추고 Normal Strength는 0.65에서 0.2로 저장했습니다. 인물에는 원화 명암을 보존하는 제한된 색·밝기 곱셈을 사용하며, 전등에 의한 과도한 표면 부풀림과 대기 손님의 검은 실루엣화를 줄입니다. 가판 투영 그림자와 환경 조명은 유지합니다. 이전 Tower Backlight의 인물 외곽선 설명은 더 이상 적용되지 않습니다.

검증 PARTIAL: diff 검사 통과. Unity 연결 무응답으로 reimport·컴파일·최종 화면 비교는 미완료입니다.

### 강한 반사광 + 하드 명암

최신 요청에 따라 Normal Strength를 0.65와 기존 몸통 곡률로 복원하고, 약한 색 보정 전용 분기를 제거했습니다. 인물 직접광은 어두운 면/중간/밝은 면의 세 단계로 구분하여 부드러운 둥근 그라데이션 대신 경계를 명확하게 만듭니다. 인물 외곽선 가산은 계속 제거 상태입니다. 가판 투영 그림자는 알파 0.5 기준의 명확한 실루엣으로 바꾸고 끝부분의 페이드를 제거했습니다. 실제 3D cast shadow는 아닌 기존 평면 투영입니다.

검증 PARTIAL: diff 검사 통과. Unity 연결 무응답으로 컴파일 및 화면 검증은 미완료입니다.

### 인물 명암의 수평 절단 보정

강제 step 경계와 중복 양자화가 인물 전체를 수평으로 나눌 수 있어 인물 직접광만 원래 N·L 응답에 좁은 전이 구간을 적용합니다. 밝음·중간·어두움의 대비와 0.65 노멀 강도는 유지합니다. 상판 실루엣 투영의 하드 경계는 별개이며 유지합니다. Unity Refresh 완료, 실제 화면 비교는 미실행입니다.

### 디바이더 실제 연결 / 감시탑·Console 수정

사용자 제공 N:/개인/정총무/디바이더.png를 TopDownTest/Art/DividerBar.png로 복사하고 Point/비압축/PPU500 Sprite로 임포트했습니다. TopDownCheckout/DividerBar에 SpriteRenderer, Rigidbody2D, CapsuleCollider2D(3.1×.32), DividerBarController2D를 배치하고 checkout.dividerBar와 MovementArea 범위를 연결하여 Scene을 저장했습니다. 탑다운 Sorting 중 UI 밖 마우스 입력에만 활성화됩니다. 기존 문서의 수동 연결 미완료 상태를 해소했습니다. Inspector에서 해당 오브젝트를 편집할 수 있습니다.

비활성 정면 Canvas에서 Graphic.canvas가 null이 되어 반복되던 ScreenPoint 예외를 부모 Canvas 조회로 수정했습니다. 감시탑 원래 WatchNeutral 머티리얼의 GrayRegion/Brightness를 새 픽셀 셰이더에도 전달하여 다리의 붉은 녹 색이 다시 드러나는 문제를 수정했습니다. deprecated CircleCastNonAlloc은 같은 결과 배열을 사용하는 CircleCast+ContactFilter2D.noFilter로 변경했습니다.

Scene 오브젝트·이미지·입력 참조 저장 확인 완료. 실제 PlayMode 마우스 이동·충돌 체감 및 최종 색 비교는 미실행입니다. 원본 이미지와 사용자 바스켓 위치는 직접 수정하지 않았습니다.
