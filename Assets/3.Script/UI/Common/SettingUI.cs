using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class SettingUI : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField]
    private Slider bgmSlider;

    [SerializeField]
    private Slider effectSlider;

    [SerializeField]
    private Slider skillSlider;

    [SerializeField]
    private Slider monsterSlider;

    [Header("Window")]
    [SerializeField]
    private bool startOpened;

    private CanvasGroup canvasGroup;

    public bool IsOpened { get; private set; }

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        SetWindowVisible(
            startOpened,
            false
        );
    }

    private void Start()
    {
        InitializeSliders();
    }

    private void InitializeSliders()
    {
        if (AudioManager.Instance == null)
            return;

        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(
                AudioManager.Instance.BgmVolume
            );

            bgmSlider.onValueChanged.AddListener(
                OnBgmVolumeChanged
            );
        }

        if (effectSlider != null)
        {
            effectSlider.SetValueWithoutNotify(
                AudioManager.Instance.EffectVolume
            );

            effectSlider.onValueChanged.AddListener(
                OnEffectVolumeChanged
            );
        }

        if (skillSlider != null)
        {
            skillSlider.SetValueWithoutNotify(
                AudioManager.Instance.PlayerVolume
            );

            skillSlider.onValueChanged.AddListener(
                OnSkillVolumeChanged
            );
        }

        if (monsterSlider != null)
        {
            monsterSlider.SetValueWithoutNotify(
                AudioManager.Instance.MonsterVolume
            );

            monsterSlider.onValueChanged.AddListener(
                OnMonsterVolumeChanged
            );
        }
    }

    private void OnDestroy()
    {
        bgmSlider?.onValueChanged.RemoveListener(
            OnBgmVolumeChanged
        );

        effectSlider?.onValueChanged.RemoveListener(
            OnEffectVolumeChanged
        );

        skillSlider?.onValueChanged.RemoveListener(
            OnSkillVolumeChanged
        );

        monsterSlider?.onValueChanged.RemoveListener(
            OnMonsterVolumeChanged
        );
    }

    private void OnBgmVolumeChanged(
        float volume)
    {
        AudioManager.Instance?.SetBgmVolume(
            volume
        );
    }

    private void OnEffectVolumeChanged(
        float volume)
    {
        AudioManager.Instance?.SetEffectVolume(
            volume
        );
    }

    private void OnSkillVolumeChanged(
        float volume)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetPlayerVolume(
            volume
        );

        AudioManager.Instance.SetOtherPlayerVolume(
            volume
        );
    }

    private void OnMonsterVolumeChanged(
        float volume)
    {
        AudioManager.Instance?.SetMonsterVolume(
            volume
        );
    }

    public void Open()
    {
        SetWindowVisible(true);
    }

    public void Close()
    {
        SetWindowVisible(false);
    }

    public void Toggle()
    {
        SetWindowVisible(
            !IsOpened
        );
    }

    public void OnClickSubmit()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.MouseClick
        );

        Close();
    }

    public void OnClickGameExit()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIClose
        );

#if UNITY_EDITOR
        UnityEditor.EditorApplication
            .isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetWindowVisible(
        bool visible,
        bool playSound = true)
    {
        IsOpened = visible;

        if (playSound)
        {
            AudioManager.Instance?.PlayEffect(
                visible
                    ? EffectSoundId.Setting
                    : EffectSoundId.UIClose
            );
        }

        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }
}