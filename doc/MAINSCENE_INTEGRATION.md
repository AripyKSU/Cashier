# MainScene 임시 UI 통합

`InitScene`에서 Play하고 Gameplay Scene Settings를 `Use MainScene`으로 설정한다. MainScene의 `MainSceneUI`에 연결된 `Dev3SandboxTester`가 실행 중 uGUI를 생성한다. Scene에는 생성된 Canvas를 저장하지 않으며 기존 카메라·GUID를 유지한다.

## 사용법

1. START SESSION → 튜토리얼 진행 → START DAY → 가격표 → OPEN STORE.
2. 실제 Customer CSV로 만든 손님의 구매 목록과 입장 대사가 표시된다. 키패드 또는 숫자 키로 전체 목록 총액을 입력하고 CONFIRM한다.
3. CustomerVisit이 한 번 판정한다. 수락이면 Finance의 일일 집계에 판매 수입을 한 번 반영하고, 거절이면 자금은 변하지 않는다. 결과 대사는 TextData에서 읽는다.
4. NEXT CUSTOMER로 판정된 손님을 퇴장시키고 새 손님을 생성한다. 방문 규칙이 보류 상태이므로 자동 입장·대기열·이탈은 사용하지 않는다.
5. 판정 후 END DAY로 Finance 집계를 닫는다. 납부일에는 CSV의 금액·주기에 따라 PAY MAINTENANCE를 사용한다. 자금 부족 시 금액·회차를 바꾸지 않으며 진행은 납부 단계에서 대기한다. 게임 오버 규칙은 미구현이다.
6. COMPLETE DAY는 임시 화면 날짜를 하루 늘리고 허브로 돌아간다. 금액은 같은 GameSessionManager의 EconomyRuntime을 계속 사용한다. 별도의 저장·날짜 시스템이 연결되면 이 화면 소유 날짜를 교체한다.

## 소유권과 제외

- CashierSession 파일은 보존하지만 이 화면에서 생성·호출하지 않는다.
- CustomerCatalog/CustomerGenerator/CustomerVisit: 상품·외형·성향·주문·가격 판정·대사.
- GameSessionManager.Economy: 금액·일일 매출·상납금. 화면이 경제 런타임을 재생성하지 않는다.
- MainScene UI는 ProductData 행을 직접 사용한다. 이름은 NameIdx → TextData, 이미지는 ImageResourceIdx → ResourceData로 해석한다. CashierProduct 변환·하드코딩 상품 초기값은 사용하지 않는다. 구형 타입은 실행에서 제외된 CashierSession의 호환 코드로만 남아 있다.
- 저장·이어하기·시민권·명성·도덕성·업그레이드·오디오 설정은 비활성화했다. PlayerPrefs 저장을 읽거나 덮어쓰지 않는다. 기존 전단·튜토리얼·장식 문구에는 UI 원형의 placeholder가 남아 있으며 실제 게임 규칙의 권위가 아니다.
- 한국어 TMP 글꼴은 로컬 Malgun Gothic을 사용한다. 정식 플랫폼용 폰트 배포는 별도 작업이다. 상품 이미지 빈값은 흰 사각형으로 표시하고 잘못된 이미지 FK/로드 실패는 중단한다.
- 거래 처리 중 예외는 입력을 중단하고 Console에 남긴다. 금액을 자동 재반영하지 않는다. 씬 재진입·저장 복원을 지원하는 완성된 세션 복구 기능은 아니다.

## 검증

새 Play 세션에서 Init → MainScene 로딩이 끝난 뒤 `Tools/Check-MainSceneIntegration.ps1` 실행: 실제 버튼 이벤트의 입장·수락·거절·중복 수입 차단·퇴장·일일 종료·다음 날 진행을 검사한다. 실행 중 임시 경제 상태가 바뀌므로 사용자 게임 도중에는 실행하지 않는다.

통합 시 위 검사와 컴파일, Console 오류·경고 0을 확인했다. Player 빌드, 저장 복구, 상납금 전체 회차 UI 검증은 미실행이다. MainScene 직접 Play 대신 Init 진입을 사용한다.
