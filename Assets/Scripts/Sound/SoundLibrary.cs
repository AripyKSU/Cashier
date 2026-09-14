using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 시스템의 믹서와 재생 기본 설정을 보관하는 라이브러리다.
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "NaN/Audio/Sound Library")]
public sealed class SoundLibrary : ScriptableObject
{
    [SerializeField]
    private AudioMixer audioMixer;

    [SerializeField]
    [Min(3)]
    private int sfxSourceCount = 3;

    [SerializeField]
    [Range(0f, 1f)]
    private float defaultMasterVolume = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    private float defaultBgmVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float defaultSfxVolume = 1f;

    /// <summary>
    /// 사운드 매니저가 사용하는 믹서를 반환한다.
    /// </summary>
    public AudioMixer AudioMixer => audioMixer;

    /// <summary>
    /// 동시에 재생할 수 있도록 생성할 효과음 오디오 소스의 수를 반환한다.
    /// </summary>
    public int SfxSourceCount => Mathf.Max(3, sfxSourceCount);

    /// <summary>
    /// 저장된 사용자 설정이 없을 때 적용할 Master 기본 볼륨을 반환한다.
    /// </summary>
    public float DefaultMasterVolume => Mathf.Clamp01(defaultMasterVolume);

    /// <summary>
    /// 저장된 사용자 설정이 없을 때 적용할 BGM 기본 볼륨을 반환한다.
    /// </summary>
    public float DefaultBgmVolume => Mathf.Clamp01(defaultBgmVolume);

    /// <summary>
    /// 저장된 사용자 설정이 없을 때 적용할 효과음 기본 볼륨을 반환한다.
    /// </summary>
    public float DefaultSfxVolume => Mathf.Clamp01(defaultSfxVolume);
}
