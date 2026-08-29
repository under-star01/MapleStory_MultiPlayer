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

    public override void OnStartClient()
    {
        base.OnStartClient();

        RefreshVisual();
    }

    /// <summary>
    /// 서버에서 드롭 아이템의 종류를 설정합니다.
    /// </summary>
    [Server]
    public void Initialize(
        ConsumableId id)
    {
        if (id == ConsumableId.None)
            return;

        consumableId = id;
    }

    /// <summary>
    /// 서버에서 드롭 아이템을 인벤토리에 추가하고
    /// 획득에 성공하면 월드 오브젝트를 제거합니다.
    /// </summary>
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
}