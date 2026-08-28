using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
public class PlayerEffectController : NetworkBehaviour
{
    private PlayerMove playerMove;

    /*
     * 공격 시작 순간에 확정된
     * 공격 이펙트 방향입니다.
     */
    private bool attackEffectFlipX;

    private void Awake()
    {
        playerMove =
            GetComponent<PlayerMove>();
    }

    /// <summary>
    /// 서버에서 공격 시작 순간의 방향을 저장하고
    /// 다른 클라이언트에도 전달합니다.
    /// </summary>
    [Server]
    public void PrepareAttackEffectDirection()
    {
        attackEffectFlipX =
            playerMove.FacingDirection.x > 0f;

        RpcSetAttackEffectDirection(
            attackEffectFlipX
        );
    }

    [ClientRpc]
    private void RpcSetAttackEffectDirection(
        bool flipX)
    {
        /*
         * 호스트는 서버에서 이미 값을 설정했으므로
         * 중복 설정을 생략합니다.
         */
        if (isServer)
            return;

        attackEffectFlipX =
            flipX;
    }

    /// <summary>
    /// AttackSkill1 애니메이션 클립의
    /// Animation Event에서 호출합니다.
    /// </summary>
    public void OnAttackSkill1EffectFrame()
    {
        EffectPool.Instance?.Play(
            EffectId.AttackSkill1,
            transform.position,
            attackEffectFlipX
        );
    }

    /// <summary>
    /// 서버에서 좌우 추가 점프 이펙트를 요청합니다.
    /// </summary>
    [Server]
    public void PlayDoubleJumpEffect(
        float direction)
    {
        bool flipX =
            direction > 0f;

        Vector3 effectPosition =
            transform.position;

        /*
         * 호스트 화면에서는 서버가 직접 재생합니다.
         */
        PlayEffectLocal(
            EffectId.DoubleJump,
            effectPosition,
            flipX
        );

        RpcPlayEffect(
            EffectId.DoubleJump,
            effectPosition,
            flipX
        );
    }

    /// <summary>
    /// 서버에서 윗점프 이펙트를 요청합니다.
    /// </summary>
    [Server]
    public void PlayUpJumpEffect()
    {
        Vector3 effectPosition =
            transform.position;

        PlayEffectLocal(
            EffectId.UpJump,
            effectPosition,
            false
        );

        RpcPlayEffect(
            EffectId.UpJump,
            effectPosition,
            false
        );
    }

    [ClientRpc]
    private void RpcPlayEffect(
        EffectId effectId,
        Vector3 position,
        bool flipX)
    {
        /*
         * 호스트는 서버 메서드에서 이미 재생했습니다.
         */
        if (isServer)
            return;

        PlayEffectLocal(
            effectId,
            position,
            flipX
        );
    }

    private void PlayEffectLocal(
        EffectId effectId,
        Vector3 position,
        bool flipX)
    {
        EffectPool.Instance?.Play(
            effectId,
            position,
            flipX
        );
    }
}