# 프로젝트 디스토피아

이 폴더는 `doc/ASTRA_DYSTOPIA_ONESHOT.md`에 따른 독립 Windows PC 플레이 구간입니다.

## 실행

Scene: `Assets/DystopiaPrototype/Scenes/DystopiaVerticalSlice.unity`.
해당 Scene을 열고 Play를 누르거나 `Dystopia > Play Vertical Slice`로 실행합니다. Game View를 클릭해 키보드 포커스를 주세요.
이 메뉴는 기존 Scene의 저장 내용을 변경하지 않고 전용 Scene으로 Play에 진입하며, 종료하면 이전 시작 Scene 설정을 복원합니다.
미저장 Scene이 있으면 저장하거나 닫는 결정은 사용자가 해야 합니다.

## 조작

- 첫날 가격표를 기억하고 `영업 시작`을 누릅니다. 3일차와 5일차에는 신규 품목만 안내합니다.
- 숫자키 또는 화면 키패드: 손님에게 받을 총 금액.
- Enter 또는 `판매 확정`: 거래. Backspace 또는 `지우기`: 한 자리 삭제.
- 소수점·음수 부호를 입력하면 `지우기`로 초기화해야 합니다.
- Escape 또는 `일시정지`: 시간과 입력 정지. 정지창에서 재시작할 수 있습니다.
- 정산에서 다음 날로 진행합니다. 매주 상납금을 처리한 다음 시민권을 구매할 수 있습니다.

정답 총액과 손님의 내부 예산·허용치는 영업 화면에 표시하지 않습니다.

## 조정과 환경

`DystopiaGame`의 `DystopiaScreen.settings`가 이 Scene의 가격·도입일·대기열·상납·시민권 임시값을 소유합니다.
초기값 코드는 `Scripts/DystopiaSession.cs`에 있습니다. 런타임은 직렬화된 Inspector 값을 덮어쓰지 않습니다.
아트 참조는 Scene에 직렬화하며 새 Addressables/Resources 경로, CSV ID 또는 ScriptableObject 데이터 로더를 추가하지 않습니다.
한국어 표시는 이 PC에 설치된 `Malgun Gothic`을 사용합니다. 폰트 파일은 재배포하지 않습니다. 다른 OS 및 WebGL은 검증 범위에 포함하지 않습니다.

## 검증 도구

- `Dystopia > Validate Rules`: 실제 런 클래스의 거래 경계·주간 상납·완결 경로 검사.
- `Dystopia > Verify Play Input`: 전용 Scene의 Play Mode에서 Input System 장치 이벤트와 uGUI raycast 경로를 검사합니다. 물리 키보드를 사람이 조작한 시험과 구분합니다.
- `Dystopia > Verify Full Run Input`: Play Mode에서 실제 UI 입력으로 주간 상납·시민권 완료·잔액 부족 실패까지 진행합니다. 자동 입력 중에는 직접 조작하지 마세요.
- `Dystopia > Capture Game View`: 실행 중인 화면 캡처.
- 결과와 캡처는 `Evidence/`에 보관합니다.

현재 실제 완료·미완료 상태는 `IMPLEMENTATION_NOTES.md`를 확인하세요.
