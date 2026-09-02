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
    Die = 8,
    Portal = 9
}

public enum SkillSoundId
{
    None = -1,
    BasicAttack = 0,
    Jump = 1,
    DoubleJump = 2,
    UpJump = 3,
    SkillAttack1 = 4,
    DashSkill = 5
}

public enum MonsterSoundId
{
    None = -1,
    Hit = 0,
    Die = 1
}

public class AudioManager : MonoBehaviour
{
    private const string BgmVolumeKey =
        "BgmVolume";

    private const string EffectVolumeKey =
        "EffectVolume";

    private const string PlayerVolumeKey =
        "PlayerVolume";

    private const string OtherPlayerVolumeKey =
        "OtherPlayerVolume";

    private const string MonsterVolumeKey =
        "MonsterVolume";

    private const float DefaultVolume = 0.5f;
    private const float DefaultOtherPlayerVolume = 0.2f;

    public static AudioManager Instance
    {
        get;
        private set;
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource effectSource;
    [SerializeField] private AudioSource playerSource;
    [SerializeField] private AudioSource otherPlayerSource; 
    [SerializeField] private AudioSource monsterSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip[] bgmClips;
    [SerializeField] private AudioClip[] effectClips;
    [SerializeField] private AudioClip[] skillClips;
    [SerializeField] private AudioClip[] monsterClips;
    
    public float BgmVolume =>
        bgmSource != null
            ? bgmSource.volume
            : 0f;

    public float EffectVolume =>
        effectSource != null
            ? effectSource.volume
            : 0f;

    public float PlayerVolume =>
        playerSource != null
            ? playerSource.volume
            : 0f;

    public float OtherPlayerVolume =>
        otherPlayerSource != null
            ? otherPlayerSource.volume
            : 0f;

    public float MonsterVolume =>
        monsterSource != null
            ? monsterSource.volume
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
        PlayBgm(
            BgmSoundId.Common
        );
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

        // 같은 BGM이면 처음부터 다시 재생하지 않습니다.
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

        effectSource.PlayOneShot(
            clip
        );
    }

    /// <summary>
    /// 플레이어 행동 효과음을 재생합니다.
    /// 자신의 행동과 다른 플레이어의 행동을
    /// 서로 다른 AudioSource로 구분합니다.
    /// </summary>
    public void PlaySkill(
        SkillSoundId soundId,
        bool isLocalPlayer)
    {
        if (!TryGetClip(
                skillClips,
                (int)soundId,
                out AudioClip clip))
        {
            return;
        }

        AudioSource source =
            isLocalPlayer
                ? playerSource
                : otherPlayerSource;

        source?.PlayOneShot(
            clip
        );
    }

    public void PlayMonster(
        MonsterSoundId soundId)
    {
        if (monsterSource == null ||
            !TryGetClip(
                monsterClips,
                (int)soundId,
                out AudioClip clip))
        {
            return;
        }

        monsterSource.PlayOneShot(
            clip
        );
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

    public void SetPlayerVolume(
        float volume)
    {
        SetVolume(
            playerSource,
            PlayerVolumeKey,
            volume
        );
    }

    public void SetOtherPlayerVolume(
        float volume)
    {
        SetVolume(
            otherPlayerSource,
            OtherPlayerVolumeKey,
            volume
        );
    }

    public void SetMonsterVolume(
    float volume)
    {
        SetVolume(
            monsterSource,
            MonsterVolumeKey,
            volume
        );
    }

    private void InitializeSources()
    {
        InitializeSource(
            bgmSource,
            true
        );

        InitializeSource(
            effectSource,
            false
        );

        InitializeSource(
            playerSource,
            false
        );

        InitializeSource(
            otherPlayerSource,
            false
        );

        InitializeSource(
            monsterSource,
            false
        );
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
            playerSource,
            PlayerPrefs.GetFloat(
                PlayerVolumeKey,
                DefaultVolume
            )
        );

        SetSourceVolume(
            otherPlayerSource,
            PlayerPrefs.GetFloat(
                OtherPlayerVolumeKey,
                DefaultOtherPlayerVolume
            )
        );

        SetSourceVolume(
            monsterSource,
            PlayerPrefs.GetFloat(
                MonsterVolumeKey,
                DefaultVolume
            )
        );
    }

    private static void InitializeSource(
        AudioSource source,
        bool loop)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = loop;
    }

    private static void SetVolume(
        AudioSource source,
        string saveKey,
        float volume)
    {
        if (source == null)
            return;

        volume =
            Mathf.Clamp01(volume);

        source.volume =
            volume;

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

    // enum 값을 AudioClip 배열 인덱스로 사용합니다.
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

        clip =
            clips[index];

        return clip != null;
    }
}