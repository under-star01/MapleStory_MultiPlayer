using Mirror;
using TMPro;
using UnityEngine;

public class PlayerAccountData : NetworkBehaviour
{
    private int userId;

    [SyncVar(hook = nameof(OnNicknameChanged))]
    private string nickname;

    [Header("World UI")]
    [SerializeField]
    private TMP_Text nicknameText;

    public int UserId => userId;
    public string Nickname => nickname;

    // 로그인한 유저 정보로 플레이어 데이터 초기화
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