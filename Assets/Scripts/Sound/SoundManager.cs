using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// BGM과 효과음의 재생, 동시 재생, 개별 볼륨을 전역에서 관리한다.
/// </summary>
public sealed class SoundManager : Singleton<SoundManager>
{
    private const string SoundLibraryResourcePath = "SoundLibrary";

    private const string BgmMixerGroupPath = "Master/BGM";
    private const string SfxMixerGroupPath = "Master/SFX";

    private const string MasterVolumeParameterName = "MasterVolume";
    private const string BgmVolumeParameterName = "BgmVolume";
    private const string SfxVolumeParameterName = "SfxVolume";

    private const float MinimumMixerVolumeDecibels = -80f;
    private const float MinimumLinearVolume = 0.0001f;

    [SerializeField]
    private SoundLibrary soundLibrary;

    private AudioMixer audioMixer;
    private AudioMixerGroup bgmMixerGroup;
    private AudioMixerGroup sfxMixerGroup;

    private AudioSource bgmSource;
    private readonly List<AudioSource> sfxSources = new();

    private int nextSfxSourceIndex;
    private AudioClip currentBgmClip;

    private float masterVolume;
    private float bgmVolume;
    private float sfxVolume;

    private bool isInitialized;

    public float MasterVolume => masterVolume;
    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;

    public float DefaultMasterVolume =>
        soundLibrary != null
            ? Mathf.Clamp01(soundLibrary.DefaultMasterVolume)
            : 1f;

    public float DefaultBgmVolume =>
        soundLibrary != null
            ? Mathf.Clamp01(soundLibrary.DefaultBgmVolume)
            : 1f;

    public float DefaultSfxVolume =>
        soundLibrary != null
            ? Mathf.Clamp01(soundLibrary.DefaultSfxVolume)
            : 1f;

    protected override void OnSingletonAwake()
    {
        if (!TryLoadSoundLibrary())
        {
            return;
        }

        if (!TryConfigureMixer())
        {
            return;
        }

        CreateAudioSources();

        masterVolume = DefaultMasterVolume;
        bgmVolume = DefaultBgmVolume;
        sfxVolume = DefaultSfxVolume;

        SetMixerVolume(MasterVolumeParameterName, masterVolume);
        SetMixerVolume(BgmVolumeParameterName, bgmVolume);
        SetMixerVolume(SfxVolumeParameterName, sfxVolume);

        isInitialized = true;
    }

    protected override void OnSingletonDestroyed()
    {
        currentBgmClip = null;
        sfxSources.Clear();

        bgmSource = null;
        audioMixer = null;
        bgmMixerGroup = null;
        sfxMixerGroup = null;

        isInitialized = false;
    }

    /// <summary>
    /// 문자열 키에 연결된 BGM을 반복 재생한다.
    /// 이미 같은 곡이 재생 중이면 재생 위치를 유지한다.
    /// </summary>
    public void PlayBgm(string key)
    {
        if (!isInitialized)
        {
            return;
        }

        if (!TryGetClip(key, out AudioClip clip))
        {
            return;
        }

        if (currentBgmClip == clip && bgmSource.isPlaying)
        {
            return;
        }

        currentBgmClip = clip;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    /// <summary>
    /// 현재 BGM을 정지한다.
    /// </summary>
    public void StopBgm()
    {
        if (!isInitialized || bgmSource == null)
        {
            return;
        }

        bgmSource.Stop();
        bgmSource.clip = null;
        currentBgmClip = null;
    }

    /// <summary>
    /// 문자열 키에 연결된 효과음을 재생한다.
    /// 여러 효과음이 동시에 재생될 수 있다.
    /// </summary>
    public void PlaySfx(string key, float volumeScale = 1f)
    {
        if (!isInitialized || sfxSources.Count == 0)
        {
            return;
        }

        if (!TryGetClip(key, out AudioClip clip))
        {
            return;
        }

        AudioSource source = sfxSources[nextSfxSourceIndex];
        nextSfxSourceIndex = (nextSfxSourceIndex + 1) % sfxSources.Count;

        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    /// <summary>
    /// Master 볼륨을 즉시 적용한다.
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        SetMixerVolume(MasterVolumeParameterName, masterVolume);
    }

    /// <summary>
    /// BGM 볼륨을 즉시 적용한다.
    /// </summary>
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        SetMixerVolume(BgmVolumeParameterName, bgmVolume);
    }

    /// <summary>
    /// 효과음 볼륨을 즉시 적용한다.
    /// </summary>
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SetMixerVolume(SfxVolumeParameterName, sfxVolume);
    }

    private void CreateAudioSources()
    {
        bgmSource = CreateAudioSource("BGM Source", bgmMixerGroup);
        bgmSource.loop = true;

        int sourceCount = soundLibrary.SfxSourceCount;

        for (int index = 0; index < sourceCount; index++)
        {
            sfxSources.Add(
                CreateAudioSource($"SFX Source {index + 1}", sfxMixerGroup)
            );
        }
    }

    private AudioSource CreateAudioSource(string sourceName, AudioMixerGroup mixerGroup)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = 1f;
        source.outputAudioMixerGroup = mixerGroup;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.bypassReverbZones = true;

        return source;
    }

    private bool TryConfigureMixer()
    {
        audioMixer = soundLibrary.AudioMixer;

        if (audioMixer == null)
        {
            Debug.LogError("SoundLibrary has no AudioMixer assigned.", this);
            return false;
        }

        return TryFindMixerGroup(BgmMixerGroupPath, out bgmMixerGroup)
               && TryFindMixerGroup(SfxMixerGroupPath, out sfxMixerGroup);
    }

    private bool TryFindMixerGroup(string groupPath, out AudioMixerGroup mixerGroup)
    {
        AudioMixerGroup[] groups = audioMixer.FindMatchingGroups(groupPath);

        if (groups.Length == 1)
        {
            mixerGroup = groups[0];
            return true;
        }

        mixerGroup = null;

        Debug.LogError(
            $"AudioMixer group was not found or is ambiguous: {groupPath}",
            this
        );

        return false;
    }

    private void SetMixerVolume(string parameterName, float linearVolume)
    {
        if (audioMixer == null)
        {
            return;
        }

        float decibels = ConvertLinearVolumeToDecibels(linearVolume);

        if (audioMixer.SetFloat(parameterName, decibels))
        {
            return;
        }

        Debug.LogError(
            $"AudioMixer exposed parameter was not found: {parameterName}",
            this
        );
    }

    private static float ConvertLinearVolumeToDecibels(float linearVolume)
    {
        return Mathf.Max(
            MinimumMixerVolumeDecibels,
            Mathf.Log10(Mathf.Max(linearVolume, MinimumLinearVolume)) * 20f
        );
    }

    private bool TryLoadSoundLibrary()
    {
        if (soundLibrary != null)
        {
            return true;
        }

        soundLibrary = Resources.Load<SoundLibrary>(SoundLibraryResourcePath);

        if (soundLibrary != null)
        {
            return true;
        }

        Debug.LogError(
            $"SoundLibrary was not found in Resources: {SoundLibraryResourcePath}",
            this
        );

        return false;
    }

    private bool TryGetClip(string key, out AudioClip clip)
    {
        clip = null;

        if (soundLibrary == null)
        {
            Debug.LogWarning("SoundManager has no SoundLibrary assigned.", this);
            return false;
        }

        if (!soundLibrary.TryGetClip(key, out clip))
        {
            Debug.LogWarning($"Sound key was not found: {key}", this);
            return false;
        }

        return true;
    }
}
