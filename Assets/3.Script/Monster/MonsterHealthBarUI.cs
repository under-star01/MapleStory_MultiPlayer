using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MonsterHealth))]
public class MonsterHealthBarUI : MonoBehaviour
{
    [Header("References")]
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
    }

    private void Start()
    {
        UpdateHealthBar(
            monsterHealth.CurrentHp,
            monsterHealth.MaxHp
        );
    }

    private void OnDisable()
    {
        monsterHealth.HealthChanged -=
            UpdateHealthBar;
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
    }
}