using UnityEngine;

public sealed class SkillContext
{
    public GameObject User { get; }
    public Transform Transform { get; }

    public PlayerMove Move { get; }
    public PlayerAnimController Anim { get; }
    public PlayerSkillController SkillController { get; }
    public PlayerEffectController Effect { get; }
    public PlayerInventory Inventory { get; }

    public Vector2 InputDirection { get; }

    public Vector3 Position =>
        Transform.position;

    public Vector2 FacingDirection =>
        Move.FacingDirection;

    public SkillContext(
        GameObject user,
        PlayerMove move,
        PlayerAnimController anim,
        PlayerEffectController effect,
        PlayerSkillController skillController,
        PlayerInventory inventory,
        Vector2 inputDirection)
    {
        User = user;
        Transform = user.transform;

        Move = move;
        Anim = anim;
        Effect = effect;
        SkillController = skillController;
        Inventory = inventory;
        InputDirection = inputDirection;
    }
}