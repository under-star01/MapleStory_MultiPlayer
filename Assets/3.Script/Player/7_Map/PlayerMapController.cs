using Mirror;
using UnityEngine;

public class PlayerMapController : NetworkBehaviour
{
    [Header("Revive")]
    [SerializeField]
    private MapId reviveMapId;

    [SerializeField]
    private string reviveSpawnId;

    [SyncVar]
    private MapId currentMapId;

    /*
     * 접촉 중인 포탈은 서버 판정에만 사용하므로
     * SyncVar로 동기화하지 않습니다.
     */
    private MapPortal currentPortal;

    public MapId CurrentMapId => currentMapId;

    /// <summary>
    /// 서버가 플레이어의 현재 맵을 설정합니다.
    /// </summary>
    [Server]
    public void SetCurrentMap(MapId mapId)
    {
        currentMapId = mapId;
        currentPortal = null;
    }

    /// <summary>
    /// 서버에서 플레이어가 포탈 영역에
    /// 진입했음을 기록합니다.
    /// </summary>
    [Server]
    public void EnterPortal(MapPortal portal)
    {
        if (portal == null)
            return;

        currentPortal = portal;

        Debug.Log(
            $"포탈 진입: " +
            $"{currentMapId} → " +
            $"{portal.TargetMapId} / " +
            $"{portal.TargetSpawnId}",
            this
        );
    }

    /// <summary>
    /// 서버에서 플레이어가 포탈 영역을
    /// 벗어났음을 처리합니다.
    /// </summary>
    [Server]
    public void ExitPortal(MapPortal portal)
    {
        /*
         * 다른 포탈에 이미 진입한 상태라면
         * 이전 포탈의 Exit가 현재 값을 지우지 않도록 합니다.
         */
        if (currentPortal != portal)
            return;

        currentPortal = null;

        Debug.Log(
            $"포탈 이탈: {currentMapId}",
            this
        );
    }

    /// <summary>
    /// 서버에서 현재 접촉 중인 포탈의
    /// 사용 가능 여부를 확인합니다.
    /// </summary>
    [Server]
    public void RequestUsePortal()
    {
        if (currentPortal == null)
        {
            Debug.Log(
                "현재 사용할 수 있는 포탈이 없습니다.",
                this
            );

            return;
        }

        /*
         * 다른 물리 씬의 포탈이 잘못 기록되는 상황을
         * 방지하는 기본 검증입니다.
         */
        if (currentPortal.gameObject.scene !=
            gameObject.scene)
        {
            Debug.LogWarning(
                "플레이어와 포탈이 서로 다른 " +
                "Scene에 존재합니다.",
                this
            );

            currentPortal = null;
            return;
        }

        Debug.Log(
            $"포탈 사용 요청 성공: " +
            $"{currentMapId} → " +
            $"{currentPortal.TargetMapId} / " +
            $"{currentPortal.TargetSpawnId}",
            this
        );

        if (NetworkManager.singleton
            is not MapNetworkManager mapNetworkManager)
        {
            Debug.LogError(
                "MapNetworkManager를 찾지 못했습니다.",
                this
            );

            return;
        }

        TargetPlayPortalSound(connectionToClient);

        mapNetworkManager.RequestMapTransition(
            connectionToClient,
            currentPortal.TargetMapId,
            currentPortal.TargetSpawnId
        );
    }

    /// <summary>
    /// 사망한 플레이어를 마을 부활 위치로 이동시킵니다.
    /// </summary>
    [Server]
    public bool RequestReviveTransition()
    {
        if (NetworkManager.singleton
            is not MapNetworkManager mapNetworkManager)
        {
            Debug.LogError(
                "MapNetworkManager를 찾지 못했습니다.",
                this
            );

            return false;
        }

        /*
         * 사망한 위치에서 접촉 중이던 포탈 정보는
         * 더 이상 사용하지 않습니다.
         */
        currentPortal = null;

        TargetPlayPortalSound(connectionToClient);

        mapNetworkManager.RequestMapTransition(
            connectionToClient,
            reviveMapId,
            reviveSpawnId
        );

        return true;
    }

    [TargetRpc]
    private void TargetPlayPortalSound(
        NetworkConnection target)
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.Portal
        );
    }
}