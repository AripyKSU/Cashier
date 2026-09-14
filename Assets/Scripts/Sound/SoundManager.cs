using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// ResourceData 식별자로 로드한 BGM과 효과음의 재생, 동시 재생, 개별 볼륨을 전역에서 관리한다.
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

    private static readonly IReadOnlyDictionary<uint, AudioClip> EmptyClipCache =
        new ReadOnlyDictionary<uint, AudioClip>(new Dictionary<uint, AudioClip>());

    [SerializeField]
    private SoundLibrary soundLibrary;

    private AudioMixer audioMixer;
    private AudioMixerGroup bgmMixerGroup;
    private AudioMixerGroup sfxMixerGroup;

    private AudioSource bgmSource;
    private readonly List<AudioSource> sfxSources = new();
    private readonly Dictionary<uint, AudioSource> loopSfxSources = new();
    private readonly Dictionary<uint, AudioSource> timedSfxSources = new();
    private readonly Dictionary<uint, int> timedSfxGenerations = new();

    private IReadOnlyDictionary<uint, AudioClip> clipCache = EmptyClipCache;
    private Task initializationTask;
    private int nextSfxSourceIndex;
    private AudioClip currentBgmClip;

    private float masterVolume;
    private float bgmVolume;
    private float sfxVolume;

    private readonly HashSet<uint> warnedResourceIds = new();
    private bool hasWarnedBeforeInitialization;
    private bool isInitialized;

    /// <summary>현재 master 볼륨을 반환한다.</summary>
    public float MasterVolume => masterVolume;

    /// <summary>현재 BGM 볼륨을 반환한다.</summary>
    public float BgmVolume => bgmVolume;

    /// <summary>현재 효과음 볼륨을 반환한다.</summary>
    public float SfxVolume => sfxVolume;

    /// <summary>필수 사운드 클립 전체 로드가 완료되었는지 반환한다.</summary>
    public bool IsInitialized => isInitialized;

    /// <summary>현재 공개된 사운드 클립 캐시를 읽기 전용으로 반환한다.</summary>
    public IReadOnlyDictionary<uint, AudioClip> CachedClips => clipCache;

    /// <summary>SoundLibrary에 저장된 master 기본 볼륨을 반환한다.</summary>
    public float DefaultMasterVolume =>
        soundLibrary != null
            ? Mathf.Clamp01(soundLibrary.DefaultMasterVolume)
            : 1f;

    /// <summary>SoundLibrary에 저장된 BGM 기본 볼륨을 반환한다.</summary>
    public float DefaultBgmVolume =>
        soundLibrary != null
            ? Mathf.Clamp01(soundLibrary.DefaultBgmVolume)
            : 1f;

    /// <summary>SoundLibrary에 저장된 효과음 기본 볼륨을 반환한다.</summary>
    public float DefaultSfxVolume =>
        soundLibrary != null
            ? Mathf.Clamp01(soundLibrary.DefaultSfxVolume)
            : 1f;

    /// <summary>
    /// 설정 asset과 mixer source만 준비한다. 오디오 클립은 InitializeAsync에서 로드한다.
    /// </summary>
    protected override void OnSingletonAwake()
    {
        if (!tryLoadSoundLibrary())
        {
            return;
        }

        if (!tryConfigureMixer())
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
    }

    /// <summary>
    /// 사운드 초기화 작업과 참조를 정리한다. 공유 AudioClip의 소유권은 ResourceManager에 남긴다.
    /// </summary>
    protected override void OnSingletonDestroyed()
    {
        currentBgmClip = null;
        clipCache = EmptyClipCache;
        initializationTask = null;
        sfxSources.Clear();
        loopSfxSources.Clear();
        timedSfxSources.Clear();
        timedSfxGenerations.Clear();
        warnedResourceIds.Clear();
        hasWarnedBeforeInitialization = false;

        bgmSource = null;
        audioMixer = null;
        bgmMixerGroup = null;
        sfxMixerGroup = null;

        isInitialized = false;
    }

    /// <summary>
    /// ResourceData에 등록된 모든 사운드 AudioClip을 로드하고 성공한 전체 캐시를 공개한다.
    /// </summary>
    /// <param name="dataTables">ResourceDataTable을 소유한 데이터 매니저입니다.</param>
    /// <param name="cancellationToken">호출자의 대기만 취소하는 토큰입니다.</param>
    /// <returns>19개 필수 사운드 클립의 초기화 완료를 나타내는 작업입니다.</returns>
    /// <exception cref="ArgumentNullException">dataTables가 null인 경우 발생합니다.</exception>
    /// <exception cref="InvalidOperationException">필수 manager, 데이터 테이블 또는 사운드 설정이 없는 경우 발생합니다.</exception>
    /// <exception cref="InvalidDataException">ResourceData 매핑이 누락되었거나 중복된 경우 발생합니다.</exception>
    /// <exception cref="OperationCanceledException">호출자 또는 SoundManager 수명이 취소된 경우 발생합니다.</exception>
    public async UniTask InitializeAsync(
        DataTableManager dataTables,
        CancellationToken cancellationToken = default)
    {
        if (dataTables == null)
        {
            throw new ArgumentNullException(nameof(dataTables));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (isInitialized)
        {
            return;
        }

        if (initializationTask == null
            || initializationTask.IsFaulted
            || initializationTask.IsCanceled)
        {
            initializationTask = initializeAsync(dataTables).AsTask();
        }

        await initializationTask.AsUniTask().AttachExternalCancellation(cancellationToken);
    }

    /// <summary>
    /// ResourceData 식별자에 연결된 BGM을 반복 재생한다.
    /// 이미 같은 AudioClip이 재생 중이면 재생 위치를 유지한다.
    /// </summary>
    /// <param name="resourceIdx">재생할 ResourceData 식별자입니다.</param>
    public void PlayBgm(uint resourceIdx)
    {
        if (!tryGetClip(resourceIdx, out AudioClip clip) || bgmSource == null)
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
    /// ResourceData 식별자에 연결된 효과음을 일회성으로 재생한다.
    /// </summary>
    /// <param name="resourceIdx">재생할 ResourceData 식별자입니다.</param>
    /// <param name="volumeScale">해당 효과음에 적용할 0~1 볼륨 배율입니다.</param>
    public void PlaySfx(uint resourceIdx, float volumeScale = 1f)
    {
        if (!tryGetClip(resourceIdx, out AudioClip clip))
        {
            return;
        }

        if (sfxSources.Count == 0)
        {
            Debug.LogWarning("SoundManager has no SFX AudioSource.", this);
            return;
        }

        AudioSource source = sfxSources[nextSfxSourceIndex];
        nextSfxSourceIndex = (nextSfxSourceIndex + 1) % sfxSources.Count;
        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    /// <summary>
    /// ResourceData 식별자에 연결된 효과음을 지정한 시간만큼만 재생한다.
    /// 일반 일회성 효과음과 재생 수명을 분리해, 긴 원본 클립도 짧은 UI 음성으로 사용할 수 있다.
    /// </summary>
    /// <param name="resourceIdx">재생할 ResourceData 식별자입니다.</param>
    /// <param name="durationSeconds">재생을 유지할 0보다 큰 실제 시간(초)입니다.</param>
    /// <param name="volumeScale">해당 효과음에 적용할 0~1 볼륨 배율입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">durationSeconds가 유한하지 않거나 0 이하인 경우 발생합니다.</exception>
    public void PlaySfxForDuration(uint resourceIdx, float durationSeconds, float volumeScale = 1f)
    {
        if (float.IsNaN(durationSeconds)
            || float.IsInfinity(durationSeconds)
            || durationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "효과음 재생 시간은 유한한 0보다 큰 값이어야 합니다.");
        }

        if (!tryGetClip(resourceIdx, out AudioClip clip))
        {
            return;
        }

        if (!timedSfxSources.TryGetValue(resourceIdx, out AudioSource source)
            || source == null)
        {
            source = CreateAudioSource($"Timed SFX Source {resourceIdx}", sfxMixerGroup);
            timedSfxSources[resourceIdx] = source;
        }

        int generation = timedSfxGenerations.TryGetValue(resourceIdx, out int previousGeneration)
            ? checked(previousGeneration + 1)
            : 1;
        timedSfxGenerations[resourceIdx] = generation;

        source.Stop();
        source.clip = clip;
        source.loop = false;
        source.volume = Mathf.Clamp01(volumeScale);
        source.Play();
        StartCoroutine(stopTimedSfxAfterDuration(
            resourceIdx,
            source,
            clip,
            generation,
            durationSeconds));
    }

    /// <summary>지정한 짧은 재생 효과음을 즉시 정지한다.</summary>
    /// <param name="resourceIdx">정지할 ResourceData 식별자입니다.</param>
    public void StopSfxForDuration(uint resourceIdx)
    {
        if (!timedSfxSources.TryGetValue(resourceIdx, out AudioSource source)
            || source == null)
        {
            return;
        }

        int generation = timedSfxGenerations.TryGetValue(resourceIdx, out int previousGeneration)
            ? checked(previousGeneration + 1)
            : 1;
        timedSfxGenerations[resourceIdx] = generation;
        source.Stop();
        source.clip = null;
    }

    /// <summary>
    /// ResourceData 식별자에 연결된 효과음을 반복 재생한다.
    /// 같은 식별자가 이미 재생 중이면 재생 위치를 유지하고 볼륨만 갱신한다.
    /// </summary>
    /// <param name="resourceIdx">재생할 ResourceData 식별자입니다.</param>
    /// <param name="volumeScale">해당 효과음에 적용할 0~1 볼륨 배율입니다.</param>
    public void PlayLoopSfx(uint resourceIdx, float volumeScale = 1f)
    {
        if (!tryGetClip(resourceIdx, out AudioClip clip))
        {
            return;
        }

        if (!loopSfxSources.TryGetValue(resourceIdx, out AudioSource source)
            || source == null)
        {
            source = CreateAudioSource($"Loop SFX Source {resourceIdx}", sfxMixerGroup);
            source.loop = true;
            loopSfxSources[resourceIdx] = source;
        }

        source.volume = Mathf.Clamp01(volumeScale);
        if (source.clip == clip && source.isPlaying)
        {
            return;
        }

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.Play();
    }

    /// <summary>
    /// 지정한 ResourceData 식별자의 반복 효과음을 정지한다.
    /// </summary>
    /// <param name="resourceIdx">정지할 ResourceData 식별자입니다.</param>
    public void StopLoopSfx(uint resourceIdx)
    {
        if (!isInitialized)
        {
            warnBeforeInitialization();
            return;
        }

        if (!clipCache.ContainsKey(resourceIdx))
        {
            warnMissingResource(resourceIdx);
            return;
        }

        if (!loopSfxSources.TryGetValue(resourceIdx, out AudioSource source)
            || source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
    }

    /// <summary>
    /// Master 볼륨을 즉시 적용한다.
    /// </summary>
    /// <param name="volume">0~1 범위의 선형 볼륨입니다.</param>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        SetMixerVolume(MasterVolumeParameterName, masterVolume);
    }

    /// <summary>
    /// BGM 볼륨을 즉시 적용한다.
    /// </summary>
    /// <param name="volume">0~1 범위의 선형 볼륨입니다.</param>
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        SetMixerVolume(BgmVolumeParameterName, bgmVolume);
    }

    /// <summary>
    /// 효과음 볼륨을 즉시 적용한다.
    /// </summary>
    /// <param name="volume">0~1 범위의 선형 볼륨입니다.</param>
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SetMixerVolume(SfxVolumeParameterName, sfxVolume);
    }

    private async UniTask initializeAsync(DataTableManager dataTables)
    {
        var managerToken = this.GetCancellationTokenOnDestroy();
        var loadedClips = new Dictionary<uint, AudioClip>();
        var loadedAddresses = new HashSet<string>(StringComparer.Ordinal);
        var loadedIds = new HashSet<uint>();

        try
        {
            if (ResourceManager.Instance == null)
            {
                throw new InvalidOperationException("ResourceManager is not available.");
            }

            if (soundLibrary == null || audioMixer == null || bgmSource == null)
            {
                throw new InvalidOperationException("SoundManager audio settings are not ready.");
            }

            ResourceDataTable resourceTable =
                dataTables.GetDB<ResourceDataTable>(DataTableType.Resource);
            if (resourceTable == null)
            {
                throw new InvalidOperationException("ResourceDataTable is not available.");
            }

            if (SoundKeys.All == null || SoundKeys.All.Count != 19)
            {
                throw new InvalidDataException("SoundKeys.All must contain exactly 19 resource IDs.");
            }

            foreach (uint resourceIdx in SoundKeys.All)
            {
                managerToken.ThrowIfCancellationRequested();

                if (!loadedIds.Add(resourceIdx))
                {
                    throw new InvalidDataException(
                        $"Duplicate sound ResourceData ID: {resourceIdx}");
                }

                if (!resourceTable.TryGetResource(resourceIdx, out ResourceData resource)
                    || resource == null)
                {
                    throw new InvalidDataException(
                        $"Sound ResourceData row is missing: {resourceIdx}");
                }

                if (string.IsNullOrWhiteSpace(resource.Path))
                {
                    throw new InvalidDataException(
                        $"Sound ResourceData path is empty: {resourceIdx}");
                }

                if (!loadedAddresses.Add(resource.Path))
                {
                    throw new InvalidDataException(
                        $"Duplicate sound Addressables address: {resource.Path}");
                }

                AudioClip clip = await ResourceManager.Instance
                    .LoadAssetAsync<AudioClip>(resource.Path, managerToken);
                if (clip == null)
                {
                    throw new InvalidDataException(
                        $"Sound AudioClip load returned null: {resourceIdx} ({resource.Path})");
                }

                loadedClips.Add(resourceIdx, clip);
            }

            managerToken.ThrowIfCancellationRequested();
            clipCache = new ReadOnlyDictionary<uint, AudioClip>(loadedClips);
            isInitialized = true;
        }
        catch
        {
            clipCache = EmptyClipCache;
            isInitialized = false;
            throw;
        }
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

    private IEnumerator stopTimedSfxAfterDuration(
        uint resourceIdx,
        AudioSource source,
        AudioClip clip,
        int generation,
        float durationSeconds)
    {
        yield return new WaitForSecondsRealtime(durationSeconds);

        if (!timedSfxGenerations.TryGetValue(resourceIdx, out int currentGeneration)
            || currentGeneration != generation
            || source == null
            || source.clip != clip)
        {
            yield break;
        }

        source.Stop();
        source.clip = null;
    }

    private bool tryConfigureMixer()
    {
        audioMixer = soundLibrary.AudioMixer;

        if (audioMixer == null)
        {
            Debug.LogError("SoundLibrary has no AudioMixer assigned.", this);
            return false;
        }

        return tryFindMixerGroup(BgmMixerGroupPath, out bgmMixerGroup)
               && tryFindMixerGroup(SfxMixerGroupPath, out sfxMixerGroup);
    }

    private bool tryFindMixerGroup(string groupPath, out AudioMixerGroup mixerGroup)
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

    private bool tryLoadSoundLibrary()
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

    private bool tryGetClip(uint resourceIdx, out AudioClip clip)
    {
        clip = null;

        if (!isInitialized)
        {
            warnBeforeInitialization();
            return false;
        }

        if (clipCache.TryGetValue(resourceIdx, out clip) && clip != null)
        {
            return true;
        }

        warnMissingResource(resourceIdx);
        clip = null;
        return false;
    }

    private void warnBeforeInitialization()
    {
        if (hasWarnedBeforeInitialization)
        {
            return;
        }

        hasWarnedBeforeInitialization = true;
        Debug.LogWarning("SoundManager is not initialized.", this);
    }

    private void warnMissingResource(uint resourceIdx)
    {
        if (!warnedResourceIds.Add(resourceIdx))
        {
            return;
        }

        Debug.LogWarning(
            $"Sound ResourceData ID is not registered: {resourceIdx}",
            this
        );
    }
}
