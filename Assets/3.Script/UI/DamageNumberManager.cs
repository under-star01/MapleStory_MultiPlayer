using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance
    {
        get;
        private set;
    }

    [Header("Prefab")]
    [SerializeField]
    private DamageNumberView viewPrefab;

    [SerializeField]
    private Transform poolRoot;

    [Header("Digit Sprites")]
    [SerializeField]
    private Sprite[] normalDigits =
        new Sprite[10];

    [SerializeField]
    private Sprite[] criticalDigits =
        new Sprite[10];

    [Header("Display")]
    [SerializeField]
    [Min(0f)]
    private float hitDisplayInterval = 0.06f;

    [Header("Pool")]
    [SerializeField]
    [Min(1)]
    private int initialPoolSize = 20;

    private readonly Queue<DamageNumberView>
        viewPool = new();

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (poolRoot == null)
        {
            poolRoot = transform;
        }

        ValidateSettings();
        CreateInitialPool();
    }

    /// <summary>
    /// 한 번의 공격으로 계산된 모든 타격 결과를
    /// 공통 시간 간격으로 순서대로 표시합니다.
    /// </summary>
    public void ShowDamageSequence(
        Vector3 worldPosition,
        DamageHitResult[] hitResults)
    {
        if (hitResults == null ||
            hitResults.Length == 0)
        {
            return;
        }

        StartCoroutine(
            ShowSequenceCoroutine(
                worldPosition,
                hitResults
            )
        );
    }

    private IEnumerator ShowSequenceCoroutine(
        Vector3 worldPosition,
        DamageHitResult[] hitResults)
    {
        for (int i = 0;
             i < hitResults.Length;
             i++)
        {
            DamageHitResult result =
                hitResults[i];

            ShowSingleDamage(
                worldPosition,
                result
            );

            /*
             * 마지막 타격 이후에는
             * 다음 숫자를 기다릴 필요가 없습니다.
             */
            if (i < hitResults.Length - 1 &&
                hitDisplayInterval > 0f)
            {
                yield return new WaitForSeconds(
                    hitDisplayInterval
                );
            }
        }
    }

    private void ShowSingleDamage(
        Vector3 worldPosition,
        DamageHitResult result)
    {
        DamageNumberView view =
            RentView();

        view.transform.position =
            worldPosition;

        view.Show(
            result.damage,
            result.isCritical,
            normalDigits,
            criticalDigits,
            ReturnView
        );
    }

    private DamageNumberView RentView()
    {
        DamageNumberView view;

        if (viewPool.Count > 0)
        {
            view = viewPool.Dequeue();
        }
        else
        {
            view = CreateView();
        }

        view.gameObject.SetActive(true);

        return view;
    }

    private void ReturnView(
        DamageNumberView view)
    {
        if (view == null)
            return;

        view.gameObject.SetActive(false);
        view.transform.SetParent(
            poolRoot,
            false
        );

        viewPool.Enqueue(view);
    }

    private void CreateInitialPool()
    {
        if (viewPrefab == null)
            return;

        for (int i = 0;
             i < initialPoolSize;
             i++)
        {
            DamageNumberView view =
                CreateView();

            view.gameObject.SetActive(false);
            viewPool.Enqueue(view);
        }
    }

    private DamageNumberView CreateView()
    {
        DamageNumberView view =
            Instantiate(
                viewPrefab,
                poolRoot
            );

        view.name =
            $"{viewPrefab.name}_Pooled";

        return view;
    }

    private void ValidateSettings()
    {
        if (viewPrefab == null)
        {
            Debug.LogError(
                "DamageNumberView 프리팹이 " +
                "연결되지 않았습니다.",
                this
            );
        }

        ValidateDigitArray(
            normalDigits,
            "일반 데미지"
        );

        ValidateDigitArray(
            criticalDigits,
            "크리티컬 데미지"
        );
    }

    private void ValidateDigitArray(
        Sprite[] digits,
        string category)
    {
        if (digits == null ||
            digits.Length != 10)
        {
            Debug.LogError(
                $"{category} 숫자 배열에는 " +
                "0부터 9까지 총 10개가 필요합니다.",
                this
            );

            return;
        }

        for (int i = 0;
             i < digits.Length;
             i++)
        {
            if (digits[i] != null)
                continue;

            Debug.LogWarning(
                $"{category} 숫자 {i}의 " +
                "스프라이트가 비어 있습니다.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}