using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MapNetworkManager))]
[RequireComponent(typeof(MapSceneManager))]
public class InitialPlayerEntryController : MonoBehaviour
{
    private class PendingPlayerEntry
    {
        public UserRecord User { get; }
        public MapId MapId { get; }
        public string SpawnId { get; }

        public PendingPlayerEntry(
            UserRecord user,
            MapId mapId,
            string spawnId)
        {
            User = user;
            MapId = mapId;
            SpawnId = spawnId;
        }
    }

    private const string DefaultSpawnId =
        "Spawn_FromBattleField1";

    [Header("Default Entry Location")]
    [SerializeField]
    private MapId defaultMapId =
        MapId.MainTown;

    /*
     * 서버 맵 준비를 기다리는 연결입니다.
     */
    private readonly HashSet<NetworkConnectionToClient>
        preparingConnections = new();

    /*
     * 최초 입장 위치가 결정된 뒤
     * 클라이언트의 맵 로드 완료를 기다리는 연결입니다.
     */
    private readonly Dictionary
        <NetworkConnectionToClient, PendingPlayerEntry>
        pendingEntries = new();

    private MapNetworkManager mapNetworkManager;
    private MapSceneManager mapSceneManager;

    private void Awake()
    {
        mapNetworkManager =
            GetComponent<MapNetworkManager>();

        mapSceneManager =
            GetComponent<MapSceneManager>();
    }

    /*
     * MapNetworkManager.OnStartServer()에서 호출합니다.
     */
    public void StartServer()
    {
        NetworkServer.RegisterHandler<MapLoadedMessage>(
            OnServerMapLoaded
        );

        preparingConnections.Clear();
        pendingEntries.Clear();

        Debug.Log(
            "[InitialPlayerEntry] 서버 메시지 등록 완료",
            this
        );
    }

    /*
     * MapNetworkManager.OnStopServer()에서 호출합니다.
     */
    public void StopServer()
    {
        NetworkServer.UnregisterHandler
            <MapLoadedMessage>();

        preparingConnections.Clear();
        pendingEntries.Clear();
    }

    /*
     * MapNetworkManager.OnStartClient()에서 호출합니다.
     */
    public void StartClient()
    {
        NetworkClient.RegisterHandler<LoadMapMessage>(
            OnClientLoadMap
        );

        Debug.Log(
            "[InitialPlayerEntry] 클라이언트 메시지 등록 완료",
            this
        );
    }

    /*
     * MapNetworkManager.OnStopClient()에서 호출합니다.
     */
    public void StopClient()
    {
        NetworkClient.UnregisterHandler
            <LoadMapMessage>();
    }

    /// <summary>
    /// 로그인한 유저의 최초 플레이어 입장을 시작합니다.
    /// </summary>
    [Server]
    public void BeginEntry(
        NetworkConnectionToClient conn,
        UserRecord user)
    {
        if (conn == null ||
            user == null)
        {
            return;
        }

        if (conn.identity != null)
        {
            Debug.LogWarning(
                "[InitialPlayerEntry] 이미 플레이어가 " +
                "생성된 연결입니다.",
                conn.identity
            );

            return;
        }

        if (preparingConnections.Contains(conn) ||
            pendingEntries.ContainsKey(conn))
        {
            Debug.LogWarning(
                "[InitialPlayerEntry] 이미 최초 입장을 " +
                "처리하고 있습니다.",
                this
            );

            return;
        }

        preparingConnections.Add(conn);

        StartCoroutine(
            PrepareEntry(
                conn,
                user
            )
        );
    }

    /// <summary>
    /// 연결 종료 시 남아 있는 최초 입장 정보를 제거합니다.
    /// </summary>
    [Server]
    public void RemoveConnection(
        NetworkConnectionToClient conn)
    {
        CancelEntry(conn);
    }

    private IEnumerator PrepareEntry(
        NetworkConnectionToClient conn,
        UserRecord user)
    {
        /*
         * 로그인이 서버 맵 초기화보다 먼저 끝날 수 있으므로
         * 모든 서버 맵이 준비될 때까지 기다립니다.
         */
        yield return new WaitUntil(
            () => mapSceneManager.AreServerMapsLoaded
        );

        preparingConnections.Remove(conn);

        if (!IsConnectionActive(conn))
            yield break;

        ResolveEntryLocation(
            user,
            out MapId targetMapId,
            out string targetSpawnId
        );

        /*
         * 저장 위치가 잘못된 경우 기본 위치로 교체되지만,
         * 기본 위치 자체도 잘못 설정됐을 가능성을 검사합니다.
         */
        if (!IsValidLocation(
                targetMapId,
                targetSpawnId))
        {
            Debug.LogError(
                $"[InitialPlayerEntry] 기본 입장 위치가 " +
                $"유효하지 않습니다: " +
                $"{targetMapId} / {targetSpawnId}",
                this
            );

            CancelEntry(conn, true);
            yield break;
        }

        pendingEntries.Add(
            conn,
            new PendingPlayerEntry(
                user,
                targetMapId,
                targetSpawnId
            )
        );

        conn.Send(
            new LoadMapMessage
            {
                MapId = targetMapId
            }
        );

        Debug.Log(
            $"[InitialPlayerEntry] 최초 맵 로드 요청 / " +
            $"UserId: {user.UserId}, " +
            $"Map: {targetMapId}, " +
            $"Spawn: {targetSpawnId}",
            this
        );
    }

    /// <summary>
    /// DB에 저장된 마지막 위치를 검사하고
    /// 최초 입장 위치를 결정합니다.
    /// </summary>
    private void ResolveEntryLocation(
        UserRecord user,
        out MapId targetMapId,
        out string targetSpawnId)
    {
        targetMapId = defaultMapId;
        targetSpawnId = DefaultSpawnId;

        if (!Enum.TryParse(
                user.LastMapId,
                out MapId savedMapId) ||
            !IsValidLocation(
                savedMapId,
                user.LastSpawnId))
        {
            Debug.LogWarning(
                $"[InitialPlayerEntry] 저장 위치가 " +
                $"유효하지 않아 기본 위치를 사용합니다: " +
                $"{user.LastMapId} / {user.LastSpawnId}",
                this
            );

            return;
        }

        targetMapId = savedMapId;
        targetSpawnId = user.LastSpawnId;
    }

    private bool IsValidLocation(
        MapId mapId,
        string spawnId)
    {
        return mapSceneManager.TryGetLoadedScene(
                   mapId,
                   out _) &&
               mapSceneManager.FindSpawnPoint(
                   mapId,
                   spawnId
               ) != null;
    }

    /*
     * 서버의 최초 맵 로드 요청을 받은 클라이언트가
     * 자신의 입장 맵을 로드합니다.
     */
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
            Debug.LogError(
                $"[InitialPlayerEntry] 클라이언트 맵을 " +
                $"정상적으로 로드하지 못했습니다: {mapId}",
                this
            );

            yield break;
        }

        NetworkClient.Send(
            new MapLoadedMessage
            {
                MapId = mapId
            }
        );

        Debug.Log(
            $"[InitialPlayerEntry] 클라이언트 맵 로드 완료 / " +
            $"Map: {mapId}, Scene: {sceneName}",
            this
        );
    }

    /*
     * 클라이언트의 최초 맵 로드가 완료되면
     * 유저 인벤토리를 불러오고 플레이어를 생성합니다.
     */
    private async void OnServerMapLoaded(
        NetworkConnectionToClient conn,
        MapLoadedMessage message)
    {
        if (!pendingEntries.TryGetValue(
                conn,
                out PendingPlayerEntry pendingEntry))
        {
            Debug.LogWarning(
                "[InitialPlayerEntry] 요청하지 않은 " +
                "최초 맵 로드 응답입니다.",
                this
            );

            return;
        }

        if (pendingEntry.MapId != message.MapId)
        {
            Debug.LogWarning(
                $"[InitialPlayerEntry] 맵 정보가 " +
                $"일치하지 않습니다: " +
                $"Server={pendingEntry.MapId}, " +
                $"Client={message.MapId}",
                this
            );

            CancelEntry(conn, true);
            return;
        }

        if (conn.identity != null)
        {
            Debug.LogWarning(
                "[InitialPlayerEntry] 해당 연결에는 이미 " +
                "플레이어가 존재합니다.",
                conn.identity
            );

            CancelEntry(conn);
            return;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                pendingEntry.MapId,
                out Scene targetScene))
        {
            Debug.LogError(
                $"[InitialPlayerEntry] 서버 맵 씬을 " +
                $"찾지 못했습니다: {pendingEntry.MapId}",
                this
            );

            CancelEntry(conn, true);
            return;
        }

        Transform spawnPoint =
            mapSceneManager.FindSpawnPoint(
                pendingEntry.MapId,
                pendingEntry.SpawnId
            );

        if (spawnPoint == null)
        {
            Debug.LogError(
                $"[InitialPlayerEntry] 스폰 지점을 " +
                $"찾지 못했습니다: " +
                $"{pendingEntry.MapId} / " +
                $"{pendingEntry.SpawnId}",
                this
            );

            CancelEntry(conn, true);
            return;
        }

        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized ||
            databaseManager.PlayerInventoryRepository == null)
        {
            Debug.LogError(
                "[InitialPlayerEntry] 인벤토리 Repository가 " +
                "준비되지 않았습니다.",
                this
            );

            CancelEntry(conn, true);
            return;
        }

        try
        {
            List<PlayerInventoryRecord> inventoryRecords =
                await databaseManager
                    .PlayerInventoryRepository
                    .LoadByUserIdAsync(
                        pendingEntry.User.UserId
                    );

            /*
             * DB 조회 중 연결이 종료되었을 수 있으므로
             * 플레이어 생성 전에 다시 검사합니다.
             */
            if (!IsConnectionActive(conn))
            {
                CancelEntry(conn);
                return;
            }

            if (!TryCreatePlayer(
                    conn,
                    pendingEntry,
                    targetScene,
                    spawnPoint,
                    inventoryRecords))
            {
                CancelEntry(conn, true);
                return;
            }

            CancelEntry(conn);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[InitialPlayerEntry] 인벤토리 로드 실패 / " +
                $"UserId: {pendingEntry.User.UserId}\n" +
                $"{exception}",
                this
            );

            CancelEntry(conn, true);
        }
    }

    [Server]
    private bool TryCreatePlayer(
        NetworkConnectionToClient conn,
        PendingPlayerEntry pendingEntry,
        Scene targetScene,
        Transform spawnPoint,
        IReadOnlyCollection<PlayerInventoryRecord>
            inventoryRecords)
    {
        GameObject player =
            Instantiate(
                mapNetworkManager.playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

        PlayerMapController mapController =
            player.GetComponent<PlayerMapController>();

        PlayerAccountData accountData =
            player.GetComponent<PlayerAccountData>();

        PlayerInventory inventory =
            player.GetComponent<PlayerInventory>();

        if (mapController == null ||
            accountData == null ||
            inventory == null)
        {
            Debug.LogError(
                "[InitialPlayerEntry] 플레이어 생성에 필요한 " +
                "컴포넌트를 찾지 못했습니다.",
                player
            );

            Destroy(player);
            return false;
        }

        accountData.Initialize(
            pendingEntry.User.UserId,
            pendingEntry.User.Nickname
        );

        if (!inventory.ApplyLoadedConsumables(
                inventoryRecords))
        {
            Debug.LogError(
                $"[InitialPlayerEntry] 인벤토리 적용 실패 / " +
                $"UserId: {pendingEntry.User.UserId}",
                player
            );

            Destroy(player);
            return false;
        }

        mapController.SetCurrentMap(
            pendingEntry.MapId
        );

        /*
         * 플레이어를 목적지 맵의 독립 PhysicsScene2D에
         * 포함시키기 위해 서버 맵 씬으로 이동합니다.
         */
        SceneManager.MoveGameObjectToScene(
            player,
            targetScene
        );

        NetworkServer.AddPlayerForConnection(
            conn,
            player
        );

        mapNetworkManager.NotifyPlayerEnteredMap(
            pendingEntry.MapId
        );

        Debug.Log(
            $"[InitialPlayerEntry] 플레이어 최초 생성 완료 / " +
            $"UserId: {pendingEntry.User.UserId}, " +
            $"Map: {pendingEntry.MapId}, " +
            $"Spawn: {pendingEntry.SpawnId}, " +
            $"Position: {spawnPoint.position}",
            player
        );

        return true;
    }

    /// <summary>
    /// 진행 중인 최초 입장 정보를 제거하고,
    /// 필요하면 해당 연결을 종료합니다.
    /// </summary>
    [Server]
    private void CancelEntry(
        NetworkConnectionToClient conn,
        bool disconnect = false)
    {
        if (conn == null)
            return;

        preparingConnections.Remove(conn);
        pendingEntries.Remove(conn);

        if (disconnect &&
            IsConnectionActive(conn))
        {
            conn.Disconnect();
        }
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