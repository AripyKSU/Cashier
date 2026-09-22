using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>인트로 한 줄의 화자, 본문과 그 줄에서 켤 연출 오브젝트.</summary>
[Serializable]
public sealed class IntroDialogueLine
{
    [SerializeField] private string speaker;
    [SerializeField, TextArea(1, 3)] private string body;
    [Tooltip("이 줄이 시작될 때 활성화할 연출 오브젝트. 예: CUT 3의 가판 영업 허가증.")]
    [SerializeField] private GameObject revealOnStart;

    /// <summary>화자 이름. 비우면 화자 표시를 숨긴다.</summary>
    public string Speaker => speaker;
    /// <summary>Typewriter로 출력할 본문.</summary>
    public string Body => body;
    /// <summary>줄 시작 시 켤 연출 오브젝트. 없으면 null.</summary>
    public GameObject RevealOnStart => revealOnStart;

    /// <summary>기본 대사 채우기에서 사용하는 생성자.</summary>
    /// <param name="speaker">화자 이름.</param>
    /// <param name="body">본문.</param>
    public IntroDialogueLine(string speaker, string body)
    {
        this.speaker = speaker;
        this.body = body;
    }
}

/// <summary>인트로 한 CUT의 배경 오브젝트와 대사 목록.</summary>
[Serializable]
public sealed class IntroCut
{
    [SerializeField] private string label;
    [Tooltip("이 CUT 동안만 켜 둘 배경 오브젝트.")]
    [SerializeField] private GameObject root;
    [Tooltip("끄면 대화창 없이 CUT 4 전용 연출을 재생한다.")]
    [SerializeField] private bool useDialogueBox = true;
    [SerializeField] private IntroDialogueLine[] lines = Array.Empty<IntroDialogueLine>();

    /// <summary>Inspector 식별용 이름.</summary>
    public string Label => label;
    /// <summary>이 CUT의 배경 오브젝트.</summary>
    public GameObject Root => root;
    /// <summary>일반 대화창 사용 여부. CUT 4만 false다.</summary>
    public bool UseDialogueBox => useDialogueBox;
    /// <summary>순서대로 출력할 대사.</summary>
    public IntroDialogueLine[] Lines => lines;

    /// <summary>기본 대사 채우기에서 사용하는 생성자.</summary>
    /// <param name="label">Inspector 식별용 이름.</param>
    /// <param name="useDialogueBox">일반 대화창 사용 여부.</param>
    /// <param name="lines">대사 목록.</param>
    public IntroCut(string label, bool useDialogueBox, IntroDialogueLine[] lines)
    {
        this.label = label;
        this.useDialogueBox = useDialogueBox;
        this.lines = lines ?? Array.Empty<IntroDialogueLine>();
    }

    /// <summary>기본 대사를 다시 채울 때 기존 배경 연결을 보존한다.</summary>
    /// <param name="value">보존할 배경 오브젝트.</param>
    public void RestoreRoot(GameObject value) => root = value;
}

/// <summary>
/// 인트로 4 CUT의 대사 출력과 진행 입력을 담당한다.
/// 진행 규칙은 소유하지 않는다. 인트로가 끝나면 이미 진입해 있는 기존 GameProgress / DayProgress 흐름이 그대로 이어진다.
/// 입력 차단은 EventSystem을 끄지 않고 전체 화면 Graphic의 Raycast Target으로 처리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class IntroDialogueController : MonoBehaviour
{
    private const float DialogueVoiceDurationSeconds = 1.2f;
    private const float DialogueVoiceVolumeScale = 0.75f;

    [Header("Root / input block")]
    [Tooltip("인트로 전체를 담는 CanvasGroup. 재생 중 blocksRaycasts로 하위 게임 UI를 잠근다.")]
    [SerializeField] private CanvasGroup introRoot;
    [Tooltip("전체 화면을 덮는 Graphic. Raycast Target이 켜져야 뒤쪽 게임 UI가 클릭을 받지 않는다.")]
    [SerializeField] private Graphic inputBlocker;
    [Tooltip("인트로 완료 후 이 오브젝트를 비활성화한다. 비우면 introRoot의 오브젝트를 사용한다.")]
    [SerializeField] private GameObject deactivateOnComplete;

    [Header("Dialogue box")]
    [SerializeField] private AutoSizeNineSliceDialogueBox dialogueBox;
    [Tooltip("대화창 전체를 켜고 끄는 오브젝트. CUT 4에서 숨긴다.")]
    [SerializeField] private GameObject dialogueBoxObject;
    [SerializeField] private TextMeshProUGUI bodyText;
    [Tooltip("화자 이름 표시. 없으면 비워도 된다.")]
    [SerializeField] private TextMeshProUGUI speakerText;
    [Tooltip("화자 이름을 감싸는 오브젝트. 화자가 빈 줄에서 숨긴다.")]
    [SerializeField] private GameObject speakerObject;

    [Header("Typewriter")]
    [Tooltip("글자 하나당 출력 시간(초).")]
    [SerializeField, Min(0.001f)] private float secondsPerCharacter = 0.035f;
    [Tooltip("줄 출력이 끝난 뒤 다음 입력을 받기까지의 최소 대기(초).")]
    [SerializeField, Min(0f)] private float lineEndDelaySeconds = 0.05f;
    [Tooltip("대사 시작마다 기존 대화 음성 SFX를 재생한다.")]
    [SerializeField] private bool playDialogueVoice = true;

    [Header("Input")]
    [Tooltip("마우스 왼쪽 버튼으로 다음 대사를 진행한다.")]
    [SerializeField] private bool advanceWithMouse = true;
    [Tooltip("Space / Enter로도 다음 대사를 진행한다.")]
    [SerializeField] private bool advanceWithKeyboard = true;

    [Header("Title")]
    [Tooltip("인트로 앞에 보여줄 타이틀 화면 오브젝트. 비우면 타이틀 단계를 건너뛴다.")]
    [SerializeField] private GameObject titleRoot;
    [Tooltip("타이틀 표시 직후 이 시간(초) 동안은 클릭을 받지 않는다.")]
    [SerializeField, Min(0f)] private float titleClickGuardSeconds = 2f;

    [Header("Cuts")]
    [SerializeField] private IntroCut[] cuts = Array.Empty<IntroCut>();

    [Header("CUT 4 - 남은 시간 표시")]
    [Tooltip("\"하루에게 주어진 시간\" + \"31일\"을 담는 CanvasGroup.")]
    [SerializeField] private CanvasGroup remainingTimeGroup;
    [SerializeField] private TextMeshProUGUI remainingTimeCaptionText;
    [SerializeField] private TextMeshProUGUI remainingDaysText;
    [Tooltip("\"DAY 1\"을 담는 CanvasGroup.")]
    [SerializeField] private CanvasGroup dayLabelGroup;
    [SerializeField] private TextMeshProUGUI dayLabelText;
    [SerializeField] private string remainingTimeCaption = "하루에게 주어진 시간";
    [SerializeField, Min(1)] private int remainingDays = 31;
    [SerializeField, Min(1)] private int startDayNumber = 1;
    [SerializeField, Min(0f)] private float finalFadeSeconds = 0.6f;
    [SerializeField, Min(0f)] private float remainingTimeHoldSeconds = 2.4f;
    [SerializeField, Min(0f)] private float dayLabelHoldSeconds = 1.6f;

    [Header("Playback")]
    [Tooltip("씬 진입 시 자동으로 인트로를 재생한다.")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("Editor에서 Escape로 인트로를 건너뛴다. 대사 진행 입력과는 무관하다.")]
    [SerializeField] private bool allowEditorSkip;

    /// <summary>인트로가 모두 끝났을 때 발생한다. 기존 게임 시작 흐름은 이미 진행 중이다.</summary>
    [SerializeField] private UnityEvent onIntroCompleted;

    private Coroutine playback;
    private bool isTyping;

    /// <summary>인트로가 모두 끝났을 때 발생한다.</summary>
    public event Action IntroCompleted;

    /// <summary>인트로가 재생 중인지 여부.</summary>
    public bool IsPlaying => playback != null;

    /// <summary>현재 줄이 Typewriter로 출력되는 중인지 여부. true인 동안 모든 진행 입력을 무시한다.</summary>
    public bool IsTyping => isTyping;

    /// <summary>재생 전에 인트로를 잠긴 상태로 세운다.</summary>
    private void Awake()
    {
        setBlocking(true);
        if (remainingTimeGroup != null) remainingTimeGroup.alpha = 0f;
        if (dayLabelGroup != null) dayLabelGroup.alpha = 0f;
        if (remainingTimeCaptionText != null) remainingTimeCaptionText.text = remainingTimeCaption;
        if (remainingDaysText != null) remainingDaysText.text = $"{remainingDays}일";
        if (dayLabelText != null) dayLabelText.text = $"DAY {startDayNumber}";
        if (titleRoot != null) titleRoot.SetActive(false);
        setDialogueBoxVisible(false);
        foreach (IntroCut cut in cuts)
        {
            if (cut?.Root != null) cut.Root.SetActive(false);
        }
    }

    /// <summary>씬 진입 시 인트로를 시작한다.</summary>
    private void Start()
    {
        if (playOnStart) Play();
    }

    /// <summary>Editor 전용 건너뛰기 입력만 확인한다. 대사 진행 입력은 코루틴이 소유한다.</summary>
    private void Update()
    {
        if (!allowEditorSkip || !IsPlaying) return;
#if UNITY_EDITOR
        if (wasSkipPressedThisFrame()) CompleteImmediately();
#endif
    }

    /// <summary>화면이 꺼지면 진행 중인 연출을 중단한다.</summary>
    private void OnDisable() => stopPlayback();

    /// <summary>인트로를 처음부터 재생한다. 이미 재생 중이면 무시한다.</summary>
    public void Play()
    {
        if (IsPlaying) return;
        if (cuts.Length == 0)
        {
            Debug.LogError("[IntroDialogueController] CUT이 비어 있습니다. 컨텍스트 메뉴의 '인트로 기본 대사 채우기'로 채우세요.", this);
            return;
        }

        if (bodyText == null || dialogueBox == null)
        {
            Debug.LogError("[IntroDialogueController] bodyText와 dialogueBox 참조가 필요합니다.", this);
            return;
        }

        setBlocking(true);
        playback = StartCoroutine(runIntro());
    }

    /// <summary>남은 연출을 생략하고 인트로를 즉시 끝낸다. 진행 입력에는 연결하지 않는다.</summary>
    public void CompleteImmediately()
    {
        if (!IsPlaying) return;
        stopPlayback();
        complete();
    }

    /// <summary>CUT을 순서대로 재생하고 마지막에 잠금을 푼다.</summary>
    /// <returns>인트로 전체 연출.</returns>
    private IEnumerator runIntro()
    {
        if (titleRoot != null) yield return playTitle();

        for (int cutIndex = 0; cutIndex < cuts.Length; cutIndex++)
        {
            IntroCut cut = cuts[cutIndex];
            if (cut == null) continue;

            activateCutRoot(cutIndex);
            if (cut.UseDialogueBox)
            {
                setDialogueBoxVisible(true);
                foreach (IntroDialogueLine line in cut.Lines)
                {
                    if (line == null) continue;
                    yield return playLine(line);
                }
            }
            else
            {
                // CUT 4는 일반 대화창을 쓰지 않는다.
                setDialogueBoxVisible(false);
                yield return playRemainingTimeCut();
            }
        }

        playback = null;
        complete();
    }

    /// <summary>
    /// 한 줄을 출력한 뒤 진행 입력을 기다린다.
    /// 출력 중에는 입력을 읽지 않으며, 출력이 끝나면 버튼을 뗀 것을 먼저 확인하고
    /// 그 이후에 새로 발생한 Down만 다음 대사로 인정한다.
    /// </summary>
    /// <param name="line">출력할 대사.</param>
    /// <returns>한 줄의 출력과 진행 입력 대기.</returns>
    private IEnumerator playLine(IntroDialogueLine line)
    {
        if (line.RevealOnStart != null) line.RevealOnStart.SetActive(true);
        applySpeaker(line.Speaker);

        // 전체 문장 기준으로 대화창 크기를 먼저 확정해야 글자마다 박스가 흔들리지 않는다.
        dialogueBox.Apply(line.Body);

        bodyText.text = line.Body ?? string.Empty;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate(true, true);

        if (playDialogueVoice)
        {
            SoundManager.Instance?.PlaySfxForDuration(
                SoundKeys.DialogueVoice, DialogueVoiceDurationSeconds, DialogueVoiceVolumeScale);
        }

        int characterCount = bodyText.textInfo.characterCount;
        isTyping = true;
        float elapsedSeconds = 0f;
        while (bodyText.maxVisibleCharacters < characterCount)
        {
            // 출력 중에는 어떤 진행 입력도 읽지 않는다. 클릭 연타로 문장이 완성되지 않는다.
            elapsedSeconds += Time.unscaledDeltaTime;
            bodyText.maxVisibleCharacters = Mathf.Min(
                characterCount, Mathf.FloorToInt(elapsedSeconds / secondsPerCharacter));
            yield return null;
        }

        bodyText.maxVisibleCharacters = characterCount;
        isTyping = false;

        float delayRemaining = lineEndDelaySeconds;
        while (delayRemaining > 0f)
        {
            delayRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return waitForFreshAdvance();
    }

    /// <summary>타이틀을 보여주고 가드 시간이 지난 뒤 새 클릭으로 인트로에 진입한다.</summary>
    /// <returns>타이틀 단계 연출.</returns>
    private IEnumerator playTitle()
    {
        titleRoot.SetActive(true);

        // 표시 직후의 오클릭을 막는다. 이 동안은 입력을 읽지 않는다.
        yield return hold(titleClickGuardSeconds);
        yield return waitForFreshAdvance();

        titleRoot.SetActive(false);
    }

    /// <summary>
    /// 누르고 있던 버튼이 떨어진 것을 먼저 확인하고, 그 이후 새로 발생한 Down만 진행으로 인정한다.
    /// 대사 진행과 타이틀 진입이 같은 규칙을 공유한다.
    /// </summary>
    /// <returns>새 진행 입력 대기.</returns>
    private IEnumerator waitForFreshAdvance()
    {
        // 1) 이미 누르고 있던 버튼이 떨어질 때까지 기다린다.
        while (isAdvanceHeld()) yield return null;

        // 2) 뗀 프레임의 입력을 소비하지 않도록 한 프레임 넘긴 뒤부터 새 Down만 받는다.
        yield return null;
        while (!wasAdvancePressedThisFrame()) yield return null;
    }

    /// <summary>CUT 4의 남은 시간과 시작 일차를 순서대로 표시한다.</summary>
    /// <returns>CUT 4 연출.</returns>
    private IEnumerator playRemainingTimeCut()
    {
        if (remainingTimeCaptionText != null) remainingTimeCaptionText.text = remainingTimeCaption;
        if (remainingDaysText != null) remainingDaysText.text = $"{remainingDays}일";
        if (dayLabelText != null) dayLabelText.text = $"DAY {startDayNumber}";

        yield return fadeGroup(remainingTimeGroup, 0f, 1f, finalFadeSeconds);
        yield return hold(remainingTimeHoldSeconds);
        yield return fadeGroup(remainingTimeGroup, 1f, 0f, finalFadeSeconds);

        yield return fadeGroup(dayLabelGroup, 0f, 1f, finalFadeSeconds);
        yield return hold(dayLabelHoldSeconds);
        yield return fadeGroup(dayLabelGroup, 1f, 0f, finalFadeSeconds);
    }

    /// <summary>CanvasGroup 알파를 실제 시간 기준으로 보간한다.</summary>
    /// <param name="group">대상 그룹. null이면 즉시 끝난다.</param>
    /// <param name="from">시작 알파.</param>
    /// <param name="to">종료 알파.</param>
    /// <param name="durationSeconds">보간 시간(초).</param>
    /// <returns>페이드 연출.</returns>
    private IEnumerator fadeGroup(CanvasGroup group, float from, float to, float durationSeconds)
    {
        if (group == null) yield break;
        if (durationSeconds <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsedSeconds = 0f;
        group.alpha = from;
        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsedSeconds / durationSeconds));
            yield return null;
        }

        group.alpha = to;
    }

    /// <summary>실제 시간 기준으로 대기한다.</summary>
    /// <param name="seconds">대기 시간(초).</param>
    /// <returns>대기 연출.</returns>
    private IEnumerator hold(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>지정한 CUT의 배경만 켠다.</summary>
    /// <param name="activeIndex">활성화할 CUT 번호.</param>
    private void activateCutRoot(int activeIndex)
    {
        for (int index = 0; index < cuts.Length; index++)
        {
            GameObject root = cuts[index]?.Root;
            if (root != null) root.SetActive(index == activeIndex);
        }
    }

    /// <summary>화자 이름을 반영하고 빈 화자에서는 표시를 숨긴다.</summary>
    /// <param name="speaker">이번 줄의 화자.</param>
    private void applySpeaker(string speaker)
    {
        bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
        if (speakerText != null) speakerText.text = hasSpeaker ? speaker : string.Empty;
        if (speakerObject != null) speakerObject.SetActive(hasSpeaker);
    }

    /// <summary>대화창 표시 여부를 적용한다.</summary>
    /// <param name="visible">표시하면 true.</param>
    private void setDialogueBoxVisible(bool visible)
    {
        if (dialogueBoxObject != null) dialogueBoxObject.SetActive(visible);
        else if (dialogueBox != null) dialogueBox.gameObject.SetActive(visible);
    }

    /// <summary>인트로의 입력 차단 상태를 적용한다. EventSystem은 건드리지 않는다.</summary>
    /// <param name="blocking">차단하면 true.</param>
    private void setBlocking(bool blocking)
    {
        if (inputBlocker != null) inputBlocker.raycastTarget = blocking;
        if (introRoot == null) return;
        introRoot.blocksRaycasts = blocking;
        introRoot.interactable = blocking;
    }

    /// <summary>진행 중인 연출을 중단한다.</summary>
    private void stopPlayback()
    {
        if (playback == null) return;
        StopCoroutine(playback);
        playback = null;
        isTyping = false;
    }

    /// <summary>잠금을 풀고 인트로 오브젝트를 숨긴 뒤 완료를 알린다.</summary>
    private void complete()
    {
        isTyping = false;
        setBlocking(false);
        if (introRoot != null) introRoot.alpha = 0f;
        SoundManager.Instance?.StopSfxForDuration(SoundKeys.DialogueVoice);

        GameObject target = deactivateOnComplete != null
            ? deactivateOnComplete
            : introRoot != null ? introRoot.gameObject : null;
        if (target != null) target.SetActive(false);

        onIntroCompleted?.Invoke();
        IntroCompleted?.Invoke();
    }

    /// <summary>진행 입력이 현재 눌려 있는지 확인한다.</summary>
    /// <returns>마우스 또는 키보드 진행 입력이 눌린 상태면 true.</returns>
    private bool isAdvanceHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (advanceWithMouse && Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        if (!advanceWithKeyboard) return false;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
#else
        if (advanceWithMouse && Input.GetMouseButton(0)) return true;
        if (!advanceWithKeyboard) return false;
        return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
#endif
    }

    /// <summary>이번 프레임에 새 진행 입력이 시작됐는지 확인한다.</summary>
    /// <returns>이번 프레임에 Down이 발생했으면 true.</returns>
    private bool wasAdvancePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (advanceWithMouse && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (!advanceWithKeyboard) return false;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.spaceKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame);
#else
        if (advanceWithMouse && Input.GetMouseButtonDown(0)) return true;
        if (!advanceWithKeyboard) return false;
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

#if UNITY_EDITOR
    /// <summary>Editor 전용 건너뛰기 입력을 확인한다.</summary>
    /// <returns>이번 프레임에 Escape가 눌렸으면 true.</returns>
    private bool wasSkipPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>인트로 4 CUT의 기본 대사를 채운다. 이미 연결한 배경 오브젝트는 유지한다.</summary>
    [ContextMenu("인트로 기본 대사 채우기")]
    private void fillDefaultScript()
    {
        GameObject[] keptRoots = new GameObject[4];
        for (int index = 0; index < keptRoots.Length && index < cuts.Length; index++)
        {
            keptRoots[index] = cuts[index]?.Root;
        }

        cuts = new[]
        {
            new IntroCut("CUT 1 - 밤의 낡은 방", true, new[]
            {
                new IntroDialogueLine("하루", "아빠…"),
            }),
            new IntroCut("CUT 2 - 병원 입구", true, new[]
            {
                new IntroDialogueLine("경비", "시민권 없이는 들어갈 수 없습니다."),
                new IntroDialogueLine("아빠", "애가 죽어가고 있습니다."),
                new IntroDialogueLine("의료진", "지금 치료하지 않으면… 길어야 한 달입니다."),
            }),
            new IntroCut("CUT 3 - 병원 밖 / 감독관", true, new[]
            {
                new IntroDialogueLine("감독관", "살리고 싶나?"),
                new IntroDialogueLine("감독관", "저 안에 들어갈 수만 있다면, 살릴 방법은 있지."),
                new IntroDialogueLine("감독관", "하지만 네 힘으론 불가능해."),
                new IntroDialogueLine("감독관", "내 밑에서 일해."),
                new IntroDialogueLine("감독관", "가판 하나는 내주지."),
                new IntroDialogueLine("감독관", "물건 값은 네가 정해."),
                new IntroDialogueLine("감독관", "시키는 만큼 벌어와."),
                new IntroDialogueLine("감독관", "그러면 네 딸이 살 수 있게 내가 손써주지."),
                new IntroDialogueLine("감독관", "시간은 많지 않을 텐데."),
            }),
            new IntroCut("CUT 4 - 집 / 남은 시간", false, Array.Empty<IntroDialogueLine>()),
        };

        for (int index = 0; index < cuts.Length && index < keptRoots.Length; index++)
        {
            cuts[index].RestoreRoot(keptRoots[index]);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[IntroDialogueController] 인트로 기본 대사를 채웠습니다. CUT 3의 허가증 연출은 '내 밑에서 일해.' 줄의 Reveal On Start에 연결하세요.", this);
    }
#endif
}
