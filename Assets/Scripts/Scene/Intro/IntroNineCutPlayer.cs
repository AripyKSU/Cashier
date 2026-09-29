using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>하루 인트로의 직렬화된 한 연출. 시간은 초, 값은 알파/음량/흔들림 픽셀이다.</summary>
[Serializable]
public sealed class IntroSequenceBeat
{
    public string label;
    public IntroBeatKind kind;
    public int index;
    public string speaker;
    [TextArea(1, 4)] public string text;
    [Min(0f)] public float seconds;
    public float value;
    /// <summary>정산 직전 이미지 암전과 동시에 BGM을 낮추는 구간에서만 사용한다.</summary>
    public bool fadeMusic;
    public float musicTarget = .15f;
}

/// <summary>한 번씩 순차 실행하는 인트로 연출 종류.</summary>
public enum IntroBeatKind { Image, Fade, Wait, Line, Effect, Music, Ambience, MusicLevel, Shake, Card, StopAudio, MealDialogue, ConcurrentShake }

/// <summary>기존 인트로 대화 UI/완료 경로를 사용하며 인트로 컷의 입력, 연출과 로컬 오디오 수명을 소유한다.</summary>
public sealed class IntroNineCutPlayer : MonoBehaviour
{
    [Header("Confirmed art, in Scene 01, 02, 02.5, 03–09 order")]
    [SerializeField] private Sprite[] artwork;
    [SerializeField] private Image picture;
    [SerializeField] private CanvasGroup pictureGroup;
    [SerializeField] private TextMeshProUGUI cardText;
    [SerializeField] private Button skipButton;
    [SerializeField] private IntroDialogueController controller;
    [Header("Skip confirmation")]
    [SerializeField] private GameObject skipConfirmation;
    [SerializeField] private Button confirmYes;
    [SerializeField] private Button confirmNo;
    [SerializeField] private IntroMealSequence mealSequence;
    [Header("Subtle cloud and steam overlays")]
    [SerializeField] private IntroAtmosphereGraphic[] atmosphere = Array.Empty<IntroAtmosphereGraphic>();
    [Header("Sequence (editable timings / dialogue)")]
    [SerializeField] private IntroSequenceBeat[] beats;
    [Header("Audio clips; beat index selects a slot")]
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource ambience;
    [SerializeField] private AudioSource effects;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.8f;
    /// <summary>하루 대사 타이핑 때 재생할 짧은 글자 효과음 슬롯.</summary>
    [SerializeField, Min(0)] private int textBlipClipIndex = 17;
    /// <summary>아빠 대사 타이핑 때 재생할 짧은 글자 효과음 슬롯.</summary>
    [SerializeField, Min(0)] private int fatherTextBlipClipIndex = 20;
    /// <summary>감독관 대사 타이핑 때 재생할 짧은 글자 효과음 슬롯.</summary>
    [SerializeField, Min(0)] private int inspectorTextBlipClipIndex = 21;
    /// <summary>전체 효과음 음량에 곱하는 하루 글자 효과음 배율.</summary>
    [SerializeField, Range(0f, 2f)] private float textBlipVolumeScale = 0.4f;
    /// <summary>전체 효과음 음량에 곱하는 아빠 글자 효과음 배율.</summary>
    [SerializeField, Range(0f, 2f)] private float fatherTextBlipVolumeScale = 0.45f;
    /// <summary>전체 효과음 음량에 곱하는 감독관 글자 효과음 배율.</summary>
    [SerializeField, Range(0f, 2f)] private float inspectorTextBlipVolumeScale = 0.25f;
    [Header("Scene 04: confirmed dialogue")]
    [SerializeField, TextArea] private string confirmedSettlement;
    [SerializeField] private IntroDialogueLine[] confirmedMealDialogue = Array.Empty<IntroDialogueLine>();
    [SerializeField, Min(0)] private int settlementTickClipIndex = 15;
    [SerializeField, Min(0f)] private float settlementRowDelaySeconds = .35f;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private Vector2 picturePosition;
    private Vector3 pictureScale;
    private Vector2 shakeOffset;
    private float shakeZoom;
    private float musicGain;
    private float ambienceGain;
    private bool running;
    private bool typing;
    private bool isConfirmingSkip;
    private int suppressAdvanceFrame = -1;
    private float playbackDelta => isConfirmingSkip ? 0f : Time.unscaledDeltaTime;

    /// <summary>타이핑 중인지 기존 컨트롤러에 제공한다.</summary>
    public bool IsTyping => typing;

    /// <summary>스킵을 기존 완료 API에만 연결한다.</summary>
    private void Awake()
    {
        picturePosition = picture.rectTransform.anchoredPosition;
        pictureScale = picture.rectTransform.localScale;
        skipButton.onClick.AddListener(requestSkip);
        if (confirmYes != null) confirmYes.onClick.AddListener(confirmSkip);
        if (confirmNo != null) confirmNo.onClick.AddListener(cancelSkip);
        if (skipConfirmation != null) skipConfirmation.SetActive(false);
    }

    /// <summary>Inspector의 독립 음량 조절값을 현재 재생에 반영한다.</summary>
    private void Update()
    {
        if (!running) return;
        float delta = playbackDelta;
        picture.rectTransform.anchoredPosition = picturePosition + shakeOffset;
        picture.rectTransform.localScale = pictureScale * (1f + shakeZoom);
        foreach (IntroAtmosphereGraphic layer in atmosphere) layer.Tick(delta);
        music.volume = musicVolume * musicGain;
        ambience.volume = ambienceVolume * ambienceGain;
        effects.volume = effectsVolume;
    }

    /// <summary>씬 종료나 스킵에서 모든 로컬 음원을 정리한다.</summary>
    private void OnDisable() => Stop();

    /// <summary>소유한 버튼 구독을 제거한다.</summary>
    private void OnDestroy()
    {
        if (skipButton != null && controller != null)
            skipButton.onClick.RemoveListener(requestSkip);
        if (confirmYes != null) confirmYes.onClick.RemoveListener(confirmSkip);
        if (confirmNo != null) confirmNo.onClick.RemoveListener(cancelSkip);
    }

    /// <summary>컨트롤러가 소유한 코루틴에서 순차 재생한다. 연출 중에는 진행 입력을 소비하지 않는다.</summary>
    /// <param name="box">기존 자동 크기 대화창.</param>
    /// <param name="body">기존 한글 본문.</param>
    /// <param name="speaker">기존 화자.</param>
    /// <param name="boxObject">대화창 표시 대상.</param>
    /// <param name="speakerObject">화자 표시 대상.</param>
    /// <param name="characterSeconds">글자당 출력 시간.</param>
    /// <returns>인트로 연출.</returns>
    public IEnumerator Run(AutoSizeNineSliceDialogueBox box, TextMeshProUGUI body,
        TextMeshProUGUI speaker, GameObject boxObject, GameObject speakerObject, float characterSeconds)
    {
        Stop();
        SoundManager.Instance?.StopBgm();
        running = true;
        pictureGroup.alpha = 0f;
        picture.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(true);
        cardText.gameObject.SetActive(false);
        foreach (IntroSequenceBeat beat in beats)
        {
            while (isConfirmingSkip) yield return null;
            boxObject.SetActive(false);
            speakerObject.SetActive(false);
            switch (beat.kind)
            {
                case IntroBeatKind.Image:
                    picture.sprite = artwork[beat.index];
                    picture.preserveAspect = true;
                    foreach (IntroAtmosphereGraphic layer in atmosphere) layer.ShowCut(beat.index);
                    break;
                case IntroBeatKind.Fade:
                    yield return fade(beat.value, beat.seconds, beat.fadeMusic, beat.musicTarget);
                    break;
                case IntroBeatKind.Wait:
                    yield return hold(beat.seconds);
                    break;
                case IntroBeatKind.Line:
                    boxObject.SetActive(true);
                    speakerObject.SetActive(false);
                    speaker.text = string.Empty;
                    yield return line(box, body, beat.speaker, dialogueText(beat.speaker, beat.text), characterSeconds);
                    break;
                case IntroBeatKind.Effect:
                    if (beat.index == 8 && mealSequence != null) mealSequence.StopRoom();
                    effects.volume = effectsVolume;
                    if (clips[beat.index] != null) effects.PlayOneShot(clips[beat.index], beat.value);
                    break;
                case IntroBeatKind.Music:
                    if (beat.index == 1 && mealSequence != null) mealSequence.StopRoom();
                    musicGain = beat.value;
                    loop(music, beat.index, musicVolume * musicGain);
                    break;
                case IntroBeatKind.Ambience:
                    ambienceGain = beat.value;
                    loop(ambience, beat.index, ambienceVolume * ambienceGain);
                    break;
                case IntroBeatKind.MusicLevel:
                    float from = musicGain;
                    float elapsed = 0f;
                    while (elapsed < beat.seconds)
                    {
                        elapsed += playbackDelta;
                        musicGain = Mathf.Lerp(from, beat.value, elapsed / beat.seconds);
                        yield return null;
                    }
                    musicGain = beat.value;
                    break;
                case IntroBeatKind.Shake:
                    yield return shake(beat.value, beat.seconds);
                    break;
                case IntroBeatKind.ConcurrentShake:
                    StartCoroutine(shake(beat.value, beat.seconds));
                    break;
                case IntroBeatKind.Card:
                    if (beat.index == 4 && mealSequence != null)
                    {
                        yield return mealSequence.PlaySettlement(() => playbackDelta);
                        break;
                    }
                    // Scene 04는 출처가 확인된 데이터가 있을 때만 표시한다. 금액을 만들지 않는다.
                    string text = beat.index == 4 ? confirmedSettlement : beat.text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        cardText.text = string.Empty;
                        cardText.gameObject.SetActive(true);
                        if (beat.index == 4)
                        {
                            foreach (string row in text.Split('\n'))
                            {
                                if (string.IsNullOrWhiteSpace(row)) continue;
                                cardText.text += row + "\n";
                                if (clips[settlementTickClipIndex] != null)
                                    effects.PlayOneShot(clips[settlementTickClipIndex], .5f);
                                yield return hold(settlementRowDelaySeconds);
                            }
                        }
                        else cardText.text = text;
                        yield return hold(beat.seconds);
                        cardText.gameObject.SetActive(false);
                    }
                    break;
                case IntroBeatKind.MealDialogue:
                    for (int mealIndex = 0; mealIndex < confirmedMealDialogue.Length; mealIndex++)
                    {
                        IntroDialogueLine meal = confirmedMealDialogue[mealIndex];
                        boxObject.SetActive(true);
                        speakerObject.SetActive(false);
                        speaker.text = string.Empty;
                        Action onTyped = mealIndex == confirmedMealDialogue.Length - 1 && mealSequence != null
                            ? mealSequence.PlayEatingSound : (Action)null;
                        yield return line(box, body, meal.Speaker, dialogueText(meal.Speaker, meal.Body), characterSeconds, onTyped);
                    }
                    break;
                case IntroBeatKind.StopAudio:
                    float initialMusic = musicGain;
                    float initialAmbience = ambienceGain;
                    float remaining = beat.seconds;
                    while (remaining > 0f)
                    {
                        remaining = Mathf.Max(0f, remaining - playbackDelta);
                        musicGain = initialMusic * remaining / beat.seconds;
                        ambienceGain = initialAmbience * remaining / beat.seconds;
                        yield return null;
                    }
                    music.Stop(); ambience.Stop(); effects.Stop();
                    break;
            }
        }
        Stop();
    }

    /// <summary>음원과 임시 표시를 정리하여 재실행 시 중첩을 막는다.</summary>
    public void Stop()
    {
        running = false;
        typing = false;
        isConfirmingSkip = false;
        foreach (IntroAtmosphereGraphic layer in atmosphere) layer.ShowCut(-1);
        if (skipConfirmation != null) skipConfirmation.SetActive(false);
        if (mealSequence != null) mealSequence.ResetPresentation();
        if (music != null) music.Stop();
        if (ambience != null) ambience.Stop();
        if (effects != null) effects.Stop();
        shakeOffset = Vector2.zero;
        shakeZoom = 0f;
        if (picture != null)
        {
            picture.rectTransform.anchoredPosition = picturePosition;
            picture.rectTransform.localScale = pictureScale;
        }
        if (cardText != null) cardText.gameObject.SetActive(false);
    }

    /// <summary>대사 전체 크기를 먼저 확정하고 입력 한 번으로 완성, 다음 입력으로 진행한다.</summary>
    /// <param name="box">기존 대화창.</param>
    /// <param name="body">표시 텍스트.</param>
    /// <param name="speakerName">화자별 글자 효과음을 선택할 이름.</param>
    /// <param name="text">확정 대사.</param>
    /// <param name="seconds">글자당 시간.</param>
    /// <param name="onTyped">문장 완성 때 한 번 호출하는 선택적 효과음 이벤트.</param>
    /// <returns>완성과 진행 입력 대기.</returns>
    private IEnumerator line(AutoSizeNineSliceDialogueBox box, TextMeshProUGUI body, string speakerName, string text, float seconds, Action onTyped = null)
    {
        box.Apply(text);
        body.text = text;
        body.maxVisibleCharacters = 0;
        body.ForceMeshUpdate(true, true);
        int count = body.textInfo.characterCount;
        typing = true;
        float elapsed = 0f;
        float characterDelaySeconds = Mathf.Max(.001f, seconds);
        int soundCharacterCount = 0;
        yield return null; // 이전 줄의 Down이 새 줄을 완성하지 않게 한다.
        while (body.maxVisibleCharacters < count)
        {
            if (advance()) break;
            elapsed += playbackDelta;
            if (elapsed >= characterDelaySeconds)
            {
                elapsed -= characterDelaySeconds;
                int previousVisibleCharacters = body.maxVisibleCharacters;
                body.maxVisibleCharacters = previousVisibleCharacters + 1;
                char revealedCharacter = body.textInfo.characterInfo[previousVisibleCharacters].character;
                if (char.IsLetterOrDigit(revealedCharacter))
                {
                    soundCharacterCount++;
                    if ((soundCharacterCount & 1) == 1)
                        playTextBlip(body, speakerName, previousVisibleCharacters, body.maxVisibleCharacters);
                }
            }
            yield return null;
        }
        body.maxVisibleCharacters = count;
        while (isConfirmingSkip) yield return null;
        typing = false;
        onTyped?.Invoke();
        yield return null;
        while (!advance()) yield return null;
    }

    /// <summary>타이핑 루프가 선택한 새 글자에 효과음을 한 번 재생한다.</summary>
    /// <param name="body">현재 대사 TMP 본문.</param>
    /// <param name="speakerName">효과음을 선택할 화자 이름.</param>
    /// <param name="from">이전까지 보인 글자 수.</param>
    /// <param name="to">이번 프레임까지 보인 글자 수.</param>
    private void playTextBlip(TextMeshProUGUI body, string speakerName, int from, int to)
    {
        int clipIndex;
        float volumeScale;
        if (speakerName == "하루")
        {
            clipIndex = textBlipClipIndex;
            volumeScale = textBlipVolumeScale;
        }
        else if (speakerName == "아빠")
        {
            clipIndex = fatherTextBlipClipIndex;
            volumeScale = fatherTextBlipVolumeScale;
        }
        else if (speakerName == "감독관")
        {
            clipIndex = inspectorTextBlipClipIndex;
            volumeScale = inspectorTextBlipVolumeScale;
        }
        else return;

        if (effects == null || clips == null || clipIndex < 0 || clipIndex >= clips.Length) return;

        AudioClip clip = clips[clipIndex];
        if (clip == null) return;

        int end = Mathf.Min(to, body.textInfo.characterCount);
        for (int i = Mathf.Max(0, from); i < end; i++)
        {
            if (!char.IsLetterOrDigit(body.textInfo.characterInfo[i].character)) continue;
            effects.PlayOneShot(clip, volumeScale);
            return;
        }
    }

    /// <summary>화자 이름을 별도 표기하지 않고 대화창 본문 앞에 붙인다.</summary>
    private static string dialogueText(string speaker, string text)
    {
        string body = text ?? string.Empty;
        return string.IsNullOrWhiteSpace(speaker) ? body : $"{speaker} : {body}";
    }

    /// <summary>알파만 조절하여 원본 이미지의 비율과 구도를 유지한다.</summary>
    /// <param name="target">목표 알파.</param>
    /// <param name="seconds">전환 시간.</param>
    /// <param name="fadeMusic">BGM도 동시에 낮추면 true.</param>
    /// <param name="musicTarget">BGM 목표 배율.</param>
    /// <returns>페이드 대기.</returns>
    private IEnumerator fade(float target, float seconds, bool fadeMusic = false, float musicTarget = .15f)
    {
        float from = pictureGroup.alpha;
        float elapsed = 0f;
        float initialMusic = musicGain;
        while (elapsed < seconds)
        {
            elapsed += playbackDelta;
            pictureGroup.alpha = Mathf.Lerp(from, target, elapsed / seconds);
            if (fadeMusic) musicGain = Mathf.Lerp(initialMusic, musicTarget, elapsed / seconds);
            yield return null;
        }
        pictureGroup.alpha = target;
        if (fadeMusic)
        {
            musicGain = musicTarget;
            if (musicTarget <= 0f && music.isPlaying) music.Pause();
        }
    }

    /// <summary>이전 루프를 멈춘 뒤 지정 클립 한 개만 재생한다.</summary>
    /// <param name="source">독립 음원 채널.</param>
    /// <param name="index">클립 슬롯.</param>
    /// <param name="volume">재생 음량.</param>
    private void loop(AudioSource source, int index, float volume)
    {
        source.Stop();
        source.clip = clips[index];
        source.loop = true;
        source.volume = volume;
        source.Play();
    }

    /// <summary>버튼 위 포인터/선택 버튼의 키보드 입력을 제외한 새 입력만 받는다.</summary>
    /// <returns>대사를 진행할 새 입력이면 true.</returns>
    private bool advance()
    {
        if (isConfirmingSkip || Time.frameCount <= suppressAdvanceFrame) return false;
        EventSystem events = EventSystem.current;
        bool buttonSelected = events != null && events.currentSelectedGameObject != null &&
            events.currentSelectedGameObject.GetComponentInParent<Button>() != null;
#if ENABLE_INPUT_SYSTEM
        Keyboard keys = Keyboard.current;
        if (!buttonSelected && keys != null && (keys.spaceKey.wasPressedThisFrame ||
            keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)) return true;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return false;
        Vector2 position = Mouse.current.position.ReadValue();
#else
        if (!buttonSelected && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) return true;
        if (!Input.GetMouseButtonDown(0)) return false;
        Vector2 position = Input.mousePosition;
#endif
        if (events == null) return true;
        hits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = position }, hits);
        foreach (RaycastResult hit in hits)
            if (hit.gameObject.GetComponentInParent<Button>() != null) return false;
        return true;
    }

    /// <summary>확인창 중에는 실제 시간 대기도 진행시키지 않는다.</summary>
    /// <param name="seconds">대기 시간.</param>
    /// <returns>일시 정지 가능한 대기.</returns>
    private IEnumerator hold(float seconds)
    {
        while (seconds > 0f) { seconds -= playbackDelta; yield return null; }
    }

    /// <summary>잔잔한 반응부터 강한 충격까지 같은 감쇠 곡선으로 화면을 흔든다.</summary>
    private IEnumerator shake(float strength, float seconds)
    {
        float duration = Mathf.Max(.01f, seconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += playbackDelta;
            float decay = 1f - Mathf.Clamp01(elapsed / duration);
            float x = Mathf.Sin(elapsed * 113f) + Mathf.Sin(elapsed * 257f) * .45f;
            float y = Mathf.Sin(elapsed * 157f + .7f) + Mathf.Sin(elapsed * 211f) * .35f;
            shakeOffset = new Vector2(x, y) * strength * decay * .72f;
            shakeZoom = Mathf.Min(.05f, strength * .002f) * decay;
            yield return null;
        }
        shakeOffset = Vector2.zero;
        shakeZoom = 0f;
    }

    /// <summary>전체 인트로를 멈추고 확인창만 입력받는다.</summary>
    private void requestSkip()
    {
        if (!running || isConfirmingSkip) return;
        if (skipConfirmation == null) { controller.CompleteImmediately(); return; }
        isConfirmingSkip = true;
        skipConfirmation.SetActive(true);
        music.Pause(); ambience.Pause(); effects.Pause();
        if (mealSequence != null) mealSequence.SetPaused(true);
        EventSystem.current?.SetSelectedGameObject(confirmNo.gameObject);
    }

    /// <summary>확정한 스킵만 기존 단일 완료 경로로 보낸다.</summary>
    private void confirmSkip()
    {
        if (!isConfirmingSkip) return;
        controller.CompleteImmediately();
    }

    /// <summary>아니요는 현재 타이핑과 오디오 위치에서 재개한다.</summary>
    private void cancelSkip()
    {
        if (!isConfirmingSkip) return;
        isConfirmingSkip = false;
        suppressAdvanceFrame = Time.frameCount;
        skipConfirmation.SetActive(false);
        music.UnPause(); ambience.UnPause(); effects.UnPause();
        if (mealSequence != null) mealSequence.SetPaused(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }
}
