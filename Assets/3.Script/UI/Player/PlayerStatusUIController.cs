using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUIController :
    MonoBehaviour,
    ILocalPlayerUI
{
    [SerializeField]
    private bool startOpened;
    public bool IsOpened { get; private set; }

    [Header("Health")]
    [SerializeField]
    private Image hpFillImage;

    private PlayerHealth playerHealth;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        SetWindowVisible(startOpened);
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
                "LocalPlayerContext에 PlayerHealth가 없습니다."
            );

            return;
        }

        playerHealth.HealthChanged +=
            UpdateHealth;

        UpdateHealth(
            playerHealth.CurrentHp,
            playerHealth.MaxHp
        );

        SetWindowVisible(true);
    }

    public void Unbind()
    {
        if (playerHealth == null)
            return;

        SetWindowVisible(false);

        playerHealth.HealthChanged -=
            UpdateHealth;

        playerHealth = null;
    }

    private void UpdateHealth(
        int currentHp,
        int maxHp)
    {
        if (hpFillImage == null)
            return;

        float ratio = maxHp > 0
            ? (float)currentHp / maxHp
            : 0f;

        hpFillImage.fillAmount =
            Mathf.Clamp01(ratio);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void SetWindowVisible(
    bool visible)
    {
        IsOpened = visible;

        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }
}