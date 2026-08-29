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

        if (!TryGetConsumableData(
                consumableId,
                out ConsumableData data))
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
                data.MaxStack
            );

        /*
         * 이미 최대 수량이라면
         * 아이템을 더 획득하지 못합니다.
         */
        if (newCount == currentCount)
            return false;

        consumables[consumableId] =
            newCount;

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

        /*
         * 체력이 가득 찬 상태에서는
         * 물약을 소비하지 않습니다.
         */
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

        if (!TryGetConsumableData(
                consumableId,
                out ConsumableData data))
        {
            return false;
        }

        if (data.HealAmount <= 0)
            return false;

        playerHealth.Heal(
            data.HealAmount
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
}