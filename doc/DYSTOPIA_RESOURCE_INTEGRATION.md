# Dystopia 리소스 선택 이관

후속 상품·손님 이미지 활성화는 [IMAGE_RESOURCE_INTEGRATION.md](IMAGE_RESOURCE_INTEGRATION.md)를 따른다. 아래 개인 Inspector 24매핑은 당시 구현 기록이며 현재는 CSV45외형의 공용 Sprite 조회로 대체됐다.

## 기준과 사용 위치

### 2026-09-13 PR #1 잔여 변경 선택 이관

- 기준은 PR #3이 병합된 `master 2d65909`이며, 원본 `astra-prototype ff67838b24523203840f38d2ae436c32b467db18`과 이전 선택 이관 기준 `87844af` 사이의 프로토타입 코드·리소스 26파일을 추가 반영한다. 원본 브랜치 전체 병합은 아니다.
- 범위: 청소기 이미지·흡입/취소/상품 제외 동작, 작업대 노멀 이미지·시간대 조명 shader/component, 손님 거래 반응의 크기·흔들림·색 변화, 스탬프1~5 이미지, 일일 지침 배치 및 편집 도구, 배경 새의 짙은 색상이다. 경로는 모두 `Assets/DystopiaPrototype/` 아래다.
- 신규 이미지와 meta/GUID는 원본 값을 유지한다. 스탬프 이름 변경·삭제는 기존 `DystopiaScreen`의 파일명 fallback을 끊으므로 기존 LedgerStamp 5종과 meta도 보존한다. 새 스탬프의 명성 단계별 의미를 임의로 지정하거나 기존 게임의 정산 이미지 연결을 바꾸지 않는다.
- 원본의 공용 manager·CSV·Addressables·패키지·ProjectSettings·운영 규칙·output은 이관하지 않는다. 원본 씬의 편집용 `previewHour 17.87`과 직렬화 필드 순서 변경도 제외한다. 기존 Main/Hub/Loading 씬과 OperatingPanel, 사용자 로컬 변경은 보존한다.
- **사용 범위:** 프로토타입 코드와 리소스를 저장소에 통합하는 작업이다. 청소기·작업대 조명을 MainScene에 연결하는 작업은 포함하지 않는다. 프로토타입의 기존 `Dystopia/Top Down Test/Connect Vacuum And Lighting` 도구로 저작 씬에 연결할 수 있으나, 이번 검증은 씬 저장 없이 실행 중 객체로 수행했다. 지침 교체 도구는 원본 작업 입력 `output/instruction-update/DailyInstruction-source.png`를 요구하며, 작업용 output 자료는 자동 배포하지 않는다.
- 원본 결함 2건을 함께 수정했다. 다음 손님으로 전환할 때 이전 거래 반응을 새 외형 적용 전에 정리하고, 흡입 취소 시 위치·분류·물리 활성 상태와 함께 선형/각속도를 복원한다. 읽기 전용 기술 리뷰에서 두 수정의 해소와 추가 지적 없음을 확인했다.
- 실제 Play: 거절 반응 도중 다음 손님으로 넘어가면 기존 원본에서는 붉은 색이 남는 것을 재현했고 수정 후 흰색 복원을 확인했다. 실제 상품으로 흡입·부분 삼키기 취소 시 위치/크기/분류/물리/선형·각속도 복원, 완료 시 장바구니 1개 제외와 중복 거부, 스탬프5개 로딩, 작업대 조명 속성 갱신을 확인했다. 실행 중 검증 객체는 제거했다. 증거는 `Temp/astra-remainder-runtime.log`, `Temp/astra-focus-check.txt`이며 제품 API·reflection 기반 검증으로 사람의 마우스 드래그 UX를 대체하지 않는다.
- Unity import·컴파일 및 신규 shader 오류 없음, 검증 중 제품 Console Error0. 최종 EditMode **249/249**, PlayMode **53/53** 통과, 실패·skip·미완료0이다. 증거: `Temp/TestResults/astra-remainder-edit-02/EditMode.xml`, `Temp/TestResults/astra-remainder-play-01/PlayMode.xml`. 신규 자산/meta·전체 Assets GUID 중복 검사, 보호 파일 해시와 `git diff --check`도 통과했다. Player 빌드와 청소기·조명의 최종 화면 품질은 미검증이다.

### 2026-09-11 최신 선택 이관

- 입력 SHA `87844af6a25a2a2c8f66affa31d1adff36de09f4`, 작업 기준 `60a729f`. `Temp/Astra-87844af-source.zip`의 `Assets/DystopiaPrototype/`·폴더meta와 `FogMotionController.cs`·`PixelFog.shader` pair만 선택 이관했다. 총386파일, 기존363파일은 `UserSettings/LocalBackups/AstraEffects-87844af-20260911`에 해시 확인 백업했다. 전체 branch merge 이력은 만들지 않는다.
- 원격에 없는 기존 파일을 삭제하지 않았다. output/browser cache, Packages, ProjectSettings, Addressables, 원격의 다른 shared 코드·사본은 제외한다. `FogMotionController`는 원본 참고용이고 World prefab에는 붙이지 않아 MPB 소유권을 중복하지 않는다.
- 기존 충돌 해결 GUID8개(상품6개 및 Customers/TimeOfDay 폴더)를 유지하고 원본 Scene/Prefab 등의 원격 GUID 소비자를 local GUID로 치환했다. 나머지 원본 import/slicing 설정은 최신 입력을 따른다. 기존 사용용 사본은 덮어쓰지 않았다.
- MainScene/OperatingPanel 파일을 보존해도 직접 참조한 원본 이미지의 결과는 달라진다. OperatingPanel의 CityLights·시계(slicing)·FrontContainerMale, CustomerWorld의 CityLights가 영향을 받는다. 실제 MainScene의 PreOpenPanel은 `Assets/Textures/UI/Dystopia/DailyInstruction.png` 사본을 참조하므로 이번 원본 DailyInstruction 변경의 직접 영향 대상이 아니다. 공용 프리팹 bytes 불변을 화면 불변으로 해석하지 않는다.
- World 효과의 Smoke0~3/WatchGuard/FogBack·Mid·Front는 기존 환경 사본8개와 원본 PNG hash가 일치하므로 재사용한다. 기존 resource map의8개 경로/GUID/hash 연결은 유지한다. 새 WorldBirds/WorldGuard shader, WorldFog3·Birds·Guard 재질과 흰 Sprite/Texture asset은 제품 표현용 생성 자산이며 원본 사본을 덮지 않는다.
- 상세 연출 계약·후속 상태는 [작업 기록](work/sprite-world-presentation.md), 최종 실행 증거는 [TESTING](TESTING.md)을 따른다. 아래 bd48cb5 및 SOURCE_AUDIT.csv는 최초 이관 당시 기록이며 이번 원본 전체의 최신 hash 목록은 아니다.

### 최초 이관 기준

- 원격 기준: `origin/astra-prototype`, 고정 SHA `bd48cb5146e2521eeaaf181c706efcbf770cf9f3`.
- 원본 계열: `Assets/DystopiaPrototype/` 및 `.meta`. 독립 prototype 참조용 스크립트·씬이며 현재 gameplay를 대체하지 않는다.
- 원본 외부 의존성: Fog 재질의 GUID `6816116619de75b4db7736c3398ba82a`를 따라 `Assets/Shaders/PixelFog.shader`와 meta를 이관했다. 나머지 정상 외부 참조는 기존 uGUI·TMP·Input System·URP 자산을 사용한다.
- 이관 전 기존 파일 53개는 `Temp/Dystopia-before-bd48cb5/`에 백업했다. 원격 그대로의 archive는 `Temp/Dystopia-bd48cb5-source.zip` 및 압축 해제 폴더다. Temp는 Git 제외이며 영구 백업이 아니다.
- Packages, ProjectSettings, Addressables, 외부 output은 이관하지 않았다. 기존 파일 중 원격에 없는 항목을 삭제하지 않았다.

| 사용용 경로 | 분류 |
|---|---|
| `Assets/Textures/Customer/Dystopia/` | 남녀 손님·검사관, 확인된 노멀맵은 `NormalMaps/` |
| `Assets/Textures/Products/Dystopia/` | 상품, 탑다운 상품은 `TopDown/` |
| `Assets/Textures/Environment/Dystopia/` | 배경·군중·가판·용기·시간대 레이어 |
| `Assets/Textures/UI/Dystopia/` | 지침·정산·대화·시계·계산기 |
| `Assets/Shaders/Dystopia/`, `Assets/Materials/Dystopia/` | shader·material 사본 |
| `Assets/Fonts/Dystopia/` | Mulmaru OTF·SDF·OFL 전문 |
| `Assets/Prefabs/Dystopia/Templates/`, `UiSlices/` | 참고용 prefab·기존 잘라낸 UI Sprite asset |

사용용 사본은 총 146개: PNG 109, shader 5, material 7, prefab 8, UiSlice 15, 폰트 2. LICENSE.txt는 별도 동봉한다. PNG 픽셀은 수정하지 않았다. 노멀맵은 `InspectorNormal`, `MaleCustomer_18_Normal` 두 개이며 `CounterClockNormal`, `FrontContainerNormal`은 일반형 이미지다. 고객 파일 번호 누락은 그대로 유지한다.

상세 경로·분류·GUID·SHA256은 [DYSTOPIA_RESOURCE_MAP.csv](data/DYSTOPIA_RESOURCE_MAP.csv), 원격 파일별 이관 hash 대조는 [DYSTOPIA_SOURCE_AUDIT.csv](data/DYSTOPIA_SOURCE_AUDIT.csv)를 따른다. 이 CSV는 문서용 목록이며 런타임 DataTable이 아니다.

## GUID와 참조

AssetDatabase.CopyAsset으로 사본의 새 GUID를 발급하고 복사 대상 사이의 GUID 참조를 새 값으로 치환했다. Sprite slicing·fileID·import 설정을 유지한다. shader 이름은 `Dystopia/Imported/<원래 이름>`으로 구분하며 사본 재질도 사본 shader를 참조한다.

원본 GUID 보존 예외는 Bandage, Battery, Can, Crackers, Painkiller, Water의 6개다. 원격 GUID를 이미 `Assets/Textures/Customer/Products/<동명>.png`가 사용하므로 기존 프로젝트 자산은 보존하고 이관본에는 Unity가 발급한 새 GUID를 사용한다. 이관본 내부 참조도 새 GUID로 연결했다. 상세표의 `archive_guid`는 원격 원본, `source_guid`는 이관본, `copy_guid`는 사용용 사본이다. `collision_existing_path`는 기존 GUID 소유자다. 원격 archive 자체는 수정하지 않았다.

CheckoutUI는 DystopiaKeyFeedback, FrontView는 DystopiaCrowdImage, InspectorPortrait는 DystopiaInspectorPortrait, Product0~3은 DystopiaTopDownItem을 참조한다. 해당 사본은 독립 제품 prefab으로 활성화하지 않은 참고 템플릿이다. 폰트 참조는 사용용 사본으로 연결했고, 현 Scene에 prefab을 추가하지 않았다. PixelStage의 전체 조명·메시 파이프라인 사용은 별도 통합 작업이다.

## 출처와 라이선스

- 아트 출처 기록은 이관본 `ART_PROMPTS.md`와 `IMPLEMENTATION_NOTES.md`를 보존했다. 내장 image_gen 생성 기록과 사용자 제공 레이어 기록이 있으며, 각 아트의 재배포 권리를 일괄 추정하지 않는다. 이 작업은 사용자가 승인한 프로젝트 내부 이관이다.
- Mulmaru OTF 내부에 `Copyright (c) 2025, Mushsooni (https://github.com/mushsooni/mulmaru)`가 존재한다. [제작자 저장소](https://github.com/mushsooni/mulmaru)의 [공식 LICENSE](https://raw.githubusercontent.com/mushsooni/mulmaru/main/LICENSE) 전문을 `Assets/Fonts/Dystopia/LICENSE.txt`에 동봉했다. SIL OFL 1.1 및 Reserved Font Name 조건을 유지하고 OTF 바이너리는 변경하지 않았다. SDF는 원본 asset 사본에 참조만 재연결했다.

## 원격 원본의 미해결 참조

아래 Sprite GUID는 고정 원격 Assets의 meta에서도 찾을 수 없다. 임의 외형으로 대체하거나 원본 슬롯을 삭제하지 않았다. 사용용 사본 146개에는 이 누락 GUID 참조가 없다.

| GUID | 소비자·의미 |
|---|---|
| `e4dd2b06ebb59a14b9fe472c625c82c7` | DystopiaVerticalSlice의 `daughter` Sprite |
| `5bc5f817041947ad87155e3ce9e69fa0` | 두 원본 씬의 maleCustomers 11번 Sprite |
| `330293dc189149da84adaf24aa474ee1` | 두 원본 씬의 maleCustomers 20번 Sprite |
| `dc44f8a468f993a4180c7aea35fa8490` | 두 원본 씬의 maleCustomers 27번 Sprite |
| `e66cb789b2a544de9d1f52cc6e7c9be0` | 두 원본 씬의 femaleCustomers 7번 Sprite |
| `876c6d5818ba44458b7d0b0e52065416` | 두 원본 씬의 femaleCustomers 13번 Sprite |

두 원본 씬은 `Scenes/DystopiaVerticalSlice.unity`, `TopDownTest/Scenes/DystopiaTopDownTest.unity`다. 원본 씬 완전성은 PARTIAL이며 이후 개인 대기열에서는 실제 존재하고 검증된 Sprite만 명시적으로 매핑해야 한다.

## 검증

- 사용용 자산: STATIC PASS. 146개 로드, PNG 109개의 바이트·정규화한 import 설정 일치, 1,174개 로드 객체의 타입/fileID 집합 일치.
- 사용용 prefab 8개의 missing script 0, 직렬화 GUID 참조 누락 0, 복사 대상의 원본 GUID 잔존 0, 프로젝트 Assets 내 GUID 중복 0, 사본 shader 오류 0.
- `Temp/Dystopia-validation.txt`에 검사 결과와 남은 prototype script 의존성을 기록했다. 한글 `시계` Sprite의 CopyAsset 후 이름 변형은 원본 import metadata와 사본 GUID로 재import하여 일치를 확인했다.
- 현재 개인 씬·원본 prefab을 Editor에서 열어 저장하지 않았다. UI 표시·PlayMode·Player 빌드는 미실행이며 정적 자산 검증을 runtime 성공으로 간주하지 않는다.
- 기존 일일 도덕성 6파일과 TMP·설정 dirty는 보존했다. commit·push·merge는 수행하지 않았다.

## 개인 대기열 연출 (2단계)

개인 `Assets/Scenes/Local/CustomerQueueSandbox.unity`와 `Assets/Scripts/Local/LocalCustomerQueueView.cs`만 연출을 변경했다. 두 파일은 Git 제외이므로 별도 백업이 필요하다. 변경 전 파일·meta·hash는 `Temp/Dystopia-Local-Before/`에 있다. 씬 GUID `8b73cb83d33f59541a62faa2ed8cbadd`와 기존 스크립트 GUID를 유지했다.

외형 PK 5001~5004 각각에 다음 성별·연령 조합을 직렬화한다(총 24행). 선택은 `AppearanceIdx`와 방문의 성별·연령 snapshot만 사용하며 성향·특수 속성·생성 정책은 바꾸지 않는다. 원본 `DystopiaSession.AppearanceType`의 파일 구분을 참고한 개인 테스트용 매핑이다.

| 속성 | PK 5001 / 5002 / 5003 / 5004의 사용 Sprite |
|---|---|
| 남성 성인 | MaleCustomer_01 / 02 / 03 / 04 |
| 여성 성인 | FemaleCustomer_01 / 02 / 03 / 04 |
| 남성 아이 | MaleCustomer_25 / 26 / 25 / 26 |
| 여성 아이 | FemaleCustomer_17 / 18 / 17 / 18 |
| 남성 노인 | MaleCustomer_28 / 29 / 28 / 29 |
| 여성 노인 | FemaleCustomer_19 / 20 / 19 / 20 |

- Sprite는 사용용 사본을 직접 연결하며 white tint·preserveAspect를 적용한다. 매핑 누락 시 기존 CSV 색상 블록을 사용한다.
- Visit별 시각 객체와 기존 Queue/거래 상태는 유지한다. 발 기준 root는 경로·크기와 외형·대사 전체 fade를, Image child는 호흡만 표현한다. 불만 표시 수명은 기존 LeavingCustomers 모델 규칙을 유지한다.
- 입장·줄 전진·계산대 이동은 0.65초 SmoothStep. 앞 대기 크기는 현재 손님의 0.62, 다음은 0.43, 뒤로 갈수록 줄어 최소 0.16이다. 기존 좌우 퇴장 시간과 정산 화면 지연은 유지한다.
- 공통 호흡 주기는 2.9초±12%, scaleY 최대 1.018·scaleX 최대 1.007, 작은 bob/sway를 사용한다. 이동 중 호흡을 멈추고 일시정지 시 동일한 시각 상태를 유지한다.
- 기존 Customer 부모의 단색 Image만 Local override로 숨겼다. 기존 가판·버튼·분류 흐름·MainScene·공유 prefab은 수정하지 않았다. 원본 prototype Session·PixelStage를 호출하지 않는다.

### 실행 확인과 한계

- `Temp/Dystopia-Before.png`는 작업 전 열린 화면, `Temp/Dystopia-Local-Before.png`는 개인 씬 변경 전 SceneView, `Temp/Dystopia-Local-After.png`는 정상 실행 중 현재 손님+대기 2명·가판·클릭 용기 화면이다. 계산기는 기존 정면 단계에서 숨기는 공유 UI 동작을 유지했다.
- `Temp/Dystopia-Local-Smoke.txt`에 실제 프레임의 위치·크기·alpha·호흡값, 첫 거래 인계, `Pause stable`, 마지막 거래의 `settlementPending=True`, 최종 `visuals=0`을 기록했다. 긴 첫 프레임으로 동시에 단계를 진행한 최초 관찰을 수정해 게임 시간 기준 단계별 검증으로 통과했다.
- 컴파일 오류 0, 매핑 24개 Sprite 누락·중복 0, 개인 씬 missing script 0, 실행 후 Console error 0. MainScene/GameUI prefab 및 두 meta의 변경 전후 SHA256 일치.
- Play는 종료했고 개인 씬과 InitScene 시작 설정을 유지한다. Play 종료 후 개인 씬이 dirty로 표시돼 추가 저장·폐기는 하지 않았다. 저장된 조립 파일은 보존한다.
- 실제 화면 배치·사용감은 사용자 확인 대상이다. 대기 이탈 불만의 전체 표시 시간은 이번 실제 실행 절차에서 별도 전수 확인하지 않았다. Git 작업은 수행하지 않았다.

### 커밋 전 후속 확인

- 리뷰 후 CanvasGroup을 Visit root로 옮겨 외형·대사의 기존 fade 범위를 복원했다. 컴파일 종료·오류 없음은 재확인했으며 dirty 개인 씬 보존을 위해 이 변경 후 Play는 재실행하지 않았다.
- 리소스 이관 622파일의 asset/meta 짝을 확인했다. `git diff --cached --check`는 원본 및 Unity 직렬화 파일의 공백 경고 3,113건을 보고했다(meta 2,204, prefab 540, unity 283, asset 82, mat 2, 원본 C#의 EOF 빈 줄 2). 원본·import 설정 보존을 위해 일괄 포맷하지 않았으며 공백 검사 통과로 보고하지 않는다.
- 개인 씬·Local 스크립트·Temp 증거와 기존 TMP·설정 dirty는 커밋 대상에서 제외한다.
