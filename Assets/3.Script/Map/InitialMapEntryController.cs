using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MapNetworkManager))]
[RequireComponent(typeof(MapSceneManager))]
public class InitialMapEntryController : MonoBehaviour
{
    private class PendingInitialSpawn
    {
        public UserRecord User { get; }
        public MapId MapId { get; }
        public string SpawnId { get; }

        public PendingInitialSpawn(
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
    private MapId defaultMapId = MapId.MainTown;

    /*
     * 서버가 최초 입장을 요청한 연결과
     * 최종 결정된 맵·스폰 정보를 보관합니다.
     */
    private readonly Dictionary
        <NetworkConnectionToClient, PendingInitialSpawn>
        pendingInitialSpawns = new();

    /*
     * 서버 맵 로드를 기다리는 동안
     * 동일 연결이 입장을 중복 요청하지 못하게 합니다.
     */
    private readonly HashSet<NetworkConnectionToClient>
        preparingConnections = new();

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

        pendingInitialSpawns.Clear();
        preparingConnections.Clear();

        Debug.Log(
            "[InitialEntry] 서버 메시지 등록 완료",
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

        pendingInitialSpawns.Clear();
        preparingConnections.Clear();
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
            "[InitialEntry] 클라이언트 메시지 등록 완료",
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
    /// 로그인에 성공한 유저의 최초 맵 입장을 시작합니다.
    /// 서버에서만 호출합니다.
    /// </summary>
    [Server]
    public void BeginInitialMapLoad(
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
                "[InitialEntry] 이미 플레이어가 " +
                "생성된 연결입니다.",
                conn.identity
            );

            return;
        }

        if (preparingConnections.Contains(conn) ||
            pendingInitialSpawns.ContainsKey(conn))
        {
            Debug.LogWarning(
                "[InitialEntry] 이미 최초 입장을 " +
                "처리하고 있습니다.",
                this
            );

            return;
        }

        preparingConnections.Add(conn);

        StartCoroutine(
            PrepareInitialMapLoad(
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
        if (conn == null)
            return;

        preparingConnections.Remove(conn);
        pendingInitialSpawns.Remove(conn);
    }

    private IEnumerator PrepareInitialMapLoad(
        NetworkConnectionToClient conn,
        UserRecord user)
    {
        /*
         * 로그인 처리가 서버 맵 로드보다 먼저 끝날 수 있으므로,
         * 모든 서버 맵이 준비될 때까지 기다립니다.
         */
        yield return new WaitUntil(
            () => mapSceneManager.AreServerMapsLoaded
        );

        preparingConnections.Remove(conn);

        if (!IsConnectionActive(conn))
            yield break;

        ResolveInitialLocation(
            user,
            out MapId targetMapId,
            out string targetSpawnId
        );

        if (!IsValidLocation(
                targetMapId,
                targetSpawnId))
        {
            Debug.LogError(
                $"[InitialEntry] 기본 입장 위치도 " +
                $"유효하지 않습니다: " +
                $"{targetMapId} / {targetSpawnId}",
                this
            );

            conn.Disconnect();
            yield break;
        }

        pendingInitialSpawns.Add(
            conn,
            new PendingInitialSpawn(
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
            $"[InitialEntry] 최초 맵 로드 요청 / " +
            $"UserId: {user.UserId}, " +
            $"Map: {targetMapId}, " +
            $"Spawn: {targetSpawnId}",
            this
        );
    }

    /// <summary>
    /// DB에 저장된 위치를 검사하고
    /// 실제 최초 입장 위치를 결정합니다.
    /// </summary>
    private void ResolveInitialLocation(
        UserRecord user,
        out MapId targetMapId,
        out string targetSpawnId)
    {
        targetMapId = defaultMapId;
        targetSpawnId = DefaultSpawnId;

        bool hasValidMapId =
            System.Enum.TryParse(
                user.LastMapId,
                out MapId savedMapId
            );

        if (!hasValidMapId)
        {
            Debug.LogWarning(
                $"[InitialEntry] 저장된 MapId를 " +
                $"변환하지 못해 기본 위치를 사용합니다: " +
                $"{user.LastMapId}",
                this
            );

            return;
        }

        if (!IsValidLocation(
                savedMapId,
                user.LastSpawnId))
        {
            Debug.LogWarning(
                $"[InitialEntry] 저장된 위치를 " +
                $"찾지 못해 기본 위치를 사용합니다: " +
                $"{user.LastMapId} / " +
                $"{user.LastSpawnId}",
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
        if (!mapSceneManager.TryGetLoadedScene(
                mapId,
                out _))
        {
            return false;
        }

        return mapSceneManager.FindSpawnPoint(
                   mapId,
                   spawnId
               ) != null;
    }

    /*
     * 서버로부터 최초 맵 로드 요청을 받은 클라이언트가
     * 해당 맵을 로드합니다.
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
                $"[InitialEntry] 클라이언트 맵을 " +
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
            $"[InitialEntry] 클라이언트 맵 로드 완료: " +
            $"{mapId} / {sceneName}",
            this
        );
    }

    /*
     * 클라이언트가 최초 맵을 모두 로드한 뒤
     * 서버에 전달하는 완료 응답입니다.
     */
    private void OnServerMapLoaded(
        NetworkConnectionToClient conn,
        MapLoadedMessage message)
    {
        if (!pendingInitialSpawns.TryGetValue(
                conn,
                out PendingInitialSpawn pendingSpawn))
        {
            Debug.LogWarning(
                "[InitialEntry] 서버가 요청하지 않은 " +
                "최초 맵 로드 응답입니다.",
                this
            );

            return;
        }

        if (pendingSpawn.MapId != message.MapId)
        {
            Debug.LogWarning(
                $"[InitialEntry] 맵 정보가 일치하지 않습니다: " +
                $"Server={pendingSpawn.MapId}, " +
                $"Client={message.MapId}",
                this
            );

            pendingInitialSpawns.Remove(conn);
            conn.Disconnect();
            return;
        }

        if (conn.identity != null)
        {
            Debug.LogWarning(
                "[InitialEntry] 해당 연결에는 이미 " +
                "플레이어가 존재합니다.",
                conn.identity
            );

            pendingInitialSpawns.Remove(conn);
            return;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                pendingSpawn.MapId,
                out Scene targetScene))
        {
            Debug.LogError(
                $"[InitialEntry] 서버 맵 씬을 " +
                $"찾지 못했습니다: {pendingSpawn.MapId}",
                this
            );

            pendingInitialSpawns.Remove(conn);
            return;
        }

        Transform spawnPoint =
            mapSceneManager.FindSpawnPoint(
                pendingSpawn.MapId,
                pendingSpawn.SpawnId
            );

        if (spawnPoint == null)
        {
            Debug.LogError(
                $"[InitialEntry] 스폰 지점을 " +
                $"찾지 못했습니다: " +
                $"{pendingSpawn.MapId} / " +
                $"{pendingSpawn.SpawnId}",
                this
            );

            pendingInitialSpawns.Remove(conn);
            return;
        }

        CreatePlayer(
            conn,
            pendingSpawn,
            targetScene,
            spawnPoint
        );

        pendingInitialSpawns.Remove(conn);
    }

    [Server]
    private void CreatePlayer(
        NetworkConnectionToClient conn,
        PendingInitialSpawn pendingSpawn,
        Scene targetScene,
        Transform spawnPoint)
    {
        GameObject player =
            Instantiate(
                mapNetworkManager.playerPrefab,
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
            pendingSpawn.MapId
        );

        /*
         * 플레이어를 목적지 맵의 독립 PhysicsScene2D에
         * 포함시키기 위해 서버 씬으로 이동합니다.
         */
        SceneManager.MoveGameObjectToScene(
            player,
            targetScene
        );

        NetworkServer.AddPlayerForConnection(
            conn,
            player
        );

        /*
         * 맵 인원 증가와 몬스터 활성화 처리는
         * 기존 MapNetworkManager에 알립니다.
         *
         * 다음 단계에서 이 메서드를 public으로 변경합니다.
         */
        mapNetworkManager.NotifyPlayerEnteredMap(
            pendingSpawn.MapId
        );

        Debug.Log(
            $"[InitialEntry] 플레이어 최초 생성 완료 / " +
            $"UserId: {pendingSpawn.User.UserId}, " +
            $"Map: {pendingSpawn.MapId}, " +
            $"Spawn: {pendingSpawn.SpawnId}, " +
            $"Position: {spawnPoint.position}",
            player
        );
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