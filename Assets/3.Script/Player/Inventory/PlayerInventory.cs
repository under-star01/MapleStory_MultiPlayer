using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerInventory : NetworkBehaviour
{
    [Header("Item Pickup")]
    [SerializeField]
    [Min(0.1f)]
    private float pickupRange = 0.3f;

    [SerializeField]
    private LayerMask dropItemLayer;

    [Header("References")]
    [SerializeField]
    private ConsumableDatabase consumableDatabase;

    private readonly SyncDictionary<ConsumableId, int>
        consumables = new();

    private bool hasUnsavedChanges;

    public bool HasUnsavedChanges =>
        hasUnsavedChanges;

    private PlayerHealth playerHealth;

    private readonly List<Collider2D>
        pickupHits = new();

    private ContactFilter2D pickupFilter;

    public event Action<ConsumableId, int>
        ConsumableChanged;

    public event Action InventoryChanged;

    private void Awake()
    {
        playerHealth =
            GetComponent<PlayerHealth>();

        pickupFilter =
            new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = dropItemLayer,
                useTriggers = true
            };
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        consumables.OnChange +=
            OnConsumablesChanged;

        // 최초 동기화된 인벤토리 상태를 UI에 반영
        foreach (var pair in consumables)
        {
            ConsumableChanged?.Invoke(
                pair.Key,
                pair.Value
            );
        }

        InventoryChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        consumables.OnChange -=
            OnConsumablesChanged;

        base.OnStopClient();
    }

    // DB 인벤토리 데이터를 검증 후 서버 인벤토리에 적용
    [Server]
    public bool ApplyLoadedConsumables(
        IReadOnlyCollection<PlayerInventoryRecord> records)
    {
        if (records == null)
        {
            Debug.LogError(
                "적용할 인벤토리 데이터가 null입니다.",
                this
            );

            return false;
        }

        Dictionary<ConsumableId, int>
            validatedConsumables = new();

        foreach (PlayerInventoryRecord record
                 in records)
        {
            if (record == null)
            {
                Debug.LogError(
                    "DB 인벤토리에 null 데이터가 있습니다.",
                    this
                );

                return false;
            }

            if (!Enum.IsDefined(
                    typeof(ConsumableId),
                    record.ItemId))
            {
                Debug.LogError(
                    $"정의되지 않은 소비 아이템입니다: " +
                    $"ItemId={record.ItemId}",
                    this
                );

                return false;
            }

            ConsumableId consumableId =
                (ConsumableId)record.ItemId;

            if (consumableId == ConsumableId.None ||
                record.Quantity <= 0)
            {
                Debug.LogError(
                    $"유효하지 않은 인벤토리 데이터입니다: " +
                    $"ItemId={record.ItemId}, " +
                    $"Quantity={record.Quantity}",
                    this
                );

                return false;
            }

            if (!TryGetItemRecord(
                    consumableId,
                    out ItemRecord itemRecord))
            {
                Debug.LogError(
                    $"정적 아이템 데이터를 찾지 못했습니다: " +
                    $"ItemId={record.ItemId}",
                    this
                );

                return false;
            }

            if (record.Quantity >
                itemRecord.MaxStack)
            {
                Debug.LogError(
                    $"DB 아이템 수량이 최대 보유량을 " +
                    $"초과했습니다: " +
                    $"ItemId={record.ItemId}, " +
                    $"Quantity={record.Quantity}, " +
                    $"MaxStack={itemRecord.MaxStack}",
                    this
                );

                return false;
            }

            if (!validatedConsumables.TryAdd(
                    consumableId,
                    record.Quantity))
            {
                Debug.LogError(
                    $"DB 인벤토리에 중복 아이템이 있습니다: " +
                    $"ItemId={record.ItemId}",
                    this
                );

                return false;
            }
        }

        consumables.Clear();

        foreach (var pair in validatedConsumables)
        {
            consumables.Add(
                pair.Key,
                pair.Value
            );
        }

        hasUnsavedChanges = false;

        Debug.Log(
            $"[Inventory] DB 인벤토리 적용 완료 / " +
            $"ItemCount: {consumables.Count}",
            this
        );

        return true;
    }

    // 현재 인벤토리를 DB 저장용 데이터로 변환
    [Server]
    public List<PlayerInventoryRecord>
        CreateSaveSnapshot()
    {
        List<PlayerInventoryRecord> records =
            new(consumables.Count);

        foreach (var pair in consumables)
        {
            if (pair.Key == ConsumableId.None ||
                pair.Value <= 0)
            {
                continue;
            }

            records.Add(
                new PlayerInventoryRecord(
                    (int)pair.Key,
                    pair.Value
                )
            );
        }

        return records;
    }

    [Server]
    public void MarkSaved()
    {
        hasUnsavedChanges = false;
    }

    public int GetConsumableCount(
        ConsumableId consumableId)
    {
        if (consumableId ==
            ConsumableId.None)
        {
            return 0;
        }

        return consumables.TryGetValue(
            consumableId,
            out int count)
                ? count
                : 0;
    }

    public bool HasConsumable(
        ConsumableId consumableId,
        int amount = 1)
    {
        if (amount <= 0)
            return false;

        return GetConsumableCount(
            consumableId
        ) >= amount;
    }

    // 소비 아이템 추가
    [Server]
    public bool TryAddConsumable(
        ConsumableId consumableId,
        int amount)
    {
        if (consumableId ==
                ConsumableId.None ||
            amount <= 0)
        {
            return false;
        }

        if (!TryGetItemRecord(
                consumableId,
                out ItemRecord itemRecord))
        {
            return false;
        }

        int currentCount =
            GetConsumableCount(
                consumableId
            );

        int newCount =
            Mathf.Min(
                currentCount + amount,
                itemRecord.MaxStack
            );

        if (newCount == currentCount)
            return false;

        consumables[consumableId] =
            newCount;

        MarkInventoryChanged();

        return true;
    }

    // 소비 아이템 수량 감소
    [Server]
    public bool TryRemoveConsumable(
        ConsumableId consumableId,
        int amount)
    {
        if (consumableId ==
                ConsumableId.None ||
            amount <= 0)
        {
            return false;
        }

        if (!consumables.TryGetValue(
                consumableId,
                out int currentCount))
        {
            return false;
        }

        if (currentCount < amount)
            return false;

        int newCount =
            currentCount - amount;

        if (newCount <= 0)
        {
            consumables.Remove(
                consumableId
            );
        }
        else
        {
            consumables[consumableId] =
                newCount;
        }

        MarkInventoryChanged();

        return true;
    }

    // 소비 아이템 사용 및 회복 처리
    [Server]
    public bool TryUseConsumable(
        ConsumableId consumableId)
    {
        if (playerHealth.IsDead)
            return false;

        if (playerHealth.CurrentHp >=
            playerHealth.MaxHp)
        {
            return false;
        }

        if (!HasConsumable(
                consumableId))
        {
            return false;
        }

        if (!TryGetItemRecord(
                consumableId,
                out ItemRecord itemRecord))
        {
            return false;
        }

        playerHealth.Heal(
            itemRecord.HealAmount
        );

        if (!TryRemoveConsumable(
                consumableId,
                1))
        {
            return false;
        }

        TargetPlayEffectSound(
            connectionToClient,
            EffectSoundId.UseItem
        );

        return true;
    }

    public bool TryGetConsumableData(
        ConsumableId consumableId,
        out ConsumableData data)
    {
        data = null;

        if (consumableDatabase == null)
        {
            Debug.LogError(
                $"{nameof(ConsumableDatabase)}가 " +
                "연결되지 않았습니다.",
                this
            );

            return false;
        }

        return consumableDatabase.TryGetData(
            consumableId,
            out data
        );
    }

    private void OnConsumablesChanged(
        SyncDictionary<ConsumableId, int>.Operation operation,
        ConsumableId consumableId,
        int newCount)
    {
        int currentCount =
            GetConsumableCount(
                consumableId
            );

        ConsumableChanged?.Invoke(
            consumableId,
            currentCount
        );

        InventoryChanged?.Invoke();
    }

    // 주변에서 가장 가까운 드롭 아이템 획득
    [Server]
    public bool TryPickupNearestItem()
    {
        PhysicsScene2D physicsScene =
            gameObject.scene.GetPhysicsScene2D();

        if (!physicsScene.IsValid())
        {
            Debug.LogWarning(
                $"유효한 PhysicsScene2D가 아닙니다: " +
                $"{gameObject.scene.name}",
                this
            );

            return false;
        }

        pickupHits.Clear();

        physicsScene.OverlapCircle(
            transform.position,
            pickupRange,
            pickupFilter,
            pickupHits
        );

        WorldDropItem nearestDrop = null;

        float nearestSqrDistance =
            float.MaxValue;

        foreach (Collider2D hit in pickupHits)
        {
            if (hit == null)
                continue;

            WorldDropItem drop =
                hit.GetComponentInParent<WorldDropItem>();

            if (drop == null)
                continue;

            float sqrDistance =
                ((Vector2)drop.transform.position -
                 (Vector2)transform.position)
                .sqrMagnitude;

            if (sqrDistance >=
                nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance =
                sqrDistance;

            nearestDrop =
                drop;
        }

        if (nearestDrop == null)
            return false;

        if (!nearestDrop.TryCollect(this))
            return false;

        TargetPlayEffectSound(
            connectionToClient,
            EffectSoundId.PickUp
        );

        return true;
    }

    [Server]
    private bool TryGetItemRecord(
        ConsumableId consumableId,
        out ItemRecord record)
    {
        record = null;

        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized)
        {
            Debug.LogError(
                "아이템 DB 데이터가 초기화되지 않았습니다.",
                this
            );

            return false;
        }

        return databaseManager.StaticData.TryGetItem(
            (int)consumableId,
            out record
        );
    }

    [Server]
    private void MarkInventoryChanged()
    {
        hasUnsavedChanges = true;
    }

    [TargetRpc]
    private void TargetPlayEffectSound(
        NetworkConnection target,
        EffectSoundId soundId)
    {
        AudioManager.Instance?.PlayEffect(
            soundId
        );
    }
}