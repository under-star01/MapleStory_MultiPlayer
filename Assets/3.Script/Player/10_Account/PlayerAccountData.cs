using Mirror;
using TMPro;
using UnityEngine;

public class PlayerAccountData : NetworkBehaviour
{
    /*
     * DB 조회와 저장에 사용하는 서버 전용 식별값입니다.
     *
     * SyncVar가 아니므로 클라이언트에는 전달되지 않습니다.
     */
    private int userId;

    [SyncVar(hook = nameof(OnNicknameChanged))]
    private string nickname;

    [Header("World UI")]
    [SerializeField]
    private TMP_Text nicknameText;

    /*
     * 서버 시스템에서만 사용하는 유저 식별값입니다.
     * 클라이언트에서는 의미 있는 값을 보장하지 않습니다.
     */
    public int UserId => userId;

    public string Nickname => nickname;

    /// <summary>
    /// 로그인한 유저 정보로 플레이어를 초기화합니다.
    /// 플레이어를 NetworkServer에 등록하기 전에 호출합니다.
    /// </summary>
    [Server]
    public void Initialize(
        int userId,
        string nickname)
    {
        this.userId = userId;
        this.nickname = nickname;

        RefreshNicknameText(
            nickname
        );
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        /*
         * 플레이어가 생성될 때 이미 SyncVar 값이
         * 적용된 경우에도 이름을 확실히 표시합니다.
         */
        RefreshNicknameText(
            nickname
        );
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        AudioManager.Instance?.PlayBgm(
            BgmSoundId.InGame
        );
    }

    private void OnNicknameChanged(
        string oldNickname,
        string newNickname)
    {
        RefreshNicknameText(
            newNickname
        );
    }

    private void RefreshNicknameText(
        string value)
    {
        if (nicknameText == null)
            return;

        nicknameText.text =
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value;
    }
}