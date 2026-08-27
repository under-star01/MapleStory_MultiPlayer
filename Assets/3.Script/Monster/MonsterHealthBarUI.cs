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

        /*
         * 생성 직후에는 체력바를 숨깁니다.
         * 스크립트가 붙은 몬스터 루트는 끄지 않습니다.
         */
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

    private void ShowHealthBar(
        DamageHitResult[] hitResults)
    {
        if (monsterHealth.IsDead)
            return;

        SetHealthBarVisible(true);
    }

    private void UpdateHealthBar(
        int currentHp,
        int maxHp)
    {
        float ratio = maxHp > 0
            ? (float)currentHp / maxHp
            : 0f;

        fillImage.fillAmount =
            Mathf.Clamp01(ratio);

        /*
         * HP가 0이 되면 사망 연출 중에는
         * 체력바가 보이지 않도록 숨깁니다.
         */
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

        hpGaugeRoot.SetActive(visible);
    }
}