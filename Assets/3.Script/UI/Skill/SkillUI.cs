using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class SkillUI : MonoBehaviour
{
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

    private void SetWindowVisible(
        bool visible,
        bool playSound = true)
    {
        IsOpened = visible;

        if (playSound)
        {
            AudioManager.Instance?.PlayEffect(
                visible
                    ? EffectSoundId.UIOpen
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