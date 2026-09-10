# 감독관 시스템 명세

상태: 구현·API 및 최소 실화면 검증 완료. 최종 UI/UX는 사용자 확인 대상. 기준: `total_merge d13d2e2`, 작업 브랜치 `codex/inspector-events`.
2026-09-10 확인한 [감독관 문서](https://docs.google.com/document/d/1lCzaQRmFRWrxfIhWr2UZy64-7A8v1N9fAvlOorMF77E/edit?tab=t.exfv0lp8nt0) 1~7절과 사용자의 최신 요청을 기준으로 한다.
이 문서는 임시 병합 인계가 아니라 구현된 기능 계약과 검증 기록이다. 기존 파일명은 유지한다.

## 1. 범위와 기획 해석

- 특정 날짜·설비 조건을 만족하면 영업 시작 전에 감독관이 등장하고, 대사를 순서대로 표시한다.
- 감독관은 대사로 스토리·튜토리얼·상납의 당위성만 설명한다. 실제 상납금 징수나 재정 상태 변경은 하지 않는다(사용자 확정).
- 첫날은 영업 자리·상납 의무·시민권 존재를 소개한다. 3단계 도달 후에는 최종 목표 공개 대화를 연결한다.
- 감독관은 일반 CustomerVisit/CustomerQueue에 넣지 않는다. 상품 목록·가격 제안·명성·거래 도덕성 판정 대상도 아니다.
- 튜토리얼은 이번 단계에서 설명 대사까지다. 실제 조작 완료 대기, 영업 중 개입, 물품 압수, 딸 사건, 시민권 구매·엔딩은 후속 범위다.
- 기획서의 3억/6억/5억은 대사 예시다. 실제 지출·승리 목표액으로 등록하지 않는다.
- 사용자 추가 확정: 감독관 입장·퇴장은 검은색 틴트와 알파 페이드, UI는 독립 prefab, 초기 화면은 준비 완료까지 기존 어두운 이미지로 가린다.

| 쟁점 | 확인된 내용 | 구현 처리 |
|---|---|---|
| 방문 시점 | 사용자 확정: 3일차 설비 구매 → 4일차 하루 시작 전 방문 | 구매 당일에는 추가 방문하지 않고 다음날 진입 시 조건 평가 |
| 상납 주기 | 문서: 매주. 현 코드: 매일 정산에서 MaintenanceService.TryPay | 감독관은 설명만 담당. 기존 경제 로직 유지, 실제 정책과 다른 주기 표현은 대사 작성 시 조정 |
| 가격 공개·인상 | 문서에서도 금액·인상 조건 미정 | 사용자 승인으로 원문 예시 금액은 대사에만 유지. 실제 목표 변경·인상 실행 제외 |
| 초기 재정 | 문서: 거의 빈 재정. 현 테스트: 별도 초기 자금 | 감독관 도입을 이유로 초기 자금을 수정하지 않음 |

## 2. 진행 위치와 상태 소유권

구현 흐름: 날짜 전환 → 당일 가격 준비 → 감독관 조건 평가 → 해당 이벤트 순차 진행 → 가격표·일일 지침 → 영업 시작.

GameProgress.startCurrentDay가 날짜별 가격 준비와 DayProgress 생성·Start를 맡는다. DayProgress.Start는 감독관 유무에 따라 InspectorEvent 또는 PreOpen으로 진입하며 OpenBusiness 이후 영업이 시작된다.

- DayProgress에 `InspectorEvent` 상태를 추가하고, Start 시 이벤트가 있으면 해당 상태로 진입한다. 없으면 기존 PreOpen으로 바로 진입한다.
- GameProgress.State는 기존 DayInProgress를 유지한다. 이벤트 전용 전역 게임 진행 상태를 중복 추가하지 않는다.
- InspectorEvent 동안 OpenBusiness와 거래·설비 구매를 모델/API 경계에서 거부한다. UI 비활성만으로 우회를 막았다고 보지 않는다.
- 영업 타이머·손님 생성·대기열은 시작하지 않는다. 감독관 입퇴장 애니메이션 시간은 별도 표시 시간이다.
- 이벤트 마지막 대사 확인과 퇴장이 끝난 뒤 PreOpen으로 한 번만 전환한다. 기존 가격표에서 사용자가 영업을 시작하는 동작은 유지한다.
- GameUIController.handleDayStarted/상태 렌더링의 기존 PreOpen 가정을 수정하고, 재정·큐·키패드 입력이 감독관 패널 뒤에서 동작하지 않도록 연결한다.

| 구성요소 | 책임 |
|---|---|
| InspectorEventData / DataTable | CSV 파싱, 조건·대사·리소스 FK 검증, 검증 완료 후 공개 |
| InspectorEventService | 날짜별 대상 선정, 진행 중 이벤트·대사 위치·완료 이력 소유. GameSessionManager가 생성·초기화·정리 |
| DayProgress / GameProgress | 이벤트 단계 진입·완료와 기존 하루 흐름 연결. 서비스 상태를 복제하지 않음 |
| InspectorPresenter / GameUIController | 전달된 표시 데이터 렌더링, 다음 대사·퇴장 완료 입력 전달. 조건 판정 없음 |

첫 구현은 전용 서비스 1개로 제한한다. 범용 이벤트 엔진, 조건식 문자열, 노드 그래프, 단일 구현용 interface는 도입하지 않는다.
현재 실행 중 저장/불러오기는 지원하지 않으므로 새 저장 시스템은 추가하지 않는다. UI 재생성으로 세션의 선정 목록·완료 이력이 초기화돼서는 안 된다.

## 3. 데이터 계약

첫 단계는 `InspectorEventData.csv` 1종과 기존 TextData·ResourceData 참조로 충분하다. 감독관 대사마다 별도 CSV 테이블은 만들지 않는다.
종류 ID·PK는 사용자 승인과 공용 Google CSV 종류 ID 예약 등록 후 적용했다. 단일 권위 목록은 CSV_RULES.md 3절의 공용 문서이며, 이번 변경 값은 8절에 기록한다.

| 컬럼 | 타입·의미 |
|---|---|
| idx | uint, 이벤트 PK |
| nameidx | uint, 기획·개발용 이벤트 이름 Text FK |
| day | uint?, 특정 표시 일자(1부터). 빈 값은 날짜 제한 없음 |
| required_facility_idx | uint?, 전날까지 구매한 특정 설비 조건. 빈 값은 조건 없음 |
| min_store_stage | uint?, 최소 가게 단계. 빈 값은 단계 제한 없음 |
| priority | int, 작은 값부터. 동일하면 idx 오름차순 |
| repeat_mode | enum: OncePerSession / OncePerDay |
| dialogue_text_idxs | uint[], 기존 `_` 구분 Text FK. 순서 보존, 1개 이상 |
| portrait_resource_idx | uint, 감독관 Sprite Resource FK |

- 조건 컬럼은 AND로 적용한다. `day=7 + 특정 설비`는 7일에 그 설비 조건을 만족할 때만 실행하며 이후 자동 이월하지 않는다.
- `day`가 비어 있고 `min_store_stage=3`, OncePerSession이면 처음 평가된 3단계 영업일에 한 번 실행한다.
- 설비 조건은 전날까지의 구매 상태로 통일한다. Owned/Active 선택 enum은 두지 않는다. 당일 즉시 적용되는 가게 확장도 감독관 방문은 다음날이다.
- 첫날 소개: day=1, OncePerSession. 최종 확장 대화: day 비움, min_store_stage=3, OncePerSession.
- 특정 날짜 상납 안내도 일반 대사 이벤트로 작성한다. 별도 금액·실행 액션 컬럼은 없다.
- 같은 날 조건이 여러 개 맞으면 모두 정해진 순서로 처리한다. 날짜 진입 시 대상 목록을 고정하고 대화 중 재평가하지 않는다.
- CSV 생성 검증을 전제로 하되 로드 경계에서 PK/FK, enum 종료값 제외, 조건 조합, 날짜·단계 범위를 검증한다. 실패 시 문맥 포함 LogError와 로딩 실패로 개발 중 발견하며 부분 공개하지 않는다.
- 현재 설비만 고려해 단일 FK로 시작한다. 복수 설비 AND/OR 요구가 실제 생기기 전 배열 조건 엔진을 만들지 않는다.

## 4. 대화·입력과 결과 계약

서비스 내부 단계는 `Dialogue → AwaitingExit → Completed`로 제한한다.
마지막 대사 확인 뒤 추가 입력을 막고 퇴장을 요청한다. 해당 이벤트의 퇴장 완료를 확인한 뒤 다음 이벤트 또는 PreOpen으로 진행한다.

서비스 API:

- `BeginDay(displayDay, priorOwnedFacilities, priorStoreStage)`: 세션이 전날까지의 설비 구매·가게 단계 스냅샷을 전달해 대상 목록을 한 번 선정. 빈 목록도 선정 완료로 기록한다. 같은 날짜 재호출 시 재평가하지 않음.
- `Advance(expectedDay, eventIdx, expectedLineIndex)`: 현재 날짜·이벤트·줄만 전진. 이전 패널의 늦은 입력은 거부.
- `CompleteExit(expectedDay, eventIdx)`: 퇴장 대기 중인 해당 날짜·이벤트만 완료. 중복·다른 날짜·이벤트 callback은 적용하지 않음.
- 완료 이력: OncePerSession은 이벤트 PK, OncePerDay는 `(이벤트 PK, 표시 일자)`를 기준으로 관리.

완료 통지는 eventIdx와 displayDay만으로 충분하다. 별도 금전 결과 DTO나 TransactionResult를 만들지 않으며 손님 거래 건수·명성·도덕성에도 집계하지 않는다.
빠른 연속 클릭·중복 callback은 현재 이벤트와 대사 위치로 검증한다. 완료 상태를 확정한 뒤 UI 알림을 발행한다.

## 5. 확정된 경제·날짜 경계

- 감독관 대화에서 FinanceService.TrySpend나 MaintenanceService.TryPay를 호출하지 않는다. 기존 EndTradingDay의 일일 유지비 처리도 변경하지 않는다.
- 납부/거절 버튼, 잔액 부족 분기, 벌금·상납 인상 실행, 별도 재정 사유·지출 집계는 범위에서 제거한다.
- 3일차 시작 평가에서는 아직 미보유인 X설비 이벤트가 선택되지 않는다. 3일차 PreOpen·영업·정산 중 X설비를 구매해도 당일 목록은 그대로다.
- 4일차 시작 평가에서 X설비 보유 조건을 만족하면 이벤트를 선정한다. 기본 OncePerSession 이벤트는 대사·퇴장 완료 후 5일차에 재등장하지 않는다.
- UI를 다시 열거나 같은 날짜의 진행 객체를 재생성해도 당일 목록을 다시 선정하지 않는다. 세션 서비스가 마지막 평가 일자와 선정 목록(빈 목록 포함)을 보존한다.
- 날짜 제한과 설비 조건의 AND 정책은 유지한다. 예를 들어 day=3인데 3일차에 구매한 설비를 요구하면 당일 등장하지 않으며 4일차에도 날짜 조건이 맞지 않는다. 구매 후 다음날 방문이 목적이면 day를 비워 작성한다.

## 6. UI·리소스 연결

- 독립 자산 `Assets/Prefabs/GameUI/Inspector/InspectorPanel.prefab`을 사용한다. 감독관 이미지, 대사 TMP, 다음 버튼, CanvasGroup과 InspectorPresenter를 prefab 내부에서 연결한다.
- GameUI에는 nested prefab 1개와 Controller의 Presenter 참조만 연결한다. MainScene에 감독관 내부 객체를 풀어 복제하지 않는다. 기존 GameUI 구조·GUID·Local 씬을 보존한다.
- InspectorPanel은 장면 객체의 직접 참조 없이 자신의 UI와 표현만 소유한다. 이벤트 snapshot과 진행 요청은 GameUIController를 통해 전달한다. prefab 자체를 새 Addressables로 등록할 필요는 없다.
- 기존 화면·버튼 스타일을 재사용하되 CustomerPresenter의 CustomerVisit 계약에 감독관을 끼워 넣지 않는다.
- 입장 → 대화 → 퇴장 연출은 Presenter 책임. 애니메이션 완료 callback이 늦게 도착해도 다른 이벤트를 완료하지 않도록 eventIdx와 화면 수명을 확인한다.
- 입장: 감독관 이미지 RGB 검정·alpha 0 → 원래 색·alpha 1. 퇴장: 현재 색·alpha → RGB 검정·alpha 0. 대사와 버튼은 RGB 검정으로 물들이지 않고 별도 CanvasGroup으로 알파만 처리한다.
- 입장 완료 전에는 대사 넘김을 막고, 마지막 대사 확인 후 퇴장 중에도 추가 입력을 막는다. 완료 후 색·알파·텍스트·버튼 상태를 초기화해 다음 이벤트에서 검은색이 남지 않게 한다.
- 입퇴장 시간은 Presenter의 Inspector 조정값 `fadeSeconds`이며 초기값은 각 0.4초다. UniTask 표현 루프를 사용하고 화면 비활성·파괴 시 취소한다. 같은 부모 화면의 재활성 순서가 달라도 Presenter.OnEnable에서 준비된 연출을 재개한다. 일시정지 정책은 기존 표현 정지 규칙과 맞춘다.
- ResourceData → ResourceManager로 Sprite를 가져온다. 이미지 파일이 있다는 이유만으로 Addressables 주소를 추측하지 않는다.
- 사용자 승인에 따라 감독관은 기존 손님 이미지를 임시 재사용한다. 첫 데이터의 portrait_resource_idx는 기존 ResourceData 4201(FemaleCustomer_01)을 사용한다. 손님 생성·외형·성향·속성과 연결하지 않고 고정 Sprite 참조로만 사용한다.
- 임시 이미지 때문에 새 ResourceData 행이나 Addressables 이미지 등록을 만들지 않는다. 전용 이미지가 준비되면 해당 리소스 등록을 검증한 뒤 이벤트의 portrait_resource_idx만 교체한다. 그 시점이 임시 이미지 제거 조건이다.
- 사용자 승인으로 기존 Default Local Group의 `InspectorEventData` address에 기존 `Datas` 라벨을 등록한다. 새 group·label·이미지 등록은 없다.

### 대사 원본

- 첫날 이벤트는 원문 4절 「게임 시작 — 첫 목표 제시」의 대사를 순서대로 TextData에 연결한다. 영업 자리, 상납 의무, 딸의 치료와 시민권 존재를 소개한다.
- 최종 확장 이벤트는 원문 5절 「마지막 가게 확장 — 최종 목표 공개」를 사용한다. 3단계 구매 다음날 등장하며 1회 완료 이력을 유지한다.
- 대사의 순서·인물 말투를 보존하고, 화면 줄바꿈과 클릭 단위 분할만 가독성에 맞춘다. 새 서사나 납부 선택지는 추가하지 않는다.
- 사용자 승인으로 원문 3억/6억/5억은 대사 전용 예시로 유지하며 실제 목표액·금전 처리를 만들지 않는다. 주기 표현 두 곳만 `매주 정해진 날에` → `정해진 때에`, `다음 주부터는` → `다음부터는`으로 변경했다. 그 외 20줄/29줄의 순서·말투를 보존한다.

### 준비 완료 전 화면 가림

GameUIController.Start는 CSV·Sprite·참조 검증 후 GameProgress.Start를 수행하고 표시 데이터를 바인딩한다. Canvas 레이아웃 반영 뒤에만 별도 `presentationReady`를 설정해 가림·입력 잠금을 해제한다. `isReady`만으로 가림을 해제하지 않는다.

- 기존 일일지침의 어두운 이미지/스타일을 재사용한다. 현재 PreOpenPanel은 고정 크기이므로 그 부모 패널을 통째로 가림으로 사용하지 않는다. 가림 Image를 하루별 내용 패널과 독립된 GameUI 직속 레이어로 분리하는 최소 변경을 우선한다.
- 초기 prefab 직렬화 상태부터 가림은 활성·불투명·전체 화면 stretch이며 최상단에서 입력을 차단한다. Start/비동기 로드 뒤 켜는 방식은 첫 프레임 노출을 막지 못하므로 사용하지 않는다.
- 로딩 중에는 감독관·일일지침 내용과 버튼을 숨긴다. UI 뒤의 키보드 확인·영업 시작·설비 구매도 초기화 경계에서 차단한다.
- 준비 완료 조건: 데이터 검증·세션 초기화, 표시할 이미지 로드, UI 참조 검증, GameProgress.Start, 최초 표시 데이터 바인딩 및 레이아웃 반영 완료. 고정 시간 대기만으로 준비 완료를 판단하지 않는다.
- 감독관이 있으면 불투명 가림 아래 감독관과 배경을 준비한 뒤 가림을 해제하고 감독관 입장을 시작한다. 없으면 완성된 일일지침을 준비한 뒤 공개한다. 대사 진행 중에는 일일지침이 뒤에서 노출되거나 입력을 받지 않는다.
- 가림이 필요한 다른 Scene에서 GameUI 생성 전에 노출되는 구간은 최초 실측 시 확인한다. 해당 구간이 있다면 기존 Scene 전환의 검은 배경으로 이어 가리며, 비동기로 생성되는 GameUI만으로 전체 Scene 전환을 가렸다고 판단하지 않는다.
- 로딩 실패 시 불완전 화면을 공개하지 않는다. 어두운 가림 위에 기존 오류 안내를 읽을 수 있게 표시하고 상세 오류를 로그에 남긴다. 무한 검은 화면이나 자동 재시도 루프는 만들지 않는다.
- 가림 해제 후 raycast 차단도 해제해 투명 이미지가 버튼을 막지 않게 한다. 감독관 페이드와 준비 가림은 서로 다른 대상이며 색·알파를 동시에 수정하는 작성자는 각각 하나만 둔다.

## 7. 구현 순서와 완료 기준

1. 확정 범위에 맞춘 이벤트 CSV 스키마·예약 ID와 대사·이미지 목록 결정.
2. 담당 프로그래머에게 데이터 검증·이벤트 서비스·DayProgress 진입을 한 작업으로 배정. 첫날/특정일/3일차 구매→4일차 방문/3단계/미등장·동일일 복수 순서 검증.
3. GameUI 패널·리소스 연결. 실제 Init→Main→감독관→PreOpen→영업→정산→다음날 경로 확인.
4. 초기 가림을 연결하고 의도적으로 느린 리소스 로드·실패·화면 파괴, 감독관 있음/없음의 첫 표시를 확인한다. 준비 전 노출 없음·준비 뒤 정상 입력 복구·오류 안내 가시성을 검증한다.

API는 기존 Unity EditMode Test Runner, 실제 로딩·UI 수명은 필요한 PlayMode로 검증하고 XML/log를 보관한다.
감독관 동안 영업시간·대기열이 진행되지 않는지, 재정·상납 납부 상태가 변하지 않는지, 이벤트가 UI 재생성으로 재실행되지 않는지 확인한다. 빈 선정 목록의 당일 재평가 금지와 중복 대사·퇴장 callback도 검증한다. 화면 배치·읽기 속도·연출 사용감은 사용자 확인 대상이다.
기존 CSV·거래·명성·도덕성·설비·일일 정산 회귀를 유지한다. 단계별 인계는 이 문서를 참조하고 허용 경로·차이·완료 기준만 짧게 전달한다.

## 8. 확정과 남은 제작 항목

사용자가 확정한 기능 범위는 대사 전용과 설비 구매 다음날 방문이다. 금전 처리에 관한 추가 결정은 필요하지 않다.
대사 원본 4·5절, 검정+알파 페이드 각 0.4초, 독립 prefab과 초기 준비 가림, Resource4201 임시 재사용, 종류 ID15/PK15001~15002 및 CSV 등록은 승인·예약 완료다. 실제 데이터는 첫날/최초 3단계 다음날의 OncePerSession 두 행이다. 추가 등장 일정·설비별 대사는 이번 범위에 넣지 않는다.

### 구현 계약과 병합 단위

- `InspectorEventData.csv` 헤더는 3절을 따른다. 반복 코드는 1=OncePerSession, 2=OncePerDay이며 숫자만 허용한다. optional 조건의 빈 셀만 제한 없음이며 0은 오류다. 대사 FK 배열은 `_` 구분, 순서·중복을 허용하고 모든 원소를 검증한다.
- TextData에는 이름 8129~8130, 첫 만남 8131~8150, 최종 목표 8151~8179를 추가했다. 기존 행은 유지한다. 초상은 Resource4201 하나를 공유한다.
- `InspectorEventService`는 행을 복사해 선정·줄 위치·퇴장 대기·완료 이력을 세션 수명에 보관한다. `Advance(expectedDay,eventIdx,expectedLineIndex)`, `CompleteExit(expectedDay,eventIdx)`는 오래되거나 중복된 입력을 false로 거부한다.
- `GameSessionManager.EnsureInspectorDay`는 기존 설비 활성일에서 전날 구매 스냅샷을 계산한다. 단계 업그레이드는 활성일=구매일, 일반 설비는 활성일=구매일+1인 기존 계약을 사용한다. 영업 시작·설비 구매 경계도 감독관 미완료를 거부한다.
- DayProgressState는 기존 0~7 값을 보존하고 InspectorEvent=8을 뒤에 추가했다. DayProgress의 `AdvanceInspector(snapshot)` / `CompleteInspectorExit(snapshot)`를 화면 입력 경계로 사용한다. 마지막 이벤트 퇴장 완료 후 PreOpen으로 이동한다.
- GameUI는 독립 InspectorPanel nested 참조와 기존 Root/Background를 ProgressCanvas 직속 StartupCover로 이동한 구성을 사용한다. 덮개는 저장 상태부터 검정/alpha1/활성/stretch/입력차단이며 오류 문구는 덮개 자식이다. MainScene·Local 씬은 수정하지 않는다.
- CSV/DTO/테이블/enum/loader/세션/DayProgress/Presenter/Controller, 두 prefab 및 신규 `.meta`, TextData, 승인된 CSV Addressables entry와 테스트를 함께 적용한다. 부분 적용 시 필수 CSV·패널 참조 검증이 의도적으로 초기화를 차단한다.
- 새 저장 포맷은 도입하지 않았다. 세션을 새로 만들면 이력도 초기화한다. UI/UX·전용 이미지 교체는 별도 사용자 확인 대상이다.
- 이번 프로그래머 작업에서는 stage·commit·push·merge를 하지 않는다. 폰트와 개인 씬 변경은 인계 묶음에서 제외한다.

## 9. 검증 결과 (2026-09-10)

- EditMode 215/215: `Temp/TestResults/20260910-181257-3b6077dbbcb3457cb71db4fed8f00ffc/EditMode.xml` 및 `.log`.
- 최종 PlayMode 37/37: `Temp/TestResults/20260910-182452-fc431564c69b4892b3fdb63a2b5cab89/PlayMode.xml` 및 `.log`. 실패·skip·미완료 0. 실제 15종 CSV 로드, 감독관 첫날/구매 다음날, 잔액·명성·도덕성 미변경, 초기 덮개·오류 가시성, 화면 재생성·같은 root 재활성·퇴장 중 파괴를 포함한다.
- 실제 Init→Main에서 첫 대사와 한글·초상을 확인했다. `Temp/InspectorMain-Dialogue.png`, `Temp/InspectorMain-PreOpen.png`를 확인했으며 `Temp/InspectorMain-Smoke-Retry.txt`에 입장 검정/alpha0→원래색/alpha1, 중간 입장25·퇴장291 관찰, PreOpen, 잔액100000 유지, 영업 시작 버튼→Operating을 기록했다. 표본 수는 프레임/관찰 빈도에 의존하며 시간 규격이 아니다. 제품 Console Error0.
- 초기 실패도 보존한다. `181151-7d6a8aa4ee6340498ffc87ceefbf128e` Edit214/215는 추가 Text51행에 대한 기존 행 수 기대값 수정으로 해결했다. `181257-3b6077dbbcb3457cb71db4fed8f00ffc` Play36/37는 기존 설비 테스트가 새 감독관 선행 단계를 거치도록 수정했다. 중간 `181559-a7637f0d4026438f9f934707bd52df00` Play37/37 이후 실화면에서 같은 root 재활성 연출 정지를 발견해 OnEnable 재개와 회귀 검사를 추가했고 최종 실행으로 재검증했다. 첫 실화면 실패 기록은 `Temp/InspectorMain-Smoke.txt`다.
- 신규 테스트의 초기 CsvHelper 직접 참조 컴파일 오류는 기존 DataTable.LoadData 호출로 고쳐 테스트 assembly 의존성을 확대하지 않았다. 최종 컴파일 오류 없음. 실패 주입 테스트의 기대 오류는 제품 실화면 Console과 구분했다.
- 원문49줄 순서는 승인된 주기 표현 두 곳 외 동일하다. Text 기존128행 변경0, 추가51행, 총179개 PK 고유. 신규 asset의 meta 누락0. MainScene·Local 씬/코드와 기존 Mulmaru 변경은 보존했다. 테스트가 만든 LiberationSans fallback과 임시 background·시작 씬·개인 씬 선택은 복원했다.
- API·최소 실행 검증 범위는 PASS. 화면 배치·읽기 속도·연출 사용감은 최종 사용자 확인 대상이며 전체 UX 완료로 확대하지 않는다. Temp 증거는 Git 제외다.
