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
    [SerializeField] private MapTransitionUI mapTransitionUI;
    [SerializeField] private float cameraSettleDelay = 0.5f;

    private readonly Dictionary
        <NetworkConnectionToClient, PendingMapTransition>
        pendingTransitions = new();

    private readonly Dictionary<MapId, MonsterSpawnManager>
        monsterSpawnManagers = new();

    private MapSceneManager mapSceneManager;
    private AccountNetworkController accountNetworkController;
    private InitialPlayerEntryController initialPlayerEntryController;

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
            "[Server] 클라이언트 연결 완료 / 로그인 요청 대기",
            this
        );
    }

    public override void OnServerDisconnect(
        NetworkConnectionToClient conn)
    {
        // 플레이어 제거 전에 저장용 스냅샷 확보
        if (conn?.identity != null)
        {
            GameObject player =
                conn.identity.gameObject;

            _ = SaveInventoryAsync(
                player,
                false
            );

            _ = SaveQuickSlotsAsync(
                player,
                false
            );
        }

        initialPlayerEntryController.RemoveConnection(conn);
        accountNetworkController.RemoveConnection(conn);
        pendingTransitions.Remove(conn);

        if (conn?.identity != null)
        {
            PlayerMapController mapController =
                conn.identity.GetComponent<PlayerMapController>();

            if (mapController != null)
            {
                NotifyPlayerExitedMap(
                    mapController.CurrentMapId
                );
            }
        }

        base.OnServerDisconnect(conn);
    }

    // DB 초기화 후 서버에서 사용할 모든 맵 로드
    private IEnumerator InitializeServerMaps()
    {
        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null)
        {
            Debug.LogError(
                $"{nameof(DatabaseManager)}를 찾지 못했습니다.",
                this
            );

            loadMapsCoroutine = null;
            StopServer();
            yield break;
        }

        databaseManager.BeginInitialization();

        yield return new WaitUntil(
            () => databaseManager.IsInitializationComplete
        );

        if (!databaseManager.IsInitialized)
        {
            Debug.LogError(
                "DB 초기화에 실패하여 서버 맵 로드를 중단합니다.",
                this
            );

            loadMapsCoroutine = null;
            StopServer();
            yield break;
        }

        yield return mapSceneManager.LoadAllServerMaps();

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

    // 입력 차단 후 목적지 클라이언트 맵 로드
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

        inputReader.SetInputBlocked(true);

        yield return mapTransitionUI.FadeOut();
        yield return mapSceneManager.LoadClientMap(
            mapId
        );

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

    // 목적지 맵 로드 요청 시작
    [Server]
    public void RequestMapTransition(
        NetworkConnectionToClient conn,
        MapId targetMapId,
        string targetSpawnId)
    {
        if (conn?.identity == null)
            return;

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
                $"목적지 맵을 찾지 못했습니다: {targetMapId}",
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

    // 클라이언트 로드 완료 후 서버 플레이어를 목적지 씬으로 이동
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
                $"{transition.TargetMapId} / {message.MapId}",
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
                "플레이어의 맵 이동 또는 체력 " +
                "컴포넌트를 찾지 못했습니다.",
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

        // 맵 전환 구간에서 변경된 플레이어 데이터 저장
        await SaveInventoryAsync(
            player,
            true
        );

        await SaveQuickSlotsAsync(
            player,
            true
        );

        if (!IsConnectionActive(conn) ||
            conn.identity == null)
        {
            pendingTransitions.Remove(conn);
            return;
        }

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

    // 이전 맵을 제거하고 카메라 및 입력 상태 복구
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
            localPlayer.GetComponent<LocalPlayerCameraBinder>();

        PlayerInputReader inputReader =
            localPlayer.GetComponent<PlayerInputReader>();

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

        yield return mapSceneManager.UnloadClientMap(
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

    // 변경된 인벤토리를 DB에 저장
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
                "[Inventory] 저장할 DB가 준비되지 않았습니다.",
                this
            );

            return;
        }

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

            if (markSaved)
                inventory.MarkSaved();

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
                $"UserId: {userId}\n{exception}",
                this
            );
        }
    }

    // 변경된 퀵슬롯을 DB에 저장
    private async Task SaveQuickSlotsAsync(
        GameObject player,
        bool markSaved)
    {
        if (player == null)
            return;

        PlayerAccountData accountData =
            player.GetComponent<PlayerAccountData>();

        PlayerQuickSlotController quickSlots =
            player.GetComponent<PlayerQuickSlotController>();

        if (accountData == null ||
            quickSlots == null)
        {
            Debug.LogError(
                "[QuickSlot] 저장에 필요한 플레이어 " +
                "컴포넌트를 찾지 못했습니다.",
                player
            );

            return;
        }

        if (!quickSlots.HasUnsavedChanges)
            return;

        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized ||
            databaseManager.PlayerQuickSlotRepository == null)
        {
            Debug.LogError(
                "[QuickSlot] 저장할 DB가 준비되지 않았습니다.",
                this
            );

            return;
        }

        int userId =
            accountData.UserId;

        List<PlayerQuickSlotRecord> records =
            quickSlots.CreateSaveSnapshot();

        try
        {
            await databaseManager
                .PlayerQuickSlotRepository
                .SaveAllAsync(
                    userId,
                    records
                );

            if (markSaved)
                quickSlots.MarkSaved();

            Debug.Log(
                $"[QuickSlot] 저장 완료 / " +
                $"UserId: {userId}, " +
                $"BindingCount: {records.Count}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[QuickSlot] 저장 실패 / " +
                $"UserId: {userId}\n{exception}",
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
            yield return mapTransitionUI.FadeIn();

        inputReader?.SetInputBlocked(false);
    }

    private bool IsClientMapLoaded(
        MapId mapId,
        out string sceneName)
    {
        return mapSceneManager.TryGetSceneName(
                   mapId,
                   out sceneName) &&
               SceneManager
                   .GetSceneByName(sceneName)
                   .isLoaded;
    }

    // 맵별 MonsterSpawnManager 조회 결과 캐싱
    private bool TryGetMonsterSpawnManager(
        MapId mapId,
        out MonsterSpawnManager spawnManager)
    {
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
                   out NetworkConnectionToClient activeConnection
               ) &&
               activeConnection == conn;
    }
}