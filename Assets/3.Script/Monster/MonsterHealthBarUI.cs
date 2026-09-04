using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MonsterHealth))]
public class MonsterHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameObject hpGaugeRoot;

    [SerializeField]
    private Image fillImage;

    private MonsterHealth monsterHealth;

    private void Awake()
    {
        monsterHealth =
            GetComponent<MonsterHealth>();
    }

    private void OnEnable()
    {
        monsterHealth.HealthChanged +=
            UpdateHealthBar;

        monsterHealth.DamageReceived +=
            ShowHealthBar;
    }

    private void Start()
    {
        UpdateHealthBar(
            monsterHealth.CurrentHp,
            monsterHealth.MaxHp
        );

        SetHealthBarVisible(false);
    }

    private void OnDisable()
    {
        if (monsterHealth == null)
            return;

        monsterHealth.HealthChanged -=
            UpdateHealthBar;

        monsterHealth.DamageReceived -=
            ShowHealthBar;
    }

    // 피격 시 체력바 표시
    private void ShowHealthBar(
        DamageHitResult[] hitResults)
    {
        if (monsterHealth.IsDead)
            return;

        SetHealthBarVisible(true);
    }

    // 현재 체력 비율에 맞춰 게이지 갱신
    private void UpdateHealthBar(
        int currentHp,
        int maxHp)
    {
        float ratio =
            maxHp > 0
                ? (float)currentHp / maxHp
                : 0f;

        fillImage.fillAmount =
            Mathf.Clamp01(ratio);

        if (currentHp <= 0)
        {
            SetHealthBarVisible(false);
        }
    }

    private void SetHealthBarVisible(
        bool visible)
    {
        if (hpGaugeRoot == null)
            return;

        hpGaugeRoot.SetActive(
            visible
        );
    }
}