using UnityEngine;

[RequireComponent(typeof(MonsterHealth))]
public class MonsterDamageNumberPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform damageNumberPoint;

    private MonsterHealth monsterHealth;

    private void Awake()
    {
        monsterHealth =
            GetComponent<MonsterHealth>();

        if (damageNumberPoint == null)
        {
            Debug.LogError(
                "DamageNumberPoint가 연결되지 않았습니다.",
                this
            );
        }
    }

    private void OnEnable()
    {
        if (monsterHealth == null)
            return;

        monsterHealth.DamageReceived +=
            ShowDamageNumbers;
    }

    private void OnDisable()
    {
        if (monsterHealth == null)
            return;

        monsterHealth.DamageReceived -=
            ShowDamageNumbers;
    }

    // 받은 피해 결과를 데미지 숫자로 표시
    private void ShowDamageNumbers(
        DamageHitResult[] hitResults)
    {
        if (damageNumberPoint == null)
            return;

        DamageNumberManager manager =
            DamageNumberManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning(
                "DamageNumberManager를 찾을 수 없습니다.",
                this
            );

            return;
        }

        manager.ShowDamageSequence(
            damageNumberPoint.position,
            hitResults
        );
    }
}