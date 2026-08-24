using Mirror;
using UnityEngine;

public class PlayerMapController : NetworkBehaviour
{
    [SyncVar]
    private MapId currentMapId;

    public MapId CurrentMapId => currentMapId;

    /// <summary>
    /// 서버가 플레이어의 현재 맵을 설정합니다.
    /// </summary>
    [Server]
    public void SetCurrentMap(MapId mapId)
    {
        currentMapId = mapId;
    }
}