using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class PlayerReviveUI :
    MonoBehaviour,
    ILocalPlayerUI
{
    [Header("References")]
    [SerializeField]
    private Button reviveButton;

    private CanvasGroup canvasGroup;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        reviveButton.onClick.AddListener(
            RequestRevive
        );

        SetVisible(false);
    }

    public void Bind(
        LocalPlayerContext context)
    {
        if (context == null)
            return;

        Unbind();

        playerHealth =
            context.Health;

        if (playerHealth == null)
        {
            Debug.LogWarning(
                "LocalPlayerContext에 PlayerHealth가 없습니다.",
                gameObject
            );

            return;
        }

        playerHealth.Died +=
            Show;

        playerHealth.Revived +=
            Hide;

        SetVisible(
            playerHealth.IsDead
        );
    }

    public void Unbind()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -=
                Show;

            playerHealth.Revived -=
                Hide;

            playerHealth = null;
        }

        SetVisible(false);
    }

    private void Show()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.Die
        );

        reviveButton.interactable = true;
        SetVisible(true);
    }

    private void Hide()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIClose
        );

        SetVisible(false);
    }

    private void SetVisible(
        bool visible)
    {
        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }

    private void RequestRevive()
    {
        if (playerHealth == null ||
            !playerHealth.IsDead)
        {
            return;
        }

        reviveButton.interactable = false;

        playerHealth.CmdRequestRevive();
    }

    private void OnDestroy()
    {
        reviveButton.onClick.RemoveListener(
            RequestRevive
        );

        Unbind();
    }
}