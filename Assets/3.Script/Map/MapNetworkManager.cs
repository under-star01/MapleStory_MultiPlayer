using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MapSceneManager))]
public class MapNetworkManager : NetworkManager
{
    private class PendingMapTransition
    {
        public MapId TargetMapId { get; }
        public string TargetSpawnId { get; }

        public PendingMapTransition(
            MapId targetMapId,
            string targetSpawnId)
        {
            TargetMapId = targetMapId;
            TargetSpawnId = targetSpawnId;
        }
    }

    private const string DefaultSpawnId = "Default";

    [Header("Initial Map")]
    [SerializeField]
    private MapId initialMapId = MapId.MainTown;

    [Header("Map Transition")]
    [SerializeField]
    private MapTransitionUI mapTransitionUI;

    [SerializeField]
    private float cameraSettleDelay = 0.5f;

    private readonly Dictionary
        <NetworkConnectionToClient, PendingMapTransition>
        pendingTransitions = new();

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

        NetworkServer.RegisterHandler
            <TransitionMapLoadedMessage>(
                OnServerTransitionMapLoaded
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

        NetworkServer.UnregisterHandler
            <TransitionMapLoadedMessage>();

        if (loadMapsCoroutine != null)
        {
            StopCoroutine(loadMapsCoroutine);
            loadMapsCoroutine = null;
        }

        pendingTransitions.Clear();
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

        NetworkClient.RegisterHandler
            <LoadTransitionMapMessage>(
                OnClientLoadTransitionMap
            );

        NetworkClient.RegisterHandler
            <CompleteMapTransitionMessage>(
                OnClientCompleteMapTransition
            );
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler
            <LoadMapMessage>();

        NetworkClient.UnregisterHandler
            <LoadTransitionMapMessage>();

        NetworkClient.UnregisterHandler
            <CompleteMapTransitionMessage>();

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

    public override void OnServerDisconnect(
        NetworkConnectionToClient conn)
    {
        pendingTransitions.Remove(conn);

        base.OnServerDisconnect(conn);
    }

    private IEnumerator LoadServerMaps()
    {
        yield return
            mapSceneManager.LoadAllServerMaps();

        loadMapsCoroutine = null;
    }

    private IEnumerator SendInitialMapWhenReady(
        NetworkConnectionToClient conn)
    {
        yield return new WaitUntil(
            () => mapSceneManager.AreServerMapsLoaded
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

        if (!IsClientMapLoaded(
                mapId,
                out string sceneName))
        {
            yield break;
        }

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

    private void OnClientLoadTransitionMap(
        LoadTransitionMapMessage message)
    {
        StartCoroutine(
            BeginClientMapTransition(
                message.MapId
            )
        );
    }

    private IEnumerator BeginClientMapTransition(
    MapId mapId)
    {
        if (!TryGetLocalInputReader(
                out PlayerInputReader inputReader))
        {
            yield break;
        }

        if (mapTransitionUI == null)
        {
            Debug.LogError(
                $"{nameof(MapTransitionUI)}가 연결되지 않았습니다.",
                this
            );

            yield break;
        }

        // 전환 시작과 동시에 모든 플레이어 입력을 차단합니다.
        inputReader.SetInputBlocked(true);

        // 화면이 완전히 검게 된 다음 맵을 로드합니다.
        yield return mapTransitionUI.FadeOut();

        yield return
            mapSceneManager.LoadClientMap(mapId);

        if (!IsClientMapLoaded(
                mapId,
                out string sceneName))
        {
            // 로드에 실패하면 기존 화면으로 돌아가고 입력을 복구합니다.
            yield return mapTransitionUI.FadeIn();

            inputReader.SetInputBlocked(false);
            yield break;
        }

        NetworkClient.Send(
            new TransitionMapLoadedMessage
            {
                MapId = mapId
            }
        );

        Debug.Log(
            $"전환용 클라이언트 맵 로드 완료: " +
            $"{mapId} / {sceneName}",
            this
        );
    }

    [Server]
    public void RequestMapTransition(
        NetworkConnectionToClient conn,
        MapId targetMapId,
        string targetSpawnId)
    {
        if (conn == null ||
            conn.identity == null)
        {
            return;
        }

        if (pendingTransitions.ContainsKey(conn))
        {
            Debug.LogWarning(
                "이미 맵 이동을 처리하고 있습니다.",
                conn.identity
            );

            return;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                targetMapId,
                out _))
        {
            Debug.LogError(
                $"목적지 맵을 찾지 못했습니다: " +
                $"{targetMapId}",
                this
            );

            return;
        }

        if (mapSceneManager.FindSpawnPoint(
                targetMapId,
                targetSpawnId) == null)
        {
            Debug.LogError(
                $"목적지 스폰 지점을 찾지 못했습니다: " +
                $"{targetMapId} / {targetSpawnId}",
                this
            );

            return;
        }

        pendingTransitions.Add(
            conn,
            new PendingMapTransition(
                targetMapId,
                targetSpawnId
            )
        );

        conn.Send(
            new LoadTransitionMapMessage
            {
                MapId = targetMapId
            }
        );

        Debug.Log(
            $"목적지 맵 로드 요청: " +
            $"{targetMapId} / {targetSpawnId}",
            conn.identity
        );
    }

    private void OnServerTransitionMapLoaded(
    NetworkConnectionToClient conn,
    TransitionMapLoadedMessage message)
    {
        if (!pendingTransitions.TryGetValue(
                conn,
                out PendingMapTransition transition))
        {
            Debug.LogWarning(
                "요청되지 않은 맵 전환 응답입니다.",
                conn.identity
            );

            return;
        }

        if (transition.TargetMapId != message.MapId)
        {
            Debug.LogWarning(
                $"맵 전환 정보가 일치하지 않습니다: " +
                $"{transition.TargetMapId} / " +
                $"{message.MapId}",
                conn.identity
            );

            pendingTransitions.Remove(conn);
            return;
        }

        if (conn.identity == null)
        {
            pendingTransitions.Remove(conn);
            return;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                transition.TargetMapId,
                out Scene targetScene))
        {
            Debug.LogError(
                $"목적지 서버 맵을 찾지 못했습니다: " +
                $"{transition.TargetMapId}",
                this
            );

            pendingTransitions.Remove(conn);
            return;
        }

        Transform spawnPoint =
            mapSceneManager.FindSpawnPoint(
                transition.TargetMapId,
                transition.TargetSpawnId
            );

        if (spawnPoint == null)
        {
            Debug.LogError(
                $"목적지 스폰 지점을 찾지 못했습니다: " +
                $"{transition.TargetMapId} / " +
                $"{transition.TargetSpawnId}",
                this
            );

            pendingTransitions.Remove(conn);
            return;
        }

        GameObject player = conn.identity.gameObject;

        PlayerMove playerMove =
            player.GetComponent<PlayerMove>();

        PlayerMapController mapController =
            player.GetComponent<PlayerMapController>();

        if (playerMove == null ||
            mapController == null)
        {
            Debug.LogError(
                "플레이어의 맵 이동 컴포넌트를 " +
                "찾지 못했습니다.",
                player
            );

            pendingTransitions.Remove(conn);
            return;
        }

        MapId previousMapId =
            mapController.CurrentMapId;

        // 목적지의 독립 PhysicsScene2D로 이동합니다.
        SceneManager.MoveGameObjectToScene(
            player,
            targetScene
        );

        playerMove.Teleport(
            spawnPoint.position
        );

        mapController.SetCurrentMap(
            transition.TargetMapId
        );

        /*
         * Scene Interest Management가 변경된
         * 플레이어의 Scene을 다시 반영하도록 합니다.
         */
        NetworkServer.RebuildObservers(
            conn.identity,
            true
        );

        conn.Send(
            new CompleteMapTransitionMessage
            {
                PreviousMapId = previousMapId,
                CurrentMapId = transition.TargetMapId
            }
        );

        Debug.Log(
            $"플레이어 맵 이동 완료: " +
            $"{transition.TargetMapId} / " +
            $"{transition.TargetSpawnId} / " +
            $"{spawnPoint.position}",
            player
        );

        pendingTransitions.Remove(conn);
    }

    private bool IsClientMapLoaded(
        MapId mapId,
        out string sceneName)
    {
        if (!mapSceneManager.TryGetSceneName(
                mapId,
                out sceneName))
        {
            return false;
        }

        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        return scene.isLoaded;
    }

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

    private void OnClientCompleteMapTransition(
    CompleteMapTransitionMessage message)
    {
        StartCoroutine(
            CompleteClientMapTransition(
                message.PreviousMapId,
                message.CurrentMapId
            )
        );
    }

    private IEnumerator CompleteClientMapTransition(
    MapId previousMapId,
    MapId currentMapId)
    {
        NetworkIdentity localPlayerIdentity =
            NetworkClient.localPlayer;

        if (localPlayerIdentity == null)
        {
            Debug.LogError(
                "로컬 플레이어를 찾지 못했습니다.",
                this
            );

            yield return RecoverClientTransition();
            yield break;
        }

        GameObject localPlayer =
            localPlayerIdentity.gameObject;

        LocalPlayerCameraBinder cameraBinder =
            localPlayer.GetComponent
                <LocalPlayerCameraBinder>();

        PlayerInputReader inputReader =
            localPlayer.GetComponent
                <PlayerInputReader>();

        if (cameraBinder == null ||
            inputReader == null)
        {
            Debug.LogError(
                "맵 전환에 필요한 플레이어 컴포넌트를 " +
                "찾지 못했습니다.",
                localPlayer
            );

            yield return RecoverClientTransition(
                inputReader
            );

            yield break;
        }

        cameraBinder.BindMapBounds(
            currentMapId
        );

        yield return
            mapSceneManager.UnloadClientMap(
                previousMapId
            );

        if (cameraSettleDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                cameraSettleDelay
            );
        }

        if (mapTransitionUI != null)
        {
            yield return mapTransitionUI.FadeIn();
        }
        else
        {
            Debug.LogError(
                $"{nameof(MapTransitionUI)}가 연결되지 않았습니다.",
                this
            );
        }

        inputReader.SetInputBlocked(false);

        Debug.Log(
            $"클라이언트 맵 전환 완료: " +
            $"{previousMapId} → {currentMapId}",
            this
        );
    }

    private bool TryGetLocalInputReader(
    out PlayerInputReader inputReader)
    {
        inputReader = null;

        NetworkIdentity localPlayer =
            NetworkClient.localPlayer;

        if (localPlayer == null)
        {
            Debug.LogError(
                "로컬 플레이어를 찾지 못했습니다.",
                this
            );

            return false;
        }

        inputReader =
            localPlayer.GetComponent<PlayerInputReader>();

        if (inputReader != null)
            return true;

        Debug.LogError(
            $"{nameof(PlayerInputReader)}를 찾지 못했습니다.",
            localPlayer
        );

        return false;
    }

    private IEnumerator RecoverClientTransition(
    PlayerInputReader inputReader = null)
    {
        if (mapTransitionUI != null)
        {
            yield return mapTransitionUI.FadeIn();
        }

        inputReader?.SetInputBlocked(false);
    }
}