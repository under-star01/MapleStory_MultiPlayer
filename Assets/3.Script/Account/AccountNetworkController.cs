using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(InitialPlayerEntryController))]
public class AccountNetworkController : MonoBehaviour
{
    public event Action<LoginResult, string>
        LoginResponseReceived;

    public event Action<RegisterResult>
        RegisterResponseReceived;

    private readonly Dictionary
        <NetworkConnectionToClient, UserRecord>
        connectedUsers = new();

    private readonly Dictionary
        <int, NetworkConnectionToClient>
        loggedInConnections = new();

    private InitialPlayerEntryController
        initialPlayerEntryController;

    private void Awake()
    {
        initialPlayerEntryController =
            GetComponent<InitialPlayerEntryController>();
    }

    // 서버 계정 메시지 핸들러 등록
    public void StartServer()
    {
        NetworkServer.RegisterHandler
            <LoginRequestMessage>(
                OnServerLoginRequest,
                false
            );

        NetworkServer.RegisterHandler
            <RegisterRequestMessage>(
                OnServerRegisterRequest,
                false
            );

        connectedUsers.Clear();
        loggedInConnections.Clear();

        Debug.Log(
            "[Account] 서버 계정 메시지 등록 완료",
            this
        );
    }

    public void StopServer()
    {
        NetworkServer.UnregisterHandler
            <LoginRequestMessage>();

        NetworkServer.UnregisterHandler
            <RegisterRequestMessage>();

        connectedUsers.Clear();
        loggedInConnections.Clear();
    }

    // 클라이언트 계정 응답 메시지 핸들러 등록
    public void StartClient()
    {
        NetworkClient.RegisterHandler
            <LoginResponseMessage>(
                OnClientLoginResponse,
                false
            );

        NetworkClient.RegisterHandler
            <RegisterResponseMessage>(
                OnClientRegisterResponse,
                false
            );

        Debug.Log(
            "[Account] 클라이언트 계정 메시지 등록 완료",
            this
        );
    }

    public void StopClient()
    {
        NetworkClient.UnregisterHandler
            <LoginResponseMessage>();

        NetworkClient.UnregisterHandler
            <RegisterResponseMessage>();
    }

    public void RequestLogin(
        string loginId,
        string password)
    {
        if (!CanSendRequest())
            return;

        NetworkClient.Send(
            new LoginRequestMessage
            {
                LoginId = loginId,
                Password = password
            }
        );

        Debug.Log(
            "[Account] 로그인 요청 전송",
            this
        );
    }

    public void RequestRegister(
        string loginId,
        string password,
        string nickname)
    {
        if (!CanSendRequest())
            return;

        NetworkClient.Send(
            new RegisterRequestMessage
            {
                LoginId = loginId,
                Password = password,
                Nickname = nickname
            }
        );

        Debug.Log(
            "[Account] 회원가입 요청 전송",
            this
        );
    }

    private bool CanSendRequest()
    {
        if (NetworkClient.active &&
            NetworkClient.isConnected)
        {
            return true;
        }

        Debug.LogWarning(
            "[Account] 서버에 연결되어 있지 않아 " +
            "계정 요청을 보낼 수 없습니다.",
            this
        );

        return false;
    }

    // 로그인 요청 검증 및 최초 플레이어 입장 처리
    private async void OnServerLoginRequest(
        NetworkConnectionToClient conn,
        LoginRequestMessage message)
    {
        if (conn == null)
            return;

        if (connectedUsers.ContainsKey(conn))
        {
            SendLoginResponse(
                conn,
                LoginResult.AlreadyLoggedIn
            );

            return;
        }

        UserAccountService accountService =
            await GetAccountServiceAsync(conn);

        if (accountService == null)
        {
            if (IsConnectionActive(conn))
            {
                SendLoginResponse(
                    conn,
                    LoginResult.DatabaseError
                );
            }

            return;
        }

        LoginServiceResult loginResult =
            await accountService.LoginAsync(
                message.LoginId,
                message.Password
            );

        if (!IsConnectionActive(conn))
            return;

        if (loginResult.Result != LoginResult.Success ||
            loginResult.User == null)
        {
            SendLoginResponse(
                conn,
                loginResult.Result
            );

            Debug.LogWarning(
                $"[Account] 로그인 실패: " +
                $"{loginResult.Result}",
                this
            );

            return;
        }

        int userId =
            loginResult.User.UserId;

        // 동일 계정의 중복 로그인 방지
        if (loggedInConnections.TryGetValue(
                userId,
                out NetworkConnectionToClient
                    existingConnection))
        {
            if (IsConnectionActive(existingConnection))
            {
                SendLoginResponse(
                    conn,
                    LoginResult.AlreadyLoggedIn
                );

                Debug.LogWarning(
                    $"[Account] 중복 로그인 차단 / " +
                    $"UserId: {userId}",
                    this
                );

                return;
            }

            loggedInConnections.Remove(userId);
        }

        connectedUsers.Add(
            conn,
            loginResult.User
        );

        loggedInConnections.Add(
            userId,
            conn
        );

        initialPlayerEntryController.BeginEntry(
            conn,
            loginResult.User
        );

        SendLoginResponse(
            conn,
            LoginResult.Success,
            loginResult.User.Nickname
        );

        Debug.Log(
            $"[Account] 로그인 성공 / " +
            $"UserId: {userId}, " +
            $"Nickname: {loginResult.User.Nickname}, " +
            $"LastMap: {loginResult.User.LastMapId}, " +
            $"LastSpawn: {loginResult.User.LastSpawnId}",
            this
        );
    }

    // 회원가입 요청 처리
    private async void OnServerRegisterRequest(
        NetworkConnectionToClient conn,
        RegisterRequestMessage message)
    {
        if (conn == null)
            return;

        UserAccountService accountService =
            await GetAccountServiceAsync(conn);

        if (accountService == null)
        {
            if (IsConnectionActive(conn))
            {
                SendRegisterResponse(
                    conn,
                    RegisterResult.DatabaseError
                );
            }

            return;
        }

        RegisterResult result =
            await accountService.RegisterAsync(
                message.LoginId,
                message.Password,
                message.Nickname
            );

        if (!IsConnectionActive(conn))
            return;

        SendRegisterResponse(
            conn,
            result
        );

        Debug.Log(
            $"[Account] 회원가입 결과: {result}",
            this
        );
    }

    // DB 초기화 완료 후 계정 서비스 반환
    private async Task<UserAccountService>
        GetAccountServiceAsync(
            NetworkConnectionToClient conn)
    {
        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null)
        {
            Debug.LogError(
                "[Account] DatabaseManager를 " +
                "찾지 못했습니다.",
                this
            );

            return null;
        }

        while (!databaseManager
                    .IsInitializationComplete)
        {
            await Task.Yield();

            if (!IsConnectionActive(conn))
                return null;
        }

        if (!databaseManager.IsInitialized ||
            databaseManager.UserAccountService == null)
        {
            Debug.LogError(
                "[Account] DB가 정상적으로 " +
                "초기화되지 않았습니다.",
                this
            );

            return null;
        }

        return databaseManager.UserAccountService;
    }

    private void SendLoginResponse(
        NetworkConnectionToClient conn,
        LoginResult result,
        string nickname = null)
    {
        if (!IsConnectionActive(conn))
            return;

        conn.Send(
            new LoginResponseMessage
            {
                Result = result,
                Nickname = nickname
            }
        );
    }

    private void SendRegisterResponse(
        NetworkConnectionToClient conn,
        RegisterResult result)
    {
        if (!IsConnectionActive(conn))
            return;

        conn.Send(
            new RegisterResponseMessage
            {
                Result = result
            }
        );
    }

    private void OnClientLoginResponse(
        LoginResponseMessage message)
    {
        LoginResponseReceived?.Invoke(
            message.Result,
            message.Nickname
        );

        if (message.Result == LoginResult.Success)
        {
            Debug.Log(
                $"[Account] 로그인 성공 응답 / " +
                $"Nickname: {message.Nickname}",
                this
            );

            return;
        }

        Debug.LogWarning(
            $"[Account] 로그인 실패 응답: " +
            $"{message.Result}",
            this
        );
    }

    private void OnClientRegisterResponse(
        RegisterResponseMessage message)
    {
        RegisterResponseReceived?.Invoke(
            message.Result
        );

        if (message.Result == RegisterResult.Success)
        {
            Debug.Log(
                "[Account] 회원가입 성공 응답",
                this
            );

            return;
        }

        Debug.LogWarning(
            $"[Account] 회원가입 실패 응답: " +
            $"{message.Result}",
            this
        );
    }

    [Server]
    public bool TryGetUser(
        NetworkConnectionToClient conn,
        out UserRecord user)
    {
        return connectedUsers.TryGetValue(
            conn,
            out user
        );
    }

    // 마지막 맵과 스폰 위치를 DB에 저장
    [Server]
    public async void SaveLastLocation(
        NetworkConnectionToClient conn,
        MapId mapId,
        string spawnId)
    {
        if (conn == null)
            return;

        if (!connectedUsers.TryGetValue(
                conn,
                out UserRecord user))
        {
            Debug.LogWarning(
                "[Account] 마지막 위치를 저장할 " +
                "로그인 유저를 찾지 못했습니다.",
                this
            );

            return;
        }

        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized ||
            databaseManager.UserRepository == null)
        {
            Debug.LogError(
                "[Account] 마지막 위치를 저장할 " +
                "DB가 준비되지 않았습니다.",
                this
            );

            return;
        }

        try
        {
            await databaseManager
                .UserRepository
                .UpdateLastLocationAsync(
                    user.UserId,
                    mapId.ToString(),
                    spawnId
                );

            Debug.Log(
                $"[Account] 마지막 위치 저장 완료 / " +
                $"UserId: {user.UserId}, " +
                $"Map: {mapId}, " +
                $"Spawn: {spawnId}",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[Account] 마지막 위치 저장 실패 / " +
                $"UserId: {user.UserId}, " +
                $"Map: {mapId}, " +
                $"Spawn: {spawnId}\n" +
                $"{exception}",
                this
            );
        }
    }

    // 연결 종료 시 로그인 정보 제거
    [Server]
    public void RemoveConnection(
        NetworkConnectionToClient conn)
    {
        if (conn == null)
            return;

        if (connectedUsers.TryGetValue(
                conn,
                out UserRecord user))
        {
            if (loggedInConnections.TryGetValue(
                    user.UserId,
                    out NetworkConnectionToClient
                        loggedInConnection) &&
                loggedInConnection == conn)
            {
                loggedInConnections.Remove(
                    user.UserId
                );
            }
        }

        connectedUsers.Remove(conn);
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