# 상품·손님 이미지 연결

2026-09-10, 기준 `886233b`. 상품 가격·PK·판매 가능·설비 조건, 손님 성향·속성 조합과 판정은 보존한다. 후속 사용자 승인으로 공유 변경을 commit/push하며 개인 씬·스크립트는 Git 제외를 유지한다.

## Upgrade 통합 현재값 (2026-09-10)

- 상품은 Upgrade16종으로 변경했다. 기존 이미지 FK는1001/1004/1006/1007/1010/1019의6종에 유지하고 나머지10종은 기본·탑뷰 둘 다 빈값이다. 삭제1009의 Resource4250은 다른 상품에 재사용하지 않고 보존한다. Resource54행 중 실제 상품·외형 참조는 고유53개다.
- Upgrade 신규 상품·설비·지침 Text ID를 보존하고 충돌한 외형 이름만8075~8081→8116~8122,8101~8106→8123~8128로 이관했다. 외형 PK·Sprite FK와 문구는 유지하며 현재 Text128행이다. 아래 최초 통합의115행 기록은 과거 기준이다.
- 일일 지침 CSV 1개만 기존 Default Local Group/Datas에 주소DailyGuidelineData로 추가했다. 이미지54등록과 기존13CSV는 보존했고 새group/label은 없다.
- Upgrade 원본 Rice/DividerBar/시계 GUID 중복은 없고 시계 원본png/meta는 Upgrade 버전이다. 사용용 복사본은 그대로 유지한다. 신규 이미지 Addressables 등록은 하지 않았다.

## 데이터 계약

### total_merge 통합 (2026-09-10)

- target의 성향 이름8071~8074는 보존한다. 충돌한 외형 이름만 8071→8112(외형5005), 8072→8113(5006), 8073→8114(5007), 8074→8115(5008)로 이관했다. 다른 외형 이름8075~8111과 상품·성향 PK는 유지한다. 현재 Text115행이다.
- target의 상품 분류5·6·7과 CustomerCompositionSelector/명성 기반 선택을 유지하고, Generator 및 기존 호환 API에 도덕성 평가기를 전달한다. 사용자 확정에 따라 모든 Normal 행6001·6004~6006의 price_tolerance는1300이다.
- MainScene은 공유 CustomerQueueView로 렌더링한다. 기존 GameUI prefab 인스턴스의 참조23개를 승인된 사용용 자산으로 연결했으며 prefab 원본과 Local 씬·스크립트는 보존했다.

- ProductData: 기존 `image_resource_idx:uint?`는 기본 UI·계산대 이미지다. 마지막 열에 `top_view_image_resource_idx:uint?`를 추가했다. 기존 컬럼 순서는 유지한다.
- 두 값이 모두 빈 경우만 이미지 미준비로 허용하고 기존 흰색 runtime Sprite를 사용한다. Placeholder FK를 넣지 않는다. 한쪽만 빈값, 0, 잘못된 Resource 대역·FK는 로그와 예외로 거부한다.
- 기본 이미지가 있고 탑뷰 원본이 없으면 기본 FK를 탑뷰 열에 **명시적으로** 복제한다. runtime에서 누락된 FK·로드 오류를 기본 이미지로 숨기지 않는다.
- CustomerAppearanceData: `idx,nameidx,image_resource_idx`. RGBA 네 열을 제거했다. 필수 `image_resource_idx:uint`는 ResourceData FK이며 한 행은 한 Sprite다. 성별·연령·성향으로 이미지를 다시 선택하지 않는다.
- 외형 5001~5004를 보존하고 5045까지 확장했다. 사용용 Female 18개, Male 27개(MaleCustomer0 포함), Inspector·NormalMap 제외. 각 외형 이름은 '여성 외형 01' 등 명시적인 TextData 참조다. 기존8001~8004는 외형 이외 소비자가 없어 이름을 변경하고8071~8111을 추가했다.
- Resource4201~4254를 추가했다. 기존 Resource72행·path는 보존하며 새 종류 ID를 배정하지 않았다. 상세 asset/GUID/address는 [등록표](data/IMAGE_RESOURCE_REGISTRATION.csv)를 따른다.

## 상품 매핑

| 상품 | 기본 FK | 탑뷰 FK | 근거 |
|---|---:|---:|---|
| 1001 물 | 4254 Water | 4253 TopDownWater | 기본·탑뷰 별도 |
| 1004 통조림 | 4248 Can | 4251 TopDownCan | 기본·탑뷰 별도 |
| 1006 영양바 | 4249 Crackers | 4252 TopDownCrackers | 기존 Product_1006_Crackers 의미 매핑 유지 |
| 1007 붕대 | 4246 Bandage | 4246 | 탑뷰 없음, 기본 복제 |
| 1010 건전지 | 4247 Battery | 4247 | 탑뷰 없음, 기본 복제 |
| 1019 배터리 | 4247 Battery | 4247 | 동일 상품 의미에 따른 Battery 공유 |

기본 이미지가 없는10종은 두 셀을 비웠다:1005,1013,1014,1015,1016,1018,1020,1021,1022,1023. Mask를 방독면으로 임의 대체하지 않았다.

## 로드·소비자

- 승인된54이미지만 기존 Default Local Group에 확장자 없는 파일명 address로 등록했다. 새 label·group은 없다. Datas 라벨은 기존 CSV에만 유지한다.
- CustomerCatalog가 실제 CSV 파싱 이후 외형·상품 두 FK를 모두 검사한 뒤 공개한다.
- GameUIController 초기화는 ResourceManager의 기존 cancellation·캐시 경로로 고유 FK별 이미지를 로드한다. 준비 전 게임 진행을 시작하지 않으며 로드 실패는 화면 오류 경로로 전달한다. 핸들 소유·해제는 ResourceManager에 유지한다.
- ProgressViewDataFactory → CustomerBasketItemViewData.Icon은 기본, TopViewIcon은 탑뷰다. CustomerPresenter는 기본, SaleSortingPanel은 TopViewIcon을 사용한다. 이전 생성자 호출은 Icon을 TopViewIcon으로 전달하는 호환성을 유지하지만 실제 CSV 경로는 둘 다 명시한다.
- GetCustomerAppearanceSprite(appearanceIdx)는 이미 로드된 외형의 읽기 API다. LocalCustomerQueueView는 이를 재사용하며 방문마다 로드하거나 Inspector의 성별/연령 매핑을 사용하지 않는다.
- Local 스크립트의 AppearanceMapping 필드를 삭제했다. 사용자 승인 후 개인 씬을 저장하여 이전 직렬화 매핑도 제거했다. Local 씬·코드는 Git 제외이며 공유 MainScene/prefab을 변경하지 않았다.

## Migration·복구·검증

2026-09-10 후속 정리: 기존72행 보존은 이미지 migration 당시 기록이다. 이후 코드·CSV·직렬화 자산의 소비 조사로 미사용72행(4001~4099의65행, 4101·4104·4106·4107·4109·4110·4199)을 ResourceData에서 제거했다. 현재54행(4201~4254)을 유지하고 삭제 ID는 재사용하지 않는다. 원본 자산·meta·Addressables는 삭제하지 않았다. 현재 원문·행수·hash는 DATA_CATALOG.md를 따른다.

정리 후 검증: `unity-cli --project C:/Users/PC/Projects/Cashier test --mode EditMode --filter CustomerCsvTests` 실행66건 모두 통과, 실패0·skip0. 실제 CsvHelper/loader·FK·재로딩 원자성 검사를 포함한다. 원본 CLI 응답은 `Temp/TestResults/20260910-resource-cleanup/CustomerCsvTests.cli.json`에 보관했다. 별도 XML 미보관으로 TESTING.md의 XML 보관 요건은 미충족이며 중복 실행하지 않았다. 컴파일 종료·오류0, Play 종료·씬 dirty=False, 유지54행의 PK/path와 카탈로그 CSV 원문 일치 확인. 이번 정리는 PlayMode·UI 재검증 및 commit/push하지 않았다.

기존 CSV·DTO·catalog·UI·관련 테스트와 이 문서를 함께 이관한다. 외형 RGBA는 새 DTO와 호환되지 않으므로 구 CSV만 혼합하지 않는다. 외부 save의 외형 PK는 유지되지만5001~5004의 시각 표현은 변경된다. 복구는 기준 커밋의 관련 파일 묶음과 기존 Addressables 연결을 함께 복원하는 별도 승인 작업이며 현재 변경·개인 씬을 자동 폐기하지 않는다.

- 컴파일 종료·오류 없음 확인. Sprite54개 Editor 로드·등록 중복/GUID 검사를 수행했다.
- CustomerCsvTests에 두 시점 FK·빈값·오류 원자성·표시 선택 검사, GameSessionApiTests에 실제 ResourceManager54로드 검사를 추가했다.
- 최초 테스트는 개인 씬 dirty gate로 시작하지 못했다(실행0건, XML없음). 저장 명령도 안전 검토에서 차단됐으나 이후 사용자 직접 승인 작업에서 backup·저장 완료를 확인하고 테스트를 재개했다. 백업: `Temp/ImageMigrationSceneBackup-20260910-140225`.
- 최종 EditMode174/174: `Temp/TestResults/20260910-140531-7c3304297493471bb17cf481bc41c3f6/EditMode.xml`. PlayMode33/33: `Temp/TestResults/20260910-140803-fee27b1d673648d7819b27c78d3be31f/PlayMode.xml`. 실패·skip·미완료0. 두 시점 CSV/FK·ViewData 선택, 실제54Sprite 로드 및 기존UI 통합 경계를 확인했다.
- 최초 fixture 오류는 현재가 공급 누락/정상 Resource 로그 기대 누락, 이후 UI3건은 단일 프레임 초기화 가정이었다. fixture를 수정했고 제품 FK·로드 실패 검증을 완화하지 않았다. 이전 실패 XML은 Temp에 보존한다.
- 최종 compile idle/오류 없음, 등록54개 GUID/address/group/label 재검사 정상. Console Error4건은 테스트의 명시적 실패 주입(ui purchase notification 1건, ResourcePoolTests 3건)이며 신규 제품 오류는 확인되지 않았다. 테스트 종료 후 개인 씬 dirty=False, Play 종료, InitScene 시작 설정 복원.
- 화면 배치·사용감, 실제 UI조작은 사용자 확인 대상이다. 이전 테스트 성공 기록은 이번 스키마 변경의 검증 증거가 아니다.
