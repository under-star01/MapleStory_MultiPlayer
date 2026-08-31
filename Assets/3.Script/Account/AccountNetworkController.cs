using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(InitialMapEntryController))]
public class AccountNetworkController : MonoBehaviour
{
    /*
     * 클라이언트 UI가 로그인 결과를 받습니다.
     */
    public event Action<LoginResult, string>
        LoginResponseReceived;

    /*
     * 클라이언트 UI가 회원가입 결과를 받습니다.
     */
    public event Action<RegisterResult>
        RegisterResponseReceived;

    /*
     * 연결별 로그인 유저 정보입니다.
     *
     * 이후 최초 맵 입장, 저장, 연결 종료 처리에서
     * 해당 연결의 UserRecord를 조회할 때 사용합니다.
     */
    private readonly Dictionary
        <NetworkConnectionToClient, UserRecord>
        connectedUsers = new();

    /*
     * 동일 계정이 다른 연결에서 다시 로그인하는 것을
     * 막기 위한 UserId 기준 연결 정보입니다.
     */
    private readonly Dictionary
        <int, NetworkConnectionToClient>
        loggedInConnections = new();

    private InitialMapEntryController
        initialMapEntryController;

    private void Awake()
    {
        initialMapEntryController =
            GetComponent<InitialMapEntryController>();
    }

    /*
     * MapNetworkManager.OnStartServer()에서 호출합니다.
     */
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

    /*
     * MapNetworkManager.OnStopServer()에서 호출합니다.
     */
    public void StopServer()
    {
        NetworkServer.UnregisterHandler
            <LoginRequestMessage>();

        NetworkServer.UnregisterHandler
            <RegisterRequestMessage>();

        connectedUsers.Clear();
        loggedInConnections.Clear();
    }

    /*
     * MapNetworkManager.OnStartClient()에서 호출합니다.
     */
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

    /*
     * MapNetworkManager.OnStopClient()에서 호출합니다.
     */
    public void StopClient()
    {
        NetworkClient.UnregisterHandler
            <LoginResponseMessage>();

        NetworkClient.UnregisterHandler
            <RegisterResponseMessage>();
    }

    /// <summary>
    /// 로그인 UI에서 호출합니다.
    /// </summary>
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

    /// <summary>
    /// 회원가입 UI에서 호출합니다.
    /// </summary>
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

    private async void OnServerLoginRequest(
        NetworkConnectionToClient conn,
        LoginRequestMessage message)
    {
        if (conn == null)
            return;

        /*
         * 같은 연결에서 이미 로그인을 완료한 경우입니다.
         */
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

        /*
         * 다른 연결에서 같은 UserId가
         * 이미 로그인했는지 검사합니다.
         */
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

            /*
             * 비정상 종료 등으로 남은 오래된 정보라면
             * 제거하고 로그인을 진행합니다.
             */
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

        initialMapEntryController.BeginInitialMapLoad(
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

    /*
     * 서버 시작 직후 DB 초기화가 끝나지 않았다면
     * 완료될 때까지 기다린 뒤 서비스를 반환합니다.
     */
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

    /// <summary>
    /// 서버에서 해당 연결의 로그인 유저를 조회합니다.
    /// </summary>
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

    /// <summary>
    /// 연결 종료 시 로그인 정보를 제거합니다.
    /// </summary>
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
            /*
             * 같은 UserId의 현재 연결일 때만 제거합니다.
             */
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