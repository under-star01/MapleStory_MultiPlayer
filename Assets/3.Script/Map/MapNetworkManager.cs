using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 서버가 특정 클라이언트에게
/// 맵 씬 로드를 요청할 때 전송합니다.
/// </summary>
public struct LoadMapMessage : NetworkMessage
{
    public MapId MapId;
}

/// <summary>
/// 클라이언트가 맵 씬 로드를 완료한 뒤
/// 서버에 완료 사실을 전달합니다.
/// </summary>
public struct MapLoadedMessage : NetworkMessage
{
    public MapId MapId;
}

[RequireComponent(typeof(MapSceneManager))]
public class MapNetworkManager : NetworkManager
{
    private const string DefaultSpawnId =
        "Default";

    [Header("Initial Map")]
    [SerializeField]
    private MapId initialMapId =
        MapId.MainTown;

    private MapSceneManager mapSceneManager;
    private Coroutine loadMapsCoroutine;

    public override void Awake()
    {
        base.Awake();

        mapSceneManager =
            GetComponent<MapSceneManager>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler
            <MapLoadedMessage>(
                OnServerMapLoaded
            );

        loadMapsCoroutine =
            StartCoroutine(
                LoadServerMaps()
            );
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler
            <MapLoadedMessage>();

        if (loadMapsCoroutine != null)
        {
            StopCoroutine(loadMapsCoroutine);
            loadMapsCoroutine = null;
        }

        mapSceneManager.ClearServerMapState();

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        NetworkClient.RegisterHandler
            <LoadMapMessage>(
                OnClientLoadMap
            );
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler
            <LoadMapMessage>();

        base.OnStopClient();
    }

    public override void OnServerConnect(
        NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);

        StartCoroutine(
            SendInitialMapWhenReady(conn)
        );
    }

    private IEnumerator LoadServerMaps()
    {
        yield return
            mapSceneManager.LoadAllServerMaps();

        loadMapsCoroutine = null;
    }

    /// <summary>
    /// 서버의 맵 로드가 끝나면 클라이언트에게
    /// 초기 맵 로드를 요청합니다.
    /// </summary>
    private IEnumerator SendInitialMapWhenReady(
        NetworkConnectionToClient conn)
    {
        yield return new WaitUntil(
            () =>
                mapSceneManager
                    .AreServerMapsLoaded
        );

        if (conn == null)
            yield break;

        conn.Send(
            new LoadMapMessage
            {
                MapId = initialMapId
            }
        );
    }

    /// <summary>
    /// 클라이언트가 서버의 맵 로드 요청을 받습니다.
    /// </summary>
    private void OnClientLoadMap(
        LoadMapMessage message)
    {
        StartCoroutine(
            LoadClientMapAndNotifyServer(
                message.MapId
            )
        );
    }

    private IEnumerator LoadClientMapAndNotifyServer(
        MapId mapId)
    {
        yield return
            mapSceneManager.LoadClientMap(mapId);

        if (!mapSceneManager.TryGetSceneName(
                mapId,
                out string sceneName))
        {
            yield break;
        }

        Scene loadedScene =
            SceneManager.GetSceneByName(
                sceneName
            );

        if (!loadedScene.isLoaded)
            yield break;

        NetworkClient.Send(
            new MapLoadedMessage
            {
                MapId = mapId
            }
        );

        Debug.Log(
            $"클라이언트 맵 씬 로드 완료: " +
            $"{mapId} / {sceneName}",
            this
        );
    }

    /// <summary>
    /// 클라이언트의 맵 로드가 끝난 뒤
    /// 플레이어를 생성합니다.
    /// </summary>
    private void OnServerMapLoaded(
        NetworkConnectionToClient conn,
        MapLoadedMessage message)
    {
        if (conn.identity != null)
        {
            Debug.LogWarning(
                "해당 연결에는 이미 플레이어가 존재합니다.",
                conn.identity
            );

            return;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                message.MapId,
                out Scene targetScene))
        {
            Debug.LogError(
                $"서버 맵 씬을 찾지 못했습니다: " +
                $"{message.MapId}",
                this
            );

            return;
        }

        Transform spawnPoint =
            mapSceneManager.FindSpawnPoint(
                message.MapId,
                DefaultSpawnId
            );

        if (spawnPoint == null)
        {
            Debug.LogError(
                $"스폰 지점을 찾지 못했습니다: " +
                $"{message.MapId} / " +
                $"{DefaultSpawnId}",
                this
            );

            return;
        }

        GameObject player =
            Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

        PlayerMapController mapController =
            player.GetComponent<PlayerMapController>();

        if (mapController == null)
        {
            Debug.LogError(
                $"{nameof(PlayerMapController)}가 " +
                "Player Prefab에 없습니다.",
                player
            );

            Destroy(player);
            return;
        }

        mapController.SetCurrentMap(
            message.MapId
        );

        SceneManager.MoveGameObjectToScene(
            player,
            targetScene
        );

        NetworkServer.AddPlayerForConnection(
            conn,
            player
        );

        Debug.Log(
            $"플레이어 생성 완료: " +
            $"{message.MapId} / " +
            $"{spawnPoint.position}",
            player
        );
    }
}