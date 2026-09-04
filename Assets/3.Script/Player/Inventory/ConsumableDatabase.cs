using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Consumable Database",
    fileName = "ConsumableDatabase")]
public class ConsumableDatabase : ScriptableObject
{
    [SerializeField]
    private List<ConsumableData> consumables = new();

    private readonly Dictionary<ConsumableId, ConsumableData>
        dataById = new();

    private bool isInitialized;

    private void OnEnable()
    {
        Initialize();
    }

    // 소비 아이템 ID에 해당하는 데이터 조회
    public bool TryGetData(
        ConsumableId consumableId,
        out ConsumableData data)
    {
        data = null;

        if (consumableId == ConsumableId.None)
            return false;

        if (!isInitialized)
        {
            Initialize();
        }

        return dataById.TryGetValue(
            consumableId,
            out data
        );
    }

    private void Initialize()
    {
        dataById.Clear();

        foreach (ConsumableData data
                 in consumables)
        {
            if (data == null ||
                data.Id == ConsumableId.None)
            {
                continue;
            }

            if (dataById.ContainsKey(data.Id))
            {
                Debug.LogWarning(
                    $"중복된 소비 아이템 ID입니다: " +
                    $"{data.Id}",
                    this
                );

                continue;
            }

            dataById.Add(
                data.Id,
                data
            );
        }

        isInitialized = true;
    }
}