using Mirror;
using TMPro;
using UnityEngine;

public class AccountUIController : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private AccountNetworkController
        accountNetworkController;

    [Header("UI State")]
    [SerializeField]
    private GameObject loginUI;

    [SerializeField]
    private GameObject registerUI;

    [Header("Common Input")]
    [SerializeField]
    private TMP_InputField loginIdInput;

    [SerializeField]
    private TMP_InputField passwordInput;

    [Header("Register Input")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [Header("Result")]
    [SerializeField]
    private TMP_Text errorLogText;

    private bool isRequestPending;

    private void Awake()
    {
        ShowLoginUI();
    }

    private void OnEnable()
    {
        if (accountNetworkController == null)
        {
            Debug.LogError(
                $"{nameof(AccountNetworkController)}가 " +
                "연결되지 않았습니다.",
                this
            );

            return;
        }

        accountNetworkController
            .LoginResponseReceived +=
            OnLoginResponseReceived;

        accountNetworkController
            .RegisterResponseReceived +=
            OnRegisterResponseReceived;
    }

    private void OnDisable()
    {
        if (accountNetworkController == null)
            return;

        accountNetworkController
            .LoginResponseReceived -=
            OnLoginResponseReceived;

        accountNetworkController
            .RegisterResponseReceived -=
            OnRegisterResponseReceived;
    }

    /// <summary>
    /// 로그인 버튼에서 호출합니다.
    /// </summary>
    public void OnClickLogin()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.MouseClick
        );

        if (isRequestPending)
            return;

        if (!IsConnectedToServer())
        {
            ShowMessage(
                "서버에 연결되어 있지 않습니다."
            );

            return;
        }

        ClearMessage();
        SetRequestPending(true);

        accountNetworkController.RequestLogin(
            loginIdInput.text,
            passwordInput.text
        );
    }

    /// <summary>
    /// 회원가입 화면을 엽니다.
    /// </summary>
    public void OnClickOpenRegister()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIOpen
        );

        if (isRequestPending)
            return;

        loginUI.SetActive(false);
        registerUI.SetActive(true);

        nicknameInput.text =
            string.Empty;

        ClearMessage();
        nicknameInput.ActivateInputField();
    }

    /// <summary>
    /// 회원가입 버튼에서 호출합니다.
    /// </summary>
    public void OnClickRegister()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.MouseClick
        );

        if (isRequestPending)
            return;

        if (!IsConnectedToServer())
        {
            ShowMessage(
                "서버에 연결되어 있지 않습니다."
            );

            return;
        }

        ClearMessage();
        SetRequestPending(true);

        accountNetworkController.RequestRegister(
            loginIdInput.text,
            passwordInput.text,
            nicknameInput.text
        );
    }

    /// <summary>
    /// 회원가입 화면에서 로그인 화면으로 돌아갑니다.
    /// </summary>
    public void OnClickBackToLogin()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIClose
        );

        if (isRequestPending)
            return;

        ShowLoginUI();
    }

    /// <summary>
    /// 끝내기 버튼에서 호출합니다.
    /// </summary>
    public void OnClickExit()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIClose
        );

#if UNITY_EDITOR
        UnityEditor.EditorApplication
            .isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnLoginResponseReceived(
        LoginResult result,
        string nickname)
    {
        SetRequestPending(false);

        switch (result)
        {
            case LoginResult.Success:
                HandleLoginSuccess(
                    nickname
                );
                break;

            case LoginResult.InvalidInput:
            case LoginResult.UserNotFound:
            case LoginResult.IncorrectPassword:
                ShowMessage(
                    "아이디 또는 비밀번호가 틀렸습니다."
                );
                break;

            case LoginResult.AlreadyLoggedIn:
                ShowMessage(
                    "이미 접속 중인 계정입니다."
                );
                break;

            case LoginResult.DatabaseError:
                ShowMessage(
                    "서버 오류가 발생했습니다. " +
                    "잠시 후 다시 시도해주세요."
                );
                break;

            default:
                ShowMessage(
                    "로그인 처리 중 오류가 발생했습니다."
                );
                break;
        }
    }

    private void OnRegisterResponseReceived(
        RegisterResult result)
    {
        SetRequestPending(false);

        switch (result)
        {
            case RegisterResult.Success:
                HandleRegisterSuccess();
                break;

            case RegisterResult.InvalidLoginId:
                ShowMessage(
                    "아이디는 4~30자로 입력해주세요."
                );
                break;

            case RegisterResult.InvalidPassword:
                ShowMessage(
                    "비밀번호는 8~50자로 입력해주세요."
                );
                break;

            case RegisterResult.InvalidNickname:
                ShowMessage(
                    "닉네임은 2~20자로 입력해주세요."
                );
                break;

            case RegisterResult.LoginIdAlreadyExists:
                ShowMessage(
                    "이미 사용 중인 아이디입니다."
                );
                break;

            case RegisterResult.NicknameAlreadyExists:
                ShowMessage(
                    "이미 사용 중인 닉네임입니다."
                );
                break;

            case RegisterResult.DatabaseError:
                ShowMessage(
                    "서버 오류가 발생했습니다. " +
                    "잠시 후 다시 시도해주세요."
                );
                break;

            default:
                ShowMessage(
                    "회원가입 처리 중 오류가 발생했습니다."
                );
                break;
        }
    }

    private void HandleLoginSuccess(
        string nickname)
    {
        ClearMessage();

        passwordInput.text =
            string.Empty;

        Debug.Log(
            $"[AccountUI] 로그인 완료: {nickname}",
            this
        );

        /*
         * LocalPlayerUIRoot는 플레이어 Authority에 따라
         * 기존 시스템이 별도로 처리합니다.
         */
        gameObject.SetActive(false);
    }

    private void HandleRegisterSuccess()
    {
        /*
         * 재로그인이 편하도록 아이디는 유지합니다.
         * 비밀번호와 닉네임만 초기화합니다.
         */
        passwordInput.text =
            string.Empty;

        nicknameInput.text =
            string.Empty;

        loginUI.SetActive(true);
        registerUI.SetActive(false);

        ShowMessage(
            "회원가입이 완료되었습니다."
        );

        passwordInput.ActivateInputField();
    }

    private void ShowLoginUI()
    {
        loginUI.SetActive(true);
        registerUI.SetActive(false);

        nicknameInput.text =
            string.Empty;

        ClearMessage();

        if (loginIdInput != null)
        {
            loginIdInput.ActivateInputField();
        }
    }

    private void SetRequestPending(
        bool isPending)
    {
        isRequestPending = isPending;
    }

    private void ShowMessage(
        string message)
    {
        if (errorLogText == null)
            return;

        errorLogText.text = message;
        errorLogText.gameObject.SetActive(true);
    }

    private void ClearMessage()
    {
        if (errorLogText == null)
            return;

        errorLogText.text =
            string.Empty;

        errorLogText.gameObject.SetActive(false);
    }

    private static bool IsConnectedToServer()
    {
        return NetworkClient.active &&
               NetworkClient.isConnected;
    }
}