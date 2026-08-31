using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MapPortal : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField]
    private MapId targetMapId;

    [SerializeField]
    private string targetSpawnId = "Spawn_FromBattleField1";

    public MapId TargetMapId => targetMapId;
    public string TargetSpawnId => targetSpawnId;

    private void Awake()
    {
        Collider2D portalCollider =
            GetComponent<Collider2D>();

        if (!portalCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{name}의 Collider2D가 Trigger로 " +
                "설정되어 있지 않습니다.",
                this
            );
        }
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        /*
         * 포탈 접촉 여부는 서버 물리 공간에서
         * 서버가 직접 판정합니다.
         */
        if (!NetworkServer.active)
            return;

        PlayerMapController mapController =
            other.GetComponentInParent
                <PlayerMapController>();

        if (mapController == null)
            return;

        mapController.EnterPortal(this);
    }

    private void OnTriggerExit2D(
        Collider2D other)
    {
        if (!NetworkServer.active)
            return;

        PlayerMapController mapController =
            other.GetComponentInParent
                <PlayerMapController>();

        if (mapController == null)
            return;

        mapController.ExitPortal(this);
    }
}