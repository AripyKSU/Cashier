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

핵심 루프는 플레이 가능합니다. 20종 파츠 손님, 운영 장비, 특수 이벤트, 거래 규정은 문서의 확장 범위로 미구현입니다. 오디오는 추가하지 않았습니다.
18장 모두 실제 생성·연결한 아트이며 단순 도형 placeholder는 없습니다. 생성 원본은 큰 픽셀 클러스터 이미지로, 640×360 기준 수작업 픽셀 밀도 통일·인물 다양성 확대는 남아 있습니다.
한국어는 Windows에 설치된 맑은 고딕을 사용하며 폰트 파일을 재배포하지 않습니다. 별도 실행 파일 빌드, 다른 OS, WebGL 및 사람의 물리 키보드/마우스 체감 시험은 미검증입니다. 재미는 사용자가 직접 판단해야 합니다.
