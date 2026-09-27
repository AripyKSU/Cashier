# 하루 인트로 9컷

## 후속 불필요한 소음 제거

사용자가 북/노크 같은 소리와 대사 중 쓸리는 소리를 끄도록 요청했다. 직접 청취로 원인을 확정한 것은 아니며, 합성 방법과 재생 위치를 기준으로 후보를 차단했다.
IntroNineCutPlayer의 SlowStoneSteps(4), PaperRustle(5), RunningStop(10), BodyCollision(11), DullImpact(12), ApproachingShoes(13) 슬롯을 해제했다. Wind의 두 Ambience beat 배율과 MealNightWind 음량은0이며 기존 컨트롤러의 PlayDialogueVoice도 껐다. 원본 파일·대사·연출 시간·BGM·정산음·라면/버너·Scene05 젓가락/낙하음은 유지한다. 9컷의 타이핑 함수 자체에는 음원 재생 호출이 없다. 저장된 연결 검증이며 실제 청취는 미검증이다.

## 후속 BGM 개정 — 잔잔한 행복 → 무거운 슬픔

- Scene01~04: Alex McCulloch의 `Happy`를 `WarmDays.wav`로 연결했다. 제작자 분류는 happy/calming/relaxing이며, 현재 요구인 잔잔하고 행복한 분위기의 후보로 선정했다.
- Scene05~09: Centurion_of_war의 `Emotional Piano` 합주 버전을 `Sorrow.wav`로 연결했다. 제작자 분류는 느리고 슬픈 피아노곡이다. 두 곡은 CC0 공개 음원이며 원본·가공·출처는 `Assets/Sounds/Intro/NineCut/MUSIC_SOURCES.md`에 기록했다.
- 두 곡 평균 레벨은 RMS -21dBFS로 맞췄다. Intro Music Volume은 0.32. 초반 바람은 기존 배율1에서0.4로 낮췄다. 정산의 기존 BGM 감쇠와 라면 환경음 프리랩은 유지한다.
- Scene04 종료 암전0.5초와 동시에 행복한 음악을0으로 낮춘다. 젓가락/털썩 및 검은 화면 첫 대사 후, Scene05 이미지 페이드인0.8초와 함께 슬픈 음악을0→0.85 배율로 올린다. Scene06~08에도 계속 재생하고 Scene09에서 재시작하지 않고0.65로 감쇠한다. 마지막 기존 감쇠·종료는 유지한다.
- 검사 범위: 프로젝트 내 WAV/기존 BGM 및 다운로드2곡을 디코딩해 길이·음량·클리핑·무음 구간을 분석했다. 직접 청취 도구가 없어 음색 적합성과 Unity 최종 믹스는 청취 미검증이다. 기존 합성 효과음과 미확보 면/식기 소리 슬롯은 그대로다.

## Scene03→04 정산·라면 및 픽셀 UI 개정

이번 개정은 IntroScene과 인트로 전용 스크립트/아트/검증만 변경한다. Scene01은 후속 첨부 이미지 `codex-clipboard-510d04d0-2de4-4701-af50-aa5bbfee4c38.png`로 기존 PNG를 덮어써 교체했다. 기존 metadata와 씬 참조는 유지했으며 첨부 원본과 교체 파일의 해시 일치를 확인했다. Unity 화면 표시는 별도 미검증이다.

- `IntroMealSequence` 추가. 정산 전용 UI는 창 배경 없이 왼쪽 항목명과 오른쪽 금액을 별도 TMP 열로 표시한다. 흰 항목명, 차분한 녹색 수익/순이익, 차분한 적색 지출, 2픽셀 회백색 Image 구분선, 따뜻한 흰색 마지막 문구다.
- 고정값: +32,000 / -14,000 / -7,000 / -9,000 / 순이익 +2,000원. Finance/GameSession API를 호출하지 않는다.
- Scene03 뒤0.6초 암전과 BGM 감쇠. 검은 상태0.3/0.8/1.3/1.8/2.3/2.6/3.3초에 일곱 행 표시. 마지막 문구1.5초, 그 마지막0.7초에 라면 루프 시작. 글씨0.3초 페이드아웃 → 이미지0.8초 페이드인 →0.4초 후 첫 대사.
- 확정 대사4줄은 `IntroNineCutPlayer.confirmedMealDialogue`에 화자와 함께 저장한다. 기존35줄은 유지한다. 마지막 하루 대사 타이핑 완료 콜백에서만 식사음 이벤트를 한 번 호출한다.
- 끓는 소리는 `MealSimmer`, 약한 밤바람은 `MealNightWind`. 두 로컬 루프는 전환 중 다시 시작하지 않는다. Scene05의 검은 화면 젓가락 효과음 직전에 종료하며 기존 버너는 유지한다.
- 기존 `SoundMixer`의 BGM/SFX 그룹을 연결했다. 믹서 자산이나 SoundManager public API는 변경하지 않았다. Music/Ambience/Effects 및 MealSequence의 환경음·행별 효과음 음량은 Inspector에서 조절한다.
- 수익음은 기존 TransactionSuccess의 첫 단음 약0.305초를 짧게 감쇠한 `SettlementCoinSoft.wav`; 비용은 기존 LedgerTick; 순이익은 CalculatorButton을 낮게 사용한다. 원본 오디오는 수정하지 않았다. 다운로드나 유료 서비스는 사용하지 않았다.
- **누락**: 자연스러운 면 먹는 소리(`noodleSlurp`)와 작은 식기 소리(`dishTick`). 미확보로 슬롯은 null이다. 다른 소리로 대체하지 않았다. 실제 클립을 연결하면 마지막 문장 완성에서 면 소리1회, 끝난 뒤 선택적인 식기 소리1회가 재생된다.
- 대화창/스킵/예/아니요는 built-in image_gen으로 만든 `Assets/Textures/UI/Intro/`의 네 PNG를 사용한다. 질문은 `정말 건너 뛰시겠습니까?`, 예는 초록색, 아니요는 빨간색. 한글은 기존 Mulmaru TMP 폰트다. 프롬프트는 같은 폴더 `GENERATION.md`에 기록했다.
- 확인창을 열면 타이머·타이핑·음원이 일시 정지된다. 아니요는 같은 위치에서 재개하며 닫은 프레임의 대사 입력을 억제한다. 예만 기존 CompleteImmediately 경로를 호출한다. 스킵·재시작·비활성화에서 예약된 식기음과 환경음을 정리한다.

개정 수정 파일: `IntroNineCutPlayer.cs`, `IntroScene.unity`, 이 문서. 추가: `IntroMealSequence.cs`, `IntroMealSetup.cs`, 이미지4개, 수익음 사본1개와 metadata. `IntroMealPlaybackTests.cs` 및 기존 PlayMode 테스트 assembly의 InputSystem 참조는 실제 키보드 이벤트 검증용이다.

최신 검증: Runtime/Editor/PlayMode 테스트 코드는 독립 C# 컴파일을 통과했다. 저장된 씬의 정적 검사에서 기존 35줄, 다른 구간의 beat, 기존 아트와 카메라 보존 및 7개 정산 행/5개 효과음 참조/확정 대사 4줄을 확인했다. Unity PlayMode 테스트 실행 요청은 MCP 시간 초과로 결과를 확보하지 못했다. 실제 표시·청취·연타·스킵·재실행·Scene05 연결은 아직 실행 검증 완료가 아니다. 면 소리 이벤트 테스트는 실제 클립 대신 테스트 전용 짧은 클립을 주입하도록 작성되어 있으며, 실제 면 소리 연결을 뜻하지 않는다.

아래 초기 구현 기록 중 ‘4컷 원문 미확인’과 104개 beat 기록은 최초9컷 구현 시점 기록이다. 이번 확정 대사와 추가 대기 beat는 위 개정으로 연결했다. 정적 검사 증거는 `Temp/IntroMealRevision/static-audit.json`에 있다.

현재 `Assets/Scenes/IntroScene.unity`의 `IntroCanvas`에 연결했다. 기존 `IntroDialogueController`, `IntroSceneEntry`, 대화창과 한글 폰트, 기존 4컷 데이터는 보존한다. `IntroNineCutPlayer`가 연결된 경우에만 9컷을 재생한다. 원래 4컷 배경 오브젝트는 삭제하지 않고 숨겼다.

## 변경 파일

- 수정: `Assets/Scenes/IntroScene.unity`, `Assets/Scripts/Scene/Intro/IntroDialogueController.cs`.
- 추가: `Assets/Scripts/Scene/Intro/IntroNineCutPlayer.cs`, `Assets/Scripts/Scene/Editor/IntroNineCutSetup.cs`와 각 `.meta`.
- 추가: `Assets/Datas/Intro/IntroNineCutSequence.json`, `Assets/Textures/art/Intro/NineCut/Scene01.png`~`Scene09.png`, `Assets/Sounds/Intro/NineCut/`의 아래 16개 WAV 및 Unity metadata.
- 기록: 이 문서. 기존 Packages 변경과 MCP 설치 파일/설정은 이번 인트로 작업에서 편집하지 않았다.

## 연결과 실행

- 인트로 화면 확인: IntroScene에서 사용자가 Play한다. 직접 실행 시 부트스트랩 manager가 없으므로 끝의 게임 전환은 기존 IntroSceneEntry가 오류를 알린다.
- 실제 1일 차 연결 확인: InitScene → Hub → 새 게임 → IntroScene → 기존 `TransitionToGameplayAsync()` 경로. 개인 Gameplay 씬 설정도 기존 경로를 따른다. 날짜/경제 상태를 새로 만들거나 변경하지 않는다.
- 마우스 왼쪽/Space/Enter: 출력 중 문장 완성, 다음 입력으로 다음 줄. 버튼 위 클릭은 대사 입력에서 제외한다.
- 오른쪽 위 `건너뛰기`: 기존 `CompleteImmediately()` 한 경로로 코루틴/음원을 정리하고 완료 이벤트를 발생시킨다. `IntroSceneEntry`의 기존 중복 전환 가드도 유지한다.

## Inspector

`IntroCanvas / IntroNineCutPlayer`:

- Artwork 9개, Picture / Picture Group / Card Text / Skip Button / Controller 연결 완료.
- Beats 104개에 이미지 순서, 화자·대사 35줄, 효과음 시작, 대기·페이드·약한 흔들림 시간이 저장돼 있다. 런타임에는 **Inspector의 저장값이 권위**다.
- Clips 16개와 IntroMusic / IntroAmbience / IntroEffects의 독립 2D AudioSource 연결 완료. Play On Awake는 모두 꺼져 있다.
- Music Volume 0.18 / Ambience Volume 0.22 / Effects Volume 0.55. 각 beat의 Value는 해당 음량의 배율이다.
- Confirmed Settlement / Confirmed Meal Dialogue는 출처 미확인으로 비워 두었다. 정산 텍스트는 행마다 표시하며 Settlement Tick Clip Index 15 / Row Delay 0.35초로 설정했다.
- 최초 생성용 데이터는 `Assets/Datas/Intro/IntroNineCutSequence.json`. `Apply Nine Cut Sequence` 메뉴는 이미 연결된 씬의 Inspector 조정을 덮어쓰지 않는다. JSON을 수정해도 기존 씬에 자동 적용되지는 않는다.

이미지는 `Assets/Textures/art/Intro/NineCut/Scene01.png`~`Scene09.png`이다. 첨부 파일을 픽셀 수정 없이 복사했다. 새 사본만 Sprite / Point / 무압축 / mipmap 없음으로 import했다. 비율 유지, Canvas Pixel Perfect를 사용하며 카메라와 기존 UI Transform은 유지한다. 모든 해상도에서 정수 확대 비율을 보장하는 것은 아니다.

| 컷 | 이미지 내용 | 첨부 이미지 번호 |
|---|---|---|
| 01 | 아빠가 하루를 업고 계단길을 내려감 | 3 |
| 02 | 할머니에게 먹을 것을 건넴 | 9 |
| 03 | 트럭에서 금속 용기를 건네며 짐 정리 | 7 |
| 04 | 트럭 생활공간에서 라면 식사 | 6 |
| 05 | 아빠가 혼절한 하루를 안음 | 8 |
| 06 | 병원 앞 경비의 제지 | 5 |
| 07 | 아빠가 하루를 감싸고 구타당함 | 4 |
| 08 | 1인칭, 바닥의 하루와 맞잡은 손 | 2 |
| 09 | 아래에서 올려다본 감독관 | 1 |

## 오디오 출처

`Assets/Sounds/Intro/NineCut/*.wav` 16개는 이번 작업에서 로컬 수치 합성으로 제작했다. 외부 음원 다운로드·샘플링·음성 더빙은 하지 않았다. 24 kHz, mono, 16-bit PCM이다. 기존 거래/진공청소기/총소리 클립을 다른 상황에 억지로 대입하지 않았다.

| 클립 | 용도 |
|---|---|
| LonelyTheme | 절제된 단음 선율과 낮은 지속음, 전반부 BGM |
| OminousDrone | 감독관 장면의 작은 저음 |
| Wind | 낮은 바람 환경음 |
| Burner | 식사와 혼절 직후 남는 버너 소리 |
| SlowStoneSteps | 첫 장면에서만 짧은 발걸음 |
| PaperRustle | 음식 포장지 |
| MetalContainer | 작은 금속 용기 |
| PotSimmer | 라면 페이드인 전 끓는 소리 |
| ChopsticksDrop | 혼절 전 검은 화면에서 젓가락 낙하 |
| BodyFall | 0.35초 후 둔한 낙하 |
| RunningStop | 병원 도입의 급한 발걸음·마찰 |
| BodyCollision | 제지 마지막 대사 후 암전 충돌 |
| DullImpact | 7컷 대사 후 각각 1회, 총 2회 |
| ApproachingShoes | 8컷 대사와 1.5초 정적 후 접근하는 구두 |
| LabouredBreath | 8·9컷의 합성 호흡 질감 |
| LedgerTick | 확정 정산 데이터가 채워지면 행마다 사용 |

실제 사람의 놀란 숨/신음 녹음, 별도의 시설 환경음 녹음은 미연결이다. 합성 호흡과 바람을 사용한다. 음원 파일의 길이·샘플·피크는 검사했으나 실제 청취 적합성은 사용자 확인이 필요하다.

## 미확인 내용과 검증

- 기존 4컷의 마지막은 라면 대사가 아니라 ‘하루에게 주어진 시간 / 31일’ 연출이며 대사 배열이 비어 있다. 새 4컷의 확정 대사와 인트로 정산 금액을 저장소에서 찾지 못했다. 임의 작성하지 않았다. 현재 4컷은 이미지·생활 소리·3초 유지로 이어진다.
- Unity 씬 연결 도구의 저장 후 검사: 이미지9 / 음원16 / 대사35 / beats104 / 구타 효과음2 / 기존 컷4 보존.
- 최종 Unity `Validate Nine Cut Sequence` 실행 로그 `STATIC VALIDATION OK` 확인. 새 파일28개의 metadata와 GUID 유일성 검사, `git diff --check` 통과.
- 별도 컴파일: 현재 Unity가 생성한 reference/define 응답 파일로 Runtime와 Scene.Editor C# 컴파일 성공. 출력은 Temp/IntroNineCut에만 저장.
- 정적 데이터 검사: 첨부 이미지 SHA256 일치9/9, 원래 씬 오브젝트·컴포넌트78개 보존, 기존 컷 데이터 동일, 35줄 대사/화자와 생성 데이터 일치, 5컷 효과음 당시 알파0, 8컷 구두음은 대사 후1.5초 이후, WAV16개 유효.
- 실행 입력, 연타/스킵, 실제 게임 진입, 재실행 음원 정리, 화면 가독성·한글 렌더·청취는 **미검증**. 사용자 Play Mode를 조작하지 않았다.
- 최종 상태: **PARTIAL**. Scene04 원문·금액과 실제 실행 확인이 남아 있다.

검사 증거는 `Temp/IntroNineCut/validation.txt`, `static-check.json`, `asset-report.json`이다. `Cashier/Intro/Validate Nine Cut Sequence`는 열린 인트로의 참조와 순서를 읽기 전용으로 검사한다.

## 2026-09-24: Scene01 구름과 Scene04 김

- `IntroAtmosphereGraphic` 두 개를 기존 `NineCutArtwork`의 자식으로 연결했다. Scene01은 원본 하늘의 작은 영역만 좌우 최대 12픽셀, 80초 주기로 이동한다. Scene04는 기존 `ChimneySmoke0` 텍스처를 냄비 위에서 세 가닥으로 옅게 띄운다.
- 원본 이미지와 기존 RectTransform, 대사, 오디오, 컷 타이밍은 유지한다. 영역·이동 폭·주기·색상/투명도는 각 효과의 Inspector에서 조절한다.
- 기존 재생기의 시간을 공유하므로 스킵 확인창에서 멈춘다. 컷 전환·정지·비활성화 시 효과 상태를 정리하며 그림의 CanvasGroup 페이드를 상속한다.
- 변경 파일: `IntroAtmosphereGraphic.cs` 및 meta, 전용 Inspector/저장 씬 프리뷰 `IntroAtmosphereGraphicEditor.cs` 및 meta, `IntroNineCutPlayer.cs`, `IntroScene.unity`, 이 문서.
- 검증: Runtime/Editor 별도 C# 컴파일 성공, 저장 씬의 기존 오브젝트 삭제 0개/추가 GameObject 2개. 기존 직렬화 변경은 artwork 자식 목록과 플레이어의 효과 참조뿐이다.
- 초기 프리뷰에서 영역이 0으로 읽혀 Rect의 serializedVersion을 보완했다. 보완 이후 Unity MCP가 응답하지 않아 최종 프레임 비교와 Play Mode의 표시·일시 정지·스킵 동작은 **미검증**이다. 이전 프리뷰의 PASS 문자열은 최종 시각 검증 증거가 아니다.
