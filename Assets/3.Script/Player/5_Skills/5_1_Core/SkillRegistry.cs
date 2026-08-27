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

            // 새로운 스킬 추가 예시
            // {
            //     SkillId.SlashBlast,
            //     typeof(SlashBlastSkill)
            // }
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