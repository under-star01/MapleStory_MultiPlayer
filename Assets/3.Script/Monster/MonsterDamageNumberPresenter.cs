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

        /*
         * 몬스터가 이후 사망하여 제거되어도
         * Manager에는 현재 월드 좌표가 값으로 전달됩니다.
         */
        manager.ShowDamageSequence(
            damageNumberPoint.position,
            hitResults
        );
    }
}