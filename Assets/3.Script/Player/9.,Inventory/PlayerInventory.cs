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

    /*
     * 서버가 실제 수량을 관리하고,
     * 변경 결과가 클라이언트에 동기화됩니다.
     */
    private readonly SyncDictionary<ConsumableId, int>
        consumables = new();

    /*
     * 마지막 DB 저장 이후
     * 인벤토리 변경 사항이 있는지 나타냅니다.
     */
    private bool hasUnsavedChanges;

    public bool HasUnsavedChanges =>
        hasUnsavedChanges;

    private PlayerHealth playerHealth;

    private readonly List<Collider2D>
        pickupHits = new();

    private ContactFilter2D pickupFilter;

    /// <summary>
    /// 특정 소비 아이템의 수량이 변경될 때 발생합니다.
    /// ItemId와 변경된 수량을 전달합니다.
    /// </summary>
    public event Action<ConsumableId, int>
        ConsumableChanged;

    /// <summary>
    /// 인벤토리 전체 UI를 갱신할 때 사용할 수 있습니다.
    /// </summary>
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

        /*
         * 최초 동기화된 기존 아이템도
         * UI에 표시할 수 있도록 알립니다.
         */
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

    /// <summary>
    /// DB에서 조회한 인벤토리 데이터를
    /// 서버 인벤토리에 적용합니다.
    ///
    /// 플레이어를 NetworkServer에 등록하기 전에
    /// 호출해야 최초 상태가 클라이언트에 동기화됩니다.
    /// </summary>
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

        /*
         * 기존 인벤토리를 바로 지우지 않고,
         * 모든 DB 데이터를 먼저 검증합니다.
         *
         * 잘못된 행이 하나라도 있다면
         * 기존 상태를 건드리지 않습니다.
         */
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

        /*
         * DB에서 정상적으로 읽어온 직후이므로
         * 아직 저장할 변경 사항은 없습니다.
         */
        hasUnsavedChanges = false;

        Debug.Log(
            $"[Inventory] DB 인벤토리 적용 완료 / " +
            $"ItemCount: {consumables.Count}",
            this
        );

        return true;
    }

    /// <summary>
    /// 현재 서버 인벤토리를
    /// DB 저장용 데이터로 복사합니다.
    /// </summary>
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

    /// <summary>
    /// 현재 인벤토리가 DB에 정상적으로
    /// 저장되었음을 기록합니다.
    /// </summary>
    [Server]
    public void MarkSaved()
    {
        hasUnsavedChanges = false;
    }

    /// <summary>
    /// 현재 보유한 소비 아이템 수량을 반환합니다.
    /// </summary>
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

    /// <summary>
    /// 지정한 수량 이상 보유했는지 확인합니다.
    /// </summary>
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

    /// <summary>
    /// 서버에서 소비 아이템을 인벤토리에 추가합니다.
    /// 드롭 아이템 획득 시 호출합니다.
    /// </summary>
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

        /*
         * 이미 최대 수량이라면
         * 아이템을 더 획득하지 못합니다.
         */
        if (newCount == currentCount)
            return false;

        consumables[consumableId] =
            newCount;

        MarkInventoryChanged();

        return true;
    }

    /// <summary>
    /// 서버에서 소비 아이템 수량을 감소시킵니다.
    /// </summary>
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

    /// <summary>
    /// 서버에서 물약 사용 가능 여부를 검사하고
    /// 체력 회복과 수량 차감을 처리합니다.
    /// </summary>
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

        return TryRemoveConsumable(
            consumableId,
            1
        );
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
        /*
         * Remove 시 전달되는 값은 기본값일 수 있으므로
         * 실제 Dictionary의 최종 수량을 다시 조회합니다.
         */
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

    /// <summary>
    /// 로컬 플레이어가 주변 드롭 아이템의
    /// 획득을 서버에 요청합니다.
    /// </summary>
    public bool RequestPickup()
    {
        if (!isOwned)
            return false;

        CmdPickupNearestItem();
        return true;
    }

    [Command]
    private void CmdPickupNearestItem()
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

            return;
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

            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance =
                sqrDistance;

            nearestDrop =
                drop;
        }

        if (nearestDrop == null)
            return;

        nearestDrop.TryCollect(this);
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
}