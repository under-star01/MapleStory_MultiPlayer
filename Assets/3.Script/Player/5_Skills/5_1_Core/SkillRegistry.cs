using System;
using System.Collections.Generic;

public static class SkillRegistry
{
    private static readonly Dictionary<SkillId, Type>
    skillTypes = new()
    {
        {
            SkillId.Jump,
            typeof(JumpSkill)
        },
        {
            SkillId.BasicAttack,
            typeof(BasicAttackSkill)
        },
        {
            SkillId.AttackSkill1,
            typeof(AttackSkill1)
        },
        {
            SkillId.DashSkill,
            typeof(DashSkill)
        },
        {
            SkillId.PickupItem,
            typeof(PickupSkill)
        }
    };

    public static bool TryGetSkillType(
        SkillId skillId,
        out Type skillType)
    {
        return skillTypes.TryGetValue(
            skillId,
            out skillType
        );
    }
}