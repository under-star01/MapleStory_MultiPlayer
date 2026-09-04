using Mirror;
using UnityEngine;

public class PlayerEffectController : NetworkBehaviour
{
    // AttackSkill1 이펙트 재생
    [Server]
    public void PlayAttackSkill1Effect(
        float direction)
    {
        bool flipX =
            direction > 0f;

        Vector3 effectPosition =
            transform.position;

        PlayEffectLocal(
            EffectId.AttackSkill1,
            effectPosition,
            flipX
        );

        RpcPlayEffect(
            EffectId.AttackSkill1,
            effectPosition,
            flipX
        );
    }

    // 좌우 추가 점프 이펙트 재생
    [Server]
    public void PlayDoubleJumpEffect(
        float direction)
    {
        bool flipX =
            direction > 0f;

        Vector3 effectPosition =
            transform.position;

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

    // 윗점프 이펙트 재생
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

    // 텔레포트 이펙트 재생
    [Server]
    public void PlayTeleportEffect(
        float direction)
    {
        bool flipX =
            direction > 0f;

        Vector3 position =
            transform.position;

        PlayEffectLocal(
            EffectId.DashSkill0,
            position,
            flipX
        );

        PlayEffectLocal(
            EffectId.DashSkill0,
            position,
            flipX
        );

        RpcPlayTeleportEffect(
            position,
            flipX
        );
    }

    [ClientRpc]
    private void RpcPlayTeleportEffect(
        Vector3 position,
        bool flipX)
    {
        if (isServer)
            return;

        PlayEffectLocal(
            EffectId.DashSkill0,
            position,
            flipX
        );

        PlayEffectLocal(
            EffectId.DashSkill1,
            position,
            flipX
        );
    }
}