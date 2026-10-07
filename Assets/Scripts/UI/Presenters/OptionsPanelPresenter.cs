using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 오른쪽 위 톱니바퀴 버튼으로 여닫는 설정 창. 전체·배경음·효과음 볼륨만 조절합니다.
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

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(value => SoundManager.Instance?.SetMasterVolume(value));
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(value => SoundManager.Instance?.SetBgmVolume(value));
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(value => SoundManager.Instance?.SetSfxVolume(value));
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        masterSlider?.onValueChanged.RemoveAllListeners();
        bgmSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.RemoveAllListeners();
    }

    /// <summary>설정 창을 닫습니다.</summary>
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>설정 창을 열거나 닫습니다. 열 때 현재 볼륨을 슬라이더에 반영합니다.</summary>
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

        panel.SetActive(open);
    }
}
