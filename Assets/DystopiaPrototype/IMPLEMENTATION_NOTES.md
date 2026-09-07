# 구현 및 검증 기록

## 범위와 파일

작업 브랜치: `astra-prototype`. 작업 결과는 `Assets/DystopiaPrototype/`와 Unity가 생성한 해당 폴더 `.meta`에 한정합니다.

- `Scripts/DystopiaSession.cs`: Scene 직렬화 임시값, 상품, 거래/대기/일일/주간/목표 상태.
- `Scripts/DystopiaScreen.cs`: 독립 Sprite UI, 한글, 키보드 및 마우스 입력, 런 수명.
- `Editor/DystopiaTools.cs`: 전용 Scene 제작/아트 연결/Play/캡처.
- `Editor/DystopiaValidation.cs`: 실제 세션 클래스의 규칙 경계·경제 검사.
- `Editor/DystopiaPlayVerification.cs`: 실제 Play Mode의 합성 장치 입력 및 화면 검증. 일반 플레이에 디버그 버튼이나 정답을 표시하지 않습니다.
- `Scenes/DystopiaVerticalSlice.unity`: Camera와 DystopiaGame 두 root, 직접 직렬화한 18개 Sprite 참조.
- `Art/`: 독립 PNG 18장 및 `.meta`.
- `Evidence/`: 실제 Game View PNG 및 3개 검사 결과 텍스트.
- `README.md`, `ART_PROMPTS.md`, 이 파일: 실행·출처·인계.

시작 전부터 존재한 변경: Packages/packages-lock.json, ProjectSettings/EditorBuildSettings.asset, ProjectSettings/ShaderGraphSettings.asset.
기존 untracked: Packages/mcp-unity/, ProjectSettings/McpUnitySettings.json, doc/ASTRA_DYSTOPIA_ONESHOT.md.
이 기존 변경은 보존했습니다. 패키지·ProjectSettings·UserSettings·MCP 설정을 편집하지 않았습니다. commit/push/merge/branch 변경은 하지 않았습니다.
Play 진입에 따라 DOTweenSettings.asset의 줄바꿈만 LF로 재직렬화된 것을 발견했습니다. Git 정규화 hash가 원본과 동일한지 확인한 후 CRLF를 복원했습니다. 설정값 변경은 없습니다.

## 구현과 데이터

가격표 1회, 3/5일차 신규 물품 안내, 10상품과 수량, 입력/판매/거절, 공통 대기 게이지/이탈, 명성 기반 방문 수, 도덕성, 정산, 7일 주기 3단계 상납, 실패/시민권 완료, 일시정지/재시작을 구현했습니다.
상품·가격·시간·경제 임시값은 `DystopiaSettings`에 모이며 Scene의 Inspector 값이 런타임 권위입니다. 코드 기본값으로 저장 값을 덮어쓰지 않습니다.
공용 CSV/DTO/loader, Addressables group/address, Resources 로더, ScriptableObject 및 singleton/test assembly는 추가하지 않았습니다.
0.85초 결과 표시 동안 입력과 게이지를 멈춥니다. 성공 회복 전 게이지로 초록 응대 명성을 판정합니다. 복합 거절은 허용치 초과를 우선하여 한 번만 적용합니다.
가난한 손님은 Customer0 외형과 별도 대사에 연결되며 85% 예산 임시값을 사용합니다. 나머지 손님은 세 가지 완성형 외형입니다.

## 검증

- `Rules.txt`: 정가/할인/80% 경계/허용치 경계/예산·복합 거절/중복 결제/대기·정지/주간 상납/실패/시민권 로직 확인. UI 조작 시험과 별도입니다.
- 정상가 seed456 표본: 첫 상납 전 336,500원, 시민권 8일차, 61거래. 판단 5초 + 결과 0.85초로 약 357초이며 안내 읽기 시간은 제외합니다. 실제 사람의 플레이 시간이나 재미를 보장하지 않습니다.
- `PlayInput.txt`: 실제 Play Mode, Input System 장치 이벤트 → uGUI raycast/키보드 Update → 거래. 마우스 시작, 빈 입력, 금액/지우기, 판매와 연타 방지, 과다 가격 거절, Esc/재개, 실제 시간 게이지 바닥 이탈, 정산, 다음 날, 재시작, Screen/EventSystem 중복 방지 확인.
- `FullRunInput.txt`: 날짜/현금/예산/게이지를 덮어쓰지 않고 실제 UI 입력으로 7일차 상납 1회 차감, 시민권 구매, 별도 1원 운영 런의 상납 부족 실패 및 양쪽 재시작 확인. 빠른 자동 입력이므로 사람의 난이도 검증과 구분합니다.
- 실제 16:9 Game View에서 가격표, 거래 상품·수량, 성공·거절, 대기 압박, 정산, 신규 품목, 감독관, 시민권 및 실패 화면을 캡처했습니다. 한글 글리프와 주요 버튼 가독성을 확인했습니다.
- 18개 Art PNG: Sprite Single / PPU100 / Point / mipmap off / uncompressed. 배경 외 17개는 실제 RGBA alpha이며 인물은 양팔 두 개를 확인했습니다. `.meta` 누락 0, 전용 GUID 중복 0, Scene의 외부 GUID는 Unity 기본 참조뿐입니다.

## 오류 이력

- 작업 전 MCP 포트8090 충돌 오류 3건을 관찰했습니다.
- 새 Editor 파일 import 전 메뉴 호출 실패 1회. Refresh 후 등록됐습니다.
- 무제목 Scene이 열려 있어 Additive 생성 시 Unity InvalidOperationException이 발생했습니다. 사용자 승인 후 dirty=false인 빈 Scene을 닫고 전용 Scene을 생성했습니다.
- 첫 합성 마우스 검사에서 영업 시작 assertion 실패. 검증 이벤트를 게임 Dynamic Input 갱신에 전달하도록 고친 후 실제 입력 검사가 통과했습니다. 게임 입력 코드를 우회하지 않았습니다.
- Play domain reload 중 MCP HTTP400 응답이 있었지만 후속 도구와 실제 Play 상태/캡처로 진입을 확인했습니다. 반복 설치·재설정이나 로그 삭제를 하지 않았습니다.
- 최근 컴파일 이후 Console error 0. 과거 실패를 지운 것으로 보고하지 않습니다.

## 남은 범위

### 2026-09-07 배급소 레이어 교체

- 안개 하단 절단선 수정: `PixelFog._BottomFadePixels`를 추가하고 Fog Material 3개에 96 원본 픽셀을 설정했습니다. 표시 사각형 하단과 UV로 샘플링한 원본 하단 모두에서 alpha가 0으로 줄어들어 고정 경계와 수직 wrap 경계가 드러나지 않도록 했습니다. Point 샘플링과 원본 PNG는 그대로입니다. DXC vertex/fragment 컴파일 오류 0, 화면 제어와 Play 확인은 미실행입니다.

- 최신 안개 범위 수정: Fog 3개를 모두 FarBackground 직후, MidBackground 이전으로 옮겼습니다. 1280×720 원본 표시 비율을 유지하고 Y=-390으로 배치해 화면 안에서 상단 330 기준 픽셀까지만 렌더링합니다. 군중·손님·가판 앞에는 안개가 겹치지 않습니다. 전용 Material 3개의 수평·수직 흐름과 노이즈·swirl 속도를 이전의 70%로 낮췄습니다. 별도 C# 컴파일 오류 0, 실제 표시 검증은 사용자 확인 대기입니다. 화면 제어와 원본 이미지 변경은 하지 않았습니다.

- 포그 후속 연결: 사용자가 방금 만든 효과의 인게임 반영을 재요청해 `DystopiaCanvas/Fog_Back`, `Fog_Mid`, `Fog_Front`를 기존 화면 생성 경로에 추가했습니다. 각각 원경 뒤가 아닌 원경 위·중경 위·손님 위에 생성하고 가판과 HUD는 앞에 유지합니다. 앞서 제공한 안개 PNG 3장은 `Art/FogBack.png`, `FogMid.png`, `FogFront.png`에 수정 없이 복사했습니다. 같은 이름의 전용 `.mat`과 씬 참조를 연결했습니다.
- 기존 화면은 SpriteRenderer가 아닌 uGUI Image이므로 `PixelFog`에 `_UseUI`와 `_ZTest` 경로를 추가했습니다. 세 Fog Material은 UI 모드, 깊이 Always, 서로 다른 속도·seed·alpha를 사용합니다. Material 복제나 다른 Sprite Material 변경은 없습니다. SpriteRenderer용 FogMotionController는 기존 용도로 유지하며 UI에는 붙이지 않습니다.
- 연결 검증 `PARTIAL`: 별도 C# 컴파일 및 DXC vertex/fragment 컴파일 오류 0, 씬·Material·이미지 참조와 원본 해시 확인. 별도 C# 컴파일의 CS0649 경고는 씬 직렬화 필드를 코드 대입으로 인식하지 못한 것으로 참조를 파일에서 확인했습니다. 화면 제어·실제 Play 검증은 사용자 지시에 따라 미실행입니다. Play 종료 후 저장된 씬을 다시 열어 6개의 새 참조를 로드해야 합니다.

- 최신 단일 손님 지시: 첨부 `codex-clipboard-1a32b32f-fe4b-4f0f-af2e-a80295c520ce.png`와 `MaleCustomer0.png`의 원본 영역 픽셀 일치를 확인했습니다. 응대·대기 손님은 이 한 종류만 사용합니다. 앞서 추가한 `MaleCustomer1~8.png`와 각 `.meta`는 참조 해제 후 삭제했으며, 화면의 외형 난수 코드도 제거했습니다. 호흡 연출과 거래 규칙은 유지합니다. 화면 제어·실행 검증 없이 파일/참조 검사만 수행했습니다.
- 포그 정지 제보: 저장된 Scene/Prefab/Material에서 새 Fog Shader/Controller 연결과 Fog 오브젝트를 찾지 못했습니다. 기존 인게임 화면은 uGUI 기반이므로 SpriteRenderer용 효과의 자동 적용 대상으로 단정하지 않았습니다. 실제 Hierarchy 대상 이름과 Renderer 종류를 사용자에게 확인 중이며, 이 단계에서 Fog 적용이나 씬 구조 변경은 하지 않았습니다.

- 최신 손님 교체 요청: 화면 제어와 Unity 실행 검증 없이 파일만 적용했습니다. `N:/개인/정총무/남자앵커.png`를 확대 없이 128×128 투명 캔버스에 (10,3)으로 배치한 `Art/MaleCustomer0.png`, `남성팩 외국인.png`의 좌상·우상·좌하·우하 128×128 셀을 `MaleCustomer1~4.png`, `남성팩1.png`의 같은 순서를 `MaleCustomer5~8.png`로 저장했습니다. 원본 픽셀과 alpha는 유지하며 사용자 제공 자료의 별도 라이선스 문서는 없습니다.
- 씬의 customers 참조 9개와 Editor 아트 갱신 경로를 교체했습니다. 외형은 `DystopiaScreen`의 독립 난수로 손님이 바뀔 때만 추첨합니다. 기존 Session의 예산·허용 가격 계산과 난수 순서는 변경하지 않았습니다. 대기 손님 외형도 새 9종을 사용합니다.
- 앞쪽 손님 3명은 좌우 이동을 없애고 하단 중심 pivot에서 높이 98.2~100%, 너비 99.5~100%로 호흡합니다. 들숨 30%·날숨 70%의 서로 다른 주기를 사용하고 일시정지 상태를 따릅니다. 뒤쪽 한 장 군중의 기존 흔들림은 유지합니다.
- 검증 상태 `PARTIAL`: 9개 PNG의 크기·투명 픽셀·비어 있지 않음, metadata와 씬 참조, diff 공백 검사만 확인했습니다. 새 metadata는 기존 Unity TextureImporter 형식으로 파일에서 작성했으며 Unity reimport·컴파일·화면 확인은 사용자에게 인계합니다. 현재 Play 중이면 종료 후 씬을 디스크에서 다시 열어 변경된 참조를 확인해야 합니다. 기존 인물 원본은 삭제하지 않았고 commit·push는 수행하지 않았습니다.

- 사람 정지 후속 수정: `DystopiaScreen.AnimatePeople`이 군중 한 장과 손님 3명을 서로 다른 주기의 정수 픽셀 오프셋(가로 ±2, 세로 ±3)으로 움직입니다. 기준 위치에 오프셋을 적용하므로 누적 이동하지 않으며 일시정지 시 시각 시간도 멈춥니다. 새 5레이어 아트의 연결과는 별개입니다.
- 움직임 최소 실행 검증 `PASS`: Unity compile 오류·경고 0, Play에서 두 차례 좌표를 읽어 군중 (-6,122)→(-5,116), 왼쪽 손님 (184,-190)→(187,-189), 오른쪽 손님 (779,-187)→(782,-187), 응대 손님 (405,-83)→(404,-82) 변화 확인, Console error 0. 일시정지 분기는 코드 확인만 수행했습니다.

- 후속 요청: 사용자 제공 `codex-clipboard-ce6e56b1-a42c-4797-9b82-a1221f96ac9f.png`를 `Art/BoothBarricade.png`로 복사하고 군중 앞·응대 손님 뒤·가판 뒤의 전경 난간 위치에 연결했습니다. 원본은 투명 RGBA이며 별도 라이선스 문서는 제공되지 않았습니다. 바리케이드의 컴파일 및 실제 Play 표시·영업 시작 입력은 확인했고 Console error 0입니다.
- 후속 군중 5레이어는 built-in imagegen으로 생성했으나 두 출력 모두 실제 alpha가 아닌 체크무늬 RGB 배경이어서 제품에 반영하지 않았습니다. 코드 배경 제거 또는 사용자 제공 군중 시트 활용 방식에 대한 사용자 답변 대기입니다. 기존 군중은 유지됩니다. 후속 작업 전체 상태는 `PARTIAL`입니다.

- 사용자 제공 `N:/개인/정총무/`의 7개 PNG를 `Art/`에 원본 바이트 그대로 복사했습니다. `가판.png`는 `BoothCounter.png`, `가판 가림막.png`는 `BoothCanopy.png`로 이름만 변경했고, 나머지는 원래 파일명을 유지했습니다. 사용자가 인게임 사용을 요청한 자료이며 별도 라이선스 문서는 제공되지 않았습니다.
- `배급소 가판앵커1.png`는 구도 참고 전용이며 프로젝트에 복사하거나 런타임에서 사용하지 않았습니다. 참고에만 있는 앞쪽 이동식 난간은 개별 원본이 없어 미포함입니다.
- `DystopiaScreen`이 원경, 중경, 군중, 좌우 감시탑, 인물, 가림막, 가판, UI 순서로 구성합니다. 중경 철책 높이와 군중 하단을 맞췄고 상품을 새 상판 위로 옮겼습니다. `DystopiaTools`의 아트 갱신도 동일한 7개 Sprite를 씬에 직접 연결합니다. Addressables 변경은 없습니다.
- 검증 `PASS`: Unity 재컴파일 오류·경고 0, 실제 16:9 Play에서 영업 시작 버튼과 레이어·인물·상품 표시 확인, Console runtime error 0. 7개 원본 해시 일치, `.meta` 짝·씬 참조 확인, 각 GUID 중복 0, Sprite Single / PPU100 / Point / mipmap off / uncompressed 확인.
- 최종 화면: `Evidence/GameView-20260907-165055.png`. 이 검증은 배경 배치의 최소 실행 검사이며 전체 게임 회귀 검사는 아닙니다.

핵심 루프는 플레이 가능합니다. 20종 파츠 손님, 운영 장비, 특수 이벤트, 거래 규정은 문서의 확장 범위로 미구현입니다. 오디오는 추가하지 않았습니다.
18장 모두 실제 생성·연결한 아트이며 단순 도형 placeholder는 없습니다. 생성 원본은 큰 픽셀 클러스터 이미지로, 640×360 기준 수작업 픽셀 밀도 통일·인물 다양성 확대는 남아 있습니다.
한국어는 Windows에 설치된 맑은 고딕을 사용하며 폰트 파일을 재배포하지 않습니다. 별도 실행 파일 빌드, 다른 OS, WebGL 및 사람의 물리 키보드/마우스 체감 시험은 미검증입니다. 재미는 사용자가 직접 판단해야 합니다.
