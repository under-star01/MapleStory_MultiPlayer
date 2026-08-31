using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MapSceneManager))]
[RequireComponent(typeof(AccountNetworkController))]
[RequireComponent(typeof(InitialPlayerEntryController))]
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

    [Header("Map Transition")]
    [SerializeField]
    private MapTransitionUI mapTransitionUI;

    [SerializeField]
    private float cameraSettleDelay = 0.5f;

    private readonly Dictionary
        <NetworkConnectionToClient, PendingMapTransition>
        pendingTransitions = new();

    private readonly Dictionary
        <MapId, MonsterSpawnManager>
        monsterSpawnManagers = new();

    private MapSceneManager mapSceneManager;

    private AccountNetworkController
        accountNetworkController;

    private InitialPlayerEntryController
        initialPlayerEntryController;

    private Coroutine loadMapsCoroutine;

    public override void Awake()
    {
        base.Awake();

        mapSceneManager =
            GetComponent<MapSceneManager>();

        accountNetworkController =
            GetComponent<AccountNetworkController>();

        initialPlayerEntryController =
            GetComponent<InitialPlayerEntryController>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        accountNetworkController.StartServer();
        initialPlayerEntryController.StartServer();

        NetworkServer.RegisterHandler
            <TransitionMapLoadedMessage>(
                OnServerTransitionMapLoaded
            );

        loadMapsCoroutine =
            StartCoroutine(
                InitializeServerMaps()
            );
    }

    public override void OnStopServer()
    {
        accountNetworkController.StopServer();
        initialPlayerEntryController.StopServer();

        NetworkServer.UnregisterHandler
            <TransitionMapLoadedMessage>();

        if (loadMapsCoroutine != null)
        {
            StopCoroutine(loadMapsCoroutine);
            loadMapsCoroutine = null;
        }

        pendingTransitions.Clear();
        monsterSpawnManagers.Clear();

        mapSceneManager.ClearServerMapState();

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        accountNetworkController.StartClient();
        initialPlayerEntryController.StartClient();

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
        accountNetworkController.StopClient();
        initialPlayerEntryController.StopClient();

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

        Debug.Log(
            "[Server] 클라이언트 연결 완료 / " +
            "로그인 요청 대기",
            this
        );
    }

    public override void OnServerDisconnect(
        NetworkConnectionToClient conn)
    {
        /*
         * 플레이어가 제거되기 전에 UserId와
         * 인벤토리 스냅샷을 확보해 저장을 시작합니다.
         */
        if (conn?.identity != null)
        {
            _ = SaveInventoryAsync(
                conn.identity.gameObject,
                false
            );
        }

        initialPlayerEntryController
            .RemoveConnection(conn);

        accountNetworkController
            .RemoveConnection(conn);

        pendingTransitions.Remove(conn);

        if (conn?.identity != null)
        {
            PlayerMapController mapController =
                conn.identity.GetComponent
                    <PlayerMapController>();

            if (mapController != null)
            {
                NotifyPlayerExitedMap(
                    mapController.CurrentMapId
                );
            }
        }

        base.OnServerDisconnect(conn);
    }

    private IEnumerator InitializeServerMaps()
    {
        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null)
        {
            Debug.LogError(
                $"{nameof(DatabaseManager)}를 " +
                "찾지 못했습니다.",
                this
            );

            loadMapsCoroutine = null;
            StopServer();
            yield break;
        }

        databaseManager.BeginInitialization();

        yield return new WaitUntil(
            () => databaseManager
                .IsInitializationComplete
        );

        if (!databaseManager.IsInitialized)
        {
            Debug.LogError(
                "DB 초기화에 실패하여 " +
                "서버 맵 로드를 중단합니다.",
                this
            );

            loadMapsCoroutine = null;
            StopServer();
            yield break;
        }

        yield return
            mapSceneManager.LoadAllServerMaps();

        loadMapsCoroutine = null;

        Debug.Log(
            "[Server] DB 초기화 및 서버 맵 로드 완료",
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
                $"{nameof(MapTransitionUI)}가 " +
                "연결되지 않았습니다.",
                this
            );

            yield break;
        }

        /*
         * 맵 전환이 시작되면 플레이어 입력을 차단합니다.
         */
        inputReader.SetInputBlocked(true);

        yield return mapTransitionUI.FadeOut();

        yield return
            mapSceneManager.LoadClientMap(mapId);

        if (!IsClientMapLoaded(
                mapId,
                out string sceneName))
        {
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

    private async void OnServerTransitionMapLoaded(
        NetworkConnectionToClient conn,
        TransitionMapLoadedMessage message)
    {
        if (!pendingTransitions.TryGetValue(
                conn,
                out PendingMapTransition transition))
        {
            Debug.LogWarning(
                "요청되지 않은 맵 전환 응답입니다.",
                conn?.identity
            );

            return;
        }

        if (transition.TargetMapId != message.MapId)
        {
            Debug.LogWarning(
                $"맵 전환 정보가 일치하지 않습니다: " +
                $"{transition.TargetMapId} / " +
                $"{message.MapId}",
                conn?.identity
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

        GameObject player =
            conn.identity.gameObject;

        PlayerMove playerMove =
            player.GetComponent<PlayerMove>();

        PlayerMapController mapController =
            player.GetComponent<PlayerMapController>();

        PlayerHealth playerHealth =
            player.GetComponent<PlayerHealth>();

        if (playerMove == null ||
            mapController == null ||
            playerHealth == null)
        {
            Debug.LogError(
                "플레이어의 맵 이동 또는 " +
                "체력 컴포넌트를 찾지 못했습니다.",
                player
            );

            pendingTransitions.Remove(conn);
            return;
        }

        MapId previousMapId =
            mapController.CurrentMapId;

        NotifyPlayerExitedMap(
            previousMapId
        );

        /*
         * 플레이어를 목적지의 독립 PhysicsScene2D로
         * 이동하고 현재 맵 정보를 변경합니다.
         */
        SceneManager.MoveGameObjectToScene(
            player,
            targetScene
        );

        playerMove.MovePosition(
            spawnPoint.position
        );

        mapController.SetCurrentMap(
            transition.TargetMapId
        );

        NotifyPlayerEnteredMap(
            transition.TargetMapId
        );

        /*
         * 입력이 차단된 맵 전환 구간에서
         * 변경된 인벤토리를 저장합니다.
         */
        await SaveInventoryAsync(
            player,
            true
        );

        /*
         * 저장을 기다리는 동안 연결이 종료될 수 있으므로
         * 이후 네트워크 처리를 진행하기 전에 확인합니다.
         */
        if (!IsConnectionActive(conn) ||
            conn.identity == null)
        {
            pendingTransitions.Remove(conn);
            return;
        }

        /*
         * 서버 맵 이동이 정상 반영된 뒤
         * 마지막 위치를 DB에 저장합니다.
         */
        accountNetworkController.SaveLastLocation(
            conn,
            transition.TargetMapId,
            transition.TargetSpawnId
        );

        if (playerHealth.IsDead &&
            transition.TargetMapId == MapId.MainTown)
        {
            playerHealth.CompleteRevive();
        }

        /*
         * Scene Interest Management가 변경된
         * 플레이어 씬을 다시 반영합니다.
         */
        NetworkServer.RebuildObservers(
            conn.identity,
            true
        );

        conn.Send(
            new CompleteMapTransitionMessage
            {
                PreviousMapId = previousMapId,
                CurrentMapId =
                    transition.TargetMapId
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
                "맵 전환에 필요한 플레이어 " +
                "컴포넌트를 찾지 못했습니다.",
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
            yield return
                new WaitForSecondsRealtime(
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
                $"{nameof(MapTransitionUI)}가 " +
                "연결되지 않았습니다.",
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

    /// <summary>
    /// 변경된 플레이어 인벤토리를 DB에 저장합니다.
    /// </summary>
    private async Task SaveInventoryAsync(
        GameObject player,
        bool markSaved)
    {
        if (player == null)
            return;

        PlayerAccountData accountData =
            player.GetComponent<PlayerAccountData>();

        PlayerInventory inventory =
            player.GetComponent<PlayerInventory>();

        if (accountData == null ||
            inventory == null)
        {
            Debug.LogError(
                "[Inventory] 저장에 필요한 플레이어 " +
                "컴포넌트를 찾지 못했습니다.",
                player
            );

            return;
        }

        if (!inventory.HasUnsavedChanges)
            return;

        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized ||
            databaseManager.PlayerInventoryRepository == null)
        {
            Debug.LogError(
                "[Inventory] 저장할 DB가 " +
                "준비되지 않았습니다.",
                this
            );

            return;
        }

        /*
         * 비동기 대기 전에 필요한 데이터를 복사합니다.
         * 연결 종료로 플레이어가 제거되어도
         * DB 저장에 필요한 값은 유지됩니다.
         */
        int userId =
            accountData.UserId;

        List<PlayerInventoryRecord> records =
            inventory.CreateSaveSnapshot();

        try
        {
            await databaseManager
                .PlayerInventoryRepository
                .SaveAllAsync(
                    userId,
                    records
                );

            /*
             * 맵 이동 중에는 플레이어가 유지되므로
             * 저장 성공 상태를 반영합니다.
             *
             * 연결 종료 시에는 제거될 오브젝트에
             * 다시 접근하지 않습니다.
             */
            if (markSaved &&
                inventory != null)
            {
                inventory.MarkSaved();
            }

            Debug.Log(
                $"[Inventory] 저장 완료 / " +
                $"UserId: {userId}, " +
                $"ItemCount: {records.Count}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[Inventory] 저장 실패 / " +
                $"UserId: {userId}\n" +
                $"{exception}",
                this
            );
        }
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
            localPlayer.GetComponent
                <PlayerInputReader>();

        if (inputReader != null)
            return true;

        Debug.LogError(
            $"{nameof(PlayerInputReader)}를 " +
            "찾지 못했습니다.",
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

        return SceneManager
            .GetSceneByName(sceneName)
            .isLoaded;
    }

    private bool TryGetMonsterSpawnManager(
        MapId mapId,
        out MonsterSpawnManager spawnManager)
    {
        /*
         * 이미 검색한 맵이면 씬 계층을
         * 다시 탐색하지 않습니다.
         *
         * 관리자가 없는 맵도 null 상태로 캐싱됩니다.
         */
        if (monsterSpawnManagers.TryGetValue(
                mapId,
                out spawnManager))
        {
            return spawnManager != null;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                mapId,
                out Scene mapScene))
        {
            return false;
        }

        foreach (GameObject rootObject
                 in mapScene.GetRootGameObjects())
        {
            spawnManager =
                rootObject.GetComponentInChildren
                    <MonsterSpawnManager>(true);

            if (spawnManager != null)
                break;
        }

        monsterSpawnManagers.Add(
            mapId,
            spawnManager
        );

        return spawnManager != null;
    }

    [Server]
    public void NotifyPlayerEnteredMap(
        MapId mapId)
    {
        if (TryGetMonsterSpawnManager(
                mapId,
                out MonsterSpawnManager spawnManager))
        {
            spawnManager.OnPlayerEnteredMap();
        }
    }

    [Server]
    private void NotifyPlayerExitedMap(
        MapId mapId)
    {
        if (TryGetMonsterSpawnManager(
                mapId,
                out MonsterSpawnManager spawnManager))
        {
            spawnManager.OnPlayerExitedMap();
        }
    }

    private static bool IsConnectionActive(
        NetworkConnectionToClient conn)
    {
        return conn != null &&
               NetworkServer.connections.TryGetValue(
                   conn.connectionId,
                   out NetworkConnectionToClient
                       activeConnection) &&
               activeConnection == conn;
    }
}