using System.Collections.Generic;
using UnityEngine;

public class EffectPool : MonoBehaviour
{
    public static EffectPool Instance
    {
        get;
        private set;
    }

    [Header("Pool")]
    [SerializeField]
    private EffectPlayer effectPlayerPrefab;

    [SerializeField]
    [Min(1)]
    private int initialPoolSize = 10;

    [SerializeField]
    private bool allowExpansion = true;

    [Header("Effect Data")]
    [SerializeField]
    private List<EffectData> effectDataList = new();

    private readonly Dictionary<EffectId, EffectData>
        effectDataById = new();

    private readonly Queue<EffectPlayer>
        availablePlayers = new();

    /*
     * 같은 EffectPlayer가 실수로 두 번 반환되는 것을
     * 방지하기 위한 확인용 컬렉션입니다.
     */
    private readonly HashSet<EffectPlayer>
        availablePlayerSet = new();

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        /*
         * 맵 씬이 변경되어도 클라이언트의
         * 공용 이펙트 풀은 유지합니다.
         */
        DontDestroyOnLoad(gameObject);

        /*
         * Dedicated Server에서는 화면 이펙트가
         * 필요하지 않으므로 풀을 생성하지 않습니다.
         */
        if (Application.isBatchMode)
            return;

        CreateEffectDataLookup();
        CreateInitialPool();
    }

    /// <summary>
    /// 등록된 EffectData를 EffectId로
    /// 빠르게 조회할 수 있도록 Dictionary를 생성합니다.
    /// </summary>
    private void CreateEffectDataLookup()
    {
        effectDataById.Clear();

        foreach (EffectData effectData
                 in effectDataList)
        {
            if (effectData == null)
                continue;

            EffectId effectId =
                effectData.EffectId;

            if (effectId == EffectId.None)
            {
                Debug.LogWarning(
                    "EffectId가 None인 EffectData는 " +
                    "등록할 수 없습니다.",
                    effectData
                );

                continue;
            }

            if (!effectDataById.TryAdd(
                    effectId,
                    effectData))
            {
                Debug.LogWarning(
                    $"중복된 EffectId입니다: {effectId}",
                    effectData
                );
            }
        }
    }

    /// <summary>
    /// 시작 시 사용할 EffectPlayer를
    /// 미리 생성합니다.
    /// </summary>
    private void CreateInitialPool()
    {
        if (effectPlayerPrefab == null)
        {
            Debug.LogError(
                $"{nameof(EffectPlayer)} 프리팹이 " +
                "연결되지 않았습니다.",
                this
            );

            return;
        }

        for (int i = 0;
             i < initialPoolSize;
             i++)
        {
            EffectPlayer effectPlayer =
                CreateEffectPlayer();

            AddToAvailablePool(
                effectPlayer
            );
        }
    }

    /// <summary>
    /// 지정한 이펙트를 원하는 위치와 방향으로 재생합니다.
    /// </summary>
    public EffectPlayer Play(
        EffectId effectId,
        Vector3 position,
        bool flipX)
    {
        if (Application.isBatchMode)
            return null;

        if (!effectDataById.TryGetValue(
                effectId,
                out EffectData effectData))
        {
            Debug.LogWarning(
                $"등록되지 않은 EffectId입니다: " +
                $"{effectId}",
                this
            );

            return null;
        }

        EffectPlayer effectPlayer =
            GetAvailablePlayer();

        if (effectPlayer == null)
        {
            Debug.LogWarning(
                "사용 가능한 EffectPlayer가 없습니다.",
                this
            );

            return null;
        }

        effectPlayer.Play(
            effectData,
            position,
            flipX
        );

        return effectPlayer;
    }

    /// <summary>
    /// 재생 가능한 EffectPlayer를 가져옵니다.
    /// 풀이 비어 있으면 설정에 따라 추가 생성합니다.
    /// </summary>
    private EffectPlayer GetAvailablePlayer()
    {
        while (availablePlayers.Count > 0)
        {
            EffectPlayer effectPlayer =
                availablePlayers.Dequeue();

            availablePlayerSet.Remove(
                effectPlayer
            );

            if (effectPlayer != null)
            {
                return effectPlayer;
            }
        }

        if (!allowExpansion)
            return null;

        return CreateEffectPlayer();
    }

    /// <summary>
    /// 새로운 공통 EffectPlayer 인스턴스를 생성합니다.
    /// </summary>
    private EffectPlayer CreateEffectPlayer()
    {
        EffectPlayer effectPlayer =
            Instantiate(
                effectPlayerPrefab,
                transform
            );

        effectPlayer.name =
            $"{effectPlayerPrefab.name}_" +
            $"{transform.childCount - 1}";

        effectPlayer.Initialize(this);

        return effectPlayer;
    }

    /// <summary>
    /// 재생이 끝난 EffectPlayer를
    /// 다시 사용 가능한 상태로 반환합니다.
    /// </summary>
    public void Return(
        EffectPlayer effectPlayer)
    {
        if (effectPlayer == null)
            return;

        /*
         * 이미 풀에 들어 있는 EffectPlayer가
         * 중복으로 Queue에 추가되는 것을 방지합니다.
         */
        if (availablePlayerSet.Contains(
                effectPlayer))
        {
            return;
        }

        effectPlayer.Stop();

        AddToAvailablePool(
            effectPlayer
        );
    }

    private void AddToAvailablePool(
        EffectPlayer effectPlayer)
    {
        if (effectPlayer == null)
            return;

        if (!availablePlayerSet.Add(
                effectPlayer))
        {
            return;
        }

        availablePlayers.Enqueue(
            effectPlayer
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        initialPoolSize =
            Mathf.Max(
                1,
                initialPoolSize
            );

        HashSet<EffectId> registeredIds =
            new();

        foreach (EffectData effectData
                 in effectDataList)
        {
            if (effectData == null)
                continue;

            if (effectData.EffectId ==
                EffectId.None)
            {
                Debug.LogWarning(
                    "EffectId가 None인 EffectData가 있습니다.",
                    effectData
                );

                continue;
            }

            if (!registeredIds.Add(
                    effectData.EffectId))
            {
                Debug.LogWarning(
                    $"중복된 EffectId입니다: " +
                    $"{effectData.EffectId}",
                    effectData
                );
            }
        }
    }
#endif
}