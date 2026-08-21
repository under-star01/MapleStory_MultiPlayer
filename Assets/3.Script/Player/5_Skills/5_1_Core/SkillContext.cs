using System;
using UnityEngine;

public sealed class SkillContext
{
    public GameObject User { get; }
    public Transform Transform { get; }

    public PlayerMove Move { get; }
    public PlayerAnimController Anim { get; }

    public Vector2 InputDirection { get; }

    public Vector3 Position => Transform.position;
    public Vector2 FacingDirection => Move.FacingDirection;

    public SkillContext(
        GameObject user,
        PlayerMove move,
        PlayerAnimController anim,
        Vector2 inputDirection)
    {
        User = user
            ? user
            : throw new ArgumentNullException(nameof(user));

        Move = move
            ? move
            : throw new ArgumentNullException(nameof(move));

        Anim = anim
            ? anim
            : throw new ArgumentNullException(nameof(anim));

        Transform = user.transform;
        InputDirection = inputDirection;
    }
}