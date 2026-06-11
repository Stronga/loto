using UnityEngine;

public class LOTOAudioController : MonoBehaviour
{
    [Header("Audio Emitter")]
    public Transform audioEmitter;

    [Header("Sources")]
    public AudioSource generatorLoopSource;
    public AudioSource oneShotSource;

    [Header("Generator")]
    public AudioClip generatorLoopClip;
    public AudioClip generatorShutdownClip;
    public bool playGeneratorLoopOnStart = true;

    [Header("Action Clips")]
    public AudioClip switchBoxOpenClip;
    public AudioClip switchBoxCloseClip;
    public AudioClip powerHandleToggleClip;
    public AudioClip applyLockClip;
    public AudioClip applyTagClip;
    public AudioClip mainDoorOpenClip;
    public AudioClip warningClip;

    [Header("Playback")]
    [Range(0f, 1f)]
    public float defaultVolume = 1f;
    [Range(0f, 1f)]
    public float spatialBlend = 1f;
    public bool spatialize = true;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
    public float minDistance = 0.5f;
    public float maxDistance = 12f;

    private void Awake()
    {
        EnsureSources();
    }

    private void Start()
    {
        if (playGeneratorLoopOnStart)
        {
            PlayGeneratorLoop();
        }
    }

    public void PlayGeneratorLoop()
    {
        EnsureSources();

        if (generatorLoopSource == null || generatorLoopClip == null)
        {
            return;
        }

        generatorLoopSource.clip = generatorLoopClip;
        generatorLoopSource.loop = true;
        generatorLoopSource.volume = defaultVolume;
        generatorLoopSource.spatialBlend = spatialBlend;

        if (!generatorLoopSource.isPlaying)
        {
            generatorLoopSource.Play();
        }
    }

    public void StopGeneratorLoop()
    {
        if (generatorLoopSource != null && generatorLoopSource.isPlaying)
        {
            generatorLoopSource.Stop();
        }
    }

    public void PlayGeneratorShutdown()
    {
        StopGeneratorLoop();
        PlayOneShot(generatorShutdownClip);
    }

    public void PlaySwitchBoxOpen()
    {
        PlayOneShot(switchBoxOpenClip);
    }

    public void PlaySwitchBoxClose()
    {
        PlayOneShot(switchBoxCloseClip);
    }

    public void PlayPowerHandleToggle()
    {
        PlayOneShot(powerHandleToggleClip);
    }

    public void PlayApplyLock()
    {
        PlayOneShot(applyLockClip);
    }

    public void PlayApplyTag()
    {
        PlayOneShot(applyTagClip);
    }

    public void PlayMainDoorOpen()
    {
        PlayOneShot(mainDoorOpenClip);
    }

    public void PlayWarning()
    {
        PlayOneShot(warningClip);
    }

    private void PlayOneShot(AudioClip clip)
    {
        EnsureSources();

        if (oneShotSource == null || clip == null)
        {
            return;
        }

        oneShotSource.volume = defaultVolume;
        oneShotSource.spatialBlend = spatialBlend;
        oneShotSource.PlayOneShot(clip, 1f);
    }

    private void EnsureSources()
    {
        Transform sourceParent = audioEmitter != null ? audioEmitter : transform;

        if (generatorLoopSource == null)
        {
            generatorLoopSource = CreateSource(sourceParent, "LOTO_GeneratorLoop_AudioSource");
            generatorLoopSource.loop = true;
        }

        if (oneShotSource == null)
        {
            oneShotSource = CreateSource(sourceParent, "LOTO_ActionOneShot_AudioSource");
        }

        ConfigureSource(generatorLoopSource);
        ConfigureSource(oneShotSource);
    }

    private AudioSource CreateSource(Transform parent, string sourceName)
    {
        Transform existing = parent != null ? parent.Find(sourceName) : null;
        GameObject sourceObject = existing != null ? existing.gameObject : new GameObject(sourceName);

        if (parent != null)
        {
            sourceObject.transform.SetParent(parent, false);
        }

        sourceObject.transform.localPosition = Vector3.zero;
        sourceObject.transform.localRotation = Quaternion.identity;

        AudioSource source = sourceObject.GetComponent<AudioSource>();
        return source != null ? source : sourceObject.AddComponent<AudioSource>();
    }

    private void ConfigureSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.spatialize = spatialize;
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
    }
}
