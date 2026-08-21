using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerAnimController))]
public class PlayerSkillController : MonoBehaviour
{
    [Header("Default Skills")]
    [SerializeField]
    private List<SkillId> defaultSkillIds = new()
    {
        SkillId.Jump,
        SkillId.BasicAttack
    };

    private readonly Dictionary<SkillId, PlayerSkillBase>
        learnedSkills = new();

    private PlayerMove playerMove;
    private PlayerAnimController playerAnim;

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerAnim = GetComponent<PlayerAnimController>();

        LearnDefaultSkills();
    }

    public bool TryExecute(
        IPlayerSkill skill,
        Vector2 inputDirection = default)
    {
        if (IsNull(skill))
            return false;

        SkillContext context = new SkillContext(
            gameObject,
            playerMove,
            playerAnim,
            inputDirection
        );

        if (!skill.CanExecute(context))
            return false;

        skill.Execute(context);
        return true;
    }

    /// <summary>
    /// 스킬 ID에 해당하는 스킬을 획득합니다.
    /// 이미 보유 중이면 기존 스킬을 반환합니다.
    /// </summary>
    public bool LearnSkill(
        SkillId skillId,
        out PlayerSkillBase learnedSkill)
    {
        // 이미 보유 중이면 기존 인스턴스를 반환합니다.
        if (learnedSkills.TryGetValue(
                skillId,
                out learnedSkill))
        {
            return true;
        }

        if (!SkillRegistry.TryGetSkillType(
                skillId,
                out Type skillType))
        {
            learnedSkill = null;
            return false;
        }

        if (!typeof(PlayerSkillBase)
            .IsAssignableFrom(skillType))
        {
            Debug.LogError(
                $"{skillType.Name}은 " +
                $"{nameof(PlayerSkillBase)}를 상속하지 않습니다."
            );

            learnedSkill = null;
            return false;
        }

        // 플레이어에게 이미 붙어 있다면 재사용합니다.
        PlayerSkillBase existingSkill =
            GetComponent(skillType) as PlayerSkillBase;

        if (existingSkill != null)
        {
            learnedSkill = existingSkill;
        }
        else
        {
            learnedSkill =
                gameObject.AddComponent(skillType)
                as PlayerSkillBase;
        }

        if (learnedSkill == null)
            return false;

        learnedSkills.Add(
            skillId,
            learnedSkill
        );

        return true;
    }

    public bool HasSkill(SkillId skillId)
    {
        return learnedSkills.ContainsKey(skillId);
    }

    public bool TryGetSkill(
        SkillId skillId,
        out PlayerSkillBase skill)
    {
        return learnedSkills.TryGetValue(
            skillId,
            out skill
        );
    }

    private void LearnDefaultSkills()
    {
        foreach (SkillId skillId in defaultSkillIds)
        {
            if (skillId == SkillId.None)
                continue;

            if (!LearnSkill(
                    skillId,
                    out _))
            {
                Debug.LogWarning(
                    $"기본 스킬 획득 실패: {skillId}",
                    this
                );
            }
        }
    }

    private bool IsNull(IPlayerSkill skill)
    {
        if (skill == null)
            return true;

        if (skill is UnityEngine.Object unityObject &&
            unityObject == null)
        {
            return true;
        }

        return false;
    }
}