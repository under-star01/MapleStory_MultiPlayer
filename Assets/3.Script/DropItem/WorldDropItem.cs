using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(NetworkIdentity))]
public class WorldDropItem : NetworkBehaviour
{
    [Header("Data")]
    [SerializeField]
    private ConsumableDatabase consumableDatabase;

    [Header("Lifetime")]
    [SerializeField]
    [Min(1f)]
    private float destroyDelay = 30f;

    [SyncVar(hook = nameof(OnConsumableIdChanged))]
    private ConsumableId consumableId =
        ConsumableId.None;

    private SpriteRenderer spriteRenderer;
    private bool isCollected;

    public ConsumableId ConsumableId =>
        consumableId;

    private void Awake()
    {
        spriteRenderer =
            GetComponent<SpriteRenderer>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        StartCoroutine(
            DestroyAfterDelay()
        );
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        RefreshVisual();
    }

    // 드롭 아이템 종류 설정
    [Server]
    public void Initialize(
        ConsumableId id)
    {
        if (id == ConsumableId.None)
            return;

        consumableId = id;
    }

    // 아이템 획득 후 월드 오브젝트 제거
    [Server]
    public bool TryCollect(
        PlayerInventory inventory)
    {
        if (isCollected ||
            inventory == null ||
            consumableId == ConsumableId.None)
        {
            return false;
        }

        if (!inventory.TryAddConsumable(
                consumableId,
                1))
        {
            return false;
        }

        isCollected = true;

        NetworkServer.Destroy(
            gameObject
        );

        return true;
    }

    private void OnConsumableIdChanged(
        ConsumableId oldId,
        ConsumableId newId)
    {
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (consumableDatabase == null ||
            !consumableDatabase.TryGetData(
                consumableId,
                out ConsumableData data))
        {
            spriteRenderer.sprite = null;
            return;
        }

        spriteRenderer.sprite =
            data.Icon;
    }

    // 일정 시간 후 드롭 아이템 제거
    [Server]
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(
            destroyDelay
        );

        NetworkServer.Destroy(
            gameObject
        );
    }
}