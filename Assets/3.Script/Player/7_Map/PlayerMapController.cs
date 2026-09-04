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

    private MapPortal currentPortal;

    public MapId CurrentMapId => currentMapId;

    // 플레이어의 현재 맵 설정
    [Server]
    public void SetCurrentMap(
        MapId mapId)
    {
        currentMapId = mapId;
        currentPortal = null;
    }

    // 접촉 중인 포탈 등록
    [Server]
    public void EnterPortal(
        MapPortal portal)
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

    // 접촉이 끝난 포탈 정보 제거
    [Server]
    public void ExitPortal(
        MapPortal portal)
    {
        if (currentPortal != portal)
            return;

        currentPortal = null;

        Debug.Log(
            $"포탈 이탈: {currentMapId}",
            this
        );
    }

    // 현재 접촉 중인 포탈 사용 요청
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

        TargetPlayPortalSound(
            connectionToClient
        );

        mapNetworkManager.RequestMapTransition(
            connectionToClient,
            currentPortal.TargetMapId,
            currentPortal.TargetSpawnId
        );
    }

    // 사망한 플레이어를 부활 맵으로 이동
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

        currentPortal = null;

        TargetPlayPortalSound(
            connectionToClient
        );

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