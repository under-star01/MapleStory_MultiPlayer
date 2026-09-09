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

        DontDestroyOnLoad(gameObject);

        if (Application.isBatchMode)
            return;

        CreateEffectDataLookup();
        CreateInitialPool();
    }

    // EffectId로 빠르게 조회할 수 있는 데이터 테이블 생성
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

    // 시작 시 EffectPlayer 미리 생성
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

    // 지정한 이펙트를 원하는 위치와 방향으로 재생
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

    // 재생이 끝난 EffectPlayer를 풀에 반환
    public void Return(
        EffectPlayer effectPlayer)
    {
        if (effectPlayer == null)
            return;

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