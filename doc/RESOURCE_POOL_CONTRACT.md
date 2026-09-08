# ResourceManager와 Pool 사용 계약

적용 파일: `ResourceManager.cs`, `SimplePool.cs`, `SimplePoolManager.cs`.
모든 API는 Unity 메인 스레드에서 호출한다. CSV·DTO·Addressables 설정은 변경하지 않는다.

## 자산 로딩

- 동일 문자열 키와 동일 요청 타입은 진행 중 로드부터 공유한다. manager가 Addressables 참조 하나를 소유한다.
- 같은 키에 다른 타입을 요청하면 명시적으로 거부한다. 기본 타입으로 바꿔 조회하는 호출도 허용하지 않는다.
- `LoadAssetAsync<T>(key)`와 `LoadAssetAsyncTask<T>(key)`는 실패를 예외로 전달한다. 이전 UniTask API의 실패 시 null에 의존한 호출은 catch 처리가 필요하다. 현재 자체 소비자는 예외를 처리한다.
- 추가 overload `LoadAssetAsync<T>(key, cancellationToken)`은 해당 호출자의 대기만 취소한다. 공유 로드는 유지된다.
- callback API는 로드 실패를 기록하고 null을 한 번 전달한다. 취소·manager 종료 후에는 전달하지 않는다. callback 자체 예외는 UniTask의 Forget 오류 경로에서 관찰하며 재호출하지 않는다.
- `LoadAssetsAsync`도 같은 내부 경로를 사용한다. 위치 로드 실패 시 callback에 null이 전달되는 계약으로 통일했다.
- `GetResource`는 진행 중이면 null, 완료 후에는 해당 타입의 자산을 반환한다.
- 공유 소비자는 자산을 직접 Destroy하거나 Addressables.Release하지 않는다. `Release(key)`는 모든 소비자의 공유 소유권을 종료하는 owner API이며, 개별 소비자의 반환 횟수를 세는 API가 아니다.
- `Release`는 해당 키의 기존 대기자를 취소한다. 새 요청은 이전 작업의 늦은 완료와 분리된다.
- `ReleaseAll`은 현재 자산과 인스턴스를 정리한다. 진행 중인 네이티브 작업은 완료 시 결과를 폐기·해제한다. 이후 새 요청은 허용한다.
- manager 종료 후 새 요청은 거부한다. 초기화는 공유하며 호출자 대기 취소와 manager 수명 취소를 분리한다.
- 인스턴스 생성은 매번 고유하다. 실패·생성 중 전체 해제·부모 소멸이면 null을 반환하며 늦게 생성된 객체도 해제한다.

## Pool

- 정원은 양수, prewarm 요청은 음수가 아니어야 한다. manager의 초기 prewarm 수는 정원 이하여야 한다.
- `Prewarm`은 로컬 Prefab 전용이다. Addressables 풀은 `PrewarmAsync`를 사용한다.
- `Get`은 대기 객체를 우선 사용한다. 로컬 풀만 남은 정원에서 즉시 생성하며 Addressables 풀 소진 시 null을 반환한다.
- 풀은 대여 객체도 소유한다. 소비자는 직접 Destroy하지 않고 원래 풀에 반환해야 한다. 지정 부모는 풀보다 먼저 파괴하지 않는다.
- 비소유 객체 반환은 예외로 거부하고 객체를 보존한다. 동일 객체의 중복 반환은 무시한다.
- 대여·반환 hook과 prewarm 중에는 다른 변경 작업의 재진입을 거부한다. hook 실패 시 해당 객체를 폐기하고 예외를 전달한다.
- `Clear`는 대여 중·대기 중 객체를 모두 해제하는 **영구 종료**다. 반복 Clear는 무시하지만 종료 후 Get·반환·prewarm은 거부한다. 재사용하려면 새 풀을 생성한다.
- `Available`은 대기 슬롯 수, `TotalOwned`는 현재 소유 수, `TotalCreated`는 누적 생성 수다. Clear에서 모두 0으로 정리된다.
- prewarm 실패는 부분 생성물까지 정리한 후 전달한다. manager는 false를 반환하고 실패한 풀을 공개하지 않는다.
- 동일 키의 생성 진행 중 요청은 false를 반환한다. 준비된 기존 풀은 동일 타입·정원이고 열려 있을 때만 true다. 이 경우 최초 parent·hook 설정을 유지하고 추가 prewarm은 하지 않는다.
- `TryGetPool`은 준비 완료·미종료·타입 일치 풀만 반환한다. `ClearPool`은 등록을 제거하므로 같은 키로 새 풀을 생성할 수 있다.

## 검증 방법

[TESTING.md](TESTING.md)의 PlayMode ResourcePoolTests를 실행한다. 별도 InitScene Play 또는 ValidateResourcePool 셸은 사용하지 않는다. 표준 Test Runner의 임시 씬에서 테스트 소유 manager·provider·locator·객체만 만들고 정리한다.

공유 로드·개별 취소·타입 충돌·해제 후 재요청·실패/retry·callback/Task·종료·로컬/Addressables 풀을 검사한다. 의도한 오류는 LogAssert.Expect로 소비하며 미예상 오류는 실패다. 실제 원격 다운로드 장애·Player build·제품 prefab은 이 fixture의 범위 밖이다.

### 2026-09-08 이전 셸 검증 기록 (당시 결과)

- Cashier Unity 6000.3.18f1, 로컬 connector 8090, PID 11092에서 재컴파일 완료와 compile error 0 확인.
- InitScene에서 시작하여 기존 개인 씬 GameplaySandbox로 진입, GameSessionManager 초기화 완료 확인.
- 당시 셸 검사 PASS: provider 로드 10건, 의도한 실패 1건, 성공 자산 해제 9건.
- Console 오류 3건은 의도한 provider 실패·준비 중 풀 종료·Component 누락 검사에서 발생. 일반 제품 오류는 확인되지 않음.
- 종료 후 검사용 객체 0개, provider 0개, background 설정 false 복원 확인. PlayMode 종료.
- 실제 Addressables API에 임시 provider로 결과를 공급한 검사다. 원격 다운로드 장애나 플랫폼별 빌드 검증을 대신하지 않는다.
- 독립 컴파일에서는 기존 Dev3SandboxTester.alertUntil 미사용 경고 CS0414만 확인. 신규 컴파일 경고 없음.

공용 API와 풀 수명 계약 변경이므로 기본 branch 통합 전 AGENTS.md의 교차 검토·명시적 동의 절차를 적용한다.
