using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 문자열 키와 오디오 클립의 연결 정보를 보관하는 사운드 라이브러리다.
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "NaN/Audio/Sound Library")]
public sealed class SoundLibrary : ScriptableObject
{
    [Serializable]
    private sealed class SoundEntry
    {
        [SerializeField]
        private string key;

        [SerializeField]
        private AudioClip clip;

        public string Key => key;
        public AudioClip Clip => clip;
    }

    [SerializeField]
    private AudioMixer audioMixer;

    [SerializeField]
    private List<SoundEntry> entries = new();

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

    /// <summary>
    /// 지정한 키에 연결된 오디오 클립을 찾아 반환한다.
    /// </summary>
    public bool TryGetClip(string key, out AudioClip clip)
    {
        clip = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        foreach (SoundEntry entry in entries)
        {
            if (entry == null
                || !string.Equals(entry.Key, key, StringComparison.Ordinal)
                || entry.Clip == null)
            {
                continue;
            }

            clip = entry.Clip;
            return true;
        }

        return false;
    }
}
