using UnityEngine;

public enum BgmSoundId
{
    None = -1,
    Common = 0,
    InGame = 1
}

public enum EffectSoundId
{
    None = -1,
    MouseClick = 0,
    UIOpen = 1,
    UIClose = 2,
    DragStart = 3,
    DragEnd = 4,
    UseItem = 5,
    PickUp = 6,
    Setting = 7,
}

public enum SkillSoundId
{
    None = -1,
    BasicAttack = 0,
    Jump = 1,
    DoubleJump = 2,
    SkillAttack1 = 3,
    DashSkill = 4
}

public class AudioManager : MonoBehaviour
{
    private const string BgmVolumeKey =
        "BgmVolume";

    private const string EffectVolumeKey =
        "EffectVolume";

    private const string SkillVolumeKey =
        "SkillVolume";

    private const float DefaultVolume = 0.5f;

    public static AudioManager Instance
    {
        get;
        private set;
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource effectSource;
    [SerializeField] private AudioSource skillSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip[] bgmClips;
    [SerializeField] private AudioClip[] effectClips;
    [SerializeField] private AudioClip[] skillClips;

    public float BgmVolume =>
        bgmSource != null
            ? bgmSource.volume
            : 0f;

    public float EffectVolume =>
        effectSource != null
            ? effectSource.volume
            : 0f;

    public float SkillVolume =>
        skillSource != null
            ? skillSource.volume
            : 0f;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeSources();
        LoadVolumes();
    }

    private void Start()
    {
        PlayBgm(BgmSoundId.Common);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayBgm(
        BgmSoundId soundId)
    {
        if (bgmSource == null ||
            !TryGetClip(
                bgmClips,
                (int)soundId,
                out AudioClip clip))
        {
            return;
        }

        // 현재 재생 중인 BGM과 같으면 처음부터 다시 재생하지 않습니다.
        if (bgmSource.clip == clip &&
            bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void PlayEffect(
        EffectSoundId soundId)
    {
        if (effectSource == null ||
            !TryGetClip(
                effectClips,
                (int)soundId,
                out AudioClip clip))
        {
            return;
        }

        effectSource.PlayOneShot(clip);
    }

    public void PlaySkill(
        SkillSoundId soundId)
    {
        if (skillSource == null ||
            !TryGetClip(
                skillClips,
                (int)soundId,
                out AudioClip clip))
        {
            return;
        }

        skillSource.PlayOneShot(clip);
    }

    public void StopBgm()
    {
        bgmSource?.Stop();
    }

    public void SetBgmVolume(
        float volume)
    {
        SetVolume(
            bgmSource,
            BgmVolumeKey,
            volume
        );
    }

    public void SetEffectVolume(
        float volume)
    {
        SetVolume(
            effectSource,
            EffectVolumeKey,
            volume
        );
    }

    public void SetSkillVolume(
        float volume)
    {
        SetVolume(
            skillSource,
            SkillVolumeKey,
            volume
        );
    }

    private void InitializeSources()
    {
        if (bgmSource != null)
        {
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
        }

        if (effectSource != null)
        {
            effectSource.playOnAwake = false;
            effectSource.loop = false;
        }

        if (skillSource != null)
        {
            skillSource.playOnAwake = false;
            skillSource.loop = false;
        }
    }

    // 이전 실행에서 저장한 로컬 볼륨을 불러옵니다.
    private void LoadVolumes()
    {
        SetSourceVolume(
            bgmSource,
            PlayerPrefs.GetFloat(
                BgmVolumeKey,
                DefaultVolume
            )
        );

        SetSourceVolume(
            effectSource,
            PlayerPrefs.GetFloat(
                EffectVolumeKey,
                DefaultVolume
            )
        );

        SetSourceVolume(
            skillSource,
            PlayerPrefs.GetFloat(
                SkillVolumeKey,
                DefaultVolume
            )
        );
    }

    private static void SetVolume(
        AudioSource source,
        string saveKey,
        float volume)
    {
        if (source == null)
            return;

        volume = Mathf.Clamp01(volume);

        source.volume = volume;

        PlayerPrefs.SetFloat(
            saveKey,
            volume
        );

        PlayerPrefs.Save();
    }

    private static void SetSourceVolume(
        AudioSource source,
        float volume)
    {
        if (source == null)
            return;

        source.volume =
            Mathf.Clamp01(volume);
    }

    // enum 값을 배열 인덱스로 사용하되 잘못된 값은 재생하지 않습니다.
    private static bool TryGetClip(
        AudioClip[] clips,
        int index,
        out AudioClip clip)
    {
        clip = null;

        if (clips == null ||
            index < 0 ||
            index >= clips.Length)
        {
            return false;
        }

        clip = clips[index];

        return clip != null;
    }
}