using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창. 게임 중에는 오른쪽 위 톱니바퀴, 타이틀에서는 메뉴의 "설정"으로 여닫습니다.
/// 전체·배경음·효과음 볼륨과 마우스 커서 모양·크기를 조절합니다.
/// 게임 진행에는 관여하지 않으며, 열려 있어도 영업 시간은 그대로 흐릅니다.
/// </summary>
public sealed class OptionsPanelPresenter : MonoBehaviour
{
    [Tooltip("설정 창을 여닫는 톱니바퀴 버튼")]
    [SerializeField] private Button toggleButton;

    [Tooltip("설정 창 패널")]
    [SerializeField] private GameObject panel;

    [Tooltip("전체 볼륨 (0~1)")]
    [SerializeField] private Slider masterSlider;

    [Tooltip("배경음 볼륨 (0~1)")]
    [SerializeField] private Slider bgmSlider;

    [Tooltip("효과음 볼륨 (0~1)")]
    [SerializeField] private Slider sfxSlider;

    [Tooltip("설정 창 닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Tooltip("커서 모양 버튼 3개: 종이, 나무, 녹슨 쇠 순서. 비워 두면 커서 설정 줄을 쓰지 않습니다")]
    [SerializeField] private Button[] cursorStyleButtons = new Button[0];

    [Tooltip("커서 크기 버튼 3개: 작게, 보통, 크게 순서")]
    [SerializeField] private Button[] cursorSizeButtons = new Button[0];

    // 선택된 커서 버튼 글자는 빛바랜 놋쇠, 나머지는 흐린 종이색으로 둔다.
    private static readonly Color SelectedLabelColor = new Color(.886f, .769f, .549f);
    private static readonly Color UnselectedLabelColor = new Color(.667f, .627f, .58f);

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(value => SoundManager.Instance?.SetMasterVolume(value));
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(value => SoundManager.Instance?.SetBgmVolume(value));
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(value => SoundManager.Instance?.SetSfxVolume(value));
        for (int index = 0; index < cursorStyleButtons.Length; index++)
        {
            var style = (GameCursor.CursorStyle)index;
            cursorStyleButtons[index]?.onClick.AddListener(() => { GameCursor.SetStyle(style); refreshCursorButtons(); });
        }
        for (int index = 0; index < cursorSizeButtons.Length; index++)
        {
            var size = (GameCursor.CursorSize)index;
            cursorSizeButtons[index]?.onClick.AddListener(() => { GameCursor.SetSize(size); refreshCursorButtons(); });
        }
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        masterSlider?.onValueChanged.RemoveAllListeners();
        bgmSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.RemoveAllListeners();
        foreach (Button button in cursorStyleButtons) button?.onClick.RemoveAllListeners();
        foreach (Button button in cursorSizeButtons) button?.onClick.RemoveAllListeners();
    }

    /// <summary>설정 창을 닫습니다.</summary>
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>현재 커서 모양·크기에 해당하는 버튼 글자만 강조합니다.</summary>
    private void refreshCursorButtons()
    {
        for (int index = 0; index < cursorStyleButtons.Length; index++)
            setLabelColor(cursorStyleButtons[index], index == (int)GameCursor.Style);
        for (int index = 0; index < cursorSizeButtons.Length; index++)
            setLabelColor(cursorSizeButtons[index], index == (int)GameCursor.Size);
    }

    /// <summary>버튼 안 글자 색으로 선택 여부를 표시합니다.</summary>
    /// <param name="button">대상 버튼. null이면 무시합니다.</param>
    /// <param name="isSelected">선택된 항목이면 true.</param>
    private static void setLabelColor(Button button, bool isSelected)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = isSelected ? SelectedLabelColor : UnselectedLabelColor;
    }

    /// <summary>설정 창을 열거나 닫습니다. 열 때 현재 볼륨과 커서 설정을 반영합니다.</summary>
    private void toggle()
    {
        if (panel == null) return;
        bool open = !panel.activeSelf;
        if (open && SoundManager.Instance != null)
        {
            masterSlider?.SetValueWithoutNotify(SoundManager.Instance.MasterVolume);
            bgmSlider?.SetValueWithoutNotify(SoundManager.Instance.BgmVolume);
            sfxSlider?.SetValueWithoutNotify(SoundManager.Instance.SfxVolume);
        }
        if (open) refreshCursorButtons();

        panel.SetActive(open);
    }
}
